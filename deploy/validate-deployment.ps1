#Requires -Version 7
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExitBlocking = 1
$ExitInvalid = 2
$ExitMissingPath = 3
$JsonVersion = '1'
$ValidFormats = @('human', 'json')
$RegistryPrefix = 'registry.hexalith.com/'
$DaprAppIds = @('eventstore', 'eventstore-admin', 'sample', 'parties', 'tenants', 'memories')
$DaprClientOnlyAppIds = @('eventstore-admin-ui', 'sample-blazor-ui')
$ForbiddenDaprAppIds = @('parties-mcp', 'parties-ui', 'redis', 'falkordb')
$PublicIngressAllowedServices = @('eventstore-admin-ui', 'sample-blazor-ui', 'parties-ui')
$SemVerPattern = '^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$'

function Get-RepoRoot {
    $directory = Get-Item -LiteralPath $PSScriptRoot
    while ($null -ne $directory -and -not (Test-Path -LiteralPath (Join-Path $directory.FullName 'global.json'))) {
        $directory = $directory.Parent
    }

    if ($null -eq $directory) {
        throw 'Repository root could not be resolved.'
    }

    $directory.FullName
}

function ConvertTo-ResolvedPath {
    param(
        [Parameter(Mandatory = $true)][string]$PathValue,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        [System.IO.Path]::GetFullPath($PathValue)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $PathValue))
    }
}

function ConvertTo-DisplayPath {
    param(
        [Parameter(Mandatory = $true)][string]$PathValue,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    $fullPath = [System.IO.Path]::GetFullPath($PathValue)
    $root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    if ($fullPath.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::Ordinal)) {
        return $fullPath.Substring($root.Length + 1).Replace('\', '/')
    }

    $portable = $fullPath.Replace('\', '/')
    $deployIndex = $portable.IndexOf('/deploy/', [System.StringComparison]::Ordinal)
    if ($deployIndex -ge 0) {
        return $portable.Substring($deployIndex + 1)
    }

    $portable
}

function Protect-Text {
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return '<empty>'
    }

    if ($Value -cmatch 'eyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]+\.?[A-Za-z0-9_-]*') {
        return '<redacted>'
    }

    if ($Value.Length -ge 40 -and $Value -cmatch '^[A-Za-z0-9+/=]{40,}$') {
        return '<redacted>'
    }

    if ($Value -cmatch '(?i)(password|passwd|secret|token|bearer|auth)') {
        return '<redacted>'
    }

    $Value
}

function New-Finding {
    param(
        [Parameter(Mandatory = $true)][string]$Severity,
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$File,
        [Parameter(Mandatory = $true)][string]$JsonPath,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    [pscustomobject]@{
        Severity = $Severity
        Category = $Category
        File = $File
        JsonPath = $JsonPath
        Reason = $Reason
    }
}

function Convert-FindingForJson {
    param([Parameter(Mandatory = $true)]$Finding)

    [ordered]@{
        severity = $Finding.Severity
        category = $Finding.Category
        file = $Finding.File
        jsonpath = $Finding.JsonPath
        reason = $Finding.Reason
    }
}

function Format-FindingLine {
    param([Parameter(Mandatory = $true)]$Finding)

    $line = "[{0}] {1} at {2}:{3} - {4}" -f $Finding.Severity, $Finding.Category, $Finding.File, $Finding.JsonPath, $Finding.Reason
    if ($line.Length -le 200) {
        return $line
    }

    $maxReason = [Math]::Max(12, 200 - ($line.Length - $Finding.Reason.Length) - 3)
    "[{0}] {1} at {2}:{3} - {4}..." -f $Finding.Severity, $Finding.Category, $Finding.File, $Finding.JsonPath, $Finding.Reason.Substring(0, [Math]::Min($Finding.Reason.Length, $maxReason))
}

function ConvertTo-ValidationDocument {
    param([AllowEmptyCollection()][array]$Findings)

    $blocking = @($Findings | Where-Object { $_.Severity -ceq 'BLOCKING' }).Count
    $warnings = @($Findings | Where-Object { $_.Severity -ceq 'WARNING' }).Count
    [ordered]@{
        version = $JsonVersion
        findings = @($Findings | ForEach-Object { Convert-FindingForJson $_ })
        summary = [ordered]@{
            findings = $Findings.Count
            blocking = $blocking
            warnings = $warnings
            status = if ($blocking -gt 0) { 'FAIL' } else { 'PASS' }
        }
    }
}

function Assert-RedactionSelfCheck {
    $sentinel = 'DO-NOT-PRINT-THIS-SECRET-eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJzZWxmLWNoZWNrIn0.signature'
    $descriptor = Get-SecretDescriptor -Key 'token' -Value $sentinel
    if ($descriptor -ne 'jwt-shaped') {
        throw 'redaction self-check failed'
    }

    $finding = New-SecretFinding -File 'self-check.yaml' -JsonPath '$.data.token' -Descriptor $descriptor
    $human = Format-FindingLine $finding
    $json = ConvertTo-ValidationDocument @($finding) | ConvertTo-Json -Depth 8 -Compress
    if ($human.Contains($sentinel, [System.StringComparison]::Ordinal) -or $json.Contains($sentinel, [System.StringComparison]::Ordinal)) {
        throw 'redaction self-check failed'
    }
}

function Parse-Arguments {
    param([string[]]$RawArgs)

    $parsed = @{
        ConfigPath = $null
        K8sPath = $null
        Format = 'human'
    }

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    for ($index = 0; $index -lt $RawArgs.Count; $index++) {
        $name = $RawArgs[$index]
        $canonical = switch -Regex ($name) {
            '^--config-path$' { 'ConfigPath'; break }
            '^-ConfigPath$' { 'ConfigPath'; break }
            '^-K8sPath$' { 'K8sPath'; break }
            '^-Format$' { 'Format'; break }
            '^-Output$' { 'Format'; break }
            default { $null }
        }

        if ($null -eq $canonical) {
            throw "Unknown argument '$($name)'."
        }

        if ($seen.Contains($canonical)) {
            throw "Duplicate argument '$($name)'."
        }

        if ($index + 1 -ge $RawArgs.Count -or $RawArgs[$index + 1].StartsWith('-', [System.StringComparison]::Ordinal)) {
            throw "Missing value for '$($name)'."
        }

        $index++
        $value = $RawArgs[$index]
        if ($canonical -eq 'Format') {
            if (-not ($ValidFormats -ccontains $value)) {
                throw "Unsupported format '$(Protect-Text $value)'."
            }
            $parsed.Format = $value
        }
        else {
            $parsed[$canonical] = $value
        }

        [void]$seen.Add($canonical)
    }

    if ([string]::IsNullOrWhiteSpace($parsed.ConfigPath)) {
        throw 'ConfigPath is required.'
    }
    if ([string]::IsNullOrWhiteSpace($parsed.K8sPath)) {
        throw 'K8sPath is required.'
    }

    [pscustomobject]$parsed
}

function Get-YamlFiles {
    param([Parameter(Mandatory = $true)][string]$Root)

    Get-ChildItem -LiteralPath $Root -File -Recurse |
        Where-Object { $_.Extension -cin @('.yaml', '.yml') } |
        Sort-Object FullName
}

function Split-YamlDocuments {
    param([Parameter(Mandatory = $true)][string]$Text)

    $documents = [System.Collections.Generic.List[object]]::new()
    $current = [System.Collections.Generic.List[string]]::new()
    foreach ($line in ($Text -split "`r?`n")) {
        if ($line -cmatch '^\s*---\s*(?:#.*)?$') {
            if ($current.Count -gt 0) {
                $documentLines = [string[]]$current.ToArray()
                $documents.Add([pscustomobject]@{ Lines = $documentLines; Text = ($documentLines -join "`n") })
                $current.Clear()
            }
            continue
        }

        $current.Add($line)
    }

    if ($current.Count -gt 0) {
        $documentLines = [string[]]$current.ToArray()
        $documents.Add([pscustomobject]@{ Lines = $documentLines; Text = ($documentLines -join "`n") })
    }

    $documents.ToArray()
}

function Get-LineIndent {
    param([AllowNull()][string]$Line)

    if ($null -eq $Line) {
        return 0
    }

    ($Line.Length - $Line.TrimStart(' ').Length)
}

function Test-IsUnderValueFromBlock {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][AllowEmptyString()][string[]]$Lines,
        [Parameter(Mandatory = $true)][int]$Index
    )

    if ($Lines.Count -eq 0 -or $Index -lt 0 -or $Index -ge $Lines.Count) {
        return $false
    }

    $lineIndent = Get-LineIndent $Lines[$Index]
    for ($i = $Index - 1; $i -ge 0; $i--) {
        if ([string]::IsNullOrWhiteSpace($Lines[$i])) {
            continue
        }

        $candidateIndent = Get-LineIndent $Lines[$i]
        if ($candidateIndent -ge $lineIndent) {
            continue
        }

        return $Lines[$i] -cmatch '^\s*(valueFrom|secretKeyRef):\s*$'
    }

    return $false
}

function Get-ScalarValue {
    param([AllowNull()][string]$Value)

    if ($null -eq $Value) {
        return ''
    }

    $trimmed = $Value.Trim()
    if ($trimmed -cmatch '^(.*?)(?:\s+#.*)?$') {
        $trimmed = $Matches[1].Trim()
    }

    $trimmed.Trim('"', "'")
}

function Test-IsPlaceholderValue {
    param([AllowNull()][string]$Value)

    [string]::IsNullOrWhiteSpace($Value) -or $Value -cin @('<placeholder>', 'placeholder', 'none', 'null', '{}', '[]', 'false', 'true')
}

function New-SecretFinding {
    param(
        [Parameter(Mandatory = $true)][string]$File,
        [Parameter(Mandatory = $true)][string]$JsonPath,
        [Parameter(Mandatory = $true)][string]$Descriptor
    )

    New-Finding -Severity 'BLOCKING' -Category 'Secret-Plaintext' -File $File -JsonPath $JsonPath -Reason "$Descriptor credential-like value redacted"
}

function Get-SecretDescriptor {
    param(
        [AllowNull()][string]$Key,
        [AllowNull()][string]$Value
    )

    if (Test-IsPlaceholderValue $Value) {
        return $null
    }

    if ($Value -cmatch 'eyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]+\.?[A-Za-z0-9_-]*') {
        return 'jwt-shaped'
    }

    if ($Key -cmatch '(?i)(password|passwd|secret|token|auth)' -or ($Key -ceq 'value' -and $Value -cmatch '(?i)(password|passwd|secret|token|bearer)')) {
        return 'password-prefixed'
    }

    if ($Key -ceq '.dockerconfigjson' -or $Value -cmatch '(?i)"auths"\s*:') {
        return 'docker-auth-shaped'
    }

    if ($Value.Length -ge 40 -and $Value -cmatch '^[A-Za-z0-9+/=]{40,}$') {
        return 'base64-shaped'
    }

    $null
}

function Get-DeploymentName {
    param([string[]]$Lines, [string]$Fallback)

    $inMetadata = $false
    foreach ($line in $Lines) {
        if ($line -cmatch '^metadata:\s*$') {
            $inMetadata = $true
            continue
        }
        if ($inMetadata -and $line -cmatch '^\S') {
            $inMetadata = $false
        }
        if ($inMetadata -and $line -cmatch '^\s{2}name:\s*[''"]?([^''"#]+)') {
            return $Matches[1].Trim()
        }
    }

    $Fallback
}

function Get-BlockEndIndex {
    param(
        [string[]]$Lines,
        [Parameter(Mandatory = $true)][int]$StartIndex,
        [Parameter(Mandatory = $true)][int]$ParentIndent
    )

    for ($i = $StartIndex + 1; $i -lt $Lines.Count; $i++) {
        if ([string]::IsNullOrWhiteSpace($Lines[$i])) {
            continue
        }

        $indent = Get-LineIndent $Lines[$i]
        if ($indent -lt $ParentIndent -or ($indent -eq $ParentIndent -and $Lines[$i] -cnotmatch '^\s*-\s+')) {
            return ($i - 1)
        }
    }

    $Lines.Count - 1
}

function Find-LineIndex {
    param(
        [string[]]$Lines,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][int]$StartIndex,
        [Parameter(Mandatory = $true)][int]$EndIndex
    )

    for ($i = $StartIndex; $i -le $EndIndex -and $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -cmatch $Pattern) {
            return $i
        }
    }

    -1
}

function Get-PodSpecRange {
    param([string[]]$Lines)

    $templateIndex = Find-LineIndex -Lines $Lines -Pattern '^\s*template:\s*$' -StartIndex 0 -EndIndex ($Lines.Count - 1)
    if ($templateIndex -lt 0) {
        return $null
    }

    $templateIndent = Get-LineIndent $Lines[$templateIndex]
    $templateEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $templateIndex -ParentIndent $templateIndent
    $specIndex = -1
    for ($i = $templateIndex + 1; $i -le $templateEnd; $i++) {
        if ($Lines[$i] -cmatch '^\s*spec:\s*$' -and (Get-LineIndent $Lines[$i]) -eq ($templateIndent + 2)) {
            $specIndex = $i
            break
        }
    }

    if ($specIndex -lt 0) {
        return $null
    }

    [pscustomobject]@{
        Start = $specIndex
        End = Get-BlockEndIndex -Lines $Lines -StartIndex $specIndex -ParentIndent (Get-LineIndent $Lines[$specIndex])
    }
}

function Test-HasZotPullSecret {
    param([string[]]$Lines)

    $podSpec = Get-PodSpecRange -Lines $Lines
    if ($null -eq $podSpec) {
        return $false
    }

    $secretIndex = Find-LineIndex -Lines $Lines -Pattern '^\s*imagePullSecrets:\s*$' -StartIndex $podSpec.Start -EndIndex $podSpec.End
    if ($secretIndex -lt 0) {
        return $false
    }

    $secretEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $secretIndex -ParentIndent (Get-LineIndent $Lines[$secretIndex])
    for ($i = $secretIndex + 1; $i -le $secretEnd; $i++) {
        if ($Lines[$i] -cmatch '^\s*-\s*name:\s*[''"]?zot-pull-secret[''"]?\s*(?:#.*)?$') {
            return $true
        }
    }

    $false
}

function Test-HasAllDaprAnnotations {
    param([string[]]$Lines)

    $templateIndex = Find-LineIndex -Lines $Lines -Pattern '^\s*template:\s*$' -StartIndex 0 -EndIndex ($Lines.Count - 1)
    if ($templateIndex -lt 0) {
        return $false
    }

    $templateIndent = Get-LineIndent $Lines[$templateIndex]
    $templateEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $templateIndex -ParentIndent $templateIndent
    $metadataIndex = -1
    for ($i = $templateIndex + 1; $i -le $templateEnd; $i++) {
        if ($Lines[$i] -cmatch '^\s*metadata:\s*$' -and (Get-LineIndent $Lines[$i]) -eq ($templateIndent + 2)) {
            $metadataIndex = $i
            break
        }
    }

    if ($metadataIndex -lt 0) {
        return $false
    }

    $metadataEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $metadataIndex -ParentIndent (Get-LineIndent $Lines[$metadataIndex])
    $annotationsIndex = Find-LineIndex -Lines $Lines -Pattern '^\s*annotations:\s*$' -StartIndex ($metadataIndex + 1) -EndIndex $metadataEnd
    if ($annotationsIndex -lt 0) {
        return $false
    }

    $annotationsEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $annotationsIndex -ParentIndent (Get-LineIndent $Lines[$annotationsIndex])
    $annotationText = ($Lines[($annotationsIndex + 1)..$annotationsEnd] -join "`n")
    foreach ($annotation in @('dapr.io/enabled', 'dapr.io/app-id', 'dapr.io/app-port', 'dapr.io/config')) {
        if ($annotationText -cnotmatch "(?m)^\s*$([regex]::Escape($annotation)):\s*") {
            return $false
        }
    }

    $true
}

function Test-HasClientOnlyDaprAnnotations {
    param([string[]]$Lines)

    $templateIndex = Find-LineIndex -Lines $Lines -Pattern '^\s*template:\s*$' -StartIndex 0 -EndIndex ($Lines.Count - 1)
    if ($templateIndex -lt 0) {
        return $false
    }

    $templateIndent = Get-LineIndent $Lines[$templateIndex]
    $templateEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $templateIndex -ParentIndent $templateIndent
    $metadataIndex = -1
    for ($i = $templateIndex + 1; $i -le $templateEnd; $i++) {
        if ($Lines[$i] -cmatch '^\s*metadata:\s*$' -and (Get-LineIndent $Lines[$i]) -eq ($templateIndent + 2)) {
            $metadataIndex = $i
            break
        }
    }

    if ($metadataIndex -lt 0) {
        return $false
    }

    $metadataEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $metadataIndex -ParentIndent (Get-LineIndent $Lines[$metadataIndex])
    $annotationsIndex = Find-LineIndex -Lines $Lines -Pattern '^\s*annotations:\s*$' -StartIndex ($metadataIndex + 1) -EndIndex $metadataEnd
    if ($annotationsIndex -lt 0) {
        return $false
    }

    $annotationsEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $annotationsIndex -ParentIndent (Get-LineIndent $Lines[$annotationsIndex])
    $annotationText = ($Lines[($annotationsIndex + 1)..$annotationsEnd] -join "`n")
    foreach ($annotation in @('dapr.io/enabled', 'dapr.io/app-id')) {
        if ($annotationText -cnotmatch "(?m)^\s*$([regex]::Escape($annotation)):\s*") {
            return $false
        }
    }

    foreach ($annotation in @('dapr.io/app-port', 'dapr.io/config')) {
        if ($annotationText -cmatch "(?m)^\s*$([regex]::Escape($annotation)):\s*") {
            return $false
        }
    }

    $true
}

function Test-HasAnyDaprAnnotations {
    param([string[]]$Lines)

    $templateIndex = Find-LineIndex -Lines $Lines -Pattern '^\s*template:\s*$' -StartIndex 0 -EndIndex ($Lines.Count - 1)
    if ($templateIndex -lt 0) {
        return $false
    }

    $templateIndent = Get-LineIndent $Lines[$templateIndex]
    $templateEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $templateIndex -ParentIndent $templateIndent
    $metadataIndex = -1
    for ($i = $templateIndex + 1; $i -le $templateEnd; $i++) {
        if ($Lines[$i] -cmatch '^\s*metadata:\s*$' -and (Get-LineIndent $Lines[$i]) -eq ($templateIndent + 2)) {
            $metadataIndex = $i
            break
        }
    }

    if ($metadataIndex -lt 0) {
        return $false
    }

    $metadataEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $metadataIndex -ParentIndent (Get-LineIndent $Lines[$metadataIndex])
    $annotationsIndex = Find-LineIndex -Lines $Lines -Pattern '^\s*annotations:\s*$' -StartIndex ($metadataIndex + 1) -EndIndex $metadataEnd
    if ($annotationsIndex -lt 0) {
        return $false
    }

    $annotationsEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $annotationsIndex -ParentIndent (Get-LineIndent $Lines[$annotationsIndex])
    $annotationText = ($Lines[($annotationsIndex + 1)..$annotationsEnd] -join "`n")
    $annotationText -cmatch "(?m)^\s*dapr\.io/[A-Za-z0-9_.-]+:\s*"
}

function Get-ContainerBlocks {
    param([string[]]$Lines)

    $containers = [System.Collections.Generic.List[object]]::new()
    $podSpec = Get-PodSpecRange -Lines $Lines
    if ($null -eq $podSpec) {
        return $containers
    }

    $containersIndex = Find-LineIndex -Lines $Lines -Pattern '^\s*containers:\s*$' -StartIndex $podSpec.Start -EndIndex $podSpec.End
    if ($containersIndex -lt 0) {
        return $containers
    }

    $containersEnd = Get-BlockEndIndex -Lines $Lines -StartIndex $containersIndex -ParentIndent (Get-LineIndent $Lines[$containersIndex])
    $currentStart = -1
    $currentIndent = -1
    for ($i = $containersIndex + 1; $i -le $containersEnd; $i++) {
        if ($Lines[$i] -cmatch '^\s*-\s*(name|image):\s*') {
            $indent = Get-LineIndent $Lines[$i]
            if ($currentStart -lt 0) {
                $currentStart = $i
                $currentIndent = $indent
                continue
            }

            if ($indent -eq $currentIndent) {
                $containers.Add([pscustomobject]@{ Start = $currentStart; End = ($i - 1); Text = ($Lines[$currentStart..($i - 1)] -join "`n") })
                $currentStart = $i
            }
        }
    }

    if ($currentStart -ge 0) {
        $containers.Add([pscustomobject]@{ Start = $currentStart; End = $containersEnd; Text = ($Lines[$currentStart..$containersEnd] -join "`n") })
    }

    $containers.ToArray()
}

function Test-HasRequiredHealthProbe {
    param(
        [Parameter(Mandatory = $true)][string]$ContainerText,
        [Parameter(Mandatory = $true)][string]$ProbeName
    )

    $pattern = "(?m)^\s*$([regex]::Escape($ProbeName)):[ \t]*`r?`n\s*httpGet:[ \t]*`r?`n\s*path:[ \t]*/health[ \t]*`r?`n\s*port:[ \t]*http[ \t]*$"
    $ContainerText -cmatch $pattern
}

function Get-ImageTag {
    param([Parameter(Mandatory = $true)][string]$Image)

    $withoutDigest = $Image.Split('@')[0]
    $lastSlash = $withoutDigest.LastIndexOf('/')
    $lastColon = $withoutDigest.LastIndexOf(':')
    if ($lastColon -le $lastSlash) {
        return ''
    }

    $withoutDigest.Substring($lastColon + 1)
}

function Find-K8sWorkloadFindings {
    param(
        [Parameter(Mandatory = $true)][string]$K8sPath,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    $findings = [System.Collections.Generic.List[object]]::new()
    foreach ($file in Get-YamlFiles $K8sPath) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $relative = ConvertTo-DisplayPath -PathValue $file.FullName -RepositoryRoot $RepositoryRoot
        foreach ($document in @(Split-YamlDocuments -Text $text)) {
            foreach ($finding in @(Find-SecretFindings -Path $file.FullName -Text $document.Text -RepositoryRoot $RepositoryRoot)) {
                $findings.Add($finding)
            }

            if ($document.Text -cnotmatch '(?m)^kind:\s*Deployment\s*$') {
                continue
            }

            $lines = [string[]]$document.Lines
            $deploymentName = Get-DeploymentName -Lines $lines -Fallback ([System.IO.Path]::GetFileNameWithoutExtension($file.Name))
            $containers = @(Get-ContainerBlocks -Lines $lines)
            $registryImageCount = 0
            for ($containerIndex = 0; $containerIndex -lt $containers.Count; $containerIndex++) {
                $container = $containers[$containerIndex]
                foreach ($line in ($container.Text -split "`n")) {
                    if ($line -cmatch '^\s*image:\s*[''"]?([^''"\s#]+)') {
                        $image = $Matches[1]
                        if (-not $image.StartsWith($RegistryPrefix, [System.StringComparison]::Ordinal)) {
                            continue
                        }

                        $registryImageCount++
                        $tag = Get-ImageTag $image
                        if ([string]::IsNullOrWhiteSpace($tag) -or $tag -ceq 'latest' -or $tag -ceq 'staging-latest' -or $tag -cnotmatch $SemVerPattern) {
                            $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sWorkload-NonSemVerTag' -File $relative -JsonPath "$.spec.template.spec.containers[$containerIndex].image" -Reason 'registry image tag is mutable or not SemVer'))
                        }

                        if ($tag.Contains('+dirty', [System.StringComparison]::Ordinal)) {
                            $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sWorkload-DirtyTagOnConsumerImage' -File $relative -JsonPath "$.spec.template.spec.containers[$containerIndex].image" -Reason 'registry image tag contains dirty build metadata'))
                        }
                    }
                }
            }

            if ($registryImageCount -gt 0 -and -not (Test-HasZotPullSecret -Lines $lines)) {
                $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sWorkload-MissingImagePullSecret' -File $relative -JsonPath '$.spec.template.spec.imagePullSecrets' -Reason 'registry image requires zot-pull-secret'))
            }

            if ($DaprAppIds -ccontains $deploymentName -and -not (Test-HasAllDaprAnnotations -Lines $lines)) {
                $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sWorkload-MissingDaprAnnotations' -File $relative -JsonPath '$.spec.template.metadata.annotations' -Reason 'Dapr app is missing required pod annotations'))
            }

            if ($DaprClientOnlyAppIds -ccontains $deploymentName -and -not (Test-HasClientOnlyDaprAnnotations -Lines $lines)) {
                $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sWorkload-MissingDaprAnnotations' -File $relative -JsonPath '$.spec.template.metadata.annotations' -Reason 'Dapr client-only app is missing required pod annotations or carries server-only annotations'))
            }

            if ($ForbiddenDaprAppIds -ccontains $deploymentName -and (Test-HasAnyDaprAnnotations -Lines $lines)) {
                $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sWorkload-MissingDaprAnnotations' -File $relative -JsonPath '$.spec.template.metadata.annotations' -Reason 'non-Dapr workload must not carry Dapr annotations'))
            }

            if ($registryImageCount -gt 0 -and
                ($containers.Count -eq 0 -or
                -not (Test-HasRequiredHealthProbe -ContainerText $containers[0].Text -ProbeName 'readinessProbe') -or
                -not (Test-HasRequiredHealthProbe -ContainerText $containers[0].Text -ProbeName 'livenessProbe'))) {
                $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sWorkload-MissingProbes' -File $relative -JsonPath '$.spec.template.spec.containers[0]' -Reason 'primary container requires readinessProbe and livenessProbe using httpGet /health on port http'))
            }

        }
    }

    $findings.ToArray()
}

function Find-DaprAclFindings {
    param(
        [Parameter(Mandatory = $true)][string]$ConfigPath,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    $findings = [System.Collections.Generic.List[object]]::new()
    foreach ($file in Get-YamlFiles $ConfigPath) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $relative = ConvertTo-DisplayPath -PathValue $file.FullName -RepositoryRoot $RepositoryRoot
        foreach ($document in @(Split-YamlDocuments -Text $text)) {
            foreach ($finding in @(Find-SecretFindings -Path $file.FullName -Text $document.Text -RepositoryRoot $RepositoryRoot)) {
                $findings.Add($finding)
            }
            if ($document.Text -cnotmatch '(?m)^kind:\s*Configuration\s*$' -or $document.Text -cnotmatch '(?m)^\s*accessControl:\s*$') {
                continue
            }

            $lines = [string[]]$document.Lines
            $policiesIndex = Find-LineIndex -Lines $lines -Pattern '^\s*policies:\s*$' -StartIndex 0 -EndIndex ($lines.Count - 1)
            if ($policiesIndex -lt 0) {
                continue
            }

            $policiesIndent = Get-LineIndent $lines[$policiesIndex]
            $policiesEnd = Get-BlockEndIndex -Lines $lines -StartIndex $policiesIndex -ParentIndent $policiesIndent
            $policyStarts = [System.Collections.Generic.List[int]]::new()
            $policyIndent = -1
            for ($i = $policiesIndex + 1; $i -le $policiesEnd; $i++) {
                if ($lines[$i] -cmatch '^\s*-\s+') {
                    $indent = Get-LineIndent $lines[$i]
                    if ($policyIndent -lt 0) {
                        $policyIndent = $indent
                    }
                    if ($indent -eq $policyIndent) {
                        $policyStarts.Add($i)
                    }
                }
            }

            for ($policyNumber = 0; $policyNumber -lt $policyStarts.Count; $policyNumber++) {
                $start = $policyStarts[$policyNumber]
                $end = if ($policyNumber + 1 -lt $policyStarts.Count) { $policyStarts[$policyNumber + 1] - 1 } else { $policiesEnd }
                $policyText = ($lines[$start..$end] -join "`n")
                $hasAllow = $policyText -cmatch '(?m)^\s*action:\s*allow\s*(?:#.*)?$' -or $policyText -cmatch '(?m)^\s*operations:\s*$'
                $appId = $null
                if ($policyText -cmatch '(?m)^\s*-?\s*appId:\s*[''"]?([^''"#]*)') {
                    $appId = $Matches[1].Trim()
                }

                if ($hasAllow -and ([string]::IsNullOrWhiteSpace($appId) -or $appId -ceq '*')) {
                    $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'DaprACL-WildcardAppId' -File $relative -JsonPath '$.spec.accessControl.policies[*].appId' -Reason 'allow policy appId must be explicit'))
                }

                foreach ($line in $lines[$start..$end]) {
                    if ($line -cmatch '^\s*-\s*name:\s*[''"]?([^''"#]+)') {
                        $operation = $Matches[1].Trim()
                        if ($operation -ceq '*' -or $operation -ceq '/**' -or $operation -cmatch '/\*$') {
                            $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'DaprACL-WildcardOperation' -File $relative -JsonPath '$.spec.accessControl.policies[*].operations[*].name' -Reason 'operation wildcard is wider than documented route map'))
                        }
                    }
                }
            }
        }
    }

    $findings.ToArray()
}

function Find-K8sIngressFindings {
    param(
        [Parameter(Mandatory = $true)][string]$K8sPath,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    $findings = [System.Collections.Generic.List[object]]::new()
    $requiredRoutes = [ordered]@{
        'eventstore.hexalith.com' = 'eventstore-admin-ui'
        'sample.hexalith.com' = 'sample-blazor-ui'
        'parties.hexalith.com' = 'parties-ui'
    }
    $seenRoutes = @{}
    foreach ($requiredHost in $requiredRoutes.Keys) {
        $seenRoutes[$requiredHost] = $false
    }

    $ingressCount = 0
    foreach ($file in Get-YamlFiles $K8sPath) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $relative = ConvertTo-DisplayPath -PathValue $file.FullName -RepositoryRoot $RepositoryRoot
        foreach ($document in @(Split-YamlDocuments -Text $text)) {
            if ($document.Text -cnotmatch '(?m)^kind:\s*Ingress\s*$') {
                continue
            }

            $ingressCount++
            $lines = [string[]]$document.Lines
            $routes = [System.Collections.Generic.List[object]]::new()
            $currentHost = $null
            $currentPath = $null
            $currentPathType = $null
            if ($document.Text -cnotmatch '(?m)^\s*name:\s*hexalith-pages-ingress\s*$') {
                $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sIngress-InvalidPublicRoute' -File $relative -JsonPath '$.metadata.name' -Reason 'public UI ingress must be named hexalith-pages-ingress'))
            }
            if ($document.Text -cnotmatch '(?m)^\s*ingressClassName:\s*nginx\s*$') {
                $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sIngress-InvalidPublicRoute' -File $relative -JsonPath '$.spec.ingressClassName' -Reason 'public UI ingress must use nginx ingress class'))
            }
            if ($document.Text -cnotmatch '(?m)^\s*secretName:\s*hexalith-pages-tls\s*$') {
                $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sIngress-InvalidPublicRoute' -File $relative -JsonPath '$.spec.tls[*].secretName' -Reason 'public UI ingress must use hexalith-pages-tls'))
            }

            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -cmatch '^\s*-\s*host:\s*[''"]?([^''"#\s]+)') {
                    $currentHost = $Matches[1]
                    $currentPath = $null
                    $currentPathType = $null
                    continue
                }
                if ($lines[$i] -cmatch '^\s*host:\s*[''"]?([^''"#\s]+)') {
                    $currentHost = $Matches[1]
                    $currentPath = $null
                    $currentPathType = $null
                    continue
                }
                if ($lines[$i] -cmatch '^\s*-\s*path:\s*[''"]?([^''"#\s]+)') {
                    $currentPath = $Matches[1]
                    $currentPathType = $null
                    continue
                }
                if ($lines[$i] -cmatch '^\s*pathType:\s*[''"]?([^''"#\s]+)') {
                    $currentPathType = $Matches[1]
                    continue
                }
                if ($lines[$i] -cnotmatch '^\s*service:\s*$') {
                    continue
                }

                $serviceIndent = Get-LineIndent $lines[$i]
                $serviceEnd = Get-BlockEndIndex -Lines $lines -StartIndex $i -ParentIndent $serviceIndent
                $serviceName = $null
                $servicePort = $null
                for ($j = $i + 1; $j -le $serviceEnd; $j++) {
                    if ($lines[$j] -cmatch '^\s*name:\s*[''"]?([^''"#\s]+)') {
                        $serviceName = $Matches[1]
                    }
                    if ($lines[$j] -cmatch '^\s*number:\s*[''"]?([^''"#\s]+)') {
                        $servicePort = $Matches[1]
                    }
                }

                $routes.Add([pscustomobject]@{
                    Host = $currentHost
                    Path = $currentPath
                    PathType = $currentPathType
                    ServiceName = $serviceName
                    ServicePort = $servicePort
                })
            }

            foreach ($route in $routes) {
                if ($null -eq $route.ServiceName) {
                    $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sIngress-InvalidPublicRoute' -File $relative -JsonPath '$.spec.rules[*].http.paths[*].backend.service.name' -Reason 'public ingress route is missing a backend service name'))
                    continue
                }

                $expectedService = $requiredRoutes[$route.Host]
                $isRequiredRoute = $null -ne $expectedService `
                    -and $route.Path -ceq '/' `
                    -and $route.PathType -ceq 'Prefix' `
                    -and $route.ServiceName -ceq $expectedService `
                    -and [string] $route.ServicePort -ceq '8080'

                if ($isRequiredRoute) {
                    $seenRoutes[$route.Host] = $true
                    continue
                }

                if ($PublicIngressAllowedServices -ccontains $route.ServiceName) {
                    $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sIngress-InvalidPublicRoute' -File $relative -JsonPath '$.spec.rules[*].http.paths[*]' -Reason "public ingress route '$($route.Host)$($route.Path)' targets UI service '$($route.ServiceName)' but does not match the documented host, root path, Prefix path type, and port 8080"))
                }
                else {
                    $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sIngress-InvalidPublicRoute' -File $relative -JsonPath '$.spec.rules[*].http.paths[*].backend.service.name' -Reason "public ingress targets non-UI backend service '$($route.ServiceName)'"))
                }
            }
        }
    }

    if ($ingressCount -eq 0) {
        $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sIngress-InvalidPublicRoute' -File (ConvertTo-DisplayPath -PathValue $K8sPath -RepositoryRoot $RepositoryRoot) -JsonPath '$.spec.rules' -Reason 'public UI ingress is missing'))
    }
    foreach ($requiredHost in $requiredRoutes.Keys) {
        if (-not $seenRoutes[$requiredHost]) {
            $findings.Add((New-Finding -Severity 'BLOCKING' -Category 'K8sIngress-InvalidPublicRoute' -File (ConvertTo-DisplayPath -PathValue $K8sPath -RepositoryRoot $RepositoryRoot) -JsonPath '$.spec.rules' -Reason "public UI ingress route for $requiredHost is missing or points to the wrong service/port"))
        }
    }

    $findings.ToArray()
}

function Find-SecretFindings {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    $findings = [System.Collections.Generic.List[object]]::new()
    if ($Text -cnotmatch '(?m)^kind:\s*(Deployment|ConfigMap|Secret|Component)\s*$') {
        return $findings
    }

    $relative = ConvertTo-DisplayPath -PathValue $Path -RepositoryRoot $RepositoryRoot
    $lines = $Text -split "`r?`n"
    $previousName = $null
    $previousNameCredentialShaped = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -cnotmatch '^\s*-?\s*([A-Za-z0-9_.-]+):\s*(.*)$') {
            continue
        }

        $key = $Matches[1]
        $value = Get-ScalarValue $Matches[2]

        if ($key -ceq 'name') {
            $previousName = $value
            $previousNameCredentialShaped = $value -cmatch '(?i)(password|passwd|secret|token|auth)'
            continue
        }

        if (Test-IsPlaceholderValue $value) {
            continue
        }

        if (Test-IsUnderValueFromBlock -Lines $lines -Index $i) {
            continue
        }

        if ($key -ceq 'key') {
            continue
        }

        $descriptor = Get-SecretDescriptor -Key $key -Value $value
        if ($null -eq $descriptor -and $key -ceq 'value' -and $previousNameCredentialShaped) {
            $descriptor = Get-SecretDescriptor -Key $previousName -Value $value
        }
        if ($null -ne $descriptor) {
            $findings.Add((New-SecretFinding -File $relative -JsonPath "$.data.$key" -Descriptor $descriptor))
        }
    }

    $findings.ToArray()
}

function Invoke-Validation {
    param([Parameter(Mandatory = $true)][object]$Parsed)

    $repositoryRoot = Get-RepoRoot
    $configPath = ConvertTo-ResolvedPath -PathValue $Parsed.ConfigPath -RepositoryRoot $repositoryRoot
    $k8sPath = ConvertTo-ResolvedPath -PathValue $Parsed.K8sPath -RepositoryRoot $repositoryRoot

    if (-not (Test-Path -LiteralPath $configPath -PathType Container)) {
        Write-Error "ConfigPath does not exist: $(Protect-Text $Parsed.ConfigPath)" -ErrorAction Continue
        exit $ExitMissingPath
    }
    if (-not (Test-Path -LiteralPath $k8sPath -PathType Container)) {
        Write-Error "K8sPath does not exist: $(Protect-Text $Parsed.K8sPath)" -ErrorAction Continue
        exit $ExitMissingPath
    }

    $findings = [System.Collections.Generic.List[object]]::new()
    foreach ($finding in @(Find-K8sWorkloadFindings -K8sPath $k8sPath -RepositoryRoot $repositoryRoot)) {
        $findings.Add($finding)
    }
    foreach ($finding in @(Find-K8sIngressFindings -K8sPath $k8sPath -RepositoryRoot $repositoryRoot)) {
        $findings.Add($finding)
    }
    foreach ($finding in @(Find-DaprAclFindings -ConfigPath $configPath -RepositoryRoot $repositoryRoot)) {
        $findings.Add($finding)
    }
    $ordered = @($findings | Sort-Object File, Category, JsonPath, Reason)
    $document = ConvertTo-ValidationDocument $ordered

    if ($Parsed.Format -ceq 'json') {
        $document | ConvertTo-Json -Depth 8
    }
    else {
        foreach ($finding in $ordered) {
            Format-FindingLine $finding
        }
        '[validate] {0} findings ({1} blocking, {2} warnings) - {3}' -f $document.summary.findings, $document.summary.blocking, $document.summary.warnings, $document.summary.status
    }

    if ($document.summary.blocking -gt 0) {
        exit $ExitBlocking
    }

    exit 0
}

try {
    $parsed = Parse-Arguments -RawArgs $args
    Assert-RedactionSelfCheck
    Invoke-Validation -Parsed $parsed
}
catch {
    Write-Error (Protect-Text $_.Exception.Message) -ErrorAction Continue
    exit $ExitInvalid
}
