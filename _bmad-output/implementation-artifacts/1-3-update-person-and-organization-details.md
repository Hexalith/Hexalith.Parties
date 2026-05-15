# Story 1.3: Update Person and Organization Details

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized client,
I want to update person-specific and organization-specific party details,
so that party records remain accurate as real-world identity details change.

## Acceptance Criteria

1. **Person detail updates emit domain events and mutate state**
   - Given an existing active person party,
   - When the client updates person details such as first name, last name, date of birth, name prefix, or name suffix,
   - Then the aggregate emits a `PersonDetailsUpdated` event,
   - And `PartyState` reflects the updated person details after the event is applied.

2. **Organization detail updates emit domain events and mutate state**
   - Given an existing active organization party,
   - When the client updates organization details such as legal name, trading name, legal form, or registration number,
   - Then the aggregate emits an `OrganizationDetailsUpdated` event,
   - And `PartyState` reflects the updated organization details after the event is applied.

3. **Display and sort names are re-derived after detail changes**
   - Given an update changes fields used for display name or sort name derivation,
   - When the update succeeds,
   - Then the aggregate emits `PartyDisplayNameDerived`,
   - And the updated party state exposes the new derived names using MVP rules: person display name `"{FirstName} {LastName}"`, person sort name `"{LastName}, {FirstName}"`, organization display and sort names both use `LegalName`.

4. **Cross-type detail updates are rejected without mutation**
   - Given a person-specific update is submitted for an organization party, or an organization-specific update is submitted for a person party,
   - When the command is handled,
   - Then the aggregate rejects the command with a typed rejection event,
   - And existing party details, display name, and sort name remain unchanged.

5. **Missing party updates are rejected**
   - Given an update command targets a party that does not exist,
   - When the command is handled,
   - Then the aggregate rejects the command with a typed not-found or current documented typed rejection event,
   - And no successful update or display-name event is emitted.

6. **Returned update state is reconciled with emitted events**
   - Given a party detail update succeeds through the current update response path,
   - When the command result is returned,
   - Then the returned party detail/state matches the aggregate state after applying the emitted events,
   - And any direct aggregate handler that intentionally returns only `DomainResult` is documented as a domain primitive rather than a public response contract.

## Tasks / Subtasks

- [ ] Task 1: Audit existing update contracts and aggregate handlers (AC: 1, 2, 4, 5)
  - [ ] Confirm `UpdatePersonDetails`, `UpdateOrganizationDetails`, `PersonDetailsUpdated`, `OrganizationDetailsUpdated`, `PartyTypeMismatch`, `PartyNotFound`, `PartyDisplayNameDerived`, `PersonDetails`, `OrganizationDetails`, and `PartyState` match this story's scope.
  - [ ] Confirm `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` still exposes pure static `Handle(UpdatePersonDetails, PartyState?)` and `Handle(UpdateOrganizationDetails, PartyState?)` methods through the EventStore aggregate convention.
  - [ ] Verify the current missing-party behavior for direct detail handlers. If it still uses `PartyTypeMismatch { Message = "Party does not exist." }`, either preserve it as the documented current typed rejection or migrate narrowly to `PartyNotFound` only if existing tests and downstream error mapping support that change.
  - [ ] Verify null command payloads throw `ArgumentNullException` for programmer misuse, while null detail payloads return deterministic typed rejections and do not reach `DeriveDisplayName(...)`.

- [ ] Task 2: Validate person and organization update success behavior (AC: 1, 2, 3)
  - [ ] For person updates, verify the successful event sequence is `PersonDetailsUpdated` followed by `PartyDisplayNameDerived`.
  - [ ] For organization updates, verify the successful event sequence is `OrganizationDetailsUpdated` followed by `PartyDisplayNameDerived`.
  - [ ] Apply emitted events to existing `PartyState` instances and verify detail payloads, display names, and sort names reflect the new data.
  - [ ] Confirm update events do not carry `PartyId` or `TenantId`; aggregate identity and tenant context remain EventStore/request metadata concerns.

- [ ] Task 3: Validate rejection, erasure, and restriction guards (AC: 4, 5)
  - [ ] Verify person detail updates against organization state reject with `PartyTypeMismatch` and emit no success events.
  - [ ] Verify organization detail updates against person state reject with `PartyTypeMismatch` and emit no success events.
  - [ ] Verify missing-party updates reject consistently with the chosen current typed rejection and emit no success events.
  - [ ] Verify updates against erasure-pending or erased state reject with `PartyErasureInProgress`.
  - [ ] Verify updates against processing-restricted state reject with `PartyProcessingRestricted`.

- [ ] Task 4: Reconcile FR69 returned-state behavior without broad response rewrites (AC: 6)
  - [ ] Inspect `CompositeCommandResult.UpdatedPartyDetail` and `PartyAggregate.Handle(UpdatePartyComposite, PartyState?)`, because the current composite update path already builds an updated `PartyDetail` from state plus emitted events.
  - [ ] Confirm detail-only `UpdatePartyComposite` tests cover person and organization updates and assert `UpdatedPartyDetail` after events are projected.
  - [ ] Do not change simple `DomainResult` return types for `Handle(UpdatePersonDetails, ...)` or `Handle(UpdateOrganizationDetails, ...)` unless architecture explicitly requires it; these direct handlers are aggregate primitives, while public API/MCP response shape is covered by composite/update response stories.
  - [ ] If a returned-state gap remains, record the smallest follow-up against Story 1.9 rather than expanding this story into API/MCP transport work.

- [ ] Task 5: Preserve EventStore, privacy, and dependency boundaries (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Keep update domain behavior in pure aggregate `Handle` methods; do not add Dapr, HTTP, database, MediatR handler, REST, Swagger, MCP, AdminPortal, Picker, or sample behavior to this story.
  - [ ] Keep `Hexalith.Parties.Contracts` free of hosting, Dapr, MediatR, FluentValidation, UI, and infrastructure dependencies beyond accepted EventStore contract references.
  - [ ] Preserve `PartyState.Apply(...)` rejection-event ordering before success applies; EventStore suffix-based apply resolution depends on it.
  - [ ] Treat person details, derived names, and organization details for sole traders as privacy-sensitive. Do not add logs, telemetry, exception messages, or operational output that leaks personal data.

- [ ] Task 6: Run focused validation (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateUpdateTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateCompositeTests` if returned-state behavior is inspected or changed.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests` if any `PartyState.Apply` declarations move.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if implementation changes touch public contracts, project references, shared build files, or EventStore-facing surfaces.

## Dev Notes

### Current Implementation Context

- This story is the current backlog-tracked Epic 1 detail-update story, but the repository already contains a historical completed Story 1.4: `_bmad-output/implementation-artifacts/1-4-party-aggregate-update-details-and-lifecycle-management.md`.
- Treat the historical story as implementation intelligence, not as the canonical status artifact for this new story key. It mixed detail updates with lifecycle behavior; this new story is only person/organization detail updates and FR69 reconciliation.
- The expected implementation largely exists in `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`, `src/Hexalith.Parties.Contracts`, `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateUpdateTests.cs`, `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs`, and `tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs`.
- Start by auditing and reconciling current behavior. Do not recreate the historical lifecycle slice and do not move contact-channel, identifier, deactivate/reactivate, GDPR, projection, MCP, admin, or picker behavior into this story.

### Architecture Patterns and Constraints

- Parties validates the Hexalith.EventStore domain-service pattern. If an EventStore abstraction does not fit, prefer fixing or adapting EventStore instead of adding Parties-side workaround structure.
- Domain behavior belongs in EventStore aggregate conventions: pure static `Handle(Command, PartyState?)` methods emit `DomainResult` events, and `PartyState.Apply(Event)` mutates state during rehydration.
- `PartyAggregate` lives in `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` and derives from `EventStoreAggregate<PartyState>`.
- Update commands carry `PartyId` as aggregate identity. Tenant context is supplied by the gateway/request pipeline, not by this aggregate story.
- Events must remain additive public contracts. Do not add `PartyId` or `TenantId` to `PersonDetailsUpdated`, `OrganizationDetailsUpdated`, or `PartyDisplayNameDerived`.
- The main `src/Hexalith.Parties` project is an actor host. Do not reintroduce public REST, Swagger/OpenAPI, controllers, or in-process MCP tools there for this story.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Contracts/
  Commands/UpdatePersonDetails.cs
  Commands/UpdateOrganizationDetails.cs
  Commands/UpdatePartyComposite.cs
  Events/PersonDetailsUpdated.cs
  Events/OrganizationDetailsUpdated.cs
  Events/PartyDisplayNameDerived.cs
  Events/PartyTypeMismatch.cs
  Events/PartyNotFound.cs
  Results/CompositeCommandResult.cs
  State/PartyState.cs
  ValueObjects/PersonDetails.cs
  ValueObjects/OrganizationDetails.cs

src/Hexalith.Parties.Server/
  Aggregates/PartyAggregate.cs

tests/Hexalith.Parties.Server.Tests/
  Aggregates/PartyAggregateUpdateTests.cs
  Aggregates/PartyAggregateCompositeTests.cs
  Aggregates/PartyAggregateErasureTests.cs
  Aggregates/PartyAggregateRestrictionTests.cs

tests/Hexalith.Parties.Contracts.Tests/
  State/PartyStateTests.cs
```

### Current Behavior Snapshot

- Direct `Handle(UpdatePersonDetails, PartyState?)` currently emits `PersonDetailsUpdated` and `PartyDisplayNameDerived` on success.
- Direct `Handle(UpdateOrganizationDetails, PartyState?)` currently emits `OrganizationDetailsUpdated` and `PartyDisplayNameDerived` on success.
- Direct detail handlers currently run erasure and processing-restriction guards before type-specific mutation.
- `PartyState.Apply(PersonDetailsUpdated)` replaces `Person`; `Apply(OrganizationDetailsUpdated)` replaces `Organization`; `Apply(PartyDisplayNameDerived)` replaces `DisplayName` and `SortName`.
- `Handle(UpdatePartyComposite, PartyState?)` currently returns `CompositeCommandResult` with `UpdatedPartyDetail` built from current state plus emitted events. This is the current strongest evidence for FR69 on update responses.
- Existing focused tests already cover direct person/organization success, null state, cross-type rejection, null payload rejection, display-name derivation, event application, and composite detail-only returned detail. Add or adjust tests only where the audit finds gaps.

### Previous Story Intelligence

- Story 1.1 framed early Epic 1 stories as reconciliation over an already-evolved repository and warned not to delete later Epic 10-12 boundaries.
- Story 1.2 clarified that aggregate identity belongs to EventStore metadata and that `PartyCreated` should not grow `PartyId` or `TenantId`.
- Historical Story 1.4 added direct update handlers and later review fixes for null payload guards and test coverage. Keep the guard behavior; do not reintroduce exception paths for business validation.
- Recent commits show this automation recreated Story 1.1 and Story 1.2 as ready-for-dev reconciliation stories (`374daec`, `98e310b`) after planning realignment.

### Testing Requirements

- Use xUnit v3 and Shouldly patterns already present in the repository.
- Keep tests pure aggregate/state tests unless implementation touches transport or topology behavior.
- Prefer the existing `PartyTestData` helpers for valid party states and update commands.
- If changing public contracts or apply ordering, run the relevant contract and fitness tests before broader suites.
- Do not require Docker, Dapr sidecars, Aspire topology, databases, or full integration startup for this story.

### Anti-Patterns To Avoid

- Do not create a second aggregate, service wrapper, MediatR handler, controller, MCP tool, admin page, or picker surface for detail updates.
- Do not add tenant ids to commands or events for this story.
- Do not add party ids to update events; aggregate identity is carried by EventStore metadata.
- Do not emit success events after a typed rejection.
- Do not fold contact channels, identifiers, lifecycle activation, erasure operations, projection rebuilds, API transport, or MCP patch parsing into this story.
- Do not weaken or reorder `PartyState.Apply` rejection overloads.
- Do not log detail payload values, derived names, or rejected personal data.
- Do not use recursive nested submodule initialization.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.3] - Story statement and BDD acceptance criteria for FR2, FR3, FR6, and FR69.
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-05-15.md#FR-coverage] - FR2, FR3, FR6, and FR69 mapping to this story.
- [Source: _bmad-output/planning-artifacts/architecture.md#Aggregate] - EventStore aggregate, command, event, and state conventions.
- [Source: _bmad-output/planning-artifacts/architecture.md#D9-MCP-Update-Strategy] - Composite update strategy and patch/update response context.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - EventStore aggregate, contract dependency, actor-host, privacy, testing, and submodule guardrails.
- [Source: _bmad-output/implementation-artifacts/1-2-create-party-aggregate-with-stable-identity.md] - Previous ready story and current aggregate identity guardrails.
- [Source: _bmad-output/implementation-artifacts/1-4-party-aggregate-update-details-and-lifecycle-management.md] - Historical implementation and review intelligence for update handlers.
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs] - Current aggregate implementation surface.
- [Source: src/Hexalith.Parties.Contracts/State/PartyState.cs] - Current rehydration behavior.
- [Source: src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs] - Current returned update detail carrier.
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateUpdateTests.cs] - Current direct detail update coverage.
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs] - Current composite update and `UpdatedPartyDetail` coverage.
- [Source: tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs] - Current state apply coverage.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-05-15: Story created by BMAD pre-dev hardening automation with current detail-update reconciliation context.
