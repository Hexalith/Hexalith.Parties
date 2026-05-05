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

function Get-YamlValue {
    param([string]$Content, [string]$Key)
    # metadata list style: - name: key \n   value: val
    if ($Content -match "(?ms)name:\s*$Key\s*\r?\n\s*(?:#[^\n]*\r?\n\s*)*value:\s*[`"']?([^`"'\r\n]+)[`"']?") {
        return $Matches[1].Trim()
    }
    if ($Content -match "(?m)^\s*$Key\s*:\s*[`"']?([^`"'\r\n#]+)[`"']?") {
        return $Matches[1].Trim()
    }
    return $null
}

function Get-YamlScopes {
    param([string]$Content)
    $scopes = @()
    if ($Content -match "(?ms)^scopes:\s*\r?\n((?:\s*-\s*[^\r\n]+[\r\n]*)*)") {
        $block = $Matches[1]
        $lines = $block -split "[\r\n]+"
        foreach ($line in $lines) {
            if ($line -match '^\s*-\s*[`"'']?([^`"''\r\n]+)[`"'']?\s*$') {
                $scopes += $Matches[1].Trim()
            }
        }
    }
    return , $scopes
}

function Get-YamlKind {
    param([string]$Content)
    if ($Content -match "(?m)^kind:\s*(\S+)") {
        return $Matches[1].Trim()
    }
    return $null
}

function Get-YamlApiVersion {
    param([string]$Content)
    if ($Content -match "(?m)^apiVersion:\s*(\S+)") {
        return $Matches[1].Trim()
    }
    return $null
}

function Get-YamlType {
    param([string]$Content)
    if ($Content -match "(?m)^\s*type:\s*(\S+)") {
        return $Matches[1].Trim()
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
        if ($parts[0].Trim() -ne $AppId) { continue }
        $topics = @($parts[1] -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        if ($topics -contains $Topic) { return $true }
    }
    return $false
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
        if ($scopes.Count -eq 0) {
            Add-Result $Category "Scopes restrict to commandapi ($fileName)" "Fail" "No scopes defined" `
                "Add scopes list containing ONLY 'commandapi'. No other app-id needs state store access."
        }
        elseif ($scopes.Count -eq 1 -and ($scopes[0] -eq "commandapi")) {
            Add-Result $Category "Scopes restrict to commandapi ($fileName)" "Pass" "Scopes: [commandapi] only"
        }
        else {
            $scopeList = $scopes -join ", "
            Add-Result $Category "Scopes restrict to commandapi ($fileName)" "Fail" "Scopes contain non-commandapi entries: [$scopeList]" `
                "State store scopes should contain ONLY 'commandapi'. Remove other app-ids."
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
        if ($scopes.Count -gt 0) {
            Add-Result $Category "Scopes defined ($fileName)" "Pass" "Component scopes: [$($scopes -join ', ')]"
        }
        else {
            Add-Result $Category "Scopes defined ($fileName)" "Fail" "No component scopes defined" `
                "Add scopes list with commandapi and authorized subscriber app-ids."
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
                if ($scope -notmatch "\{env:" -and $scope -ne "commandapi") {
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
    $expectedAppId = "commandapi"

    $tenantConfigFiles = @(Get-ChildItem -Path $ConfigPath -Filter "*tenants*.yaml" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike "subscription*" })
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
            $fileName = $cfg.Name
            $pubsubName = Get-YamlValue $content "pubsubName"
            $topicName = Get-YamlValue $content "topicName"
            $appId = Get-YamlValue $content "commandApiAppId"
            $bypass = Get-YamlValue $content "bypassTenantsAuthorization"
            $dependencyHealth = Get-YamlValue $content "tenantsDependencyHealth"

            if ($pubsubName -eq $expectedPubSub -and $topicName -eq $expectedTopic -and $appId -eq $expectedAppId) {
                Add-Result $Category "Tenants configuration values ($fileName)" "Pass" "Tenants config targets $expectedPubSub/$expectedTopic for $expectedAppId"
            }
            else {
                Add-Result $Category "Tenants configuration values ($fileName)" "Fail" "Missing or malformed Tenants config values" `
                    "Set pubsubName=$expectedPubSub, topicName=$expectedTopic, and commandApiAppId=$expectedAppId. Impact: tenant access projection may not receive authoritative Tenants state. Remediation target: $fileName."
            }

            if ($bypass -eq "true") {
                Add-Result $Category "Parties does not bypass Tenants authorization ($fileName)" "Fail" "bypassTenantsAuthorization is true" `
                    "Remove bypass mode. Impact: JWT tenant claims could be treated as authorization. Remediation target: Parties authorization configuration."
            }
            else {
                Add-Result $Category "Parties does not bypass Tenants authorization ($fileName)" "Pass" "No Tenants authorization bypass configured"
            }

            if ($dependencyHealth -match "^(unhealthy|unreachable|missing)$") {
                Add-Result $Category "Tenants dependency health ($fileName)" "Fail" "Tenants dependency signal is $dependencyHealth" `
                    "Verify Hexalith.Tenants deployment, service discovery, and event publishing. Impact: Parties fails closed or uses stale tenant access state."
            }
            elseif ($dependencyHealth) {
                Add-Result $Category "Tenants dependency health ($fileName)" "Pass" "Tenants dependency signal is $dependencyHealth"
            }
        }
    }

    $tenantSubscriptions = @(Get-ChildItem -Path $ConfigPath -Filter "subscription*.yaml" -ErrorAction SilentlyContinue |
        Where-Object {
            $content = Read-YamlFile $_.FullName
            ($content -match "(?m)^\s*topic:\s*[`"']?$expectedTopic[`"']?") -or
            ($content -match "(?m)^\s*topicName:\s*[`"']?$expectedTopic[`"']?")
        })

    if ($tenantSubscriptions.Count -eq 0) {
        if ($script:IsLocalDevelopment) {
            Add-Result $Category "Tenants subscription present" "Warn" "No declarative subscription for $expectedTopic found (local development profile)" `
                "Ensure MapTenantEventSubscription() is active locally or add subscription-tenants.yaml for production."
        }
        else {
            Add-Result $Category "Tenants subscription present" "Fail" "No Tenants subscription for $expectedTopic found" `
                "Add a DAPR subscription for $expectedTopic routed to commandapi. Impact: Parties local tenant projection will not update. Remediation target: subscription-tenants.yaml or MapTenantEventSubscription()."
        }
    }
    else {
        foreach ($sub in $tenantSubscriptions) {
            $content = Read-YamlFile $sub.FullName
            $fileName = $sub.Name
            $pubsubName = Get-YamlValue $content "pubsubname"
            $scopes = Get-YamlScopes $content

            if ($pubsubName -eq $expectedPubSub) {
                Add-Result $Category "Tenants subscription pub/sub ($fileName)" "Pass" "Subscription uses pubsubname: $expectedPubSub"
            }
            else {
                Add-Result $Category "Tenants subscription pub/sub ($fileName)" "Fail" "Subscription pubsubname is '$pubsubName'" `
                    "Set pubsubname to '$expectedPubSub'. Impact: Tenants events may be routed through the wrong component."
            }

            if ($scopes -contains $expectedAppId) {
                Add-Result $Category "Tenants subscription scoped to commandapi ($fileName)" "Pass" "Subscription scopes include $expectedAppId"
            }
            else {
                Add-Result $Category "Tenants subscription scoped to commandapi ($fileName)" "Fail" "Subscription scopes do not include $expectedAppId" `
                    "Add commandapi to subscription scopes. Impact: the Parties Tenants event subscription cannot run."
            }

            if ($content -match "(?m)deadLetterTopic:") {
                Add-Result $Category "Tenants subscription dead-letter ($fileName)" "Pass" "Dead-letter topic is configured"
            }
            else {
                Add-Result $Category "Tenants subscription dead-letter ($fileName)" "Fail" "No deadLetterTopic configured for Tenants subscription" `
                    "Add a deadLetterTopic and resiliency policy. Impact: failed Tenants events can be lost silently."
            }
        }
    }

    $pubsubFiles = @(Get-ChildItem -Path $ConfigPath -Filter "pubsub*.yaml" -ErrorAction SilentlyContinue)
    foreach ($psFile in $pubsubFiles) {
        $content = Read-YamlFile $psFile.FullName
        if ((Get-YamlKind $content) -ne "Component") { continue }
        $fileName = $psFile.Name
        $subScopes = Get-YamlValue $content "subscriptionScopes"
        if (Test-TopicAllowedForApp $subScopes $expectedAppId $expectedTopic) {
            Add-Result $Category "commandapi can subscribe to Tenants topic ($fileName)" "Pass" "$expectedAppId is allowed to subscribe to $expectedTopic"
        }
        elseif ($script:IsLocalDevelopment -or ((Get-YamlType $content) -eq "pubsub.redis")) {
            Add-Result $Category "commandapi can subscribe to Tenants topic ($fileName)" "Warn" "No explicit commandapi subscription scope for $expectedTopic (local development profile)" `
                "Add subscriptionScopes entry '$expectedAppId=$expectedTopic' for production scoping."
        }
        else {
            Add-Result $Category "commandapi can subscribe to Tenants topic ($fileName)" "Fail" "Missing commandapi subscription permission for $expectedTopic" `
                "Add '$expectedAppId=$expectedTopic' to subscriptionScopes. Impact: production DAPR scoping blocks Tenants events from Parties."
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
