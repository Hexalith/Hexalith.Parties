[CmdletBinding()]
param(
    [ValidateSet('Local', 'Live')][string]$Mode = 'Local',
    [string]$EventStoreRoot = (Join-Path $PSScriptRoot '../../eventstore'),
    [string]$PlatformRoot = (Join-Path $PSScriptRoot '../../platform'),
    [string]$EvidenceDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) ('ext-parties-1-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss')))
)
$ErrorActionPreference = 'Stop'
$PartiesRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$EventStoreRoot = [System.IO.Path]::GetFullPath($EventStoreRoot)
$PlatformRoot = [System.IO.Path]::GetFullPath($PlatformRoot)
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null

if ($Mode -eq 'Live') {
    $required = @('EXT_PARTIES_GATEWAY_URL', 'EXT_PARTIES_TENANT_A', 'EXT_PARTIES_TENANT_B',
        'EXT_PARTIES_PROVISIONER_CREDENTIAL', 'EXT_PARTIES_IDENTITY_WRITER_CREDENTIAL', 'EXT_PARTIES_READER_CREDENTIAL',
        'EXT_PARTIES_POLICY_ID', 'EXT_PARTIES_RETENTION', 'EXT_PARTIES_EXPIRY_TRIGGER', 'EXT_PARTIES_CUSTODY_TARGET',
        'EXT_PARTIES_RESTORE_TARGET', 'EXT_PARTIES_FAILURE_INJECTION_TARGET')
    $missing = @($required | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
    $gate = if ($missing.Count) { 'Missing live inputs: ' + ($missing -join ', ') } else {
        'Live qualification remains blocked: no installed production custody or purpose-scoped retained-history source after profile erasure. Authenticated live P-01–P-10 persisted-state/restart/restore/failure-injection probing lanes are not implemented; local fixtures cannot qualify them.'
    }
    Set-Content -Path (Join-Path $EvidenceDirectory 'live-gate.txt') -Value $gate
    Write-Error $gate -ErrorAction Continue
    exit 1
}

$flags = @('-c', 'Debug', '-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false',
    "-p:HexalithEventStoreRoot=$EventStoreRoot", "-p:HexalithCommonsRoot=$PartiesRoot/references/Hexalith.Commons", '-p:NuGetAudit=false', '-m:1')
$lanes = @(
    @{ Root=$EventStoreRoot; Project='Hexalith.EventStore.Contracts.Tests'; Classes=@('*StreamReadPageValidatorTests'); Matrix='Shared source contract validation' },
    @{ Root=$EventStoreRoot; Project='Hexalith.EventStore.Client.Tests'; Classes=@('*AuthoritativeEventStreamReaderTests', '*EventStoreGatewayClientTests', '*EventStoreGatewayClientStreamTests'); Matrix='Shared source/isolation' },
    @{ Root=$EventStoreRoot; Project='Hexalith.EventStore.Server.Tests'; Classes=@('*IdentityAdmissionTests', '*ActorRegistryTests', '*IdentityGatewayDenialTests', '*CommandsControllerTrustedExtensionTests', '*QueriesControllerTests', '*StreamsControllerTests'); Matrix='Isolation/actor authority' },
    @{ Root=$PlatformRoot; Project='Hexalith.Platform.Identity.Tests'; Classes=@('*PlatformIdentityAdmissionTests'); Matrix='Global registry/bootstrap' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.Contracts.Tests'; Classes=@('*PartyStateTests', '*PartyIdentityContractTests', '*ContractsPublicApiSnapshotTests'); Matrix='History/compatibility' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.Server.Tests'; Classes=@('*AgentPartyProvisioningTests', '*HumanActorBindingTests', '*PartyAggregateCreateTests', '*PartyAggregateCompositeTests'); Matrix='Provision/history' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.Tests'; Classes=@('*PartyIdentityAdmissionTests', '*PartyIdentityQueryHandlerTests', '*PartyDomainProcessorValidationTests', '*PartySdkQueryHandlerTests'); Matrix='Current identity/history/isolation' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.Security.Tests'; Classes=@('*IdentityHistoryProtectionTests', '*PartyPayloadProtectionServiceTests'); Matrix='Custody separation/denial' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.Client.Tests'; Classes=@('*HttpPartiesIdentityClientTests', '*HttpPartiesCommandClientTests', '*HttpPartiesQueryClientTests', '*DependencyInjectionTests'); Matrix='Isolation/compatibility' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.UI.Tests'; Classes=@('*IdentityBindingBoundaryTests', '*IdentityBindingProvisioningServiceTests', '*PartyIdClaimResolverTests', '*SelfScopedPartiesClientTests'); Matrix='Consumer compatibility' }
)
$manifest = @()
foreach ($lane in $lanes) {
    $project = Join-Path $lane.Root "tests/$($lane.Project)/$($lane.Project).csproj"
    if (-not (Test-Path $project)) { throw "Missing required owner project: $project" }
    $buildLog = Join-Path $EvidenceDirectory "$($lane.Project)-build.log"
    Write-Host "Building $($lane.Project)"
    & dotnet build $project @flags *> $buildLog
    if ($LASTEXITCODE -ne 0) { Get-Content $buildLog -Tail 25; throw "Build failed: $($lane.Project)" }
    $assembly = Join-Path $lane.Root "tests/$($lane.Project)/bin/Debug/net10.0/$($lane.Project).dll"
    $filter = @()
    foreach ($class in $lane.Classes) { $filter += @('-class', $class) }
    $testLog = Join-Path $EvidenceDirectory "$($lane.Project)-tests.log"
    $xmlLog = Join-Path $EvidenceDirectory "$($lane.Project)-tests.xml"
    & dotnet $assembly @filter -result-xml $xmlLog *> $testLog
    if ($LASTEXITCODE -ne 0) { Get-Content $testLog; throw "Tests failed: $($lane.Project)" }
    $summary = (Get-Content $testLog | Where-Object { $_ -match 'Total: (\d+), Errors: (\d+), Failed: (\d+), Skipped: (\d+), Not Run: (\d+)' } | Select-Object -Last 1)
    if (-not $summary -or $summary -notmatch 'Total: (\d+), Errors: (\d+), Failed: (\d+), Skipped: (\d+), Not Run: (\d+)') { throw "No execution evidence: $($lane.Project)" }
    if ([int]$Matches[1] -le 0 -or [int]$Matches[2] -ne 0 -or [int]$Matches[3] -ne 0 -or [int]$Matches[4] -ne 0 -or [int]$Matches[5] -ne 0) { throw "Required lane incomplete: $summary" }
    [xml]$xmlEvidence = Get-Content -Raw $xmlLog
    $executed = @($xmlEvidence.SelectNodes('//test'))
    if ($executed.Count -eq 0 -or @($executed | Where-Object { $_.result -ne 'Pass' }).Count -ne 0) { throw "Incomplete XML execution evidence: $($lane.Project)" }
    foreach ($pattern in $lane.Classes) {
        $matched = @($executed | Where-Object { $_.type -like $pattern -and $_.result -eq 'Pass' })
        if ($matched.Count -eq 0) { throw "Required class had no passing executed test: $pattern in $($lane.Project)" }
    }
    $manifest += @{ Project=$lane.Project; Matrix=$lane.Matrix; Classes=$lane.Classes; BuildLog=$buildLog; TestLog=$testLog; XmlLog=$xmlLog; Summary=$summary }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $EvidenceDirectory 'local-evidence.json')
    Write-Host $summary
}
Write-Host "Local verification passed. Evidence: $EvidenceDirectory"
Write-Host 'Live qualification is not established; run -Mode Live for the explicit installed-target gate.'
