# Story 1.4: Manage Contact Channels

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized client,
I want to add, update, remove, and mark preferred contact channels on a party,
so that party records carry usable structured contact information.

## Acceptance Criteria

1. **Adding contact channels emits events and mutates state**
   - Given an existing active party,
   - When the client adds a postal, email, phone, or social contact channel with valid type-specific data,
   - Then the aggregate emits a `ContactChannelAdded` event,
   - And `PartyState` includes the new channel with its type and payload after the event is applied.

2. **Updating contact channels emits events and mutates state**
   - Given an existing contact channel on an active party,
   - When the client updates the channel payload or metadata,
   - Then the aggregate emits a `ContactChannelUpdated` event,
   - And `PartyState` reflects the updated channel after the event is applied.

3. **Removing contact channels emits events and mutates state**
   - Given an existing contact channel on an active party,
   - When the client removes the channel,
   - Then the aggregate emits a `ContactChannelRemoved` event,
   - And `PartyState` no longer includes the removed channel after the event is applied.

4. **Preferred contact channel changes are type-scoped**
   - Given multiple contact channels of the same type exist for a party,
   - When the client marks one channel as preferred for that type,
   - Then the aggregate emits `PreferredContactChannelChanged`,
   - And only that channel is preferred for its type while preferred channels of other types remain unchanged.

5. **Invalid contact channel mutations reject without success events**
   - Given a contact channel command targets a missing party, missing channel, invalid channel payload, removed channel, erased party, or processing-restricted party,
   - When the command is handled,
   - Then the aggregate emits the current documented typed rejection event,
   - And no successful contact-channel event is emitted.

6. **Returned update state includes applied contact channel changes**
   - Given a contact channel mutation succeeds through the current update response path,
   - When the command result is returned,
   - Then the response includes the updated party state,
   - And the returned state includes the applied add, update, remove, and preferred-channel change.

### Advanced Elicitation Clarifications

- Treat contact-channel mutation evidence as event-list, state, and result-shape evidence, not message-string evidence alone. Focused tests should assert exact success or rejection event types, absence of success events after invalid commands, and unchanged state without snapshotting unnecessary contact values.
- Keep validation-layer and aggregate-layer identity rules distinct. Boundary validators may require GUID-shaped party and contact-channel ids, while pure aggregate tests may continue using existing human-readable ids unless the implementation intentionally reconciles both layers together.
- Preferred-channel edge cases should stay type-scoped and deterministic. If an update changes a channel type or preferred flag, tests should prove the current accepted behavior for the affected type only and record broader cross-type migration semantics as deferred rather than inventing them here.
- Composite returned-state evidence must be built from current state plus emitted success events only. Skipped duplicate operations and typed rejections must not appear as applied contact-channel mutations in `CompositeCommandResult.UpdatedPartyDetail`.
- Privacy checks should prefer absence assertions on operation outcomes, validation/rejection text inspected by tests, and diagnostic-style strings. Do not assert against raw email, phone, postal, or social values unless the field under test is explicitly the personal-data-carrying state or event payload.
- Keep resilience/idempotency limited to the current aggregate contract: duplicate add is a no-op or skipped outcome, missing update/remove is typed rejection, and repeated remove behaves as missing-channel unless a future history-aware contract says otherwise.

## Tasks / Subtasks

- [x] Task 1: Audit existing contracts, validators, and payload representation (AC: 1, 2, 5)
  - [x] Confirm `AddContactChannel`, `UpdateContactChannel`, `RemoveContactChannel`, `UpdatePartyComposite`, `ContactChannelAdded`, `ContactChannelUpdated`, `ContactChannelRemoved`, `PreferredContactChannelChanged`, `ContactChannelNotFound`, `PartyCannotAddDuplicateChannel`, `ContactChannel`, and `ContactChannelType` match this story's scope.
  - [x] Confirm the current MVP representation is a stable aggregate-local contact channel id, `ContactChannelType`, `[PersonalData] string Value`, and preferred-channel state; do not replace it with a polymorphic postal/email/phone/social payload model unless an explicit architecture decision updates the public contract.
  - [x] Review `PostalAddress`, `EmailAddress`, `PhoneNumber`, and `SocialMediaHandle` as existing value-object inventory only. If they remain unused by commands/events, document the compatibility reason instead of wiring them into this story opportunistically.
  - [x] Verify `src/Hexalith.Parties/Validation/AddContactChannelValidator.cs`, `UpdateContactChannelValidator.cs`, `RemoveContactChannelValidator.cs`, and `UpdatePartyCompositeValidator.cs` cover party id, channel id, enum, value, and sub-operation limit constraints expected at the boundary.
  - [x] If stricter type-specific format validation is required beyond non-empty `Value`, keep it narrow and deterministic; do not add external address/email/phone normalization services.

- [x] Task 2: Validate standalone aggregate contact-channel handlers (AC: 1, 2, 3, 4, 5)
  - [x] Confirm `PartyAggregate.Handle(AddContactChannel, PartyState?)` rejects missing parties with `PartyNotFound`, no-ops duplicate channel ids without emitting a success event, emits `ContactChannelAdded` on success, and emits `PreferredContactChannelChanged` when the added channel is preferred.
  - [x] Confirm `PartyAggregate.Handle(UpdateContactChannel, PartyState?)` rejects missing parties and missing channels, emits `ContactChannelUpdated`, preserves omitted nullable fields as partial updates, and emits `PreferredContactChannelChanged` only when the update explicitly changes preferred status or type-preferred ownership.
  - [x] Confirm `PartyAggregate.Handle(RemoveContactChannel, PartyState?)` rejects missing parties and missing channels, and emits `ContactChannelRemoved` for existing channels.
  - [x] Treat attempts to update, remove, or prefer a previously removed channel as missing-channel behavior unless a future event-sourced history contract explicitly distinguishes removed channels.
  - [x] Verify erasure and processing restriction guards run before successful add/update/remove events.
  - [x] Audit the "active party" wording in the ACs against current deactivated-party policy. If no accepted rejection contract exists for inactive-party contact-channel mutations, record the policy gap as deferred to the lifecycle story instead of inventing a new rejection event in this story.

- [x] Task 3: Validate state rehydration and preferred-channel invariants (AC: 1, 2, 3, 4)
  - [x] Confirm `PartyState.Apply(ContactChannelAdded)` appends a channel with id, type, value, and preferred flag.
  - [x] Confirm `PartyState.Apply(ContactChannelUpdated)` merges only non-null `Type`, `Value`, and `IsPreferred` fields into the matching channel and ignores unknown ids defensively.
  - [x] Confirm `PartyState.Apply(ContactChannelRemoved)` removes only the targeted channel id.
  - [x] Confirm `PartyState.Apply(PreferredContactChannelChanged)` clears preferred flags only for the target channel's type and does not alter other channel types.
  - [x] Confirm preferred-channel changes resolve the target channel after earlier events in the same command/composite operation are applied, so changing preferred email cannot disturb preferred phone, postal, or social channels.
  - [x] Preserve the no-op rejection `Apply(...)` methods before success applies; EventStore suffix-based rehydration depends on that ordering.

- [x] Task 4: Reconcile composite command behavior and FR69 returned state (AC: 1, 2, 3, 4, 5, 6)
  - [x] Confirm `CreatePartyComposite` emits contact-channel add and preferred-change events for initial channels and safely skips duplicate channel ids for MCP/client retry safety.
  - [x] Confirm `UpdatePartyComposite` validates missing channel updates/removals before emitting success events and rejects add/update/remove conflicts on the same channel id.
  - [x] Confirm duplicate add/update/remove channel operations produce the current documented skipped outcomes without leaking personal data in `Applied`, `Skipped`, or `Rejected` strings.
  - [x] Confirm composite sub-results and emitted success events preserve stable operation order for mixed applied, skipped, and rejected contact-channel operations.
  - [x] Confirm `CompositeCommandResult.UpdatedPartyDetail` is built from current state plus emitted success events only and includes added channels, updated nullable-field merges, removed channels, and preferred-channel changes.
  - [x] Do not change simple `DomainResult` return types for standalone handlers; public "updated state" response evidence for this story is the composite update path unless the architecture explicitly changes the response contract.

- [x] Task 5: Preserve architecture, privacy, and boundary constraints (AC: 1, 2, 3, 4, 5, 6)
  - [x] Keep domain behavior in pure static aggregate `Handle` methods and `PartyState.Apply(...)`; do not add Dapr, database, MediatR handler, controller, Swagger/OpenAPI, MCP tool, AdminPortal, Picker, or sample work to this story.
  - [x] Keep `Hexalith.Parties.Contracts` additive and dependency-light. Do not add hosting, Dapr, MediatR, FluentValidation, UI, or infrastructure dependencies to contracts.
  - [x] Keep contact channel values marked as personal data in commands, events, state/model value objects, and any new tests that inspect privacy metadata.
  - [x] Do not log, trace, exception-message, validation-message, rejection-message, or outcome-string raw email addresses, phone numbers, postal addresses, social handles, or derived contact payload values. Assert privacy by absence of raw values rather than by matching full human-readable messages.
  - [x] Keep tenant context out of contact-channel commands/events for this story; aggregate identity is `PartyId`, and tenant authorization remains outside the aggregate.

- [x] Task 6: Run focused validation (AC: 1, 2, 3, 4, 5, 6)
  - [x] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateContactChannelTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateCompositeTests` if composite add/update/remove/preferred or returned-state behavior is inspected or changed.
  - [x] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests` if any `PartyState.Apply` declarations move.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if implementation changes touch public contracts, validators, project references, or EventStore-facing surfaces.

## Required Test Evidence

| AC | Required evidence | Focused files/classes |
| --- | --- | --- |
| AC1 | Add succeeds with `ContactChannelAdded`, optional preferred-change event, post-apply state includes id/type/value/preferred state, duplicate add is skipped/no-op without a success event. | `PartyAggregateContactChannelTests`, `PartyAggregateCompositeTests`, `PartyStateTests` |
| AC2 | Update succeeds with `ContactChannelUpdated`, nullable fields merge into state, missing or removed channel rejects with unchanged state and no success event. | `PartyAggregateContactChannelTests`, `PartyAggregateCompositeTests`, `PartyStateTests` |
| AC3 | Remove succeeds with `ContactChannelRemoved`, state omits only the removed channel, repeated or missing remove rejects/skips according to direct versus composite rules without mutating state twice. | `PartyAggregateContactChannelTests`, `PartyAggregateCompositeTests`, `PartyStateTests` |
| AC4 | Preferred change is type-scoped: preferring one email leaves preferred phone, postal, and social channels unchanged; preferred target must exist after prior emitted events are applied. | `PartyAggregateContactChannelTests`, `PartyAggregateCompositeTests`, `PartyStateTests` |
| AC5 | Every invalid path asserts typed rejection, no success event, and unchanged state: missing party, missing/removed channel, invalid payload, duplicate/conflicting composite operation, erased party, and processing-restricted party. | `PartyAggregateContactChannelTests`, `PartyAggregateCompositeTests`, `PartyAggregateErasureTests`, `PartyAggregateRestrictionTests` |
| AC6 | `CompositeCommandResult.UpdatedPartyDetail` is asserted through the current composite response path after add, update, remove, and preferred changes; tests must not reconstruct an independent expected state as the only proof. | `PartyAggregateCompositeTests` |
| Privacy | Composite `Applied`, `Skipped`, and `Rejected` strings, rejection/validation text inspected by tests, and any log-style diagnostics must not include raw contact values. | `PartyAggregateCompositeTests`, contract privacy metadata tests when touched |

## Dev Notes

### Current Implementation Context

- This is the current backlog-tracked Epic 1 contact-channel story, but the repository already contains contact-channel handlers, contracts, validators, state apply behavior, composite command support, and tests.
- Treat this story as an audit/reconciliation pass over an already-evolved codebase. Start by proving what exists, then patch only concrete gaps.
- Historical `_bmad-output/implementation-artifacts/1-4-party-aggregate-update-details-and-lifecycle-management.md` is an older completed Story 1.4 for detail/lifecycle behavior. Use it only as evidence that early Epic 1 story numbers were later resliced; do not treat it as the canonical artifact for this story key.
- Expected implementation surfaces are `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`, `src/Hexalith.Parties.Contracts`, `src/Hexalith.Parties/Validation`, `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateContactChannelTests.cs`, `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs`, and `tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs`.

### Architecture Patterns and Constraints

- Parties validates the Hexalith.EventStore aggregate convention. Domain behavior belongs in pure static `Handle(Command, PartyState?)` methods that return `DomainResult` or `CompositeCommandResult`; state mutation belongs in `PartyState.Apply(Event)`.
- Standalone contact-channel handlers return `DomainResult`; composite create/update handlers return `CompositeCommandResult` with per-sub-operation `Applied`, `Skipped`, `Rejected`, and update-response `UpdatedPartyDetail`.
- `UpdatePartyComposite` is the architecture-approved update response path for FR69. It uses explicit add/update/remove lists, not MCP-side diff or generic patch semantics.
- Duplicate add operations are retry-safe skips. Missing update/remove targets are typed rejections. Conflicting add/update/remove operations on the same channel id are typed `CompositeOperationConflict` rejections.
- The main `src/Hexalith.Parties` project is an actor host. Do not reintroduce public REST controllers, Swagger/OpenAPI, or in-process MCP tools there while working this story.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Contracts/
  Commands/AddContactChannel.cs
  Commands/UpdateContactChannel.cs
  Commands/RemoveContactChannel.cs
  Commands/UpdatePartyComposite.cs
  Events/ContactChannelAdded.cs
  Events/ContactChannelUpdated.cs
  Events/ContactChannelRemoved.cs
  Events/PreferredContactChannelChanged.cs
  Events/ContactChannelNotFound.cs
  Events/PartyCannotAddDuplicateChannel.cs
  Results/CompositeCommandResult.cs
  State/PartyState.cs
  ValueObjects/ContactChannel.cs
  ValueObjects/ContactChannelType.cs
  ValueObjects/PostalAddress.cs
  ValueObjects/EmailAddress.cs
  ValueObjects/PhoneNumber.cs
  ValueObjects/SocialMediaHandle.cs

src/Hexalith.Parties.Server/
  Aggregates/PartyAggregate.cs

src/Hexalith.Parties/
  Validation/AddContactChannelValidator.cs
  Validation/UpdateContactChannelValidator.cs
  Validation/RemoveContactChannelValidator.cs
  Validation/UpdatePartyCompositeValidator.cs

tests/Hexalith.Parties.Server.Tests/
  Aggregates/PartyAggregateContactChannelTests.cs
  Aggregates/PartyAggregateCompositeTests.cs
  Aggregates/PartyAggregateErasureTests.cs
  Aggregates/PartyAggregateRestrictionTests.cs

tests/Hexalith.Parties.Contracts.Tests/
  State/PartyStateTests.cs
```

### Current Behavior Snapshot

- `AddContactChannel`, `UpdateContactChannel`, `ContactChannelAdded`, `ContactChannelUpdated`, and `ContactChannel` currently carry `ContactChannelType` plus `[PersonalData] string Value`; `UpdateContactChannel` uses nullable fields for partial update semantics.
- `ContactChannelType` currently contains `Email`, `Phone`, `PostalAddress`, and `SocialMedia`.
- Standalone add/update/remove handlers already reject null state with `PartyNotFound`, reject missing update/remove channel ids with `ContactChannelNotFound`, guard erasure/restriction before success, and avoid I/O.
- `AddContactChannel` no-ops duplicate channel ids. `UpdatePartyComposite` skips duplicate additions, reports duplicate update/remove entries as skipped, and rejects invalid update/remove targets before emitting success events.
- Preferred-channel behavior is represented by `PreferredContactChannelChanged`; state apply clears other preferred flags only inside the target channel's type.
- `BuildPartyDetailFromState(...)` already projects contact-channel add/update/remove/preferred events into `CompositeCommandResult.UpdatedPartyDetail`.
- Existing validators enforce GUID-shaped `PartyId` and, for composite channel ids, GUID-shaped `ContactChannelId`; several aggregate tests use human-readable ids. Preserve the distinction unless validation and tests are intentionally reconciled together.

### Previous Story Intelligence

- Story 1.1 framed early Epic 1 stories as reconciliation over an already-evolved repository and warned not to delete later Epic 10-12 boundaries.
- Story 1.2 clarified that aggregate identity belongs to EventStore metadata and that public contract changes should remain additive.
- Story 1.3 reinforced the pattern for direct aggregate primitives versus composite returned-state response evidence; this story should use the same distinction for FR69.
- The historical completed Story 1.4 mixed detail updates and lifecycle behavior before the planning reslice. Do not pull lifecycle or detail-update scope back into this story.
- Recent automation commits created Stories 1.1, 1.2, and 1.3 as ready-for-dev reconciliation stories (`374daec`, `98e310b`, `038ad6e`). Keep this story in the same style: audit first, patch narrow gaps, avoid broad rewrites.

### Testing Requirements

- Use xUnit v3 and Shouldly patterns already present in the repository.
- Prefer `PartyTestData` helpers for valid party states, channel-rich states, composite commands, erasure-pending state, and restricted state.
- Keep tests pure aggregate/state/validator tests unless the implementation touches transport or topology behavior.
- Add coverage for four contact channel types only where it proves current contract behavior; do not add integration tests or external format validation dependencies for postal/email/phone/social data.
- If changing `PartyState.Apply(...)` declarations, run the EventStore apply-ordering fitness test before broader suites.

### Anti-Patterns To Avoid

- Do not introduce a new contact-channel aggregate, repository, service wrapper, MediatR handler, controller, MCP tool, admin UI, picker UI, projection handler, or sample workflow for this story.
- Do not replace the existing scalar `Value` public contract with nested `PostalAddress`, `EmailAddress`, `PhoneNumber`, or `SocialMediaHandle` payloads without an explicit architecture decision.
- Do not add `TenantId` to contact-channel commands or success events.
- Do not add `PartyId` to contact-channel success events; aggregate identity is carried by EventStore metadata.
- Do not emit success events after a typed rejection or conflict.
- Do not leak raw contact-channel values in applied/skipped/rejected operation messages.
- Do not weaken or reorder rejection `Apply(...)` overloads in `PartyState`.
- Do not use recursive nested submodule initialization.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.4] - Story statement and BDD acceptance criteria for FR8, FR9, FR10, FR11, and FR69.
- [Source: _bmad-output/planning-artifacts/prd.md#Functional-Requirements] - FR8-FR11 and FR69 requirements for contact channels and returned mutation state.
- [Source: _bmad-output/planning-artifacts/architecture.md#D9-MCP-Update-Strategy] - Composite update command shape and aggregate-side update semantics.
- [Source: _bmad-output/planning-artifacts/architecture.md#D10-Composite-Sub-Operation-Idempotency-and-Conflict-Detection] - Duplicate, invalid id, and conflict behavior for composite operations.
- [Source: _bmad-output/planning-artifacts/architecture.md#Implementation-Patterns-and-Consistency-Rules] - command/event/state/aggregate conventions and contact-channel file locations.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - EventStore aggregate, actor-host, contract, privacy, testing, and submodule guardrails.
- [Source: _bmad-output/implementation-artifacts/1-3-update-person-and-organization-details.md] - Previous ready story and FR69 direct-vs-composite response distinction.
- [Source: _bmad-output/implementation-artifacts/1-4-party-aggregate-update-details-and-lifecycle-management.md] - Historical implementation intelligence only; not canonical for this resliced story.
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs] - Current standalone and composite contact-channel implementation surface.
- [Source: src/Hexalith.Parties.Contracts/State/PartyState.cs] - Current rehydration behavior and apply-ordering guardrail.
- [Source: src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs] - Current returned update detail carrier.
- [Source: src/Hexalith.Parties/Validation/AddContactChannelValidator.cs] - Current add command boundary validation.
- [Source: src/Hexalith.Parties/Validation/UpdatePartyCompositeValidator.cs] - Current composite update boundary validation.
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateContactChannelTests.cs] - Current standalone contact-channel coverage.
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs] - Current composite contact-channel and returned-state coverage.
- [Source: tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs] - Current state apply coverage.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-16: Added failing validator tests proving standalone contact-channel validators accepted non-GUID channel ids.
- 2026-05-16: Tightened standalone add/update/remove contact-channel validators to require GUID-shaped `ContactChannelId` while preserving required-field messages.
- 2026-05-16: Audited existing aggregate, state, composite, privacy, and architecture surfaces; no public contract shape or aggregate behavior changes were required beyond validator boundary reconciliation.
- 2026-05-16: Full solution regression initially hit two timing-sensitive search performance threshold failures; rerunning the failed benchmark classes passed, and the subsequent full solution test run passed.

### Completion Notes List

- Reconciled standalone contact-channel boundary validation with the existing composite validator behavior by requiring GUID-shaped contact-channel ids for add, update, and remove commands.
- Added focused validator coverage for invalid contact-channel ids on `AddContactChannel`, `UpdateContactChannel`, and `RemoveContactChannel`.
- Confirmed existing aggregate handlers, `PartyState.Apply(...)`, composite returned-state behavior, erasure/restriction guards, preferred-channel type scoping, scalar `[PersonalData] string Value` contract shape, and privacy-safe operation outcomes satisfy the story scope.
- `PartyState.Apply(...)` declarations were not moved, so the apply-ordering fitness test was not required for this implementation.
- Final validation passed with `dotnet test Hexalith.Parties.slnx --configuration Release`; integration health-check tests reported expected skips.

### File List

- src/Hexalith.Parties/Validation/AddContactChannelValidator.cs
- src/Hexalith.Parties/Validation/UpdateContactChannelValidator.cs
- src/Hexalith.Parties/Validation/RemoveContactChannelValidator.cs
- src/Hexalith.Parties/Validation/CreatePartyCompositeValidator.cs
- src/Hexalith.Parties/Validation/UpdatePartyCompositeValidator.cs
- tests/Hexalith.Parties.Tests/Validation/ContactChannelValidatorTests.cs
- tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs
- _bmad-output/implementation-artifacts/1-4-manage-contact-channels.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/deferred-work.md

## Party-Mode Review

- Date/time: 2026-05-15T19:14:59+02:00
- Selected story key: `1-4-manage-contact-channels`
- Command/skill invocation used: `/bmad-party-mode 1-4-manage-contact-channels; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - Winston and Murat considered the story bounded enough for development, while Amelia and John recommended `needs-story-update` until evidence and product semantics were sharper.
  - The common risk was not a new architecture decision; it was missing explicit evidence around typed rejection paths, no success events after invalid mutations, FR69 returned-state assertions, preferred-channel type scoping, stable channel identity, and privacy-safe operation outcomes.
  - Reviewers agreed that REST/OpenAPI/MCP, UI, projection/search, tenant authorization, lifecycle policy, normalization, and richer postal/email/phone/social payload modeling must remain outside this story unless accepted elsewhere.
- Changes applied:
  - Clarified the MVP contact-channel field shape as stable aggregate-local id, `ContactChannelType`, `[PersonalData] string Value`, and preferred-channel state while preserving the current scalar public contract.
  - Tightened duplicate and invalid mutation expectations to require typed rejection or no-op/skip behavior, unchanged state, and no emitted success event.
  - Added preferred-channel evidence that type scoping is preserved and the target channel is resolved after prior emitted events in the same command/composite path.
  - Tightened FR69 evidence so `CompositeCommandResult.UpdatedPartyDetail` is built from current state plus emitted success events only and is asserted through the composite response path.
  - Added a required test evidence matrix mapping AC1-AC6 and privacy assertions to focused aggregate, composite, state, erasure, and restriction test surfaces.
  - Expanded privacy guidance for validation, rejection, operation outcome, and diagnostic strings to assert absence of raw contact values.
- Findings deferred:
  - Inactive-party contact-channel policy remains deferred to lifecycle Story 1.6 unless current accepted tests already define narrow behavior.
  - International postal formatting, email/phone deliverability, normalization, contact-channel visibility/consent, contact-value search/indexing, UI accessibility copy, tenant-aware rules, projections, and public host-facing REST/OpenAPI/MCP surfaces remain out of scope.
  - Exact contact-channel uniqueness beyond stable channel id and duplicate operation handling remains governed by the existing aggregate/composite conventions unless product or architecture changes it later.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-15T23:08:46+02:00
- Selected story key: `1-4-manage-contact-channels`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-4-manage-contact-channels`
- Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Security Audit Personas; Self-Consistency Validation; Architecture Decision Records
- Reshuffled Batch 2 method names: Pre-mortem Analysis; Chaos Monkey Scenarios; User Persona Focus Group; Critique and Refine; Expand or Contract for Audience
- Findings summary:
  - The story was already ready for development after party-mode review, but implementation evidence needed sharper separation between boundary validation, pure aggregate behavior, event-list assertions, and privacy-safe outcome checks.
  - The highest-risk ambiguity was treating contact-channel mutation proof as message text or broad snapshots instead of exact event/result/state evidence.
  - Preferred-channel type-change semantics and richer contact-value normalization remain decision-heavy areas and should not be silently solved during this reconciliation story.
- Changes applied:
  - Added advanced elicitation clarifications for event-list and no-success-event assertions, boundary-vs-aggregate id rules, type-scoped preferred-channel edge cases, FR69 composite returned-state evidence, privacy-safe absence assertions, and current idempotency boundaries.
- Findings deferred:
  - Cross-type preferred-channel migration semantics beyond the current accepted behavior remain deferred unless existing tests already define them.
  - International contact validation, deliverability, normalization, visibility/consent, projections/search, UI, REST/OpenAPI, MCP, and tenant-aware policy remain out of scope.
  - Any public contract change that replaces scalar `[PersonalData] string Value` with richer postal/email/phone/social payloads remains an architecture decision outside this story.
- Final recommendation: ready-for-dev

### Review Findings

_Code review on 2026-05-16 (commit `e428d6d`). Layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor. 9 findings after triage (6 dismissed)._

- [x] [Review][Patch] Relax `UpdateContactChannelValidator` and `RemoveContactChannelValidator` to drop the GUID rule (keep Add strict) — Resolution to validator-vs-aggregate contract drift: keep GUID enforcement on `AddContactChannel` only. Drop `.Cascade(CascadeMode.Stop)` + `.Must(Guid.TryParse...)` from `UpdateContactChannelValidator` and `RemoveContactChannelValidator` so legacy non-GUID channels can still be administered. Remove the now-stale invalid-id failure tests for those two validators (replace with valid-GUID happy-path + empty-id required-message tests). [src/Hexalith.Parties/Validation/UpdateContactChannelValidator.cs, RemoveContactChannelValidator.cs, tests/Hexalith.Parties.Tests/Validation/ContactChannelValidatorTests.cs]
- [x] [Review][Patch] Align composite child rules to `AddContactChannelValidator` shape — Resolution to composite-vs-standalone divergence: in `CreatePartyCompositeValidator.AddContactChannels[i].ContactChannelId` and `UpdatePartyCompositeValidator.AddContactChannels[i].ContactChannelId` add `.Cascade(CascadeMode.Stop)` + explicit `"ContactChannelId is required."` message so empty id produces exactly one error. Apply the same `Cascade(Stop)` + required-message shape to `UpdateContactChannels[i].ContactChannelId` and `RemoveContactChannelIds[i]` (without GUID rule, per Decision 1). [src/Hexalith.Parties/Validation/CreatePartyCompositeValidator.cs, UpdatePartyCompositeValidator.cs]
- [x] [Review][Patch] AC6 has no test asserting `UpdatedPartyDetail.ContactChannels` in the composite response — Composite tests `Handle_UpdatePartyComposite_AddChannelsOnly_…` (line 431), `_UpdateChannelsOnly_…` (459), `_RemoveChannelsOnly_…` (487) in `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs` only assert `result.Events`/`result.Applied`; they do not touch `result.UpdatedPartyDetail`. Required Test Evidence row AC6 demands assertion through the composite response path for add/update/remove/preferred-change. Implementation logic exists at `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs:1305-1337` but is untested at the AC6 boundary. [tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs:431,459,487]
- [x] [Review][Patch] No happy-path validator tests — inverted `Must` predicate would not be caught — All three new tests in `tests/Hexalith.Parties.Tests/Validation/ContactChannelValidatorTests.cs` use `ContactChannelId = "not-a-guid"` and assert failure. A typo like `!Guid.TryParse(id, out _)` would leave every test passing. Add one positive-case test per validator with a valid GUID. [tests/Hexalith.Parties.Tests/Validation/ContactChannelValidatorTests.cs]
- [x] [Review][Patch] No test for empty/null/whitespace `ContactChannelId` after `Cascade(Stop)` — `Cascade(CascadeMode.Stop)` was introduced alongside the new `Must` rule. A future refactor that removes the cascade would silently start emitting two messages for the empty case. Add at least one `[Theory]` row asserting that `""` / `null` produces exactly `"ContactChannelId is required."`. [tests/Hexalith.Parties.Tests/Validation/ContactChannelValidatorTests.cs]
- [x] [Review][Patch] Inconsistent cascade behaviour for `PartyId` vs `ContactChannelId` in the same validator — `PartyId` rule lacks `Cascade(CascadeMode.Stop)` and emits two messages (`NotEmpty` default + `"PartyId must be a valid GUID."`) for an empty id, while `ContactChannelId` now emits one. Same validator class, two different validation styles. Either add `Cascade(CascadeMode.Stop)` + a "PartyId is required." message to `PartyId`, or drop the cascade from `ContactChannelId` for symmetry. [src/Hexalith.Parties/Validation/AddContactChannelValidator.cs:11-21, UpdateContactChannelValidator.cs, RemoveContactChannelValidator.cs]
- [x] [Review][Patch] Tests use `ShouldContain` instead of asserting error count — `result.Errors.Select(e => e.PropertyName).ShouldContain(…)` only verifies one error is present; other unrelated errors on the same command could be hiding. Tighten the assertions to also verify `result.Errors.Count(e => e.PropertyName == nameof(...ContactChannelId)) == 1` (or equivalent). [tests/Hexalith.Parties.Tests/Validation/ContactChannelValidatorTests.cs]
- [x] [Review][Defer] GUID whitespace/brace forms accepted by validator but compared ordinally by aggregate — deferred, pre-existing — `Guid.TryParse(" {valid-guid} ", out _)` returns `true`, so the validator accepts wrapped/whitespace forms; the aggregate compares with ordinal `==` (`PartyAggregate.cs:917,959,1010`) so a later `Update` with the trimmed form silently returns `ContactChannelNotFound`. Pre-existing — this diff merely advertises GUID semantics more sharply. Pair with normalization story or a single `Guid.Parse(...).ToString("D")` pass at command construction. [src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs:917,959,1010]
- [x] [Review][Defer] `PartyTestData` builders use non-GUID ids that future validator-running integration tests would now reject — deferred, pre-existing — `PartyTestData.DefaultChannelId = "ch-email-1"`, `"id-vat-1"`, etc. The current diff explicitly preserves the "human-readable ids in pure aggregate tests" distinction per the Advanced Elicitation Clarifications. The trap activates only if someone later wires `ValidCreatePersonComposite()` into a `WebApplicationFactory` integration test that runs the validators. Pair with the future integration-test story or replace static strings with deterministic GUID constants. [src/Hexalith.Parties.Testing/PartyTestData.cs]

_Dismissed (not actionable): diff artifact on Update validator (file compiles), `UpdateContactChannel.Type` suspicion (Edge Case Hunter verified the command shape), LINQ implicit usings (tests pass), DRY/var style nits, whitespace-only-id "valid GUID" message (acceptable UX per spec)._

## Change Log

- 2026-05-17: Code review patches applied. Decision 1: relaxed `UpdateContactChannelValidator` and `RemoveContactChannelValidator` to drop the GUID rule (kept Add strict) so legacy non-GUID channels remain administrable. Decision 2: aligned composite child rules in `CreatePartyCompositeValidator` and `UpdatePartyCompositeValidator` to the standalone shape (Cascade Stop + explicit "is required." message; GUID still required on composite Add). Also applied Cascade Stop + "PartyId is required." to PartyId rules for in-validator consistency. Tightened `ContactChannelValidatorTests` (happy-path, empty-id, error-count assertions) and added `UpdatedPartyDetail.ContactChannels` evidence for AC6 in `PartyAggregateCompositeTests` (add/update/remove/preferred-change). Two findings deferred (GUID whitespace/brace acceptance, `PartyTestData` non-GUID ids).
- 2026-05-16: Implemented story 1.4 contact-channel reconciliation; added standalone validator coverage and enforced GUID-shaped contact-channel ids on add/update/remove boundary validators.
- 2026-05-15: Advanced elicitation applied pre-dev clarifications for event-list evidence, boundary-vs-aggregate id rules, preferred-channel edge cases, composite returned-state evidence, privacy-safe assertions, and current idempotency boundaries.
- 2026-05-15: Party-mode review applied pre-dev clarifications for contact-channel MVP field shape, rejection/no-success evidence, preferred-channel type scoping, FR69 returned-state assertions, privacy-safe outcome checks, focused test evidence, and deferred lifecycle/normalization/surface decisions.
- 2026-05-15: Story created by BMAD pre-dev hardening automation with current contact-channel reconciliation context.
