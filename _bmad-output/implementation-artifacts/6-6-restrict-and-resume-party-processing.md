# Story 6.6 - Restrict and Resume Party Processing

## Status

Done - 2026-05-21T20:40:00Z

## Scope

Completed and hardened processing restriction behavior:

- `RestrictProcessing` and `LiftRestriction` now accept bounded operational metadata for actor user id and correlation id.
- `ProcessingRestricted` and `RestrictionLifted` now emit stable actor and correlation metadata with backward-compatible defaults for older serialized payloads.
- Restriction reasons are normalized and validated before protected-state replay; invalid party id, tenant id, or overlong/missing reasons return bounded validation rejections without exposing request details.
- Aggregate tests now pin restriction event metadata, default metadata behavior, blocked-operation guards, allowed GDPR operations, idempotent duplicate restriction, lift rejection, missing party, and erasure-in-progress behavior.
- Domain contract tests now guard the additive restriction event fields for subscribers.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Server.Tests\Hexalith.Parties.Server.Tests.csproj --filter "FullyQualifiedName~PartyAggregateRestrictionTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 28/28 tests.

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainServiceInvokerValidationTests|FullyQualifiedName~PartyDomainEventPublicationContractTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 22/22 tests.

```powershell
dotnet test tests\Hexalith.Parties.Projections.Tests\Hexalith.Parties.Projections.Tests.csproj --filter "FullyQualifiedName~ProcessingRestricted|FullyQualifiedName~RestrictionLifted" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 3/3 tests.

Warning override note: the focused commands used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
