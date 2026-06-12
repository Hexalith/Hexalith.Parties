#Requires -Version 7
<#
.SYNOPSIS
Publishes the Hexalith.Parties Kubernetes topology.

.EXIT CODES
0 Success.
1 General bounded operational failure.
2 ConfirmContext mismatch or missing current context.
3 Required CLI missing.
4 Expected Aspirate service folder missing or unexpected generated service folder detected.
5 MinVer resolution empty, stale, or malformed.
6 Zot Docker config/auth failure or credential-helper indirection.
7 Reserved for teardown residual state.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ConfirmContext,

    [switch] $SkipDaprInit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExitGeneral = 1
$ExitContext = 2
$ExitCliMissing = 3
$ExitFolderDrift = 4
$ExitMinVer = 5
$ExitZotAuth = 6
$ExitResidual = 7

$Script:Step = 0
$Script:Started = [System.Diagnostics.Stopwatch]::StartNew()
$K8sRoot = $PSScriptRoot
$DeployRoot = Split-Path -Parent $K8sRoot
$RepoRoot = Split-Path -Parent $DeployRoot
$DaprRoot = Join-Path $DeployRoot 'dapr'
$AppHostRoot = Join-Path $RepoRoot 'src/Hexalith.Parties.AppHost'
$AppHostProject = 'Hexalith.Parties.AppHost.csproj'
$Namespace = 'hexalith-parties'
$Registry = 'registry.hexalith.com'
$TacheIssuer = 'http://auth.tache.ai:8080/realms/tache'
$TachePublicIssuer = 'https://auth.tache.ai/realms/tache'
$TacheIssuerHost = 'auth.tache.ai'
$KeycloakNamespace = 'keycloak'
$KeycloakServiceName = 'keycloak'
$UiCredentialsSecretName = 'hexalith-tache-ui-credentials'
$PartiesUiOidcSecretName = 'hexalith-parties-ui-oidc-client'
$PartiesUiOidcSecretKey = 'client-secret'
$ZotSecretName = 'zot-pull-secret'
$Script:KeycloakClusterIp = $null

$GeneratedServiceFolders = @(
    'eventstore',
    'eventstore-admin',
    'eventstore-admin-ui',
    'sample',
    'sample-blazor-ui',
    'parties',
    'parties-mcp',
    'parties-ui',
    'tenants',
    'memories'
)

$PreservedEntries = @(
    'redis',
    'falkordb',
    'ingress.yaml',
    'kustomization.yaml',
    'namespace.yaml',
    'README.md',
    'publish.ps1',
    'teardown.ps1',
    '_lib'
)

$KnownAspiratePlaceholders = @(
    'aspirate-state.json',
    'aspirate-manifest.json',
    'dashboard.yaml',
    'dapr',
    'components',
    'secrets'
)

$DaprPatchMap = [ordered]@{
    'eventstore' = 'accesscontrol'
    'eventstore-admin' = 'accesscontrol-eventstore-admin'
    'sample' = 'accesscontrol-sample'
    'parties' = 'accesscontrol-parties'
    'tenants' = 'accesscontrol-tenants'
    'memories' = 'accesscontrol-memories'
}

$DaprClientOnlyTargets = @('eventstore-admin-ui', 'sample-blazor-ui')
$ForbiddenDaprTargets = @('parties-mcp', 'parties-ui', 'redis', 'falkordb')
$LegacyLocalKeycloakResources = @(
    'deployment/keycloak',
    'service/keycloak',
    'configmap/keycloak-realm',
    'secret/hexalith-keycloak-admin'
)

. (Join-Path $K8sRoot '_lib/Confirm-KubeContext.ps1')

function Fail([int] $Code, [string] $Message) {
    Write-Host "[publish] ERROR: $Message"
    exit $Code
}

function Write-Step([string] $Message) {
    Write-Host "[publish] Step $($Script:Step): $Message"
    $Script:Step++
}

function Require-Command([string] $Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Fail $ExitCliMissing "required CLI '$Name' was not found on PATH"
    }
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [int] $FailureCode = $ExitGeneral,

        [string] $FailureMessage = 'command failed',

        [string] $WorkingDirectory = $RepoRoot
    )

    Push-Location $WorkingDirectory
    try {
        $output = & $FilePath @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0) {
        $bounded = ($output | Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_) } | Select-Object -Last 30) -join [Environment]::NewLine
        Fail $FailureCode "$FailureMessage (exit $exitCode). $bounded"
    }

    return $output
}

function Invoke-KubectlInput {
    param(
        [Parameter(Mandatory = $true)]
        [string] $InputText,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [int] $FailureCode = $ExitGeneral,

        [string] $FailureMessage = 'kubectl command failed'
    )

    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = 'kubectl'
    foreach ($argument in $Arguments) {
        [void] $start.ArgumentList.Add($argument)
    }

    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.UseShellExecute = $false
    $process = [System.Diagnostics.Process]::Start($start)
    if ($null -eq $process) {
        Fail $FailureCode $FailureMessage
    }

    $process.StandardInput.Write($InputText)
    $process.StandardInput.Close()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        $bounded = (($stderr + $stdout) -split "`r?`n" | Where-Object { $_ } | Select-Object -First 20) -join [Environment]::NewLine
        Fail $FailureCode "$FailureMessage (exit $($process.ExitCode)). $bounded"
    }

    return $stdout
}

function Resolve-MinVerTag {
    Require-Command 'dotnet'

    Push-Location $AppHostRoot
    try {
        $raw = (& dotnet msbuild $AppHostProject -t:Build -p:Configuration=Release -getProperty:Version 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0) {
            Fail $ExitMinVer 'MinVer resolution command failed'
        }
    }
    finally {
        Pop-Location
    }

    $normalized = $raw
    if ($normalized.StartsWith('v', [System.StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(1)
    }

    if ([string]::IsNullOrWhiteSpace($normalized) -or $normalized -eq '1.0.0') {
        Fail $ExitMinVer "MinVer returned stale or empty value '$raw'"
    }

    if ($normalized -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?(?:\+[A-Za-z0-9.-]+)?$') {
        Fail $ExitMinVer "MinVer value '$raw' is not a SemVer image tag"
    }

    if ($normalized.Contains('+dirty')) {
        Write-Warning "[publish] MinVer tag contains +dirty; proceeding by operator request"
    }

    Write-Host "[publish] MinVer command: dotnet msbuild $AppHostProject -t:Build -p:Configuration=Release -getProperty:Version"
    Write-Host "[publish] MinVer raw: $raw"
    Write-Host "[publish] MinVer normalized image tag: $normalized"
    Write-Host "[publish] Image proof: $Registry/parties:$normalized and $Registry/parties-ui:$normalized"
    return $normalized
}

function Get-TopLevelEntryNames {
    return @(Get-ChildItem -LiteralPath $K8sRoot -Force | Select-Object -ExpandProperty Name)
}

function Assert-KnownTopLevelEntries([string] $Phase) {
    $known = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($entry in $PreservedEntries + $GeneratedServiceFolders + $KnownAspiratePlaceholders) {
        [void] $known.Add($entry)
    }

    $unknown = Get-TopLevelEntryNames | Where-Object { -not $known.Contains($_) }
    if ($unknown) {
        $names = ($unknown | Sort-Object) -join ', '
        Fail $ExitFolderDrift "unknown deploy/k8s top-level entries refused during ${Phase}: $names"
    }
}

function Clean-DeploymentTree {
    Assert-KnownTopLevelEntries 'cleanup'

    $removed = 0
    foreach ($folder in $GeneratedServiceFolders) {
        $path = Join-Path $K8sRoot $folder
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
            $removed++
        }
    }

    Write-Host "[publish] cleanup removed generated folders: $removed"
}

function Invoke-AspirateGenerate([string] $ImageTag) {
    Require-Command 'dotnet'

    $previousRollForward = $env:DOTNET_ROLL_FORWARD
    $previousContainerImageTag = $env:ContainerImageTag
    $previousContainerImageTags = $env:ContainerImageTags
    $previousPublishTarget = $env:PUBLISH_TARGET
    try {
        if (-not [string]::IsNullOrWhiteSpace($env:PUBLISH_TARGET)) {
            Fail $ExitGeneral 'PUBLISH_TARGET must be unset before aspirate generate'
        }

        $env:DOTNET_ROLL_FORWARD = 'Major'
        $env:ContainerImageTag = $ImageTag
        Remove-Item Env:ContainerImageTags -ErrorAction SilentlyContinue
        $arguments = @(
            'tool', 'run', '--allow-roll-forward', 'aspirate', '--', 'generate',
            '--project-path', $AppHostProject,
            '--container-image-tag', $ImageTag,
            '--container-registry', $Registry,
            '--non-interactive',
            '--disable-state',
            '--disable-secrets',
            '--include-dashboard', 'false',
            '--image-pull-policy', 'IfNotPresent',
            '--output-path', '../../deploy/k8s'
        )

        Write-Host "[publish] aspirate command: dotnet $($arguments -join ' ')"
        [void](Invoke-Checked 'dotnet' $arguments $ExitGeneral 'aspirate generate failed' $AppHostRoot)
    }
    finally {
        $env:DOTNET_ROLL_FORWARD = $previousRollForward
        $env:ContainerImageTag = $previousContainerImageTag
        if ($null -eq $previousContainerImageTags) {
            Remove-Item Env:ContainerImageTags -ErrorAction SilentlyContinue
        }
        else {
            $env:ContainerImageTags = $previousContainerImageTags
        }

        $env:PUBLISH_TARGET = $previousPublishTarget
    }
}

function Remove-AspiratePlaceholders {
    $removed = @()
    foreach ($name in $KnownAspiratePlaceholders) {
        $path = Join-Path $K8sRoot $name
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
            $removed += $name
        }
    }

    Write-Host "[publish] stripped aspirate placeholders: $($removed.Count)"

    $kustomizationPath = Join-Path $K8sRoot 'kustomization.yaml'
    @"
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
namespace: $Namespace
resources:
  - namespace.yaml
  - eventstore
  - eventstore-admin
  - eventstore-admin-ui
  - sample
  - sample-blazor-ui
  - memories
  - parties
  - parties-mcp
  - parties-ui
  - tenants
  - redis
  - falkordb
  - ingress.yaml
"@ | Set-Content -LiteralPath $kustomizationPath -Encoding UTF8
    Write-Host '[publish] restored canonical kustomization.yaml'
}

function Assert-ServiceFolders {
    $actual = Get-ChildItem -LiteralPath $K8sRoot -Directory | Select-Object -ExpandProperty Name
    $generated = $actual | Where-Object { $GeneratedServiceFolders -contains $_ } | Sort-Object
    $missing = $GeneratedServiceFolders | Where-Object { $generated -notcontains $_ }
    Assert-KnownTopLevelEntries 'post-generation validation'

    if ($missing) {
        Fail $ExitFolderDrift "service folder mismatch. missing=[$($missing -join ', ')]"
    }

    Write-Host "[publish] expected service folders present: $($GeneratedServiceFolders -join ', ')"
}

function Normalize-GeneratedKustomizations {
    foreach ($folder in $GeneratedServiceFolders) {
        $path = Join-Path $K8sRoot "$folder/kustomization.yaml"
        if (-not (Test-Path -LiteralPath $path)) {
            continue
        }

        $content = Get-Content -Raw -LiteralPath $path
        $normalized = $content -replace "(\r?\n\s*)+\z", [Environment]::NewLine
        Set-Content -LiteralPath $path -Value $normalized -NoNewline
    }

    Write-Host "[publish] normalized generated kustomization.yaml files"
}

function Read-DeploymentFile([string] $Name) {
    $path = Join-Path $K8sRoot "$Name/deployment.yaml"
    if (-not (Test-Path -LiteralPath $path)) {
        Fail $ExitGeneral "missing expected Deployment file for $Name"
    }

    $content = Get-Content -LiteralPath $path -Raw
    if ($content -notmatch "(?m)^kind:\s*Deployment\s*$" -or $content -notmatch "(?m)^\s*name:\s*$([regex]::Escape($Name))\s*$") {
        Fail $ExitGeneral "deployment file for $Name is structurally unparseable"
    }

    return @($path, $content)
}

function Add-DaprAppPortAfterAppId([string] $Content, [string] $AppId) {
    if ($Content -match "(?m)^\s*dapr\.io/app-port:") {
        return $Content
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in ($Content -split "`r?`n")) {
        $lines.Add($line)
        if ($line -match "^(\s*)dapr\.io/app-id:\s*['""]?$([regex]::Escape($AppId))['""]?\s*$") {
            $lines.Add("$($Matches[1])dapr.io/app-port: '8080'")
        }
    }

    return ($lines -join [Environment]::NewLine)
}

function Get-TemplateAnnotationBlock([string] $Content) {
    $match = [regex]::Match($Content, '(?ms)^\s{2}template:\s*\r?\n(?:(?!^\s{2}\S).)*?^\s{6}annotations:\s*\r?\n(?<block>(?:^\s{8}\S.*(?:\r?\n|$))*)')
    if (-not $match.Success) {
        return $null
    }

    return $match.Groups['block'].Value
}

function Upsert-TemplateAnnotation([string] $Content, [string] $Key, [string] $Value) {
    $block = Get-TemplateAnnotationBlock $Content
    if ($null -eq $block) {
        Fail $ExitGeneral 'missing pod-template annotations block'
    }

    if ($block -match "(?m)^\s{8}$([regex]::Escape($Key)):\s*") {
        return $Content
    }

    $insert = "        ${Key}: $Value$([Environment]::NewLine)"
    return [regex]::Replace(
        $Content,
        '(?ms)(^\s{2}template:\s*\r?\n(?:(?!^\s{2}\S).)*?^\s{6}annotations:\s*\r?\n)',
        { param($match) $match.Groups[1].Value + $insert },
        1)
}

function Remove-Annotation([string] $Content, [string] $Key) {
    return [regex]::Replace(
        $Content,
        "(?m)^\s*$([regex]::Escape($Key)):\s*.*`r?`n?",
        '')
}

function Assert-DaprTemplateAnnotations([string] $Content, [string] $Name, [string] $Config) {
    $block = Get-TemplateAnnotationBlock $Content
    if ($null -eq $block) {
        Fail $ExitGeneral "missing pod-template annotations block in $Name"
    }

    $expected = [ordered]@{
        'dapr.io/enabled' = '"true"'
        'dapr.io/app-id' = $Name
        'dapr.io/app-port' = "'8080'"
        'dapr.io/config' = $Config
    }

    foreach ($entry in $expected.GetEnumerator()) {
        if ($block -notmatch "(?m)^\s{8}$([regex]::Escape($entry.Key)):\s*$([regex]::Escape($entry.Value))\s*$") {
            Fail $ExitGeneral "pod-template Dapr annotation $($entry.Key) missing or incorrect in $Name"
        }
    }
}

function Assert-DaprClientOnlyTemplateAnnotations([string] $Content, [string] $Name) {
    $block = Get-TemplateAnnotationBlock $Content
    if ($null -eq $block) {
        Fail $ExitGeneral "missing pod-template annotations block in $Name"
    }

    $expected = [ordered]@{
        'dapr.io/enabled' = '"true"'
        'dapr.io/app-id' = $Name
    }

    foreach ($entry in $expected.GetEnumerator()) {
        if ($block -notmatch "(?m)^\s{8}$([regex]::Escape($entry.Key)):\s*$([regex]::Escape($entry.Value))\s*$") {
            Fail $ExitGeneral "client-only Dapr annotation $($entry.Key) missing or incorrect in $Name"
        }
    }

    foreach ($forbidden in @('dapr.io/app-port', 'dapr.io/config')) {
        if ($block -match "(?m)^\s{8}$([regex]::Escape($forbidden)):\s*") {
            Fail $ExitGeneral "client-only Dapr annotation $forbidden must be absent in $Name"
        }
    }
}

function Patch-DaprAnnotations {
    foreach ($forbidden in $ForbiddenDaprTargets) {
        $tuple = Read-DeploymentFile $forbidden
        if ($tuple[1] -match 'dapr\.io/(enabled|app-id|config|app-port)') {
            Fail $ExitGeneral "forbidden target $forbidden already carries Dapr annotations"
        }
    }

    foreach ($entry in $DaprPatchMap.GetEnumerator()) {
        $name = $entry.Key
        $config = $entry.Value
        $tuple = Read-DeploymentFile $name
        $path = $tuple[0]
        $content = $tuple[1]
        $content = $content -replace "(?m)^(\s*)dapr\.io/enabled:\s*.+$", '$1dapr.io/enabled: "true"'
        $content = $content -replace "(?m)^(\s*)dapr\.io/config:\s*.+$", "`$1dapr.io/config: $config"
        $content = $content -replace "(?m)^(\s*)dapr\.io/app-id:\s*.+$", "`$1dapr.io/app-id: $name"
        $content = Add-DaprAppPortAfterAppId $content $name
        $content = Upsert-TemplateAnnotation $content 'dapr.io/enabled' '"true"'
        $content = Upsert-TemplateAnnotation $content 'dapr.io/app-id' $name
        $content = Upsert-TemplateAnnotation $content 'dapr.io/app-port' "'8080'"
        $content = Upsert-TemplateAnnotation $content 'dapr.io/config' $config
        Assert-DaprTemplateAnnotations $content $name $config
        Set-Content -LiteralPath $path -Value $content -NoNewline
    }

    foreach ($name in $DaprClientOnlyTargets) {
        $tuple = Read-DeploymentFile $name
        $path = $tuple[0]
        $content = $tuple[1]
        $content = $content -replace "(?m)^(\s*)dapr\.io/enabled:\s*.+$", '$1dapr.io/enabled: "true"'
        $content = $content -replace "(?m)^(\s*)dapr\.io/app-id:\s*.+$", "`$1dapr.io/app-id: $name"
        $content = Remove-Annotation $content 'dapr.io/config'
        $content = Remove-Annotation $content 'dapr.io/app-port'
        $content = Upsert-TemplateAnnotation $content 'dapr.io/enabled' '"true"'
        $content = Upsert-TemplateAnnotation $content 'dapr.io/app-id' $name
        Assert-DaprClientOnlyTemplateAnnotations $content $name
        Set-Content -LiteralPath $path -Value $content -NoNewline
    }

    Write-Host "[publish] Dapr annotation patch targets: $($DaprPatchMap.Keys -join ', ')"
    Write-Host "[publish] Dapr client-only annotation targets: $($DaprClientOnlyTargets -join ', ')"
}

function Remove-EnvEntry([string] $Content, [string] $Name) {
    $escaped = [regex]::Escape($Name)
    $lines = $Content -split "`r?`n"
    $kept = [System.Collections.Generic.List[string]]::new()

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        if ($line -notmatch "^\s{8}-\s+name:\s*$escaped\s*$") {
            $kept.Add($line)
            continue
        }

        $index++
        while ($index -lt $lines.Count) {
            $next = $lines[$index]
            if ($next -match '^\s{8}-\s+name:\s*' -or
                $next -match '^\s{8}envFrom:\s*$' -or
                $next -match '^\s{6}\S' -or
                $next -match '^\s{4}\S' -or
                $next -match '^\s{2}\S') {
                $index--
                break
            }

            $index++
        }
    }

    return ($kept -join [Environment]::NewLine)
}

function Remove-SigningKeyEnvEntries {
    foreach ($name in $GeneratedServiceFolders) {
        $tuple = Read-DeploymentFile $name
        $path = $tuple[0]
        $content = $tuple[1]
        $content = Remove-EnvEntry $content 'Authentication__JwtBearer__SigningKey'
        $content = Remove-EnvEntry $content 'EventStore__Authentication__SigningKey'
        $content = $content -replace "(?m)^\s{8}env:\s*`r?`n\s{8}envFrom:", '        envFrom:'
        Set-Content -LiteralPath $path -Value $content -NoNewline
    }

    Write-Host "[publish] removed symmetric signing-key env entries"
}

function Ensure-UiCredentialSecretRefs {
    $names = @('eventstore-admin-ui', 'sample-blazor-ui')
    $envBlock = @"
        env:
        - name: EventStore__Authentication__Username
          valueFrom:
            secretKeyRef:
              name: $UiCredentialsSecretName
              key: username
        - name: EventStore__Authentication__Password
          valueFrom:
            secretKeyRef:
              name: $UiCredentialsSecretName
              key: password
"@

    foreach ($name in $names) {
        $tuple = Read-DeploymentFile $name
        $path = $tuple[0]
        $content = $tuple[1]
        $content = Remove-EnvEntry $content 'EventStore__Authentication__SigningKey'
        $content = Remove-EnvEntry $content 'EventStore__Authentication__Username'
        $content = Remove-EnvEntry $content 'EventStore__Authentication__Password'
        $content = $content -replace "(?m)^\s{8}env:\s*`r?`n\s{8}envFrom:", '        envFrom:'

        if ($content -match "(?m)^\s{8}envFrom:\s*$") {
            $content = $content -replace "(?m)^(\s*)envFrom:\s*$", "$envBlock`n`$1envFrom:"
        }
        elseif ($content -match "(?m)^\s{6}terminationGracePeriodSeconds:\s*") {
            $content = $content -replace "(?m)^(\s*)terminationGracePeriodSeconds:\s*", "$envBlock`n`$1terminationGracePeriodSeconds:"
        }
        else {
            Fail $ExitGeneral "unable to locate insertion point for UI credential env entries in $name"
        }

        if ($content -notmatch "EventStore__Authentication__Username" -or
            $content -notmatch "EventStore__Authentication__Password" -or
            $content -notmatch "secretKeyRef:\s*`r?`n\s*name:\s*$UiCredentialsSecretName\s*`r?`n\s*key:\s*username" -or
            $content -notmatch "secretKeyRef:\s*`r?`n\s*name:\s*$UiCredentialsSecretName\s*`r?`n\s*key:\s*password") {
            Fail $ExitGeneral "UI credential secretKeyRef postcondition failed in $name"
        }

        Set-Content -LiteralPath $path -Value $content -NoNewline
    }

    Write-Host "[publish] UI credential secretKeyRef patch targets: $($names -join ', ')"
}

function Remove-ConfigMapLiteral([string] $Content, [string] $Name) {
    return [regex]::Replace(
        $Content,
        "(?m)^\s+-\s*$([regex]::Escape($Name))=.*`r?`n?",
        '')
}

function Ensure-PartiesUiOidcClientSecretRef {
    $name = 'parties-ui'
    $tuple = Read-DeploymentFile $name
    $path = $tuple[0]
    $content = $tuple[1]
    $content = Remove-EnvEntry $content 'Authentication__OpenIdConnect__ClientSecret'
    $content = $content -replace "(?m)^\s{8}env:\s*`r?`n\s{8}envFrom:", '        envFrom:'

    $envBlock = @"
        env:
        - name: Authentication__OpenIdConnect__ClientSecret
          valueFrom:
            secretKeyRef:
              name: $PartiesUiOidcSecretName
              key: $PartiesUiOidcSecretKey
"@

    if ($content -match "(?m)^\s{8}envFrom:\s*$") {
        $content = $content -replace "(?m)^(\s*)envFrom:\s*$", "$envBlock`n`$1envFrom:"
    }
    elseif ($content -match "(?m)^\s{6}terminationGracePeriodSeconds:\s*") {
        $content = $content -replace "(?m)^(\s*)terminationGracePeriodSeconds:\s*", "$envBlock`n`$1terminationGracePeriodSeconds:"
    }
    else {
        Fail $ExitGeneral "unable to locate insertion point for parties-ui OIDC client-secret env entry"
    }

    if ($content -notmatch "Authentication__OpenIdConnect__ClientSecret" -or
        $content -notmatch "secretKeyRef:\s*`r?`n\s*name:\s*$PartiesUiOidcSecretName\s*`r?`n\s*key:\s*$PartiesUiOidcSecretKey") {
        Fail $ExitGeneral 'parties-ui OIDC client-secret secretKeyRef postcondition failed'
    }

    Set-Content -LiteralPath $path -Value $content -NoNewline

    $kustomizationPath = Join-Path $K8sRoot "$name/kustomization.yaml"
    if (Test-Path -LiteralPath $kustomizationPath) {
        $kustomization = Get-Content -Raw -LiteralPath $kustomizationPath
        $kustomization = Remove-ConfigMapLiteral $kustomization 'Authentication__OpenIdConnect__ClientSecret'
        if ($kustomization -match 'Authentication__OpenIdConnect__ClientSecret=') {
            Fail $ExitGeneral 'parties-ui generated kustomization still contains an inline OIDC client secret'
        }

        Set-Content -LiteralPath $kustomizationPath -Value $kustomization -NoNewline
    }

    Write-Host "[publish] parties-ui OIDC client secretKeyRef: $PartiesUiOidcSecretName/$PartiesUiOidcSecretKey"
}

function Assert-NoSigningKeyReferences {
    foreach ($name in $GeneratedServiceFolders) {
        foreach ($fileName in @('deployment.yaml', 'kustomization.yaml')) {
            $path = Join-Path $K8sRoot "$name/$fileName"
            if (-not (Test-Path -LiteralPath $path)) {
                continue
            }

            $content = Get-Content -Raw -LiteralPath $path
            if ($content -match 'Authentication__JwtBearer__SigningKey' -or
                $content -match 'EventStore__Authentication__SigningKey' -or
                $content -match 'hexalith-jwt-signing') {
                Fail $ExitGeneral "symmetric signing-key reference remains in $name/$fileName"
            }
        }
    }

    Write-Host '[publish] symmetric signing-key references absent from generated manifests'
}

function Resolve-KeycloakServiceClusterIp {
    if (-not [string]::IsNullOrWhiteSpace($Script:KeycloakClusterIp)) {
        return $Script:KeycloakClusterIp
    }

    $ip = Invoke-Checked 'kubectl' @('get', 'service', $KeycloakServiceName, '-n', $KeycloakNamespace, '-o', 'jsonpath={.spec.clusterIP}') $ExitGeneral "Keycloak service $KeycloakNamespace/$KeycloakServiceName read failed"
    $normalized = ($ip | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($normalized) -or $normalized -eq 'None') {
        Fail $ExitGeneral "Keycloak service $KeycloakNamespace/$KeycloakServiceName has no clusterIP"
    }

    $parsedIp = $null
    if (-not [System.Net.IPAddress]::TryParse($normalized, [ref] $parsedIp)) {
        Fail $ExitGeneral "Keycloak service $KeycloakNamespace/$KeycloakServiceName returned invalid clusterIP '$normalized'"
    }

    $Script:KeycloakClusterIp = $normalized
    return $Script:KeycloakClusterIp
}

function Set-HostAliasEntry([string] $Content, [string] $HostName, [string] $IpAddress) {
    $aliasLines = @(
        "      - ip: $IpAddress",
        "        hostnames:",
        "        - $HostName"
    )
    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in ($Content -split "`r?`n")) {
        $lines.Add($line)
    }

    $hostAliasesIndex = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match '^\s+hostAliases:\s*$') {
            $hostAliasesIndex = $index
            break
        }
    }

    if ($hostAliasesIndex -lt 0) {
        for ($index = 0; $index -lt $lines.Count; $index++) {
            if ($lines[$index] -match '^(\s+)(imagePullSecrets|containers):\s*$') {
                $indent = $Matches[1]
                $insertLines = @(
                    "${indent}hostAliases:",
                    "${indent}- ip: $IpAddress",
                    "$indent  hostnames:",
                    "$indent  - $HostName"
                )
                for ($aliasIndex = 0; $aliasIndex -lt $insertLines.Count; $aliasIndex++) {
                    $lines.Insert($index + $aliasIndex, $insertLines[$aliasIndex])
                }

                return ($lines -join [Environment]::NewLine)
            }
        }

        Fail $ExitGeneral "unable to locate pod spec insertion point for Keycloak host alias"
    }

    $blockEnd = $hostAliasesIndex + 1
    while ($blockEnd -lt $lines.Count) {
        $line = $lines[$blockEnd]
        if ($line -match '^\s{6}\S' -and $line -notmatch '^\s{6}-\s+') {
            break
        }

        if ($line -match '^\s{4}\S' -or $line -match '^\s{2}\S') {
            break
        }

        $blockEnd++
    }

    $newBlock = [System.Collections.Generic.List[string]]::new()
    $newBlock.Add($lines[$hostAliasesIndex])
    foreach ($aliasLine in $aliasLines) {
        $newBlock.Add($aliasLine)
    }

    $entry = [System.Collections.Generic.List[string]]::new()
    for ($index = $hostAliasesIndex + 1; $index -lt $blockEnd; $index++) {
        $line = $lines[$index]
        if ($line -match '^\s{6}-\s+' -and $entry.Count -gt 0) {
            if (-not (($entry -join [Environment]::NewLine) -match "(?m)^\s{8,}-\s*$([regex]::Escape($HostName))\s*$")) {
                foreach ($entryLine in $entry) {
                    $newBlock.Add($entryLine)
                }
            }

            $entry.Clear()
        }

        $entry.Add($line)
    }

    if ($entry.Count -gt 0 -and
        -not (($entry -join [Environment]::NewLine) -match "(?m)^\s{8,}-\s*$([regex]::Escape($HostName))\s*$")) {
        foreach ($entryLine in $entry) {
            $newBlock.Add($entryLine)
        }
    }

    $rebuilt = [System.Collections.Generic.List[string]]::new()
    for ($index = 0; $index -lt $hostAliasesIndex; $index++) {
        $rebuilt.Add($lines[$index])
    }

    foreach ($blockLine in $newBlock) {
        $rebuilt.Add($blockLine)
    }

    for ($index = $blockEnd; $index -lt $lines.Count; $index++) {
        $rebuilt.Add($lines[$index])
    }

    return ($rebuilt -join [Environment]::NewLine)
}

function Patch-KeycloakHostAlias {
    $clusterIp = Resolve-KeycloakServiceClusterIp

    foreach ($name in $GeneratedServiceFolders) {
        if ($name -eq 'eventstore-admin-ui') {
            continue
        }

        $tuple = Read-DeploymentFile $name
        $path = $tuple[0]
        $content = $tuple[1]
        $content = Set-HostAliasEntry $content $TacheIssuerHost $clusterIp

        if ($content -notmatch "hostAliases:" -or
            $content -notmatch "ip:\s*$([regex]::Escape($clusterIp))" -or
            $content -notmatch $TacheIssuerHost) {
            Fail $ExitGeneral "Keycloak host alias postcondition failed in $name"
        }

        Set-Content -LiteralPath $path -Value $content -NoNewline
    }

    Write-Host "[publish] Keycloak host alias: $TacheIssuerHost -> $KeycloakNamespace/$KeycloakServiceName ($clusterIp) except eventstore-admin-ui"
}

function Set-EventStoreAdminUiPublicKeycloakIssuer {
    $path = Join-Path $K8sRoot 'eventstore-admin-ui/kustomization.yaml'
    if (-not (Test-Path -LiteralPath $path)) {
        Fail $ExitGeneral 'eventstore-admin-ui/kustomization.yaml missing after generation'
    }

    $content = Get-Content -Raw -LiteralPath $path
    $content = $content -replace 'EventStore__Authentication__Authority=http://auth\.tache\.ai:8080/realms/tache', "EventStore__Authentication__Authority=$TachePublicIssuer"
    $content = $content -replace 'EventStore__Authentication__Issuer=http://auth\.tache\.ai:8080/realms/tache', "EventStore__Authentication__Issuer=$TachePublicIssuer"

    if ($content -match 'EventStore__Authentication__(Authority|Issuer)=http://auth\.tache\.ai:8080/realms/tache') {
        Fail $ExitGeneral 'eventstore-admin-ui still contains HTTP Keycloak authority or issuer'
    }

    if ($content -notmatch "EventStore__Authentication__Authority=$([regex]::Escape($TachePublicIssuer))" -or
        $content -notmatch "EventStore__Authentication__Issuer=$([regex]::Escape($TachePublicIssuer))") {
        Fail $ExitGeneral 'eventstore-admin-ui public Keycloak authority postcondition failed'
    }

    Set-Content -LiteralPath $path -Value $content -NoNewline
    Write-Host "[publish] eventstore-admin-ui Keycloak issuer: $TachePublicIssuer"
}

function Ensure-ImagePullSecrets {
    $patched = @()
    foreach ($folder in $GeneratedServiceFolders) {
        $tuple = Read-DeploymentFile $folder
        $path = $tuple[0]
        $content = $tuple[1]
        $usesRegistry = $content -match "image:\s*$([regex]::Escape($Registry))/"
        if (-not $usesRegistry) {
            continue
        }

        if (-not (Test-PodTemplateImagePullSecret $content)) {
            $content = $content -replace "(?m)^(\s*)containers:\s*$", "`$1imagePullSecrets:`n`$1- name: $ZotSecretName`n`$1containers:"
            Set-Content -LiteralPath $path -Value $content -NoNewline
        }

        $patched += $folder
    }

    foreach ($folder in $GeneratedServiceFolders) {
        $tuple = Read-DeploymentFile $folder
        if ($tuple[1] -match "image:\s*$([regex]::Escape($Registry))/" -and -not (Test-PodTemplateImagePullSecret $tuple[1])) {
            Fail $ExitGeneral "registry-backed workload $folder lacks $ZotSecretName after patch"
        }
    }

    Write-Host "[publish] imagePullSecrets patch targets: $($patched -join ', ')"
}

function Assert-ZotImageManifests([string] $ImageTag) {
    $entry = Read-ZotAuthBlock
    $headers = @{
        Accept = 'application/vnd.oci.image.index.v1+json, application/vnd.oci.image.manifest.v1+json, application/vnd.docker.distribution.manifest.v2+json, application/vnd.docker.distribution.manifest.list.v2+json'
        Authorization = "Basic $($entry.auth)"
    }

    foreach ($repository in $GeneratedServiceFolders) {
        if ($env:SCRIPT_TEST_LOG) {
            Add-Content -LiteralPath $env:SCRIPT_TEST_LOG -Value "zot manifest $repository $ImageTag"
            continue
        }

        $uri = "https://$Registry/v2/$repository/manifests/$ImageTag"
        try {
            $response = Invoke-WebRequest -Uri $uri -Method Head -Headers $headers -MaximumRedirection 0 -SkipHttpErrorCheck -TimeoutSec 30
        }
        catch {
            Fail $ExitZotAuth "Zot manifest verification failed for ${repository}:$ImageTag"
        }

        if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
            Fail $ExitZotAuth "Zot manifest verification failed for ${repository}:$ImageTag with HTTP $($response.StatusCode)"
        }
    }

    Write-Host "[publish] verified Zot manifests: $($GeneratedServiceFolders -join ', ')"
}

function Set-PrimaryContainerHealthProbes([string] $Content) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $inputLines = $Content -split "`r?`n"
    $index = 0
    while ($index -lt $inputLines.Count) {
        $line = $inputLines[$index]
        if ($line -match '^\s{8}(readinessProbe|livenessProbe):\s*$') {
            $index++
            while ($index -lt $inputLines.Count) {
                $next = $inputLines[$index]
                if ($next -match '^\s{8}\S' -or
                    $next -match '^\s{6}\S' -or
                    $next -match '^\s{4}\S' -or
                    $next -match '^\s{2}\S') {
                    break
                }

                $index++
            }

            continue
        }

        $lines.Add($line)
        if ($line -match '^\s{8}imagePullPolicy:\s*IfNotPresent\s*$') {
            $lines.Add('        readinessProbe:')
            $lines.Add('          httpGet:')
            $lines.Add('            path: /health')
            $lines.Add('            port: http')
            $lines.Add('          initialDelaySeconds: 10')
            $lines.Add('          timeoutSeconds: 10')
            $lines.Add('          periodSeconds: 10')
            $lines.Add('          failureThreshold: 30')
            $lines.Add('        livenessProbe:')
            $lines.Add('          httpGet:')
            $lines.Add('            path: /health')
            $lines.Add('            port: http')
            $lines.Add('          initialDelaySeconds: 120')
            $lines.Add('          timeoutSeconds: 10')
            $lines.Add('          periodSeconds: 10')
            $lines.Add('          failureThreshold: 30')
        }

        $index++
    }

    return ($lines -join [Environment]::NewLine)
}

function Test-PrimaryContainerHealthProbes([string] $Content) {
    return $Content -match "(?m)^\s{8}readinessProbe:[ \t]*`r?`n\s{10}httpGet:[ \t]*`r?`n\s{12}path:[ \t]*/health[ \t]*`r?`n\s{12}port:[ \t]*http[ \t]*$" -and
        $Content -match "(?m)^\s{10}timeoutSeconds:[ \t]*10[ \t]*$" -and
        $Content -match "(?m)^\s{8}livenessProbe:[ \t]*`r?`n\s{10}httpGet:[ \t]*`r?`n\s{12}path:[ \t]*/health[ \t]*`r?`n\s{12}port:[ \t]*http[ \t]*$" -and
        (([regex]::Matches($Content, "(?m)^\s{10}timeoutSeconds:[ \t]*10[ \t]*$")).Count -ge 2)
}

function Ensure-HealthProbes {
    $patched = @()
    foreach ($folder in $GeneratedServiceFolders) {
        $tuple = Read-DeploymentFile $folder
        $path = $tuple[0]
        $content = $tuple[1]
        if (Test-PrimaryContainerHealthProbes $content) {
            continue
        }

        $content = Set-PrimaryContainerHealthProbes $content
        if (-not (Test-PrimaryContainerHealthProbes $content)) {
            Fail $ExitGeneral "unable to patch health probes in $folder"
        }

        Set-Content -LiteralPath $path -Value $content -NoNewline
        $patched += $folder
    }

    Write-Host "[publish] health probe patch targets: $($patched -join ', ')"
}

function Invoke-DeploymentValidator {
    Require-Command 'pwsh'
    $validatorPath = Join-Path $DeployRoot 'validate-deployment.ps1'
    [void](Invoke-Checked 'pwsh' @('-NoProfile', '-File', $validatorPath, '--config-path', $DaprRoot, '-K8sPath', $K8sRoot) $ExitGeneral 'deployment validator failed')
    Write-Host '[publish] deployment validator passed'
}

function Test-PodTemplateImagePullSecret([string] $Content) {
    return $Content -match "(?ms)^\s{4}spec:\s*`r?`n(?:(?!^\s{4}\S).)*?^\s{6}imagePullSecrets:\s*`r?`n\s{6,8}-\s+name:\s*$([regex]::Escape($ZotSecretName))\s*$"
}

function Ensure-Namespace {
    $yaml = @"
apiVersion: v1
kind: Namespace
metadata:
  name: $Namespace
"@
    [void](Invoke-KubectlInput $yaml @('apply', '-f', '-') $ExitGeneral 'namespace apply failed')
    Write-Host "[publish] namespace $Namespace exists"
}

function ConvertFrom-Base64UrlJson([string] $Value) {
    $normalized = $Value.Replace('-', '+').Replace('_', '/')
    switch ($normalized.Length % 4) {
        2 { $normalized += '==' }
        3 { $normalized += '=' }
        0 { }
        default { Fail $ExitGeneral 'Keycloak token payload is not valid base64url' }
    }

    try {
        $json = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($normalized))
        return $json | ConvertFrom-Json -Depth 20
    }
    catch {
        Fail $ExitGeneral 'Keycloak token payload could not be decoded'
    }
}

function Test-JsonPropertyHasValue($Object, [string] $Name) {
    if ($null -eq $Object -or -not ($Object.PSObject.Properties.Name -contains $Name)) {
        return $false
    }

    $value = $Object.PSObject.Properties[$Name].Value
    if ($null -eq $value) {
        return $false
    }

    if ($value -is [array]) {
        return @($value | Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_) }).Count -gt 0
    }

    return -not [string]::IsNullOrWhiteSpace([string] $value)
}

function Assert-TokenContainsValue($Payload, [string] $ClaimName, [string] $ExpectedValue) {
    if (-not (Test-JsonPropertyHasValue $Payload $ClaimName)) {
        Fail $ExitGeneral "Keycloak token is missing required claim '$ClaimName'"
    }

    $value = $Payload.PSObject.Properties[$ClaimName].Value
    $values = if ($value -is [array]) { @($value) } else { @($value) }
    if (-not ($values | Where-Object { [string] $_ -eq $ExpectedValue })) {
        Fail $ExitGeneral "Keycloak token claim '$ClaimName' does not include required value '$ExpectedValue'"
    }
}

function Assert-KeycloakTokenContract([string] $TokenJson) {
    $jsonMatch = [regex]::Matches($TokenJson, '\{[^{}]*"access_token"\s*:[^{}]*\}') | Select-Object -Last 1
    if ($null -ne $jsonMatch) {
        $TokenJson = $jsonMatch.Value
    }

    try {
        $response = $TokenJson | ConvertFrom-Json -Depth 20
    }
    catch {
        Fail $ExitGeneral 'Keycloak token response was not valid JSON'
    }

    if ($null -eq $response -or -not ($response.PSObject.Properties.Name -contains 'access_token')) {
        Fail $ExitGeneral 'Keycloak token response did not include access_token'
    }

    $accessToken = [string] $response.access_token
    $parts = $accessToken.Split('.')
    if ($parts.Count -lt 2) {
        Fail $ExitGeneral 'Keycloak access token is not a JWT'
    }

    $payload = ConvertFrom-Base64UrlJson $parts[1]
    if ([string] $payload.iss -ne $TacheIssuer) {
        Fail $ExitGeneral 'Keycloak token issuer does not match the configured tache issuer'
    }

    Assert-TokenContainsValue $payload 'aud' 'hexalith-eventstore'
    Assert-TokenContainsValue $payload 'eventstore:tenant' 'tenant-a'
    Assert-TokenContainsValue $payload 'eventstore:domain' 'counter'
    Assert-TokenContainsValue $payload 'eventstore:permission' 'commands:*'
}

function Invoke-KeycloakPreflightPod {
    $clusterIp = Resolve-KeycloakServiceClusterIp
    $podName = "keycloak-tache-preflight-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
    $script = @"
set -eu
issuer="$TacheIssuer"
curl -fsS "`$issuer/.well-known/openid-configuration" | grep -q '"issuer":"'$TacheIssuer'"'
curl -fsS "`$issuer/protocol/openid-connect/certs" >/dev/null
curl -fsS -X POST "`$issuer/protocol/openid-connect/token" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password' \
  -d 'client_id=hexalith-eventstore' \
  --data-urlencode "username=`$KEYCLOAK_USERNAME" \
  --data-urlencode "password=`$KEYCLOAK_PASSWORD"
"@
    $overrides = [ordered]@{
        spec = [ordered]@{
            restartPolicy = 'Never'
            hostAliases = @(
                [ordered]@{
                    ip = $clusterIp
                    hostnames = @($TacheIssuerHost)
                }
            )
            containers = @(
                [ordered]@{
                    name = $podName
                    image = 'curlimages/curl'
                    command = @('/bin/sh', '-c')
                    args = @($script)
                    env = @(
                        [ordered]@{
                            name = 'KEYCLOAK_USERNAME'
                            valueFrom = [ordered]@{
                                secretKeyRef = [ordered]@{
                                    name = $UiCredentialsSecretName
                                    key = 'username'
                                }
                            }
                        },
                        [ordered]@{
                            name = 'KEYCLOAK_PASSWORD'
                            valueFrom = [ordered]@{
                                secretKeyRef = [ordered]@{
                                    name = $UiCredentialsSecretName
                                    key = 'password'
                                }
                            }
                        }
                    )
                }
            )
        }
    } | ConvertTo-Json -Depth 20 -Compress

    return Invoke-Checked 'kubectl' @(
        'run', $podName,
        '-n', $Namespace,
        '--rm',
        '-i',
        '--restart=Never',
        '--image=curlimages/curl',
        "--overrides=$overrides"
    ) $ExitGeneral 'Keycloak tache realm preflight failed'
}

function Test-KeycloakTacheRealmContract {
    $tokenJson = Invoke-KeycloakPreflightPod
    Assert-KeycloakTokenContract (($tokenJson | Out-String).Trim())
    Write-Host '[publish] Keycloak tache realm preflight passed: discovery, JWKS, token issuer, audience, and EventStore claims'
}

function Ensure-DaprControlPlane {
    function Assert-DaprCrds {
        $crds = @('components.dapr.io', 'configurations.dapr.io', 'subscriptions.dapr.io', 'resiliencies.dapr.io')
        [void](Invoke-Checked 'kubectl' (@('get', 'crd') + $crds) $ExitGeneral 'required Dapr CRDs are missing')
    }

    if ($SkipDaprInit) {
        Assert-DaprCrds
        Write-Host '[publish] Dapr init skipped; required CRDs verified'
        return
    }

    Require-Command 'dapr'
    $status = & dapr status -k 2>&1
    if ($LASTEXITCODE -eq 0) {
        Assert-DaprCrds
        Write-Host '[publish] existing Dapr control plane is healthy'
        return
    }

    $existingDaprNamespace = & kubectl get namespace dapr-system --ignore-not-found=true -o name 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace(($existingDaprNamespace | Out-String).Trim())) {
        $bounded = ($status | Select-Object -First 20) -join [Environment]::NewLine
        Fail $ExitGeneral "existing Dapr control plane is unhealthy. $bounded"
    }

    & dapr init -k --wait --timeout 300
    if ($LASTEXITCODE -ne 0) {
        Fail $ExitGeneral 'dapr init -k failed or existing install is unhealthy'
    }

    Assert-DaprCrds
    Write-Host '[publish] Dapr control plane initialized or already healthy'
}

function Reconcile-LegacyLocalKeycloakResources {
    [void](Invoke-Checked 'kubectl' (@('delete') + $LegacyLocalKeycloakResources + @('-n', $Namespace, '--ignore-not-found=true')) $ExitGeneral 'legacy local Keycloak resource cleanup failed')
    Write-Host "[publish] legacy local Keycloak resources absent: $($LegacyLocalKeycloakResources -join ', ')"
}

function Wait-WorkloadsReady {
    $expectedDeployments = $GeneratedServiceFolders + @('redis', 'falkordb')
    foreach ($deployment in $expectedDeployments) {
        [void](Invoke-Checked 'kubectl' @('rollout', 'status', "deployment/$deployment", '-n', $Namespace, '--timeout=600s') $ExitGeneral "deployment $deployment did not become Ready")
    }

    [void](Invoke-Checked 'kubectl' @('wait', '--for=condition=Ready', 'pod', '-n', $Namespace, '--all', '--timeout=600s') $ExitGeneral 'not all pods became Ready')
    Write-Host "[publish] workloads Ready: $($expectedDeployments -join ', ')"
}

function Restart-GeneratedDeployments {
    foreach ($deployment in $GeneratedServiceFolders) {
        [void](Invoke-Checked 'kubectl' @('rollout', 'restart', "deployment/$deployment", '-n', $Namespace) $ExitGeneral "deployment $deployment restart failed")
    }

    Write-Host "[publish] rollout restart targets: $($GeneratedServiceFolders -join ', ')"
}

function Apply-DaprResources {
    $orderedFiles = @(
        'statestore.yaml',
        'pubsub.yaml',
        'resiliency.yaml',
        'accesscontrol.yaml',
        'accesscontrol-eventstore-admin.yaml',
        'accesscontrol-sample.yaml',
        'accesscontrol-parties.yaml',
        'accesscontrol-tenants.yaml',
        'accesscontrol-memories.yaml',
        'subscription-parties.yaml',
        'subscription-tenants.yaml'
    )

    foreach ($file in $orderedFiles) {
        [void](Invoke-Checked 'kubectl' @('apply', '-f', (Join-Path $DaprRoot $file)) $ExitGeneral "Dapr apply failed for $file")
        Write-Host "[publish] applied Dapr CR: $file"
    }
}

function ConvertTo-SecretDataValue([string] $Value) {
    return [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($Value))
}

function New-RandomPrintableSecretData([int] $ByteCount) {
    $bytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes($ByteCount)
    $printableSecret = [Convert]::ToBase64String($bytes)
    return ConvertTo-SecretDataValue $printableSecret
}

function Test-SecretExists([string] $Name) {
    $output = & kubectl get secret $Name -n $Namespace --ignore-not-found=true -o name 2>$null
    return ($LASTEXITCODE -eq 0) -and -not [string]::IsNullOrWhiteSpace(($output | Out-String).Trim())
}

function Apply-SecretYaml([string] $Yaml, [string] $Name, [string] $Verb) {
    [void](Invoke-KubectlInput $Yaml @('apply', '-f', '-') $ExitGeneral "secret $Name apply failed")
    Write-Host "[publish] secret $Name $Verb"
}

function Ensure-OpaqueSecretIfMissing([string] $Name, [hashtable] $Data) {
    if (Test-SecretExists $Name) {
        $json = Invoke-Checked 'kubectl' @('get', 'secret', $Name, '-n', $Namespace, '-o', 'json') $ExitGeneral "secret $Name read failed"
        $secret = ($json | Out-String) | ConvertFrom-Json -Depth 20
        $missing = [ordered]@{}
        foreach ($key in $Data.Keys) {
            if ($null -eq $secret.data -or -not ($secret.data.PSObject.Properties.Name -contains $key)) {
                $missing[$key] = $Data[$key]
            }
        }

        if ($missing.Count -gt 0) {
            $patch = @{ data = $missing } | ConvertTo-Json -Depth 5 -Compress
            [void](Invoke-Checked 'kubectl' @('patch', 'secret', $Name, '-n', $Namespace, '--type', 'merge', '-p', $patch) $ExitGeneral "secret $Name patch failed")
            Write-Host "[publish] secret $Name patched missing keys"
            return
        }

        Write-Host "[publish] secret $Name exists"
        return
    }

    $dataLines = foreach ($key in ($Data.Keys | Sort-Object)) {
        "  $($key): $($Data[$key])"
    }

    $yaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: $Name
  namespace: $Namespace
type: Opaque
data:
$($dataLines -join [Environment]::NewLine)
"@
    Apply-SecretYaml $yaml $Name 'created'
}

function Get-DockerConfigPath {
    if (-not [string]::IsNullOrWhiteSpace($env:DOCKER_CONFIG)) {
        return (Join-Path $env:DOCKER_CONFIG 'config.json')
    }

    return (Join-Path $HOME '.docker/config.json')
}

function Read-ZotAuthBlock {
    $configPath = Get-DockerConfigPath
    if (-not (Test-Path -LiteralPath $configPath)) {
        Fail $ExitZotAuth "Docker config not found; run docker login -u parties-publisher $Registry or set DOCKER_CONFIG"
    }

    try {
        $json = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -Depth 20
    }
    catch {
        Fail $ExitZotAuth 'Docker config is malformed JSON'
    }

    if ($json.PSObject.Properties.Name -contains 'credsStore') {
        Fail $ExitZotAuth 'Docker config uses credsStore; helper-backed auth is not supported for publish'
    }

    if (($json.PSObject.Properties.Name -contains 'credHelpers') -and
        $null -ne $json.credHelpers -and
        $json.credHelpers -isnot [pscustomobject]) {
        Fail $ExitZotAuth 'Docker config credHelpers must be a JSON object when present'
    }

    if (($json.PSObject.Properties.Name -contains 'credHelpers') -and
        $null -ne $json.credHelpers -and
        ($json.credHelpers.PSObject.Properties.Name -contains $Registry)) {
        Fail $ExitZotAuth "Docker config uses credHelpers['$Registry']; helper-backed auth is not supported for publish"
    }

    if (-not ($json.PSObject.Properties.Name -contains 'auths') -or
        $null -eq $json.auths -or
        $json.auths -isnot [pscustomobject] -or
        -not ($json.auths.PSObject.Properties.Name -contains $Registry)) {
        Fail $ExitZotAuth "Docker config missing auths['$Registry']; run docker login -u parties-publisher $Registry"
    }

    $entry = $json.auths.$Registry
    if ($null -eq $entry -or
        $entry -isnot [pscustomobject] -or
        -not ($entry.PSObject.Properties.Name -contains 'auth') -or
        [string]::IsNullOrWhiteSpace($entry.auth)) {
        Fail $ExitZotAuth "Docker config auths['$Registry'] is missing auth"
    }

    $auth = [string] $entry.auth
    if (($auth.Length % 4) -ne 0 -or $auth -notmatch '^[A-Za-z0-9+/]*={0,2}$') {
        Fail $ExitZotAuth "Docker config auths['$Registry'].auth is malformed base64"
    }

    return $entry
}

function Ensure-ZotPullSecret {
    $entry = Read-ZotAuthBlock
    $dockerConfig = [ordered]@{
        auths = [ordered]@{
            $Registry = $entry
        }
    } | ConvertTo-Json -Depth 20 -Compress
    $dockerConfigB64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($dockerConfig))

    $yaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: $ZotSecretName
  namespace: $Namespace
type: kubernetes.io/dockerconfigjson
data:
  .dockerconfigjson: $dockerConfigB64
"@

    Apply-SecretYaml $yaml $ZotSecretName 'created-or-updated'
}

function Validate-UiCredentialsSecret {
    foreach ($key in @('username', 'password')) {
        $value = Invoke-Checked 'kubectl' @('get', 'secret', $UiCredentialsSecretName, '-n', $Namespace, '-o', "jsonpath={.data.$key}") $ExitGeneral "required UI credential Secret $UiCredentialsSecretName key '$key' is missing"
        if ([string]::IsNullOrWhiteSpace(($value | Out-String).Trim())) {
            Fail $ExitGeneral "required UI credential Secret $UiCredentialsSecretName key '$key' is missing"
        }
    }

    Write-Host "[publish] UI credential Secret validated: $UiCredentialsSecretName keys username,password"
}

function Validate-PartiesUiOidcClientSecret {
    $presenceTemplate = "{{- if index .data `"$PartiesUiOidcSecretKey`" -}}present{{- end -}}"
    $presence = Invoke-Checked 'kubectl' @('get', 'secret', $PartiesUiOidcSecretName, '-n', $Namespace, '-o', "go-template=$presenceTemplate") $ExitGeneral "required parties-ui OIDC Secret $PartiesUiOidcSecretName key '$PartiesUiOidcSecretKey' is missing"
    if (($presence | Out-String).Trim() -ne 'present') {
        Fail $ExitGeneral "required parties-ui OIDC Secret $PartiesUiOidcSecretName key '$PartiesUiOidcSecretKey' is missing"
    }

    Write-Host "[publish] parties-ui OIDC Secret validated: $PartiesUiOidcSecretName key $PartiesUiOidcSecretKey"
}

Require-Command 'kubectl'

try {
    Write-Step 'Confirm Kubernetes context'
    [void](Assert-KubeContext -Expected $ConfirmContext)
}
catch {
    Write-Host "[publish] ERROR: $($_.Exception.Message)"
    exit $ExitContext
}

Write-Step 'Ensure namespace and preflight external auth'
Ensure-Namespace
Validate-UiCredentialsSecret
Validate-PartiesUiOidcClientSecret
Test-KeycloakTacheRealmContract

Write-Step 'Resolve MinVer image tag'
$imageTag = Resolve-MinVerTag

Write-Step 'Clean generated deploy/k8s entries'
Clean-DeploymentTree

Write-Step 'Run dotnet aspirate generate'
Invoke-AspirateGenerate $imageTag

Write-Step 'Strip Aspirate placeholder files'
Remove-AspiratePlaceholders

Write-Step 'Patch Dapr annotations'
Patch-DaprAnnotations

Write-Step 'Patch Keycloak host alias'
Patch-KeycloakHostAlias

Write-Step 'Patch eventstore-admin-ui public Keycloak issuer'
Set-EventStoreAdminUiPublicKeycloakIssuer

Write-Step 'Patch UI credential secretKeyRefs'
Remove-SigningKeyEnvEntries
Ensure-UiCredentialSecretRefs
Ensure-PartiesUiOidcClientSecretRef
Assert-NoSigningKeyReferences

Write-Step 'Patch health probes'
Ensure-HealthProbes

Write-Step 'Patch imagePullSecrets'
Ensure-ImagePullSecrets
Normalize-GeneratedKustomizations

Write-Step 'Verify expected service folders'
Assert-ServiceFolders

Write-Step 'Verify Zot image manifests'
Assert-ZotImageManifests $imageTag

Write-Step 'Run static deployment validator'
Invoke-DeploymentValidator

Write-Step 'Install or verify Dapr control plane'
Ensure-DaprControlPlane

Write-Step 'Dry-run resiliency CR'
[void](Invoke-Checked 'kubectl' @('apply', '-f', (Join-Path $DaprRoot 'resiliency.yaml'), '--dry-run=server') $ExitGeneral 'Dapr resiliency server-side dry-run failed')
Write-Host '[publish] Dapr resiliency dry-run OK: resiliency.yaml'

Write-Step 'Ensure Zot pull Secret'
Ensure-ZotPullSecret

Write-Step 'Apply Dapr CRs'
Apply-DaprResources

Write-Step 'Reconcile legacy local Keycloak resources'
Reconcile-LegacyLocalKeycloakResources

Write-Step 'Apply Kubernetes workloads'
[void](Invoke-Checked 'kubectl' @('apply', '-k', $K8sRoot) $ExitGeneral 'kustomize apply failed')

Write-Step 'Restart generated workloads'
Restart-GeneratedDeployments

Write-Step 'Wait for workloads to become Ready'
Wait-WorkloadsReady

$Script:Started.Stop()
Write-Host "[publish] OK: $imageTag applied to $ConfirmContext in $($Script:Started.Elapsed)"
exit 0
