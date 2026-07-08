#Requires -Version 7.0

[CmdletBinding()]
param(
    [string]$Registry = "registry.hexalith.com",
    [string]$ImageTag,
    [switch]$SkipManifestVerification,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$AppHostProject = Join-Path $RepoRoot "src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj"
$SemVerPattern = '^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$'
$Registry = $Registry.Trim().TrimEnd("/")

$PartiesImages = @(
    [pscustomobject]@{
        Repository = "parties"
        Project = Join-Path $RepoRoot "src/Hexalith.Parties/Hexalith.Parties.csproj"
    },
    [pscustomobject]@{
        Repository = "parties-mcp"
        Project = Join-Path $RepoRoot "src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj"
    },
    [pscustomobject]@{
        Repository = "parties-ui"
        Project = Join-Path $RepoRoot "src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj"
    }
)

function Fail([string]$Message) {
    throw "[publish-parties-containers] $Message"
}

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Fail "Required command '$Name' was not found on PATH."
    }
}

function Invoke-Checked([string]$FilePath, [string[]]$ArgumentList) {
    Write-Host "[publish-parties-containers] $FilePath $($ArgumentList -join ' ')"
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        Fail "'$FilePath' exited with code $LASTEXITCODE."
    }
}

function Normalize-ImageTag([string]$Version) {
    $normalized = $Version.Trim()

    if ($normalized.StartsWith("v", [StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring(1)
    }

    if ([string]::IsNullOrWhiteSpace($normalized)) {
        Fail "Resolved image tag is empty."
    }

    if ($normalized.Contains("+dirty", [StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to publish a dirty MinVer image tag: $normalized"
    }

    if ($normalized.Contains("+", [StringComparison]::Ordinal)) {
        Fail "Refusing to publish an image tag with SemVer build metadata: $normalized"
    }

    if ($normalized -notmatch $SemVerPattern) {
        Fail "Image tag '$normalized' must be SemVer without build metadata."
    }

    if ($normalized -in @("latest", "staging-latest")) {
        Fail "Mutable image tag '$normalized' is forbidden."
    }

    return $normalized
}

function Resolve-MinVerImageTag {
    if (-not (Test-Path $AppHostProject)) {
        Fail "Cannot resolve image tag because AppHost project is missing: $AppHostProject"
    }

    Invoke-Checked "dotnet" @(
        "restore",
        $AppHostProject,
        "-p:UseHexalithProjectReferences=true",
        "-p:UseNuGetDeps=false",
        "-p:HexalithMemoriesFromSource=false"
    )

    $arguments = @(
        "msbuild",
        $AppHostProject,
        "-t:Build",
        "-p:Configuration=Release",
        "-p:UseHexalithProjectReferences=true",
        "-p:UseNuGetDeps=false",
        "-p:HexalithMemoriesFromSource=false",
        "-getProperty:Version"
    )

    Write-Host "[publish-parties-containers] Resolving MinVer image tag from AppHost..."
    $version = & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        Fail "Could not resolve MinVer image tag from AppHost."
    }

    return Normalize-ImageTag ($version | Select-Object -Last 1)
}

function New-ZotManifestHeaders {
    $headers = @{
        Accept = "application/vnd.oci.image.manifest.v1+json, application/vnd.docker.distribution.manifest.v2+json"
    }

    $username = $env:ZOT_REGISTRY_USERNAME
    $apiKey = $env:ZOT_REGISTRY_API_KEY
    if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($apiKey)) {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes("${username}:$apiKey")
        $headers.Authorization = "Basic " + [Convert]::ToBase64String($bytes)
    }

    return $headers
}

function Assert-ZotManifest([string]$Repository, [string]$Tag) {
    $uri = "https://$Registry/v2/$Repository/manifests/$Tag"
    try {
        Invoke-WebRequest -Uri $uri -Method Head -Headers (New-ZotManifestHeaders) -TimeoutSec 30 | Out-Null
    }
    catch {
        Fail "Zot manifest verification failed for ${Repository}:$Tag at $uri. Check repository permissions and ZOT_REGISTRY_USERNAME/ZOT_REGISTRY_API_KEY."
    }
}

Require-Command "dotnet"

$ImageTag = if ([string]::IsNullOrWhiteSpace($ImageTag)) {
    Resolve-MinVerImageTag
}
else {
    Normalize-ImageTag $ImageTag
}

Write-Host "[publish-parties-containers] Registry: $Registry"
Write-Host "[publish-parties-containers] Image tag: $ImageTag"
Write-Host "[publish-parties-containers] Repositories: $($PartiesImages.Repository -join ', ')"

foreach ($image in $PartiesImages) {
    if (-not (Test-Path $image.Project)) {
        Fail "Container project missing for $($image.Repository): $($image.Project)"
    }

    $qualifiedImage = "$Registry/$($image.Repository):$ImageTag"
    if ($DryRun) {
        Write-Host "[publish-parties-containers] Dry run: would publish $qualifiedImage from $($image.Project)"
        continue
    }

    Invoke-Checked "dotnet" @(
        "publish",
        $image.Project,
        "--configuration",
        "Release",
        "--os",
        "linux",
        "--arch",
        "x64",
        "/t:PublishContainer",
        "-p:ContainerRegistry=$Registry",
        "-p:ContainerRepository=$($image.Repository)",
        "-p:ContainerImageTag=$ImageTag",
        "-p:UseHexalithProjectReferences=true",
        "-p:UseNuGetDeps=false",
        "-p:HexalithMemoriesFromSource=false"
    )
}

if (-not $DryRun -and -not $SkipManifestVerification) {
    foreach ($image in $PartiesImages) {
        Assert-ZotManifest $image.Repository $ImageTag
    }
}

Write-Host "[publish-parties-containers] Completed Parties container publish for tag $ImageTag."
