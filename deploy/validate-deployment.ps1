#!/usr/bin/env pwsh
# ===========================================================================
# Hexalith.Parties -- Deployment Validation Tool
# ===========================================================================
# Verifies DAPR security configuration AND aspirate-generated Kubernetes
# manifests before production use. Story 8.1 created the DAPR-only checks
# (Access Control, State Store, Pub/Sub, Subscription, Resiliency, Secret
# Store, Authentication, Tenant, Transport, Topology). Story 9.2 added the
# K8s-manifest lint surface (workload shape, DAPR annotations, DAPR Component/
# Configuration/Subscription parity vs deploy/dapr/, plaintext-secret scan
# with redaction contract, static tenant-id scan, local-cluster capability
# allowlist, committed Secret-resource scan).
#
# Usage (Story 8.1 — unchanged):
#   ./validate-deployment.ps1 --config-path ./deploy/dapr
#   ./validate-deployment.ps1 --config-path ./deploy/dapr --output json
#
# Usage (Story 9.2 — additive):
#   ./validate-deployment.ps1 --config-path ./deploy/dapr -K8sPath ./deploy/k8s
#   ./validate-deployment.ps1 -K8sPath ./deploy/k8s --output json
#   ./validate-deployment.ps1 -K8sPath ./deploy/k8s -AllowCloudCapabilities
#
# Exit codes:
#   0 = All checks passed (warnings may exist)
#   1 = One or more blocking failures
#   2 = Invalid arguments or config path not found
#
# Output contract (Story 9.2 K8s findings):
#   - K8s findings carry {category, code, severity, target, recommendation};
#     severity in {fail, warn, pass}; sorted by (category, code, target).
#   - Plaintext-secret findings render as <redacted:N chars at <file>:<line>>;
#     the offending value never appears in stdout, stderr, or recommendation.
#   - File paths are sanitized: control chars (\r \n \t \b \x00-\x1f) → '?'.
#   - Recommendation strings are parametrized constants per category code;
#     only {category, file, line, N} are interpolated.
#   - Console output truncates at 50 findings per category with the marker
#     "N additional findings suppressed — re-run with --output json for full list".
#   - JSON output emits every finding without truncation.
#
# K8s category codes:
#   Workload:    K8sWorkload-MissingImage (fail),
#                K8sWorkload-MissingDaprAnnotation (fail),
#                K8sWorkload-UnresolvedConfigMapRef (fail),
#                K8sWorkload-UnresolvedKustomizationResource (fail),
#                K8sWorkload-MissingProbes (warn),
#                K8sWorkload-MissingResources (warn),
#                K8sWorkload-LatestImageTag (warn)
#   DAPR ACL:    DAPR-ACL-DefaultActionNotDeny (fail),
#                DAPR-ACL-WildcardAppId (fail),
#                DAPR-ACL-MissingPerServiceRule (fail)
#   DAPR Sub:    DAPR-Subscription-MissingDeadLetter (fail),
#                DAPR-Subscription-WrongPubsubName (fail)
#   DAPR Comp:   DAPR-Component-MissingAuthoritativeFile (fail),
#                DAPR-Regen-PlaceholderNotStripped (fail)
#   Secrets:     K8sSecret-PlaintextCredential (fail),
#                K8sSecret-UrlEmbeddedCred (fail),
#                K8sSecret-JwtTokenLiteral (fail),
#                K8sSecret-AwsAccessKey (fail),
#                K8sSecret-AzureConnString (fail),
#                K8sSecret-PrivateKey (fail),
#                K8sSecret-StaticTenantId (fail),
#                K8sSecret-CommittedSecretValue (fail)
#   Local:       K8s-NonLocalClusterCapability (fail; warn under -AllowCloudCapabilities)
#   Generic:     K8s-YamlParseError (fail), K8s-PathTraversal (fail)
#
# Plaintext-secret scan scope (Story 9.2 AC3):
#   IN-SCOPE: configMapGenerator.literals, container env, envFrom/valueFrom,
#             Secret.data/stringData, YAML anchor definitions (&anchor value).
#   OUT-OF-SCOPE: metadata.annotations, spec.template.metadata.annotations,
#                 metadata.labels, selector.matchLabels, YAML comments (# ...),
#                 YAML anchor references (*anchor).
#
# Key-allowlist (never trigger the value regex even if value would match):
#   services__*__http__*, Tenants__ServiceName, Tenants__PubSubName,
#   Tenants__TopicName, Tenants__Enabled, Tenants__CommandApiAppId,
#   EVENTSTORE_HTTP, TENANTS_HTTP, ASPNETCORE_URLS,
#   ASPNETCORE_FORWARDEDHEADERS_ENABLED, HTTP_PORTS,
#   OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY, dapr.io/enable-api-logging.
#
# Value-allowlist (placeholder set; case-insensitive, whitespace-trimmed,
# URL-decoded): {env:*}, {env:*|*}, $(VAR), ${VAR}, valueFrom.*Ref,
# <set-by-operator>, <placeholder>, REPLACE_ME, empty.
#
# Reference: docs/deployment-security-checklist.md, deploy/k8s/README.md
# ===========================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [Alias("config-path")]
    [string]$ConfigPath,

    [Parameter(Mandatory = $false)]
    [Alias("k8s-path")]
    [string]$K8sPath,

    [Parameter(Mandatory = $false)]
    [switch]$AllowCloudCapabilities,

    [Parameter(Mandatory = $false)]
    [ValidateSet("console", "json")]
    [string]$Output = "console"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:IsLocalDevelopment = $false

# ---------------------------------------------------------------------------
# Result storage
# ---------------------------------------------------------------------------

$script:Results = New-Object System.Collections.ArrayList

function Add-Result {
    param(
        [string]$Category,
        [string]$Check,
        [string]$Status,
        [string]$Details,
        [string]$Recommendation = ""
    )
    $r = [PSCustomObject]@{
        Category       = $Category
        Check          = $Check
        Status         = $Status
        Details        = $Details
        Recommendation = $Recommendation
    }
    [void]$script:Results.Add($r)
}

# ---------------------------------------------------------------------------
# YAML parser (minimal, no external dependency)
# ---------------------------------------------------------------------------

function Read-YamlFile {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    Get-Content $Path -Raw
}

function ConvertFrom-YamlScalar {
    param([string]$Value)
    if ($null -eq $Value) { return $null }
    $trimmed = $Value.Trim()
    # Strip trailing comment (whitespace + '#' to end). Inside quotes, '#' is preserved by the
    # quote-pair extraction below; only unquoted scalars get the comment stripped here.
    if ($trimmed.Length -ge 2) {
        $first = $trimmed[0]
        $last = $trimmed[$trimmed.Length - 1]
        if (($first -eq '"' -and $last -eq '"') -or ($first -eq "'" -and $last -eq "'")) {
            return $trimmed.Substring(1, $trimmed.Length - 2)
        }
    }
    if ($trimmed -match '^(.*?)(?:\s+#.*)?$') {
        return $Matches[1].Trim()
    }
    return $trimmed
}

function Split-YamlScopeList {
    param([string]$Value)
    if (-not $Value) { return @() }
    return @($Value -split "[,;]" |
        ForEach-Object { ConvertFrom-YamlScalar $_ } |
        Where-Object { $_ })
}

function Split-YamlDocuments {
    param([string]$Content)
    if (-not $Content) { return @() }
    return @([regex]::Split($Content, "(?m)^\s*---\s*$") |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Get-YamlValue {
    param(
        [string]$Content,
        [string]$Key,
        # Restrict matching to a specific shape to avoid the metadata-list pattern accidentally
        # matching unrelated `name:`/`value:` pairs that surround a top-level scalar.
        # Default ('Auto') tries top-level first, then metadata-list — top-level keys like
        # `pubsubname:` or `topic:` are unambiguous and should not fall through to the list pattern.
        [ValidateSet("Auto", "TopLevel", "MetadataList")]
        [string]$Mode = "Auto"
    )
    if (-not $Content) { return $null }
    $escapedKey = [regex]::Escape($Key)
    # Capture full line content; ConvertFrom-YamlScalar strips trailing comments and quote pairs.
    if ($Mode -ne "MetadataList") {
        if ($Content -match "(?mi)^\s*$escapedKey\s*:\s*([^\r\n]+)") {
            return ConvertFrom-YamlScalar $Matches[1]
        }
        if ($Mode -eq "TopLevel") { return $null }
    }
    # metadata list style: - name: key \n   value: val. Case-insensitive on key only.
    if ($Content -match "(?msi)name:\s*$escapedKey\s*\r?\n\s*(?:#[^\n]*\r?\n\s*)*value:\s*([^\r\n]+)") {
        return ConvertFrom-YamlScalar $Matches[1]
    }
    return $null
}

function Get-YamlScopes {
    param([string]$Content)
    $scopes = @()
    if (-not $Content) { return , $scopes }
    if ($Content -match "(?ms)^scopes:\s*\r?\n((?:\s*-\s*[^\r\n]+[\r\n]*)*)") {
        $block = $Matches[1]
        $lines = $block -split "[\r\n]+"
        foreach ($line in $lines) {
            if ($line -match '^\s*-\s*([^\r\n#]+)') {
                $scopes += ConvertFrom-YamlScalar $Matches[1]
            }
        }
    }
    return , $scopes
}

function Get-YamlKind {
    param([string]$Content)
    if ($Content -match "(?m)^\s*kind:\s*([^\r\n#]+)") {
        return ConvertFrom-YamlScalar $Matches[1]
    }
    return $null
}

function Get-YamlApiVersion {
    param([string]$Content)
    if ($Content -match "(?m)^\s*apiVersion:\s*([^\r\n#]+)") {
        return ConvertFrom-YamlScalar $Matches[1]
    }
    return $null
}

function Get-YamlType {
    param([string]$Content)
    if ($Content -match "(?m)^\s*type:\s*([^\r\n#]+)") {
        return ConvertFrom-YamlScalar $Matches[1]
    }
    return $null
}

function Test-TopicAllowedForApp {
    param([string]$ScopesValue, [string]$AppId, [string]$Topic)
    if (-not $ScopesValue) { return $false }
    $entries = $ScopesValue -split ";"
    foreach ($entry in $entries) {
        $parts = $entry -split "=", 2
        if ($parts.Count -ne 2) { continue }
        $left = ConvertFrom-YamlScalar $parts[0]
        $topics = @($parts[1] -split "," | ForEach-Object { ConvertFrom-YamlScalar $_ } | Where-Object { $_ })
        # '*' wildcard grants all apps access.
        if ($left -eq "*") {
            if ($topics -contains $Topic) {
                return $true
            }
            continue
        }
        # Env-token left sides (e.g. {env:SUBSCRIBER_APP_ID}) name OTHER applications and never
        # the parties target by project convention; skip them so an env-token entry doesn't
        # mask a missing explicit parties=... scope.
        if ($left -match '\{env:[^}]+\}') { continue }
        if ($left -ne $AppId) { continue }
        if ($topics -contains $Topic) { return $true }
    }
    return $false
}

function Test-ScopesContainAll {
    param(
        [string[]]$Scopes,
        [string[]]$Required
    )
    # DAPR app-ids are case-sensitive at runtime; PowerShell's default -contains is not.
    # Use ordinal comparison so a manifest with the wrong casing fails validation here
    # instead of being denied silently by the sidecar at runtime.
    foreach ($requiredScope in $Required) {
        $found = $false
        foreach ($scope in $Scopes) {
            if ([string]::Equals($scope, $requiredScope, [System.StringComparison]::Ordinal)) {
                $found = $true
                break
            }
        }
        if (-not $found) {
            return $false
        }
    }
    return $true
}

function Test-AppIdListed {
    param(
        [string]$Content,
        [string]$AppId
    )
    $escaped = [regex]::Escape($AppId)
    # Accept block-style (- value) with optional surrounding quotes. Flow-style sequences
    # (appIds: [a, b]) are not in the current manifest convention; revisit if introduced.
    return $Content -match "(?m)^\s*-\s*[`"']?$escaped[`"']?\s*$"
}

function Test-InvocationPolicy {
    param(
        [string]$Content,
        [string]$AppId,
        [string]$Path,
        [string]$Verb
    )
    $escapedAppId = [regex]::Escape($AppId)
    $escapedPath = [regex]::Escape($Path)
    $escapedVerb = [regex]::Escape($Verb)

    # Match `- appId: <id>` and then capture everything up to (but NOT including) the next
    # `- appId:` peer entry, or end-of-string. This prevents the non-greedy operator from
    # spanning across unrelated policy blocks and producing a false-positive match when the
    # requested (path, verb, action) tuple actually belongs to a different appId block in
    # the same file. (We do not also anchor on top-level keys because legitimate child keys
    # like `operations:` end with a colon and would prematurely terminate the body capture.)
    $blockPattern = "(?ms)-\s*appId:\s*[`"']?$escapedAppId[`"']?(?<body>(?:(?!^\s*-\s*appId:\s*[`"']?\w).)*)"
    $blockMatches = [regex]::Matches($Content, $blockPattern)
    foreach ($blockMatch in $blockMatches) {
        $body = $blockMatch.Groups["body"].Value
        $allowPattern = "(?ms)operations:\s*.*?name:\s*[`"']?$escapedPath[`"']?.*?httpVerb:\s*\[[^\]]*$escapedVerb[^\]]*\].*?action:\s*allow"
        if ($body -match $allowPattern) {
            return $true
        }
    }
    return $false
}

function Remove-YamlComments {
    param([string]$Content)
    if ([string]::IsNullOrEmpty($Content)) {
        return ""
    }
    # Strip whole-line comments. Do not attempt to strip trailing comments since DAPR
    # YAML may contain `#` inside quoted strings (e.g., URL fragments) and the cost of
    # missing those is low compared to the cost of an incorrect quoted-string parser.
    return ($Content -replace "(?m)^\s*#.*$", "")
}

# ---------------------------------------------------------------------------
# Validation checks
# ---------------------------------------------------------------------------

function Test-AccessControl {
    param([string]$ConfigPath)
    $Category = "Access Control"

    $acFile = Join-Path $ConfigPath "accesscontrol.yaml"
    $content = Read-YamlFile $acFile

    if (-not $content) {
        Add-Result $Category "File exists" "Fail" "accesscontrol.yaml not found in $ConfigPath" `
            "Create accesscontrol.yaml with defaultAction: deny. See docs/deployment-security-checklist.md"
        return
    }
    Add-Result $Category "File exists" "Pass" "accesscontrol.yaml found"

    # defaultAction (spec-level, not policy-level)
    $defaultAction = $null
    $allMatches = [regex]::Matches($content, "(?m)^\s{2,4}defaultAction:\s*(\w+)")
    if ($allMatches.Count -gt 0) {
        $defaultAction = $allMatches[0].Groups[1].Value
    }

    # Trust domain (spec-level)
    $trustDomain = $null
    $allTd = [regex]::Matches($content, "(?m)^\s{2,4}trustDomain:\s*[`"']?([^`"'\r\n]+)[`"']?")
    if ($allTd.Count -gt 0) {
        $trustDomain = $allTd[0].Groups[1].Value.Trim()
    }
    $script:IsLocalDevelopment = ($defaultAction -eq "allow") -and ($trustDomain -eq "public")

    if ($defaultAction -eq "deny") {
        Add-Result $Category "Default action is deny" "Pass" "defaultAction: deny (secure by default)"
    }
    elseif ($defaultAction -eq "allow") {
        if ($script:IsLocalDevelopment) {
            Add-Result $Category "Default action is deny" "Warn" "defaultAction: allow (acceptable for local self-hosted development)" `
                "Set defaultAction to 'deny' for production. 'allow' permits unrestricted service invocation."
        }
        else {
            Add-Result $Category "Default action is deny" "Fail" "defaultAction: allow (insecure)" `
                "Set defaultAction to 'deny' for production. 'allow' permits unrestricted service invocation."
        }
    }
    else {
        Add-Result $Category "Default action is deny" "Fail" "defaultAction not found or unrecognized: $defaultAction" `
            "Set defaultAction to 'deny' in the accessControl spec."
    }

    if ($trustDomain -eq "public") {
        if ($script:IsLocalDevelopment) {
            Add-Result $Category "Trust domain is not 'public'" "Warn" "trustDomain: public (local development self-hosted mode)" `
                "Use a real SPIFFE trust domain for production deployments."
        }
        else {
            Add-Result $Category "Trust domain is not 'public'" "Fail" "trustDomain: public (self-hosted default)" `
                "Set trustDomain to a real SPIFFE domain for production (e.g., hexalith.io or use env var)."
        }
    }
    elseif ($trustDomain) {
        Add-Result $Category "Trust domain is not 'public'" "Pass" "trustDomain: $trustDomain"
    }
    else {
        Add-Result $Category "Trust domain is not 'public'" "Fail" "trustDomain not found" `
            "Configure trustDomain in the accessControl spec."
    }

    # Policies restrict to known app-ids
    $appIdMatches = [regex]::Matches($content, "(?m)^\s*-\s*appId:\s*[`"']?([^`"'\r\n]+)[`"']?")
    $appIds = @($appIdMatches | ForEach-Object { $_.Groups[1].Value.Trim() })
    if ($appIds.Count -eq 0) {
        Add-Result $Category "Policies restrict to known app-ids" "Fail" "No appId-scoped policies found" `
            "Add policy entries restricting operations to known app-ids only."
    }
    elseif (@($appIds | Where-Object { ($_ -eq "*") -or ($_ -match "[\*\?]") }).Count -gt 0) {
        Add-Result $Category "Policies restrict to known app-ids" "Fail" "Wildcard appId entries found: [$($appIds -join ', ')]" `
            "Replace wildcard app-ids with explicit known callers only."
    }
    else {
        Add-Result $Category "Policies restrict to known app-ids" "Pass" "Policy entries restrict access to explicit app-ids: [$($appIds -join ', ')]"
    }

    $eventStoreCallers = @("eventstore-admin", "tenants", "parties")
    foreach ($caller in $eventStoreCallers) {
        if ($appIds -contains $caller) {
            Add-Result $Category "EventStore caller allowed: $caller" "Pass" "$caller is explicitly listed for the EventStore receiving sidecar"
        }
        elseif ($script:IsLocalDevelopment) {
            Add-Result $Category "EventStore caller allowed: $caller" "Warn" "Required production EventStore caller is missing (local development profile)" `
                "Add an explicit $caller policy to accesscontrol.yaml for production."
        }
        else {
            Add-Result $Category "EventStore caller allowed: $caller" "Fail" "Required EventStore caller is missing" `
                "Add an explicit $caller policy to accesscontrol.yaml. Remediation target: EventStore gateway access control."
        }
    }

    $partiesFile = Join-Path $ConfigPath "accesscontrol.parties.yaml"
    $partiesContent = Read-YamlFile $partiesFile
    if (-not $partiesContent) {
        if ($script:IsLocalDevelopment) {
            Add-Result $Category "Parties receiving sidecar policy" "Warn" "accesscontrol.parties.yaml not found (local development profile)" `
                "Add accesscontrol.parties.yaml for production so only eventstore can invoke POST /process."
        }
        else {
            Add-Result $Category "Parties receiving sidecar policy" "Fail" "accesscontrol.parties.yaml not found" `
                "Add accesscontrol.parties.yaml with eventstore -> POST /process only."
        }
    }
    else {
        if ($partiesContent -match "(?m)^\s{2,4}defaultAction:\s*deny") {
            Add-Result $Category "Parties sidecar deny-by-default" "Pass" "accesscontrol.parties.yaml uses defaultAction: deny"
        }
        else {
            Add-Result $Category "Parties sidecar deny-by-default" "Fail" "Parties sidecar default action is not deny" `
                "Set accesscontrol.parties.yaml spec.accessControl.defaultAction to deny."
        }

        if (Test-InvocationPolicy $partiesContent "eventstore" "/process" "POST") {
            Add-Result $Category "eventstore invokes parties /process" "Pass" "Receiving sidecar allows eventstore -> POST /process"
        }
        else {
            Add-Result $Category "eventstore invokes parties /process" "Fail" "Required receiving-sidecar invocation is missing" `
                "Allow only appId=eventstore, method POST, path /process in accesscontrol.parties.yaml."
        }

        $partiesPolicyBody = Remove-YamlComments $partiesContent
        if ($partiesPolicyBody -match "appId:\s*['`"]?\*['`"]?" -or $partiesPolicyBody -match "name:\s*['`"]?/\*\*['`"]?") {
            Add-Result $Category "Parties sidecar has no wildcard invocation" "Fail" "Wildcard caller or broad Parties path is configured" `
                "Remove wildcard app-id and broad /** operations from accesscontrol.parties.yaml."
        }
        else {
            Add-Result $Category "Parties sidecar has no wildcard invocation" "Pass" "No wildcard app-id or /** Parties operation found"
        }
    }

    $tenantsFile = Join-Path $ConfigPath "accesscontrol.tenants.yaml"
    $tenantsContent = Read-YamlFile $tenantsFile
    if (-not $tenantsContent) {
        if ($script:IsLocalDevelopment) {
            Add-Result $Category "Tenants receiving sidecar policy" "Warn" "accesscontrol.tenants.yaml not found (local development profile)" `
                "Add accesscontrol.tenants.yaml for production Tenants authority invocation."
        }
        else {
            Add-Result $Category "Tenants receiving sidecar policy" "Fail" "accesscontrol.tenants.yaml not found" `
                "Add accesscontrol.tenants.yaml with explicit EventStore and accepted readiness callers."
        }
    }
    elseif (($tenantsContent -match "(?m)^\s{2,4}defaultAction:\s*deny") -and
        (Test-InvocationPolicy $tenantsContent "parties" "/ready" "GET")) {
        Add-Result $Category "Tenants sidecar accepted paths" "Pass" "Tenants receiving sidecar is deny-by-default and exposes the accepted readiness path"
    }
    else {
        Add-Result $Category "Tenants sidecar accepted paths" "Fail" "Tenants receiving-sidecar policy is missing deny posture or /ready shape" `
            "Keep accesscontrol.tenants.yaml deny-by-default and restrict readiness to appId=parties, GET /ready."
    }

    $adminFile = Join-Path $ConfigPath "accesscontrol.eventstore-admin.yaml"
    $adminContent = Read-YamlFile $adminFile
    if (-not $adminContent) {
        if ($script:IsLocalDevelopment) {
            Add-Result $Category "EventStore Admin receiving sidecar policy" "Warn" "accesscontrol.eventstore-admin.yaml not found (local development profile)" `
                "Add accesscontrol.eventstore-admin.yaml for production admin sidecar isolation."
        }
        else {
            Add-Result $Category "EventStore Admin receiving sidecar policy" "Fail" "accesscontrol.eventstore-admin.yaml not found" `
                "Add accesscontrol.eventstore-admin.yaml with deny-by-default and no peer policies."
        }
    }
    else {
        $adminBody = Remove-YamlComments $adminContent
        $denyByDefault = $adminBody -match "(?m)^\s{2,4}defaultAction:\s*deny"
        # Accept either explicit empty flow-list `policies: []` (with optional inner whitespace)
        # or block-style `policies:` followed by null/empty (any sibling top-level key on the
        # next non-empty line, or end of file). Both forms represent the same "no peer callers"
        # posture; the previous strict `policies: \[\]` match rejected the equally-secure null form.
        $hasNoPolicies = ($adminBody -match "(?m)^\s{2,4}policies:\s*\[\s*\]\s*$") -or
            ($adminBody -match "(?ms)^\s{2,4}policies:\s*(?:#[^\r\n]*)?\r?\n(?=\s*\w+:|\z)")
        if ($denyByDefault -and $hasNoPolicies) {
            Add-Result $Category "EventStore Admin sidecar locked down" "Pass" "Admin Server receiving sidecar has no DAPR peer callers"
        }
        else {
            Add-Result $Category "EventStore Admin sidecar locked down" "Fail" "Admin Server sidecar is not locked down" `
                "Keep accesscontrol.eventstore-admin.yaml deny-by-default with policies: [] (or no policies entries)."
        }
    }
}

function Test-StateStore {
    param([string]$ConfigPath)
    $Category = "State Store"

    $ssFiles = @(Get-ChildItem -Path $ConfigPath -Filter "statestore*.yaml" -ErrorAction SilentlyContinue)
    if ($ssFiles.Count -eq 0) {
        Add-Result $Category "State store component exists" "Fail" "No statestore*.yaml files found in $ConfigPath" `
            "Create a state store component with actorStateStore: true. See docs/deployment-security-checklist.md"
        return
    }

    foreach ($ssFile in $ssFiles) {
        $content = Read-YamlFile $ssFile.FullName
        $fileName = $ssFile.Name

        $kind = Get-YamlKind $content
        if ($kind -ne "Component") {
            continue
        }

        Add-Result $Category "State store component exists ($fileName)" "Pass" "Found state store component"

        # actorStateStore
        $actorStore = Get-YamlValue $content "actorStateStore"
        if ($actorStore -eq "true") {
            Add-Result $Category "actorStateStore is true ($fileName)" "Pass" "actorStateStore: true"
        }
        else {
            Add-Result $Category "actorStateStore is true ($fileName)" "Fail" "actorStateStore is not set to true" `
                "Set actorStateStore metadata to 'true'. Actors require this for state persistence."
        }

        # Scopes
        $scopes = Get-YamlScopes $content
        $requiredSharedScopes = @("eventstore", "eventstore-admin", "parties", "tenants")
        if ($scopes.Count -eq 0) {
            Add-Result $Category "Shared topology scopes ($fileName)" "Fail" "No scopes defined" `
                "Add required shared topology scopes: eventstore, eventstore-admin, parties, tenants."
        }
        elseif (Test-ScopesContainAll $scopes $requiredSharedScopes) {
            Add-Result $Category "Shared topology scopes ($fileName)" "Pass" "State store includes required shared topology scopes"
        }
        elseif ($script:IsLocalDevelopment -and $scopes -contains "parties") {
            Add-Result $Category "Shared topology scopes ($fileName)" "Warn" "Only the local Parties state-store scope is configured" `
                "Production manifests must include required shared topology scopes: eventstore, eventstore-admin, parties, tenants."
        }
        else {
            Add-Result $Category "Shared topology scopes ($fileName)" "Fail" "Missing required shared topology scopes" `
                "Add scopes for eventstore, eventstore-admin, parties, and tenants."
        }

        $keyPrefix = Get-YamlValue $content "keyPrefix"
        if ($keyPrefix -eq "none") {
            Add-Result $Category "keyPrefix is none ($fileName)" "Pass" "keyPrefix=none preserves shared topology state visibility"
        }
        elseif ($script:IsLocalDevelopment) {
            Add-Result $Category "keyPrefix is none ($fileName)" "Warn" "keyPrefix=none is not configured (local development profile)" `
                "Set keyPrefix=none for the shared EventStore-fronted topology."
        }
        else {
            Add-Result $Category "keyPrefix is none ($fileName)" "Fail" "keyPrefix=none is not configured" `
                "Set metadata keyPrefix=none for the shared EventStore-fronted topology."
        }

        # Connection string uses env-var
        $hasHardcoded = $false
        $connFields = @("connectionString", "url", "masterKey")
        foreach ($field in $connFields) {
            if ($content -match "(?ms)name:\s*$field\s*\r?\n\s*(?:#[^\n]*\r?\n\s*)*value:\s*[`"']?([^`"'\r\n]+)") {
                $val = $Matches[1].Trim()
                if ($val -notmatch "\{env:") {
                    $hasHardcoded = $true
                }
            }
        }
        if ($hasHardcoded) {
            Add-Result $Category "Connection uses env-var reference ($fileName)" "Fail" "Connection string or credentials appear hardcoded" `
                "Use {env:VAR_NAME} references for connection strings and secrets. Never hardcode credentials."
        }
        else {
            Add-Result $Category "Connection uses env-var reference ($fileName)" "Pass" "Connection values use environment variable references"
        }
    }
}

function Test-PubSub {
    param([string]$ConfigPath)
    $Category = "Pub/Sub"

    $psFiles = @(Get-ChildItem -Path $ConfigPath -Filter "pubsub*.yaml" -ErrorAction SilentlyContinue)
    if ($psFiles.Count -eq 0) {
        Add-Result $Category "Pub/sub component exists" "Fail" "No pubsub*.yaml files found in $ConfigPath" `
            "Create a pub/sub component with scoping and dead-letter enabled. See docs/deployment-security-checklist.md"
        return
    }

    foreach ($psFile in $psFiles) {
        $content = Read-YamlFile $psFile.FullName
        $fileName = $psFile.Name

        $kind = Get-YamlKind $content
        $type = Get-YamlType $content
        $isLocalDevPubSub = $script:IsLocalDevelopment -or ($type -eq "pubsub.redis")
        if ($kind -ne "Component") {
            continue
        }

        Add-Result $Category "Pub/sub component exists ($fileName)" "Pass" "Found pub/sub component"

        # Scopes defined
        $scopes = Get-YamlScopes $content
        $requiredPubSubScopes = @("eventstore", "parties", "tenants")
        if ($scopes.Count -gt 0) {
            Add-Result $Category "Scopes defined ($fileName)" "Pass" "Component scopes configured"
            if (Test-ScopesContainAll $scopes $requiredPubSubScopes) {
                Add-Result $Category "EventStore topology pub/sub scopes ($fileName)" "Pass" "Pub/sub includes eventstore, parties, and tenants"
            }
            elseif ($isLocalDevPubSub -and $scopes -contains "parties") {
                Add-Result $Category "EventStore topology pub/sub scopes ($fileName)" "Warn" "Only local pub/sub scopes are configured" `
                    "Production pub/sub scopes must include eventstore, parties, and tenants."
            }
            else {
                Add-Result $Category "EventStore topology pub/sub scopes ($fileName)" "Fail" "Missing required EventStore-fronted pub/sub scopes" `
                    "Add eventstore, parties, and tenants to pub/sub component scopes."
            }
        }
        else {
            Add-Result $Category "Scopes defined ($fileName)" "Fail" "No component scopes defined" `
                "Add scopes list with parties and authorized subscriber app-ids."
        }

        # publishingScopes restricts subscribers
        $pubScopes = Get-YamlValue $content "publishingScopes"
        if ($pubScopes) {
            if ($pubScopes -match "=\s*(;|$)") {
                Add-Result $Category "publishingScopes restricts subscribers ($fileName)" "Pass" "Subscribers denied publishing access"
            }
            else {
                Add-Result $Category "publishingScopes restricts subscribers ($fileName)" "Fail" "Subscribers may have publishing access to topics" `
                    "Deny subscriber publishing: set subscriber entries to 'SUBSCRIBER_APP_ID=' (empty topics)."
            }

            # EventStore must be able to publish to every {tenant}.parties.events topic.
            # DAPR does not support wildcards in publishingScopes; the architecturally
            # correct posture is therefore to OMIT eventstore from publishingScopes
            # (absent app = unrestricted publish). Accept either: (a) eventstore not
            # listed, or (b) eventstore explicitly listed with at least one party-events
            # topic. Reject only when eventstore appears with an empty topic list,
            # which would deny publishing entirely.
            $eventstoreEntries = @($pubScopes -split ";" | Where-Object { ($_ -split "=", 2)[0] -eq "eventstore" })
            if ($eventstoreEntries.Count -eq 0) {
                Add-Result $Category "eventstore can publish party events ($fileName)" "Pass" "EventStore is unrestricted in publishingScopes (multi-tenant publish posture)"
            }
            elseif ($eventstoreEntries | Where-Object { ($_ -split "=", 2)[1] -match "\S" }) {
                Add-Result $Category "eventstore can publish party events ($fileName)" "Pass" "EventStore publisher scope is explicitly configured"
            }
            elseif ($isLocalDevPubSub) {
                Add-Result $Category "eventstore can publish party events ($fileName)" "Warn" "EventStore is listed in publishingScopes with empty topics (local development profile)" `
                    "Remove the eventstore entry from publishingScopes to grant multi-tenant publish access for production."
            }
            else {
                Add-Result $Category "eventstore can publish party events ($fileName)" "Fail" "EventStore is listed in publishingScopes with empty topics" `
                    "Remove the eventstore entry from publishingScopes (absent = unrestricted publish) or list every required {tenant}.parties.events topic explicitly."
            }

            if (Test-TopicAllowedForApp $pubScopes "tenants" "system.tenants.events") {
                Add-Result $Category "tenants can publish authority events ($fileName)" "Pass" "Tenants publisher scope is configured"
            }
            elseif ($isLocalDevPubSub) {
                Add-Result $Category "tenants can publish authority events ($fileName)" "Warn" "Tenants publisher scope is not configured (local development profile)" `
                    "Add publishingScopes entry tenants=system.tenants.events for production."
            }
            else {
                Add-Result $Category "tenants can publish authority events ($fileName)" "Fail" "Tenants publisher scope is missing" `
                    "Add publishingScopes entry tenants=system.tenants.events."
            }
        }
        else {
            if ($isLocalDevPubSub) {
                Add-Result $Category "publishingScopes restricts subscribers ($fileName)" "Warn" "No publishingScopes metadata found (local development profile)" `
                    "Add publishingScopes metadata for production deployments to deny subscribers from publishing."
            }
            else {
                Add-Result $Category "publishingScopes restricts subscribers ($fileName)" "Fail" "No publishingScopes metadata found" `
                    "Add publishingScopes metadata to deny subscribers from publishing."
            }
        }

        # subscriptionScopes restricts subscribers
        $subScopes = Get-YamlValue $content "subscriptionScopes"
        if ($subScopes) {
            Add-Result $Category "subscriptionScopes defined ($fileName)" "Pass" "Subscription scoping configured"
        }
        else {
            if ($isLocalDevPubSub) {
                Add-Result $Category "subscriptionScopes defined ($fileName)" "Warn" "No subscriptionScopes metadata found (local development profile)" `
                    "Add subscriptionScopes to restrict subscribers to authorized tenant topics in production."
            }
            else {
                Add-Result $Category "subscriptionScopes defined ($fileName)" "Fail" "No subscriptionScopes metadata found" `
                    "Add subscriptionScopes to restrict subscribers to authorized tenant topics only."
            }
        }

        # enableDeadLetter
        $deadLetter = Get-YamlValue $content "enableDeadLetter"
        if ($deadLetter -eq "true") {
            Add-Result $Category "Dead letter enabled ($fileName)" "Pass" "enableDeadLetter: true"
        }
        else {
            if ($isLocalDevPubSub) {
                Add-Result $Category "Dead letter enabled ($fileName)" "Warn" "enableDeadLetter is not set to true (local development profile)" `
                    "Set enableDeadLetter to 'true' for reliable production message handling."
            }
            else {
                Add-Result $Category "Dead letter enabled ($fileName)" "Fail" "enableDeadLetter is not set to true" `
                    "Set enableDeadLetter to 'true' for reliable message handling."
            }
        }

        # Connection string uses env-var
        $hasHardcodedConn = $false
        $valueMatches = [regex]::Matches($content, "(?ms)name:\s*(brokers|connectionString)\s*\r?\n\s*(?:#[^\n]*\r?\n\s*)*value:\s*[`"']?([^`"'\r\n]+)")
        foreach ($vm in $valueMatches) {
            $val = $vm.Groups[2].Value.Trim()
            if ($val -notmatch "\{env:") {
                $hasHardcodedConn = $true
            }
        }
        if ($hasHardcodedConn) {
            Add-Result $Category "Connection uses env-var reference ($fileName)" "Fail" "Connection string appears hardcoded" `
                "Use {env:VAR_NAME} references for connection strings. Never hardcode credentials."
        }
        else {
            Add-Result $Category "Connection uses env-var reference ($fileName)" "Pass" "Connection values use environment variable references"
        }
    }
}

function Test-Subscription {
    param([string]$ConfigPath)
    $Category = "Subscription"

    $subFiles = @(Get-ChildItem -Path $ConfigPath -Filter "subscription*.yaml" -ErrorAction SilentlyContinue)
    if ($subFiles.Count -eq 0) {
        Add-Result $Category "Subscription file exists" "Fail" "No subscription*.yaml files found in $ConfigPath" `
            "Create subscription files for tenant event routing. See docs/deployment-security-checklist.md"
        return
    }

    foreach ($subFile in $subFiles) {
        $content = Read-YamlFile $subFile.FullName
        $fileName = $subFile.Name

        $kind = Get-YamlKind $content
        if ($kind -ne "Subscription") {
            continue
        }

        Add-Result $Category "Subscription file exists ($fileName)" "Pass" "Found subscription component"

        # API version
        $apiVersion = Get-YamlApiVersion $content
        if ($apiVersion -eq "dapr.io/v2alpha1") {
            Add-Result $Category "API version is v2alpha1 ($fileName)" "Pass" "apiVersion: dapr.io/v2alpha1"
        }
        else {
            Add-Result $Category "API version is v2alpha1 ($fileName)" "Fail" "apiVersion: $apiVersion" `
                "Use apiVersion: dapr.io/v2alpha1 for declarative subscriptions."
        }

        # Dead-letter topic
        if ($content -match "(?m)deadLetterTopic:") {
            Add-Result $Category "Dead-letter topic configured ($fileName)" "Pass" "Dead-letter topic is configured"
        }
        else {
            Add-Result $Category "Dead-letter topic configured ($fileName)" "Fail" "No deadLetterTopic configured" `
                "Add deadLetterTopic for failed message handling."
        }

        # Scopes reference subscriber app-ids
        $scopes = Get-YamlScopes $content
        if ($scopes.Count -gt 0) {
            $hasHardcoded = $false
            foreach ($scope in $scopes) {
                if ($scope -notmatch "\{env:" -and $scope -ne "parties") {
                    $hasHardcoded = $true
                }
            }
            if ($hasHardcoded) {
                if ($script:IsLocalDevelopment) {
                    Add-Result $Category "Scopes use env-var references ($fileName)" "Warn" "Subscriber app-ids appear hardcoded in scopes (local development profile)" `
                        "Use {env:SUBSCRIBER_APP_ID} references in production deployment manifests."
                }
                else {
                    Add-Result $Category "Scopes use env-var references ($fileName)" "Fail" "Subscriber app-ids appear hardcoded in scopes" `
                        "Use {env:SUBSCRIBER_APP_ID} references instead of hardcoding app-ids."
                }
            }
            else {
                Add-Result $Category "Scopes use env-var references ($fileName)" "Pass" "Subscriber scopes use environment variable references"
            }
        }
        else {
            Add-Result $Category "Scopes defined ($fileName)" "Fail" "No scopes defined on subscription" `
                "Add scopes with subscriber app-ids to restrict who can receive events."
        }
    }
}

function Test-TenantsIntegration {
    param([string]$ConfigPath)
    $Category = "Tenants Integration"
    $expectedPubSub = "pubsub"
    $expectedTopic = "system.tenants.events"
    $expectedAppId = "parties"
    $expectedCommandAppId = "eventstore"

    # Filter by manifest kind rather than filename: a file canonically named differently
    # (e.g. parties-integration.yaml) is still a valid TenantsIntegration manifest, while a
    # file named tenants-foo.yaml whose kind is something else must not be parsed here.
    # Use a case-insensitive Where-Object so non-Windows filesystems do not silently miss
    # files like Tenants*.yaml or *.YAML.
    $tenantConfigFiles = @(Get-ChildItem -Path $ConfigPath -Filter "*.yaml" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -notmatch '(?i)^subscription' -and
            (Get-YamlKind (Read-YamlFile $_.FullName)) -eq "TenantsIntegration"
        })
    if ($tenantConfigFiles.Count -eq 0) {
        if ($script:IsLocalDevelopment) {
            Add-Result $Category "Tenants configuration present" "Warn" "No Tenants integration config found (local development profile)" `
                "Document local-only mode or add a Tenants integration config with pubsubName, topicName, and commandApiAppId."
        }
        else {
            Add-Result $Category "Tenants configuration present" "Fail" "No Tenants integration config found" `
                "Add Tenants integration config. Impact: Parties cannot prove it consumes Hexalith.Tenants state for authorization. Remediation target: deployment configuration."
        }
    }
    else {
        foreach ($cfg in $tenantConfigFiles) {
            $content = Read-YamlFile $cfg.FullName
            if (-not $content) {
                Add-Result $Category "Tenants configuration values ($($cfg.Name))" "Fail" "Tenants integration manifest is unreadable or empty" `
                    "Verify file permissions and YAML content. Remediation target: $($cfg.FullName)."
                continue
            }

            $fileName = $cfg.Name
            $pubsubName = Get-YamlValue $content "pubsubName" -Mode TopLevel
            $topicName = Get-YamlValue $content "topicName" -Mode TopLevel
            $appId = Get-YamlValue $content "commandApiAppId" -Mode TopLevel
            $dependencyHealth = Get-YamlValue $content "tenantsDependencyHealth" -Mode TopLevel

            if ($pubsubName -eq $expectedPubSub -and $topicName -eq $expectedTopic -and $appId -eq $expectedCommandAppId) {
                Add-Result $Category "Tenants configuration values ($fileName)" "Pass" "Tenants config targets EventStore-fronted command gateway"
            }
            else {
                Add-Result $Category "Tenants configuration values ($fileName)" "Fail" "Missing or malformed Tenants config values" `
                    "Set pubsubName=$expectedPubSub, topicName=$expectedTopic, and commandApiAppId=$expectedCommandAppId. Impact: tenant access projection may not receive authoritative Tenants state. Remediation target: $fileName."
            }

            # dependencyHealth signal: case-insensitive match; unknown values warn instead of silently passing.
            # Echo only the matched normalized keyword (never the raw operator-supplied value) so a
            # secret accidentally pasted into this field never reaches CI logs.
            if ($null -ne $dependencyHealth -and $dependencyHealth -match "^(?i)(unhealthy|unreachable|missing)$") {
                $matched = $Matches[1].ToLowerInvariant()
                Add-Result $Category "Tenants dependency health ($fileName)" "Fail" "Tenants dependency signal is $matched" `
                    "Verify Hexalith.Tenants deployment, service discovery, and event publishing. Impact: Parties fails closed or uses stale tenant access state."
            }
            elseif ($null -ne $dependencyHealth -and $dependencyHealth -match "^(?i)(healthy|ok|ready)$") {
                $matched = $Matches[1].ToLowerInvariant()
                Add-Result $Category "Tenants dependency health ($fileName)" "Pass" "Tenants dependency signal is $matched"
            }
            elseif (-not [string]::IsNullOrWhiteSpace($dependencyHealth)) {
                Add-Result $Category "Tenants dependency health ($fileName)" "Warn" "Tenants dependency signal is not a recognized value" `
                    "Use one of: healthy, ok, ready, unhealthy, unreachable, missing. Remediation target: $fileName."
            }
        }
    }

    $escapedExpectedTopic = [regex]::Escape($expectedTopic)
    # Anchor at end of line so `system.tenants.events.foo` does not match as the Tenants topic.
    # Walk every YAML document in the file to support multi-document subscription files.
    $tenantSubscriptions = @(Get-ChildItem -Path $ConfigPath -Filter "*.yaml" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -match '(?i)^subscription'
        } |
        Where-Object {
            $content = Read-YamlFile $_.FullName
            if (-not $content) { return $false }
            $documents = @(Split-YamlDocuments $content)
            if ($documents.Count -eq 0) { $documents = @($content) }
            foreach ($doc in $documents) {
                if (($doc -match "(?m)^\s*topic:\s*[`"']?$escapedExpectedTopic[`"']?\s*$") -or
                    ($doc -match "(?m)^\s*topicName:\s*[`"']?$escapedExpectedTopic[`"']?\s*$")) {
                    return $true
                }
            }
            return $false
        })

    if ($tenantSubscriptions.Count -eq 0) {
        if ($script:IsLocalDevelopment) {
            Add-Result $Category "Tenants subscription present" "Warn" "No declarative subscription for $expectedTopic found (local development profile)" `
                "Ensure MapTenantEventSubscription() is active locally or add subscription-tenants.yaml for production."
        }
        else {
            Add-Result $Category "Tenants subscription present" "Fail" "No Tenants subscription for $expectedTopic found" `
                "Add a DAPR subscription for $expectedTopic routed to parties. Impact: Parties local tenant projection will not update. Remediation target: subscription-tenants.yaml or MapTenantEventSubscription()."
        }
    }
    else {
        foreach ($sub in $tenantSubscriptions) {
            $content = Read-YamlFile $sub.FullName
            $fileName = $sub.Name
            # Iterate every document in the subscription file so multi-document files don't
            # silently miss subsequent docs.
            $subscriptionDocs = @(Split-YamlDocuments $content)
            if ($subscriptionDocs.Count -eq 0) { $subscriptionDocs = @($content) }
            $pubsubName = $null
            $scopes = @()
            $deadLetterValue = $null
            foreach ($doc in $subscriptionDocs) {
                if ((Get-YamlKind $doc) -ne "Subscription") { continue }
                if (-not (($doc -match "(?m)^\s*topic:\s*[`"']?$escapedExpectedTopic[`"']?\s*$") -or
                          ($doc -match "(?m)^\s*topicName:\s*[`"']?$escapedExpectedTopic[`"']?\s*$"))) {
                    continue
                }
                if (-not $pubsubName) {
                    $pubsubName = Get-YamlValue $doc "pubsubname" -Mode TopLevel
                }
                $docScopes = Get-YamlScopes $doc
                foreach ($s in $docScopes) {
                    if ($scopes -notcontains $s) { $scopes += $s }
                }
                if (-not $deadLetterValue -and $doc -match "(?m)^\s*deadLetterTopic:\s*[`"']?([^`"'\r\n#]+)[`"']?") {
                    $deadLetterValue = (ConvertFrom-YamlScalar $Matches[1])
                }
            }

            if ($pubsubName -eq $expectedPubSub) {
                Add-Result $Category "Tenants subscription pub/sub ($fileName)" "Pass" "Subscription uses the expected pubsub component"
            }
            else {
                Add-Result $Category "Tenants subscription pub/sub ($fileName)" "Fail" "Subscription pubsubname does not match expected value" `
                    "Set pubsubname to '$expectedPubSub'. Impact: Tenants events may be routed through the wrong component. Remediation target: $fileName."
            }

            if ($scopes -contains $expectedAppId) {
                Add-Result $Category "Tenants subscription scoped to parties ($fileName)" "Pass" "Subscription scopes include $expectedAppId"
            }
            else {
                Add-Result $Category "Tenants subscription scoped to parties ($fileName)" "Fail" "Subscription scopes do not include $expectedAppId" `
                    "Add parties to subscription scopes. Impact: the Parties Tenants event subscription cannot run."
            }

            # Dead-letter topic must be present AND have a non-empty value (after trim).
            if (-not [string]::IsNullOrWhiteSpace($deadLetterValue)) {
                Add-Result $Category "Tenants subscription dead-letter ($fileName)" "Pass" "Dead-letter topic is configured"
            }
            else {
                Add-Result $Category "Tenants subscription dead-letter ($fileName)" "Fail" "No deadLetterTopic configured for Tenants subscription" `
                    "Add a non-empty deadLetterTopic and resiliency policy. Impact: failed Tenants events can be lost silently."
            }
        }
    }

    $pubsubFiles = @(Get-ChildItem -Path $ConfigPath -Filter "*.yaml" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '(?i)^pubsub' })
    foreach ($psFile in $pubsubFiles) {
        $content = Read-YamlFile $psFile.FullName
        $fileName = $psFile.Name
        $componentDocuments = @(Split-YamlDocuments $content | Where-Object { (Get-YamlKind $_) -eq "Component" })
        if ($componentDocuments.Count -eq 0) {
            # Surface that the file produced no inspectable Component documents so operators
            # see a row instead of silently treating the file as green.
            Add-Result $Category "parties can subscribe to Tenants topic ($fileName)" "Warn" "No Component document found in pub/sub file" `
                "Verify the YAML contains a `kind: Component` document for the pub/sub component."
            continue
        }
        foreach ($componentDocument in $componentDocuments) {
            $subScopes = Get-YamlValue $componentDocument "subscriptionScopes"
            if (Test-TopicAllowedForApp $subScopes $expectedAppId $expectedTopic) {
                Add-Result $Category "parties can subscribe to Tenants topic ($fileName)" "Pass" "$expectedAppId is allowed to subscribe to $expectedTopic"
            }
            elseif ($script:IsLocalDevelopment -or ((Get-YamlType $componentDocument) -eq "pubsub.redis")) {
                Add-Result $Category "parties can subscribe to Tenants topic ($fileName)" "Warn" "No explicit parties subscription scope for $expectedTopic (local development profile)" `
                    "Add subscriptionScopes entry '$expectedAppId=$expectedTopic' for production scoping."
            }
            else {
                Add-Result $Category "parties can subscribe to Tenants topic ($fileName)" "Fail" "Missing parties subscription permission for $expectedTopic" `
                    "Add '$expectedAppId=$expectedTopic' to subscriptionScopes. Impact: production DAPR scoping blocks Tenants events from Parties."
            }
        }
    }
}

function Test-Resiliency {
    param([string]$ConfigPath)
    $Category = "Resiliency"

    $resFile = Join-Path $ConfigPath "resiliency.yaml"
    $content = Read-YamlFile $resFile

    if (-not $content) {
        Add-Result $Category "Resiliency policy exists" "Fail" "resiliency.yaml not found in $ConfigPath" `
            "Create resiliency.yaml with retry policies and circuit breakers. See docs/deployment-security-checklist.md"
        return
    }
    Add-Result $Category "Resiliency policy exists" "Pass" "resiliency.yaml found"

    # Circuit breakers
    if ($content -match "(?m)circuitBreakers:") {
        Add-Result $Category "Circuit breakers configured" "Pass" "Circuit breaker policies defined"
    }
    else {
        Add-Result $Category "Circuit breakers configured" "Fail" "No circuit breaker policies found" `
            "Add circuitBreakers section for pub/sub and state store resilience."
    }

    # Retry policies use exponential backoff
    if ($content -match "(?m)policy:\s*exponential") {
        Add-Result $Category "Retry uses exponential backoff" "Pass" "Exponential retry policies configured"
    }
    else {
        Add-Result $Category "Retry uses exponential backoff" "Fail" "No exponential retry policy found" `
            "Set retry policy to 'exponential' for production-grade resilience."
    }

    $hasPubSubTargets = $content -match "(?ms)components:\s*.*?\bpubsub:\s*.*?\boutbound:\s*.*?\bretry:\s*\S+.*?\bcircuitBreaker:\s*\S+.*?\binbound:\s*.*?\bretry:\s*\S+"
    if ($hasPubSubTargets) {
        Add-Result $Category "Pub/sub component targets configured" "Pass" "Pub/sub inbound/outbound resiliency targets are configured"
    }
    elseif ($script:IsLocalDevelopment) {
        Add-Result $Category "Pub/sub component targets configured" "Warn" "Pub/sub component targets are not configured (local development profile)" `
            "Configure pub/sub inbound/outbound retry and circuit breaker targets for production deployments."
    }
    else {
        Add-Result $Category "Pub/sub component targets configured" "Fail" "Pub/sub inbound/outbound resiliency targets are missing" `
            "Configure components.pubsub.inbound and components.pubsub.outbound with retry and circuitBreaker references."
    }

    $hasStateStoreTarget = $content -match "(?ms)components:\s*.*?\bstatestore:\s*.*?\bretry:\s*\S+.*?\bcircuitBreaker:\s*\S+"
    if ($hasStateStoreTarget) {
        Add-Result $Category "State store component target configured" "Pass" "State store resiliency target is configured"
    }
    elseif ($script:IsLocalDevelopment) {
        Add-Result $Category "State store component target configured" "Warn" "State store component target is not configured (local development profile)" `
            "Configure statestore retry and circuitBreaker targets for production deployments."
    }
    else {
        Add-Result $Category "State store component target configured" "Fail" "State store resiliency target is missing" `
            "Configure components.statestore with retry and circuitBreaker references."
    }
}

function Test-SecretStore {
    param([string]$ConfigPath)
    $Category = "Secret Store"

    $secretFiles = @(Get-ChildItem -Path $ConfigPath -Filter "secretstore*.yaml" -ErrorAction SilentlyContinue)
    if ($secretFiles.Count -eq 0) {
        Add-Result $Category "Secret store component exists" "Warn" "No secretstore*.yaml found (v1.1 preparation)" `
            "Consider adding a secret store component for v1.1 key management. This is advisory, not blocking."
        return
    }

    Add-Result $Category "Secret store component exists" "Pass" "Secret store component found"
}

function Test-EventStoreFrontedTopology {
    param([string]$ConfigPath)
    $Category = "EventStore Topology"

    $topologyFiles = @(Get-ChildItem -Path $ConfigPath -Filter "*.yaml" -ErrorAction SilentlyContinue |
        Where-Object { (Get-YamlKind (Read-YamlFile $_.FullName)) -eq "PartiesTopology" })

    if ($topologyFiles.Count -eq 0) {
        if ($script:IsLocalDevelopment) {
            Add-Result $Category "Topology manifest present" "Warn" "No EventStore-fronted topology manifest found (local development profile)" `
                "Add topology.yaml for production deployment validation."
        }
        else {
            Add-Result $Category "Topology manifest present" "Fail" "No EventStore-fronted topology manifest found" `
                "Add topology.yaml with eventstore, eventstore-admin, eventstore-admin-ui, parties, tenants, and optional parties-mcp."
        }
        return
    }

    foreach ($topologyFile in $topologyFiles) {
        $content = Read-YamlFile $topologyFile.FullName
        $fileName = $topologyFile.Name

        Add-Result $Category "Topology manifest present ($fileName)" "Pass" "EventStore-fronted topology manifest found"

        $requiredApps = @(
            @{ AppId = "eventstore"; Label = "EventStore gateway"; Recommendation = "Add eventstore to topology.yaml so command/query gateway readiness is validated." },
            @{ AppId = "eventstore-admin"; Label = "EventStore Admin Server"; Recommendation = "Add eventstore-admin to topology.yaml for admin inspection validation." },
            @{ AppId = "eventstore-admin-ui"; Label = "EventStore Admin UI"; Recommendation = "Add eventstore-admin-ui to topology.yaml and wire it to eventstore-admin." },
            @{ AppId = "parties"; Label = "Parties actor host"; Recommendation = "Add parties to topology.yaml so EventStore command routing can invoke the domain host." },
            @{ AppId = "tenants"; Label = "Tenants authority"; Recommendation = "Add tenants to topology.yaml so tenant authority reachability is validated." }
        )

        foreach ($required in $requiredApps) {
            if (Test-AppIdListed $content $required.AppId) {
                Add-Result $Category "$($required.Label) resource ($fileName)" "Pass" "$($required.AppId) resource is declared"
            }
            else {
                Add-Result $Category "$($required.Label) resource ($fileName)" "Fail" "$($required.AppId) resource is missing from topology.yaml" `
                    $required.Recommendation
            }
        }

        $mcpEnabled = Get-YamlValue $content "mcpEnabled" -Mode TopLevel
        if ($mcpEnabled -eq "true") {
            if (Test-AppIdListed $content "parties-mcp") {
                Add-Result $Category "Optional MCP resource ($fileName)" "Pass" "parties-mcp is declared because mcpEnabled=true"
            }
            else {
                Add-Result $Category "Optional MCP resource ($fileName)" "Fail" "parties-mcp resource is missing while mcpEnabled=true" `
                    "Add parties-mcp or set mcpEnabled=false if MCP is not deployed."
            }
        }
        elseif (Test-AppIdListed $content "parties-mcp") {
            Add-Result $Category "Optional MCP resource ($fileName)" "Pass" "parties-mcp is declared as an optional consumer host"
        }
        else {
            Add-Result $Category "Optional MCP resource ($fileName)" "Warn" "parties-mcp is not declared and mcpEnabled is not true" `
                "Declare parties-mcp when deploying the optional MCP consumer host."
        }

        # Match the domain-route triple inside a single `domainServices` list entry so
        # that disjoint values from unrelated stanzas (e.g., `appId: parties` in
        # `metadata.labels`) cannot satisfy the check. Allow optional quoting on all values.
        $domainEntryPattern = "(?ms)-\s+key:\s*[`"']?\*\|party\|v1[`"']?\s*\r?\n(?<body>(?:(?!^\s*-\s+key:|^\s*\w+:\s*$).)*)"
        $domainRouteOk = $false
        foreach ($entry in [regex]::Matches($content, $domainEntryPattern)) {
            $body = $entry.Groups["body"].Value
            if (($body -match "(?m)^\s*appId:\s*[`"']?parties[`"']?\s*$") -and
                ($body -match "(?m)^\s*methodName:\s*[`"']?process[`"']?\s*$") -and
                ($body -match "(?m)^\s*domain:\s*[`"']?party[`"']?\s*$")) {
                $domainRouteOk = $true
                break
            }
        }
        if ($domainRouteOk) {
            Add-Result $Category "Party domain route ($fileName)" "Pass" "Party domain route targets parties/process"
        }
        else {
            Add-Result $Category "Party domain route ($fileName)" "Fail" "Party domain route is missing or malformed" `
                "Configure *|party|v1 with AppId=parties, MethodName=process, and Domain=party inside a single domainServices entry."
        }

        # Anchor adminServerAppId under the `eventStoreAdminUi:` parent so a stray top-level
        # key elsewhere in the file cannot satisfy the wiring check.
        $adminUiPattern = "(?ms)^\s*eventStoreAdminUi:\s*\r?\n(?<body>(?:(?!^\s*\w+:\s*$).)*)"
        $adminUiBody = $null
        $adminUiMatch = [regex]::Match($content, $adminUiPattern)
        if ($adminUiMatch.Success) {
            $adminUiBody = $adminUiMatch.Groups["body"].Value
        }
        if ($null -ne $adminUiBody -and $adminUiBody -match "(?m)^\s*adminServerAppId:\s*[`"']?eventstore-admin[`"']?\s*$") {
            Add-Result $Category "Admin UI wiring ($fileName)" "Pass" "EventStore Admin UI targets the Admin Server resource"
        }
        else {
            Add-Result $Category "Admin UI wiring ($fileName)" "Fail" "EventStore Admin UI wiring is missing" `
                "Set eventStoreAdminUi.adminServerAppId=eventstore-admin."
        }
    }
}

# ===========================================================================
# Story 9.2 -- Kubernetes manifest lint surface
# ===========================================================================
# Detection-only. Does not modify any manifest. Reads deploy/k8s/ for shape
# checks (workload, DAPR annotations, secrets, cloud capabilities) and
# deploy/dapr/ for parity (when both paths supplied). No live cluster, no
# kubectl, no DAPR install, no aspirate invocation.
# ---------------------------------------------------------------------------

# Separate finding storage: each K8s finding is
#   { Category, Code, Severity, Target, Recommendation }
# Severity in {fail, warn, pass}. Target is "<relative-path>" or
# "<relative-path>:<line>" (file-path component pre-sanitized).
$script:K8sResults = New-Object System.Collections.ArrayList

# DAPR-enabled / DAPR-excluded constants mirror
# K8sManifestGenerationTests.daprAppToConfig (line 88-94 of that test) and
# the documented non-DAPR exclusion set. Drift in either side fails both the
# manifest-shape fitness test AND the lint, by design.
$script:K8sDaprEnabledApps = @{
    'eventstore'       = 'accesscontrol'
    'eventstore-admin' = 'accesscontrol-eventstore-admin'
    'parties'          = 'accesscontrol-parties'
    'tenants'          = 'accesscontrol-tenants'
}
$script:K8sDaprExcludedApps = @('eventstore-admin-ui', 'parties-mcp')

# Authoritative DAPR file list mirrors
# K8sManifestGenerationTests.AuthoritativeDaprTemplatesRemainTheBackingComponentSource
# (line 245-263). Extend in BOTH places when adding components.
$script:K8sAuthoritativeDaprFiles = @(
    'statestore.yaml',
    'pubsub.yaml',
    'statestore-cosmosdb.yaml',
    'statestore-postgresql.yaml',
    'pubsub-kafka.yaml',
    'pubsub-rabbitmq.yaml',
    'pubsub-servicebus.yaml',
    'accesscontrol.yaml',
    'accesscontrol.eventstore-admin.yaml',
    'accesscontrol.parties.yaml',
    'accesscontrol.tenants.yaml',
    'subscription-parties.yaml',
    'subscription-tenants.yaml',
    'resiliency.yaml',
    'topology.yaml',
    'tenants-integration.yaml'
)

# Key-allowlist for plaintext-secret scan. Exact-match for full keys, '*' is
# a glob wildcard inside service-discovery keys like services__*__http__*.
$script:K8sSecretKeyAllowlist = @(
    'services__*__http__*',
    'Tenants__ServiceName',
    'Tenants__PubSubName',
    'Tenants__TopicName',
    'Tenants__Enabled',
    'Tenants__CommandApiAppId',
    'EVENTSTORE_HTTP',
    'TENANTS_HTTP',
    'ASPNETCORE_URLS',
    'ASPNETCORE_FORWARDEDHEADERS_ENABLED',
    'HTTP_PORTS',
    'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY',
    'dapr.io/enable-api-logging'
)

# Cloud-only StorageClass / IngressClass names. Names match metadata.name,
# case-insensitively. Service.type=LoadBalancer is handled separately.
$script:K8sCloudStorageClasses = @('managed-csi', 'gp2', 'gp3', 'azurefile', 'standard-rwo', 'pd-standard', 'pd-ssd')
$script:K8sCloudIngressClasses = @('alb', 'nginx-aws', 'gce', 'azure/application-gateway')
$script:K8sCloudServiceAnnotationPrefixes = @(
    'service.beta.kubernetes.io/aws-',
    'service.beta.kubernetes.io/azure-',
    'cloud.google.com/'
)

# Static-tenant-id key pattern.
# P21: tightened to exact-key matches only. The earlier `.*__TenantId$`
# suffix wildcard accidentally matched EventStore registration shapes such
# as `EventStore__DomainServices__Registrations__*|party|v1__TenantId`,
# which legitimately carry `*` (any-tenant wildcard). The explicit
# `Tenants__TenantId` already covers the intended detection surface.
$script:K8sStaticTenantIdPattern = '^(Tenants__TenantId|TENANT_ID|DEFAULT_TENANT)$'

# Truncation budget per category (console mode). JSON mode emits all findings.
$script:K8sConsoleCategoryFindingLimit = 50

function Add-K8sResult {
    param(
        [string]$Category,
        [string]$Code,
        [ValidateSet('fail', 'warn', 'pass')]
        [string]$Severity,
        [string]$Target,
        [string]$Recommendation = ''
    )
    $finding = [PSCustomObject]@{
        Category       = $Category
        Code           = $Code
        Severity       = $Severity
        Target         = (Format-SafePath $Target)
        Recommendation = $Recommendation
    }
    [void]$script:K8sResults.Add($finding)
}

function Format-SafePath {
    # Replace control chars in file paths so a maliciously-named file
    # `evil\n[FAKE:fail]\n.yaml` cannot inject fake findings into the output.
    # Applies to Target field only; recommendation strings are constants.
    # P23: also strip DEL (0x7F) and C1 control range (0x80..0x9F).
    param([string]$Path)
    if ([string]::IsNullOrEmpty($Path)) { return $Path }
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $Path.ToCharArray()) {
        $code = [int][char]$ch
        if ($code -lt 32 -or $code -eq 127 -or ($code -ge 0x80 -and $code -le 0x9F)) {
            [void]$sb.Append('?')
        }
        else {
            [void]$sb.Append($ch)
        }
    }
    return $sb.ToString()
}

function Get-K8sRelativePath {
    param([string]$AbsolutePath, [string]$RootPath)
    if ([string]::IsNullOrEmpty($AbsolutePath) -or [string]::IsNullOrEmpty($RootPath)) {
        return $AbsolutePath
    }
    try {
        $rootResolved = (Resolve-Path -LiteralPath $RootPath -ErrorAction Stop).Path
        $absResolved = $AbsolutePath
        if (Test-Path -LiteralPath $AbsolutePath) {
            $absResolved = (Resolve-Path -LiteralPath $AbsolutePath -ErrorAction Stop).Path
        }
        $rootWithSep = $rootResolved.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        if ($absResolved.StartsWith($rootWithSep, [System.StringComparison]::OrdinalIgnoreCase)) {
            $rel = $absResolved.Substring($rootWithSep.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
            return ($rel -replace '\\', '/')
        }
    }
    catch {
        # Fall through to return raw input — caller may still sanitize.
    }
    return $AbsolutePath
}

function Read-K8sYamlDocuments {
    # Multi-document YAML stream parser. Splits on `---` separators at column
    # zero; returns array of document text blocks (each may be empty/whitespace).
    # Designed for the regex helpers Get-YamlKind / Get-YamlValue / etc. The
    # single-doc Read-YamlFile remains for legacy single-doc shape checks.
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    try {
        $raw = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop
    }
    catch {
        return @()
    }
    if ([string]::IsNullOrWhiteSpace($raw)) { return @() }
    # P16: split on lines that contain ONLY `---` or `...` (the YAML
    # stream-end marker), with an optional trailing comment. This matches
    # both `--- # comment` and `...` separators.
    $docs = [regex]::Split($raw, "(?m)^\s*(?:---|\.\.\.)\s*(?:#.*)?$") |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    return @($docs)
}

function Test-K8sPlaceholderValue {
    # Returns $true if $Value is a recognized placeholder shape (DAPR env-ref,
    # K8s env-var ref, valueFrom marker, REPLACE_ME, empty, etc.).
    # Case-insensitive, whitespace-trimmed, URL-decoded.
    # P22: `*` / `**` are treated as placeholders ONLY for EventStore
    # registration keys (Key starts with `EventStore__DomainServices__Registrations__`).
    # Anywhere else, a bare `*` is a real value and should be inspected.
    param([string]$Value, [string]$Key = '')
    if ($null -eq $Value) { return $true }
    $trimmed = $Value.Trim().Trim("'").Trim('"').Trim()
    if ([string]::IsNullOrEmpty($trimmed)) { return $true }
    # URL-decode for forms like %24%7Benv%3ADB_PASSWORD%7D.
    try {
        $decoded = [System.Net.WebUtility]::UrlDecode($trimmed)
    }
    catch {
        $decoded = $trimmed
    }
    $candidate = $decoded.ToLowerInvariant().Trim()
    # DAPR env-ref: {env:VAR}, {env:VAR|default}
    if ($candidate -match '^\{env:[^}]+\}$') { return $true }
    # K8s env-var ref: $(VAR) or ${VAR}
    if ($candidate -match '^\$\([^)]+\)$') { return $true }
    if ($candidate -match '^\$\{[^}]+\}$') { return $true }
    # valueFrom markers (callers convert these to a sentinel value before calling)
    if ($candidate -match '^valuefrom\.(fieldref|configmapkeyref|secretkeyref|resourcefieldref)$') { return $true }
    # Documented placeholder strings (case-insensitive)
    if ($candidate -in @('replace_me', '<placeholder>', '<set-by-operator>', '<redacted>', 'todo')) {
        return $true
    }
    # P22: Wildcard placeholders are valid ONLY for EventStore registration keys.
    if ($candidate -in @('*', '**')) {
        if ($Key -and $Key.StartsWith('EventStore__DomainServices__Registrations__', [System.StringComparison]::Ordinal)) {
            return $true
        }
    }
    return $false
}

function Test-K8sSecretKeyAllowlisted {
    param([string]$Key)
    if ([string]::IsNullOrEmpty($Key)) { return $false }
    foreach ($entry in $script:K8sSecretKeyAllowlist) {
        if ($entry -eq $Key) { return $true }
        # Glob match: convert `services__*__http__*` to regex.
        if ($entry.Contains('*')) {
            $pattern = '^' + ([regex]::Escape($entry) -replace '\\\*', '.*') + '$'
            if ($Key -match $pattern) { return $true }
        }
    }
    return $false
}

function Get-K8sLineNumberForLiteralKey {
    # Best-effort line lookup. Used only for target hint; redaction never
    # depends on it. Returns 0 if not found.
    # P27: supports a $StartIndex offset so callers searching within a
    # specific YAML document of a multi-doc file get correctly-rebased
    # line numbers. The returned line number is absolute (counted from
    # the start of $Content, not from $StartIndex).
    param([string]$Content, [string]$Key, [int]$StartIndex = 0)
    if ([string]::IsNullOrEmpty($Content) -or [string]::IsNullOrEmpty($Key)) { return 0 }
    if ($StartIndex -lt 0) { $StartIndex = 0 }
    if ($StartIndex -ge $Content.Length) { return 0 }
    $haystack = $Content.Substring($StartIndex)
    # Count lines before the offset so we can rebase the result.
    $linesBefore = 0
    if ($StartIndex -gt 0) {
        $linesBefore = ($Content.Substring(0, $StartIndex) -split "`n").Length - 1
    }
    $lines = $haystack -split "`n"
    $escaped = [regex]::Escape($Key)
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match "(?i)$escaped\s*[=:]") {
            return ($linesBefore + $i + 1)
        }
    }
    return 0
}

function Get-K8sSecretRegexes {
    # Per-regex category code map. Order matters: more specific patterns
    # first so e.g. JWT shape does not get flagged as generic credential.
    # Each entry: @{ Code = '...'; Pattern = '...'; Scope = 'KeyValue'|'Value' }.
    # - 'KeyValue' patterns are applied to the reconstructed `KEY=VALUE` string.
    # - 'Value' patterns are applied to the value only.
    # P1: K8sSecret-PlaintextCredential allows whitespace-containing values
    #     (quoted scalars) but still rejects placeholder leads ({, <, $, ().
    # P2: JWT regex accepts base64 standard alphabet (+, /) and trailing
    #     padding (=) in the final segment.
    # P3: URL-cred scheme matches RFC 3986 (`[a-z][a-z0-9+.\-]*`).
    # P25: Azure connection string match is case-insensitive and accepts
    #      both http; and https; with optional whitespace around =.
    return @(
        @{ Code = 'K8sSecret-UrlEmbeddedCred';   Scope = 'Value'; Pattern = '(?i)[a-z][a-z0-9+.\-]*://[^/\s:@]+:[^/\s@]+@' }
        @{ Code = 'K8sSecret-JwtTokenLiteral';   Scope = 'Value'; Pattern = 'eyJ[A-Za-z0-9_+/\-]{10,}\.[A-Za-z0-9_+/\-]{10,}\.[A-Za-z0-9_+/\-=]{10,}' }
        @{ Code = 'K8sSecret-AwsAccessKey';      Scope = 'Value'; Pattern = 'AKIA[0-9A-Z]{16}' }
        @{ Code = 'K8sSecret-AzureConnString';   Scope = 'Value'; Pattern = '(?i)DefaultEndpointsProtocol\s*=\s*https?;' }
        @{ Code = 'K8sSecret-PrivateKey';        Scope = 'Value'; Pattern = '-----BEGIN [A-Z ]+ PRIVATE KEY-----' }
        @{ Code = 'K8sSecret-PlaintextCredential'; Scope = 'KeyValue'; Pattern = '(?i)(password|pwd|passwd|secret|token|api[_-]?key|client[_-]?secret)\s*[=:]\s*(?:"[^"\r\n]+"|''[^''\r\n]+''|[^\s{<$(][^\r\n#]*)' }
    )
}

# ---------------------------------------------------------------------------
# K8s parsers (multi-doc aware, minimal regex)
# ---------------------------------------------------------------------------

function Get-K8sDocKind {
    param([string]$DocText)
    if ($DocText -match "(?m)^\s*kind:\s*([^\r\n#]+)") {
        return (ConvertFrom-YamlScalar $Matches[1])
    }
    return $null
}

function Get-K8sDocMetadataName {
    # Returns the first `metadata.name` (top-level metadata block) in $DocText.
    param([string]$DocText)
    if ($DocText -match "(?ms)^metadata:\s*\r?\n(?<body>(?:[ \t]+[^\r\n]+\r?\n?)+)") {
        $body = $Matches['body']
        if ($body -match "(?m)^\s*name:\s*([^\r\n#]+)") {
            return (ConvertFrom-YamlScalar $Matches[1])
        }
    }
    # Fallback: any indented name under metadata.
    if ($DocText -match "(?ms)metadata:\s*\r?\n\s+name:\s*([^\r\n#]+)") {
        return (ConvertFrom-YamlScalar $Matches[1])
    }
    return $null
}

function Get-K8sDocLabelApp {
    # P13: anchor exclusively to top-level metadata.labels.app so that
    # selector.matchLabels.app (a different conceptual key) never matches.
    # We locate the top-level `metadata:` block, then scan its inner lines
    # for `labels:` whose body holds `app:`. Anything outside that scope
    # (selector.matchLabels, spec.template.metadata.labels) is ignored.
    param([string]$DocText)
    if ([string]::IsNullOrEmpty($DocText)) { return $null }
    $lines = $DocText -split "`n"
    $metadataIndent = -1
    $i = 0
    while ($i -lt $lines.Length) {
        $line = $lines[$i]
        if ($line -match '^(?<i>[ \t]*)metadata:\s*$') {
            $metadataIndent = $Matches['i'].Length
            $i++
            break
        }
        $i++
    }
    if ($metadataIndent -lt 0) { return $null }
    # Walk metadata body to find labels: at metadataIndent + N indent
    # (anything deeper than metadataIndent, but stop at sibling key).
    $labelsIndent = -1
    while ($i -lt $lines.Length) {
        $line = $lines[$i]
        if ([string]::IsNullOrWhiteSpace($line)) { $i++; continue }
        $indentMatch = [regex]::Match($line, '^(?<i>[ \t]*)\S')
        if (-not $indentMatch.Success) { $i++; continue }
        $lineIndent = $indentMatch.Groups['i'].Length
        if ($lineIndent -le $metadataIndent) { break }
        if ($line -match '^(?<i>[ \t]+)labels:\s*$') {
            $labelsIndent = $Matches['i'].Length
            $i++
            break
        }
        $i++
    }
    if ($labelsIndent -lt 0) { return $null }
    # Walk labels body for app:
    while ($i -lt $lines.Length) {
        $line = $lines[$i]
        if ([string]::IsNullOrWhiteSpace($line)) { $i++; continue }
        $indentMatch = [regex]::Match($line, '^(?<i>[ \t]*)\S')
        if (-not $indentMatch.Success) { $i++; continue }
        $lineIndent = $indentMatch.Groups['i'].Length
        if ($lineIndent -le $labelsIndent) { break }
        if ($line -match '^\s+app:\s*([^\r\n#]+)') {
            return (ConvertFrom-YamlScalar $Matches[1])
        }
        $i++
    }
    return $null
}

function Get-K8sDeploymentImages {
    # Return all container image strings in spec.template.spec.containers.
    # Use [ \t] (not \s) for inline whitespace so a trailing-space-only `image:`
    # line does not greedily consume the next line's content. Empty values are
    # preserved as empty strings so callers can flag missing-image shapes.
    param([string]$DocText)
    $images = @()
    foreach ($m in [regex]::Matches($DocText, "(?m)^[ \t]+image:[ \t]*([^\r\n#]*)")) {
        $val = ConvertFrom-YamlScalar $m.Groups[1].Value
        if ($null -eq $val) { $val = '' }
        $images += $val
    }
    return , $images
}

function Get-K8sDeploymentConfigMapRefs {
    # Return all envFrom.configMapRef.name values.
    param([string]$DocText)
    $names = @()
    foreach ($m in [regex]::Matches($DocText, "(?ms)configMapRef:\s*\r?\n\s+name:\s*([^\r\n#]+)")) {
        $val = ConvertFrom-YamlScalar $m.Groups[1].Value
        if (-not [string]::IsNullOrEmpty($val)) {
            $names += $val
        }
    }
    return , $names
}

function Get-K8sAnnotationsBlock {
    # Returns the annotations block text for a given anchor key
    # (the immediate scope is `metadata:` or `spec.template.metadata:`).
    # Strategy: locate `annotations:` headers and capture each indented body.
    # P26: also expand flow-style `annotations: { k: v, k2: v2 }` into a
    # block-equivalent text so callers (Test-K8sAnnotationPresent and the
    # cloud-annotation prefix scan) see a uniform shape.
    param([string]$DocText)
    $blocks = @()
    foreach ($m in [regex]::Matches($DocText, "(?ms)^(?<indent>[ \t]*)annotations:\s*\r?\n(?<body>(?:\k<indent>[ \t]+[^\r\n]+\r?\n?)+)")) {
        $blocks += $m.Groups['body'].Value
    }
    # Flow-style: `annotations: { foo/bar: 'baz', x: y }`
    foreach ($flow in [regex]::Matches($DocText, "(?m)^[ \t]*annotations:\s*\{(?<body>[^}]*)\}\s*$")) {
        $rendered = New-Object System.Text.StringBuilder
        foreach ($pair in ($flow.Groups['body'].Value -split ',')) {
            $kv = $pair.Trim()
            if ([string]::IsNullOrEmpty($kv)) { continue }
            [void]$rendered.AppendLine("    $kv")
        }
        if ($rendered.Length -gt 0) {
            $blocks += $rendered.ToString()
        }
    }
    return , $blocks
}

function Test-K8sAnnotationPresent {
    param([string[]]$AnnotationBlocks, [string]$Key)
    $escaped = [regex]::Escape($Key)
    foreach ($block in $AnnotationBlocks) {
        if ($block -match "(?m)^\s+$escaped\s*:") {
            return $true
        }
    }
    return $false
}

function Get-K8sContainerSection {
    # P12: structural line-walker. Find the first `containers:` line, capture
    # its indent, then accumulate every subsequent line whose indent is
    # strictly greater than the containers-indent. Stop at the first line
    # whose indent is less-than-or-equal (i.e. a sibling key) or EOF.
    param([string]$DocText)
    if ([string]::IsNullOrEmpty($DocText)) { return $null }
    $lines = $DocText -split "`n"
    $containersIndent = -1
    $bodyLines = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ($containersIndent -lt 0) {
            if ($line -match '^(?<i>[ \t]+)containers:\s*$') {
                $containersIndent = $Matches['i'].Length
            }
            continue
        }
        # Skip blank lines (preserve them in the body) without changing scope.
        if ([string]::IsNullOrWhiteSpace($line)) {
            [void]$bodyLines.Add($line)
            continue
        }
        $indentMatch = [regex]::Match($line, '^(?<i>[ \t]*)\S')
        if (-not $indentMatch.Success) {
            [void]$bodyLines.Add($line)
            continue
        }
        $lineIndent = $indentMatch.Groups['i'].Length
        if ($lineIndent -le $containersIndent) {
            break
        }
        [void]$bodyLines.Add($line)
    }
    if ($containersIndent -lt 0 -or $bodyLines.Count -eq 0) { return $null }
    return ($bodyLines -join "`n")
}

function Test-K8sContainerHasProbes {
    param([string]$ContainerText)
    if ([string]::IsNullOrEmpty($ContainerText)) { return $false }
    $hasReady = $ContainerText -match "(?m)^\s+readinessProbe:"
    $hasLive = $ContainerText -match "(?m)^\s+livenessProbe:"
    return ($hasReady -and $hasLive)
}

function Test-K8sContainerHasResources {
    # P12: structural walker rather than a single regex. Locate `resources:`
    # then within it locate `requests:` and `limits:` sub-blocks (by indent),
    # then within each assert `cpu:` and `memory:` keys are present.
    param([string]$ContainerText)
    if ([string]::IsNullOrEmpty($ContainerText)) { return $false }
    $lines = $ContainerText -split "`n"
    $resourcesIndent = -1
    $resourcesBody = New-Object System.Collections.ArrayList
    $insideResources = $false
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if (-not $insideResources) {
            if ($line -match '^(?<i>[ \t]+)resources:\s*$') {
                $resourcesIndent = $Matches['i'].Length
                $insideResources = $true
            }
            continue
        }
        if ([string]::IsNullOrWhiteSpace($line)) {
            [void]$resourcesBody.Add($line)
            continue
        }
        $indentMatch = [regex]::Match($line, '^(?<i>[ \t]*)\S')
        if (-not $indentMatch.Success) {
            [void]$resourcesBody.Add($line)
            continue
        }
        $lineIndent = $indentMatch.Groups['i'].Length
        if ($lineIndent -le $resourcesIndent) {
            break
        }
        [void]$resourcesBody.Add($line)
    }
    if (-not $insideResources -or $resourcesBody.Count -eq 0) { return $false }

    # Locate requests: and limits: sub-blocks at the next-deeper indent.
    function script:Get-K8sResourceSubBlock {
        param([string[]]$Body, [string]$Header)
        $headerIndent = -1
        $out = New-Object System.Collections.ArrayList
        $started = $false
        foreach ($line in $Body) {
            if (-not $started) {
                if ($line -match "^(?<i>[ \t]+)$Header\s*:\s*$") {
                    $headerIndent = $Matches['i'].Length
                    $started = $true
                }
                continue
            }
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $indentMatch = [regex]::Match($line, '^(?<i>[ \t]*)\S')
            if (-not $indentMatch.Success) { continue }
            $lineIndent = $indentMatch.Groups['i'].Length
            if ($lineIndent -le $headerIndent) { break }
            [void]$out.Add($line)
        }
        return ($out -join "`n")
    }

    $requestsBlock = Get-K8sResourceSubBlock -Body $resourcesBody -Header 'requests'
    $limitsBlock = Get-K8sResourceSubBlock -Body $resourcesBody -Header 'limits'
    if ([string]::IsNullOrEmpty($requestsBlock) -or [string]::IsNullOrEmpty($limitsBlock)) {
        return $false
    }

    $hasReqCpu = $requestsBlock -match "(?m)^\s+cpu:\s+\S"
    $hasReqMem = $requestsBlock -match "(?m)^\s+memory:\s+\S"
    $hasLimCpu = $limitsBlock -match "(?m)^\s+cpu:\s+\S"
    $hasLimMem = $limitsBlock -match "(?m)^\s+memory:\s+\S"
    return ($hasReqCpu -and $hasReqMem -and $hasLimCpu -and $hasLimMem)
}

function Get-K8sKustomizationConfigMapNames {
    # Parse a per-app kustomization.yaml and return all
    # configMapGenerator[*].name values.
    param([string]$KustomizationPath)
    if (-not (Test-Path -LiteralPath $KustomizationPath)) { return @() }
    try {
        $text = Get-Content -LiteralPath $KustomizationPath -Raw
    }
    catch {
        return @()
    }
    $names = @()
    foreach ($m in [regex]::Matches($text, "(?ms)configMapGenerator:\s*\r?\n(?<body>(?:- name:.*?\r?\n(?:\s+.+\r?\n)*)+)")) {
        foreach ($nm in [regex]::Matches($m.Groups['body'].Value, "(?m)^\s*-\s*name:\s*([^\r\n#]+)")) {
            $names += (ConvertFrom-YamlScalar $nm.Groups[1].Value)
        }
    }
    return , $names
}

function Get-K8sKustomizationLiterals {
    # Return @( @{ Key = ...; Value = ...; LineNumber = ... } ) for every
    # `- KEY=VALUE` entry under a configMapGenerator[*].literals block.
    param([string]$KustomizationPath)
    if (-not (Test-Path -LiteralPath $KustomizationPath)) { return @() }
    try {
        $text = Get-Content -LiteralPath $KustomizationPath -Raw
    }
    catch {
        return @()
    }
    $entries = @()
    # Allow the final entry to lack a trailing newline (file may not end with `\n`).
    foreach ($literalsMatch in [regex]::Matches($text, "(?ms)literals:\s*\r?\n(?<body>(?:\s+- [^\r\n]+(?:\r?\n|\z))+)")) {
        foreach ($line in ($literalsMatch.Groups['body'].Value -split "`n")) {
            $trimmed = $line.TrimEnd()
            if ($trimmed -match '^\s*-\s*(?<k>[^=\s][^=]*)=(?<v>.*)$') {
                $rawKey = $Matches['k'].Trim()
                $rawVal = $Matches['v'].Trim()
                $strippedVal = $rawVal -replace "^['""]|['""]$", ''
                $lineNumber = Get-K8sLineNumberForLiteralKey -Content $text -Key $rawKey
                $entries += [PSCustomObject]@{
                    Key        = $rawKey
                    Value      = $strippedVal
                    LineNumber = $lineNumber
                }
            }
        }
    }
    return , $entries
}

function Get-K8sDocContainerEnv {
    # Return @( @{ Name = ...; Value = ...; ValueFrom = $bool; LineNumber = ... } )
    # for every container env entry (excluding envFrom-only blocks).
    #
    # Story 9.3 — handle the standard Kubernetes YAML pattern where env list items are at
    # the SAME indent as the `env:` key (the most common aspirate-emitted shape), as well as
    # the indented-list variant. The previous parser exited the env block on the first
    # list item at `indent == envIndent`, missing all entries.
    param([string]$DocText)
    $entries = @()
    $lines = $DocText -split "`n"
    $inEnv = $false
    $envIndent = -1
    $current = $null
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        # Accept optional trailing comment on the `env:` line itself (e.g., `env: # foo`).
        if ($line -match '^(\s*)env:\s*(#.*)?$') {
            $inEnv = $true
            $envIndent = $Matches[1].Length
            continue
        }
        if ($inEnv) {
            # Skip comment-only lines without exiting the env block.
            if ($line -match '^\s*#') { continue }
            $indentMatch = [regex]::Match($line, '^(\s*)\S')
            if (-not $indentMatch.Success) { continue }
            $indent = $indentMatch.Groups[1].Length
            $isListItem = ($line -match '^\s*-\s')
            # Exit the env block when we encounter a non-list-item line at the same or
            # smaller indent than `env:`. List items at the same indent as `env:` are the
            # standard K8s shape and stay inside the block.
            if ($indent -le $envIndent -and -not $isListItem -and -not [string]::IsNullOrWhiteSpace($line)) {
                $inEnv = $false
                if ($current) { $entries += $current; $current = $null }
                continue
            }
            if ($line -match '^\s*-\s+name:\s*([^\r\n#]+)') {
                if ($current) { $entries += $current }
                $name = ConvertFrom-YamlScalar $Matches[1]
                $current = [PSCustomObject]@{
                    Name       = $name
                    Value      = $null
                    ValueFrom  = $false
                    LineNumber = $i + 1
                }
            }
            elseif ($current -and $line -match '^\s+value:\s*([^\r\n#]+)') {
                $current.Value = ConvertFrom-YamlScalar $Matches[1]
            }
            elseif ($current -and $line -match '^\s+valueFrom:') {
                $current.ValueFrom = $true
            }
        }
    }
    if ($current) { $entries += $current }
    return , $entries
}

# ---------------------------------------------------------------------------
# K8s lint checks
# ---------------------------------------------------------------------------

function Test-K8sWorkload {
    param([string]$K8sRoot)
    $Category = 'K8sWorkload'

    # Per-app deployment.yaml scan (both .yaml and .yml, case-insensitive).
    $appFolders = @(Get-ChildItem -LiteralPath $K8sRoot -Directory -ErrorAction SilentlyContinue)
    foreach ($folder in $appFolders) {
        $deploymentCandidates = @(
            Get-ChildItem -LiteralPath $folder.FullName -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '(?i)^deployment\.ya?ml$' }
        )
        foreach ($deployFile in $deploymentCandidates) {
            $deployPath = $deployFile.FullName
            $relDeploy = Get-K8sRelativePath -AbsolutePath $deployPath -RootPath $K8sRoot
            $docs = Read-K8sYamlDocuments -Path $deployPath
            foreach ($doc in $docs) {
                $kind = Get-K8sDocKind -DocText $doc
                if ($kind -ne 'Deployment') { continue }

                $appId = Get-K8sDocLabelApp -DocText $doc
                if ([string]::IsNullOrEmpty($appId)) {
                    $appId = $folder.Name
                }

                # Image check (fail when missing/empty/placeholder).
                $images = Get-K8sDeploymentImages -DocText $doc
                $hasNonEmptyImage = $false
                if ($images.Count -eq 0) {
                    Add-K8sResult -Category $Category -Code 'K8sWorkload-MissingImage' -Severity 'fail' -Target $relDeploy `
                        -Recommendation 'Deployment.spec.template.spec.containers[].image must be a non-empty image reference (registry/name:tag or @sha256 digest pin).'
                }
                else {
                    foreach ($img in $images) {
                        $imgTrim = $img.Trim()
                        if ([string]::IsNullOrEmpty($imgTrim) -or $imgTrim -eq '[]' -or $imgTrim -eq 'null') {
                            Add-K8sResult -Category $Category -Code 'K8sWorkload-MissingImage' -Severity 'fail' -Target $relDeploy `
                                -Recommendation 'Deployment.spec.template.spec.containers[].image must be a non-empty image reference (registry/name:tag or @sha256 digest pin).'
                        }
                        else {
                            $hasNonEmptyImage = $true
                            # P24: detect both explicit `:latest`, trailing-colon
                            # (empty tag → implicit `:latest`), and no-colon
                            # (no tag → implicit `:latest`). Skip digest-pinned
                            # references (`@sha256:...`) which are immutable.
                            $isDigestPinned = $imgTrim -match '@sha256:[0-9a-fA-F]{64}'
                            $lastSlash = $imgTrim.LastIndexOf('/')
                            $lastSeg = if ($lastSlash -ge 0) { $imgTrim.Substring($lastSlash + 1) } else { $imgTrim }
                            $isLatest = $false
                            if ($imgTrim -match ':latest$') { $isLatest = $true }
                            elseif (-not $isDigestPinned -and ($lastSeg -notmatch ':' -or $imgTrim -match ':\s*$')) {
                                $isLatest = $true
                            }
                            if ($isLatest -and -not $isDigestPinned) {
                                Add-K8sResult -Category $Category -Code 'K8sWorkload-LatestImageTag' -Severity 'warn' -Target $relDeploy `
                                    -Recommendation 'Pin container images to an immutable tag (semver, digest @sha256:, or build-id). The :latest tag (explicit or implicit when no tag is given) is mutable and breaks rollback observability.'
                            }
                        }
                    }
                }
                # Suppress unused-variable strict-mode warning.
                $null = $hasNonEmptyImage

                # DAPR annotation check (only for DAPR-enabled app ids).
                if ($script:K8sDaprEnabledApps.ContainsKey($appId)) {
                    $annotationBlocks = Get-K8sAnnotationsBlock -DocText $doc
                    foreach ($expectedKey in @('dapr.io/enabled', 'dapr.io/app-id', 'dapr.io/app-port', 'dapr.io/config')) {
                        if (-not (Test-K8sAnnotationPresent -AnnotationBlocks $annotationBlocks -Key $expectedKey)) {
                            Add-K8sResult -Category $Category -Code 'K8sWorkload-MissingDaprAnnotation' -Severity 'fail' -Target "$relDeploy#${appId}:$expectedKey" `
                                -Recommendation 'DAPR-enabled Deployments must carry dapr.io/enabled, dapr.io/app-id, dapr.io/app-port, and dapr.io/config on both metadata.annotations and spec.template.metadata.annotations. Run deploy/k8s/regen.ps1 to regenerate.'
                        }
                    }
                }
                elseif ($script:K8sDaprExcludedApps -notcontains $appId -and $folder.Name -notin $script:K8sDaprExcludedApps) {
                    # Unknown app id — neither DAPR-enabled nor explicitly excluded.
                    # Treat as DAPR-disabled (no annotation check) but record neutrality.
                }

                # ConfigMap reference resolution.
                $configMapRefs = Get-K8sDeploymentConfigMapRefs -DocText $doc
                $perAppKustomization = Join-Path $folder.FullName 'kustomization.yaml'
                $availableConfigMapNames = Get-K8sKustomizationConfigMapNames -KustomizationPath $perAppKustomization
                foreach ($cmRef in $configMapRefs) {
                    if ($availableConfigMapNames -notcontains $cmRef) {
                        Add-K8sResult -Category $Category -Code 'K8sWorkload-UnresolvedConfigMapRef' -Severity 'fail' -Target "$relDeploy#${appId}:$cmRef" `
                            -Recommendation 'envFrom.configMapRef.name must resolve to a configMapGenerator.name in the same app folder kustomization.yaml.'
                    }
                }

                # Container shape warns.
                $containerText = Get-K8sContainerSection -DocText $doc
                if (-not (Test-K8sContainerHasProbes -ContainerText $containerText)) {
                    Add-K8sResult -Category $Category -Code 'K8sWorkload-MissingProbes' -Severity 'warn' -Target $relDeploy `
                        -Recommendation 'Add readinessProbe and livenessProbe targeting /ready and /alive (Hexalith.Parties.ServiceDefaults). Hardening deferred to Story 9.3.'
                }
                if (-not (Test-K8sContainerHasResources -ContainerText $containerText)) {
                    Add-K8sResult -Category $Category -Code 'K8sWorkload-MissingResources' -Severity 'warn' -Target $relDeploy `
                        -Recommendation 'Add resources.requests.{cpu,memory} and resources.limits.{cpu,memory}. Per-service envelopes deferred to Story 9.3 (profiling-driven sizing).'
                }
            }
        }
    }

    # Top-level kustomization.yaml resources resolution.
    $topKustomization = Join-Path $K8sRoot 'kustomization.yaml'
    if (Test-Path -LiteralPath $topKustomization) {
        try {
            $topText = Get-Content -LiteralPath $topKustomization -Raw
        }
        catch {
            $topText = ''
        }
        $inResources = $false
        $resourceEntries = @()
        foreach ($line in ($topText -split "`n")) {
            if ($line -match '^\s*resources:\s*$') { $inResources = $true; continue }
            if ($inResources) {
                if ($line -match '^\s*-\s+([^\r\n#]+)') {
                    $resourceEntries += (ConvertFrom-YamlScalar $Matches[1])
                }
                elseif ($line -match '^\s*[A-Za-z]') {
                    $inResources = $false
                }
            }
        }
        $relTopKust = Get-K8sRelativePath -AbsolutePath $topKustomization -RootPath $K8sRoot
        foreach ($entry in $resourceEntries) {
            $resolved = Join-Path $K8sRoot $entry
            if (-not (Test-Path -LiteralPath $resolved)) {
                Add-K8sResult -Category $Category -Code 'K8sWorkload-UnresolvedKustomizationResource' -Severity 'fail' -Target "$relTopKust#$entry" `
                    -Recommendation 'Every entry in deploy/k8s/kustomization.yaml resources: must resolve to an existing file or folder under deploy/k8s/.'
            }
        }
    }
}

function Test-K8sDaprComponentParity {
    param([string]$DaprPath, [string]$K8sRoot)
    $Category = 'DAPR-Parity'

    # Regen invariant: no deploy/k8s/dapr/statestore.yaml, no deploy/k8s/dapr/pubsub.yaml.
    foreach ($placeholderName in @('dapr/statestore.yaml', 'dapr/pubsub.yaml')) {
        $candidate = Join-Path $K8sRoot $placeholderName
        if (Test-Path -LiteralPath $candidate) {
            Add-K8sResult -Category $Category -Code 'DAPR-Regen-PlaceholderNotStripped' -Severity 'fail' -Target (Get-K8sRelativePath -AbsolutePath $candidate -RootPath $K8sRoot) `
                -Recommendation 'deploy/k8s/regen.ps1 must strip aspirate-emitted dapr/{statestore,pubsub}.yaml placeholders. Re-run regen.ps1.'
        }
    }
    # Top-level kustomization.yaml must not reference the stripped placeholders.
    $topKust = Join-Path $K8sRoot 'kustomization.yaml'
    if (Test-Path -LiteralPath $topKust) {
        try {
            $topText = Get-Content -LiteralPath $topKust -Raw
        }
        catch {
            $topText = ''
        }
        if ($topText -match '(?m)^\s*-\s+dapr/(statestore|pubsub)\.yaml\s*$') {
            Add-K8sResult -Category $Category -Code 'DAPR-Regen-PlaceholderNotStripped' -Severity 'fail' -Target (Get-K8sRelativePath -AbsolutePath $topKust -RootPath $K8sRoot) `
                -Recommendation 'deploy/k8s/kustomization.yaml must not reference dapr/statestore.yaml or dapr/pubsub.yaml after regen.ps1.'
        }
    }

    # Authoritative DAPR file list must be complete.
    foreach ($name in $script:K8sAuthoritativeDaprFiles) {
        $candidate = Join-Path $DaprPath $name
        if (-not (Test-Path -LiteralPath $candidate)) {
            Add-K8sResult -Category $Category -Code 'DAPR-Component-MissingAuthoritativeFile' -Severity 'fail' -Target $name `
                -Recommendation 'Authoritative DAPR component file is missing from deploy/dapr/. Restore it from version control.'
        }
    }

    # Access-control / Configuration drift checks.
    $aclFiles = @(Get-ChildItem -LiteralPath $DaprPath -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '(?i)^accesscontrol.*\.yaml$' })
    foreach ($aclFile in $aclFiles) {
        $relAcl = Format-SafePath $aclFile.Name
        $docs = Read-K8sYamlDocuments -Path $aclFile.FullName
        foreach ($doc in $docs) {
            $kind = Get-K8sDocKind -DocText $doc
            if ($kind -ne 'Configuration') { continue }
            # P14: defaultAction: deny must hold for EVERY defaultAction match,
            # not just the first. Per-policy entries may set their own
            # defaultAction; all must be deny.
            $defActionMatches = [regex]::Matches($doc, "(?m)^\s{2,}defaultAction:\s*(\w+)")
            foreach ($defMatch in $defActionMatches) {
                $val = $defMatch.Groups[1].Value.Trim()
                if ($val -ne 'deny') {
                    Add-K8sResult -Category $Category -Code 'DAPR-ACL-DefaultActionNotDeny' -Severity 'fail' -Target $relAcl `
                        -Recommendation 'spec.accessControl.defaultAction must equal deny.'
                    break
                }
            }
            if ($defActionMatches.Count -eq 0) {
                Add-K8sResult -Category $Category -Code 'DAPR-ACL-DefaultActionNotDeny' -Severity 'fail' -Target $relAcl `
                    -Recommendation 'spec.accessControl.defaultAction must equal deny.'
            }
            # P15: wildcard appId regex now accepts flow-style, tag forms, and
            # mismatched quotes. The capture group records `*` or `**`.
            if ($doc -match "(?m)^\s*-\s*appId:\s*(?:!!str\s+)?(?:\[)?(?:[`"']?)(\*{1,2})(?:[`"']?)(?:\])?\s*$") {
                Add-K8sResult -Category $Category -Code 'DAPR-ACL-WildcardAppId' -Severity 'fail' -Target $relAcl `
                    -Recommendation 'Replace wildcard appId entries (* or **) with explicit caller app ids.'
            }
            # P8: operation-path wildcard. `name: *` or `name: foo/*` is
            # flagged; `name: /**` and `name: foo/**` are accepted as the
            # canonical match-all suffix.
            foreach ($opMatch in [regex]::Matches($doc, "(?m)^\s*-\s*name:\s*[`"']?([^`"'\r\n]+)[`"']?\s*$")) {
                $opName = $opMatch.Groups[1].Value.Trim()
                if ($opName.Contains('*') -and $opName -ne '/**' -and -not $opName.EndsWith('/**')) {
                    Add-K8sResult -Category $Category -Code 'DAPR-ACL-WildcardOperation' -Severity 'fail' -Target $relAcl `
                        -Recommendation 'Replace wildcard operation names (* or path*) with explicit paths. Only /** or trailing /** suffixes are allowed.'
                }
            }
            # P6: per-service ACL shape parity. accesscontrol.yaml (umbrella)
            # must list eventstore-admin, tenants, parties policies.
            # accesscontrol.parties.yaml / accesscontrol.tenants.yaml must
            # have defaultAction deny and at least one policy entry.
            # accesscontrol.eventstore-admin.yaml must have policies absent
            # or empty list (locked-down posture).
            $aclLower = $aclFile.Name.ToLowerInvariant()
            $appIdEntries = @()
            foreach ($appMatch in [regex]::Matches($doc, "(?m)^\s*-\s*appId:\s*[`"']?([^`"'\r\n]+?)[`"']?\s*$")) {
                $appIdEntries += $appMatch.Groups[1].Value.Trim()
            }
            if ($aclLower -eq 'accesscontrol.yaml') {
                foreach ($required in @('eventstore-admin', 'tenants', 'parties')) {
                    if ($appIdEntries -notcontains $required) {
                        Add-K8sResult -Category $Category -Code 'DAPR-ACL-MissingPerServiceRule' -Severity 'fail' -Target $relAcl `
                            -Recommendation 'accesscontrol.yaml must list policies for eventstore-admin, tenants, and parties as callers.'
                        break
                    }
                }
            }
            elseif ($aclLower -eq 'accesscontrol.parties.yaml' -or $aclLower -eq 'accesscontrol.tenants.yaml') {
                if ($appIdEntries.Count -eq 0) {
                    Add-K8sResult -Category $Category -Code 'DAPR-ACL-MissingPerServiceRule' -Severity 'fail' -Target $relAcl `
                        -Recommendation 'Per-service ACL must declare at least one explicit caller policy entry (appId: ...).'
                }
            }
            elseif ($aclLower -eq 'accesscontrol.eventstore-admin.yaml') {
                # Locked-down: policies absent or empty list.
                $hasNonEmptyPolicies = $false
                if ($appIdEntries.Count -gt 0) { $hasNonEmptyPolicies = $true }
                if ($hasNonEmptyPolicies) {
                    Add-K8sResult -Category $Category -Code 'DAPR-ACL-MissingPerServiceRule' -Severity 'fail' -Target $relAcl `
                        -Recommendation 'accesscontrol.eventstore-admin.yaml must use policies: [] (locked-down posture).'
                }
            }
        }
    }

    # P7: DAPR-Component shape checks (statestore.actorStateStore=true and
    # pubsub.enableDeadLetter=true; pubsub.type matches `pubsub.<provider>`).
    $componentFiles = @(Get-ChildItem -LiteralPath $DaprPath -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '(?i)\.yaml$' })
    foreach ($compFile in $componentFiles) {
        $relComp = Format-SafePath $compFile.Name
        $docs = Read-K8sYamlDocuments -Path $compFile.FullName
        foreach ($doc in $docs) {
            $kind = Get-K8sDocKind -DocText $doc
            if ($kind -ne 'Component') { continue }
            $compName = Get-K8sDocMetadataName -DocText $doc
            $compType = $null
            if ($doc -match "(?m)^\s+type:\s*([^\r\n#]+)") {
                $compType = (ConvertFrom-YamlScalar $Matches[1])
            }
            $isStatestore = $compName -and $compName.ToLowerInvariant().Contains('statestore')
            $isPubsub = $compType -and $compType -match '^pubsub\.'
            if ($isStatestore) {
                # actorStateStore: true must appear in spec.metadata.
                if ($doc -notmatch "(?ms)name:\s*actorStateStore\s*\r?\n\s+(?:#[^\n]*\r?\n\s+)*value:\s*[`"']?true[`"']?") {
                    Add-K8sResult -Category $Category -Code 'DAPR-Component-MissingActorStateStore' -Severity 'fail' -Target $relComp `
                        -Recommendation 'Statestore component must declare metadata { name: actorStateStore, value: "true" } for actor state.'
                }
            }
            if ($isPubsub) {
                if ($doc -notmatch "(?ms)name:\s*enableDeadLetter\s*\r?\n\s+(?:#[^\n]*\r?\n\s+)*value:\s*[`"']?true[`"']?") {
                    Add-K8sResult -Category $Category -Code 'DAPR-Component-MissingEnableDeadLetter' -Severity 'fail' -Target $relComp `
                        -Recommendation 'Pubsub component must declare metadata { name: enableDeadLetter, value: "true" } for reliable retry semantics.'
                }
                # Provider portion may contain further `.` segments (e.g.
                # pubsub.azure.servicebus.topics). Reject empty or uppercase
                # or non-letter characters in the segments.
                if ($compType -notmatch '^pubsub\.[a-z][a-z0-9.]*$') {
                    Add-K8sResult -Category $Category -Code 'DAPR-Component-InvalidPubsubType' -Severity 'fail' -Target $relComp `
                        -Recommendation 'Pubsub component spec.type must match pubsub.<provider> (lowercase letters, digits, and dots only).'
                }
            }
        }
    }

    # Subscription drift.
    $subFiles = @(Get-ChildItem -LiteralPath $DaprPath -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '(?i)^subscription.*\.yaml$' })
    foreach ($subFile in $subFiles) {
        $relSub = Format-SafePath $subFile.Name
        $docs = Read-K8sYamlDocuments -Path $subFile.FullName
        foreach ($doc in $docs) {
            $kind = Get-K8sDocKind -DocText $doc
            if ($kind -ne 'Subscription') { continue }
            $pubsubname = Get-YamlValue $doc 'pubsubname' -Mode TopLevel
            if ([string]::IsNullOrEmpty($pubsubname)) {
                # Fallback: search anywhere.
                $pubsubname = Get-YamlValue $doc 'pubsubname'
            }
            if ($pubsubname -ne 'pubsub') {
                Add-K8sResult -Category $Category -Code 'DAPR-Subscription-WrongPubsubName' -Severity 'fail' -Target $relSub `
                    -Recommendation 'spec.pubsubname must equal "pubsub" (the authoritative pub/sub component).'
            }
            if ($doc -notmatch '(?m)^\s+deadLetterTopic:\s*\S') {
                Add-K8sResult -Category $Category -Code 'DAPR-Subscription-MissingDeadLetter' -Severity 'fail' -Target $relSub `
                    -Recommendation 'spec.deadLetterTopic must be set and non-empty for reliable retry semantics.'
            }
            # P9: route-shape validation. Accept either:
            #   - `route: /something` (v1alpha1 single-route form), or
            #   - `routes.default: /something` (v2alpha1 default form), or
            #   - `routes.rules[].path: /something` (v2alpha1 rules form).
            $hasRoute = $false
            if ($doc -match "(?m)^\s+route:\s*[`"']?(/\S+)[`"']?\s*$") { $hasRoute = $true }
            elseif ($doc -match "(?ms)^\s+routes:\s*\r?\n(?:[ \t]+[^\r\n]*\r?\n)*?[ \t]+default:\s*[`"']?(/\S+)[`"']?") { $hasRoute = $true }
            elseif ($doc -match "(?ms)^\s+routes:\s*\r?\n(?:[ \t]+[^\r\n]*\r?\n)*?[ \t]+rules:") {
                if ($doc -match "(?m)^\s+path:\s*[`"']?(/\S+)[`"']?") { $hasRoute = $true }
            }
            if (-not $hasRoute) {
                Add-K8sResult -Category $Category -Code 'DAPR-Subscription-InvalidRouteShape' -Severity 'fail' -Target $relSub `
                    -Recommendation 'spec.route or spec.routes.default must be a non-empty path starting with /. v2alpha1 rules form requires routes.rules[].path.'
            }
        }
    }
}

function Test-K8sPlaintextSecrets {
    param([string]$K8sRoot)
    $Category = 'K8sSecret'
    $regexes = Get-K8sSecretRegexes

    $allYamlFiles = @(Get-ChildItem -LiteralPath $K8sRoot -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '(?i)\.ya?ml$' })

    foreach ($yamlFile in $allYamlFiles) {
        $rel = Get-K8sRelativePath -AbsolutePath $yamlFile.FullName -RootPath $K8sRoot
        # P10: sanitize $rel before any recommendation/target interpolation
        # so a maliciously-named file (e.g. with embedded newline) cannot
        # inject fake-finding lines into stdout/stderr.
        $safeRel = Format-SafePath $rel
        # Skip per-app kustomization.yaml when scanning containers/env; handle
        # configMapGenerator.literals via its dedicated parser below. Skip
        # top-level kustomization.yaml entirely (no secrets there).
        $isKustomization = $yamlFile.Name -match '(?i)^kustomization\.ya?ml$'

        if ($isKustomization) {
            $literals = Get-K8sKustomizationLiterals -KustomizationPath $yamlFile.FullName
            foreach ($literal in $literals) {
                if (Test-K8sSecretKeyAllowlisted -Key $literal.Key) { continue }
                # Static tenant id check.
                if ($literal.Key -match $script:K8sStaticTenantIdPattern) {
                    # P22: scope `*`/`**` wildcard-as-placeholder behaviour to
                    # EventStore registration keys only.
                    if (-not (Test-K8sPlaceholderValue -Value $literal.Value -Key $literal.Key)) {
                        $line = $literal.LineNumber
                        $n = $literal.Value.Length
                        Add-K8sResult -Category $Category -Code 'K8sSecret-StaticTenantId' -Severity 'fail' -Target "$($safeRel):$line" `
                            -Recommendation "Tenants__TenantId-shaped keys must reference {env:VAR} or a valueFrom-secret. Found <redacted:$n chars at $($safeRel):$line>."
                    }
                    continue
                }
                if (Test-K8sPlaceholderValue -Value $literal.Value -Key $literal.Key) { continue }
                $keyValue = "$($literal.Key)=$($literal.Value)"
                foreach ($entry in $regexes) {
                    $haystack = if ($entry.Scope -eq 'KeyValue') { $keyValue } else { $literal.Value }
                    if ($haystack -match $entry.Pattern) {
                        $line = $literal.LineNumber
                        $n = $literal.Value.Length
                        Add-K8sResult -Category $Category -Code $entry.Code -Severity 'fail' -Target "$($safeRel):$line" `
                            -Recommendation "Plaintext secret material detected. Replace with {env:VAR} or valueFrom.secretKeyRef. Found <redacted:$n chars at $($safeRel):$line>."
                    }
                }
            }
        }
        else {
            # Multi-doc scan: containers env + Secret.data/stringData.
            $docs = Read-K8sYamlDocuments -Path $yamlFile.FullName
            foreach ($doc in $docs) {
                $kind = Get-K8sDocKind -DocText $doc
                if ($kind -eq 'Secret') {
                    Test-K8sCommittedSecret -DocText $doc -RelPath $rel
                    continue
                }
                # Container env entries.
                $envEntries = Get-K8sDocContainerEnv -DocText $doc
                foreach ($envEntry in $envEntries) {
                    if ($envEntry.ValueFrom) { continue }
                    if (Test-K8sSecretKeyAllowlisted -Key $envEntry.Name) { continue }
                    if ($envEntry.Name -match $script:K8sStaticTenantIdPattern) {
                        if (-not (Test-K8sPlaceholderValue -Value $envEntry.Value -Key $envEntry.Name)) {
                            $line = $envEntry.LineNumber
                            $n = if ($envEntry.Value) { $envEntry.Value.Length } else { 0 }
                            Add-K8sResult -Category $Category -Code 'K8sSecret-StaticTenantId' -Severity 'fail' -Target "$($safeRel):$line" `
                                -Recommendation "Tenants__TenantId-shaped env vars must reference {env:VAR} or valueFrom-secret. Found <redacted:$n chars at $($safeRel):$line>."
                        }
                        continue
                    }
                    if (Test-K8sPlaceholderValue -Value $envEntry.Value -Key $envEntry.Name) { continue }
                    $envKv = "$($envEntry.Name)=$($envEntry.Value)"
                    foreach ($entry in $regexes) {
                        $haystack = if ($entry.Scope -eq 'KeyValue') { $envKv } else { $envEntry.Value }
                        if ($haystack -match $entry.Pattern) {
                            $line = $envEntry.LineNumber
                            $n = $envEntry.Value.Length
                            Add-K8sResult -Category $Category -Code $entry.Code -Severity 'fail' -Target "$($safeRel):$line" `
                                -Recommendation "Plaintext secret material detected in container env. Replace with valueFrom.secretKeyRef. Found <redacted:$n chars at $($safeRel):$line>."
                        }
                    }
                }
            }
        }
    }
}

function Test-K8sCommittedSecret {
    # Detect a `kind: Secret` whose data/stringData contains a non-placeholder value.
    param([string]$DocText, [string]$RelPath)
    $kind = Get-K8sDocKind -DocText $DocText
    if ($kind -ne 'Secret') { return }
    $Category = 'K8sSecret'

    # P10: sanitize $RelPath in recommendation strings.
    $safeRel = Format-SafePath $RelPath

    # P4: locate the start of `stringData:` / `data:` via a start-of-line
    # anchored regex so that an inline reference such as `data:` inside a
    # comment or string scalar does not shift the line offset. The
    # disambiguation between `data:` and `stringData:` is via the capture
    # group: `stringData:` matches before `data:` because the regex tests
    # the `stringData` alternative first.

    # stringData block (raw values).
    if ($DocText -match "(?ms)^stringData:\s*\r?\n(?<body>(?:\s+[^\r\n]+\r?\n?)+)") {
        $body = $Matches['body']
        $lines = $body -split "`n"
        $sdMatch = [regex]::Match($DocText, '(?m)^\s*stringData:')
        if ($sdMatch.Success) {
            # Story 9.3 review fix: the first body line is the line AFTER `stringData:`.
            # ($DocText.Substring(0, idx) -split "`n").Length counts lines preceding the
            # match (≡ the `stringData:` line index in 1-based numbering); +1 lands on the
            # first body line.
            $lineOffset = ($DocText.Substring(0, $sdMatch.Index) -split "`n").Length + 1
        }
        else {
            $lineOffset = 1
        }
        for ($i = 0; $i -lt $lines.Length; $i++) {
            $line = $lines[$i]
            if ($line -match '^\s+(?<k>[^:\s]+):\s*(?<v>.*)$') {
                $val = $Matches['v'].Trim().Trim("'").Trim('"').Trim()
                if (-not (Test-K8sPlaceholderValue -Value $val)) {
                    $n = $val.Length
                    $absLine = $lineOffset + $i
                    Add-K8sResult -Category $Category -Code 'K8sSecret-CommittedSecretValue' -Severity 'fail' -Target "$($safeRel):$absLine" `
                        -Recommendation "Secret.stringData must contain only placeholder values; the real value is managed by an external operator. Found <redacted:$n chars at $($safeRel):$absLine>."
                }
            }
        }
    }

    # data block (base64). Use a start-of-line `data:` anchor that is NOT
    # `stringData:` (negative look-behind via `(?<!string)`).
    if ($DocText -match "(?ms)(?<!string)^data:\s*\r?\n(?<body>(?:\s+[^\r\n]+\r?\n?)+)") {
        $body = $Matches['body']
        $lines = $body -split "`n"
        $dMatch = [regex]::Match($DocText, '(?m)^\s*(?<!string)data:')
        if ($dMatch.Success) {
            # Same off-by-one fix as the stringData block above.
            $lineOffset = ($DocText.Substring(0, $dMatch.Index) -split "`n").Length + 1
        }
        else {
            $lineOffset = 1
        }
        for ($i = 0; $i -lt $lines.Length; $i++) {
            $line = $lines[$i]
            if ($line -match '^\s+(?<k>[^:\s]+):\s*(?<v>.*)$') {
                $valEncoded = $Matches['v'].Trim().Trim("'").Trim('"').Trim()
                if ([string]::IsNullOrEmpty($valEncoded)) { continue }
                try {
                    $decodedBytes = [System.Convert]::FromBase64String($valEncoded)
                    $decoded = [System.Text.Encoding]::UTF8.GetString($decodedBytes)
                }
                catch {
                    $decoded = $valEncoded
                }
                if (-not (Test-K8sPlaceholderValue -Value $decoded)) {
                    # Story 9.3 review fix: report decoded byte length (operator-facing real
                    # secret size), not the base64-encoded char count (~4/3 of the byte count).
                    # Falls back to base64 length on decode failure.
                    $n = if ($null -ne $decodedBytes) { $decodedBytes.Length } else { $valEncoded.Length }
                    $absLine = $lineOffset + $i
                    Add-K8sResult -Category $Category -Code 'K8sSecret-CommittedSecretValue' -Severity 'fail' -Target "$($safeRel):$absLine" `
                        -Recommendation "Secret.data must contain only base64-encoded placeholder values; the real value is managed by an external operator. Found <redacted:$n bytes at $($safeRel):$absLine>."
                }
            }
        }
    }

    # P11: multi-line literal-scalar private-key detection. A YAML block
    # scalar like `stringData: { cert: | \n  -----BEGIN ... -----` lives
    # outside the line-by-line `key: value` parser above; scan the doc text
    # for inline PEM headers (any leading whitespace, any private-key
    # type label). Each match emits a redacted K8sSecret-PrivateKey finding.
    foreach ($pemMatch in [regex]::Matches($DocText, '(?ms)^[ \t]+-----BEGIN [A-Z ]+ PRIVATE KEY-----')) {
        $absLine = ($DocText.Substring(0, $pemMatch.Index) -split "`n").Length
        $n = $pemMatch.Length
        Add-K8sResult -Category $Category -Code 'K8sSecret-PrivateKey' -Severity 'fail' -Target "$($safeRel):$absLine" `
            -Recommendation "Plaintext PEM private key detected in Secret block-scalar. Replace with valueFrom.secretKeyRef. Found <redacted:$n chars at $($safeRel):$absLine>."
    }
}

function Test-K8sCloudCapabilities {
    # P17: [switch]$Allow lets callers use `-Allow:$AllowCloudCapabilities`
    # directly (no `.IsPresent` indirection). Internal logic uses
    # `$Allow.IsPresent` to disambiguate from a bare $true value.
    param([string]$K8sRoot, [switch]$Allow)
    $Category = 'K8s-Local'
    $sev = if ($Allow.IsPresent) { 'warn' } else { 'fail' }
    $allYamlFiles = @(Get-ChildItem -LiteralPath $K8sRoot -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '(?i)\.ya?ml$' })
    foreach ($yamlFile in $allYamlFiles) {
        $rel = Get-K8sRelativePath -AbsolutePath $yamlFile.FullName -RootPath $K8sRoot
        $safeRel = Format-SafePath $rel
        $docs = Read-K8sYamlDocuments -Path $yamlFile.FullName
        foreach ($doc in $docs) {
            $kind = Get-K8sDocKind -DocText $doc
            $name = Get-K8sDocMetadataName -DocText $doc
            if ($kind -eq 'StorageClass' -and $name -and ($script:K8sCloudStorageClasses -contains $name.ToLowerInvariant())) {
                Add-K8sResult -Category $Category -Code 'K8s-NonLocalClusterCapability' -Severity $sev -Target $safeRel `
                    -Recommendation 'Cloud-provider StorageClass detected. Local-cluster MVP must not ship cloud-only resources. See deploy/k8s/README.md "Out of MVP scope".'
            }
            if ($kind -eq 'IngressClass' -and $name -and ($script:K8sCloudIngressClasses -contains $name.ToLowerInvariant())) {
                Add-K8sResult -Category $Category -Code 'K8s-NonLocalClusterCapability' -Severity $sev -Target $safeRel `
                    -Recommendation 'Cloud-provider IngressClass detected. Local-cluster MVP must not ship cloud-only resources. See deploy/k8s/README.md "Out of MVP scope".'
            }
            if ($kind -eq 'Service') {
                $serviceType = $null
                if ($doc -match "(?m)^\s+type:\s*([^\r\n#]+)") {
                    $serviceType = (ConvertFrom-YamlScalar $Matches[1])
                }
                if ($serviceType -eq 'LoadBalancer') {
                    Add-K8sResult -Category $Category -Code 'K8s-NonLocalClusterCapability' -Severity $sev -Target $safeRel `
                        -Recommendation 'Service of type LoadBalancer detected. Local-cluster MVP uses port-forward (see deploy/k8s/README.md "Out of MVP scope").'
                }
                $annotationBlocks = Get-K8sAnnotationsBlock -DocText $doc
                foreach ($block in $annotationBlocks) {
                    foreach ($prefix in $script:K8sCloudServiceAnnotationPrefixes) {
                        $escaped = [regex]::Escape($prefix)
                        if ($block -match "(?m)^\s+$escaped") {
                            Add-K8sResult -Category $Category -Code 'K8s-NonLocalClusterCapability' -Severity $sev -Target $safeRel `
                                -Recommendation 'Cloud-provider Service annotation detected. Local-cluster MVP must not ship cloud-only resources. See deploy/k8s/README.md "Out of MVP scope".'
                            break
                        }
                    }
                }
            }
        }
    }
}

# P5: path-traversal detection. Symlink within $K8sRoot pointing outside
# the root (e.g. `/etc/passwd`) is rejected: emits a K8s-PathTraversal
# fail. Filesystem reparse points are detected via FileInfo.Attributes.
function Test-K8sPathTraversal {
    param([string]$K8sRoot)
    $Category = 'K8s-Generic'
    $rootResolved = $null
    try {
        $rootResolved = (Resolve-Path -LiteralPath $K8sRoot -ErrorAction Stop).Path.TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    }
    catch {
        return
    }
    $files = @()
    try {
        $files = @(Get-ChildItem -LiteralPath $K8sRoot -Recurse -Force -ErrorAction SilentlyContinue)
    }
    catch {
        return
    }
    foreach ($entry in $files) {
        $attrs = $null
        try {
            $attrs = [System.IO.FileInfo]::new($entry.FullName).Attributes
        }
        catch {
            continue
        }
        $isReparsePoint = ($attrs -band [System.IO.FileAttributes]::ReparsePoint) -eq [System.IO.FileAttributes]::ReparsePoint
        if (-not $isReparsePoint) { continue }
        $resolvedTarget = $null
        try {
            $resolvedTarget = (Resolve-Path -LiteralPath $entry.FullName -ErrorAction Stop).Path
        }
        catch {
            $rel = Get-K8sRelativePath -AbsolutePath $entry.FullName -RootPath $K8sRoot
            $safeRel = Format-SafePath $rel
            Add-K8sResult -Category $Category -Code 'K8s-PathTraversal' -Severity 'fail' -Target $safeRel `
                -Recommendation 'Symbolic link or reparse point detected and could not be resolved. Remove symlinks from deploy/k8s/.'
            continue
        }
        if (-not $resolvedTarget.StartsWith($rootResolved, [System.StringComparison]::OrdinalIgnoreCase)) {
            $rel = Get-K8sRelativePath -AbsolutePath $entry.FullName -RootPath $K8sRoot
            $safeRel = Format-SafePath $rel
            Add-K8sResult -Category $Category -Code 'K8s-PathTraversal' -Severity 'fail' -Target $safeRel `
                -Recommendation 'Symbolic link escapes deploy/k8s/. Remove symlinks to files outside the manifest root.'
        }
    }
}

# ---------------------------------------------------------------------------
# K8s output emitters
# ---------------------------------------------------------------------------

function Get-SortedK8sResults {
    # Sort ascending by (Category, Code, Target) so byte-identical output
    # across consecutive runs is preserved. PowerShell hashtable enumeration
    # is not trusted; sort explicitly.
    $arr = @($script:K8sResults)
    if ($arr.Count -eq 0) { return @() }
    return @($arr | Sort-Object -Property Category, Code, Target)
}

function Write-K8sConsoleSummary {
    # Console output: grouped by category code, truncated at the per-category
    # budget. Findings of severity `pass` are not printed (no successes today;
    # absence of fail/warn is the success signal).
    param([array]$Results)
    if (-not $Results -or $Results.Count -eq 0) {
        Write-Host '--- K8s Manifest Lint ---' -ForegroundColor White
        Write-Host '  [PASS] No K8s findings.' -ForegroundColor Green
        Write-Host ''
        return
    }

    Write-Host '--- K8s Manifest Lint ---' -ForegroundColor White
    $byCategoryCode = $Results | Group-Object -Property Code
    foreach ($group in $byCategoryCode) {
        $items = @($group.Group)
        $shown = [Math]::Min($items.Count, $script:K8sConsoleCategoryFindingLimit)
        for ($i = 0; $i -lt $shown; $i++) {
            $r = $items[$i]
            $icon = switch ($r.Severity) {
                'fail' { '[FAIL]' }
                'warn' { '[WARN]' }
                'pass' { '[PASS]' }
            }
            $color = switch ($r.Severity) {
                'fail' { 'Red' }
                'warn' { 'Yellow' }
                'pass' { 'Green' }
            }
            Write-Host "  $icon $($r.Code) " -ForegroundColor $color -NoNewline
            Write-Host "$($r.Target)"
            if ($r.Recommendation) {
                Write-Host "         -> $($r.Recommendation)" -ForegroundColor Yellow
            }
        }
        if ($items.Count -gt $shown) {
            $suppressed = $items.Count - $shown
            Write-Host "  $suppressed additional findings suppressed -- re-run with --output json for full list" -ForegroundColor DarkYellow
        }
    }
    Write-Host ''
}

# ---------------------------------------------------------------------------
# Story 9.3 lint categories (additive — Story 9.2 categories remain unchanged)
# ---------------------------------------------------------------------------

# Story 9.3 AC7 — Topology contract: every AppHost-composed service id MUST have a per-app
# manifest folder under deploy/k8s/<app-id>/ AND the folder MUST contain a Deployment + a
# Service whose selector matches the Deployment labels.
$script:K8sTopologyExpectedAppFolders = @(
    'eventstore'
    'eventstore-admin'
    'eventstore-admin-ui'
    'parties'
    'parties-mcp'
    'tenants'
    'memories'
    'keycloak'
    'redis'
)
# Service-type values that legitimately have no clusterIP / no selector and must NOT
# trigger the missing-Service rule (Story 9.3 Outcome B FrontComposer carve-out is handled
# by ABSENCE from the expected set above, not by Service-type exception).
$script:K8sTopologyServiceTypeWhitelist = @('ExternalName')

function Test-K8sTopology {
    param([string]$K8sRoot)
    $Category = 'K8sTopology'

    # Threshold gate: the topology contract only applies to "full topology" trees. Synthetic
    # fixtures used by other tests (Story 9.2 K8sManifestLintTests) typically ship 1-2 apps
    # and would otherwise drown in spurious missing-app findings. Run the lint only when at
    # least half of the expected apps are present in the tree, indicating the operator (or a
    # full-topology test fixture) intended to validate the complete deploy/k8s topology.
    $presentCount = 0
    foreach ($appId in $script:K8sTopologyExpectedAppFolders) {
        if (Test-Path -LiteralPath (Join-Path $K8sRoot $appId) -PathType Container) {
            $presentCount++
        }
    }
    $threshold = [int][Math]::Ceiling($script:K8sTopologyExpectedAppFolders.Count / 2.0)
    if ($presentCount -lt $threshold) {
        # Story 9.3 review fix: emit a warn-severity informational finding instead of a
        # completely silent skip. A regression that wipes most of the topology would
        # otherwise be undetectable by the lint. Operators / CI logs see the gate trip.
        $expectedCount = $script:K8sTopologyExpectedAppFolders.Count
        $safeRoot = Format-SafePath $K8sRoot
        Add-K8sResult -Category $Category -Code 'K8sTopology-ThresholdGateTripped' -Severity 'warn' -Target $safeRoot `
            -Recommendation "Topology lint skipped: only $presentCount of $expectedCount expected app folders present (threshold $threshold). Synthetic fixtures hit this path legitimately; on the full deploy tree this signals topology collapse — investigate before merging."
        return
    }

    foreach ($appId in $script:K8sTopologyExpectedAppFolders) {
        $appDir = Join-Path $K8sRoot $appId
        $relApp = Get-K8sRelativePath -AbsolutePath $appDir -RootPath $K8sRoot
        $safeRel = Format-SafePath $relApp

        if (-not (Test-Path -LiteralPath $appDir -PathType Container)) {
            Add-K8sResult -Category $Category -Code 'K8sTopology-MissingService' -Severity 'fail' -Target $safeRel `
                -Recommendation "Expected per-app folder is missing. Story 9.3 / FR31a requires deploy/k8s/$appId/ to be emitted (aspirate) or hand-authored (carve-out). See deploy/k8s/regen.ps1 `$ExpectedAppFolders + this validator's `$K8sTopologyExpectedAppFolders constant — keep both in lockstep."
            continue
        }

        # Read all YAML documents in the app folder (deployment.yaml + service.yaml + any
        # kustomization.yaml siblings). Collect kinds + their selectors / labels.
        $deploymentLabels = @{}
        $serviceSelectors = @{}
        $serviceTypes = @{}
        $hasService = $false

        Get-ChildItem -Path $appDir -Recurse -Include '*.yaml', '*.yml' -File | ForEach-Object {
            $docs = Read-K8sYamlDocuments -Path $_.FullName
            foreach ($doc in $docs) {
                $kind = Get-K8sDocKind -DocText $doc
                if ($kind -eq 'Deployment') {
                    # Capture spec.selector.matchLabels.app.
                    if ($doc -match '(?ms)^\s*selector:\s*\r?\n\s*matchLabels:\s*\r?\n(?<lbl>(?:\s+[^\r\n]+\r?\n?)+)') {
                        $blk = $Matches['lbl']
                        if ($blk -match '(?m)^\s+app:\s*(?<v>\S+)\s*$') {
                            $deploymentLabels['app'] = $Matches['v']
                        }
                    }
                }
                elseif ($kind -eq 'Service') {
                    $hasService = $true
                    # Capture spec.type.
                    $svcType = 'ClusterIP'
                    if ($doc -match '(?m)^\s*type:\s*(?<t>\S+)\s*$') {
                        $svcType = $Matches['t']
                    }
                    $serviceTypes['type'] = $svcType
                    # Capture spec.selector.app.
                    if ($doc -match '(?ms)^\s*selector:\s*\r?\n(?<sel>(?:\s+[^\r\n]+\r?\n?)+)') {
                        $sel = $Matches['sel']
                        if ($sel -match '(?m)^\s+app:\s*(?<v>\S+)\s*$') {
                            $serviceSelectors['app'] = $Matches['v']
                        }
                    }
                }
            }
        }

        if (-not $hasService) {
            Add-K8sResult -Category $Category -Code 'K8sTopology-MissingService' -Severity 'fail' -Target $safeRel `
                -Recommendation "Per-app folder is present but contains no Service. Each app-folder must ship a Service so the in-cluster DNS name resolves (e.g., Dapr Components, peer service-invocation)."
            continue
        }

        # If both Deployment and Service exist, the Service selector MUST match the Deployment
        # labels. Whitelisted Service types (ExternalName / headless) are exempt — they
        # legitimately have no selector.
        if ($serviceTypes.ContainsKey('type') -and ($script:K8sTopologyServiceTypeWhitelist -contains $serviceTypes['type'])) {
            continue
        }
        if ($deploymentLabels.ContainsKey('app') -and $serviceSelectors.ContainsKey('app') -and
            ($deploymentLabels['app'] -ne $serviceSelectors['app'])) {
            Add-K8sResult -Category $Category -Code 'K8sTopology-MissingService' -Severity 'fail' -Target $safeRel `
                -Recommendation "Service selector.app=$($serviceSelectors['app']) does not match Deployment selector.matchLabels.app=$($deploymentLabels['app']) — pods will not be routed to."
        }
    }
}

# Story 9.3 AC7 — JWT signing-key literal scan. Any non-empty literal value for an env-var
# key matching ^Authentication__JwtBearer__SigningKey$ in either a ConfigMap literal block,
# a Deployment container env entry, or a Secret data/stringData entry fires this category.
# `valueFrom.secretKeyRef` references pass (env entries with `valueFrom:` and no inline `value`).
$script:K8sJwtSigningKeyName = 'Authentication__JwtBearer__SigningKey'

function Test-K8sJwtSigningKeyLiteral {
    param([string]$K8sRoot)
    $Category = 'K8sSecret'
    $keyPattern = "^$([regex]::Escape($script:K8sJwtSigningKeyName))$"

    Get-ChildItem -Path $K8sRoot -Recurse -Include '*.yaml', '*.yml' -File | ForEach-Object {
        $rel = Get-K8sRelativePath -AbsolutePath $_.FullName -RootPath $K8sRoot
        $safeRel = Format-SafePath $rel
        $isKustomization = ($_.Name -eq 'kustomization.yaml')

        if ($isKustomization) {
            $literals = Get-K8sKustomizationLiterals -KustomizationPath $_.FullName
            foreach ($literal in $literals) {
                if ($literal.Key -match $keyPattern) {
                    if (-not [string]::IsNullOrEmpty($literal.Value)) {
                        $n = $literal.Value.Length
                        Add-K8sResult -Category $Category -Code 'K8sSecret-JwtSigningKeyLiteral' -Severity 'fail' -Target "$($safeRel):$($literal.LineNumber)" `
                            -Recommendation "JWT SigningKey must be sourced via valueFrom.secretKeyRef (Secret name: hexalith-jwt-signing). Found <redacted:$n chars at $($safeRel):$($literal.LineNumber)>."
                    }
                }
            }
        }
        else {
            $docs = Read-K8sYamlDocuments -Path $_.FullName
            foreach ($doc in $docs) {
                $kind = Get-K8sDocKind -DocText $doc
                if ($kind -eq 'Secret') {
                    # Test stringData first, then data (base64-decoded probe).
                    if ($doc -match "(?ms)^stringData:\s*\r?\n(?<body>(?:\s+[^\r\n]+\r?\n?)+)") {
                        $body = $Matches['body']
                        $sdMatch = [regex]::Match($doc, '(?m)^\s*stringData:')
                        $offset = if ($sdMatch.Success) { ($doc.Substring(0, $sdMatch.Index) -split "`n").Length } else { 1 }
                        $lines = $body -split "`n"
                        for ($i = 0; $i -lt $lines.Length; $i++) {
                            if ($lines[$i] -match '^\s+(?<k>[^:\s]+):\s*(?<v>.*)$') {
                                $k = $Matches['k']; $v = $Matches['v'].Trim().Trim("'").Trim('"').Trim()
                                if ($k -match $keyPattern -and -not [string]::IsNullOrEmpty($v)) {
                                    $n = $v.Length
                                    $absLine = $offset + $i
                                    Add-K8sResult -Category $Category -Code 'K8sSecret-JwtSigningKeyLiteral' -Severity 'fail' -Target "$($safeRel):$absLine" `
                                        -Recommendation "JWT SigningKey in Secret.stringData must be a placeholder (real value bootstrapped by deploy-local.ps1). Found <redacted:$n chars at $($safeRel):$absLine>."
                                }
                            }
                        }
                    }
                    if ($doc -match "(?ms)^data:\s*\r?\n(?<body>(?:\s+[^\r\n]+\r?\n?)+)") {
                        $body = $Matches['body']
                        $dMatch = [regex]::Match($doc, '(?m)^\s*data:')
                        # Story 9.3 review fix: +1 so the first body line lines up with the
                        # first line AFTER `data:` (off-by-one was reporting the `data:` line
                        # itself).
                        $offset = if ($dMatch.Success) { ($doc.Substring(0, $dMatch.Index) -split "`n").Length + 1 } else { 1 }
                        $lines = $body -split "`n"
                        for ($i = 0; $i -lt $lines.Length; $i++) {
                            if ($lines[$i] -match '^\s+(?<k>[^:\s]+):\s*(?<v>.*)$') {
                                $k = $Matches['k']; $v = $Matches['v'].Trim().Trim("'").Trim('"').Trim()
                                if ($k -match $keyPattern -and -not [string]::IsNullOrEmpty($v)) {
                                    # Story 9.3 review fix: report decoded byte length (the
                                    # real signing-key size operators reason about), not the
                                    # base64-encoded char count (~4/3 inflation). Fall back to
                                    # base64 char count if the value is not valid base64.
                                    try {
                                        $decodedBytes = [System.Convert]::FromBase64String($v)
                                        $n = $decodedBytes.Length
                                        $unit = 'bytes'
                                    }
                                    catch {
                                        $n = $v.Length
                                        $unit = 'chars'
                                    }
                                    $absLine = $offset + $i
                                    Add-K8sResult -Category $Category -Code 'K8sSecret-JwtSigningKeyLiteral' -Severity 'fail' -Target "$($safeRel):$absLine" `
                                        -Recommendation "JWT SigningKey in Secret.data (base64) must be a placeholder. Found <redacted:$n $unit at $($safeRel):$absLine>."
                                }
                            }
                        }
                    }
                    continue
                }
                # Container env entries with inline `value:` (valueFrom skips).
                $envEntries = Get-K8sDocContainerEnv -DocText $doc
                foreach ($envEntry in $envEntries) {
                    if ($envEntry.ValueFrom) { continue }
                    if ($envEntry.Name -match $keyPattern -and -not [string]::IsNullOrEmpty($envEntry.Value)) {
                        $n = $envEntry.Value.Length
                        $line = $envEntry.LineNumber
                        Add-K8sResult -Category $Category -Code 'K8sSecret-JwtSigningKeyLiteral' -Severity 'fail' -Target "$($safeRel):$line" `
                            -Recommendation "JWT SigningKey container env must reference valueFrom.secretKeyRef (Secret name: hexalith-jwt-signing). Found <redacted:$n chars at $($safeRel):$line>."
                    }
                }
            }
        }
    }
}

# Story 9.3 AC7 — Dapr resiliency CRD schema-drift static lint. Detects the legacy field
# shapes that Dapr 1.14.4+ rejects: (a) nested `timeouts.daprSidecar.general`, and (b)
# `targets.components.<name>.{retry,timeout,circuitBreaker}` flat shape (must be split into
# `outbound`/`inbound` for components that have both directions). Tolerates unknown
# `apiVersion` (warn-severity informational) for graceful CRD-version-skew handling.
function Test-K8sResiliencyCrdSchemaDrift {
    param([string]$DaprPath)
    if ([string]::IsNullOrEmpty($DaprPath)) { return }
    $Category = 'K8sDapr'
    $resiliencyPath = Join-Path $DaprPath 'resiliency.yaml'
    if (-not (Test-Path -LiteralPath $resiliencyPath)) { return }

    $relTarget = 'deploy/dapr/resiliency.yaml'
    $safeRel = Format-SafePath $relTarget
    $text = Get-Content -Raw -LiteralPath $resiliencyPath
    # Story 9.3 review fix: strip comment-only lines before structural matching so a
    # commented-out legacy shape (or unrelated documentation prose) cannot false-fire the
    # `K8sDapr-ResiliencyCrdSchemaDrift` regexes below. Inline comments (`field: value  # …`)
    # are left alone since they do not affect the structural anchor.
    $textNoComments = ($text -split "`n" | Where-Object { $_ -notmatch '^\s*#' }) -join "`n"

    # apiVersion graceful skew handling.
    if ($text -match '(?m)^apiVersion:\s*(?<v>\S+)\s*$') {
        $apiVersion = $Matches['v']
        if ($apiVersion -notmatch '^dapr\.io/v1alpha1$') {
            Add-K8sResult -Category $Category -Code 'K8sDapr-ResiliencyCrdSchemaDrift' -Severity 'warn' -Target $safeRel `
                -Recommendation "Resiliency CR uses apiVersion '$apiVersion'; this validator was authored for 'dapr.io/v1alpha1' (Dapr 1.14.4). Verify the active CRD schema against the deployed Dapr control plane."
        }
    }

    # Legacy shape 1: nested `timeouts.daprSidecar.general`.
    # Story 9.3 review fix: tighten the anchor by requiring `daprSidecar:` to be a direct
    # indent-child of `timeouts:` (indent-group backreference), and run against the
    # comment-stripped text so commented legacy shapes do not false-fire.
    if ($textNoComments -match '(?ms)^(?<i>[ \t]*)timeouts:\s*\r?\n(?:\k<i>[ \t]+[^\r\n]*\r?\n)*?\k<i>[ \t]+daprSidecar:\s*\r?\n\k<i>[ \t]+[ \t]+general:\s*\S+') {
        Add-K8sResult -Category $Category -Code 'K8sDapr-ResiliencyCrdSchemaDrift' -Severity 'fail' -Target $safeRel `
            -Recommendation "spec.policies.timeouts.daprSidecar must be a Duration scalar (e.g., daprSidecar: 5s), not a nested object with a 'general' key. Dapr 1.14.4 CRD rejects the nested form."
    }

    # Legacy shape 2: `targets.components.<name>.{retry,timeout,circuitBreaker}` flat shape.
    # The active CRD requires `outbound`/`inbound` split for component targets. Detect by
    # finding the `components:` block and walking each named component; flag any that have
    # `retry:` / `timeout:` / `circuitBreaker:` as a direct child instead of inside an
    # `outbound:` or `inbound:` sub-block.
    if ($textNoComments -match '(?ms)^\s*components:\s*\r?\n(?<body>(?:\s+[^\r\n]+\r?\n?)+)') {
        $componentsBody = $Matches['body']
        # Split into per-component blocks. A component is `  <name>:\n` followed by an
        # indented sub-block until the next outdent.
        $compMatches = [regex]::Matches($componentsBody, '(?ms)^(?<indent>\s+)(?<name>[A-Za-z0-9_-]+):\s*\r?\n(?<sub>(?:\1\s+[^\r\n]+\r?\n?)+)')
        foreach ($cm in $compMatches) {
            $compName = $cm.Groups['name'].Value
            $sub = $cm.Groups['sub'].Value
            # Flat shape detection: a `retry:` / `timeout:` / `circuitBreaker:` line at the
            # FIRST indent level inside the component block (i.e., a sibling of `outbound`/
            # `inbound`, not a child).
            $firstIndent = $cm.Groups['indent'].Value + '  '
            $flatRetry = [regex]::Match($sub, "(?m)^$([regex]::Escape($firstIndent))(retry|timeout|circuitBreaker):\s*")
            if ($flatRetry.Success) {
                Add-K8sResult -Category $Category -Code 'K8sDapr-ResiliencyCrdSchemaDrift' -Severity 'fail' -Target $safeRel `
                    -Recommendation "spec.targets.components.$compName has flat {retry,timeout,circuitBreaker} keys; Dapr 1.14.4 CRD requires the outbound/inbound split for components that have both directions."
            }
        }
    }
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

# Validate argument set.
# Use [Console]::Error.WriteLine instead of Write-Error so that
# $ErrorActionPreference = 'Stop' does not short-circuit the exit code.
if ([string]::IsNullOrEmpty($ConfigPath) -and [string]::IsNullOrEmpty($K8sPath)) {
    if ($Output -eq 'json') {
        @{ error = 'At least one of --config-path or -K8sPath is required.'; timestamp = (Get-Date -Format 'o') } | ConvertTo-Json
    }
    else {
        [Console]::Error.WriteLine('At least one of --config-path or -K8sPath is required.')
    }
    exit 2
}

# Validate config path (Story 8.1 mode).
$resolvedPath = $null
if ($ConfigPath) {
    if (-not (Test-Path $ConfigPath)) {
        if ($Output -eq "json") {
            @{ error = "Config path not found: $ConfigPath"; timestamp = (Get-Date -Format "o"); configPath = $ConfigPath } | ConvertTo-Json
        }
        else {
            [Console]::Error.WriteLine("Config path not found: $ConfigPath")
        }
        exit 2
    }
    $resolvedPath = (Resolve-Path $ConfigPath).Path
}

# Validate K8s path (Story 9.2 mode).
$resolvedK8sPath = $null
if ($K8sPath) {
    if (-not (Test-Path $K8sPath)) {
        if ($Output -eq 'json') {
            @{ error = "k8s-path not found: $K8sPath"; timestamp = (Get-Date -Format 'o'); k8sPath = $K8sPath } | ConvertTo-Json
        }
        else {
            [Console]::Error.WriteLine("k8s-path not found: $K8sPath")
        }
        exit 2
    }
    $resolvedK8sPath = (Resolve-Path $K8sPath).Path
}

# Run Story 8.1 validation checks (DAPR-only).
if ($resolvedPath) {
    Test-AccessControl $resolvedPath
    Test-EventStoreFrontedTopology $resolvedPath
    Test-StateStore $resolvedPath
    Test-PubSub $resolvedPath
    Test-Subscription $resolvedPath
    Test-TenantsIntegration $resolvedPath
    Test-Resiliency $resolvedPath
    Test-SecretStore $resolvedPath
}

# Run Story 9.2 K8s manifest lint.
if ($resolvedK8sPath) {
    try {
        Test-K8sPathTraversal -K8sRoot $resolvedK8sPath
        Test-K8sWorkload -K8sRoot $resolvedK8sPath
        Test-K8sPlaintextSecrets -K8sRoot $resolvedK8sPath
        Test-K8sCloudCapabilities -K8sRoot $resolvedK8sPath -Allow:$AllowCloudCapabilities
        if ($resolvedPath) {
            Test-K8sDaprComponentParity -DaprPath $resolvedPath -K8sRoot $resolvedK8sPath
        }
        # Story 9.3 — additive lint categories.
        Test-K8sTopology -K8sRoot $resolvedK8sPath
        Test-K8sJwtSigningKeyLiteral -K8sRoot $resolvedK8sPath
        if ($resolvedPath) {
            Test-K8sResiliencyCrdSchemaDrift -DaprPath $resolvedPath
        }
    }
    catch {
        # Bounded catch: surface category-coded message, never raw exception.
        Add-K8sResult -Category 'K8s-Generic' -Code 'K8s-YamlParseError' -Severity 'fail' -Target 'deploy/k8s' `
            -Recommendation 'A K8s lint pass aborted due to an unexpected parser fault. Re-run with --output json to inspect; raw exception details are intentionally suppressed.'
    }
}

# ---------------------------------------------------------------------------
# Output results
# ---------------------------------------------------------------------------

$totalChecks = $script:Results.Count
$passed = @($script:Results | Where-Object { $_.Status -eq "Pass" }).Count
$failed = @($script:Results | Where-Object { $_.Status -eq "Fail" }).Count
$warnings = @($script:Results | Where-Object { $_.Status -eq "Warn" }).Count

# K8s findings (Story 9.2): sorted ascending by (Category, Code, Target) for
# deterministic emission. Severity contributes to the aggregate exit code.
$sortedK8s = @(Get-SortedK8sResults)
$k8sFail = @($sortedK8s | Where-Object { $_.Severity -eq 'fail' }).Count
$k8sWarn = @($sortedK8s | Where-Object { $_.Severity -eq 'warn' }).Count
$k8sPass = @($sortedK8s | Where-Object { $_.Severity -eq 'pass' }).Count

$totalFailed = $failed + $k8sFail
$totalWarn = $warnings + $k8sWarn

if ($Output -eq "json") {
    $checks = @()
    foreach ($r in $script:Results) {
        $checks += [ordered]@{
            category       = $r.Category
            check          = $r.Check
            status         = $r.Status
            details        = $r.Details
            recommendation = $r.Recommendation
        }
    }
    # Story 9.2 K8s findings carry a different schema per AC5:
    # { category, code, severity, target, recommendation }. No value/raw fields.
    $k8sFindings = @()
    foreach ($f in $sortedK8s) {
        $k8sFindings += [ordered]@{
            category       = $f.Category
            code           = $f.Code
            severity       = $f.Severity
            target         = $f.Target
            recommendation = $f.Recommendation
        }
    }
    $jsonOutput = [ordered]@{
        timestamp  = (Get-Date -Format "o")
        configPath = $resolvedPath
        k8sPath    = $resolvedK8sPath
        summary    = [ordered]@{
            total    = $totalChecks
            passed   = $passed
            failed   = $failed
            warnings = $warnings
            k8sFail  = $k8sFail
            k8sWarn  = $k8sWarn
            k8sPass  = $k8sPass
            result   = if ($totalFailed -gt 0) { "FAIL" } else { "PASS" }
        }
        checks       = $checks
        k8sFindings  = $k8sFindings
    }
    # P20: depth 16 covers worst-case finding-object nesting without truncation.
    $jsonOutput | ConvertTo-Json -Depth 16
}
else {
    Write-Host ""
    Write-Host "=====================================================================" -ForegroundColor Cyan
    Write-Host " Hexalith.Parties -- Deployment Validation Report" -ForegroundColor Cyan
    Write-Host "=====================================================================" -ForegroundColor Cyan
    if ($resolvedPath) {
        Write-Host " Config Path : $resolvedPath"
    }
    if ($resolvedK8sPath) {
        Write-Host " K8s Path    : $resolvedK8sPath"
    }
    Write-Host " Timestamp   : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Host "=====================================================================" -ForegroundColor Cyan
    Write-Host ""

    $categories = @($script:Results | Select-Object -ExpandProperty Category -Unique)

    foreach ($cat in $categories) {
        Write-Host "--- $cat ---" -ForegroundColor White
        $catResults = @($script:Results | Where-Object { $_.Category -eq $cat })
        foreach ($r in $catResults) {
            $icon = switch ($r.Status) {
                "Pass" { "[PASS]" }
                "Fail" { "[FAIL]" }
                "Warn" { "[WARN]" }
            }
            $color = switch ($r.Status) {
                "Pass" { "Green" }
                "Fail" { "Red" }
                "Warn" { "Yellow" }
            }
            Write-Host "  $icon " -ForegroundColor $color -NoNewline
            Write-Host "$($r.Check)"
            if ($r.Details) {
                Write-Host "         $($r.Details)" -ForegroundColor Gray
            }
            if ($r.Recommendation -and $r.Status -ne "Pass") {
                Write-Host "         -> $($r.Recommendation)" -ForegroundColor Yellow
            }
        }
        Write-Host ""
    }

    # Story 9.2 K8s manifest lint section (only when -K8sPath was supplied).
    if ($resolvedK8sPath) {
        Write-K8sConsoleSummary -Results $sortedK8s
    }

    Write-Host "=====================================================================" -ForegroundColor Cyan
    Write-Host " SUMMARY: $totalChecks DAPR checks | $passed passed | $failed failed | $warnings warnings" -ForegroundColor Cyan
    if ($resolvedK8sPath) {
        Write-Host " K8S LINT: $($sortedK8s.Count) findings | $k8sFail fail | $k8sWarn warn | $k8sPass pass" -ForegroundColor Cyan
    }

    if ($totalFailed -gt 0) {
        Write-Host " RESULT: FAIL -- Address failed checks before production deployment" -ForegroundColor Red
    }
    else {
        Write-Host " RESULT: PASS -- Deployment validated" -ForegroundColor Green
    }
    Write-Host "=====================================================================" -ForegroundColor Cyan
    Write-Host ""
}

# Exit code: aggregate Story 8.1 fail + Story 9.2 K8s fail.
if ($totalFailed -gt 0) {
    exit 1
}
else {
    exit 0
}
