# Story 1.3: Update Person and Organization Details

Status: done

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
   - And `PartyState` reflects the updated person details after the event is applied,
   - And organization details on the same party state remain absent or unchanged.

2. **Organization detail updates emit domain events and mutate state**
   - Given an existing active organization party,
   - When the client updates organization details such as legal name, trading name, legal form, or registration number,
   - Then the aggregate emits an `OrganizationDetailsUpdated` event,
   - And `PartyState` reflects the updated organization details after the event is applied,
   - And person details on the same party state remain absent or unchanged.

3. **Display and sort names are re-derived after detail changes**
   - Given an update changes fields used for display name or sort name derivation,
   - When the update succeeds,
   - Then the aggregate emits `PartyDisplayNameDerived` after the detail-updated event,
   - And the updated party state exposes the new derived names using MVP rules: person display name `"{FirstName} {LastName}"`, person sort name `"{LastName}, {FirstName}"`, organization display and sort names both use `LegalName`.

4. **Cross-type detail updates are rejected without mutation**
   - Given a person-specific update is submitted for an organization party, or an organization-specific update is submitted for a person party,
   - When the command is handled,
   - Then the aggregate rejects the command with a typed rejection event,
   - And existing party details, display name, and sort name remain unchanged,
   - And no successful detail-updated or display-name event is emitted.

5. **Missing party updates are rejected**
   - Given an update command targets a party that does not exist,
   - When the command is handled,
   - Then the aggregate rejects the command with a typed not-found or current documented typed rejection event,
   - And no successful update or display-name event is emitted.

6. **Returned update state is reconciled with emitted events**
   - Given a party detail update succeeds through the current update response path,
   - When the command result is returned,
   - Then `CompositeCommandResult.UpdatedPartyDetail` matches the aggregate state after applying the emitted events,
   - And any direct aggregate handler that intentionally returns only `DomainResult` is documented as a domain primitive rather than a public response contract.

### Acceptance Traceability

| AC | Command path | Expected event/result evidence | State evidence | Focused validation target |
| --- | --- | --- | --- | --- |
| AC1 | `UpdatePersonDetails` and person-only `UpdatePartyComposite` detail operation | `PersonDetailsUpdated` followed by `PartyDisplayNameDerived` on success | `PartyState.Person`, `DisplayName`, and `SortName` reflect the update; organization details are not mutated | `PartyAggregateUpdateTests`, `PartyAggregateCompositeTests`, `PartyStateTests` |
| AC2 | `UpdateOrganizationDetails` and organization-only `UpdatePartyComposite` detail operation | `OrganizationDetailsUpdated` followed by `PartyDisplayNameDerived` on success | `PartyState.Organization`, `DisplayName`, and `SortName` reflect the update; person details are not mutated | `PartyAggregateUpdateTests`, `PartyAggregateCompositeTests`, `PartyStateTests` |
| AC3 | Successful direct and composite detail updates | Display-name event occurs after the detail-updated event | Derived names use current MVP rules after all emitted events are applied | `PartyAggregateUpdateTests`, `PartyStateTests` |
| AC4 | Wrong-type direct and composite detail updates | Typed rejection only; no success event and no returned success detail | Existing details, display name, and sort name remain unchanged | `PartyAggregateUpdateTests`, `PartyAggregateCompositeTests` |
| AC5 | Missing-party direct detail updates | Current typed not-found behavior only; no success event | Null or missing state remains unmutated | `PartyAggregateUpdateTests` |
| AC6 | Successful `UpdatePartyComposite` detail operation | `CompositeCommandResult.UpdatedPartyDetail` is populated from the post-event state | Returned detail matches state after projected events | `PartyAggregateCompositeTests` |

### Advanced Elicitation Clarifications

- Treat rejection evidence as event-list and state evidence, not message-string evidence alone. Focused tests should assert the exact rejection event type, event count where relevant, absence of `PersonDetailsUpdated`, `OrganizationDetailsUpdated`, and `PartyDisplayNameDerived`, and unchanged pre-command state without snapshotting unnecessary personal data.
- Keep direct handler and composite handler missing-party semantics explicit. Direct `UpdatePersonDetails` / `UpdateOrganizationDetails` may preserve the current documented `PartyTypeMismatch { Message = "Party does not exist." }` behavior, while `UpdatePartyComposite` may continue returning `PartyNotFound`; any unification to `PartyNotFound` is a narrow compatibility-sensitive change, not a hidden requirement of this story.
- Guard precedence for existing party state matters. Erasure and processing-restriction rejection paths should return before detail mutation or display-name derivation, and tests should prove they do not emit success events or build returned success detail.
- For FR69, assert `CompositeCommandResult.UpdatedPartyDetail` as a post-event projection of the aggregate state, including updated detail fields and derived display/sort names, without expanding direct aggregate primitives into public response contracts.
- Derived-name validation should stay at the MVP rule boundary already stated by the ACs. Do not introduce culture-specific sorting, legal-name normalization, date-of-birth policy, or personal-name formatting rules in this story.

## Tasks / Subtasks

- [x] Task 1: Audit existing update contracts and aggregate handlers (AC: 1, 2, 4, 5)
  - [x] Confirm `UpdatePersonDetails`, `UpdateOrganizationDetails`, `PersonDetailsUpdated`, `OrganizationDetailsUpdated`, `PartyTypeMismatch`, `PartyNotFound`, `PartyDisplayNameDerived`, `PersonDetails`, `OrganizationDetails`, and `PartyState` match this story's scope.
  - [x] Confirm `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` still exposes pure static `Handle(UpdatePersonDetails, PartyState?)` and `Handle(UpdateOrganizationDetails, PartyState?)` methods through the EventStore aggregate convention.
  - [x] Verify the current missing-party behavior for direct detail handlers. If it still uses `PartyTypeMismatch { Message = "Party does not exist." }`, either preserve it as the documented current typed rejection or migrate narrowly to `PartyNotFound` only if existing tests and downstream error mapping support that change.
  - [x] Verify null command payloads throw `ArgumentNullException` for programmer misuse, while null detail payloads return deterministic typed rejections and do not reach `DeriveDisplayName(...)`.
  - [x] Treat same-value updates and broader person/organization field normalization as current-contract behavior unless existing tests already define otherwise; record any desired audit-event/no-op or normalization change as a deferred decision.

- [x] Task 2: Validate person and organization update success behavior (AC: 1, 2, 3)
  - [x] For person updates, verify the successful event sequence is `PersonDetailsUpdated` followed by `PartyDisplayNameDerived`.
  - [x] For organization updates, verify the successful event sequence is `OrganizationDetailsUpdated` followed by `PartyDisplayNameDerived`.
  - [x] Apply emitted events to existing `PartyState` instances and verify detail payloads, display names, and sort names reflect the new data.
  - [x] Verify person updates do not mutate organization detail state and organization updates do not mutate person detail state.
  - [x] Confirm update events do not carry `PartyId` or `TenantId`; aggregate identity and tenant context remain EventStore/request metadata concerns.

- [x] Task 3: Validate rejection, erasure, and restriction guards (AC: 4, 5)
  - [x] Verify person detail updates against organization state reject with `PartyTypeMismatch` and emit no success events.
  - [x] Verify organization detail updates against person state reject with `PartyTypeMismatch` and emit no success events.
  - [x] Verify missing-party updates reject consistently with the chosen current typed rejection and emit no success events.
  - [x] Verify updates against erasure-pending or erased state reject with `PartyErasureInProgress`.
  - [x] Verify updates against processing-restricted state reject with `PartyProcessingRestricted`.
  - [x] Verify every rejection path leaves the original state unchanged and produces no returned success detail.
  - [x] Do not invent deactivated-party update policy in this story. If deactivation behavior is not already represented by existing contracts/tests, record it as deferred to Story 1.6 rather than adding lifecycle semantics here.

- [x] Task 4: Reconcile FR69 returned-state behavior without broad response rewrites (AC: 6)
  - [x] Inspect `CompositeCommandResult.UpdatedPartyDetail` and `PartyAggregate.Handle(UpdatePartyComposite, PartyState?)`, because the current composite update path already builds an updated `PartyDetail` from state plus emitted events.
  - [x] Confirm detail-only `UpdatePartyComposite` tests cover person and organization updates and assert `UpdatedPartyDetail` after events are projected, including updated details and derived display/sort names.
  - [x] Assert `UpdatedPartyDetail` is not produced for rejected detail updates.
  - [x] Do not change simple `DomainResult` return types for `Handle(UpdatePersonDetails, ...)` or `Handle(UpdateOrganizationDetails, ...)` unless architecture explicitly requires it; these direct handlers are aggregate primitives, while public API/MCP response shape is covered by composite/update response stories.
  - [x] If a returned-state gap remains, record the smallest follow-up against Story 1.9 rather than expanding this story into API/MCP transport work.

- [x] Task 5: Preserve EventStore, privacy, and dependency boundaries (AC: 1, 2, 3, 4, 5, 6)
  - [x] Keep update domain behavior in pure aggregate `Handle` methods; do not add Dapr, HTTP, database, MediatR handler, REST, Swagger, MCP, AdminPortal, Picker, or sample behavior to this story.
  - [x] Keep `Hexalith.Parties.Contracts` free of hosting, Dapr, MediatR, FluentValidation, UI, and infrastructure dependencies beyond accepted EventStore contract references.
  - [x] Preserve `PartyState.Apply(...)` rejection-event ordering before success applies; EventStore suffix-based apply resolution depends on it.
  - [x] Treat person details, derived names, and organization details for sole traders as privacy-sensitive. Do not add logs, telemetry, exception messages, snapshots, or operational output that leaks personal data; tests should assert only the specific fields needed to prove behavior.

- [x] Task 6: Run focused validation (AC: 1, 2, 3, 4, 5, 6)
  - [x] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateUpdateTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateCompositeTests` if returned-state behavior is inspected or changed.
  - [x] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests` if any `PartyState.Apply` declarations move.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if implementation changes touch public contracts, project references, shared build files, or EventStore-facing surfaces.

### Review Findings

Bmad-code-review run on 2026-05-16 against commit `83e6548`. Acceptance Auditor: full PASS on AC1–AC6. Blind Hunter + Edge Case Hunter raised 21 findings → 6 patch, 5 defer, 10 dismissed as noise.

- [x] [Review][Patch] Strengthen identity-metadata reflection to be case-insensitive and cover every public property [tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateUpdateTests.cs:154-170, 310-327] — `GetType().GetProperty("PartyId")` is case-sensitive and returns null for any unknown name, so a contract change introducing `partyId` (lowercase) or any renamed identity-like property would slip past. Iterate `event.GetType().GetProperties()` and assert none match `partyid`/`tenantid` case-insensitively.
- [x] [Review][Patch] Assert rejection message on direct missing-party update tests to pin direct-vs-composite divergence [tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateUpdateTests.cs:36-51, 194-209] — Advanced Elicitation Clarifications explicitly preserves the documented `PartyTypeMismatch { Message = "Party does not exist." }` for direct handlers while composite uses `PartyNotFound`. Composite tests pin the type AND message; direct tests only assert the type. A regression flipping the direct rejection type would still pass — add `rejection.Message.ShouldBe("Party does not exist.");`.
- [x] [Review][Patch] Add terminal `ErasureStatus.Erased` coverage for direct update handlers [tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateUpdateTests.cs] — Task 3 requires "updates against erasure-pending **or erased** state reject with `PartyErasureInProgress`". Current tests cover only `CreateErasurePendingState()`. `PartyTestData.CreateErasedState()` already exists and is used in `PartyAggregateErasureTests`. Add `Handle_UpdatePersonDetails_WhenErased_RejectsWithoutSuccessEvents` (and the org mirror) using it.
- [x] [Review][Patch] Strengthen null-payload rejection tests with event-count and display-name-absence assertions [tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateUpdateTests.cs:78-96, 236-254] — `Handle_UpdatePersonDetails_NullPayload_ReturnsRejection` and the org mirror assert only `Events[0]` is `PartyTypeMismatch` with a message. They should also assert `Events.Count.ShouldBe(1)` and `Events.OfType<PartyDisplayNameDerived>().ShouldBeEmpty()` to prove no derived-name event slipped through.
- [x] [Review][Patch] Symmetrize erasure-pending arrangement between person and organization tests [tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateUpdateTests.cs:329-352, 353-382] — Person test uses `PartyTestData.CreateErasurePendingState()` (factory-opaque); organization test calls `CreateOrganizationState()` + manual `state.Apply(new ErasePartyRequested { ..., RequestedAt = DateTimeOffset.UtcNow })`. The two arrangements may diverge silently and hide which fields actually matter. Pick one approach: either add `CreateErasurePendingOrganizationState()` to `PartyTestData` and use it, or build both explicitly.
- [x] [Review][Patch] Replace `DateTimeOffset.UtcNow` with fixed instants in arrange blocks [tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateUpdateTests.cs:363, 418] — `RequestedAt = DateTimeOffset.UtcNow` (org erasure test) and `RestrictedAt = DateTimeOffset.UtcNow` (org restriction test) inject a wall-clock dependency into a unit test. Pin to a constant such as `new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)` to keep arrangements deterministic.
- [x] [Review][Defer] Same-value detail-update no-op semantics not yet pinned by tests — deferred, pre-existing. Story 1.3 Deferred Decisions explicitly defers whether same-value updates emit audit events, are idempotent no-ops, or are rejected. Pick up when that policy decision is taken.
- [x] [Review][Defer] Whitespace-only / empty `FirstName`, `LastName`, `LegalName` payload handling — deferred, pre-existing. Story 1.3 defers name normalization beyond MVP display/sort-name rules. Currently `DeriveDisplayName` interpolates blanks verbatim. Pick up with normalization policy decision.
- [x] [Review][Defer] Deactivated-party detail-update precedence untested — deferred, pre-existing. Story 1.3 Task 3 explicitly defers deactivated-party update policy to Story 1.6. Helpers `CreateDeactivatedPersonState()` / `CreateDeactivatedOrganizationState()` exist; pin behaviour there.
- [x] [Review][Defer] Repeated-invocation rejection idempotency not exercised — deferred, pre-existing. Single-call rejection tests are sufficient for AC4/AC5. Multi-call idempotency proof belongs in Story 1.7 (idempotent commands and typed rejections).
- [x] [Review][Defer] Composite rejection paths beyond missing-party / type-mismatch lack `UpdatedPartyDetail.ShouldBeNull()` — deferred, pre-existing. New composite tests pin `UpdatedPartyDetail.ShouldBeNull()` on `PartyNotFound` and `PartyTypeMismatch` paths. Composite erasure/restriction/conflict rejection tests pre-date this story and do not assert that field. Extension belongs in a follow-up composite-hardening pass.
- [x] [Review][Defer] Grammar bug in production rejection message: `"Cannot update person details on a Organization party."` (should be "an Organization") — deferred, pre-existing. The string is emitted by production code and only newly asserted by this commit. Cosmetic; touching it would change `Hexalith.Parties.Server` production code, which is out of scope for a tests-only story.

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
- Erasure and processing-restriction guards are in scope because current direct handlers already enforce them. Deactivation/reactivation policy is reserved for Story 1.6 unless existing code already defines a narrow behavior that tests can preserve without adding lifecycle semantics.
- The story does not define new same-value update, audit-event, or name-normalization policy. Preserve current behavior unless a failing focused test proves documented drift.

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

### Deferred Decisions

- Whether same-value detail updates should emit audit events, be idempotent no-ops, or be rejected.
- Final person-name and organization-name normalization beyond the existing MVP display/sort-name rules.
- Deactivated-party detail update policy, unless existing lifecycle behavior is already documented by Story 1.6 or current tests.
- Whether FR69 returned-state evidence should be standardized on `CompositeCommandResult.UpdatedPartyDetail` for every later Epic 1 update story.

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

GPT-5 Codex

### Debug Log References

- `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateUpdateTests`
- `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateCompositeTests`
- `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateTests`
- `dotnet test Hexalith.Parties.slnx --configuration Release`

### Completion Notes List

- Audited the existing detail update contracts and aggregate handlers; direct update handlers remain pure static EventStore aggregate primitives returning `DomainResult`.
- Preserved current direct missing-party semantics as `PartyTypeMismatch { Message = "Party does not exist." }` and composite missing-party semantics as `PartyNotFound`.
- Added focused tests proving update events do not expose `PartyId`/`TenantId`, detail updates preserve the opposite party detail shape, rejection paths emit no success events, and rejected composite detail updates do not produce `UpdatedPartyDetail`.
- Confirmed FR69 returned-state behavior through `CompositeCommandResult.UpdatedPartyDetail` after person and organization detail-only composite updates.
- No public contract, aggregate implementation, transport, MCP, admin, picker, Dapr, or `PartyState.Apply` ordering changes were required.

### File List

- `_bmad-output/implementation-artifacts/1-3-update-person-and-organization-details.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateUpdateTests.cs`
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs`
## Party-Mode Review

- Date/time: 2026-05-15T19:03:33+02:00
- Selected story key: `1-3-update-person-and-organization-details`
- Command/skill invocation used: `/bmad-party-mode 1-3-update-person-and-organization-details; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - All reviewers initially recommended `needs-story-update`, not `blocked`, because the story needed sharper traceability before normal development.
  - Returned-state evidence for FR69 needed to name `CompositeCommandResult.UpdatedPartyDetail` and require post-event state assertions.
  - Rejection paths needed explicit no-success-event, no-returned-success-detail, and no-state-mutation expectations.
  - Person and organization update success paths needed clearer event ordering and cross-type non-mutation evidence.
  - Same-value update behavior, name normalization, and deactivated-party policy needed to be treated as deferred decisions instead of hidden implementation choices.
- Changes applied:
  - Added an acceptance traceability matrix mapping each AC to command paths, event/result evidence, state evidence, and focused validation targets.
  - Clarified success event ordering for `PersonDetailsUpdated`/`OrganizationDetailsUpdated` followed by `PartyDisplayNameDerived`.
  - Added explicit no-success-after-rejection, no returned success detail, and unchanged-state requirements for rejection paths.
  - Tightened FR69 tasks around `CompositeCommandResult.UpdatedPartyDetail` after events are projected.
  - Added privacy-safe test guidance and deferred decisions for same-value updates, normalization, deactivated-party policy, and future FR69 standardization.
- Findings deferred:
  - Same-value detail update semantics remain a product/domain decision.
  - Name and legal-name normalization beyond MVP display/sort-name derivation remains a product/domain decision.
  - Deactivated-party detail update behavior remains deferred to lifecycle Story 1.6 unless current tests already define a narrow behavior.
  - Broader FR69 response-shape standardization across later Epic 1 stories remains an architecture decision.
- Final recommendation: ready-for-dev

## Change Log

- 2026-05-16: Code review applied 6 test-quality patches (case-insensitive identity-property check, direct missing-party message assertion, terminal `Erased` coverage for both party types, null-payload event-count + display-name-absence assertions, symmetric erasure/restriction arrangement helpers, fixed-instant timestamps). 28 `PartyAggregateUpdateTests` and 50 `PartyAggregateCompositeTests` pass. Story closed as `done`.
- 2026-05-16: Implemented Story 1.3 test reconciliation; added focused success, rejection, metadata-boundary, and returned-state assertions; marked story ready for review.
- 2026-05-15: Advanced elicitation applied pre-dev clarifications for rejection evidence, direct/composite missing-party semantics, lifecycle guard precedence, FR69 post-event returned-state assertions, privacy-safe tests, and deferred normalization/localization decisions.
- 2026-05-15: Party-mode review applied pre-dev clarifications for AC traceability, update event ordering, no-success-after-rejection evidence, FR69 returned-state assertions, privacy-safe test expectations, and deferred lifecycle/normalization decisions.
- 2026-05-15: Story created by BMAD pre-dev hardening automation with current detail-update reconciliation context.

## Advanced Elicitation

- Date/time: 2026-05-15T22:02:52+02:00
- Selected story key: `1-3-update-person-and-organization-details`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-3-update-person-and-organization-details`
- Batch 1 method names:
  - Red Team vs Blue Team
  - Failure Mode Analysis
  - Security Audit Personas
  - Self-Consistency Validation
  - Architecture Decision Records
- Batch 2 method names:
  - Pre-mortem Analysis
  - Chaos Monkey Scenarios
  - User Persona Focus Group
  - Critique and Refine
  - Expand or Contract for Audience
- Findings summary:
  - The party-mode trace made the story ready, but the advanced pass found remaining implementation traps around string-only rejection assertions, ambiguous direct-versus-composite missing-party semantics, guard precedence for erasure/restriction paths, and accidental expansion of FR69 into direct aggregate response rewrites.
  - Security and privacy review found that tests can prove unchanged state and returned-state behavior without broad snapshots, logs, or operational output containing personal details.
  - Self-consistency and ADR review confirmed that MVP derived-name rules are sufficient for this story and that culture-specific sorting, normalization, and date-of-birth policy remain outside scope.
- Changes applied:
  - Added advanced elicitation clarifications for event-list rejection evidence, no-success-event assertions, and privacy-safe state checks.
  - Clarified that direct handler missing-party behavior and composite `PartyNotFound` behavior may remain distinct unless a narrow compatibility-safe migration is intentionally made.
  - Added guard-precedence guidance for erasure and processing-restriction rejections.
  - Tightened FR69 returned-state guidance around `CompositeCommandResult.UpdatedPartyDetail` as post-event aggregate state evidence, without changing direct aggregate primitive return types.
  - Reaffirmed MVP derived-name scope and deferred normalization/localization policy.
- Findings deferred:
  - Whether direct missing-party detail handlers should migrate from current `PartyTypeMismatch` wording to `PartyNotFound` remains a compatibility-sensitive domain/API mapping decision.
  - Culture-aware person-name formatting, sort-name collation, date-of-birth validation policy, and legal-name normalization remain product/domain decisions outside this story.
  - Standardizing all direct aggregate mutation handlers on public returned-state contracts remains deferred to FR69 follow-up work, primarily Story 1.9.
- Final recommendation: ready-for-dev
