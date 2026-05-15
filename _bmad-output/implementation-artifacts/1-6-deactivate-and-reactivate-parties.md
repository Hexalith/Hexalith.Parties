# Story 1.6: Deactivate and Reactivate Parties

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized client,
I want to deactivate and reactivate party records without deleting their history,
so that lifecycle changes are reversible and auditable.

## Acceptance Criteria

1. **Deactivate emits lifecycle event and preserves party data**
   - Given an existing active party,
   - When the client deactivates the party,
   - Then the aggregate emits a `PartyDeactivated` event,
   - And `PartyState.IsActive` becomes `false` after the event is applied,
   - And person or organization details, contact channels, identifiers, consent records, display/sort names, and event history are not removed or rewritten.

2. **Reactivate emits lifecycle event and preserves party data**
   - Given an existing inactive party,
   - When the client reactivates the party,
   - Then the aggregate emits a `PartyReactivated` event,
   - And `PartyState.IsActive` becomes `true` after the event is applied,
   - And the prior party data remains intact.

3. **Duplicate deactivate is retry-safe**
   - Given a deactivate command targets an existing already inactive party,
   - When the command is handled,
   - Then the aggregate idempotently no-ops or skips the duplicate lifecycle change according to the documented command idempotency rules,
   - And no additional `PartyDeactivated` success event is emitted,
   - And the party remains inactive.

4. **Duplicate reactivate is retry-safe**
   - Given a reactivate command targets an existing already active party,
   - When the command is handled,
   - Then the aggregate idempotently no-ops or skips the duplicate lifecycle change according to the documented command idempotency rules,
   - And no additional `PartyReactivated` success event is emitted,
   - And the party remains active.

5. **Missing-party lifecycle commands reject without success events**
   - Given a lifecycle command targets a party that does not exist,
   - When the command is handled,
   - Then the aggregate emits the current typed `PartyNotFound` rejection contract,
   - And null aggregate state is treated as missing party, not as inactive or active lifecycle state,
   - And no successful lifecycle event is emitted.

6. **Returned update state reflects lifecycle changes**
   - Given a lifecycle mutation succeeds through the current update response path,
   - When the command result is returned,
   - Then the response includes the updated party state,
   - And the returned state reflects the new active or inactive status,
   - And no broad public contract shape is invented only to satisfy this story when the accepted update path does not yet define lifecycle operations.

7. **Lifecycle is soft deactivation, not erasure**
   - Given lifecycle command results, events, tests, user-visible wording, logs, telemetry, or MCP/admin integration text are reviewed,
   - When deactivation is described,
   - Then it is clearly treated as a reversible soft lifecycle/status transition,
   - And it is not described as archival, anonymization, GDPR erasure, deletion, crypto-shredding, or personal-data removal.

## Tasks / Subtasks

- [ ] Task 1: Audit lifecycle contracts, validators, and current command surfaces (AC: 1, 2, 3, 4, 5, 7)
  - [ ] Confirm `DeactivateParty`, `ReactivateParty`, `PartyDeactivated`, `PartyReactivated`, `PartyCannotBeDeactivatedWhenInactive`, `PartyCannotBeReactivatedWhenActive`, `PartyNotFound`, `PartyState`, and `PartyDetail` match this story's scope.
  - [ ] Confirm `DeactivatePartyValidator` and `ReactivatePartyValidator` require non-empty GUID-shaped `PartyId` and do not add tenant/RBAC validation inside Parties.
  - [ ] Treat "authorized client" as an upstream gateway/client precondition. Do not add tenant authorization to the aggregate, contracts, validators, projections, or client abstractions in this story.
  - [ ] Confirm lifecycle success events intentionally carry no `PartyId`; aggregate identity remains EventStore metadata.
  - [ ] Keep lifecycle as soft deactivation only. Do not wire GDPR erasure, key deletion, crypto-shredding, consent changes, or physical deletion into deactivate/reactivate commands.

- [ ] Task 2: Reconcile standalone aggregate lifecycle behavior (AC: 1, 2, 3, 4, 5, 7)
  - [ ] Confirm `PartyAggregate.Handle(DeactivateParty, PartyState?)` rejects null state with the accepted not-found rejection contract, guards erasure and processing restriction before success, no-ops already inactive state, and emits exactly one `PartyDeactivated` event on success.
  - [ ] Confirm `PartyAggregate.Handle(ReactivateParty, PartyState?)` rejects null state with the accepted not-found rejection contract, guards erasure and processing restriction before success, no-ops already active state, and emits exactly one `PartyReactivated` event on success.
  - [ ] Update the current null-state lifecycle behavior if needed. At story creation time the handlers returned `PartyCannotBeDeactivatedWhenInactive` and `PartyCannotBeReactivatedWhenActive` for missing state; the story AC requires a typed not-found rejection.
  - [ ] Keep duplicate lifecycle commands as no-op retry-safe outcomes only when the party exists and is already in the requested lifecycle state, unless an accepted Story 1.7 decision changes command idempotency globally.
  - [ ] Ensure rejection paths never also emit `PartyDeactivated` or `PartyReactivated`.

- [ ] Task 3: Prove state rehydration preserves data across lifecycle events (AC: 1, 2, 3, 4, 7)
  - [ ] Confirm `PartyState.Apply(PartyDeactivated)` sets only `IsActive = false`.
  - [ ] Confirm `PartyState.Apply(PartyReactivated)` sets only `IsActive = true`.
  - [ ] Add or update state tests that start from a party with details, contact channels, identifiers, and consent records where available, apply lifecycle events, and assert all non-lifecycle data is preserved.
  - [ ] Preserve the no-op rejection `Apply(...)` methods before success applies; EventStore suffix-based rehydration depends on that ordering.
  - [ ] Do not alter display name, sort name, created timestamp, erasure status, restriction status, contact channels, identifiers, or consent records in lifecycle `Apply` methods.

- [ ] Task 4: Reconcile returned-state evidence for FR69 (AC: 6)
  - [ ] Audit the current response path that is accepted for lifecycle mutations before changing public contracts. `BuildPartyDetailFromState(...)` already knows how to apply `PartyDeactivated` and `PartyReactivated` events, but `UpdatePartyComposite` currently has no lifecycle operation fields.
  - [ ] If an accepted architecture/product decision already defines lifecycle in `UpdatePartyComposite`, implement that shape and prove `CompositeCommandResult.UpdatedPartyDetail.IsActive` changes after deactivate/reactivate.
  - [ ] If no accepted decision exists, record a deferred decision instead of inventing a broad public contract shape. In that case, keep implementation focused on aggregate/state/client evidence and explicitly name the FR69 lifecycle response gap in the Dev Agent Record.
  - [ ] Do not change simple standalone `DomainResult` return types to carry `PartyDetail`; public updated-state behavior belongs to the accepted update response path, not ad hoc aggregate return changes.

- [ ] Task 5: Preserve client and integration boundary semantics (AC: 6, 7)
  - [ ] Confirm `IPartiesCommandClient.DeactivatePartyAsync` and `ReactivatePartyAsync` still send typed lifecycle commands without exposing event payload types through the client boundary.
  - [ ] Confirm `HttpPartiesCommandClient` serialization uses the existing command envelope pattern and remains correlation/response compatible with current client tests.
  - [ ] Keep MCP `delete_party` semantics as an adapter over soft deactivation, not erasure. Do not add MCP domain logic or event-type dependencies while working this story.
  - [ ] Do not add REST controllers, Swagger/OpenAPI, in-process MCP tools, AdminPortal UI, Picker UI, projections, search, or samples unless a focused test fails because the existing contract already depends on lifecycle wording.

- [ ] Task 6: Run focused validation (AC: 1, 2, 3, 4, 5, 6, 7)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateLifecycleTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests` if any `PartyState.Apply` declarations move.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~HttpPartiesCommandClientTests` if client command serialization is touched.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj --configuration Release --filter FullyQualifiedName~PartiesMcpToolDispatchTests` if MCP delete/reactivate wording or dispatch is touched.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if implementation changes public contracts, validators, client abstractions, project references, or EventStore-facing surfaces.

## Dev Notes

### Current Implementation Context

- This story is a reconciliation story over an already-evolved codebase. Start by proving the existing lifecycle implementation, then patch only concrete gaps.
- `DeactivateParty`, `ReactivateParty`, `PartyDeactivated`, `PartyReactivated`, lifecycle rejection events, validators, aggregate handlers, state apply methods, client methods, MCP delete dispatch, and lifecycle tests already exist.
- Current aggregate lifecycle success behavior is close to the story: deactivate active emits `PartyDeactivated`, reactivate inactive emits `PartyReactivated`, duplicate lifecycle changes no-op, erasure/restriction guards exist, and state apply toggles `IsActive`.
- Current missing-state behavior appears inconsistent with AC 5: null-state deactivate/reactivate returns lifecycle-state rejection events instead of `PartyNotFound`. Treat this as the primary aggregate gap unless an accepted architecture note says otherwise.
- `BuildPartyDetailFromState(...)` already maps lifecycle events to `PartyDetail.IsActive`, but `UpdatePartyComposite` currently has no lifecycle operation fields. Treat lifecycle returned-state evidence as a decision point, not a reason for broad unapproved public contract edits.

### Party-Mode Review Clarifications

- Null aggregate state for deactivate/reactivate means missing party. It must emit the typed `PartyNotFound` rejection and must not fall through to inactive/active lifecycle-state rejections.
- Duplicate deactivate/reactivate no-op behavior applies only after an existing party is already in the requested lifecycle state. Tests must assert the state remains unchanged and no extra lifecycle success event is emitted.
- Lifecycle success events must remain metadata-identified EventStore events; do not add or assert `PartyId` payload fields on `PartyDeactivated` or `PartyReactivated`.
- FR69 returned-state evidence is acceptable through the currently accepted update response path. If lifecycle operations are not yet accepted in that path, defer the public contract decision instead of expanding `UpdatePartyComposite`, client DTOs, MCP responses, UI, search, or projections in this story.
- Privacy-safe preservation tests should verify stable field presence/equality without writing personal-data snapshots, logs, or user-facing strings that imply deletion, anonymization, archival, or erasure.

### Architecture Patterns and Constraints

- Domain behavior belongs in pure static `PartyAggregate.Handle(Command, PartyState?)` methods returning `DomainResult` for simple commands and `CompositeCommandResult` for composite commands.
- `PartyState.Apply(...)` owns state mutation. Lifecycle apply methods must toggle only `IsActive` and must not delete or redact party data.
- Rejection events are persisted and replayed as no-op applies. Do not remove or reorder rejection `Apply(...)` overloads before lifecycle success applies.
- EventStore metadata carries aggregate identity. Do not add `PartyId` to `PartyDeactivated` or `PartyReactivated` unless a migration decision explicitly changes event contracts.
- The main `src/Hexalith.Parties` project is an actor host. Do not reintroduce public REST controllers, Swagger/OpenAPI, or in-process MCP tools.
- EventStore-owned gateways handle public request authorization. Parties owns domain behavior and projection-side access checks; this story does not move tenant/RBAC enforcement into lifecycle commands.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Contracts/
  Commands/DeactivateParty.cs
  Commands/ReactivateParty.cs
  Events/PartyDeactivated.cs
  Events/PartyReactivated.cs
  Events/PartyCannotBeDeactivatedWhenInactive.cs
  Events/PartyCannotBeReactivatedWhenActive.cs
  Events/PartyNotFound.cs
  Models/PartyDetail.cs
  Results/CompositeCommandResult.cs
  State/PartyState.cs

src/Hexalith.Parties.Server/
  Aggregates/PartyAggregate.cs

src/Hexalith.Parties/
  Validation/DeactivatePartyValidator.cs
  Validation/ReactivatePartyValidator.cs

src/Hexalith.Parties.Client/
  Abstractions/IPartiesCommandClient.cs
  HttpPartiesCommandClient.cs

src/Hexalith.Parties.Mcp/
  Tools/PartiesMcpTools.cs

tests/Hexalith.Parties.Server.Tests/
  Aggregates/PartyAggregateLifecycleTests.cs
  Aggregates/PartyAggregateErasureTests.cs
  Aggregates/PartyAggregateRestrictionTests.cs

tests/Hexalith.Parties.Contracts.Tests/
  State/PartyStateTests.cs

tests/Hexalith.Parties.Client.Tests/
  HttpPartiesCommandClientTests.cs

tests/Hexalith.Parties.Mcp.Tests/
  PartiesMcpToolDispatchTests.cs
```

### Previous Story Intelligence

- Story 1.1 framed early Epic 1 stories as audit/reconciliation work over an existing EventStore-shaped repository and warned not to delete later Epic 10-12 boundaries.
- Story 1.2 clarified that stable party identity belongs to EventStore aggregate/stream identity and success events should not grow `PartyId` fields casually.
- Stories 1.3 through 1.5 reinforced the distinction between standalone aggregate `DomainResult` behavior and the accepted returned-state evidence path for FR69.
- Story 1.5 explicitly deferred active/deactivated-party identifier policy to this lifecycle story. Use this story to clarify lifecycle commands themselves, but do not retrofit identifier/contact/detail mutation policy unless required by accepted ACs.
- Recent reviews favored low-risk clarifications, focused tests, and avoiding public contract drift without explicit architecture/product decisions.

### Testing Requirements

- Use xUnit v3 and Shouldly patterns already present in the repository.
- Prefer `PartyTestData` helpers such as `ValidDeactivateParty`, `ValidReactivateParty`, `CreatePersonState`, `CreateDeactivatedPersonState`, `CreateRestrictedState`, and erasure-state builders.
- Lifecycle aggregate tests should assert exact event counts for success and duplicate/rejection paths so a rejection cannot be followed by a success event.
- State tests should prove lifecycle events preserve data, not only toggle `IsActive`.
- AC-to-test trace should map AC 1-5 to aggregate command handling and state apply tests, AC 6 to accepted returned-state/update-path evidence or a named deferred decision, and AC 7 to contract/client/MCP wording only when those surfaces are touched.
- Missing-party tests should assert `PartyNotFound` for both deactivate and reactivate, plus no `PartyDeactivated` or `PartyReactivated` success event in the same result.
- If `PartyState.Apply` declarations move, rerun the apply-ordering fitness test and capture it in the Dev Agent Record.
- Client and MCP tests are only needed when their surfaces are touched or lifecycle wording/dispatch changes.

### Anti-Patterns To Avoid

- Do not model deactivation as archival, anonymization, erasure, deletion, key destruction, or personal-data cleanup.
- Do not add `TenantId` or `PartyId` to lifecycle success events.
- Do not convert duplicate lifecycle commands from no-op behavior to hard failures unless Story 1.7 changes idempotency globally.
- Do not emit success events after `PartyNotFound`, erasure, processing-restricted, or other rejection events.
- Do not change simple aggregate handlers to return `PartyDetail`.
- Do not add lifecycle UI, projection, search, REST, MCP hosting, or sample work to this story.
- Do not weaken `PartyState.Apply` ordering or remove rejection applies.
- Do not initialize nested submodules recursively.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.6] - Story statement and BDD acceptance criteria for FR4, FR5, and FR69.
- [Source: _bmad-output/planning-artifacts/prd.md#Functional-Requirements] - FR4, FR5, and FR69 lifecycle and updated-state requirements.
- [Source: _bmad-output/planning-artifacts/architecture.md#D8-MCP-Create-Strategy-Composite-Aggregate-Command] - Composite command pattern and atomic aggregate turn guidance.
- [Source: _bmad-output/planning-artifacts/architecture.md#D9-MCP-Update-Strategy-Composite-Command-with-Aggregate-Side-Diff] - Accepted update response path context and current composite scope.
- [Source: _bmad-output/planning-artifacts/architecture.md#D10-Composite-Sub-Operation-Idempotency-and-Conflict-Detection] - Retry-safe skip/no-op and per-operation outcome guidance.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - EventStore aggregate, actor-host, contract, privacy, testing, and submodule guardrails.
- [Source: _bmad-output/process-notes/story-creation-lessons.md#L08-Party-Review-vs-Elicitation] - Trace semantics for later party-mode and elicitation passes.
- [Source: _bmad-output/implementation-artifacts/1-5-manage-party-identifiers.md] - Previous ready story and deferred lifecycle-policy handoff.
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs] - Current standalone lifecycle handlers and updated-detail helper.
- [Source: src/Hexalith.Parties.Contracts/State/PartyState.cs] - Current lifecycle rehydration behavior and apply-ordering guardrail.
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateLifecycleTests.cs] - Current aggregate lifecycle coverage.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-05-15: Party-mode review applied pre-dev clarifications for missing-party `PartyNotFound`, duplicate no-op boundaries, FR69 contract deferral, privacy-safe preservation evidence, EventStore identity, and lifecycle wording.
- 2026-05-15: Story created by BMAD pre-dev hardening automation with current lifecycle reconciliation context.

## Party-Mode Review

- Date/time: 2026-05-15T20:10:29+02:00
- Selected story key: `1-6-deactivate-and-reactivate-parties`
- Command/skill invocation used: `/bmad-party-mode 1-6-deactivate-and-reactivate-parties; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - All reviewers recommended `ready-for-dev`; no reviewer identified a blocker.
  - The main implementation trap is treating null aggregate state as inactive/active lifecycle state instead of missing party.
  - Duplicate deactivate/reactivate behavior needed clearer scope: retry-safe no-op applies only when the party exists and is already in the requested lifecycle state.
  - FR69 lifecycle returned-state evidence remains bounded to an accepted update response path; broad `UpdatePartyComposite`, client, MCP, UI, projection, or search expansion requires a separate accepted decision.
  - Lifecycle wording and tests must preserve the distinction between reversible soft deactivation and archival, anonymization, deletion, erasure, crypto-shredding, or retention policy.
- Changes applied:
  - Tightened AC 3 and AC 4 to existing-party duplicate cases.
  - Tightened AC 5 to require typed `PartyNotFound` and no lifecycle success event for null/missing state.
  - Clarified AC 6 and Task 4 so the story does not invent a broad public returned-state contract.
  - Added party-mode review clarifications for EventStore identity, duplicate no-op boundaries, privacy-safe preservation assertions, and conditional client/MCP validation.
  - Added focused AC-to-test trace guidance for aggregate, state, returned-state, apply-ordering, and wording evidence.
- Findings deferred:
  - Whether lifecycle operations become first-class `UpdatePartyComposite` operations remains a product/architecture decision outside this pre-dev review.
  - Broader public API, MCP response, AdminPortal, Picker, projection, search, and audit presentation behavior remains out of scope unless a focused existing contract test proves a dependency.
  - Global command idempotency semantics remain deferred to the accepted Story 1.7 decision path.
- Final recommendation: ready-for-dev
