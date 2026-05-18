#!/usr/bin/env pwsh
# ===========================================================================
# Hexalith.Parties -- Local Kubernetes Teardown
# ===========================================================================
# Removes Deployments, Services, ConfigMaps, and DAPR component CRs applied by
# deploy-local.ps1 from a *local* Kubernetes cluster. Refuses to run against
# any non-local kubectl context.
#
# By default the DAPR control plane (dapr-system namespace) is preserved.
# Pass -PurgeDapr to also uninstall it (`dapr uninstall -k`).
#
# Output is metadata-only (kind + name + namespace per deleted resource). No
# Secret values, ConfigMap data, or actor state-store payload is logged.
#
# Usage:
#   pwsh deploy/k8s/teardown-local.ps1
#   pwsh deploy/k8s/teardown-local.ps1 -PurgeDapr
#
# Exit codes:
#   0 = teardown clean, no residual resources
#   1 = delete step failed or residual resources detected
#   2 = active kubectl context not in local-cluster allowlist
#   3 = kubectl / dapr CLI not available
#
# Reference: deploy/k8s/README.md
# ===========================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ManifestPath = $PSScriptRoot,

    [Parameter(Mandatory = $false)]
    [string]$DaprComponentsPath,

    [Parameter(Mandatory = $false)]
    [string]$Namespace = "hexalith-parties",

    [Parameter(Mandatory = $false)]
    [switch]$PurgeDapr,

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Local-cluster context allowlist (mirrors deploy-local.ps1 and smoke tests).
# ---------------------------------------------------------------------------
$LocalContextPatterns = @(
    '^kind-[a-z0-9][a-z0-9-]*$',
    '^k3d-[a-z0-9][a-z0-9-]*$',
    '^minikube$',
    '^docker-desktop$'
)

function Test-LocalContext {
    param([string]$Context)
    # Case-sensitive (-cmatch) so a renamed managed context like 'Kind-Phishing'
    # does not bypass the gate. Mirrors deploy-local.ps1.
    foreach ($pattern in $LocalContextPatterns) {
        if ($Context -cmatch $pattern) { return $true }
    }
    return $false
}

function Test-CommandAvailable {
    param([string]$Name)
    $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

if (-not $DaprComponentsPath) {
    $DaprComponentsPath = (Resolve-Path (Join-Path $PSScriptRoot "../dapr")).Path
}

if (-not (Test-CommandAvailable "kubectl")) {
    Write-Error "kubectl is not on PATH. Install kubectl before running teardown-local."
    exit 3
}

# ---------------------------------------------------------------------------
# Verify local context
# ---------------------------------------------------------------------------
$currentContext = $null
try {
    $currentContext = (& kubectl config current-context 2>$null).Trim()
}
catch {
    Write-Error "Failed to read active kubectl context. Ensure a kubeconfig is configured."
    exit 2
}

if ([string]::IsNullOrWhiteSpace($currentContext)) {
    Write-Error "No active kubectl context. Set a local-cluster context before running teardown-local."
    exit 2
}

if (-not (Test-LocalContext -Context $currentContext)) {
    Write-Host "ERROR: Refusing to teardown against non-local kubectl context '$currentContext'."
    Write-Host "Allowed context patterns: kind-*, k3d-*, minikube, docker-desktop"
    exit 2
}

Write-Host "Active kubectl context: $currentContext (local-cluster allowlist OK)"

# ---------------------------------------------------------------------------
# Delete via kustomize (workloads, services, ConfigMaps, aspirate-emitted DAPR placeholders)
# ---------------------------------------------------------------------------
if (Test-Path (Join-Path $ManifestPath "kustomization.yaml")) {
    Write-Host ""
    Write-Host "Deleting Kubernetes manifests via kustomize: $ManifestPath"
    $deleteOutput = & kubectl delete -k $ManifestPath --ignore-not-found 2>&1
    # Bounded passthrough — kind/name lines only.
    foreach ($line in $deleteOutput) {
        if ($line -match '^(\S+)/(\S+)\s+(deleted|not found)') {
            Write-Host "  $line"
        }
    }
}

# ---------------------------------------------------------------------------
# Delete authoritative DAPR component CRs
# ---------------------------------------------------------------------------
if (Test-Path $DaprComponentsPath) {
    Write-Host ""
    Write-Host "Deleting authoritative DAPR component CRs from: $DaprComponentsPath"
    foreach ($yamlFile in Get-ChildItem -Path $DaprComponentsPath -Filter "*.yaml" -File) {
        # Mirror the deploy script's skip list: alternative-backend templates
        # and Hexalith CRDs (topology, tenants-integration) are not applied.
        if ($yamlFile.Name -match '^(statestore|pubsub)-[a-z0-9].*\.yaml$' -or
            $yamlFile.Name -eq 'topology.yaml' -or
            $yamlFile.Name -eq 'tenants-integration.yaml') { continue }
        & kubectl delete -f $yamlFile.FullName -n $Namespace --ignore-not-found 2>&1 | ForEach-Object {
            if ($_ -match '^\S+/\S+\s+(deleted|not found)') {
                Write-Host "  $_"
            }
        }
    }
}

# ---------------------------------------------------------------------------
# Residual-state probe
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Probing residual resources in namespace '$Namespace'..."
$residualKinds = @(
    "deployments",
    "services",
    "configmaps",
    "secrets",
    "components.dapr.io",
    "subscriptions.dapr.io",
    "configurations.dapr.io",
    "resiliencies.dapr.io"
)
$residuals = New-Object System.Collections.ArrayList
foreach ($kind in $residualKinds) {
    $listOutput = & kubectl get $kind -n $Namespace --no-headers --ignore-not-found 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($listOutput)) { continue }
    foreach ($line in ($listOutput -split "`n")) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
        # ConfigMaps named kube-root-ca.crt and default-token-* are platform-owned;
        # skip them so the residual check focuses on Parties-applied resources.
        $name = ($trimmed -split '\s+')[0]
        if ($name -eq "kube-root-ca.crt") { continue }
        if ($name -match '^default-token-') { continue }
        [void]$residuals.Add("$kind/$name")
    }
}

if ($residuals.Count -eq 0) {
    Write-Host "  No residual Parties resources detected in namespace '$Namespace'."
}
else {
    Write-Host "  RESIDUAL resources still present:"
    foreach ($r in $residuals) {
        Write-Host "    - $r"
    }
}

# ---------------------------------------------------------------------------
# Optional: purge DAPR control plane
# ---------------------------------------------------------------------------
if ($PurgeDapr) {
    if (Test-CommandAvailable "dapr") {
        # DAPR is cluster-wide shared state. `dapr uninstall -k --all` removes
        # the `dapr-system` namespace, control-plane Deployments, and CRDs --
        # which breaks ANY other DAPR project running on the same cluster.
        # Require an explicit confirmation unless -Force is passed.
        if (-not $Force) {
            Write-Host ""
            Write-Host "WARNING: -PurgeDapr will run 'dapr uninstall -k --all'."
            Write-Host "  This removes the DAPR control plane CLUSTER-WIDE, including the"
            Write-Host "  dapr-system namespace, CRDs, and Helm metadata. Any other DAPR"
            Write-Host "  project sharing this cluster will break."
            $reply = Read-Host "Type 'PURGE' (uppercase) to proceed, anything else to abort"
            if ($reply -cne 'PURGE') {
                Write-Host "Aborted -PurgeDapr; DAPR control plane left intact."
                if ($residuals.Count -gt 0) { exit 1 }
                exit 0
            }
        }
        Write-Host ""
        Write-Host "Uninstalling DAPR control plane (-PurgeDapr requested)..."
        & dapr uninstall -k --all 2>&1 | ForEach-Object {
            if ($_ -match '^\S') { Write-Host "  $_" }
        }
    }
    else {
        Write-Host "WARNING: dapr CLI not on PATH; cannot uninstall DAPR control plane."
    }
}

if ($residuals.Count -gt 0) { exit 1 }
exit 0
