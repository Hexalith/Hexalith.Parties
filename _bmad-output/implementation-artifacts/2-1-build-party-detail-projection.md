# Story 2.1: Build Party Detail Projection

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a consumer of party data,
I want each party to have a read-optimized detail projection,
so that I can retrieve complete party details without rehydrating the aggregate on every query.

## Acceptance Criteria

1. **Lifecycle and detail events build complete party detail**
   - Given party lifecycle, detail, contact channel, identifier, and lifecycle events are published for a party,
   - When the projection handler processes those events in order,
   - Then it builds a party detail projection containing party id, type, active status, person or organization details, display/sort names, contact channels, identifiers, and relevant timestamps.

2. **Rejection events never mutate successful projection state**
   - Given a party detail projection handler receives a rejection event,
   - When the event is applied during normal processing or rebuild,
   - Then the handler does not mutate the successful party detail state,
   - And projection replay remains compatible with persisted rejection events.
   - And replayed rejection events do not advance projection timestamps, version-like fields, or checkpoint-derived detail fields.
   - And a handler no-op result is interpreted as "preserve current projection state", never as "clear the stored projection".

3. **Actor persistence uses documented Dapr state-key convention**
   - Given a party detail projection is persisted through its actor wrapper,
   - When the actor stores projection state,
   - Then the Dapr actor state key uses the documented `{tenant}:party-detail:{partyId}` convention,
   - And the handler logic remains free of Dapr dependencies.

4. **Duplicate and repeated event delivery is idempotent where possible**
   - Given duplicate or late repeated events are delivered by pub/sub,
   - When the projection handler processes them,
   - Then processing is idempotent where event identity or sequence data allows,
   - And duplicate delivery does not create duplicate contact channels or identifiers.
   - And collection idempotency is defined by stable event item ids (`ChannelId`, `IdentifierId`) rather than display values, labels, or case-normalized personal data.
   - And duplicate or no-effective-change events do not refresh `LastModifiedAt` unless a real projection field mutation is persisted.

5. **Pure handler tests verify representative event replay**
   - Given projection handler tests run without Dapr infrastructure,
   - When they replay representative party event sequences,
   - Then they verify detail state after create, update, contact channel, identifier, deactivate, and reactivate flows,
   - And they verify event ordering assumptions documented by the architecture.

## Tasks / Subtasks

- [x] Task 1: Audit and reuse the existing projection implementation before editing (AC: 1, 3, 4)
  - [x] Start from `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs`; do not create a second projection handler or parallel projection project.
  - [x] Inspect `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs`, `src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs`, and `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`.
  - [x] Confirm the current behavior against this story before changing it: the handler already covers create, display name, person/organization detail updates, contact channels, identifiers, lifecycle, consent, restriction, erasure, idempotent add-by-id, and no-op unknown events.
  - [x] Treat existing names, project references, and actor key format as established patterns unless an acceptance criterion proves they are wrong.

- [x] Task 2: Complete party-detail event mapping and no-op semantics (AC: 1, 2, 4)
  - [x] Ensure `PartyCreated` initializes `PartyDetail.Id`, `Type`, `IsActive`, person/organization details, display/sort names, empty collections, `CreatedAt`, and `LastModifiedAt`.
  - [x] Ensure `PartyDisplayNameDerived`, `PersonDetailsUpdated`, `OrganizationDetailsUpdated`, contact channel events, identifier events, `PartyDeactivated`, and `PartyReactivated` update only the relevant fields while preserving unrelated state.
  - [x] Keep rejection events as projection no-ops. Add explicit coverage for representative rejection events such as `PartyCannotBeCreatedWithInvalidId`, `PartyCannotAddDuplicateChannel`, `PartyCannotAddDuplicateIdentifier`, `PartyCannotBeDeactivatedWhenInactive`, and `PartyCannotBeReactivatedWhenActive`.
  - [x] Cover rejection events both before and after successful events so replay proves null state stays null, existing successful state stays unchanged, and timestamps are not advanced by rejection-only inputs.
  - [x] Treat a `null` result from `PartyDetailProjectionHandler.Apply(...)` as a no-mutation signal. The actor/replay path must preserve any existing stored detail instead of overwriting, clearing, or timestamp-refreshing it.
  - [x] Preserve idempotent add semantics for contact channels and identifiers by stable item ids (`ChannelId`, `IdentifierId`). Duplicate add delivery must not append duplicate list entries; scalar update/detail/lifecycle events may remain last-event-wins according to aggregate replay order.
  - [x] Prove duplicate contact, identifier, and consent adds with the same stable id do not replace the existing item, do not append a second item, and do not advance `LastModifiedAt`.
  - [x] Prove missing-item update/remove events and no-effective-change updates are no-ops that leave fields and timestamps unchanged.
  - [x] Treat update/contact/identifier events that arrive before `PartyCreated` as no-ops unless existing code already has a stricter corruption path; do not synthesize partial party detail state from child events.
  - [x] Do not use exceptions for domain rejections and do not delete or special-case persisted rejection events out of replay.

- [x] Task 3: Validate actor wrapper persistence, replay, and key safety (AC: 3, 4)
  - [x] Keep Dapr dependencies in the actor wrapper only; `PartyDetailProjectionHandler` must remain free of `Dapr.*` references.
  - [x] Confirm `PartyDetailProjectionActor.ResolveStateContext(...)` derives the state key from actor id `{tenant}:party-detail:{partyId}` and rejects malformed projection segments or party-id mismatches.
  - [x] Test malformed actor ids and party-id mismatches as separate failure cases. They must fail fast with metadata-only diagnostics and no Dapr state write or checkpoint advancement.
  - [x] Confirm `HandleSerializedEventAsync(...)` resolves event type names only through `PartyEventTypeResolver` and does not reintroduce `Type.GetType` or arbitrary assembly loading.
  - [x] Preserve sequence checkpoint behavior so replay-from-zero skips already applied events, while idempotent collection handlers still protect against state/checkpoint divergence on non-transactional state stores.
  - [x] Distinguish accepted no-op events from invalid/dropped events in tests: accepted events that deserialize and dispatch may advance the checkpoint even when the handler returns no state mutation, while invalid event names, unsupported formats, and unreadable payload policies must follow the existing actor behavior explicitly.
  - [x] When accepted no-op events advance the checkpoint, assert no party detail state write occurs and no user-facing freshness/query contract is invented by this story.
  - [x] Keep actor logs bounded to metadata such as party id, event type name, sequence, projection name, tenant id, and correlation-safe context. Do not log contact values, identifiers, display names, serialized details, or raw event payloads.

- [x] Task 4: Clarify timestamp behavior without inventing unsupported event metadata (AC: 1)
  - [x] Verify whether Parties event payloads expose event-time metadata through the payload or envelope available to the projection actor.
  - [x] Use this precedence for projection timestamps: existing event metadata or payload timestamp fields if already available on the projection path; otherwise projection processing timestamps. Document the limitation in completion notes when processing timestamps are used.
  - [x] Do not add event-time fields to existing public event contracts unless a separate architecture decision explicitly approves an additive event schema change.
  - [x] Ensure repeated no-op events do not accidentally advance `LastModifiedAt`.
  - [x] Prefer timestamp assertions that prove unchanged values for no-ops and monotonic movement for real mutations; do not assert exact wall-clock processing times.

- [x] Task 5: Strengthen pure handler and actor tests (AC: 1, 2, 3, 4, 5)
  - [x] Extend `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs` for complete create -> derived-name -> detail update -> contact add/update/remove -> preferred channel -> identifier add/remove -> deactivate -> reactivate sequences.
  - [x] Add duplicate-delivery tests proving repeated `ContactChannelAdded` and `IdentifierAdded` do not duplicate entries, and no-op updates return no mutation.
  - [x] Add rejection-event tests proving existing successful state is preserved, null state remains null, and interleaved rejections do not mutate detail fields or timestamps.
  - [x] Add handler or actor-wrapper tests proving `null` no-op results preserve the current projection state rather than clearing it.
  - [x] Add serialized replay tests, where already supported by the actor test harness, proving accepted no-op events can advance the sequence checkpoint without writing `PartyDetail` state.
  - [x] Add or update actor tests for state-key convention, malformed actor ids, invalid tenant/party id segments, party-id mismatch, serialized event dispatch, non-JSON drop behavior, unknown/ambiguous event names, checkpoint skipping, and corruption/rebuild behavior.
  - [x] Add corruption/log-safety expectations for unreadable Dapr JSON state: fail closed, preserve rebuild behavior where already implemented, and do not emit party names, identifiers, contact values, serialized `PartyDetail`, or raw event payloads in diagnostics.
  - [x] Keep pure handler tests in `Hexalith.Parties.Projections.Tests`; keep Dapr actor/state-manager behavior in actor/integration-style tests that already use `ActorHost.CreateForTest` and NSubstitute.

- [x] Task 6: Preserve architecture fitness and package boundaries (AC: 3, 5)
  - [x] Do not add REST controllers, public minimal APIs, Swagger/OpenAPI, or MCP tools to `src/Hexalith.Parties` for this story.
  - [x] Do not move query behavior into the aggregate or server project; projections remain read-side infrastructure.
  - [x] Do not make Contracts depend on Projections, Server, Dapr, MediatR, FluentValidation, UI, or infrastructure packages.
  - [x] Do not add package versions to `.csproj`; keep versions in `Directory.Packages.props`.
  - [x] If architecture fitness tests fail, fix the boundary violation rather than weakening the test.

- [x] Task 7: Run focused validation (AC: 1, 2, 3, 4, 5)
  - [x] Run `dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionHandler`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyEventTypeResolver`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionActorCorruptionTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitecturalFitnessTests` if project references, Dapr boundaries, or handler dependencies change.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if public contracts, project references, or package configuration change.

## Dev Notes

### Current Implementation Context

- This is not a greenfield story in the current workspace. `Hexalith.Parties.Projections` already exists with `PartyDetailProjectionHandler`, `PartyDetailProjectionActor`, `IPartyDetailProjectionActor`, `PartyEventTypeResolver`, projection options, rebuild service, and projection tests. Reuse and harden these files rather than recreating them.
- `PartyDetailProjectionHandler.Apply(...)` is already pure and static. It accepts `string partyId`, `IEventPayload`, and nullable `PartyDetail` state, then returns a new state or `null` for no mutation.
- The current handler returns `null` for unknown events and for rejection events by default. That aligns with AC2, but this story needs explicit tests so future changes do not accidentally mutate detail state during replay.
- Existing add handlers deduplicate contact channels, identifiers, and consent records by ids. Preserve that behavior; it is the replay-safety layer that complements actor sequence checkpoints.
- `PartyDetailProjectionActor` currently stores detail state under `{tenant}:party-detail:{partyId}`, stores a companion `{stateKey}:last-sequence` checkpoint, caches last known detail during rebuild/degraded reads, and supports serialized event dispatch.
- The actor currently accepts `json` and `json-redacted` payload formats, advances checkpoints for dropped redacted/un-deserializable events, and schedules rebuild through a persistent reminder after state corruption. Keep this behavior unless a test proves it violates this story.
- `PartyEventTypeResolver` intentionally resolves only from the Parties contracts event assembly and rejects arbitrary assembly-qualified inputs. Do not reintroduce `Type.GetType`.
- `PartyDetail` already contains personal-data-bearing fields (`DisplayName`, `SortName`, `NameHistory`) plus contact channels, identifiers, consent, restriction, erasure, and timestamps. Treat it as the canonical detail projection shape for this story.

### Architecture Patterns and Constraints

- Read projections are Dapr actor-managed JSON state persisted to the Dapr state store. The documented detail key format is `{tenant}:party-detail:{partyId}`.
- Projection granularity is hybrid: party detail is per-party, tenant party index is per-tenant. This story owns the detail projection only; do not implement list/search/index behavior here.
- Projection logic belongs in pure handler classes. Actors are thin wrappers that handle Dapr state, serialized event dispatch, checkpointing, reminders, and rebuild/degraded mode.
- EventStore remains the write-side source of truth. Query/read models must not rehydrate aggregates on every query, and aggregate handlers must not depend on projection state.
- Projection-side tenant isolation is a Parties responsibility, but this story is the write/update side of the detail projection. Public tenant-safe read query behavior belongs to later Epic 2 stories.
- Event contracts are additive and forward-compatible. Avoid changing event shapes to satisfy timestamp preferences; use envelope metadata only if it is already safely available to the projection actor path.
- Rejection events are persisted and replayed. They are not exceptions, and projection replay must stay compatible with historical streams containing them.
- Rejection events are durable audit facts for replay compatibility. They must be recognized where persisted event compatibility requires it, but applying them to party detail projection state is a no-op for both data fields and timestamps.
- Keep personal data out of logs, telemetry dimensions, exception messages, and test snapshots. It is acceptable for `PartyDetail` to contain personal data as product data returned to authorized consumers, but operational diagnostics must stay metadata-only.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Contracts/Models/PartyDetail.cs
src/Hexalith.Parties.Contracts/Events/PartyCreated.cs
src/Hexalith.Parties.Contracts/Events/PartyDisplayNameDerived.cs
src/Hexalith.Parties.Contracts/Events/PersonDetailsUpdated.cs
src/Hexalith.Parties.Contracts/Events/OrganizationDetailsUpdated.cs
src/Hexalith.Parties.Contracts/Events/ContactChannelAdded.cs
src/Hexalith.Parties.Contracts/Events/ContactChannelUpdated.cs
src/Hexalith.Parties.Contracts/Events/ContactChannelRemoved.cs
src/Hexalith.Parties.Contracts/Events/PreferredContactChannelChanged.cs
src/Hexalith.Parties.Contracts/Events/IdentifierAdded.cs
src/Hexalith.Parties.Contracts/Events/IdentifierRemoved.cs
src/Hexalith.Parties.Contracts/Events/PartyDeactivated.cs
src/Hexalith.Parties.Contracts/Events/PartyReactivated.cs
src/Hexalith.Parties.Contracts/State/PartyState.cs
src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs
src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs
src/Hexalith.Parties.Projections/Actors/PartyEventTypeResolver.cs
src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs
tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs
tests/Hexalith.Parties.Projections.Tests/Actors/PartyEventTypeResolverTests.cs
tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs
tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs
```

### Previous Story Intelligence

- Story 1.1 established the current solution shape and root-level submodule guardrail. Use `Hexalith.Parties.slnx`; do not initialize nested submodules recursively.
- Story 1.2 established stable party identity and person/organization creation semantics. Detail projection id must come from the actor/aggregate party id, not from untrusted event payload fields.
- Story 1.3 established that detail updates emit the specific update event and may emit `PartyDisplayNameDerived`; projection state must reflect ordered event replay.
- Stories 1.4 and 1.5 established contact-channel and identifier ids as stable collection keys. Projection idempotency should deduplicate by those ids.
- Story 1.6 kept deactivation/reactivation as lifecycle state changes, not deletion or erasure. Detail projection should preserve party details while toggling `IsActive`.
- Story 1.7 clarified rejection/no-op semantics. Rejections must not carry or imply successful state changes.
- Story 1.8 reinforced personal-data marking and log safety. Projection code must not log raw party details, display names, contact values, identifiers, or serialized event payloads.
- Story 1.9 reinforced reuse of `PartyDetail` as the canonical complete party shape. Avoid adding duplicate detail DTOs unless serialization forces a narrow wrapper.

### Advanced Elicitation Clarifications

- `PartyDetailProjectionHandler.Apply(...)` returning `null` is a no-mutation result. It must not be interpreted by actor code, rebuild code, or tests as an instruction to delete, reset, or overwrite the current stored projection.
- Accepted no-op events and invalid dropped events have different operational meaning. Deserialized events that dispatch through the accepted event path may update the sequence checkpoint even when no detail-state write occurs; invalid event names, unsupported formats, and unreadable payloads must follow the current actor policy and be tested without inventing a public freshness contract.
- Timestamp evidence should focus on invariants: no-op and rejection events keep existing timestamps byte-for-byte, while real mutations move `LastModifiedAt` monotonically. Exact wall-clock timestamp assertions are too brittle unless event metadata is explicitly available.
- User/operator-observable verification for this story is test and completion-note evidence around state key, checkpoint, corruption/rebuild, and metadata-only diagnostics. It is not a license to add query APIs, dashboards, admin UI, or public freshness response fields.
- Log-safety assertions should include exception and drop paths. Actor diagnostics may contain metadata such as tenant id, party id, projection name, event type, sequence, and reason, but not raw event payloads, serialized `PartyDetail`, display names, contact values, identifiers, or consent values.

### Latest Technical Notes

- Local source of truth for package versions is `Directory.Packages.props`: .NET SDK `10.0.103`, `net10.0`, Dapr packages `1.17.9`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`, and Microsoft.NET.Test.Sdk `18.5.1`.
- Dapr actor reminders are persistent callbacks that survive actor deactivation/failover until explicitly unregistered; this supports the current rebuild reminder approach. [Source: docs.dapr.io Actor timers and reminders]
- Dapr actor state requires an actor state store (`actorStateStore=true`), and only one state store is used for all actors. Do not bypass actor state management with direct state-store access from handlers or MCP/client code. [Source: docs.dapr.io Actors overview / State management]

### Testing Requirements

- Use xUnit v3 and Shouldly assertions. Use NSubstitute where actor state manager or rebuild service collaborators are mocked.
- Keep pure projection handler tests infrastructure-free. They should directly call `PartyDetailProjectionHandler.Apply(...)` and assert exact state transitions.
- Use existing `Hexalith.Parties.Testing` helpers such as `PartyTestData` where they fit. Prefer obvious synthetic placeholder values; do not introduce real personal data.
- Avoid snapshotting full personal-data-bearing JSON. Assert selected structural fields and counts.
- Actor tests may use Dapr `ActorHost.CreateForTest`, reflection only where needed for protected lifecycle hooks, and mocked `IActorStateManager`.
- If `PartyState` is touched, rerun the rejection apply ordering fitness tests because EventStore rehydration depends on rejection `Apply` overload ordering.

### Anti-Patterns To Avoid

- Do not recreate `PartyDetailProjectionHandler`, `PartyDetailProjectionActor`, or `PartyDetail` under new names.
- Do not put Dapr references in projection handler classes.
- Do not add query APIs, REST controllers, OpenAPI/Swagger, or MCP tools in this story.
- Do not fake idempotency by dropping all repeated event types; deduplicate collection additions by stable ids and use sequence checkpoints where event sequence is available.
- Do not change existing public event contracts only to improve projection timestamps.
- Do not treat rejection events as deserialization failures or delete them from replay.
- Do not log personal data, raw event payloads, serialized `PartyDetail`, contact values, identifiers, display names, or raw tenant membership payloads.
- Do not weaken project boundary or architectural fitness tests to make projection code compile.
- Do not recursively initialize or update nested submodules.

### Deferred Decisions

- Event-time accuracy depends on whether EventStore envelope metadata is available to projection delivery. If not, projection processing timestamps are acceptable for this story and should be documented in completion notes.
- Query responses with freshness/degradation indicators are owned by later Epic 2 stories. This story may preserve actor rebuild/degraded primitives, but should not design the public query response contract.
- Party index projection behavior is Story 2.2. Avoid pulling tenant list/search/index requirements into this story except where shared abstractions must not be broken.
- Automated drift detection and operational rebuild runbooks are broader Story 2.8 concerns. This story should preserve current rebuild hooks without expanding scope unnecessarily.
- Projection schema versioning, migration/backfill strategy, and cross-projection consistency guarantees remain deferred until a later operational or compatibility story requires them.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-2.1-Build-Party-Detail-Projection] - Story statement and BDD acceptance criteria.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2-Searchable-Tenant-Safe-Read-Models] - Epic goal and cross-story context for detail, index, query, freshness, rebuild, and deferred search.
- [Source: _bmad-output/planning-artifacts/prd.md#Party-Discovery-Search-MVP] - FR18 and FR19 read projection requirements plus eventual consistency target.
- [Source: _bmad-output/planning-artifacts/architecture.md#D1-Projection-Data-Store] - Dapr actor-managed JSON state decision.
- [Source: _bmad-output/planning-artifacts/architecture.md#D4-Projection-Actor-Granularity] - Per-party detail and per-tenant index projection split.
- [Source: _bmad-output/planning-artifacts/architecture.md#D18-Projection-Testability] - Pure handler classes and thin actor wrapper requirement.
- [Source: _bmad-output/planning-artifacts/architecture.md#DAPR-State-Key-Conventions] - Detail projection state key convention.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - Actor host, EventStore ownership, projection-side tenant safety, privacy, testing, and submodule guardrails.
- [Source: src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs] - Existing pure party detail projection handler.
- [Source: src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs] - Existing Dapr actor wrapper, serialized event dispatch, checkpointing, degraded cache, and rebuild reminder behavior.
- [Source: src/Hexalith.Parties.Projections/Actors/PartyEventTypeResolver.cs] - Existing safe event type resolver.
- [Source: tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs] - Existing pure handler test coverage to extend.
- [Source: docs.dapr.io/developing-applications/building-blocks/actors/actors-timers-reminders/] - Dapr reminder persistence and retry behavior.
- [Source: docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/] - Dapr actor state store requirements.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-18: Red-phase handler validation failed on no-effective name, missing contact/identifier remove, already inactive/active lifecycle, and restriction-lift no-op cases before production fixes.
- 2026-05-18: `dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionHandler` passed (56 tests).
- 2026-05-18: `dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyEventTypeResolver` passed (12 tests).
- 2026-05-18: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionActorCorruptionTests` passed (13 tests).
- 2026-05-18: `dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Release` passed (86 tests).
- 2026-05-18: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitecturalFitnessTests` passed (17 tests).
- 2026-05-18: `dotnet test Hexalith.Parties.slnx --configuration Release` passed. Existing health E2E tests remained skipped by their configured conditions.

### Completion Notes List

- Reused the existing party detail projection handler, actor wrapper, actor abstraction, and `PartyDetail` model. No parallel handler, query API, REST surface, MCP surface, public event schema change, project reference change, or package change was introduced.
- Hardened handler no-op semantics so duplicate/no-effective detail name, person detail, organization detail, contact remove, identifier remove, lifecycle, and restriction events return `null` instead of refreshing `LastModifiedAt` or replacing stored state.
- Preserved stable-id idempotency for contact channels, identifiers, and consent records, with tests proving duplicate adds do not replace existing items, append duplicates, or advance `LastModifiedAt`.
- Added rejection replay coverage for representative persisted rejection events before/after successful state, proving null state remains null and existing successful detail state is preserved without timestamp movement.
- Added actor-wrapper coverage for the documented `{tenant}:party-detail:{partyId}` state key, malformed projection/actor id failures, party-id mismatch failures, checkpoint skipping, and accepted rejection no-op checkpoint advancement without `PartyDetail` state writes.
- Projection timestamps continue to use processing-time `DateTimeOffset.UtcNow` because the current event payloads and projection actor path do not expose event-time metadata. Tests assert unchanged timestamps for no-ops and monotonic/changed timestamps for real mutations rather than exact wall-clock values.

### File List

- `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs`
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs`
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerNameHistoryTests.cs`
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerRejectionFitnessTests.cs`
- `tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs`
- `_bmad-output/implementation-artifacts/2-1-build-party-detail-projection.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-05-16: Story created by BMAD pre-dev context workflow with existing projection implementation analysis, Dapr actor guardrails, pure handler testing guidance, and replay/idempotency requirements.
- 2026-05-17: Party-mode review clarifications applied for stable idempotency keys, rejection replay no-op evidence, pre-create event ordering, actor-id failure behavior, timestamp precedence, and log-safe corruption testing.
- 2026-05-17: Advanced elicitation clarifications applied for handler no-op preservation, accepted no-op checkpoint behavior, timestamp assertions, and metadata-only diagnostics.
- 2026-05-18: Implemented story 2.1 party detail projection hardening, no-op semantics, rejection replay coverage, actor persistence/key tests, and Release regression validation.
- 2026-05-19: Code review (Blind/Edge/Auditor) — applied 7 patches: (P1) cleaned tautological `result ?? state` assertions across 4 rejection/idempotency tests; (P2) added `LastModifiedAt` monotonic assertion + consistent `?? state` fallback in `Apply_CompleteDetailReplay_…`; (P3) fixed `HandleNameDerived` empty-history branch to also compare `state.DisplayName/SortName`, plus two new positive/negative empty-history tests; (P4) added reflection-based `IRejectionEvent` fitness test discovering all 19 contracts rejection types and asserting `Apply` returns null for both null and populated state; (P5) added explicit tests for `PartyMerged` and `IsNaturalPersonChanged` returning null; (P6) added actor-layer `HandleEventAsync_PartyDeactivated_WhenAlreadyInactive_DoesNotWritePartyDetailStateAsync` proving no-op success event preservation parity with rejection-event variant; (P7) strengthened `HandleEventAsync_InvalidActorId_FailsBeforeStateWriteAsync` with `DidNotReceive().SetStateAsync<long>` checkpoint guarantee. 16 findings deferred (Epic 6 erasure semantics, aggregate-trust pre-existing patterns, observability improvements). Validation: PartyDetailProjectionHandler 99/99, PartyEventTypeResolver 12/12, PartyDetailProjectionActorCorruptionTests 14/14, ArchitecturalFitnessTests 17/17, full Projections.Tests 129/129.

## Advanced Elicitation

- Date/time: 2026-05-17T11:03:54+02:00
- Selected story key: `2-1-build-party-detail-projection`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-1-build-party-detail-projection`
- Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Security Audit Personas; Self-Consistency Validation; Architecture Decision Records
- Reshuffled Batch 2 method names: Pre-mortem Analysis; Chaos Monkey Scenarios; User Persona Focus Group; Critique and Refine; Expand or Contract for Audience
- Findings summary:
  - The most important replay risk is confusing the handler's no-mutation `null` result with deletion or state reset in actor/rebuild paths.
  - Accepted no-op events and invalid dropped events need distinct checkpoint and state-write expectations so replay progress does not masquerade as projection mutation or public freshness.
  - Timestamp tests should prove unchanged no-op values and monotonic real mutations instead of relying on exact processing-time assertions.
  - Diagnostic and corruption paths need the same privacy discipline as happy-path projection logic because projection state contains personal data.
  - Operator-observable evidence should remain in tests and completion notes rather than expanding public read/query, admin, or dashboard surfaces.
- Changes applied:
  - Added AC and task language requiring handler no-op results to preserve existing projection state.
  - Added duplicate/no-effective-change timestamp and collection-idempotency coverage.
  - Clarified accepted no-op checkpoint behavior versus invalid/dropped event policy.
  - Added timestamp assertion guidance and metadata-only diagnostic expectations.
  - Added an `Advanced Elicitation Clarifications` subsection with implementation traps for dev-story execution.
- Findings deferred:
  - Public query/freshness response behavior remains later Epic 2 work.
  - Dashboard/admin/operator UI surfaces remain out of scope for this story.
  - Event-time metadata redesign and additive event schema changes remain deferred to a separate architecture decision.
  - Projection schema migration/backfill and cross-projection consistency guarantees remain deferred.
- Final recommendation: ready-for-dev

## Party-Mode Review

- Date/time: 2026-05-17T08:57:48+02:00
- Selected story: `2-1-build-party-detail-projection`
- Command/skill invocation used: `/bmad-party-mode 2-1-build-party-detail-projection; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - All reviewers recommended `ready-for-dev`; no blocker was identified.
  - Review risk centered on replay confidence rather than product scope: deterministic idempotency keys, rejection-event no-op evidence, pre-create event ordering, actor-id parsing failures, timestamp provenance, and metadata-only diagnostics.
  - Architecture/product boundaries remain correct: no read/query API surface, search/index behavior, event contract timestamp redesign, cross-projection consistency guarantee, or migration/backfill strategy belongs in this story.
- Changes applied:
  - Clarified collection idempotency by stable `ChannelId` and `IdentifierId`, while scalar/detail/lifecycle updates remain aggregate-order last-event-wins.
  - Required rejection replay tests before, after, and interleaved with successful events, including no data-field or timestamp mutation.
  - Clarified pre-create child events as no-ops unless existing implementation already has a stricter corruption path.
  - Required separate malformed actor-id and party-id mismatch tests with metadata-only diagnostics and no Dapr state write or checkpoint advancement.
  - Clarified timestamp precedence without changing event contracts.
  - Added corruption/log-safety expectations for unreadable actor JSON state.
- Findings deferred:
  - Public read/query API shape and freshness response behavior remain later Epic 2 work.
  - Search, list, tenant index projection, and cross-party projection shapes remain Story 2.2+ work.
  - Event contract timestamp redesign and normalization semantics remain deferred unless a future architecture decision approves them.
  - Projection schema versioning, migration/backfill, and cross-projection consistency guarantees remain deferred.
- Final recommendation: `ready-for-dev`

### Review Findings

_Code review 2026-05-19 — three parallel adversarial layers (Blind Hunter, Edge Case Hunter, Acceptance Auditor). All 5 ACs Pass; 7 patches and 16 defers raised. Baseline diff: commit `a7112f3` filtered to story 2.1 file list._

- [x] [Review][Patch] Tautological assertions in rejection/idempotency tests — after `result.ShouldBeNull()` passes, `preserved = result ?? state` makes `preserved` the same reference as `state`, so the trailing `preserved.X.ShouldBe(state.X)` assertions are tautologies and document the test setup rather than handler behavior. Affects `Apply_RejectionEvent_WithExistingState_ReturnsNullAndCallerPreservesState`, `Apply_InterleavedRejection_DoesNotMutateStateOrTimestamp`, `Apply_DuplicateContactIdentifierAndConsentAdds_ReturnNullWithoutReplacingExistingItems`, and `PartyDetailProjectionHandlerNameHistoryTests` sameNameEvent test. Either rename to `_ReturnsNull` and rely on actor-layer `HandleEventAsync_RejectionNoOp_*` for "caller preserves state", or delete the redundant assertions. [tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs:709-714, :731-736, :817-823, PartyDetailProjectionHandlerNameHistoryTests.cs:96-100]
- [x] [Review][Patch] `Apply_CompleteDetailReplay_…` does not assert `LastModifiedAt` monotonic and inconsistently uses `?? state` fallback — only the `PreferredContactChannelChanged` step (line 548) uses the `?? state` fallback; `PartyDeactivated` / `PartyReactivated` steps (lines 567-569) use `state.ShouldNotBeNull()`. The final assertions only check `state.CreatedAt.ShouldBe(createdAt)`. A regression removing `LastModifiedAt = DateTimeOffset.UtcNow` from one handler would not be caught. Add `state.LastModifiedAt.ShouldBeGreaterThan(createdAt)` and use `?? state` consistently for conditional-mutation steps. [tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs:476-583]
- [x] [Review][Patch] `HandleNameDerived` empty-history branch silently re-populates NameHistory and bumps timestamp — the guard `state.NameHistory.Count > 0 && string.Equals(state.NameHistory[^1].…)` falls through when `NameHistory` is empty (post-erasure replay, legacy snapshot), always appending a history entry and bumping `LastModifiedAt` even when `state.DisplayName == e.DisplayName && state.SortName == e.SortName`. Extend the guard to also check `state.DisplayName`/`state.SortName` so empty-history idempotency holds. Add a sort-only-change positive test (DisplayName same, SortName different) to prove history IS appended in that path; add a no-effective-change diverged-state test where `state.DisplayName` matches the event but `NameHistory` does not. [src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs:95-100]
- [x] [Review][Patch] No fitness test covers all `IRejectionEvent` types — `RepresentativeRejectionEvents` theory exercises 5 of 19 rejection types in `Hexalith.Parties.Contracts.Events`. Future rejection types are silently absorbed by the `_ => null` switch arm without test coverage. Add a reflection-based fitness test in `Hexalith.Parties.Projections.Tests` (or `Hexalith.Parties.Tests/FitnessTests`) that discovers every type implementing `IRejectionEvent`, instantiates it (`Activator.CreateInstance` with `required`-property tolerance via `RuntimeHelpers.GetUninitializedObject` if needed for `PartyCommandValidationRejected`), and asserts `PartyDetailProjectionHandler.Apply(...)` returns `null` for each. Also assert the actor-level `HandleSerializedEventAsync` advances the checkpoint without writing PartyDetail state for at least one parameterised representative per category. [tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs:1144-1151]
- [x] [Review][Patch] No test asserts `PartyMerged` and `IsNaturalPersonChanged` return `null` from `Apply` — both are explicitly mapped to `null` in the switch but have no test pinning the contract. A refactor that removes either case would fall through to `_ => null` (same observable behavior) but lose the documented intent. Add `[Fact]` tests `Apply_PartyMerged_ReturnsNull` and `Apply_IsNaturalPersonChanged_ReturnsNull` against a populated state. [src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs:31-32]
- [x] [Review][Patch] Already-(de)active/not-restricted no-op tests do not assert `LastModifiedAt` is unchanged — `Apply_PartyDeactivated_WhenAlreadyInactive_ReturnsNull`, `Apply_PartyReactivated_WhenAlreadyActive_ReturnsNull`, and `Apply_RestrictionLifted_WhenNotRestricted_ReturnsNull` only assert `result.ShouldBeNull()`. Pattern the new tests after `Apply_RejectionEvent_*` once that pattern is unTAUTOLOGICAL (P1) and add a `state.LastModifiedAt` invariance assertion at the actor-layer or pair with an explicit "no-op preserves prior timestamp" sub-test. [tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs:286-294 and siblings]
- [x] [Review][Patch] `HandleEventAsync_InvalidActorId_FailsBeforeStateWriteAsync` does not assert checkpoint key is not written — the test only checks `DidNotReceive().SetStateAsync(string, PartyDetail, CT)`. A regression that writes `:last-sequence` before throwing would pass. Add `DidNotReceive().SetStateAsync(string, long, CT)` to fully cover "fails before state write" per Task 3. [tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs:236-249]
- [x] [Review][Defer] `PersonDetailsUpdated`/`OrganizationDetailsUpdated` on wrong party type silently accepted [src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs:121-141] — pre-existing, projection trusts aggregate per "Do not synthesize partial party detail state from child events" guardrail
- [x] [Review][Defer] `DateTimeOffset.UtcNow` same-tick collision in processing timestamps [all handlers] — documented limitation in completion notes; deferred to event-time metadata architecture decision
- [x] [Review][Defer] `HandleProcessingRestricted` accepts `default(DateTimeOffset)` without validation [PartyDetailProjectionHandler.cs:357-369] — pre-existing pattern, trust aggregate
- [x] [Review][Defer] `HandleProcessingRestricted` exact `==` on `DateTimeOffset` for dedup [PartyDetailProjectionHandler.cs:359] — pre-existing pattern, clock-skew/replay-recompute would silently re-write `RestrictedAt`; needs design discussion
- [x] [Review][Defer] `RestrictionLifted` OR-check silently repairs corrupt state when `IsRestricted=false` but `RestrictedAt is not null` [PartyDetailProjectionHandler.cs:170] — pre-existing repair behavior, needs design call on no-op vs repair
- [x] [Review][Defer] `ConsentRevoked` overwrites already-revoked records without idempotency check [PartyDetailProjectionHandler.cs:313-333] — pre-existing pattern, audit-log integrity concern; needs design call
- [x] [Review][Defer] `HandleContactChannelAdded`/`Removed` dedup by id ignores divergent payload [PartyDetailProjectionHandler.cs:151, 203] — pre-existing pattern; no observability when projection drifts from aggregate
- [x] [Review][Defer] `.Any(...)` on potentially-null collections in stale-snapshot replay [PartyDetailProjectionHandler.cs:151, 203, 277] — pre-existing pattern relying on init defaults; not in story scope
- [x] [Review][Defer] PartyCreated after erasure silently ignored [PartyDetailProjectionActor.cs:60-63] — Epic 6 erasure-semantics territory
- [x] [Review][Defer] Post-erasure replay path not exercised in `Apply_CompleteDetailReplay_…` [tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs:476-583] — Epic 6 erasure-semantics territory; couples to P3's empty-history fix
- [x] [Review][Defer] `EnsureLastSequenceLoadedAsync` swallows `KeyNotFoundException` then never re-loads [PartyDetailProjectionActor.cs:166-191] — pre-existing actor flow; transient outage recovery is a broader operability concern
- [x] [Review][Defer] HandleEventAsync / PersistLastSequenceAsync partial-failure coupling [PartyDetailProjectionActor.cs:152-211] — pre-existing actor flow; no test asserting state vs checkpoint divergence on transient failure
- [x] [Review][Defer] `EraseAsync` keeps erased shell in static `s_lastKnownDetails` cache [PartyDetailProjectionActor.cs:217-244] — Epic 6 erasure-semantics territory
- [x] [Review][Defer] `JsonException` from `PayloadDeserializationFailed` log path may include payload fragment [PartyDetailProjectionActor.cs:461] — pre-existing logging behavior; PII-bleed concern via exception chain
- [x] [Review][Defer] `HandleContactChannelAdded` accepts empty/whitespace `ContactChannelId` [PartyDetailProjectionHandler.cs:151] — pre-existing pattern, trust aggregate
- [x] [Review][Defer] No log/diagnostic for "duplicate event with divergent payload" silent drop [PartyDetailProjectionHandler.cs:151, 257, 292] — speculative observability improvement, needs design discussion


