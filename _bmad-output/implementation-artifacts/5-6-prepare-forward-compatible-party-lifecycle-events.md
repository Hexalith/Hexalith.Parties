# Story 5.6 - Prepare Forward-Compatible Party Lifecycle Events

## Status

Done - 2026-05-21T18:55:00Z

## Scope

Added forward-compatibility validation for party lifecycle event contracts:

- Contract test now pins `PartyMerged`, `PartyErased`, erasure request, consent, and processing restriction event names and required field names.
- Contract test verifies forward-compatible events remain public EventStore `IEventPayload` contracts in the Contracts assembly.
- Tolerant deserialization is validated with an additive future field on `PartyMerged`.
- Event handler documentation now names consent, restriction, export, and processing-record event space as v1.1+ future workflow guidance.
- Documentation remains explicit that current consumers should use tolerant default handling unless they opt into future GDPR workflows.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainEventPublicationContractTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 11/11 tests.

```powershell
dotnet test tests\Hexalith.Parties.Sample.Tests\Hexalith.Parties.Sample.Tests.csproj --filter "FullyQualifiedName~TolerantDeserializationTests|FullyQualifiedName~PartyErasedHandlerPatternTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 12/12 tests.

Warning override note: the focused commands used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
