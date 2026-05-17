# Story 1.5: Manage Party Identifiers

Status: review

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
   - Then the aggregate idempotently skips the duplicate according to the documented command idempotency rules,
   - And no additional `IdentifierAdded` success event is emitted for that duplicate,
   - And the party state does not contain duplicate identifier entries.

4. **Invalid identifier mutations reject without success events**
   - Given an identifier command targets a missing party, missing identifier, invalid identifier payload, erased party, or processing-restricted party,
   - When the command is handled,
   - Then the aggregate emits the current documented typed rejection event,
   - And no successful identifier event is emitted,
   - And rejection-only evidence proves the event list does not also contain `IdentifierAdded` or `IdentifierRemoved` after the rejection.

5. **Returned update state includes applied identifier changes**
   - Given an identifier mutation succeeds through the current update response path,
   - When the command result is returned,
   - Then the response includes the updated party state,
   - And the returned state includes the applied identifier addition or removal.

6. **Identifier values remain privacy-safe**
   - Given identifier values may identify natural persons or sensitive business records,
   - When commands, events, state, operation outcomes, tests, logs, or telemetry are reviewed,
   - Then identifier values are marked and handled as personal data where applicable,
   - And raw identifier values are not placed in logs, telemetry dimensions, exception text, typed rejection text, diagnostics, applied/skipped/rejected operation strings, assertion display names, or test output names.

## Tasks / Subtasks

- [x] Task 1: Audit existing identifier contracts, validators, and payload representation (AC: 1, 2, 3, 4, 6)
  - [x] Confirm `AddIdentifier`, `RemoveIdentifier`, `CreatePartyComposite`, `UpdatePartyComposite`, `IdentifierAdded`, `IdentifierRemoved`, `IdentifierNotFound`, `PartyCannotAddDuplicateIdentifier`, `PartyIdentifier`, `IdentifierType`, and `PartyState` match this story's scope.
  - [x] Confirm the current MVP command/event representation is identifier id, `IdentifierType`, and `[PersonalData] string Value`; do not add broad jurisdiction metadata to commands/events unless an explicit architecture decision updates the public contract.
  - [x] Treat "jurisdiction-specific identifiers" in this MVP story as `IdentifierType` plus value semantics; persisted jurisdiction remains a deferred contract decision unless already accepted elsewhere.
  - [x] Review `PartyIdentifier.Jurisdiction` as current value-object inventory. Because `AddIdentifier` and `IdentifierAdded` do not currently carry jurisdiction, treat jurisdiction persistence as a deferred contract decision unless already accepted elsewhere.
  - [x] Verify `src/Hexalith.Parties/Validation/AddIdentifierValidator.cs`, `RemoveIdentifierValidator.cs`, `CreatePartyCompositeValidator.cs`, and `UpdatePartyCompositeValidator.cs` cover party id, identifier id, enum value, identifier value, and sub-operation limit constraints expected at the boundary.
  - [x] Preserve the current distinction between standalone aggregate tests that use readable identifier ids and composite validators that require GUID-shaped identifier ids unless validation and tests are intentionally reconciled together.

- [x] Task 2: Validate standalone aggregate identifier handlers (AC: 1, 2, 3, 4)
  - [x] Confirm `PartyAggregate.Handle(AddIdentifier, PartyState?)` rejects missing parties with `PartyNotFound`, guards erasure and processing restriction before success, no-ops duplicate identifier ids, and emits `IdentifierAdded` on success.
  - [x] Confirm duplicate add no-ops do not emit a second `IdentifierAdded` event and do not mutate state.
  - [x] Confirm repeated duplicate adds remain stable when replayed against already-updated state; this story should not rely on caller-side de-duplication for retry safety.
  - [x] Confirm `PartyAggregate.Handle(RemoveIdentifier, PartyState?)` rejects missing parties with `PartyNotFound`, guards erasure and processing restriction before success, rejects missing identifier ids with `IdentifierNotFound`, and emits `IdentifierRemoved` on success.
  - [x] Confirm missing remove paths emit only the typed rejection and never emit `IdentifierRemoved`.
  - [x] Confirm erasure/restriction guards take precedence over identifier-specific success behavior and do not leak the identifier value in rejection evidence.
  - [x] Audit the "active party" wording in the ACs against current deactivated-party policy. If no accepted rejection contract exists for inactive-party identifier mutations, record the policy gap as deferred to the lifecycle story instead of inventing a new rejection event in this story.
  - [x] Do not add identifier search, duplicate detection by identifier value, normalization, or jurisdiction-specific validation services to this story; MVP search by identifier is deferred to the dedicated search capability.

- [x] Task 3: Validate state rehydration and privacy metadata (AC: 1, 2, 6)
  - [x] Confirm `PartyState.Apply(IdentifierAdded)` appends an identifier with id, type, and value.
  - [x] Confirm `PartyState.Apply(IdentifierRemoved)` removes only the targeted identifier id and tolerates unknown ids defensively.
  - [x] Confirm `PartyIdentifier.Value`, `AddIdentifier.Value`, and `IdentifierAdded.Value` remain marked with `[PersonalData]`.
  - [x] Preserve the no-op rejection `Apply(...)` methods before success applies; EventStore suffix-based rehydration depends on that ordering.

- [x] Task 4: Reconcile composite command behavior and FR69 returned state (AC: 1, 2, 3, 4, 5, 6)
  - [x] Confirm `CreatePartyComposite` emits `IdentifierAdded` events for initial identifiers and safely skips duplicate identifier ids for MCP/client retry safety.
  - [x] Confirm `UpdatePartyComposite` rejects add/remove conflicts on the same identifier id, validates missing remove targets before emitting success events, skips duplicate additions, and skips duplicate removals without mutating state twice.
  - [x] Confirm add/remove conflict evidence covers the same identifier id appearing in both lists and that the result is rejection-only for the conflicting sub-operation, not an implicit ordering policy.
  - [x] Confirm duplicate add/remove and missing identifier outcomes do not include raw identifier values in `Applied`, `Skipped`, or `Rejected` strings. Identifier ids are allowed as stable operation identifiers when they are GUID-shaped or otherwise non-PII.
  - [x] Confirm privacy-safe outcome evidence covers logs, telemetry dimensions, exception text, typed rejection text, diagnostics, and composite operation strings.
  - [x] Confirm `CompositeCommandResult.UpdatedPartyDetail` is built from current state plus emitted events and includes added and removed identifiers in the same returned response that reports the operation outcomes.
  - [x] Do not change simple `DomainResult` return types for standalone handlers; public "updated state" response evidence for this story is the composite update path unless the architecture explicitly changes the response contract.

- [x] Task 5: Preserve architecture, scope, and boundary constraints (AC: 1, 2, 3, 4, 5, 6)
  - [x] Keep domain behavior in pure static aggregate `Handle` methods and `PartyState.Apply(...)`; do not add Dapr, database, MediatR handler, controller, Swagger/OpenAPI, MCP tool, AdminPortal, Picker, projection, search, or sample work to this story.
  - [x] Keep `Hexalith.Parties.Contracts` additive and dependency-light. Do not add hosting, Dapr, MediatR, FluentValidation, UI, or infrastructure dependencies to contracts.
  - [x] Keep tenant context out of identifier commands/events for this story; aggregate identity is `PartyId`, and tenant authorization remains outside the aggregate.
  - [x] Treat "authorized client" as an upstream gateway/client precondition; do not add tenant/RBAC enforcement to the Parties aggregate, contracts, or validation in this story.
  - [x] Do not add identifier-value normalization, country-specific checksum validation, or duplicate matching by value unless a separate architecture/product decision explicitly schedules it.
  - [x] Do not broaden this story into lifecycle, contact-channel, GDPR erasure, read projection, REST, MCP, admin, or picker behavior.

- [x] Task 6: Run focused validation (AC: 1, 2, 3, 4, 5, 6)
  - [x] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateIdentifierTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateCompositeTests` if composite add/remove/skipped/rejected or returned-state behavior is inspected or changed.
  - [x] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests` if any `PartyState.Apply` declarations move.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if implementation changes touch public contracts, validators, project references, or EventStore-facing surfaces.

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
- Treat composite operation ordering as explicit evidence, not an assumption. If a command includes both add and remove for the same identifier id, the conflict rejection path is the accepted behavior for this story; do not reinterpret the payload as add-then-remove or remove-then-add.
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
- Include explicit evidence for duplicate add no-op/no-success-event behavior, missing remove typed rejection/no-success-event behavior, composite add/remove conflicts, invalid remove rejection, returned `UpdatedPartyDetail` after add/remove, and privacy-safe rejection/outcome strings.
- Keep privacy tests assertion-focused: use synthetic placeholder values and assert absence of raw values from outcomes without adding real legal, tax, registry, or national identifiers to test names or diagnostics.
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

GPT-5 Codex

### Debug Log References

- 2026-05-17T12:58:14+02:00 - `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateIdentifierTests` - Passed 13/13.
- 2026-05-17T12:58:14+02:00 - `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateCompositeTests` - Passed 53/53.
- 2026-05-17T12:58:14+02:00 - `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~IdentifierValidatorTests` - Passed 7/7.
- 2026-05-17T12:58:14+02:00 - `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateTests` - Passed 24/24.
- 2026-05-17T12:58:14+02:00 - `dotnet build Hexalith.Parties.slnx --configuration Release` - Passed with 0 warnings and 0 errors.
- 2026-05-17T12:58:14+02:00 - `dotnet test Hexalith.Parties.slnx --configuration Release --no-build` - One timing-sensitive unrelated performance benchmark failed once; all story-adjacent assemblies passed.
- 2026-05-17T12:58:14+02:00 - `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~SemanticSearchPerformanceBenchmarkTests.Search_100KEntries_ExactMatch_CompletesWithin500ms` - Passed 1/1 on rerun.
- 2026-05-17T12:58:14+02:00 - `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --no-build` - Passed 266/266.

### Completion Notes List

- Audited identifier contracts, validators, state application, standalone aggregate handlers, and composite create/update behavior against Story 1.5 scope.
- Added missing regression evidence for remove-identifier erasure/restriction rejection-only behavior.
- Added composite update duplicate-add payload coverage to prove one `IdentifierAdded`, one privacy-safe skip, and one returned-state identifier entry.
- Added identifier validator tests documenting the accepted distinction between standalone readable identifier ids and composite GUID-shaped identifier ids.
- Kept implementation scope to tests and story tracking; no production contracts, aggregate behavior, validators, projections, REST/MCP/UI, or search code changed.

### File List

- `_bmad-output/implementation-artifacts/1-5-manage-party-identifiers.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs`
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateIdentifierTests.cs`
- `tests/Hexalith.Parties.Tests/Validation/IdentifierValidatorTests.cs`

## Party-Mode Review

- Date/time: 2026-05-15T19:33:18+02:00
- Selected story key: `1-5-manage-party-identifiers`
- Command/skill invocation used: `/bmad-party-mode 1-5-manage-party-identifiers; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - Winston and Amelia considered the story ready after minor inline clarification; Murat and John recommended `needs-story-update` until duplicate, privacy, jurisdiction, and composite evidence expectations were sharper.
  - The common risk was decision drift, not a blocker: reviewers wanted the MVP phrase "jurisdiction-specific identifiers" tied to `IdentifierType` plus value semantics while persisted jurisdiction remains deferred.
  - Reviewers also aligned on explicit no-success-event evidence for duplicate add and missing remove paths, privacy-safe rejection/outcome text, `CompositeCommandResult.UpdatedPartyDetail` as the FR69 evidence surface, and the existing distinction between aggregate-readable ids and composite GUID-shaped ids.
  - The review reaffirmed that tenant/RBAC, lifecycle policy, REST/OpenAPI/MCP hosting, UI, projections, search, normalization, and jurisdiction persistence must stay outside this story unless accepted elsewhere.
- Changes applied:
  - Clarified duplicate add as retry-safe no-op/skip behavior with no additional `IdentifierAdded` event and no duplicate state entry.
  - Clarified missing remove and invalid mutation expectations so typed rejection paths emit no success identifier event.
  - Added concrete privacy-safe evidence requirements for `[PersonalData]` inventory and absence of raw identifier values in rejection, diagnostic, telemetry, and composite operation strings.
  - Clarified that MVP jurisdiction specificity is represented by `IdentifierType` plus value semantics and that persisted jurisdiction remains deferred.
  - Clarified that authorization is an upstream precondition and this story does not add tenant/RBAC enforcement to aggregate, contracts, or validators.
  - Added focused test evidence expectations for duplicate no-op, missing remove, composite conflicts, invalid remove, returned updated detail, and privacy-safe outcomes.
- Findings deferred:
  - Persisted jurisdiction modeling remains deferred unless an accepted product/architecture decision changes the public contract.
  - Active/deactivated-party identifier policy remains deferred to lifecycle Story 1.6.
  - Projection/search/UI/API exposure, tenant/RBAC enforcement, identifier normalization/checksum validation, and duplicate matching by identifier value remain out of scope.
  - Reconciling readable aggregate test ids with GUID-shaped composite validation remains a deliberate future decision unless implementation finds accepted guidance elsewhere.
- Final recommendation: ready-for-dev

## Change Log

- 2026-05-17: Implemented Story 1.5 audit/reconciliation guardrail tests for identifier validators, remove-identifier rejection-only behavior, and composite duplicate-add privacy-safe outcomes; focused tests and release build passed.
- 2026-05-15: Party-mode review applied pre-dev clarifications for duplicate no-op/no-success-event evidence, missing-remove rejection behavior, privacy-safe outcome checks, MVP jurisdiction scope, upstream authorization boundary, composite returned-state assertions, and deferred lifecycle/jurisdiction/search/API decisions.
- 2026-05-15: Story created by BMAD pre-dev hardening automation with current identifier reconciliation context.

## Advanced Elicitation

- Date/time: 2026-05-15T23:11:00+02:00
- Selected story key: `1-5-manage-party-identifiers`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-5-manage-party-identifiers`
- Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Security Audit Personas; Self-Consistency Validation; Architecture Decision Records
- Reshuffled Batch 2 method names: Pre-mortem Analysis; Chaos Monkey Scenarios; User Persona Focus Group; Critique and Refine; Expand or Contract for Audience
- Findings summary:
  - The story was already narrow and ready, but duplicate/retry, rejection-only, conflict, and privacy evidence needed sharper implementation-facing wording before development.
  - The highest-risk ambiguity was treating composite add/remove ordering as implicit behavior instead of asserting the accepted `CompositeOperationConflict` path.
  - Privacy review reinforced that raw identifier values can leak through test names and diagnostics as easily as through production outcome strings.
  - Self-consistency review reaffirmed that FR69 returned-state proof belongs to `CompositeCommandResult.UpdatedPartyDetail`; standalone `DomainResult` contracts should not be broadened for this story.
- Changes applied:
  - Strengthened invalid-mutation AC evidence to require rejection-only event lists without trailing identifier success events.
  - Added repeated duplicate-add replay stability and erasure/restriction guard precedence checks.
  - Clarified composite same-id add/remove conflicts as rejection-only evidence, not an add/remove ordering policy.
  - Tightened returned-state wording to require added/removed identifiers in the same composite response that reports outcomes.
  - Extended privacy guidance to assertion display names, test output names, and synthetic placeholder test values.
- Findings deferred:
  - Jurisdiction persistence, identifier normalization/checksum validation, duplicate matching by value, identifier search, and tenant/RBAC enforcement remain outside this story.
  - Any public contract change that broadens standalone handler return types or adds identifier metadata remains deferred to a separate accepted architecture/product decision.
- Final recommendation: ready-for-dev
