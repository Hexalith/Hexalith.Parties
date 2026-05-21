# Story 6.8 - Record Processing Activities

## Status

Done - 2026-05-21T21:10:00Z

## Scope

Completed privacy-safe processing activity records:

- Extended `ProcessingActivityRecord` with bounded party id, tenant id, actor id, correlation id, operation category, and outcome metadata.
- `ProjectionRebuildService.GetProcessingRecordsAsync` now derives ordered activity records from persisted aggregate event envelopes and sanitizes summaries so free-text reasons, contact values, identifiers, names, and command payload fragments do not leak.
- Added `GetProcessingRecords` handling to `PartyDetailProjectionQueryActor` with tenant/party route validation and no party detail read.
- `HttpAdminPortalGdprClient.GetProcessingRecordsAsync` now calls the authoritative EventStore query instead of failing closed.
- Added processing activity documentation and updated the public contract snapshot for the intentional additive schema.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~ProjectionRebuildServiceTests|FullyQualifiedName~PartyDetailProjectionQueryActorTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 29/29 tests.

```powershell
dotnet test tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortalGdprOperationContractTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 11/11 tests.

```powershell
dotnet test tests\Hexalith.Parties.Contracts.Tests\Hexalith.Parties.Contracts.Tests.csproj --filter "FullyQualifiedName~ContractsPublicApiSnapshotTests|FullyQualifiedName~AdminPortalGdprPrivacyGuardrailTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 6/6 tests.

```powershell
dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --filter "FullyQualifiedName~ProcessingRecords|FullyQualifiedName~Gdpr" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 9/9 tests.

Warning override note: the focused commands used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
