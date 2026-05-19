#!/usr/bin/env pwsh
# ===========================================================================
# Hexalith.Parties -- Local Kubernetes Deploy
# ===========================================================================
# Applies the AppHost-generated Kubernetes manifests (deploy/k8s/) plus the
# authoritative DAPR component templates (deploy/dapr/) to a *local* Kubernetes
# cluster. Refuses to run against any non-local kubectl context.
#
# Behavior:
#   1. Verify active kubectl context matches the local-cluster allowlist
#      (kind-*, k3d-*, minikube, docker-desktop). Refuses with exit code 2 if not.
#   2. If DAPR control plane is not present on the cluster, run `dapr init -k`
#      and wait for readiness (skip with -SkipDaprInit).
#   3. Apply deploy/dapr/ first (authoritative DAPR component CRs).
#   4. Apply deploy/k8s/ via kustomize (workloads, services, scaffolded DAPR CRs).
#
# Output is metadata-only (kind + name + namespace per applied resource). No
# secret values, ConfigMap contents, or token/connection-string text is logged.
#
# Usage:
#   pwsh deploy/k8s/deploy-local.ps1
#   pwsh deploy/k8s/deploy-local.ps1 -SkipDaprInit
#
# Exit codes:
#   0 = applied successfully
#   1 = apply step failed
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
    [switch]$SkipDaprInit,

    [Parameter(Mandatory = $false)]
    [string]$DaprComponentsPath,

    [Parameter(Mandatory = $false)]
    [string]$Namespace = "hexalith-parties"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:NamespaceEnsured = $false

# ---------------------------------------------------------------------------
# Local-cluster context allowlist (mirrors teardown-local.ps1 and smoke tests).
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
    # does not bypass the gate. Patterns also reject dot-containing suffixes
    # ('kind-acme.com') and require an alphanumeric first character. NOTE:
    # the gate still trusts the operator not to rename a managed AKS/EKS/GKE
    # context to a literal 'kind-*' name; a node providerID probe is deferred
    # to a hardening pass.
    foreach ($pattern in $LocalContextPatterns) {
        if ($Context -cmatch $pattern) { return $true }
    }
    return $false
}

# ---------------------------------------------------------------------------
# Tool probing
# ---------------------------------------------------------------------------
function Test-CommandAvailable {
    param([string]$Name)
    $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

# Resolve dapr/ authoritative components dir.
if (-not $DaprComponentsPath) {
    $DaprComponentsPath = (Resolve-Path (Join-Path $PSScriptRoot "../dapr")).Path
}

if (-not (Test-CommandAvailable "kubectl")) {
    Write-Error "kubectl is not on PATH. Install kubectl before running deploy-local."
    exit 3
}

# ---------------------------------------------------------------------------
# Step 1: Verify local context
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
    Write-Error "No active kubectl context. Set a local-cluster context before running deploy-local."
    exit 2
}

if (-not (Test-LocalContext -Context $currentContext)) {
    Write-Host "ERROR: Refusing to deploy against non-local kubectl context '$currentContext'."
    Write-Host "Allowed context patterns: kind-*, k3d-*, minikube, docker-desktop"
    Write-Host "Switch context with: kubectl config use-context <local-context-name>"
    exit 2
}

Write-Host "Active kubectl context: $currentContext (local-cluster allowlist OK)"

# ---------------------------------------------------------------------------
# Step 2: DAPR control plane install (optional)
# ---------------------------------------------------------------------------
if (-not $SkipDaprInit) {
    if (-not (Test-CommandAvailable "dapr")) {
        $daprMissingMessage = "dapr CLI is not on PATH. The deploy script needs it to ensure the DAPR control plane is installed on '$currentContext'. Install dapr (see https://docs.dapr.io/getting-started/install-dapr-cli/) or re-run with -SkipDaprInit if you have already provisioned the control plane out-of-band."
        Write-Error $daprMissingMessage
        exit 3
    }
    else {
        Write-Host "Checking DAPR control plane on cluster..."
        $daprStatus = & dapr status -k 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0 -or $daprStatus -notmatch "dapr-operator") {
            Write-Host "DAPR control plane not detected; running 'dapr init -k'..."
            & dapr init -k --wait
            if ($LASTEXITCODE -ne 0) {
                Write-Error "dapr init -k failed (exit $LASTEXITCODE)."
                exit 1
            }
        }
        else {
            Write-Host "DAPR control plane already present."
        }
    }
}

# Ensure the deploy namespace exists before any namespaced dry-run / Secret write.
# Tolerate `AlreadyExists` errors on the create path (CI parallelism race).
if (-not $script:NamespaceEnsured) {
    & kubectl get namespace $Namespace 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $createOutput = & kubectl create namespace $Namespace 2>&1
        if ($LASTEXITCODE -ne 0 -and ($createOutput -notmatch 'AlreadyExists')) {
            Write-Error "kubectl create namespace $Namespace failed (exit $LASTEXITCODE): $createOutput"
            exit 1
        }
    }
    $script:NamespaceEnsured = $true
}

# ---------------------------------------------------------------------------
# Step 2a (Story 9.3 AC6): server-side dry-run apply for `deploy/dapr/resiliency.yaml`.
# Catches Dapr CRD schema drift fast — any field-name mismatch with the active
# `resiliencies.dapr.io` CRD on the cluster surfaces here, BEFORE any namespaced
# resource is written. Exits 1 if the dry-run rejects the manifest.
# Sequenced BEFORE Step 2b (Secret bootstrap) per Story 9.3 spec § AC4 / Task 5 — a
# schema-regression dry-run failure must abort before any namespaced resource is written.
# ---------------------------------------------------------------------------
$resiliencyPath = Join-Path $DaprComponentsPath "resiliency.yaml"
if (Test-Path $resiliencyPath) {
    Write-Host ""
    Write-Host "Pre-flight: kubectl apply --dry-run=server -f deploy/dapr/resiliency.yaml"
    $dryRunOutput = & kubectl apply --dry-run=server -f $resiliencyPath -n $Namespace 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: resiliency.yaml schema dry-run failed (exit $LASTEXITCODE)."
        Write-Host "This usually means the active Dapr `resiliencies.dapr.io` CRD on '$currentContext' differs"
        Write-Host "from the schema deploy/dapr/resiliency.yaml targets (Dapr 1.14.4 per Story 9.3 AC6)."
        Write-Host "See _bmad-output/implementation-artifacts/9-3-close-k8s-deployment-spec-gaps.md AC6."
        $dryRunOutput | Select-Object -First 30 | ForEach-Object { Write-Host "  $_" }
        exit 1
    }
    else {
        # Bounded passthrough — emit the resource kind/name line only.
        $dryRunOutput | Where-Object { $_ -match '^\S+/\S+\s+\S+' } | ForEach-Object { Write-Host "  $_" }
    }
}

# ---------------------------------------------------------------------------
# Step 2b (Story 9.3 AC4): bootstrap operator-managed Secrets — JWT signing key + Keycloak
# admin password. Both are generated from a cryptographically random source on the local
# operator and applied via `kubectl apply --dry-run=client -o yaml | kubectl apply -f -`
# (idempotent: first invocation creates, subsequent invocations leave existing Secrets in
# place — `kubectl apply` is a server-side merge, not a re-create).
#
# Output is metadata-only: "Secret <name> applied" or "Secret <name> already present". The
# random-value never appears in stdout / stderr / Get-Content output.
# ---------------------------------------------------------------------------
function New-RandomBase64Key {
    param([int]$ByteLength = 32)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $bytes = New-Object byte[] $ByteLength
        $rng.GetBytes($bytes)
        return [System.Convert]::ToBase64String($bytes)
    }
    finally {
        $rng.Dispose()
    }
}

function Set-OperatorSecretIfMissing {
    param(
        [string]$SecretName,
        [string]$KeyName,
        [int]$ByteLength = 32
    )
    & kubectl get secret $SecretName --namespace $Namespace 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Secret $SecretName already present."
        return
    }
    # Defer random-value computation until we know the Secret is missing — avoids burning
    # CSRNG entropy and keeping a sensitive string on the heap on idempotent re-runs.
    $value = New-RandomBase64Key -ByteLength $ByteLength
    $manifest = & kubectl create secret generic $SecretName `
        --namespace $Namespace `
        --from-literal "$KeyName=$value" `
        --dry-run=client `
        -o yaml
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to render Secret manifest for $SecretName (exit $LASTEXITCODE)."
        exit 1
    }
    $manifest | & kubectl apply -f - 2>&1 | ForEach-Object {
        if ($_ -match '^secret/\S+\s+\S+') {
            Write-Host "  applied: $_"
        }
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Error "kubectl apply failed for Secret $SecretName (exit $LASTEXITCODE)."
        exit 1
    }
}

Write-Host ""
Write-Host "Bootstrapping operator-managed Secrets..."
Set-OperatorSecretIfMissing `
    -SecretName 'hexalith-jwt-signing' `
    -KeyName 'Authentication__JwtBearer__SigningKey' `
    -ByteLength 32
Set-OperatorSecretIfMissing `
    -SecretName 'hexalith-keycloak-admin' `
    -KeyName 'KEYCLOAK_ADMIN_PASSWORD' `
    -ByteLength 24

# ---------------------------------------------------------------------------
# Step 3: Apply authoritative DAPR component CRs (deploy/dapr/)
# ---------------------------------------------------------------------------
$daprApplied = 0
$daprSkipped = @()
if (Test-Path $DaprComponentsPath) {
    Write-Host ""
    Write-Host "Applying authoritative DAPR component CRs from: $DaprComponentsPath"
    foreach ($yamlFile in Get-ChildItem -Path $DaprComponentsPath -Filter "*.yaml" -File) {
        # Skip files that require out-of-MVP CRDs or are alternative backends:
        #   - statestore-*.yaml / pubsub-*.yaml: alternative-backend templates;
        #     operator opts in by overwriting statestore.yaml / pubsub.yaml.
        #   - topology.yaml / tenants-integration.yaml: Hexalith CRDs
        #     (apiVersion hexalith.io/v1). The CRDs are out of MVP scope for
        #     story 9-1 (deferred to story 9.3+); applying without the CRDs
        #     present would fail with 'no matches for kind' and abort the deploy.
        if ($yamlFile.Name -match '^(statestore|pubsub)-[a-z0-9].*\.yaml$' -or
            $yamlFile.Name -eq 'topology.yaml' -or
            $yamlFile.Name -eq 'tenants-integration.yaml') {
            $daprSkipped += $yamlFile.Name
            continue
        }

        # Ensure target namespace exists before applying namespace-scoped DAPR CRs.
        if (-not $script:NamespaceEnsured) {
            & kubectl get namespace $Namespace 2>$null | Out-Null
            if ($LASTEXITCODE -ne 0) {
                & kubectl create namespace $Namespace 2>&1 | Out-Null
            }
            $script:NamespaceEnsured = $true
        }

        & kubectl apply -f $yamlFile.FullName -n $Namespace 2>&1 | ForEach-Object {
            if ($_ -match '^(component\.dapr\.io|configuration\.dapr\.io|subscription\.dapr\.io)/[^\s]+\s+(created|configured|unchanged)') {
                Write-Host "  applied: $_"
                $script:daprApplied++
            }
            elseif ($_ -match '^error') {
                Write-Host "  ERROR: $_"
            }
            else {
                # Bounded passthrough — emit kind/name lines only.
                if ($_ -match '^\S+/\S+\s+\S+') {
                    Write-Host "  $_"
                }
            }
        }
        if ($LASTEXITCODE -ne 0) {
            Write-Error "kubectl apply failed for $($yamlFile.Name) (exit $LASTEXITCODE)."
            exit 1
        }
    }
    if ($daprSkipped.Count -gt 0) {
        Write-Host "Skipped alternative-backend templates: $($daprSkipped -join ', ')"
    }
}
else {
    Write-Host "WARNING: $DaprComponentsPath not found; skipping DAPR component apply."
}

# ---------------------------------------------------------------------------
# Step 4: Apply aspirate-generated manifests under deploy/k8s/
# ---------------------------------------------------------------------------
if (-not (Test-Path (Join-Path $ManifestPath "kustomization.yaml"))) {
    Write-Error "Manifest path '$ManifestPath' does not contain kustomization.yaml. Run regen.ps1 first."
    exit 1
}

Write-Host ""
Write-Host "Applying Kubernetes manifests via kustomize: $ManifestPath"
$applyOutput = & kubectl apply -k $ManifestPath 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: kubectl apply -k '$ManifestPath' exited $LASTEXITCODE."
    # Emit a bounded summary of the failure without leaking secrets.
    $applyOutput | Select-Object -First 50 | ForEach-Object { Write-Host "  $_" }
    exit 1
}

# Bounded summary: count kinds applied.
$counts = @{}
foreach ($line in $applyOutput) {
    if ($line -match '^(\S+)/(\S+)\s+(created|configured|unchanged)') {
        $kind = $matches[1]
        if (-not $counts.ContainsKey($kind)) { $counts[$kind] = 0 }
        $counts[$kind]++
    }
}

Write-Host ""
Write-Host "Apply summary:"
foreach ($kind in ($counts.Keys | Sort-Object)) {
    Write-Host ("  {0,-40} {1}" -f $kind, $counts[$kind])
}
Write-Host "  DAPR components (deploy/dapr/) applied: $daprApplied"
Write-Host ""
Write-Host "Done. Check readiness with: kubectl get pods -n $Namespace"
exit 0
