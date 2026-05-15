# Story 1.5: Manage Party Identifiers

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized client,
I want to add and remove jurisdiction-specific identifiers on a party,
so that consumers can associate parties with external legal, tax, or registry references.

## Acceptance Criteria

1. **Adding identifiers emits events and mutates state**
   - Given an existing active party,
   - When the client adds a valid identifier such as VAT, SIRET, national ID, company registration, tax id, or other jurisdiction-specific reference,
   - Then the aggregate emits an `IdentifierAdded` event,
   - And `PartyState` includes the identifier type, value, and stable identifier key after the event is applied.

2. **Removing identifiers emits events and mutates state**
   - Given an existing identifier on an active party,
   - When the client removes that identifier,
   - Then the aggregate emits an `IdentifierRemoved` event,
   - And `PartyState` no longer includes the removed identifier after the event is applied.

3. **Duplicate identifier additions are retry-safe**
   - Given the client adds an identifier id that already exists on the party,
   - When the command is handled directly or through the composite update path,
   - Then the aggregate rejects or idempotently skips the duplicate according to the documented command idempotency rules,
   - And the party state does not contain duplicate identifier entries.

4. **Invalid identifier mutations reject without success events**
   - Given an identifier command targets a missing party, missing identifier, invalid identifier payload, erased party, or processing-restricted party,
   - When the command is handled,
   - Then the aggregate emits the current documented typed rejection event,
   - And no successful identifier event is emitted.

5. **Returned update state includes applied identifier changes**
   - Given an identifier mutation succeeds through the current update response path,
   - When the command result is returned,
   - Then the response includes the updated party state,
   - And the returned state includes the applied identifier addition or removal.

6. **Identifier values remain privacy-safe**
   - Given identifier values may identify natural persons or sensitive business records,
   - When commands, events, state, operation outcomes, tests, logs, or telemetry are reviewed,
   - Then identifier values are marked and handled as personal data where applicable,
   - And raw identifier values are not placed in logs, telemetry dimensions, exception text, or applied/skipped/rejected operation strings.

## Tasks / Subtasks

- [ ] Task 1: Audit existing identifier contracts, validators, and payload representation (AC: 1, 2, 3, 4, 6)
  - [ ] Confirm `AddIdentifier`, `RemoveIdentifier`, `CreatePartyComposite`, `UpdatePartyComposite`, `IdentifierAdded`, `IdentifierRemoved`, `IdentifierNotFound`, `PartyCannotAddDuplicateIdentifier`, `PartyIdentifier`, `IdentifierType`, and `PartyState` match this story's scope.
  - [ ] Confirm the current MVP command/event representation is identifier id, `IdentifierType`, and `[PersonalData] string Value`; do not add broad jurisdiction metadata to commands/events unless an explicit architecture decision updates the public contract.
  - [ ] Review `PartyIdentifier.Jurisdiction` as current value-object inventory. Because `AddIdentifier` and `IdentifierAdded` do not currently carry jurisdiction, treat jurisdiction persistence as a deferred contract decision unless already accepted elsewhere.
  - [ ] Verify `src/Hexalith.Parties/Validation/AddIdentifierValidator.cs`, `RemoveIdentifierValidator.cs`, `CreatePartyCompositeValidator.cs`, and `UpdatePartyCompositeValidator.cs` cover party id, identifier id, enum value, identifier value, and sub-operation limit constraints expected at the boundary.
  - [ ] Preserve the current distinction between standalone aggregate tests that use readable identifier ids and composite validators that require GUID-shaped identifier ids unless validation and tests are intentionally reconciled together.

- [ ] Task 2: Validate standalone aggregate identifier handlers (AC: 1, 2, 3, 4)
  - [ ] Confirm `PartyAggregate.Handle(AddIdentifier, PartyState?)` rejects missing parties with `PartyNotFound`, guards erasure and processing restriction before success, no-ops duplicate identifier ids, and emits `IdentifierAdded` on success.
  - [ ] Confirm `PartyAggregate.Handle(RemoveIdentifier, PartyState?)` rejects missing parties with `PartyNotFound`, guards erasure and processing restriction before success, rejects missing identifier ids with `IdentifierNotFound`, and emits `IdentifierRemoved` on success.
  - [ ] Audit the "active party" wording in the ACs against current deactivated-party policy. If no accepted rejection contract exists for inactive-party identifier mutations, record the policy gap as deferred to the lifecycle story instead of inventing a new rejection event in this story.
  - [ ] Do not add identifier search, duplicate detection by identifier value, normalization, or jurisdiction-specific validation services to this story; MVP search by identifier is deferred to the dedicated search capability.

- [ ] Task 3: Validate state rehydration and privacy metadata (AC: 1, 2, 6)
  - [ ] Confirm `PartyState.Apply(IdentifierAdded)` appends an identifier with id, type, and value.
  - [ ] Confirm `PartyState.Apply(IdentifierRemoved)` removes only the targeted identifier id and tolerates unknown ids defensively.
  - [ ] Confirm `PartyIdentifier.Value`, `AddIdentifier.Value`, and `IdentifierAdded.Value` remain marked with `[PersonalData]`.
  - [ ] Preserve the no-op rejection `Apply(...)` methods before success applies; EventStore suffix-based rehydration depends on that ordering.

- [ ] Task 4: Reconcile composite command behavior and FR69 returned state (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Confirm `CreatePartyComposite` emits `IdentifierAdded` events for initial identifiers and safely skips duplicate identifier ids for MCP/client retry safety.
  - [ ] Confirm `UpdatePartyComposite` rejects add/remove conflicts on the same identifier id, validates missing remove targets before emitting success events, skips duplicate additions, and skips duplicate removals without mutating state twice.
  - [ ] Confirm duplicate add/remove and missing identifier outcomes do not include raw identifier values in `Applied`, `Skipped`, or `Rejected` strings. Identifier ids are allowed as stable operation identifiers when they are GUID-shaped or otherwise non-PII.
  - [ ] Confirm `CompositeCommandResult.UpdatedPartyDetail` is built from current state plus emitted events and includes added and removed identifiers.
  - [ ] Do not change simple `DomainResult` return types for standalone handlers; public "updated state" response evidence for this story is the composite update path unless the architecture explicitly changes the response contract.

- [ ] Task 5: Preserve architecture, scope, and boundary constraints (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Keep domain behavior in pure static aggregate `Handle` methods and `PartyState.Apply(...)`; do not add Dapr, database, MediatR handler, controller, Swagger/OpenAPI, MCP tool, AdminPortal, Picker, projection, search, or sample work to this story.
  - [ ] Keep `Hexalith.Parties.Contracts` additive and dependency-light. Do not add hosting, Dapr, MediatR, FluentValidation, UI, or infrastructure dependencies to contracts.
  - [ ] Keep tenant context out of identifier commands/events for this story; aggregate identity is `PartyId`, and tenant authorization remains outside the aggregate.
  - [ ] Do not add identifier-value normalization, country-specific checksum validation, or duplicate matching by value unless a separate architecture/product decision explicitly schedules it.
  - [ ] Do not broaden this story into lifecycle, contact-channel, GDPR erasure, read projection, REST, MCP, admin, or picker behavior.

- [ ] Task 6: Run focused validation (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateIdentifierTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateCompositeTests` if composite add/remove/skipped/rejected or returned-state behavior is inspected or changed.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests` if any `PartyState.Apply` declarations move.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if implementation changes touch public contracts, validators, project references, or EventStore-facing surfaces.

## Dev Notes

### Current Implementation Context

- This is the current backlog-tracked Epic 1 identifier story, but the repository already contains identifier handlers, contracts, validators, state apply behavior, composite command support, and focused tests.
- Treat this story as an audit/reconciliation pass over an already-evolved codebase. Start by proving what exists, then patch only concrete gaps.
- Expected implementation surfaces are `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`, `src/Hexalith.Parties.Contracts`, `src/Hexalith.Parties/Validation`, `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateIdentifierTests.cs`, `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs`, and `tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs`.

### Architecture Patterns and Constraints

- Parties validates the Hexalith.EventStore aggregate convention. Domain behavior belongs in pure static `Handle(Command, PartyState?)` methods that return `DomainResult` or `CompositeCommandResult`; state mutation belongs in `PartyState.Apply(Event)`.
- Standalone identifier handlers return `DomainResult`; composite create/update handlers return `CompositeCommandResult` with per-sub-operation `Applied`, `Skipped`, `Rejected`, and update-response `UpdatedPartyDetail`.
- `UpdatePartyComposite` is the architecture-approved update response path for FR69. It uses explicit add/remove identifier lists, not MCP-side diff or generic patch semantics.
- Duplicate add operations are retry-safe skips/no-ops. Missing remove targets are typed rejections. Conflicting add/remove operations on the same identifier id are typed `CompositeOperationConflict` rejections.
- The main `src/Hexalith.Parties` project is an actor host. Do not reintroduce public REST controllers, Swagger/OpenAPI, or in-process MCP tools there while working this story.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Contracts/
  Commands/AddIdentifier.cs
  Commands/RemoveIdentifier.cs
  Commands/CreatePartyComposite.cs
  Commands/UpdatePartyComposite.cs
  Events/IdentifierAdded.cs
  Events/IdentifierRemoved.cs
  Events/IdentifierNotFound.cs
  Events/PartyCannotAddDuplicateIdentifier.cs
  Results/CompositeCommandResult.cs
  State/PartyState.cs
  ValueObjects/PartyIdentifier.cs
  ValueObjects/IdentifierType.cs

src/Hexalith.Parties.Server/
  Aggregates/PartyAggregate.cs

src/Hexalith.Parties/
  Validation/AddIdentifierValidator.cs
  Validation/RemoveIdentifierValidator.cs
  Validation/CreatePartyCompositeValidator.cs
  Validation/UpdatePartyCompositeValidator.cs

tests/Hexalith.Parties.Server.Tests/
  Aggregates/PartyAggregateIdentifierTests.cs
  Aggregates/PartyAggregateCompositeTests.cs
  Aggregates/PartyAggregateErasureTests.cs
  Aggregates/PartyAggregateRestrictionTests.cs

tests/Hexalith.Parties.Contracts.Tests/
  State/PartyStateTests.cs
```

### Current Behavior Snapshot

- `AddIdentifier`, `IdentifierAdded`, and `PartyIdentifier` currently carry `IdentifierType` plus `[PersonalData] string Value`; `PartyIdentifier` also has optional `Jurisdiction`, but current add commands/events do not populate it.
- `IdentifierType` currently contains `VAT`, `SIRET`, `NationalId`, `CompanyRegistration`, `TaxId`, and `Other`.
- Standalone add/remove handlers already reject null state with `PartyNotFound`, guard erasure/restriction before success, no-op duplicate identifier ids on add, and reject missing remove ids with `IdentifierNotFound`.
- `PartyState.Apply(IdentifierAdded)` appends id/type/value. `PartyState.Apply(IdentifierRemoved)` removes matching ids.
- `CreatePartyComposite` and `UpdatePartyComposite` already include identifier add/remove paths. Composite update detects add/remove conflicts, skips duplicate add/remove operations, rejects invalid remove targets, and builds `CompositeCommandResult.UpdatedPartyDetail` from state plus emitted events.
- Existing validators enforce GUID-shaped `PartyId` and, in composite identifier lists, GUID-shaped `IdentifierId`; several aggregate tests use human-readable ids such as `id-vat-1`. Preserve the distinction unless validation and tests are intentionally reconciled together.

### Previous Story Intelligence

- Story 1.1 framed early Epic 1 stories as reconciliation over an already-evolved repository and warned not to delete later Epic 10-12 boundaries.
- Story 1.2 clarified that aggregate identity belongs to EventStore metadata and that public contract changes should remain additive.
- Story 1.3 reinforced the pattern for direct aggregate primitives versus composite returned-state response evidence; this story should use the same distinction for FR69.
- Story 1.4 established the same audit-first pattern for contact-channel contracts, aggregate handlers, validators, state applies, composite returned state, privacy, and narrow tests. Keep identifier work in that style.
- Recent automation commits created Stories 1.1 through 1.4 as ready-for-dev reconciliation stories (`374daec`, `98e310b`, `038ad6e`, `a35d2f1`). Keep this story narrow: audit first, patch focused gaps, avoid broad rewrites.

### Testing Requirements

- Use xUnit v3 and Shouldly patterns already present in the repository.
- Prefer `PartyTestData` helpers for valid party states, identifier-rich states, composite commands, erasure-pending state, and restricted state.
- Keep tests pure aggregate/state/validator tests unless the implementation touches transport or topology behavior.
- Add coverage for identifier types only where it proves current contract behavior; do not add external tax, company registry, national-id, or jurisdiction validation dependencies.
- If changing `PartyState.Apply(...)` declarations, run the EventStore apply-ordering fitness test before broader suites.

### Anti-Patterns To Avoid

- Do not introduce a new identifier aggregate, repository, service wrapper, MediatR handler, controller, MCP tool, admin UI, picker UI, projection handler, search index, or sample workflow for this story.
- Do not add `TenantId` to identifier commands or success events.
- Do not add `PartyId` to identifier success events; aggregate identity is carried by EventStore metadata.
- Do not emit success events after a typed rejection or conflict.
- Do not leak raw identifier values in applied/skipped/rejected operation messages, logs, telemetry dimensions, exception messages, routes, filenames, or test output names.
- Do not implement identifier-based search or duplicate detection in this story; MVP search by email/identifier is explicitly deferred to the dedicated search capability.
- Do not weaken or reorder rejection `Apply(...)` overloads in `PartyState`.
- Do not use recursive nested submodule initialization.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.5] - Story statement and BDD acceptance criteria for FR12, FR13, and FR69.
- [Source: _bmad-output/planning-artifacts/prd.md#Identifier-Management-MVP] - FR12 and FR13 requirements for identifiers.
- [Source: _bmad-output/planning-artifacts/prd.md#Party-Discovery-Search-MVP] - Identifier search is deferred to the dedicated search capability for MVP.
- [Source: _bmad-output/planning-artifacts/architecture.md#Identifier-Management] - Identifier management architectural significance.
- [Source: _bmad-output/planning-artifacts/architecture.md#D9-MCP-Update-Strategy] - Composite update command shape and aggregate-side update semantics.
- [Source: _bmad-output/planning-artifacts/architecture.md#D10-Composite-Sub-Operation-Idempotency-and-Conflict-Detection] - Duplicate, invalid id, and conflict behavior for composite operations.
- [Source: _bmad-output/planning-artifacts/architecture.md#D17-Maximum-Composite-Payload-Size] - Composite sub-operation limit guardrail.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - EventStore aggregate, actor-host, contract, privacy, testing, and submodule guardrails.
- [Source: _bmad-output/implementation-artifacts/1-4-manage-contact-channels.md] - Previous ready story and contact-channel reconciliation pattern.
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs] - Current standalone and composite identifier implementation surface.
- [Source: src/Hexalith.Parties.Contracts/State/PartyState.cs] - Current rehydration behavior and apply-ordering guardrail.
- [Source: src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs] - Current returned update detail carrier.
- [Source: src/Hexalith.Parties/Validation/AddIdentifierValidator.cs] - Current add command boundary validation.
- [Source: src/Hexalith.Parties/Validation/UpdatePartyCompositeValidator.cs] - Current composite update boundary validation.
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateIdentifierTests.cs] - Current standalone identifier coverage.
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs] - Current composite identifier and returned-state coverage.
- [Source: tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs] - Current state apply coverage.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-05-15: Story created by BMAD pre-dev hardening automation with current identifier reconciliation context.
