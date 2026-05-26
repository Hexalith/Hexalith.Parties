#Requires -Version 7
<#
.SYNOPSIS
Tears down the Hexalith.Parties Kubernetes topology.

.EXIT CODES
0 Success or absent namespace no-op.
1 General bounded operational failure.
2 ConfirmContext mismatch or missing current context.
3 Required CLI missing.
7 Residual state detected.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ConfirmContext,

    [switch] $PurgeNamespace,

    [switch] $PurgeDapr
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExitGeneral = 1
$ExitContext = 2
$ExitCliMissing = 3
$ExitResidual = 7
$Script:Step = 0
$K8sRoot = $PSScriptRoot
$DeployRoot = Split-Path -Parent $K8sRoot
$RepoRoot = Split-Path -Parent $DeployRoot
$DaprRoot = Join-Path $DeployRoot 'dapr'
$Namespace = 'hexalith-parties'
$OperatorSecrets = @('hexalith-jwt-signing', 'hexalith-keycloak-admin', 'zot-pull-secret')
$KustomizeResourceFolders = @(
    'eventstore',
    'eventstore-admin',
    'eventstore-admin-ui',
    'memories',
    'parties',
    'parties-mcp',
    'tenants',
    'redis',
    'keycloak',
    'falkordb'
)
$OwnedResourceNames = @(
    'deployment.apps/eventstore',
    'deployment.apps/eventstore-admin',
    'deployment.apps/eventstore-admin-ui',
    'deployment.apps/parties',
    'deployment.apps/parties-mcp',
    'deployment.apps/tenants',
    'deployment.apps/memories',
    'deployment.apps/keycloak',
    'deployment.apps/redis',
    'deployment.apps/falkordb',
    'service/eventstore',
    'service/eventstore-admin',
    'service/eventstore-admin-ui',
    'service/parties',
    'service/parties-mcp',
    'service/tenants',
    'service/memories',
    'service/keycloak',
    'service/redis',
    'service/falkordb',
    'configmap/eventstore-env',
    'configmap/eventstore-admin-env',
    'configmap/eventstore-admin-ui-env',
    'configmap/memories-env',
    'configmap/parties-env',
    'configmap/parties-mcp-env',
    'configmap/tenants-env',
    'configmap/keycloak-realm',
    'secret/hexalith-jwt-signing',
    'secret/hexalith-keycloak-admin',
    'secret/zot-pull-secret',
    'component.dapr.io/statestore',
    'component.dapr.io/pubsub',
    'configuration.dapr.io/accesscontrol',
    'configuration.dapr.io/accesscontrol-eventstore-admin',
    'configuration.dapr.io/accesscontrol-parties',
    'configuration.dapr.io/accesscontrol-tenants',
    'configuration.dapr.io/accesscontrol-memories',
    'subscription.dapr.io/parties-events-reference',
    'subscription.dapr.io/tenant-lifecycle-events',
    'resiliency.dapr.io/resiliency'
)

. (Join-Path $K8sRoot '_lib/Confirm-KubeContext.ps1')

function Fail([int] $Code, [string] $Message) {
    Write-Host "[teardown] ERROR: $Message"
    exit $Code
}

function Write-Step([string] $Message) {
    Write-Host "[teardown] Step $($Script:Step): $Message"
    $Script:Step++
}

function Require-Command([string] $Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Fail $ExitCliMissing "required CLI '$Name' was not found on PATH"
    }
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [int] $FailureCode = $ExitGeneral,

        [string] $FailureMessage = 'command failed'
    )

    $output = & $FilePath @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        $bounded = ($output | Select-Object -First 20) -join [Environment]::NewLine
        Fail $FailureCode "$FailureMessage (exit $exitCode). $bounded"
    }

    return $output
}

function Test-NamespaceExists {
    & kubectl get namespace $Namespace 1>$null 2>$null
    return $LASTEXITCODE -eq 0
}

function Get-OwnedResiduals {
    $remaining = @()
    foreach ($resource in $OwnedResourceNames) {
        & kubectl get $resource -n $Namespace 1>$null 2>$null
        if ($LASTEXITCODE -eq 0) {
            $remaining += $resource
        }
    }

    return @($remaining)
}

function Get-AllNamespaceResources {
    $output = & kubectl api-resources --verbs=list --namespaced -o name 2>$null
    if ($LASTEXITCODE -ne 0) {
        Fail $ExitResidual 'unable to enumerate namespaced API resources before namespace purge'
    }

    $items = @()
    foreach ($resource in ($output | Where-Object { $_ -and $_ -notmatch 'events' })) {
        $resourceItems = & kubectl get $resource -n $Namespace -o name --ignore-not-found 2>&1
        if ($LASTEXITCODE -ne 0) {
            $bounded = ($resourceItems | Select-Object -First 5) -join [Environment]::NewLine
            Fail $ExitResidual "unable to list namespaced resource '$resource' before namespace purge. $bounded"
        }

        $items += @($resourceItems | Where-Object { $_ })
    }

    return @($items)
}

Require-Command 'kubectl'

try {
    Write-Step 'Confirm Kubernetes context'
    [void](Assert-KubeContext -Expected $ConfirmContext)
}
catch {
    Write-Host "[teardown] ERROR: $($_.Exception.Message)"
    exit $ExitContext
}

Write-Step 'Check namespace'
if (-not (Test-NamespaceExists)) {
    Write-Host "[teardown] namespace $Namespace not present - nothing to delete"
    exit 0
}

Write-Step 'Delete Kubernetes workload kustomizations'
foreach ($folder in $KustomizeResourceFolders) {
    [void](Invoke-Checked 'kubectl' @('delete', '-k', (Join-Path $K8sRoot $folder), '--ignore-not-found=true') $ExitGeneral "kustomize delete failed for $folder")
}

Write-Step 'Delete Dapr CRs'
[void](Invoke-Checked 'kubectl' @('delete', '-f', $DaprRoot, '--ignore-not-found=true') $ExitGeneral 'Dapr CR delete failed')

Write-Step 'Delete operator-managed Secrets'
foreach ($secret in $OperatorSecrets) {
    [void](Invoke-Checked 'kubectl' @('delete', 'secret', $secret, '-n', $Namespace, '--ignore-not-found=true') $ExitGeneral "secret delete failed for $secret")
}

Write-Step 'Probe residual owned resources'
$ownedResiduals = @(Get-OwnedResiduals)
if ($ownedResiduals.Count -gt 0) {
    $bounded = ($ownedResiduals | Sort-Object | Select-Object -First 25) -join ', '
    Fail $ExitResidual "Residual state detected - manual intervention required before next publish. owned=$($ownedResiduals.Count): $bounded"
}

if ($PurgeNamespace) {
    Write-Step 'Validate namespace purge safety'
    $allResiduals = @(Get-AllNamespaceResources)
    if ($allResiduals.Count -gt 0) {
        $bounded = ($allResiduals | Sort-Object | Select-Object -First 25) -join ', '
        Fail $ExitResidual "Residual state detected - manual intervention required before next publish. non-story=$($allResiduals.Count): $bounded"
    }

    Write-Step 'Purge namespace'
    [void](Invoke-Checked 'kubectl' @('delete', 'namespace', $Namespace, '--ignore-not-found=true', '--wait=true') $ExitGeneral 'namespace purge failed')
}
else {
    Write-Host "[teardown] OK: namespace $Namespace clean"
}

if ($PurgeDapr) {
    Write-Step 'Purge Dapr control plane'
    Require-Command 'dapr'
    [void](Invoke-Checked 'dapr' @('uninstall', '-k', '--all') $ExitGeneral 'dapr uninstall failed')
}

exit 0
