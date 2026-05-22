# Story 6.7 - Export Party Data Portability Package

## Status

Done - 2026-05-21T20:55:00Z

## Scope

Added the authoritative EventStore portability export query path:

- Added `PartyDataPortabilityPackage` as the machine-readable export contract.
- `PartyDetailProjectionQueryActor` now handles `ExportPartyData` and returns a tenant-scoped package with party detail, contact channels, identifiers, consent records, restriction state, processing activity summaries, freshness metadata, actor metadata, timestamp, and correlation id.
- Erased parties and unavailable personal data return privacy-preserving package statuses without a partial `Party` payload.
- Restricted parties are exportable for authorized GDPR callers with status `RestrictedExported`, matching the documented restriction policy.
- `HttpAdminPortalGdprClient.ExportPartyDataAsync` now calls the authoritative EventStore query and returns an `application/json` download envelope with a filename derived only from party id and UTC export timestamp.
- Added GDPR export documentation and updated the contracts public API snapshot for the intentional additive contract surface.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDetailProjectionQueryActorTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 16/16 tests.

```powershell
dotnet test tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortalGdprOperationContractTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 10/10 tests.

```powershell
dotnet test tests\Hexalith.Parties.Contracts.Tests\Hexalith.Parties.Contracts.Tests.csproj --filter "FullyQualifiedName~ContractsPublicApiSnapshotTests|FullyQualifiedName~AdminPortalGdprPrivacyGuardrailTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 6/6 tests.

Warning override note: the focused commands used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
