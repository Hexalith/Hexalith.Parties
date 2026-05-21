# Story 6.5 - Manage Per-Channel Per-Purpose Consent

## Status

Done - 2026-05-21T20:25:00Z

## Scope

Added and validated precise consent metadata:

- `RecordConsent` / `ConsentRecorded` now preserve consent source metadata alongside party id, channel id, purpose, lawful basis, timestamp, and actor metadata.
- `RevokeConsent` / `ConsentRevoked` now preserve revocation reason and source metadata alongside timestamp and actor metadata.
- `ConsentRecord` and projection handling now retain grant source, revocation reason, and revocation source in aggregate and read-model state.
- Aggregate tests now pin source/reason metadata, history reconstruction, erased-party rejection coverage already present, and explicit rejection of party-wide consent shortcuts such as `ChannelId="*"`.
- Contract tests pin the additive consent event fields for forward-compatible subscribers.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Server.Tests\Hexalith.Parties.Server.Tests.csproj --filter "FullyQualifiedName~PartyAggregateConsentTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 16/16 tests.

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainEventPublicationContractTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 12/12 tests.

```powershell
dotnet test tests\Hexalith.Parties.Projections.Tests\Hexalith.Parties.Projections.Tests.csproj --filter "FullyQualifiedName~Consent" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 4/4 tests.

Warning override note: the focused commands used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
