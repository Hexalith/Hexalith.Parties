# Story 1.6: Deactivate and Reactivate Parties

Status: review

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
   - And the party remains inactive,
   - And the result does not imply deletion, erasure, archival, or data cleanup.

4. **Duplicate reactivate is retry-safe**
   - Given a reactivate command targets an existing already active party,
   - When the command is handled,
   - Then the aggregate idempotently no-ops or skips the duplicate lifecycle change according to the documented command idempotency rules,
   - And no additional `PartyReactivated` success event is emitted,
   - And the party remains active,
   - And the result does not imply that previously erased, restricted, or missing parties can be reactivated by retry semantics.

5. **Missing-party lifecycle commands reject without success events**
   - Given a lifecycle command targets a party that does not exist,
   - When the command is handled,
   - Then the aggregate emits the current typed `PartyNotFound` rejection contract,
   - And null aggregate state is treated as missing party, not as inactive or active lifecycle state,
   - And no successful lifecycle event is emitted,
   - And the rejection evidence is checked from the emitted event list so a rejection cannot be followed by a lifecycle success event in the same command result.

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
   - And any future delete-facing adapter wording must name soft deactivation as the domain behavior without adding erasure semantics to this story.

## Tasks / Subtasks

- [x] Task 1: Audit lifecycle contracts, validators, and current command surfaces (AC: 1, 2, 3, 4, 5, 7)
  - [x] Confirm `DeactivateParty`, `ReactivateParty`, `PartyDeactivated`, `PartyReactivated`, `PartyCannotBeDeactivatedWhenInactive`, `PartyCannotBeReactivatedWhenActive`, `PartyNotFound`, `PartyState`, and `PartyDetail` match this story's scope.
  - [x] Confirm `DeactivatePartyValidator` and `ReactivatePartyValidator` require non-empty GUID-shaped `PartyId` and do not add tenant/RBAC validation inside Parties.
  - [x] Treat "authorized client" as an upstream gateway/client precondition. Do not add tenant authorization to the aggregate, contracts, validators, projections, or client abstractions in this story.
  - [x] Confirm lifecycle success events intentionally carry no `PartyId`; aggregate identity remains EventStore metadata.
  - [x] Keep lifecycle as soft deactivation only. Do not wire GDPR erasure, key deletion, crypto-shredding, consent changes, or physical deletion into deactivate/reactivate commands.

- [x] Task 2: Reconcile standalone aggregate lifecycle behavior (AC: 1, 2, 3, 4, 5, 7)
  - [x] Confirm `PartyAggregate.Handle(DeactivateParty, PartyState?)` rejects null state with the accepted not-found rejection contract, guards erasure and processing restriction before success, no-ops already inactive state, and emits exactly one `PartyDeactivated` event on success.
  - [x] Confirm `PartyAggregate.Handle(ReactivateParty, PartyState?)` rejects null state with the accepted not-found rejection contract, guards erasure and processing restriction before success, no-ops already active state, and emits exactly one `PartyReactivated` event on success.
  - [x] Update the current null-state lifecycle behavior if needed. At story creation time the handlers returned `PartyCannotBeDeactivatedWhenInactive` and `PartyCannotBeReactivatedWhenActive` for missing state; the story AC requires a typed not-found rejection.
  - [x] Keep duplicate lifecycle commands as no-op retry-safe outcomes only when the party exists and is already in the requested lifecycle state, unless an accepted Story 1.7 decision changes command idempotency globally.
  - [x] Preserve guard precedence for erased or processing-restricted parties: they must reject through the accepted privacy/restriction contracts before any lifecycle success event or duplicate no-op outcome is treated as successful.
  - [x] Ensure rejection paths never also emit `PartyDeactivated` or `PartyReactivated`.

- [x] Task 3: Prove state rehydration preserves data across lifecycle events (AC: 1, 2, 3, 4, 7)
  - [x] Confirm `PartyState.Apply(PartyDeactivated)` sets only `IsActive = false`.
  - [x] Confirm `PartyState.Apply(PartyReactivated)` sets only `IsActive = true`.
  - [x] Add or update state tests that start from a party with details, contact channels, identifiers, and consent records where available, apply lifecycle events, and assert all non-lifecycle data is preserved.
  - [x] Include a deactivate-then-reactivate round trip state test so preservation is proven across both lifecycle transitions, not only each event in isolation.
  - [x] Preserve the no-op rejection `Apply(...)` methods before success applies; EventStore suffix-based rehydration depends on that ordering.
  - [x] Do not alter display name, sort name, created timestamp, erasure status, restriction status, contact channels, identifiers, or consent records in lifecycle `Apply` methods.

- [x] Task 4: Reconcile returned-state evidence for FR69 (AC: 6)
  - [x] Audit the current response path that is accepted for lifecycle mutations before changing public contracts. `BuildPartyDetailFromState(...)` already knows how to apply `PartyDeactivated` and `PartyReactivated` events, but `UpdatePartyComposite` currently has no lifecycle operation fields.
  - [x] If an accepted architecture/product decision already defines lifecycle in `UpdatePartyComposite`, implement that shape and prove `CompositeCommandResult.UpdatedPartyDetail.IsActive` changes after deactivate/reactivate.
  - [x] If no accepted decision exists, record a deferred decision instead of inventing a broad public contract shape. In that case, keep implementation focused on aggregate/state/client evidence and explicitly name the FR69 lifecycle response gap in the Dev Agent Record.
  - [x] If FR69 remains deferred for lifecycle commands, the Dev Agent Record must state which accepted evidence still exists (`BuildPartyDetailFromState(...)` lifecycle mapping) and which public mutation path is not yet approved.
  - [x] Do not change simple standalone `DomainResult` return types to carry `PartyDetail`; public updated-state behavior belongs to the accepted update response path, not ad hoc aggregate return changes.

- [x] Task 5: Preserve client and integration boundary semantics (AC: 6, 7)
  - [x] Confirm `IPartiesCommandClient.DeactivatePartyAsync` and `ReactivatePartyAsync` still send typed lifecycle commands without exposing event payload types through the client boundary.
  - [x] Confirm `HttpPartiesCommandClient` serialization uses the existing command envelope pattern and remains correlation/response compatible with current client tests.
  - [x] Keep MCP `delete_party` semantics as an adapter over soft deactivation, not erasure. Do not add MCP domain logic or event-type dependencies while working this story.
  - [x] Do not add REST controllers, Swagger/OpenAPI, in-process MCP tools, AdminPortal UI, Picker UI, projections, search, or samples unless a focused test fails because the existing contract already depends on lifecycle wording.

- [x] Task 6: Run focused validation (AC: 1, 2, 3, 4, 5, 6, 7)
  - [x] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateLifecycleTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests` if any `PartyState.Apply` declarations move.
  - [x] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~HttpPartiesCommandClientTests` if client command serialization is touched.
  - [x] Run `dotnet test tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj --configuration Release --filter FullyQualifiedName~PartiesMcpToolDispatchTests` if MCP delete/reactivate wording or dispatch is touched.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if implementation changes public contracts, validators, client abstractions, project references, or EventStore-facing surfaces.

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
- Erased and processing-restricted lifecycle tests should assert the accepted guard rejection wins before success or duplicate no-op semantics, and that no lifecycle success event appears later in the same event list.
- Preservation tests should include a full deactivate/reactivate round trip over a populated state and assert stable non-lifecycle fields by equality or count, without logging personal-data snapshots.
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

GPT-5 Codex

### Debug Log References

- 2026-05-17: Red test run confirmed null-state lifecycle commands emitted lifecycle-state rejections instead of `PartyNotFound`.
- 2026-05-17: Focused lifecycle, state, and MCP dispatch tests passed after implementation.
- 2026-05-17: `dotnet build Hexalith.Parties.slnx --configuration Release` passed.
- 2026-05-17: First solution-wide `dotnet test Hexalith.Parties.slnx --configuration Release --no-build` run had one timing-only benchmark miss; the exact failed benchmark passed on rerun.
- 2026-05-17: Second solution-wide `dotnet test Hexalith.Parties.slnx --configuration Release --no-build` run passed with 6 integration health checks skipped by test metadata.

### Implementation Plan

- Prove the documented missing-party gap with failing aggregate tests before changing the handlers.
- Keep lifecycle commands as standalone `DomainResult` operations; do not add lifecycle fields to `UpdatePartyComposite` without an accepted product/architecture decision.
- Add state preservation coverage around populated party state and the deactivate/reactivate round trip.
- Limit MCP changes to wording so the delete-facing adapter names soft deactivation without adding erasure semantics.

### Completion Notes List

- `DeactivateParty` and `ReactivateParty` now reject null aggregate state with `PartyNotFound` and do not emit lifecycle success events on that rejection path.
- Duplicate deactivate/reactivate behavior remains retry-safe no-op behavior for existing parties already in the requested lifecycle state.
- Added state tests proving lifecycle events preserve details, contact channels, identifiers, consent records, display/sort names, creation timestamp, erasure status, and restriction status.
- FR69 lifecycle returned-state public mutation remains deferred: `BuildPartyDetailFromState(...)` already maps `PartyDeactivated` and `PartyReactivated`, but `UpdatePartyComposite` has no accepted lifecycle operation fields yet.
- MCP delete-facing wording now says soft deactivation rather than soft deletion; no MCP domain behavior or event-type dependency was added.

### File List

- src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs
- src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs
- tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateLifecycleTests.cs
- tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs
- _bmad-output/implementation-artifacts/1-6-deactivate-and-reactivate-parties.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-05-17: Implemented lifecycle missing-party `PartyNotFound` behavior, lifecycle preservation tests, MCP soft-deactivation wording, and validation updates.
- 2026-05-15: Party-mode review applied pre-dev clarifications for missing-party `PartyNotFound`, duplicate no-op boundaries, FR69 contract deferral, privacy-safe preservation evidence, EventStore identity, and lifecycle wording.
- 2026-05-15: Story created by BMAD pre-dev hardening automation with current lifecycle reconciliation context.

## Advanced Elicitation

- Date/time: 2026-05-16T01:02:20+02:00
- Selected story key: `1-6-deactivate-and-reactivate-parties`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-6-deactivate-and-reactivate-parties`
- Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Security Audit Personas; Self-Consistency Validation; Architecture Decision Records
- Reshuffled Batch 2 method names: Pre-mortem Analysis; Chaos Monkey Scenarios; User Persona Focus Group; Critique and Refine; Expand or Contract for Audience
- Findings summary:
  - The main hidden coupling risk is guard precedence: erased or processing-restricted parties must not be treated as successful duplicate lifecycle no-ops.
  - Missing-party and rejection assertions need event-list evidence so a typed rejection cannot be followed by `PartyDeactivated` or `PartyReactivated` in the same command result.
  - Preservation should be proven across a deactivate/reactivate round trip, not only by isolated `Apply(...)` checks.
  - FR69 lifecycle returned-state evidence is still bounded to accepted response paths; the story must name the current mapping evidence and the unapproved public mutation gap if lifecycle composite operations remain deferred.
  - Delete-facing adapter wording remains a user/adopter risk because it can accidentally imply erasure rather than reversible soft lifecycle status.
- Changes applied:
  - Tightened duplicate lifecycle ACs so retry-safe no-op behavior cannot be read as deletion, cleanup, or resurrection of missing/restricted/erased parties.
  - Added explicit event-list rejection evidence for missing-party lifecycle commands.
  - Added guard-precedence task guidance for erased and processing-restricted lifecycle commands.
  - Added deactivate/reactivate round-trip preservation coverage and privacy-safe assertion guidance.
  - Clarified FR69 deferred evidence expectations and soft-deactivation wording for delete-facing adapters.
- Findings deferred:
  - First-class lifecycle operations in `UpdatePartyComposite` remain a product/architecture decision outside this hardening pass.
  - Global command idempotency semantics remain deferred to Story 1.7.
  - Broader REST, MCP response, AdminPortal, Picker, projection, search, and audit presentation behavior remains out of scope unless an existing focused contract proves the dependency.
- Final recommendation: ready-for-dev

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
