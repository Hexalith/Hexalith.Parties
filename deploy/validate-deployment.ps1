#!/usr/bin/env pwsh
# ===========================================================================
# Hexalith.Parties -- Deployment Validation Tool
# ===========================================================================
# Verifies DAPR security configuration before production use.
# Checks access control, state store, pub/sub, subscription, resiliency,
# and secret store configurations for security best practices.
#
# Usage:
#   ./validate-deployment.ps1 --config-path ./deploy/dapr
#   ./validate-deployment.ps1 --config-path ./deploy/dapr --output json
#
# Exit codes:
#   0 = All checks passed (warnings may exist)
#   1 = One or more checks failed
#   2 = Invalid arguments or config path not found
#
# Reference: docs/deployment-security-checklist.md
# ===========================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Alias("config-path")]
    [string]$ConfigPath,

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
    foreach ($requiredScope in $Required) {
        if ($Scopes -notcontains $requiredScope) {
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
    return $Content -match "(?m)^\s*-\s*$escaped\s*$"
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
    return $Content -match "(?ms)-\s*appId:\s*[`"']?$escapedAppId[`"']?.*?operations:\s*.*?name:\s*[`"']?$escapedPath[`"']?.*?httpVerb:\s*\[[^\]]*$escapedVerb[^\]]*\].*?action:\s*allow"
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

        if ($partiesContent -match "appId:\s*['`"]?\*['`"]?" -or $partiesContent -match "name:\s*['`"]?/\*\*['`"]?") {
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
    elseif (($adminContent -match "(?m)^\s{2,4}defaultAction:\s*deny") -and ($adminContent -match "policies:\s*\[\]")) {
        Add-Result $Category "EventStore Admin sidecar locked down" "Pass" "Admin Server receiving sidecar has no DAPR peer callers"
    }
    else {
        Add-Result $Category "EventStore Admin sidecar locked down" "Fail" "Admin Server sidecar is not locked down" `
            "Keep accesscontrol.eventstore-admin.yaml deny-by-default with policies: []."
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

            if (Test-TopicAllowedForApp $pubScopes "eventstore" "sample.parties.events") {
                Add-Result $Category "eventstore can publish party events ($fileName)" "Pass" "EventStore publisher scope is configured"
            }
            elseif ($isLocalDevPubSub) {
                Add-Result $Category "eventstore can publish party events ($fileName)" "Warn" "EventStore publisher scope is not configured (local development profile)" `
                    "Add publishingScopes entry eventstore=sample.parties.events for production."
            }
            else {
                Add-Result $Category "eventstore can publish party events ($fileName)" "Fail" "EventStore publisher scope is missing" `
                    "Add publishingScopes entry eventstore=sample.parties.events."
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

        if ($content -match '\*\|party\|v1' -and
            $content -match "(?m)^\s*appId:\s*parties\s*$" -and
            $content -match "(?m)^\s*methodName:\s*process\s*$" -and
            $content -match "(?m)^\s*domain:\s*party\s*$") {
            Add-Result $Category "Party domain route ($fileName)" "Pass" "Party domain route targets parties/process"
        }
        else {
            Add-Result $Category "Party domain route ($fileName)" "Fail" "Party domain route is missing or malformed" `
                "Configure *|party|v1 with AppId=parties, MethodName=process, and Domain=party."
        }

        if ($content -match "(?m)^\s*adminServerAppId:\s*eventstore-admin\s*$") {
            Add-Result $Category "Admin UI wiring ($fileName)" "Pass" "EventStore Admin UI targets the Admin Server resource"
        }
        else {
            Add-Result $Category "Admin UI wiring ($fileName)" "Fail" "EventStore Admin UI wiring is missing" `
                "Set eventStoreAdminUi.adminServerAppId=eventstore-admin."
        }
    }
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

# Validate config path
if (-not (Test-Path $ConfigPath)) {
    if ($Output -eq "json") {
        @{ error = "Config path not found: $ConfigPath"; timestamp = (Get-Date -Format "o"); configPath = $ConfigPath } | ConvertTo-Json
    }
    else {
        Write-Error "Config path not found: $ConfigPath"
    }
    exit 2
}

$resolvedPath = (Resolve-Path $ConfigPath).Path

# Run all validation checks
Test-AccessControl $resolvedPath
Test-EventStoreFrontedTopology $resolvedPath
Test-StateStore $resolvedPath
Test-PubSub $resolvedPath
Test-Subscription $resolvedPath
Test-TenantsIntegration $resolvedPath
Test-Resiliency $resolvedPath
Test-SecretStore $resolvedPath

# ---------------------------------------------------------------------------
# Output results
# ---------------------------------------------------------------------------

$totalChecks = $script:Results.Count
$passed = @($script:Results | Where-Object { $_.Status -eq "Pass" }).Count
$failed = @($script:Results | Where-Object { $_.Status -eq "Fail" }).Count
$warnings = @($script:Results | Where-Object { $_.Status -eq "Warn" }).Count

if ($Output -eq "json") {
    $checks = @()
    foreach ($r in $script:Results) {
        $checks += @{
            category       = $r.Category
            check          = $r.Check
            status         = $r.Status
            details        = $r.Details
            recommendation = $r.Recommendation
        }
    }
    $jsonOutput = @{
        timestamp  = (Get-Date -Format "o")
        configPath = $resolvedPath
        summary    = @{
            total    = $totalChecks
            passed   = $passed
            failed   = $failed
            warnings = $warnings
            result   = if ($failed -gt 0) { "FAIL" } else { "PASS" }
        }
        checks     = $checks
    }
    $jsonOutput | ConvertTo-Json -Depth 5
}
else {
    Write-Host ""
    Write-Host "=====================================================================" -ForegroundColor Cyan
    Write-Host " Hexalith.Parties -- Deployment Validation Report" -ForegroundColor Cyan
    Write-Host "=====================================================================" -ForegroundColor Cyan
    Write-Host " Config Path : $resolvedPath"
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

    Write-Host "=====================================================================" -ForegroundColor Cyan
    Write-Host " SUMMARY: $totalChecks checks | $passed passed | $failed failed | $warnings warnings" -ForegroundColor Cyan

    if ($failed -gt 0) {
        Write-Host " RESULT: FAIL -- Address failed checks before production deployment" -ForegroundColor Red
    }
    else {
        Write-Host " RESULT: PASS -- Deployment validated" -ForegroundColor Green
    }
    Write-Host "=====================================================================" -ForegroundColor Cyan
    Write-Host ""
}

# Exit code
if ($failed -gt 0) {
    exit 1
}
else {
    exit 0
}
