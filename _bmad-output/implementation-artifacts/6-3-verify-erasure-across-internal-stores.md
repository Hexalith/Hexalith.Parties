# Story 6.3 - Verify Erasure Across Internal Stores

## Status

Done - 2026-05-21T19:55:00Z

## Scope

Added and validated erasure verification behavior across internal stores:

- Store cleanup results now include explicit `Pending` and `NotApplicable` statuses in addition to cleaned, failed, and skipped outcomes.
- Overall verification status now returns `Pending` when any store is temporarily unavailable or rebuilding and no store has failed.
- Verification reports sanitize delegate error messages and exception fallback messages so personal data values and raw store exceptions do not leak into DPO/operator-facing reports or logs.
- DI erasure verification delegates now cover aggregate readable state, snapshots, detail projection, index projection, projection cache, and memories search, with memories search reported as `NotApplicable` when disabled.
- Tests now cover complete erasure, partial/failed states, pending/retry states, not-applicable stores, all internal store categories, checkpoint retry, corrupted state fallback, and bounded redacted report output.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Security.Tests\Hexalith.Parties.Security.Tests.csproj --filter "FullyQualifiedName~ErasureVerificationServiceTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 15/15 tests.

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainServiceInvokerValidationTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 9/9 tests.

```powershell
dotnet test tests\Hexalith.Parties.Security.Tests\Hexalith.Parties.Security.Tests.csproj -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 136/136 tests.

Warning override note: the focused commands used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
