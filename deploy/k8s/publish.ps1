#!/usr/bin/env pwsh
# ===========================================================================
# Hexalith.Parties -- Kubernetes Publish Pipeline (Zot + MinVer + apply)
# ===========================================================================
# One-command pipeline that subsumes the former regen.ps1 + deploy-local.ps1
# pair. Resolves the MinVer version, builds and pushes container images to
# the Zot registry at registry.hexalith.com, regenerates deploy/k8s/ from the
# Aspire AppHost via aspirate, patches DAPR annotations / JWT secretKeyRef /
# imagePullSecrets onto consumer Deployments, bootstraps operator-managed
# Secrets (including zot-pull-secret), applies authoritative DAPR component
# CRs, and applies the full topology via kustomize.
#
# Behavior:
#   0. Verify -ConfirmContext matches `kubectl config current-context` exactly
#      (no local-cluster regex allowlist — ADR 9.5-2).
#   1. Resolve MinVer version from src/Hexalith.Parties.AppHost via
#      `dotnet msbuild -getProperty:MinVerVersion`. Validate SemVer shape.
#      Warn-and-proceed on dirty-tree marker.
#   2. Clean aspirate-emitted artefacts (preserve scripts + README + carve-outs).
#   3. `dotnet aspirate generate` without --skip-build; tag images with the
#      MinVer version and push them to registry.hexalith.com.
#   4. Post-aspirate placeholder strip + kustomization line filter.
#   5. Patch DAPR annotations (app-port + per-app dapr.io/config).
#   6. Patch JWT signing-key env to secretKeyRef on consumer Deployments.
#   7. Patch imagePullSecrets: [{ name: zot-pull-secret }] on every
#      registry.hexalith.com/* consumer Deployment.
#   8. Assert aspirate emitted every expected per-app folder.
#   9. Optionally `dapr init -k` (skip with -SkipDaprInit).
#  10. Ensure namespace exists; `kubectl apply --dry-run=server` for
#      deploy/dapr/resiliency.yaml (Story 9.3 AC6).
#  11. Bootstrap operator-managed Secrets: hexalith-jwt-signing,
#      hexalith-keycloak-admin, and zot-pull-secret (Path B — re-emit
#      ~/.docker/config.json auths entry wholesale, never decode).
#  12. Apply authoritative DAPR component CRs from deploy/dapr/.
#  13. `kubectl apply -k deploy/k8s/`.
#
# Output is metadata-only (kind + name + namespace per applied resource).
# No Secret values, ConfigMap data, credentials, or tokens are logged.
#
# Usage:
#   pwsh deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local
#   pwsh deploy/k8s/publish.ps1 -ConfirmContext kind-foo -SkipDaprInit
#
# Exit codes:
#   0 = pipeline succeeded
#   1 = apply / build / push step failed
#   2 = -ConfirmContext mismatch (or missing kubectl context)
#   3 = kubectl / dotnet / dapr CLI not available
#   4 = aspirate post-condition failed (missing expected app folder)
#   5 = MinVer version resolved to empty or non-SemVer value
#   6 = ~/.docker/config.json missing / malformed / no registry.hexalith.com
#       credentials / uses unsupported credsStore / credHelpers
#
# Reference: deploy/k8s/README.md, ADR 9.5-1 (Zot Registry as Image Substrate),
#            ADR 9.5-2 (mandatory -ConfirmContext replaces allowlist).
# ===========================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConfirmContext,

    [Parameter(Mandatory = $false)]
    [switch]$SkipDaprInit,

    [Parameter(Mandatory = $false)]
    [string]$Namespace = "hexalith-parties",

    [Parameter(Mandatory = $false)]
    [string]$DaprComponentsPath,

    [Parameter(Mandatory = $false)]
    [string]$ManifestPath = $PSScriptRoot,

    # Test-only shim per Story 9.5 AC1.1. When set, bypasses the
    # `dotnet msbuild -getProperty:MinVerVersion` call and feeds the override
    # through the same SemVer regex validation. Used exclusively by
    # K8sManifestPublishTests edge-case tests; never invoked in operator runs.
    [Parameter(Mandatory = $false)]
    [string]$MinVerVersionOverride
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:NamespaceEnsured = $false

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$AppHostDir = Join-Path $RepositoryRoot "src/Hexalith.Parties.AppHost"
$AppHostCsproj = Join-Path $AppHostDir "Hexalith.Parties.AppHost.csproj"
# $ManifestPath defaults to the script directory; tests can target a temp dir
# to avoid destroying the committed deploy/k8s/ tree during preserved-name clean.
$OutputDir = $ManifestPath

function Test-CommandAvailable {
    param([string]$Name)
    $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Exit-WithError {
    # Writes to stderr without triggering the $ErrorActionPreference = "Stop"
    # terminating-error path that Write-Error takes, then exits with the
    # specified code. Using Write-Error here would throw a terminating
    # exception that bypasses the explicit `exit` below and force PowerShell
    # to exit with code 1, defeating the contract that every exit code in this
    # script has a specific meaning (see the header comment-doc).
    param(
        [Parameter(Mandatory = $true)][string]$Message,
        [Parameter(Mandatory = $true)][int]$Code
    )
    [Console]::Error.WriteLine($Message)
    exit $Code
}

if (-not $DaprComponentsPath) {
    $DaprComponentsPath = (Resolve-Path (Join-Path $PSScriptRoot "../dapr")).Path
}

if (-not (Test-Path -LiteralPath $AppHostDir)) {
    Exit-WithError -Message "AppHost directory not found at '$AppHostDir'." -Code 2
}

# ---------------------------------------------------------------------------
# Step 0: Context gate (AC6) — mandatory -ConfirmContext must match the active
# kubectl context exactly. No regex allowlist (ADR 9.5-2).
# ---------------------------------------------------------------------------
if (-not (Test-CommandAvailable "kubectl")) {
    Exit-WithError -Message "kubectl is not on PATH. Install kubectl before running publish." -Code 3
}

$currentContext = $null
try {
    $currentContext = (& kubectl config current-context 2>$null).Trim()
}
catch {
    Exit-WithError -Message "Failed to read active kubectl context. Ensure a kubeconfig is configured." -Code 2
}

if ([string]::IsNullOrWhiteSpace($currentContext)) {
    Exit-WithError -Message "No active kubectl context. Set a context before running publish." -Code 2
}

if ($currentContext -cne $ConfirmContext) {
    Exit-WithError -Message "expected '$ConfirmContext', got '$currentContext'. Switch context with: kubectl config use-context $ConfirmContext" -Code 2
}

Write-Host "Active kubectl context: $currentContext (-ConfirmContext OK)"

# ---------------------------------------------------------------------------
# Step 1: MinVer resolution (AC1, AC4).
# ---------------------------------------------------------------------------
$semVerPattern = '^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?(?:\+[A-Za-z0-9.-]+)?$'
$MinVerVersion = $null

if ($PSBoundParameters.ContainsKey('MinVerVersionOverride')) {
    # Test-only path — operator never passes -MinVerVersionOverride. Any value
    # (including empty) is fed through the same SemVer validation below; that
    # is the explicit contract documented in AC1.1 and exercised by
    # K8sManifestPublishTests.PublishPs1_EmptyMinVerOverride_ExitsWith5.
    Write-Host "MinVer override active (test-only shim): '$MinVerVersionOverride'"
    $MinVerVersion = if ($null -eq $MinVerVersionOverride) { "" } else { $MinVerVersionOverride.Trim() }
}
else {
    if (-not (Test-CommandAvailable "dotnet")) {
        Exit-WithError -Message "dotnet is not on PATH. Install the .NET SDK before running publish." -Code 3
    }
    if (-not (Test-Path -LiteralPath $AppHostCsproj)) {
        Exit-WithError -Message "AppHost csproj not found at '$AppHostCsproj'." -Code 5
    }
    # -t:MinVer forces the MinVer target to populate $(MinVerVersion); without it,
    # `-getProperty` returns empty because MinVer's target is not in the default
    # dependency chain.
    $msbuildOutput = & dotnet msbuild $AppHostCsproj -t:MinVer -getProperty:MinVerVersion -nologo -v:q 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: dotnet msbuild -getProperty:MinVerVersion failed (exit $LASTEXITCODE)."
        $msbuildOutput | Select-Object -Last 30 | ForEach-Object { Write-Host "  $_" }
        exit 5
    }
    $MinVerVersion = ($msbuildOutput -join "`n").Trim()
}

if ([string]::IsNullOrWhiteSpace($MinVerVersion) -or ($MinVerVersion -notmatch $semVerPattern)) {
    Exit-WithError -Message "MinVer version resolved to '$MinVerVersion' — expected SemVer per MinVerTagPrefix=v in Directory.Build.props. See https://github.com/adamralph/minver for guidance." -Code 5
}

# Dirty-tree warn-and-proceed. MinVer appends `+dirty` or `+<git-sha>-dirty`
# when `git status` is non-empty at build time. Refusal logic is deferred to
# the Story 9.6 CI gate.
if ($MinVerVersion -match '\+.*dirty') {
    Write-Warning "MinVer resolved to '$MinVerVersion' — working tree is dirty. Image tag may reference a build that only exists on this workstation; do not commit the resulting deploy/k8s/<app-id>/deployment.yaml diff."
}

Write-Host "MinVer version: $MinVerVersion"
Write-Host "Namespace: $Namespace"
Write-Host "Registry: registry.hexalith.com"

# ---------------------------------------------------------------------------
# Step 2: Preserved-name clean (AC5, AC7).
# ---------------------------------------------------------------------------
# Story 9.3 AC4 / ADR 9.3-3: `keycloak/` is a hand-authored carve-out (path b) — aspirate
# cannot emit Keycloak with a stable `secretKeyRef` admin password + realm import.
# Story 9.3 AC5: `redis/` is a hand-authored carve-out (aspirate 9.1.0 `AddRedis()` not
# verified). Story 9.5 drops regen.ps1 + deploy-local.ps1 from $PreservedNames (deleted)
# and adds publish.ps1 + teardown.ps1.
$PreservedNames = @("publish.ps1", "teardown.ps1", "README.md", "namespace.yaml", "keycloak", "redis")
Get-ChildItem -Path $OutputDir -Force | Where-Object {
    $PreservedNames -notcontains $_.Name
} | ForEach-Object {
    Remove-Item -Recurse -Force -LiteralPath $_.FullName
}

# ---------------------------------------------------------------------------
# Step 3: Aspirate generate + push (AC1, AC4) — no --skip-build; image tags
# from MinVer; push to registry.hexalith.com via aspirate's built-in publish.
# ---------------------------------------------------------------------------
Push-Location $AppHostDir
try {
    # Aspirate captures Keycloak's randomized admin bootstrap into the emitted
    # ConfigMap when EnableKeycloak=true; the hand-authored deploy/k8s/keycloak/
    # carve-out preserves the deterministic stable admin password contract.
    $env:EnableKeycloak = "false"
    & dotnet aspirate generate `
        --output-path $OutputDir `
        --non-interactive `
        --image-pull-policy IfNotPresent `
        --container-registry registry.hexalith.com `
        --container-image-tag $MinVerVersion `
        --output-format kustomize `
        --disable-secrets `
        --disable-state `
        --include-dashboard false `
        --namespace $Namespace
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: dotnet aspirate generate exited with code $LASTEXITCODE."
        exit $LASTEXITCODE
    }

    # Aspirate writes an intermediate manifest.json next to the AppHost; remove it.
    $intermediateManifest = Join-Path $AppHostDir "manifest.json"
    if (Test-Path -LiteralPath $intermediateManifest) {
        Remove-Item -Force -LiteralPath $intermediateManifest
    }
}
finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# Step 4: Post-aspirate placeholder strip + kustomization filter (AC7).
# ---------------------------------------------------------------------------
# Aspirate 9.1.0 emits `deploy/k8s/dapr/{statestore,pubsub}.yaml` as name-only
# placeholders that would override the authoritative deploy/dapr/ components.
# Behavior ported verbatim from regen.ps1:106-161.
$placeholders = @(
    Join-Path $OutputDir "dapr/statestore.yaml"
    Join-Path $OutputDir "dapr/pubsub.yaml"
)
foreach ($placeholder in $placeholders) {
    if (Test-Path -LiteralPath $placeholder) {
        Remove-Item -Force -LiteralPath $placeholder
    }
}

$kustomizationPath = Join-Path $OutputDir "kustomization.yaml"
if (Test-Path -LiteralPath $kustomizationPath) {
    $kustomizationLines = Get-Content -LiteralPath $kustomizationPath
    $filteredLines = $kustomizationLines | Where-Object {
        $_ -notmatch '^\s*-\s+dapr/statestore\.yaml\s*$' -and
        $_ -notmatch '^\s*-\s+dapr/pubsub\.yaml\s*$'
    }
    # Story 9.3 — idempotent carve-out append. `memories` and `redis` are AppHost-composed,
    # `keycloak` is hand-authored. All three must remain referenced from the top-level
    # kustomization across regens.
    # Story 9.5 fix: insert missing carve-outs INSIDE the `resources:` list, not at end
    # of file. Aspirate 9.1.0 emits `generatorOptions:` after `resources:`; appending at
    # end places a bare `- entry` list item outside any key → YAML parse error.
    $kustomizationCarveOuts = @('memories', 'redis', 'keycloak')
    $linesIn = @($filteredLines)
    $out = New-Object System.Collections.Generic.List[string]
    $inResources = $false
    $resourcesAlreadyHas = @{}
    # First pass — collect what's already present in resources: so the second-pass insert
    # doesn't duplicate. Anchored to `^resources:` and the indented list items below it.
    foreach ($line in $linesIn) {
        if ($line -match '^resources:\s*$') { $inResources = $true; continue }
        if ($inResources) {
            if ($line -match '^\s*-\s+([^\s#][^\s#]*)\s*$') {
                $resourcesAlreadyHas[$Matches[1]] = $true
            }
            elseif ($line -match '^[A-Za-z]') { $inResources = $false }
        }
    }
    # Second pass — re-emit the file, inserting missing carve-outs just before the line
    # that closes the resources: block (the first non-list, non-blank line at column 0).
    $inResources = $false
    $insertedCarveOuts = $false
    foreach ($line in $linesIn) {
        if ($line -match '^resources:\s*$') {
            $inResources = $true
            $out.Add($line)
            continue
        }
        if ($inResources -and $line -match '^[A-Za-z]') {
            # Closing the resources block — insert missing carve-outs first.
            foreach ($entry in $kustomizationCarveOuts) {
                if (-not $resourcesAlreadyHas.ContainsKey($entry)) {
                    $out.Add("- $entry")
                }
            }
            $insertedCarveOuts = $true
            $inResources = $false
        }
        $out.Add($line)
    }
    # If we never closed the resources block (EOF inside it), append missing carve-outs.
    if ($inResources -and -not $insertedCarveOuts) {
        foreach ($entry in $kustomizationCarveOuts) {
            if (-not $resourcesAlreadyHas.ContainsKey($entry)) {
                $out.Add("- $entry")
            }
        }
    }
    Set-Content -LiteralPath $kustomizationPath -Value $out
}

$daprDir = Join-Path $OutputDir "dapr"
if ((Test-Path -LiteralPath $daprDir) -and (-not (Get-ChildItem -Path $daprDir -Force))) {
    Remove-Item -Force -LiteralPath $daprDir
}

# ---------------------------------------------------------------------------
# Step 5: Dapr annotation patch (AC7) — ported verbatim from regen.ps1:163-203.
# ---------------------------------------------------------------------------
$DaprAppConfigMap = @{
    'eventstore'       = 'accesscontrol'
    'eventstore-admin' = 'accesscontrol-eventstore-admin'
    'parties'          = 'accesscontrol-parties'
    'tenants'          = 'accesscontrol-tenants'
    'memories'         = 'accesscontrol-memories'
}
foreach ($appId in $DaprAppConfigMap.Keys) {
    $deploymentPath = Join-Path $OutputDir (Join-Path $appId "deployment.yaml")
    if (-not (Test-Path -LiteralPath $deploymentPath)) { continue }
    $configName = $DaprAppConfigMap[$appId]
    $deploymentText = Get-Content -Raw -LiteralPath $deploymentPath
    $deploymentText = $deploymentText -replace 'dapr\.io/config: tracing', "dapr.io/config: $configName"
    # Idempotency anchor: only insert `dapr.io/app-port` if it does not already
    # follow `dapr.io/app-id: $appId` at the same indent. Without this guard the
    # `-replace` below would re-insert the line on every publish (the source
    # anchor still matches its own replacement). Pattern asserts the next line
    # is NOT `dapr.io/app-port`.
    $appPortAnchor = "(?m)^([ \t]*)dapr\.io/app-id: $([regex]::Escape($appId))\s*\r?\n[ \t]*dapr\.io/app-port:"
    if ($deploymentText -notmatch $appPortAnchor) {
        $deploymentText = $deploymentText -replace `
            "(?m)^([ \t]*)dapr\.io/app-id: $([regex]::Escape($appId))(\r?\n)", `
            "`$1dapr.io/app-id: $appId`$2`$1dapr.io/app-port: '8080'`$2"
    }
    Set-Content -LiteralPath $deploymentPath -Value $deploymentText -NoNewline
}

# ---------------------------------------------------------------------------
# Step 6: JWT SigningKey -> secretKeyRef envFrom patch (AC7) — ported verbatim
# from regen.ps1:205-296.
# ---------------------------------------------------------------------------
# Idempotency contract (Story 9.3 AC7): strict sibling-check anchor — if a
# `valueFrom.secretKeyRef` block already references `hexalith-jwt-signing` for
# the JWT env key, no-op. Same anchor + replace pattern used by regen.ps1.
$JwtConsumerAppIds = @('eventstore', 'eventstore-admin', 'parties', 'parties-mcp', 'tenants')
$JwtSecretName = 'hexalith-jwt-signing'
$JwtKeyName = 'Authentication__JwtBearer__SigningKey'
$siblingPattern = '(?ms)- name:\s*' + [regex]::Escape($JwtKeyName) +
    '\s*\r?\n\s+valueFrom:\s*\r?\n\s+secretKeyRef:\s*\r?\n\s+name:\s*' + [regex]::Escape($JwtSecretName)
$literalShapePattern = '(?m)^([ \t]+)- name:\s*' + [regex]::Escape($JwtKeyName) +
    '\s*\r?\n[ \t]+value:\s*(?:""|'''')\s*\r?\n'
foreach ($appId in $JwtConsumerAppIds) {
    $deploymentPath = Join-Path $OutputDir (Join-Path $appId "deployment.yaml")
    if (-not (Test-Path -LiteralPath $deploymentPath)) { continue }
    $deploymentText = Get-Content -Raw -LiteralPath $deploymentPath

    if ($deploymentText -match $siblingPattern) {
        continue
    }
    if ($deploymentText -notmatch [regex]::Escape($JwtKeyName)) {
        continue
    }

    # T16: detect non-empty `value:` shapes (e.g., AppHost emits
    # `value: 'dev-only-placeholder'`). The empty-value pattern below would miss
    # them and the envFrom fallback below would insert a NEW `env:` block,
    # leaving the original env entry in place — pod ends up with two
    # `Authentication__JwtBearer__SigningKey` entries, ordering-dependent.
    # Fail fast instead.
    $nonEmptyValuePattern = '(?m)^[ \t]+- name:\s*' + [regex]::Escape($JwtKeyName) +
        '\s*\r?\n[ \t]+value:\s*(?!(?:""|''''))\S'
    if ($deploymentText -match $nonEmptyValuePattern) {
        Exit-WithError -Message "AppHost emits a non-empty '$JwtKeyName' value: in $appId/deployment.yaml. publish.ps1 only knows how to patch the empty-value shape (value: '') into a secretKeyRef. Either set the AppHost env value to an empty string or pre-author the secretKeyRef. Aborting before partial patch." -Code 1
    }

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
# Step 7: imagePullSecrets patch (AC3, AC7) — NEW for Story 9.5.
# ---------------------------------------------------------------------------
# Inject `imagePullSecrets: [{ name: zot-pull-secret }]` into the pod-template
# spec of every Deployment whose container image starts with
# `registry.hexalith.com/`. Vendor-image carve-outs (`keycloak/`, `redis/`)
# pull from public registries and are excluded via the image-prefix check.
#
# Anchor strategy (mirrors Story 9.3 AC4 documentation style): use an
# indent-aware regex to locate the pod-template `spec:` (always at indent >= 2)
# followed by `containers:` at the next indent level. Insert the
# `imagePullSecrets:` block immediately BEFORE `containers:` at the same indent.
#
# Idempotency contract (AC7): if the literal `name: zot-pull-secret` already
# appears in the deployment text, no-op (second invocation produces zero diff).
$ConsumerDeployments = @('eventstore', 'eventstore-admin', 'eventstore-admin-ui', 'parties', 'parties-mcp', 'tenants', 'memories')
foreach ($appId in $ConsumerDeployments) {
    $deploymentPath = Join-Path $OutputDir (Join-Path $appId "deployment.yaml")
    if (-not (Test-Path -LiteralPath $deploymentPath)) { continue }
    $deploymentText = Get-Content -Raw -LiteralPath $deploymentPath

    # Image-prefix gate: only patch Deployments referencing registry.hexalith.com/* images.
    if ($deploymentText -notmatch '(?m)^\s+image:\s+registry\.hexalith\.com/') {
        continue
    }

    # Idempotency anchor (word-bounded): already patched on a prior invocation.
    # Use `name: zot-pull-secret` followed by end-of-line / whitespace / EOF so we
    # do not match partial-prefix collisions like `name: zot-pull-secret-staging`.
    if ($deploymentText -match '(?m)name:\s*zot-pull-secret(?:\s|$)') {
        continue
    }

    # Pod-template spec anchor: target `containers:` and walk back to the inner
    # `spec:` parent. Tolerates sibling keys (serviceAccountName, securityContext,
    # terminationGracePeriodSeconds, etc.) between `spec:` and `containers:` —
    # whereas the original regex required them to be immediately adjacent and
    # silently no-op'd otherwise (Story 9.5 review T7).
    $containersPattern = '(?m)^([ \t]+)containers:'
    $containersMatches = [regex]::Matches($deploymentText, $containersPattern)
    if ($containersMatches.Count -eq 0) { continue }

    # Take the FIRST occurrence as the pod-template containers: (outer
    # Deployment.spec has no `containers:` child — only template.spec does).
    $contMatch = $containersMatches[0]
    $contIndent = $contMatch.Groups[1].Value

    # Walk back from the match start to find the enclosing `spec:` line at a
    # strictly shallower indent. This is the pod-template spec.
    $prefix = $deploymentText.Substring(0, $contMatch.Index)
    $specPattern = "(?m)^([ \t]+)spec:[ \t]*\r?$"
    $specRegex = [regex]::new($specPattern)
    $specMatches = $specRegex.Matches($prefix)
    if ($specMatches.Count -eq 0) { continue }

    # The pod-template spec is the LAST `spec:` line before `containers:` whose
    # indent is shallower than the `containers:` indent.
    $podSpecMatch = $null
    for ($i = $specMatches.Count - 1; $i -ge 0; $i--) {
        $candIndent = $specMatches[$i].Groups[1].Value
        if ($candIndent.Length -lt $contIndent.Length) {
            $podSpecMatch = $specMatches[$i]
            break
        }
    }
    if ($null -eq $podSpecMatch) { continue }

    # Insert `imagePullSecrets:` block immediately BEFORE the `containers:` line
    # at the same indent.
    $insertion = "${contIndent}imagePullSecrets:`r`n${contIndent}- name: zot-pull-secret`r`n"
    $patched = $deploymentText.Substring(0, $contMatch.Index) + $insertion + $deploymentText.Substring($contMatch.Index)
    if ($patched -ne $deploymentText) {
        Set-Content -LiteralPath $deploymentPath -Value $patched -NoNewline
    }
}

# ---------------------------------------------------------------------------
# Step 8: Aspirate post-condition (AC1) — assert every expected per-app folder
# exists. Catches aspirate exit-0-with-empty-composition.
# ---------------------------------------------------------------------------
$ExpectedAppFolders = @('eventstore', 'eventstore-admin', 'eventstore-admin-ui', 'parties', 'parties-mcp', 'tenants', 'memories', 'redis', 'keycloak')
$missingFolders = @()
foreach ($expected in $ExpectedAppFolders) {
    if (-not (Test-Path -LiteralPath (Join-Path $OutputDir $expected))) {
        $missingFolders += $expected
    }
}
if ($missingFolders.Count -gt 0) {
    $missingList = $missingFolders -join ', '
    Exit-WithError -Message "Aspirate generation post-condition failed. Missing expected per-app folders: $missingList. Check src/Hexalith.Parties.AppHost/Program.cs for AddProject<...> registrations and rerun publish.ps1. This guards against aspirate exit-0 with an empty composition." -Code 4
}

# ---------------------------------------------------------------------------
# Step 9: DAPR control plane install (optional, -SkipDaprInit gated).
# Ported from deploy-local.ps1:122-144.
# ---------------------------------------------------------------------------
if (-not $SkipDaprInit) {
    if (-not (Test-CommandAvailable "dapr")) {
        Exit-WithError -Message "dapr CLI is not on PATH. Install dapr (https://docs.dapr.io/getting-started/install-dapr-cli/) or re-run with -SkipDaprInit if the control plane is already provisioned." -Code 3
    }
    Write-Host "Checking DAPR control plane on cluster..."
    $daprStatus = & dapr status -k 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -or $daprStatus -notmatch "dapr-operator") {
        Write-Host "DAPR control plane not detected; running 'dapr init -k'..."
        & dapr init -k --wait
        if ($LASTEXITCODE -ne 0) {
            Exit-WithError -Message "dapr init -k failed (exit $LASTEXITCODE)." -Code 1
        }
    }
    else {
        Write-Host "DAPR control plane already present."
    }
}

# ---------------------------------------------------------------------------
# Step 10: Namespace ensure + resiliency dry-run (AC8).
# Ported from deploy-local.ps1:146-185.
# ---------------------------------------------------------------------------
function Ensure-Namespace {
    if ($script:NamespaceEnsured) { return }
    & kubectl get namespace $Namespace 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $createOutput = & kubectl create namespace $Namespace 2>&1
        if ($LASTEXITCODE -ne 0 -and ($createOutput -notmatch 'AlreadyExists')) {
            Exit-WithError -Message "kubectl create namespace $Namespace failed (exit $LASTEXITCODE): $createOutput" -Code 1
        }
    }
    $script:NamespaceEnsured = $true
}

Ensure-Namespace

$resiliencyPath = Join-Path $DaprComponentsPath "resiliency.yaml"
if (Test-Path -LiteralPath $resiliencyPath) {
    Write-Host ""
    Write-Host "Pre-flight: kubectl apply --dry-run=server -f deploy/dapr/resiliency.yaml"
    $dryRunOutput = & kubectl apply --dry-run=server -f $resiliencyPath -n $Namespace 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: resiliency.yaml schema dry-run failed (exit $LASTEXITCODE)."
        Write-Host "This usually means the active Dapr `resiliencies.dapr.io` CRD on '$currentContext' differs"
        Write-Host "from the schema deploy/dapr/resiliency.yaml targets (Dapr 1.14.4 per Story 9.3 AC6)."
        $dryRunOutput | Select-Object -First 30 | ForEach-Object { Write-Host "  $_" }
        exit 1
    }
    else {
        $dryRunOutput | Where-Object { $_ -match '^\S+/\S+\s+\S+' } | ForEach-Object { Write-Host "  $_" }
    }
}

# ---------------------------------------------------------------------------
# Step 11: Operator-managed Secret bootstrap (AC2, AC8).
# Extends Story 9.3 AC4 Secret bootstrap to include zot-pull-secret.
# zot-pull-secret uses Path B exclusively: re-emit ~/.docker/config.json
# auths["registry.hexalith.com"] block wholesale into the dockerconfigjson
# Secret without decoding the `auth` field. Avoids argv exposure
# (--docker-password) and never decodes the credential string.
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
    # Defer random-value computation until we know the Secret is missing.
    $value = New-RandomBase64Key -ByteLength $ByteLength
    $manifest = & kubectl create secret generic $SecretName `
        --namespace $Namespace `
        --from-literal "$KeyName=$value" `
        --dry-run=client `
        -o yaml
    if ($LASTEXITCODE -ne 0) {
        Exit-WithError -Message "Failed to render Secret manifest for $SecretName (exit $LASTEXITCODE)." -Code 1
    }
    $manifest | & kubectl apply -f - 2>&1 | ForEach-Object {
        if ($_ -match '^secret/\S+\s+\S+') {
            Write-Host "  applied: $_"
        }
    }
    if ($LASTEXITCODE -ne 0) {
        Exit-WithError -Message "kubectl apply failed for Secret $SecretName (exit $LASTEXITCODE)." -Code 1
    }
}

function Set-ZotPullSecretIfMissing {
    # Idempotency anchor — check cluster state BEFORE reading docker config.
    # T14: verify type, not just existence — an `Opaque` Secret named
    # `zot-pull-secret` would silently satisfy the existence probe but produce
    # ImagePullBackOff at runtime because pods reference `.dockerconfigjson`.
    $existingType = (& kubectl get secret zot-pull-secret --namespace $Namespace -o "jsonpath={.type}" 2>$null)
    if ($LASTEXITCODE -eq 0) {
        if ($existingType -eq 'kubernetes.io/dockerconfigjson') {
            Write-Host "  Secret zot-pull-secret already present."
            return
        }
        Exit-WithError -Message "Secret zot-pull-secret already exists but has type '$existingType' (expected 'kubernetes.io/dockerconfigjson'). Delete it manually (kubectl delete secret zot-pull-secret -n $Namespace) and re-run publish.ps1." -Code 6
    }

    # Resolve docker config path (honor $env:DOCKER_CONFIG override).
    $dockerConfigPath = if ($env:DOCKER_CONFIG) {
        Join-Path $env:DOCKER_CONFIG 'config.json'
    }
    else {
        Join-Path ([System.Environment]::GetFolderPath('UserProfile')) '.docker/config.json'
    }

    if (-not (Test-Path -LiteralPath $dockerConfigPath)) {
        Exit-WithError -Message "no plain-text credentials for registry.hexalith.com in ~/.docker/config.json. Run: docker login -u parties-publisher registry.hexalith.com. publish.ps1 does not support Docker credential helpers (credsStore/credHelpers) in MVP — remove the directive or use `$env:DOCKER_CONFIG to point at a helper-free config." -Code 6
    }

    try {
        $configRaw = Get-Content -Raw -LiteralPath $dockerConfigPath -ErrorAction Stop
        $config = $configRaw | ConvertFrom-Json -AsHashtable -ErrorAction Stop
    }
    catch {
        Exit-WithError -Message "~/.docker/config.json is malformed JSON at $dockerConfigPath" -Code 6
    }

    # T13: strict-mode guard — `ConvertFrom-Json -AsHashtable` returns `$null`
    # for literal `null` JSON, and Docker has shipped config-shape migrations
    # producing arrays / strings under top-level keys. Without this guard the
    # `.ContainsKey()` calls below raise `InvokeMethodOnNull` / `MethodNotFound`
    # under `Set-StrictMode`, exit 1 with a stack trace instead of the
    # documented exit 6.
    if ($null -eq $config -or -not ($config -is [System.Collections.IDictionary])) {
        Exit-WithError -Message "~/.docker/config.json at $dockerConfigPath is not a valid Docker config object. Run: docker login -u parties-publisher registry.hexalith.com" -Code 6
    }

    # credsStore / credHelpers detection — credential helpers are out of MVP scope.
    if ($config.ContainsKey('credsStore') -and -not [string]::IsNullOrWhiteSpace([string]$config['credsStore'])) {
        $credsStoreValue = [string]$config['credsStore']
        Exit-WithError -Message "Docker credsStore '$credsStoreValue' detected at $dockerConfigPath. publish.ps1 cannot write auth through credential helpers in MVP. Either (1) remove the credsStore directive temporarily, (2) set `$env:DOCKER_CONFIG to a directory with a helper-free config.json, or (3) pre-create zot-pull-secret manually." -Code 6
    }
    if ($config.ContainsKey('credHelpers') -and $config['credHelpers'] -is [System.Collections.IDictionary] -and $config['credHelpers'].ContainsKey('registry.hexalith.com')) {
        $helperValue = [string]$config['credHelpers']['registry.hexalith.com']
        Exit-WithError -Message "Docker credHelpers entry '$helperValue' detected for registry.hexalith.com at $dockerConfigPath. publish.ps1 cannot write auth through credential helpers in MVP. Either (1) remove the credHelpers directive temporarily, (2) set `$env:DOCKER_CONFIG to a directory with a helper-free config.json, or (3) pre-create zot-pull-secret manually." -Code 6
    }

    # Strict-match auth lookup (no fallback to registry.hexalith.com:443 or https:// variants).
    if (-not ($config.ContainsKey('auths') -and $config['auths'] -is [System.Collections.IDictionary])) {
        Exit-WithError -Message "no credentials for registry.hexalith.com in $dockerConfigPath. Run: docker login -u parties-publisher registry.hexalith.com" -Code 6
    }
    if (-not $config['auths'].ContainsKey('registry.hexalith.com')) {
        # T15: when the strict-match fails, list the auths keys we DID see so the
        # operator can spot a legacy `https://registry.hexalith.com/v1/` or
        # `registry.hexalith.com:443` entry produced by an older Docker client.
        # Never echo auth values — only the keys.
        $seenKeys = @($config['auths'].Keys) -join ', '
        Exit-WithError -Message "no credentials for registry.hexalith.com (bare hostname) in $dockerConfigPath. Found auths keys: [$seenKeys]. Re-run: docker login -u parties-publisher registry.hexalith.com (use the bare hostname form; scheme/port variants are not accepted in MVP)." -Code 6
    }
    $authEntry = $config['auths']['registry.hexalith.com']
    if (-not ($authEntry -is [System.Collections.IDictionary]) -or -not $authEntry.ContainsKey('auth') -or [string]::IsNullOrWhiteSpace([string]$authEntry['auth'])) {
        Exit-WithError -Message "no credentials for registry.hexalith.com in $dockerConfigPath. Run: docker login -u parties-publisher registry.hexalith.com" -Code 6
    }

    # Path B emission — re-emit auths entry wholesale; never decode or split the credential.
    # The $authEntry object (containing the base64-encoded parties-publisher:<password> auth
    # field) lives on the heap only during the ConvertTo-Json → GetBytes → ToBase64String pipe.
    $dockerConfigPayload = @{ auths = @{ 'registry.hexalith.com' = $authEntry } } | ConvertTo-Json -Depth 10 -Compress
    $dockerConfigB64 = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($dockerConfigPayload))

    $manifest = @"
apiVersion: v1
kind: Secret
type: kubernetes.io/dockerconfigjson
metadata:
  name: zot-pull-secret
  namespace: $Namespace
data:
  .dockerconfigjson: $dockerConfigB64
"@

    $applyOutput = $manifest | & kubectl apply -f - 2>&1
    if ($LASTEXITCODE -ne 0) {
        # NEVER echo the manifest body, $dockerConfigPayload, or $authEntry on failure paths.
        Exit-WithError -Message "kubectl apply failed for Secret zot-pull-secret (exit $LASTEXITCODE)." -Code 1
    }
    # Bounded passthrough — only metadata lines (kind/name <state>).
    $applyOutput | ForEach-Object {
        if ($_ -match '^secret/\S+\s+\S+') {
            Write-Host "  applied: $_"
        }
    }
    Write-Host "  Secret zot-pull-secret applied."
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
Set-ZotPullSecretIfMissing

# ---------------------------------------------------------------------------
# Step 12: Apply authoritative DAPR component CRs (deploy/dapr/) (AC8).
# Ported from deploy-local.ps1:255-313.
# ---------------------------------------------------------------------------
$daprApplied = 0
$daprSkipped = @()
if (Test-Path -LiteralPath $DaprComponentsPath) {
    Write-Host ""
    Write-Host "Applying authoritative DAPR component CRs from: $DaprComponentsPath"
    foreach ($yamlFile in Get-ChildItem -Path $DaprComponentsPath -Filter "*.yaml" -File) {
        if ($yamlFile.Name -match '^(statestore|pubsub)-[a-z0-9].*\.yaml$' -or
            $yamlFile.Name -eq 'topology.yaml' -or
            $yamlFile.Name -eq 'tenants-integration.yaml') {
            $daprSkipped += $yamlFile.Name
            continue
        }

        Ensure-Namespace

        & kubectl apply -f $yamlFile.FullName -n $Namespace 2>&1 | ForEach-Object {
            if ($_ -match '^(component\.dapr\.io|configuration\.dapr\.io|subscription\.dapr\.io)/[^\s]+\s+(created|configured|unchanged)') {
                Write-Host "  applied: $_"
                $script:daprApplied++
            }
            elseif ($_ -match '^error') {
                Write-Host "  ERROR: $_"
            }
            else {
                if ($_ -match '^\S+/\S+\s+\S+') {
                    Write-Host "  $_"
                }
            }
        }
        if ($LASTEXITCODE -ne 0) {
            Exit-WithError -Message "kubectl apply failed for $($yamlFile.Name) (exit $LASTEXITCODE)." -Code 1
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
# Step 13: Apply aspirate-generated manifests via kustomize (AC8).
# Ported from deploy-local.ps1:315-350.
# ---------------------------------------------------------------------------
if (-not (Test-Path -LiteralPath (Join-Path $ManifestPath "kustomization.yaml"))) {
    Exit-WithError -Message "Manifest path '$ManifestPath' does not contain kustomization.yaml after aspirate generate." -Code 1
}

Write-Host ""
Write-Host "Applying Kubernetes manifests via kustomize: $ManifestPath"
$applyOutput = & kubectl apply -k $ManifestPath 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: kubectl apply -k '$ManifestPath' exited $LASTEXITCODE."
    $applyOutput | Select-Object -First 50 | ForEach-Object { Write-Host "  $_" }
    exit 1
}

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
