# Story 2.1: Build Party Detail Projection

Status: ready-for-dev

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

5. **Pure handler tests verify representative event replay**
   - Given projection handler tests run without Dapr infrastructure,
   - When they replay representative party event sequences,
   - Then they verify detail state after create, update, contact channel, identifier, deactivate, and reactivate flows,
   - And they verify event ordering assumptions documented by the architecture.

## Tasks / Subtasks

- [ ] Task 1: Audit and reuse the existing projection implementation before editing (AC: 1, 3, 4)
  - [ ] Start from `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs`; do not create a second projection handler or parallel projection project.
  - [ ] Inspect `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs`, `src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs`, and `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`.
  - [ ] Confirm the current behavior against this story before changing it: the handler already covers create, display name, person/organization detail updates, contact channels, identifiers, lifecycle, consent, restriction, erasure, idempotent add-by-id, and no-op unknown events.
  - [ ] Treat existing names, project references, and actor key format as established patterns unless an acceptance criterion proves they are wrong.

- [ ] Task 2: Complete party-detail event mapping and no-op semantics (AC: 1, 2, 4)
  - [ ] Ensure `PartyCreated` initializes `PartyDetail.Id`, `Type`, `IsActive`, person/organization details, display/sort names, empty collections, `CreatedAt`, and `LastModifiedAt`.
  - [ ] Ensure `PartyDisplayNameDerived`, `PersonDetailsUpdated`, `OrganizationDetailsUpdated`, contact channel events, identifier events, `PartyDeactivated`, and `PartyReactivated` update only the relevant fields while preserving unrelated state.
  - [ ] Keep rejection events as projection no-ops. Add explicit coverage for representative rejection events such as `PartyCannotBeCreatedWithInvalidId`, `PartyCannotAddDuplicateChannel`, `PartyCannotAddDuplicateIdentifier`, `PartyCannotBeDeactivatedWhenInactive`, and `PartyCannotBeReactivatedWhenActive`.
  - [ ] Preserve idempotent add semantics for contact channels and identifiers by natural ids. Duplicate add delivery must not append duplicate list entries.
  - [ ] Do not use exceptions for domain rejections and do not delete or special-case persisted rejection events out of replay.

- [ ] Task 3: Validate actor wrapper persistence, replay, and key safety (AC: 3, 4)
  - [ ] Keep Dapr dependencies in the actor wrapper only; `PartyDetailProjectionHandler` must remain free of `Dapr.*` references.
  - [ ] Confirm `PartyDetailProjectionActor.ResolveStateContext(...)` derives the state key from actor id `{tenant}:party-detail:{partyId}` and rejects malformed projection segments or party-id mismatches.
  - [ ] Confirm `HandleSerializedEventAsync(...)` resolves event type names only through `PartyEventTypeResolver` and does not reintroduce `Type.GetType` or arbitrary assembly loading.
  - [ ] Preserve sequence checkpoint behavior so replay-from-zero skips already applied events, while idempotent collection handlers still protect against state/checkpoint divergence on non-transactional state stores.
  - [ ] Keep actor logs bounded to metadata such as party id, event type name, sequence, projection name, tenant id, and correlation-safe context. Do not log contact values, identifiers, display names, serialized details, or raw event payloads.

- [ ] Task 4: Clarify timestamp behavior without inventing unsupported event metadata (AC: 1)
  - [ ] Verify whether Parties event payloads expose event-time metadata through the payload or envelope available to the projection actor.
  - [ ] If only payload events are available to the pure handler, keep timestamps as projection processing timestamps and document the limitation in completion notes.
  - [ ] Do not add event-time fields to existing public event contracts unless a separate architecture decision explicitly approves an additive event schema change.
  - [ ] Ensure repeated no-op events do not accidentally advance `LastModifiedAt`.

- [ ] Task 5: Strengthen pure handler and actor tests (AC: 1, 2, 3, 4, 5)
  - [ ] Extend `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs` for complete create -> derived-name -> detail update -> contact add/update/remove -> preferred channel -> identifier add/remove -> deactivate -> reactivate sequences.
  - [ ] Add duplicate-delivery tests proving repeated `ContactChannelAdded` and `IdentifierAdded` do not duplicate entries, and no-op updates return no mutation.
  - [ ] Add rejection-event tests proving existing successful state is preserved and null state remains null.
  - [ ] Add or update actor tests for state-key convention, malformed actor ids, party-id mismatch, serialized event dispatch, non-JSON drop behavior, unknown/ambiguous event names, checkpoint skipping, and corruption/rebuild behavior.
  - [ ] Keep pure handler tests in `Hexalith.Parties.Projections.Tests`; keep Dapr actor/state-manager behavior in actor/integration-style tests that already use `ActorHost.CreateForTest` and NSubstitute.

- [ ] Task 6: Preserve architecture fitness and package boundaries (AC: 3, 5)
  - [ ] Do not add REST controllers, public minimal APIs, Swagger/OpenAPI, or MCP tools to `src/Hexalith.Parties` for this story.
  - [ ] Do not move query behavior into the aggregate or server project; projections remain read-side infrastructure.
  - [ ] Do not make Contracts depend on Projections, Server, Dapr, MediatR, FluentValidation, UI, or infrastructure packages.
  - [ ] Do not add package versions to `.csproj`; keep versions in `Directory.Packages.props`.
  - [ ] If architecture fitness tests fail, fix the boundary violation rather than weakening the test.

- [ ] Task 7: Run focused validation (AC: 1, 2, 3, 4, 5)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionHandler`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyEventTypeResolver`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionActorCorruptionTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitecturalFitnessTests` if project references, Dapr boundaries, or handler dependencies change.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if public contracts, project references, or package configuration change.

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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-05-16: Story created by BMAD pre-dev context workflow with existing projection implementation analysis, Dapr actor guardrails, pure handler testing guidance, and replay/idempotency requirements.
