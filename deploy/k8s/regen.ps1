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
# Story 9.3 AC4 / ADR 9.3-3: `keycloak/` is a hand-authored carve-out (path b) — aspirate
# cannot emit Keycloak with a stable `secretKeyRef` admin password + realm import, so the
# subfolder is preserved here and excluded from the byte-determinism contract on that subset
# only (presence-only assertion, documented in `deploy/k8s/README.md`).
# Story 9.3 AC5: `redis/` ships as a hand-authored carve-out under the same treatment —
# aspirate 9.1.0 `AddRedis()` translation is not yet verified against the local-cluster
# MVP topology; the hand-authored Deployment + Service with `emptyDir` volume is the
# deterministic committed surface that the Dapr Components reference at `redis:6379`.
$PreservedNames = @("regen.ps1", "deploy-local.ps1", "teardown-local.ps1", "README.md", "keycloak", "redis")
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
    # Story 9.3 — ensure the hand-authored carve-out and AppHost-composed sub-folders are
    # referenced from the top-level kustomization. The top-level file is regenerated by
    # aspirate on every run, so the additions must be applied idempotently here.
    # `memories` and `redis` are AppHost-composed and aspirate typically emits them, but
    # appending defensively avoids drift if aspirate's emission lags. `keycloak` is the
    # hand-authored carve-out (Story 9.3 ADR 9.3-3) and is never emitted by aspirate.
    $kustomizationCarveOuts = @('memories', 'redis', 'keycloak')
    $filteredArray = @($filteredLines)
    foreach ($entry in $kustomizationCarveOuts) {
        $entryRegex = '^\s*-\s+' + [regex]::Escape($entry) + '\s*$'
        if (-not ($filteredArray | Where-Object { $_ -match $entryRegex })) {
            $filteredArray += "- $entry"
        }
    }
    Set-Content -LiteralPath $kustomizationPath -Value $filteredArray
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
    # Story 9.3 AC2 — Memories.Server composed in-cluster with deny-by-default ACL.
    'memories'         = 'accesscontrol-memories'
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
# Post-aspirate patch: JWT SigningKey -> secretKeyRef envFrom on consumer Deployments.
# ---------------------------------------------------------------------------
# Story 9.3 AC4 / ADR 9.3-3. When `EnableKeycloak=true` is passed to `regen.ps1`, aspirate
# emits `Authentication__JwtBearer__SigningKey` as a literal-empty env entry on the consumer
# Deployments (a leftover of the AppHost's `WithEnvironment(...., "")` lines). The literal
# (even empty) violates the no-secret-values contract — the lint
# `K8sSecret-JwtSigningKeyLiteral` is enforced regardless of value, and the operator-facing
# semantics REQUIRE the signing key come from a Secret bootstrapped by `deploy-local.ps1`.
#
# This block walks each consumer Deployment, locates the container `envFrom:` (or `env:`)
# block, and ensures an explicit `env:` entry exists for `Authentication__JwtBearer__SigningKey`
# sourced via `valueFrom.secretKeyRef` pointing at the `hexalith-jwt-signing` Secret.
#
# Idempotency contract (must be preserved — Story 9.3 AC7 patch-idempotency test):
#   (a) anchor on the literal env-var name `Authentication__JwtBearer__SigningKey`;
#   (b) if a `valueFrom.secretKeyRef` sibling already references `hexalith-jwt-signing`
#       for that env var, no-op (second `regen.ps1` invocation produces zero diff);
#   (c) if the env var is absent from the Deployment entirely, no-op (the consumer Deployment
#       does not need the signing key — `EnableKeycloak=false` default path).
# The pattern differs from the Dapr annotation patch (which uses single-line anchors): the
# JWT env entry spans multiple YAML lines (`name:`, `valueFrom:`, `secretKeyRef:`, `name:`,
# `key:`). The patching uses a multi-line regex anchored on `- name: Authentication__JwtBearer__SigningKey`.
$JwtConsumerAppIds = @('eventstore', 'eventstore-admin', 'parties', 'parties-mcp', 'tenants')
$JwtSecretName = 'hexalith-jwt-signing'
$JwtKeyName = 'Authentication__JwtBearer__SigningKey'
# Strict sibling-check regex: secretKeyRef AND matching name must follow the JWT env entry
# within the same multi-line YAML structure. Used as the idempotency anchor (no-op on re-run).
$siblingPattern = '(?ms)- name:\s*' + [regex]::Escape($JwtKeyName) +
    '\s*\r?\n\s+valueFrom:\s*\r?\n\s+secretKeyRef:\s*\r?\n\s+name:\s*' + [regex]::Escape($JwtSecretName)
# Literal-shape regex: matches the `- name: <key>` + `value: ""` (or `''`) pair aspirate emits.
# Captures indent on group 1 so the replacement preserves it (works across 4/6/8-space styles).
$literalShapePattern = '(?m)^([ \t]+)- name:\s*' + [regex]::Escape($JwtKeyName) +
    '\s*\r?\n[ \t]+value:\s*(?:""|'''')\s*\r?\n'
foreach ($appId in $JwtConsumerAppIds) {
    $deploymentPath = Join-Path $OutputDir (Join-Path $appId "deployment.yaml")
    if (-not (Test-Path $deploymentPath)) { continue }
    $deploymentText = Get-Content -Raw -LiteralPath $deploymentPath

    # (a) Idempotency — strict sibling check. If the secretKeyRef block already targets the
    # JWT env var, no-op (second-run zero-diff). The previous loose two-substring check could
    # false-pass on partially-patched files; this anchored multi-line regex closes that hole.
    if ($deploymentText -match $siblingPattern) {
        continue
    }

    # (b) If the env var is absent entirely, no-op — Keycloak was disabled this regen.
    if ($deploymentText -notmatch [regex]::Escape($JwtKeyName)) {
        continue
    }

    # (c) Replace path (preferred). Aspirate emits the env var as a `- name: <key>` + `value: ""`
    # pair. Replace that pair with a secretKeyRef block at the same indent — no duplicate keys,
    # no extra env: header, no risk of injecting into sidecars/init-containers.
    if ($deploymentText -match $literalShapePattern) {
        $deploymentText = [regex]::Replace(
            $deploymentText,
            $literalShapePattern,
            {
                param($m)
                $indent = $m.Groups[1].Value
                $childIndent = $indent + '  '
                return "$indent- name: $JwtKeyName`r`n" +
                       "$childIndent  valueFrom:`r`n" +
                       "$childIndent    secretKeyRef:`r`n" +
                       "$childIndent      name: $JwtSecretName`r`n" +
                       "$childIndent      key: $JwtKeyName`r`n"
            },
            1)
        Set-Content -LiteralPath $deploymentPath -Value $deploymentText -NoNewline
        continue
    }

    # (d) Fallback. The env var is referenced (e.g., via envFrom.configMapRef) but the literal
    # `value: ""` shape did not match. Insert an explicit env: block before the FIRST envFrom:
    # only (count=1). This narrows the previous global -replace which would also inject into
    # sidecar / init-container envFrom: blocks.
    $envFromMatch = [regex]::Match($deploymentText, '(?m)^([ \t]+)envFrom:')
    if ($envFromMatch.Success) {
        $indent = $envFromMatch.Groups[1].Value
        $envEntry = "$indent" + "env:`r`n" +
            "$indent- name: $JwtKeyName`r`n" +
            "$indent  valueFrom:`r`n" +
            "$indent    secretKeyRef:`r`n" +
            "$indent      name: $JwtSecretName`r`n" +
            "$indent      key: $JwtKeyName`r`n"
        $deploymentText = $deploymentText.Substring(0, $envFromMatch.Index) +
            $envEntry +
            $deploymentText.Substring($envFromMatch.Index)
    }
    Set-Content -LiteralPath $deploymentPath -Value $deploymentText -NoNewline
}

# ---------------------------------------------------------------------------
# Post-condition: assert aspirate emitted the expected per-app folders.
# ---------------------------------------------------------------------------
# Aspirate can exit 0 while emitting an empty or partial set if the AppHost
# composition is broken in a way that does not throw (e.g. zero AddProject
# calls). The deploy script would then fail later with an obscure kustomize
# error. Fail fast here with a clear message.
# Story 9.3 — Topology contract: aspirate must emit one folder per AppHost-composed service.
# `memories` and `redis` are AppHost-composed (Tasks 3 / 6); `keycloak` is hand-authored under
# the path-(b) carve-out (Task 5 / ADR 9.3-3) and preserved by `$PreservedNames` above.
# FrontComposer is intentionally absent — Story 9.3 Outcome B carves it out to Story 9.4
# `9-4-frontcomposer-deployable-host`.
$ExpectedAppFolders = @('eventstore', 'eventstore-admin', 'eventstore-admin-ui', 'parties', 'parties-mcp', 'tenants', 'memories', 'redis', 'keycloak')
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
