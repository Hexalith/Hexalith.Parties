# Story 1.4: Manage Contact Channels

Status: ready-for-dev

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

## Tasks / Subtasks

- [ ] Task 1: Audit existing contracts, validators, and payload representation (AC: 1, 2, 5)
  - [ ] Confirm `AddContactChannel`, `UpdateContactChannel`, `RemoveContactChannel`, `UpdatePartyComposite`, `ContactChannelAdded`, `ContactChannelUpdated`, `ContactChannelRemoved`, `PreferredContactChannelChanged`, `ContactChannelNotFound`, `PartyCannotAddDuplicateChannel`, `ContactChannel`, and `ContactChannelType` match this story's scope.
  - [ ] Confirm the current MVP representation is `ContactChannelType` plus `[PersonalData] string Value`; do not replace it with a polymorphic postal/email/phone/social payload model unless an explicit architecture decision updates the public contract.
  - [ ] Review `PostalAddress`, `EmailAddress`, `PhoneNumber`, and `SocialMediaHandle` as existing value-object inventory only. If they remain unused by commands/events, document the compatibility reason instead of wiring them into this story opportunistically.
  - [ ] Verify `src/Hexalith.Parties/Validation/AddContactChannelValidator.cs`, `UpdateContactChannelValidator.cs`, `RemoveContactChannelValidator.cs`, and `UpdatePartyCompositeValidator.cs` cover party id, channel id, enum, value, and sub-operation limit constraints expected at the boundary.
  - [ ] If stricter type-specific format validation is required beyond non-empty `Value`, keep it narrow and deterministic; do not add external address/email/phone normalization services.

- [ ] Task 2: Validate standalone aggregate contact-channel handlers (AC: 1, 2, 3, 4, 5)
  - [ ] Confirm `PartyAggregate.Handle(AddContactChannel, PartyState?)` rejects missing parties with `PartyNotFound`, no-ops duplicate channel ids, emits `ContactChannelAdded` on success, and emits `PreferredContactChannelChanged` when the added channel is preferred.
  - [ ] Confirm `PartyAggregate.Handle(UpdateContactChannel, PartyState?)` rejects missing parties and missing channels, emits `ContactChannelUpdated`, preserves omitted nullable fields as partial updates, and emits `PreferredContactChannelChanged` only when the update explicitly changes preferred status or type-preferred ownership.
  - [ ] Confirm `PartyAggregate.Handle(RemoveContactChannel, PartyState?)` rejects missing parties and missing channels, and emits `ContactChannelRemoved` for existing channels.
  - [ ] Verify erasure and processing restriction guards run before successful add/update/remove events.
  - [ ] Audit the "active party" wording in the ACs against current deactivated-party policy. If no accepted rejection contract exists for inactive-party contact-channel mutations, record the policy gap as deferred to the lifecycle story instead of inventing a new rejection event in this story.

- [ ] Task 3: Validate state rehydration and preferred-channel invariants (AC: 1, 2, 3, 4)
  - [ ] Confirm `PartyState.Apply(ContactChannelAdded)` appends a channel with id, type, value, and preferred flag.
  - [ ] Confirm `PartyState.Apply(ContactChannelUpdated)` merges only non-null `Type`, `Value`, and `IsPreferred` fields into the matching channel and ignores unknown ids defensively.
  - [ ] Confirm `PartyState.Apply(ContactChannelRemoved)` removes only the targeted channel id.
  - [ ] Confirm `PartyState.Apply(PreferredContactChannelChanged)` clears preferred flags only for the target channel's type and does not alter other channel types.
  - [ ] Preserve the no-op rejection `Apply(...)` methods before success applies; EventStore suffix-based rehydration depends on that ordering.

- [ ] Task 4: Reconcile composite command behavior and FR69 returned state (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Confirm `CreatePartyComposite` emits contact-channel add and preferred-change events for initial channels and safely skips duplicate channel ids for MCP/client retry safety.
  - [ ] Confirm `UpdatePartyComposite` validates missing channel updates/removals before emitting success events and rejects add/update/remove conflicts on the same channel id.
  - [ ] Confirm duplicate add/update/remove channel operations produce the current documented skipped outcomes without leaking personal data in `Applied`, `Skipped`, or `Rejected` strings.
  - [ ] Confirm `CompositeCommandResult.UpdatedPartyDetail` is built from current state plus emitted events and includes added channels, updated nullable-field merges, removed channels, and preferred-channel changes.
  - [ ] Do not change simple `DomainResult` return types for standalone handlers; public "updated state" response evidence for this story is the composite update path unless the architecture explicitly changes the response contract.

- [ ] Task 5: Preserve architecture, privacy, and boundary constraints (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Keep domain behavior in pure static aggregate `Handle` methods and `PartyState.Apply(...)`; do not add Dapr, database, MediatR handler, controller, Swagger/OpenAPI, MCP tool, AdminPortal, Picker, or sample work to this story.
  - [ ] Keep `Hexalith.Parties.Contracts` additive and dependency-light. Do not add hosting, Dapr, MediatR, FluentValidation, UI, or infrastructure dependencies to contracts.
  - [ ] Keep contact channel values marked as personal data in commands, events, state/model value objects, and any new tests that inspect privacy metadata.
  - [ ] Do not log, trace, exception-message, or outcome-string raw email addresses, phone numbers, postal addresses, social handles, or derived contact payload values.
  - [ ] Keep tenant context out of contact-channel commands/events for this story; aggregate identity is `PartyId`, and tenant authorization remains outside the aggregate.

- [ ] Task 6: Run focused validation (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateContactChannelTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateCompositeTests` if composite add/update/remove/preferred or returned-state behavior is inspected or changed.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests` if any `PartyState.Apply` declarations move.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if implementation changes touch public contracts, validators, project references, or EventStore-facing surfaces.

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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-05-15: Story created by BMAD pre-dev hardening automation with current contact-channel reconciliation context.
