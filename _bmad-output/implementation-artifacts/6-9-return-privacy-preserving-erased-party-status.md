# Story 6.9 - Return Privacy-Preserving Erased Party Status

## Status

Done - 2026-05-22T00:00:00Z

## Scope

Returned an explicit, privacy-preserving erased status from read and write paths so consumers can distinguish erased parties from missing, inactive, restricted, and transient key-store failures without exposing decrypted personal data or raw cryptographic errors.

- Extended `PartyErasureInProgress` with bounded `PartyId`, `TenantId`, and stable `Status` ("Erased" or "ErasureInProgress") metadata while keeping the existing `Message` shape.
- `PartyAggregate` composite update rejection and lifecycle protected-state replay now return the stable `Erased` status with the bounded message "Party is erased and no longer inspectable." for completed erasures, and `ErasureInProgress` for transitional erasure states.
- Erasure command rejections during the erasure lifecycle (`MarkKeyDestroyed`, `MarkErasureVerified`, `CompleteErasure`) now include the party id, tenant id, and explicit status without leaking key, decrypt, or store internals.
- `HttpAdminPortalGdprClient.GetErasureStatusAsync` continues to project a privacy-safe `PartyErasureStatusRecord` from the party detail with `Status="Erased"` and no decrypted personal data.
- Added `docs/gdpr-erased-party-status.md` to document stable signals across detail, export, processing records, commands, and list/search/picker surfaces.
- Updated the public contract snapshot for the intentional additive schema on `PartyErasureInProgress`.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Server.Tests\Hexalith.Parties.Server.Tests.csproj --filter "FullyQualifiedName~PartyAggregateErasureTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 19/19 tests.

```powershell
dotnet test tests\Hexalith.Parties.Server.Tests\Hexalith.Parties.Server.Tests.csproj --filter "FullyQualifiedName~Aggregates" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 217/217 tests.

```powershell
dotnet test tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortalGdprOperationContractTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 12/12 tests.

```powershell
dotnet test tests\Hexalith.Parties.Contracts.Tests\Hexalith.Parties.Contracts.Tests.csproj --filter "FullyQualifiedName~ContractsPublicApiSnapshotTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 1/1 test.

```powershell
dotnet test tests\Hexalith.Parties.Security.Tests\Hexalith.Parties.Security.Tests.csproj -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 136/136 tests.

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 468/468 tests.

Warning override note: the focused commands used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
