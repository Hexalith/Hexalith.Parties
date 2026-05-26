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
$JwtSecretName = 'hexalith-jwt-signing'
$KeycloakSecretName = 'hexalith-keycloak-admin'
$ZotSecretName = 'zot-pull-secret'

$GeneratedServiceFolders = @(
    'eventstore',
    'eventstore-admin',
    'eventstore-admin-ui',
    'parties',
    'parties-mcp',
    'tenants',
    'memories'
)

$PreservedEntries = @(
    'redis',
    'keycloak',
    'falkordb',
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
    'parties' = 'accesscontrol-parties'
    'tenants' = 'accesscontrol-tenants'
    'memories' = 'accesscontrol-memories'
}

$DaprClientOnlyTargets = @('eventstore-admin-ui')
$ForbiddenDaprTargets = @('parties-mcp', 'redis', 'keycloak', 'falkordb')

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
    Write-Host "[publish] Image proof: $Registry/parties:$normalized"
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
  - memories
  - parties
  - parties-mcp
  - tenants
  - redis
  - keycloak
  - falkordb
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
    foreach ($line in ($Content -split "`r?`n", -1)) {
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

function Ensure-JwtSecretRef {
    $envBlock = @"
        env:
        - name: Authentication__JwtBearer__SigningKey
          valueFrom:
            secretKeyRef:
              name: $JwtSecretName
              key: value
"@
    $envItem = @"
        - name: Authentication__JwtBearer__SigningKey
          valueFrom:
            secretKeyRef:
              name: $JwtSecretName
              key: value
"@

    function Replace-JwtSigningEntry([string] $Content, [string] $Replacement) {
        $lines = $Content -split "`r?`n", -1
        $output = [System.Collections.Generic.List[string]]::new()
        $replacementLines = $Replacement -split "`r?`n"
        $index = 0
        while ($index -lt $lines.Count) {
            $line = $lines[$index]
            if ($line -match '^\s{8}-\s+name:\s*Authentication__JwtBearer__SigningKey\s*$') {
                foreach ($replacementLine in $replacementLines) {
                    $output.Add($replacementLine)
                }

                $index++
                while ($index -lt $lines.Count) {
                    $next = $lines[$index]
                    if ($next -match '^\s{8}(-\s+name:|envFrom:)\s*' -or
                        $next -match '^\s{6}\S' -or
                        $next -match '^\s{4}\S' -or
                        $next -match '^\s{2}\S') {
                        break
                    }

                    $index++
                }

                continue
            }

            $output.Add($line)
            $index++
        }

        return ($output -join [Environment]::NewLine)
    }

    foreach ($name in $DaprPatchMap.Keys) {
        $tuple = Read-DeploymentFile $name
        $path = $tuple[0]
        $content = $tuple[1]
        $matches = [regex]::Matches($content, 'Authentication__JwtBearer__SigningKey')
        if ($matches.Count -gt 1) {
            Fail $ExitGeneral "duplicate JWT signing env entries in $name"
        }

        if ($content -match "Authentication__JwtBearer__SigningKey" -and $content -match "secretKeyRef:\s*`r?`n\s*name:\s*$JwtSecretName") {
            continue
        }

        if ($content -match "Authentication__JwtBearer__SigningKey") {
            $literalPattern = "(?m)^\s*-\s+name:\s*Authentication__JwtBearer__SigningKey\s*`r?`n\s*value:\s*.*$"
            $replaced = [regex]::Replace($content, $literalPattern, $envItem, 1)
            $content = if ($replaced -ne $content) { $replaced } else { Replace-JwtSigningEntry $content $envItem }
        }
        elseif ($content -match "(?m)^\s{8}envFrom:\s*$") {
            $content = $content -replace "(?m)^(\s*)envFrom:\s*$", "$envBlock`n`$1envFrom:"
        }
        elseif ($content -match "(?m)^\s{6}terminationGracePeriodSeconds:\s*") {
            $content = $content -replace "(?m)^(\s*)terminationGracePeriodSeconds:\s*", "$envBlock`n`$1terminationGracePeriodSeconds:"
        }
        else {
            Fail $ExitGeneral "unable to locate insertion point for JWT signing env entry in $name"
        }

        if ($content -notmatch "Authentication__JwtBearer__SigningKey" -or
            $content -notmatch "secretKeyRef:\s*`r?`n\s*name:\s*$JwtSecretName\s*`r?`n\s*key:\s*value") {
            Fail $ExitGeneral "JWT secretKeyRef postcondition failed in $name"
        }

        Set-Content -LiteralPath $path -Value $content -NoNewline
    }

    Write-Host "[publish] JWT secretKeyRef patch targets: $($DaprPatchMap.Keys -join ', ')"
}

function Ensure-AdminUiJwtSecretRef {
    $name = 'eventstore-admin-ui'
    $envBlock = @"
        env:
        - name: EventStore__Authentication__SigningKey
          valueFrom:
            secretKeyRef:
              name: $JwtSecretName
              key: value
"@
    $envItem = @"
        - name: EventStore__Authentication__SigningKey
          valueFrom:
            secretKeyRef:
              name: $JwtSecretName
              key: value
"@

    $tuple = Read-DeploymentFile $name
    $path = $tuple[0]
    $content = $tuple[1]

    if ($content -match "EventStore__Authentication__SigningKey" -and
        $content -match "secretKeyRef:\s*`r?`n\s*name:\s*$JwtSecretName\s*`r?`n\s*key:\s*value") {
        Write-Host "[publish] Admin UI JWT secretKeyRef patch target: $name"
        return
    }

    if ($content -match "EventStore__Authentication__SigningKey") {
        $literalPattern = "(?m)^\s*-\s+name:\s*EventStore__Authentication__SigningKey\s*`r?`n\s*value:\s*.*$"
        $content = [regex]::Replace($content, $literalPattern, $envItem, 1)
    }
    elseif ($content -match "(?m)^\s{8}envFrom:\s*$") {
        $content = $content -replace "(?m)^(\s*)envFrom:\s*$", "$envBlock`n`$1envFrom:"
    }
    elseif ($content -match "(?m)^\s{6}terminationGracePeriodSeconds:\s*") {
        $content = $content -replace "(?m)^(\s*)terminationGracePeriodSeconds:\s*", "$envBlock`n`$1terminationGracePeriodSeconds:"
    }
    else {
        Fail $ExitGeneral "unable to locate insertion point for Admin UI JWT signing env entry"
    }

    if ($content -notmatch "EventStore__Authentication__SigningKey" -or
        $content -notmatch "secretKeyRef:\s*`r?`n\s*name:\s*$JwtSecretName\s*`r?`n\s*key:\s*value") {
        Fail $ExitGeneral "Admin UI JWT secretKeyRef postcondition failed"
    }

    Set-Content -LiteralPath $path -Value $content -NoNewline
    Write-Host "[publish] Admin UI JWT secretKeyRef patch target: $name"
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

function Wait-WorkloadsReady {
    $expectedDeployments = $GeneratedServiceFolders + @('redis', 'keycloak', 'falkordb')
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

function Ensure-OperatorSecrets {
    Ensure-OpaqueSecretIfMissing $JwtSecretName @{ value = New-RandomPrintableSecretData 32 }
    Ensure-OpaqueSecretIfMissing $KeycloakSecretName @{
        KC_BOOTSTRAP_ADMIN_USERNAME = ConvertTo-SecretDataValue 'admin'
        KC_BOOTSTRAP_ADMIN_PASSWORD = New-RandomPrintableSecretData 24
    }
    Ensure-ZotPullSecret
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

Write-Step 'Patch JWT secretKeyRef'
Ensure-JwtSecretRef
Ensure-AdminUiJwtSecretRef

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

Write-Step 'Ensure namespace and dry-run resiliency CR'
Ensure-Namespace
[void](Invoke-Checked 'kubectl' @('apply', '-f', (Join-Path $DaprRoot 'resiliency.yaml'), '--dry-run=server') $ExitGeneral 'Dapr resiliency server-side dry-run failed')
Write-Host '[publish] Dapr resiliency dry-run OK: resiliency.yaml'

Write-Step 'Bootstrap operator-managed Secrets'
Ensure-OperatorSecrets

Write-Step 'Apply Dapr CRs'
Apply-DaprResources

Write-Step 'Apply Kubernetes workloads'
[void](Invoke-Checked 'kubectl' @('apply', '-k', $K8sRoot) $ExitGeneral 'kustomize apply failed')

Write-Step 'Restart generated workloads'
Restart-GeneratedDeployments

Write-Step 'Wait for workloads to become Ready'
Wait-WorkloadsReady

$Script:Started.Stop()
Write-Host "[publish] OK: $imageTag applied to $ConfirmContext in $($Script:Started.Elapsed)"
exit 0
