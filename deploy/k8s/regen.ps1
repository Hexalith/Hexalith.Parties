#!/usr/bin/env pwsh
# ===========================================================================
# Hexalith.Parties -- Kubernetes Manifest Regeneration
# ===========================================================================
# Regenerates deploy/k8s/ from the Aspire AppHost via aspirate (aspir8).
# Aspirate is pinned in .config/dotnet-tools.json; run `dotnet tool restore`
# first if you have not done so in this checkout.
#
# Determinism: two consecutive invocations against the same AppHost commit
# and pinned aspirate version produce byte-identical output.
#
# Usage:
#   pwsh deploy/k8s/regen.ps1
#   pwsh deploy/k8s/regen.ps1 -EnableKeycloak true
#
# Reference: deploy/k8s/README.md
# ===========================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("true", "false")]
    # NOTE: regen defaults to "false" even though the AppHost defaults to keycloak
    # enabled. Aspirate captures Keycloak's randomized admin bootstrap password
    # into the emitted ConfigMap each regen, which (a) breaks AC1 byte-determinism
    # (a different password every regen) and (b) ships a credential value in the
    # committed tree (anti-pattern: no secret values in manifest output). Until
    # the AppHost pins a stable Keycloak admin credential AND aspirate preserves
    # it across regens, keycloak K8s manifests are deferred to a follow-up story.
    # `dotnet aspire run` (Aspire orchestration) is unaffected: it still spins
    # keycloak with a per-process random password as Aspire's local-dev default.
    [string]$EnableKeycloak = "false",

    [Parameter(Mandatory = $false)]
    [string]$ContainerRegistry = "registry.hexalith.com",

    [Parameter(Mandatory = $false)]
    [string]$Namespace = "hexalith-parties"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$AppHostDir = Join-Path $RepositoryRoot "src/Hexalith.Parties.AppHost"
$OutputDir = $PSScriptRoot

if (-not (Test-Path $AppHostDir)) {
    Write-Error "AppHost directory not found at '$AppHostDir'."
    exit 2
}

Write-Host "Regenerating Kubernetes manifests under: $OutputDir"
Write-Host "AppHost: $AppHostDir"
Write-Host "Namespace: $Namespace"
Write-Host "Registry: $ContainerRegistry"
Write-Host "EnableKeycloak: $EnableKeycloak"

# Preserve scripts and README; clean only aspirate-emitted artifacts.
$PreservedNames = @("regen.ps1", "deploy-local.ps1", "teardown-local.ps1", "README.md")
Get-ChildItem -Path $OutputDir -Force | Where-Object {
    $PreservedNames -notcontains $_.Name
} | ForEach-Object {
    Remove-Item -Recurse -Force -LiteralPath $_.FullName
}

Push-Location $AppHostDir
try {
    $env:EnableKeycloak = $EnableKeycloak
    & dotnet aspirate generate `
        --output-path $OutputDir `
        --non-interactive `
        --skip-build `
        --image-pull-policy IfNotPresent `
        --container-registry $ContainerRegistry `
        --output-format kustomize `
        --disable-secrets `
        --disable-state `
        --include-dashboard false `
        --namespace $Namespace
    if ($LASTEXITCODE -ne 0) {
        Write-Error "aspirate generate exited with code $LASTEXITCODE."
        exit $LASTEXITCODE
    }

    # Aspirate writes an intermediate manifest.json next to the AppHost; remove it.
    # It is also listed in .gitignore as a defense-in-depth precaution.
    $intermediateManifest = Join-Path $AppHostDir "manifest.json"
    if (Test-Path $intermediateManifest) {
        Remove-Item -Force $intermediateManifest
    }
}
finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# Post-aspirate cleanup: strip the broken DAPR component placeholders.
# ---------------------------------------------------------------------------
# Aspirate 9.1.0 emits `deploy/k8s/dapr/{statestore,pubsub}.yaml` as name-only
# placeholders -- statestore.yaml has `metadata: []` (no `redisHost`, no
# `actorStateStore: true`), pubsub.yaml has `spec.type: pubsub` (invalid -- DAPR
# requires `pubsub.<backend>`). Both are listed in the emitted top-level
# `kustomization.yaml` and would be applied to the `hexalith-parties`
# namespace by `kubectl apply -k`, overriding the authoritative Redis
# Components from `deploy/dapr/{statestore,pubsub}.yaml`.
#
# This cleanup deletes the broken placeholders and removes the matching
# `dapr/statestore.yaml` / `dapr/pubsub.yaml` lines from `kustomization.yaml`.
# It is the only post-aspirate edit the regen pipeline performs: it is a
# documented, deterministic workaround for a specific aspirate gap. See
# `deploy/k8s/README.md` -> "Known aspirate limitations".
$placeholders = @(
    Join-Path $OutputDir "dapr/statestore.yaml"
    Join-Path $OutputDir "dapr/pubsub.yaml"
)
foreach ($placeholder in $placeholders) {
    if (Test-Path $placeholder) {
        Remove-Item -Force -LiteralPath $placeholder
    }
}

$kustomizationPath = Join-Path $OutputDir "kustomization.yaml"
if (Test-Path $kustomizationPath) {
    $kustomizationLines = Get-Content -LiteralPath $kustomizationPath
    $filteredLines = $kustomizationLines | Where-Object {
        $_ -notmatch '^\s*-\s+dapr/statestore\.yaml\s*$' -and
        $_ -notmatch '^\s*-\s+dapr/pubsub\.yaml\s*$'
    }
    Set-Content -LiteralPath $kustomizationPath -Value $filteredLines
}

# If the dapr/ directory is now empty, remove it. The authoritative DAPR
# Components live under deploy/dapr/ and are applied directly by
# deploy-local.ps1, not via kustomize.
$daprDir = Join-Path $OutputDir "dapr"
if ((Test-Path $daprDir) -and (-not (Get-ChildItem -Path $daprDir -Force))) {
    Remove-Item -Force -LiteralPath $daprDir
}

# ---------------------------------------------------------------------------
# Post-aspirate patch: per-app DAPR annotations (app-port + access-control config).
# ---------------------------------------------------------------------------
# Aspirate 9.1.0 emits `dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/config: tracing`
# (its hardcoded default), and `dapr.io/enable-api-logging` on DAPR-enabled
# Deployments. It does NOT emit `dapr.io/app-port`, and it does NOT translate
# the AppHost's `DaprSidecarOptions.Config = <accesscontrol path>` into a
# proper `dapr.io/config: <configuration-name>` annotation. Without these:
#   - `dapr.io/app-port` missing -> sidecar default callback port is not 8080,
#     so pub/sub subscriptions and DAPR-callback inbound requests silently
#     fail to deliver to the app.
#   - `dapr.io/config: tracing` -> sidecar loads the wrong Configuration CR
#     (or none, since `tracing` is not emitted by aspirate). The per-app
#     access-control Configuration CRs from deploy/dapr/accesscontrol.*.yaml
#     are applied to the cluster but never consulted by any sidecar -- the
#     deny-by-default contract is silently bypassed.
# This block patches both annotations into both annotation locations (the
# Deployment-level `metadata.annotations` and the pod-template-level
# `spec.template.metadata.annotations`) for each DAPR-enabled app.
$DaprAppConfigMap = @{
    'eventstore'       = 'accesscontrol'
    'eventstore-admin' = 'accesscontrol-eventstore-admin'
    'parties'          = 'accesscontrol-parties'
    'tenants'          = 'accesscontrol-tenants'
}
foreach ($appId in $DaprAppConfigMap.Keys) {
    $deploymentPath = Join-Path $OutputDir (Join-Path $appId "deployment.yaml")
    if (-not (Test-Path $deploymentPath)) { continue }
    $configName = $DaprAppConfigMap[$appId]
    $deploymentText = Get-Content -Raw -LiteralPath $deploymentPath
    # Replace dapr.io/config: tracing with the per-app access-control Configuration name.
    $deploymentText = $deploymentText -replace 'dapr\.io/config: tracing', "dapr.io/config: $configName"
    # Insert dapr.io/app-port: '8080' immediately after every dapr.io/app-id: <appId>
    # annotation line. Use a regex that captures the indentation so we preserve it.
    $deploymentText = $deploymentText -replace
        "(?m)^([ \t]*)dapr\.io/app-id: $([regex]::Escape($appId))(\r?\n)",
        "`$1dapr.io/app-id: $appId`$2`$1dapr.io/app-port: '8080'`$2"
    Set-Content -LiteralPath $deploymentPath -Value $deploymentText -NoNewline
}

# ---------------------------------------------------------------------------
# Post-condition: assert aspirate emitted the expected per-app folders.
# ---------------------------------------------------------------------------
# Aspirate can exit 0 while emitting an empty or partial set if the AppHost
# composition is broken in a way that does not throw (e.g. zero AddProject
# calls). The deploy script would then fail later with an obscure kustomize
# error. Fail fast here with a clear message.
$ExpectedAppFolders = @('eventstore', 'eventstore-admin', 'eventstore-admin-ui', 'parties', 'parties-mcp', 'tenants')
$missingFolders = @()
foreach ($expected in $ExpectedAppFolders) {
    if (-not (Test-Path (Join-Path $OutputDir $expected))) {
        $missingFolders += $expected
    }
}
if ($missingFolders.Count -gt 0) {
    $missingList = $missingFolders -join ', '
    $errorMessage = "Aspirate generation post-condition failed. Missing expected per-app folders: $missingList. Check src/Hexalith.Parties.AppHost/Program.cs for AddProject<...> registrations and rerun regen.ps1. This guards against aspirate exit-0 with an empty composition."
    Write-Error $errorMessage
    exit 4
}

Write-Host ""
Write-Host "Regeneration complete. Review with: git diff deploy/k8s/"
Write-Host "Tip: a clean run against an unchanged AppHost commit produces zero diff."
