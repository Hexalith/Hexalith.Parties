# Story 5.2 - Include Tenant Context and Envelope Metadata

## Status

Done - 2026-05-21T17:40:00Z

## Scope

Added event envelope metadata validation for party events:

- Persisted event envelopes use authenticated envelope tenant context, not tenant-like payload fields.
- Envelopes include aggregate id, aggregate type, domain, correlation id, causation id, user id, event type, timestamp, metadata version, serialization format, and payload bytes.
- Envelope sequence numbers remain ordered for multi-event party mutations.
- Envelope `ToString()` redacts payload values and exposes safe metadata only.
- Missing tenant context fails before publication by EventStore identity/envelope construction.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainEventPublicationContractTests|FullyQualifiedName~PartyDomainServiceInvokerValidationTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 15/15 tests.

Warning override note: the focused command used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
