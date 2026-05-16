# Story 1.7: Idempotent Commands and Typed Rejections

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer integrating with Parties,
I want duplicate or invalid commands to return stable typed outcomes,
so that retries are safe and failures are understandable without inspecting internal service state.

## Acceptance Criteria

1. **Equivalent retries do not create duplicate side effects**
   - Given a command has already succeeded and the same logical command is retried with the same aggregate identity and equivalent payload,
   - When the aggregate handles the retry against the resulting state,
   - Then command handling does not emit duplicate success events,
   - And the outcome is stable and retry-safe for the supported command family,
   - And "equivalent retry" means the same business command intent and aggregate target, independent of fresh EventStore `MessageId` / `CorrelationId` transport metadata,
   - And "stable outcome" means the same success/no-op or typed rejection classification plus a privacy-safe reason, not byte-identical envelope metadata,
   - And this story does not add a new `IdempotencyKey` property to domain commands unless an accepted EventStore contract explicitly requires it.

2. **State conflicts emit only typed rejection events**
   - Given a command conflicts with the current aggregate state, such as invalid party creation, invalid type-specific update, missing contact channel, missing identifier, missing party, erasure, restriction, or invalid lifecycle transition,
   - When the command is handled,
   - Then the aggregate emits the appropriate typed rejection event,
   - And no success event for that command appears later in the same result event list,
   - And rejection-only evidence is asserted for direct and composite command paths that this story touches.

3. **Rejection replay remains state-safe**
   - Given typed rejection events are persisted and replayed during aggregate rehydration,
   - When `PartyState.Apply(...)` handles those historical rejection events,
   - Then rejection applies remain no-op handlers,
   - And they do not mutate successful party state,
   - And the EventStore suffix-match ordering guardrail remains protected.

4. **Returned failure outcomes are stable and privacy-safe**
   - Given an invalid command is rejected,
   - When the result is mapped toward external responses, client exceptions, or composite operation outcomes,
   - Then the rejection exposes a stable rejection type/code and human-readable message suitable for external error mapping,
   - And the response does not expose personal data, raw contact values, raw identifier values, authorization data, payloads, tokens, or infrastructure secrets.

5. **Focused tests cover retry, rejection, and replay paths**
   - Given the aggregate and contract test suites run,
   - When duplicate commands, invalid transitions, missing targets, composite conflicts, and rejection replay are exercised,
   - Then tests prove duplicate commands do not create duplicate success events,
   - And tests prove persisted rejection events do not corrupt party state,
   - And tests pin the accepted current semantics instead of silently changing command contracts.

## Tasks / Subtasks

- [ ] Task 1: Inventory current idempotency and rejection contracts (AC: 1, 2, 4)
  - [ ] Confirm direct command handlers in `PartyAggregate` already return `DomainResult.NoOp()` for existing duplicate semantics: duplicate create, same natural-person classification, duplicate add contact channel, duplicate add identifier, duplicate consent, duplicate restriction, and duplicate lifecycle state where accepted by Story 1.6.
  - [ ] Confirm composite handlers return `CompositeCommandResult` with `Applied`, `Skipped`, `Rejected`, and `UpdatedPartyDetail` only on accepted update paths.
  - [ ] Treat EventStore command-envelope `MessageId` / `CorrelationId` as platform transport metadata. Do not add `IdempotencyKey`, `MessageId`, `CorrelationId`, or tenant metadata to domain command records unless an accepted EventStore architecture decision requires it.
  - [ ] Keep domain command/event contracts additive and dependency-light; do not add Dapr, MediatR, FluentValidation, ProblemDetails, HTTP, UI, or infrastructure dependencies to `Hexalith.Parties.Contracts`.

- [ ] Task 2: Reconcile direct aggregate retry behavior (AC: 1, 2, 5)
  - [ ] Confirm `CreateParty` over existing state returns no events and preserves existing state.
  - [ ] Confirm duplicate add-contact and add-identifier commands return no events and do not mutate state.
  - [ ] Confirm duplicate lifecycle commands follow the accepted Story 1.6 behavior: existing inactive deactivate and existing active reactivate are retry-safe no-ops, while missing parties reject with `PartyNotFound`.
  - [ ] Confirm erased or processing-restricted parties reject before any duplicate no-op or success semantics for mutation commands.
  - [ ] Confirm retrying the same invalid command returns the same typed rejection classification and privacy-safe reason without adding success events or domain idempotency metadata.
  - [ ] If a direct command currently emits a state-specific rejection where Story 1.6 or this story requires `PartyNotFound`, patch the narrow handler and tests only.

- [ ] Task 3: Reconcile typed rejection event evidence (AC: 2, 3, 4, 5)
  - [ ] Confirm invalid create paths emit only `PartyCannotBeCreatedWithInvalidId`, `PartyCannotBeCreatedWithoutType`, `PartyCannotBeCreatedWithoutPersonDetails`, or `PartyCannotBeCreatedWithoutOrganizationDetails`.
  - [ ] Confirm missing update targets emit only the current typed rejection: `PartyNotFound`, `PartyTypeMismatch`, `ContactChannelNotFound`, `IdentifierNotFound`, `PartyErasureInProgress`, `PartyProcessingRestricted`, `PartyNotRestricted`, `ConsentNotFound`, `InvalidConsentPurpose`, or `CompositeOperationConflict` as appropriate.
  - [ ] Assert rejection event lists do not also contain success events such as `PartyCreated`, `PersonDetailsUpdated`, `OrganizationDetailsUpdated`, `ContactChannelAdded`, `ContactChannelUpdated`, `ContactChannelRemoved`, `IdentifierAdded`, `IdentifierRemoved`, `PartyDeactivated`, or `PartyReactivated`.
  - [ ] For composite rejection paths, assert the full `CompositeCommandResult` shape: `Applied`, `Skipped`, `Rejected`, emitted event order, and no success event after the first rejection.
  - [ ] Preserve existing rejection events as event-stream payloads, not exceptions-only behavior.
  - [ ] Do not delete legacy rejection event types just because current duplicate semantics are no-op; retained event contracts protect replay and compatibility.

- [ ] Task 4: Protect rejection replay and EventStore apply ordering (AC: 3, 5)
  - [ ] Confirm every persisted rejection event that can appear in a Parties stream has a no-op `PartyState.Apply(...)` overload when the EventStore rehydrator requires it.
  - [ ] Keep all rejection `Apply(...)` overloads before success-event applies in `PartyState`.
  - [ ] Extend `PartyStateApplyOrderingFitnessTests` only when a new suffix-collision rejection event is introduced.
  - [ ] Extend `PartyStateRejectionApplyEndToEndTests` for any newly identified replay-sensitive rejection event.
  - [ ] Do not reorder `PartyState.Apply(...)` methods through formatting, alphabetization, or broad cleanup.

- [ ] Task 5: Reconcile external failure mapping without broad API work (AC: 4)
  - [ ] Audit the current client and gateway behavior that maps command failures to typed client errors or ProblemDetails-compatible responses before changing public surfaces.
  - [ ] If an accepted mapping already exists, prove rejection type/code stability with focused tests.
  - [ ] If no accepted type URI/corrective-action catalog exists for FR30, record the gap as deferred instead of inventing a broad error catalog, REST controller, OpenAPI surface, or new public API in this story.
  - [ ] Keep rejection messages privacy-safe. Do not include raw person names, contact values, identifier values, serialized payloads, authorization headers, tokens, sidecar details, connection strings, or infrastructure exception text in rejection messages, composite outcome strings, logs, telemetry dimensions, test names, or assertion failure messages.
  - [ ] Use absence assertions for raw names, contact values, identifier values, payload text, and infrastructure secret markers in any touched failure mapping tests.

- [ ] Task 6: Run focused validation (AC: 1, 2, 3, 4, 5)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateCreateTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateContactChannelTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateIdentifierTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateLifecycleTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateCompositeTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateRejectionApplyEndToEndTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~HttpPartiesCommandClientTests` if client failure mapping is touched.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if implementation changes public contracts, validators, client abstractions, EventStore-facing surfaces, or shared error mapping.

## Dev Notes

### Current Implementation Context

- This is a reconciliation story over an already-evolved codebase. Start by proving current behavior, then patch only concrete gaps.
- Current command contracts do not expose a domain-level idempotency key. `HttpPartiesCommandClient` creates EventStore command-envelope `MessageId` and `CorrelationId` values per request. Do not move those transport fields into domain commands without an accepted EventStore contract decision.
- Direct aggregate handlers already implement several state-derived retry-safe no-ops through `DomainResult.NoOp()`.
- `CreateParty` over existing state returns no-op. `CreatePartyComposite` over existing state returns an empty `CompositeCommandResult`.
- `AddContactChannel` and `AddIdentifier` skip existing IDs as no-ops. `UpdatePartyComposite` reports duplicate add/remove/update operations through `Skipped` and rejects same-id conflicts through `CompositeOperationConflict`.
- Existing lifecycle handlers currently show a known pre-Story-1.6 gap in source: null-state deactivate/reactivate return lifecycle-state rejections. Story 1.6 already requires `PartyNotFound`; treat this story as the global typed-rejection consistency pass if that gap is still present when development starts.
- Rejection events are persisted stream events and must remain replay-compatible. `PartyState` contains no-op rejection `Apply(...)` overloads before success applies because EventStore resolves event types by short-name suffix matching.

### Architecture Patterns and Constraints

- Domain behavior belongs in pure static `PartyAggregate.Handle(Command, PartyState?)` methods returning `DomainResult` for simple commands and `CompositeCommandResult` for composite commands.
- State mutation belongs in `PartyState.Apply(Event)`. Rejection applies must remain no-op.
- Composite command semantics follow D10: skip duplicate additions, reject invalid update/remove IDs, reject conflicting operations on the same entity ID, and return applied/skipped/rejected operation evidence.
- D12 says composite operations are atomic from the aggregate turn perspective. Do not emit partial success events after a rejection path.
- EventStore-owned gateways handle public request authorization and transport metadata. Parties owns domain behavior and projection-side access checks; this story does not move tenant/RBAC or idempotency storage into the aggregate.
- The main `src/Hexalith.Parties` project is an actor host. Do not reintroduce public REST controllers, Swagger/OpenAPI, or in-process MCP tools.
- Keep `Hexalith.Parties.Contracts` stable, additive, and free of hosting/infrastructure dependencies.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Contracts/
  Commands/CreateParty.cs
  Commands/CreatePartyComposite.cs
  Commands/UpdatePartyComposite.cs
  Commands/AddContactChannel.cs
  Commands/UpdateContactChannel.cs
  Commands/RemoveContactChannel.cs
  Commands/AddIdentifier.cs
  Commands/RemoveIdentifier.cs
  Commands/DeactivateParty.cs
  Commands/ReactivateParty.cs
  Events/*NotFound.cs
  Events/PartyCannot*.cs
  Events/PartyTypeMismatch.cs
  Events/PartyErasureInProgress.cs
  Events/PartyProcessingRestricted.cs
  Events/CompositeOperationConflict.cs
  Results/CompositeCommandResult.cs
  State/PartyState.cs

src/Hexalith.Parties.Server/
  Aggregates/PartyAggregate.cs

src/Hexalith.Parties.Client/
  HttpPartiesCommandClient.cs
  PartiesClientException.cs

src/Hexalith.Parties/
  Domain/PartyDomainServiceInvoker.cs
  ErrorHandling/PartiesValidationExceptionHandler.cs
  ErrorHandling/PartiesGlobalExceptionHandler.cs
```

### Previous Story Intelligence

- Story 1.2 pinned the rule that aggregate identity stays in EventStore metadata; do not add `PartyId` to success events casually.
- Stories 1.3 through 1.5 reinforced direct aggregate `DomainResult` behavior versus composite returned-state evidence through `CompositeCommandResult.UpdatedPartyDetail`.
- Story 1.5 deferred active/deactivated-party identifier policy to lifecycle work rather than inventing a new identifier rejection event.
- Story 1.6 owns lifecycle soft-deactivation semantics and missing-party lifecycle behavior. Reuse its decisions instead of creating a parallel lifecycle policy here.
- Recent hardening runs favor audit-first, low-risk reconciliation stories with focused tests and explicit deferred decisions for public contract expansion.

### Testing Requirements

- Use xUnit v3 and Shouldly patterns already present in the repository.
- Prefer `PartyTestData` helpers for valid create, update, contact channel, identifier, lifecycle, erasure, and restriction states.
- For direct handlers, assert exact event counts and exact rejection types so a rejection cannot be followed by a success event.
- For no-op retries, assert `IsNoOp`, empty event lists, and unchanged state where the state object is available.
- For composite handlers, assert `Applied`, `Skipped`, `Rejected`, emitted event order, and `UpdatedPartyDetail` only for accepted update response paths.
- For privacy-safe evidence, use synthetic placeholder data and assert absence of raw person names, contact values, identifier values, payload text, and infrastructure secrets from public failure strings.
- If any `PartyState.Apply(...)` declarations move or new rejection apply methods are added, run both apply-ordering and rejection-apply end-to-end fitness tests.

### Anti-Patterns To Avoid

- Do not add a domain-level idempotency-key property without an accepted EventStore contract decision.
- Do not implement a Parties-side idempotency repository, cache, table, Dapr state store, retry store, or message ledger in this story.
- Do not turn rejection events into exceptions-only behavior.
- Do not emit success events after typed rejection events in a single command result.
- Do not broaden standalone `DomainResult` into `CompositeCommandResult` only to expose richer failure details.
- Do not add REST controllers, Swagger/OpenAPI, MCP hosting, AdminPortal UI, Picker UI, projections, search, samples, or deployment topology work.
- Do not leak personal data or infrastructure details through rejection messages, composite outcomes, logs, telemetry, exception detail, test names, or assertion messages.
- Do not reorder `PartyState.Apply(...)` methods or remove no-op rejection applies.
- Do not initialize nested submodules recursively.

### Deferred Decisions

- A complete FR30 external error catalog with type URI, corrective action text, status mapping, localization, and documentation remains a product/API decision unless an accepted source already defines it.
- End-to-end duplicate command deduplication by command-envelope `MessageId` belongs to Hexalith.EventStore/gateway behavior unless an accepted architecture decision assigns a Parties-owned surface.
- Whether duplicate lifecycle transitions should always no-op or sometimes emit typed lifecycle-state rejections is governed by Story 1.6 and future product decisions, not by an ad hoc policy change in this story.
- Byte-identical retry response metadata is not required by this story; only stable domain classification and privacy-safe failure reasons are in scope.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.7] - Story statement and BDD acceptance criteria for FR30 and FR36.
- [Source: _bmad-output/planning-artifacts/prd.md#Functional-Requirements] - FR30 typed rejection responses, FR36 idempotent duplicate handling, and FR69 updated-state boundaries.
- [Source: _bmad-output/planning-artifacts/prd.md#Non-Functional-Requirements] - NFR24 idempotent command handling.
- [Source: _bmad-output/planning-artifacts/architecture.md#D10-Composite-Sub-Operation-Idempotency-and-Conflict-Detection] - Duplicate skip, invalid ID rejection, conflict detection, and outcome reporting.
- [Source: _bmad-output/planning-artifacts/architecture.md#D12-Partial-Failure-Eliminated-by-Design] - Composite aggregate turn atomicity guidance.
- [Source: _bmad-output/planning-artifacts/architecture.md#D17-Maximum-Composite-Payload-Size] - Composite payload guardrail.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - EventStore aggregate, actor-host, contract, privacy, testing, and submodule guardrails.
- [Source: _bmad-output/process-notes/story-creation-lessons.md#L08-Party-Review-vs-Elicitation] - Trace semantics for later party-mode and elicitation passes.
- [Source: _bmad-output/implementation-artifacts/1-6-deactivate-and-reactivate-parties.md] - Previous lifecycle story and missing-party/duplicate lifecycle decisions.
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs] - Current direct and composite retry/rejection implementation surface.
- [Source: src/Hexalith.Parties.Contracts/State/PartyState.cs] - Current rejection apply and EventStore suffix-ordering guardrail.
- [Source: src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs] - Current applied/skipped/rejected outcome carrier.
- [Source: src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs] - Current EventStore command-envelope `MessageId` and `CorrelationId` construction.
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCreateTests.cs] - Current duplicate create and typed rejection coverage.
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs] - Current composite duplicate, conflict, and rejection coverage.
- [Source: tests/Hexalith.Parties.Tests/FitnessTests/PartyStateApplyOrderingFitnessTests.cs] - Rejection apply ordering fitness coverage.
- [Source: tests/Hexalith.Parties.Tests/FitnessTests/PartyStateRejectionApplyEndToEndTests.cs] - Runtime rejection apply no-op coverage.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-05-16: Party-mode review applied pre-dev clarifications for equivalent retry semantics, stable outcome wording, invalid-command retry evidence, composite rejection result-shape assertions, privacy-safe absence assertions, and deferred byte-identical metadata decisions.
- 2026-05-16: Story created by BMAD pre-dev hardening automation with current idempotency, typed rejection, replay, and privacy-safe error-mapping reconciliation context.

## Party-Mode Review

- Date/time: 2026-05-16T12:24:38+02:00
- Selected story key: `1-7-idempotent-commands-and-typed-rejections`
- Command/skill invocation used: `/bmad-party-mode 1-7-idempotent-commands-and-typed-rejections; review;`
- Participating BMAD agents:
  - Winston (System Architect)
  - Amelia (Senior Software Engineer)
  - Murat (Master Test Architect and Quality Advisor)
  - John (Product Manager)
- Findings summary:
  - All reviewers recommended `ready-for-dev`.
  - The story is ready if it remains scoped to deterministic aggregate behavior, persisted typed rejection events, replay safety, and narrow client/gateway failure mapping.
  - Equivalent retry needed explicit wording as same business command intent and aggregate target, not byte-identical EventStore envelope metadata.
  - Composite rejection evidence needed full result-shape assertions and proof that no success event appears after the first rejection.
  - Test evidence should include retrying the same invalid command and privacy-safe absence assertions for failure strings.
- Changes applied:
  - Clarified equivalent retry and stable outcome semantics in AC1.
  - Added invalid-command retry evidence to Task 2.
  - Added composite `CompositeCommandResult` shape and no-success-after-rejection evidence to Task 3.
  - Added privacy-safe absence assertions to Task 5.
  - Recorded byte-identical retry response metadata as deferred/out of scope.
- Findings deferred:
  - Domain-level idempotency keys, Parties-owned idempotency stores, and EventStore command-envelope deduplication remain outside this story.
  - Complete FR30 external error catalog, type URI, corrective-action text, status mapping, localization, and documentation remain deferred unless already accepted by existing contracts.
  - Lifecycle duplicate transition policy remains governed by Story 1.6 and future product decisions.
- Final recommendation: ready-for-dev
