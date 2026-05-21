# Story 6.2 - Trigger Right-to-Erasure by Crypto-Shredding

## Status

Done - 2026-05-21T19:40:00Z

## Scope

Added and validated right-to-erasure safeguards around the existing crypto-shredding flow:

- `EraseParty` now has a FluentValidation validator that rejects blank or non-GUID party ids and blank tenant ids before protected state is replayed.
- Domain invoker validation now proves invalid erasure payloads return `PartyCommandValidationRejected` without unprotecting protected snapshots or events.
- Key-management integration now proves key deletion destroys the party key, verifies no key versions remain, and makes previously encrypted personal data permanently unreadable through the payload protection service.
- Existing aggregate tests cover accepted erasure requests, already-erased and in-progress idempotency, missing party rejections, erasure state transitions, key-destroyed status, verification, terminal `PartyErased`, and modification rejection during erasure.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainServiceInvokerValidationTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 9/9 tests.

```powershell
dotnet test tests\Hexalith.Parties.Security.Tests\Hexalith.Parties.Security.Tests.csproj --filter "FullyQualifiedName~KeyManagementIntegrationTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 8/8 tests.

```powershell
dotnet test tests\Hexalith.Parties.Server.Tests\Hexalith.Parties.Server.Tests.csproj --filter "FullyQualifiedName~PartyAggregateErasureTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 18/18 tests.

Warning override note: the focused commands used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
