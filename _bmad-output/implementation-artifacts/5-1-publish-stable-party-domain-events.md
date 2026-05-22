# Story 5.1 - Publish Stable Party Domain Events

## Status

Done - 2026-05-21T17:25:00Z

## Scope

Added stable party-domain event contract validation for consumer integration:

- Composite create produces stable `PartyCreated`, `PartyDisplayNameDerived`, `ContactChannelAdded`, and `IdentifierAdded` events plus an updated-party payload.
- Composite update produces stable detail, contact-channel, and identifier events plus an updated-party payload.
- Lifecycle commands produce stable `PartyDeactivated` and `PartyReactivated` events with updated-party payloads.
- Rejection paths remain distinguishable from successful state changes through `IRejectionEvent`.
- Published event contracts are asserted to live in the Contracts assembly and serialize/deserialize with readable personal-data payloads for MVP/v1.1 consumers.
- Existing invoker validation tests continue to prove invalid payloads produce safe rejection events before unprotect/replay or aggregate processing.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainEventPublicationContractTests|FullyQualifiedName~PartyDomainServiceInvokerValidationTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 13/13 tests.

Warning override note: the focused command used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
