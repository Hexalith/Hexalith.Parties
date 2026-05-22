# Story 6.4 - Publish PartyErased Subscriber Notification

## Status

Done - 2026-05-21T20:10:00Z

## Scope

Added and validated the privacy-safe `PartyErased` subscriber notification contract:

- `PartyErased` now carries cleanup-only status metadata: `partyId`, `tenantId`, `erasedAt`, `erasureStatus`, and `verificationStatus`.
- Aggregate completion now emits `PartyErased` with `ErasureStatus=Erased` and `VerificationStatus=Complete`.
- Publication contract tests verify the event is persisted with tenant, aggregate, correlation, causation, user, and event type metadata without personal data in the payload.
- Documentation now states `PartyErased` is emitted only at the documented completion point and explains that pending or partial verification remains visible through companion erasure status surfaces.
- Sample handler tests now prove duplicate `PartyErased` delivery is idempotent and does not reintroduce personal data.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainEventPublicationContractTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 12/12 tests.

```powershell
dotnet test tests\Hexalith.Parties.Sample.Tests\Hexalith.Parties.Sample.Tests.csproj --filter "FullyQualifiedName~PartyErasedHandlerPatternTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 10/10 tests.

```powershell
dotnet test tests\Hexalith.Parties.Server.Tests\Hexalith.Parties.Server.Tests.csproj --filter "FullyQualifiedName~PartyAggregateErasureTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 18/18 tests.

Warning override note: the focused commands used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
