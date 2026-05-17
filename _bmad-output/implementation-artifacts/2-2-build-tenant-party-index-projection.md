# Story 2.2: Build Tenant Party Index Projection

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a consumer browsing party records,
I want each tenant to have a read-optimized party index,
so that I can list and filter parties without scanning aggregate event streams.

## Acceptance Criteria

1. **Tenant index entries reflect party lifecycle and summary changes**
   - Given party create, display-name, lifecycle, contact-channel, identifier, erasure, and relevant detail events are published for a tenant,
   - When the party index projection handler processes those events in aggregate order,
   - Then it maintains lightweight party index entries with party id, type, display name, sort/sortable name behavior, active status, created timestamp, last-modified timestamp, erasure state, and non-PII status indicators.

2. **Changed indexed values do not leave stale list/search data**
   - Given an indexed party changes display name, sort name inputs, type-specific details that affect derived names, active status, contact summary state, identifier summary state, or erasure state,
   - When the corresponding event is applied,
   - Then the tenant index entry is updated or removed consistently,
   - And stale values are not retained in the index, search fallback, or serialized actor state.

3. **Partition strategy remains the durable scale boundary**
   - Given a tenant has many party index entries,
   - When the index actor persists state,
   - Then it uses `IIndexPartitionStrategy` to resolve the state partition,
   - And the v1.0 `SingleKeyPartitionStrategy` remains valid while preserving a path to multi-key partitioning without changing handler logic or public contracts.

4. **Burst event delivery persists through bounded batching**
   - Given burst event delivery occurs for many party changes in the same tenant,
   - When the index actor processes events,
   - Then it batches persistence using configured batch size or timing,
   - And flush, deactivation, rebuild, and explicit read paths preserve the eventual consistency target without losing accepted events.

5. **Replay and duplicate delivery are idempotent where event identity allows**
   - Given duplicate, repeated, out-of-order-before-create, or replayed events are delivered,
   - When the index handler and actor process them,
   - Then duplicate index entries are not created,
   - And no-op events preserve existing index state and timestamps rather than clearing state or refreshing `LastModifiedAt`.

6. **Pure handler tests verify tenant stream behavior without Dapr**
   - Given projection handler tests run without Dapr infrastructure,
   - When they replay representative tenant event streams,
   - Then they verify create, update, deactivate, reactivate, erasure, date metadata, duplicate delivery, missing-item updates, and rejection/no-op behavior,
   - And the handler remains free of Dapr dependencies.

7. **Observable list/search behavior uses the tenant-safe index**
   - Given a tenant party index projection has been built for the current tenant,
   - When consumers list, filter, or display-name search parties through the documented read path,
   - Then results are served from the tenant-safe index with pagination, type/status/date filtering, display-name matching, and bounded match metadata where applicable,
   - And the story demonstrates observable consumer browsing/search behavior without adding retired REST endpoints, projection actor internals, aggregate stream details, or cross-tenant shortcuts.
   - And missing, invalid, or mismatched tenant context fails closed with no cross-tenant result, state-key leak, diagnostic payload, or reusable cursor/token behavior.

## Acceptance Evidence and Traceability

| AC | Required evidence before review |
| --- | --- |
| AC1 | `PartyIndexProjectionHandlerTests` covers create, derived-name, lifecycle, contact, identifier, erasure, and relevant detail event streams without Dapr. |
| AC2 | Handler/search tests prove changed or erased indexed values no longer match old list/search results and no stale durable state is retained. |
| AC3 | Actor/fitness tests prove `IIndexPartitionStrategy` and `SingleKeyPartitionStrategy` keep deterministic tenant partition keys with no personal data in actor ids, state keys, logs, snapshots, or metadata. |
| AC4 | Actor tests cover configured batch size/time-window flushes, explicit read/deactivation/reminder flushes, successful persistence cleanup, and checkpoint behavior without real sleeps. |
| AC5 | Handler and actor tests cover duplicate replay, child-event-before-create, stale events, rejection-event replay, and no-effective-change paths as no mutation with unchanged projected timestamps. Checkpoint advancement must follow the existing actor policy and must never imply a visible index mutation. |
| AC6 | Pure handler tests remain infrastructure-free; actor-owned checkpoint, reminder, batching, rebuild, corruption, and degraded-cache behavior stays in actor tests. |
| AC7 | Query/search boundary tests show tenant A and tenant B list/search independently, pagination/filter/search metadata aligns with returned page items, erased entries are excluded, and no cross-tenant fallback leakage occurs. |

### Advanced Elicitation Clarifications

- Treat authorization inputs as evidence gates, not hints. If tenant context, authorized party ids, actor state, cursor/search state, or request normalization cannot prove tenant ownership, the accepted query/search boundary must return no results and metadata-only diagnostics rather than falling back to aggregate scans, all-tenant local search, cached entries from another tenant, or projection actor internals.
- Reconcile the existing `LocalFuzzyPartySearchProvider` contact/identifier matching as an implementation detail only. This story must either keep public MVP evidence bounded to display-name/type/status/date behavior or explicitly prove that any current contact/identifier matching is not exposed as a promised public search capability, does not leak values through metadata, and remains safe to narrow in later dedicated search stories.
- Distinguish malformed event payloads, unreadable actor state, stale sequence events, duplicate no-op events, and persisted rejection events in tests. They may share metadata-only diagnostics, but they must not all collapse into the same branch if doing so would hide whether state mutation, checkpoint advancement, cache fallback, or rebuild scheduling occurred.
- Keep privacy assertions structural. Tests may assert ids, counts, event type names, sequence numbers, status flags, page metadata, and bounded reason codes; they must not snapshot display names, contact values, identifiers, raw event JSON, serialized actor state, or search query text into logs, diagnostics, exception messages, or approval-style snapshots.
- Make the no-op/write boundary observable. For duplicate replay, stale events, rejection replay, child-before-create, missing child updates/removals, ignored cross-tenant input, and unreadable payloads, tests should separately assert visible index state, `LastModifiedAt`, persisted write attempts, pending-change cleanup, and checkpoint behavior where the actor exposes or can verify those effects.
- Avoid turning hardening into platform work. Do not add multi-partition routing, schema migration/backfill, public freshness contracts, semantic search, operational rebuild runbooks, new REST/OpenAPI/MCP surfaces, or EventStore authorization workarounds to satisfy this story; record any discovered need under deferred decisions.

## Tasks / Subtasks

- [ ] Task 1: Audit and reuse existing index projection surfaces before editing (AC: 1, 3, 4, 7)
  - [ ] Start from `src/Hexalith.Parties.Projections/Handlers/PartyIndexProjectionHandler.cs`; do not create a second handler, second index model, or parallel projection project.
  - [ ] Inspect `src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs`, `src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs`, `src/Hexalith.Parties.Projections/Abstractions/IIndexPartitionStrategy.cs`, `src/Hexalith.Parties.Projections/Strategies/SingleKeyPartitionStrategy.cs`, and `src/Hexalith.Parties.Projections/Configuration/ProjectionOptions.cs`.
  - [ ] Inspect `src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs`, `src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs`, `src/Hexalith.Parties/Search/LocalPartySearchService.cs`, and `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs`.
  - [ ] Confirm current behavior before changing it: the index handler, actor, per-party sequence checkpoint map, partition strategy abstraction, batch flush reminder, rebuild reminder, and local search fallback already exist.
  - [ ] Treat existing names, actor id shape, and state key conventions as established patterns unless an acceptance criterion proves they are wrong.

- [ ] Task 2: Complete index entry shape and event mapping (AC: 1, 2, 5)
  - [ ] Ensure `PartyCreated` initializes `PartyIndexEntry.Id`, `Type`, `IsActive`, `DisplayName`, `CreatedAt`, `LastModifiedAt`, and erasure/default status values from trusted actor/aggregate context.
  - [ ] Reconcile the story requirement for sort/sortable name behavior with the current `PartyIndexEntry` contract. If `SortName` is absent, either add an additive public field with tests or explicitly document why current display-name ordering or normalized display-name ordering is the accepted v1.0 behavior.
  - [ ] Ensure `PartyDisplayNameDerived`, lifecycle events, contact-channel events, identifier events, and erasure paths update only the relevant fields while preserving unrelated entry state.
  - [ ] Do not make index state authoritative for full detail. Full details remain owned by `PartyDetail`; the index is a lightweight list/search projection.
  - [ ] Treat events arriving before `PartyCreated` as no-ops unless existing code already has a stricter corruption policy. Do not synthesize partial index entries from child events.
  - [ ] Keep rejection events as index no-ops. Add explicit representative coverage so persisted rejection events replay without mutating state or timestamps.
  - [ ] For erasure, ensure index behavior is privacy-safe: erased parties must not remain searchable/listable unless a later story explicitly defines an erased-status list contract.
  - [ ] Ensure post-erasure index/search state, logs, metadata, exception messages, and test assertions do not retain or expose personal names, contact values, identifier values, raw payloads, or serialized index entries.

- [ ] Task 3: Strengthen idempotency, timestamp, and no-op semantics (AC: 2, 5, 6)
  - [ ] Preserve duplicate `PartyCreated` behavior: existing index state must not be reset by replayed create events.
  - [ ] Deduplicate contact channels and identifiers by stable ids (`ContactChannelId`, `IdentifierId`), not by values, labels, case-normalized text, or personal data.
  - [ ] Missing-item updates/removals and no-effective-change events must return a no-mutation result and leave `LastModifiedAt` unchanged.
  - [ ] Duplicate or no-effective-change events must not replace existing contact/identifier entries, append duplicates, refresh timestamps, or create search-visible duplicates.
  - [ ] Apply no-op timestamp preservation to duplicate replay, stale events, rejection events, child-event-before-create, missing child updates/removals, and ignored cross-tenant input.
  - [ ] Prefer timestamp assertions that prove unchanged no-op values and monotonic movement for real mutations. Distinguish event timestamp, projected `LastModifiedAt`, replay/checkpoint timestamp, and persistence timing. Do not assert exact wall-clock processing times.
  - [ ] Do not add event-time fields to existing public events unless a separate architecture decision approves an additive schema change.

- [ ] Task 4: Validate actor state keys, partitioning, batching, and checkpoints (AC: 3, 4, 5)
  - [ ] Keep Dapr dependencies in `PartyIndexProjectionActor`; `PartyIndexProjectionHandler` must remain free of `Dapr.*` references.
  - [ ] Keep checkpoint, reminder, actor state, batching, rebuild, corruption, degraded-cache, logging, and serialization side effects out of `PartyIndexProjectionHandler`; it must return deterministic projection effects only.
  - [ ] Confirm actor ids use `{tenant}:party-index` and state keys use `{tenant}:party-index:{partitionKey}` with `default` from `SingleKeyPartitionStrategy` for v1.0.
  - [ ] Confirm per-party sequence checkpoints use stable party ids and do not require rewriting the whole tenant checkpoint map on every event.
  - [ ] Verify malformed actor ids, invalid projection segments, unsupported serialization formats, unknown/ambiguous event names, unreadable payloads, and checkpoint corruption follow explicit actor behavior.
  - [ ] Prove accepted no-op events can advance the per-party checkpoint without writing changed index state when that is current actor policy.
  - [ ] Verify batch persistence uses `ProjectionOptions.BatchSize` and `BatchTimeWindowMs`, flushes on reminder/deactivation/read, and does not leave `_pendingChanges` stuck after successful persistence.
  - [ ] Test reminders and batch timing deterministically through actor/reminder hooks or configured timing abstractions; do not use real sleeps or timing-sensitive assertions.
  - [ ] If multi-key partitioning is not implemented in v1.0, keep `SingleKeyPartitionStrategy` as the only active strategy and record multi-key behavior as deferred rather than silently changing actor activation semantics.

- [ ] Task 5: Preserve tenant safety and search/list boundaries (AC: 2, 7)
  - [ ] Ensure list/search consumers obtain entries only from the tenant-scoped index actor for the current tenant context.
  - [ ] Keep write-side tenant validation in EventStore/gateway ownership; do not add Parties-side write authorization workarounds.
  - [ ] Keep projection-side tenant reads fail-closed when tenant context, authorized party ids, actor state, or query inputs cannot prove safety.
  - [ ] Add tenant isolation proof for missing tenant context, mismatched tenant context, malformed partition keys, and reused pagination/search cursors from another tenant.
  - [ ] Keep local search bounded to display-name/type/status behavior required for MVP unless current accepted code already supports more. Do not expand email/identifier search as public MVP scope without updating the story decision trail.
  - [ ] Preserve accepted current local fallback behavior only. Do not introduce semantic search, email search, identifier search, freshness/degradation response shapes, migration/backfill behavior, rebuild runbooks, or operational repair workflows in this story.
  - [ ] Do not expose `SearchableContactChannels` or `SearchableIdentifiers` through serialized `PartyIndexEntry` payloads or durable callbacks. They may only remain internal implementation details if tests prove they are not persisted or leaked.
  - [ ] Search/list response metadata must not include raw contact values, identifiers, tenant membership payloads, tokens, raw ProblemDetails, raw query payloads, or serialized actor state.
  - [ ] Demonstrate user-visible behavior with at least two tenants: tenant A and tenant B can list/search independently, changed names/statuses stop matching stale values, erased entries are excluded, and results come through the accepted query/search boundary rather than aggregate stream scans.

- [ ] Task 6: Strengthen focused tests (AC: 1, 2, 3, 4, 5, 6, 7)
  - [ ] Extend `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyIndexProjectionHandlerTests.cs` for create -> derived name -> contact add/update/remove -> identifier add/remove -> deactivate -> reactivate -> erasure sequences.
  - [ ] Add tests for duplicate create, duplicate contact add, duplicate identifier add, missing contact/identifier update/remove, child-event-before-create, and rejection-event no-ops.
  - [ ] Add timestamp invariants proving no-op/rejection paths keep `LastModifiedAt` unchanged and real mutations move it monotonically.
  - [ ] Add or update actor tests in `tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs` or adjacent tests for malformed actor ids, partition state key resolution, batch flush, reminder flush, per-party sequence checkpoint, skipped old sequence, redacted/unreadable payload policy, and corruption/rebuild behavior.
  - [ ] Ensure actor tests distinguish visible index mutation from checkpoint advancement, including persisted rejection replay and accepted no-op events.
  - [ ] Add tests for list/filter/search read behavior where the current query boundary exists, including pagination bounds, type filter, active filter, date filters, erased-entry exclusion, empty query behavior, stale/degraded local fallback semantics, and metadata alignment with returned page items.
  - [ ] Add fail-closed search/list tests for missing/mismatched tenant context, malformed partition key behavior, and cross-tenant cursor or token reuse.
  - [ ] Add or preserve log-safety expectations for projection and search diagnostics. Metadata such as tenant id, party id, event type name, sequence number, projection name, case id, and bounded reason is acceptable; party names, contact values, identifier values, raw payloads, and serialized index entries are not.

- [ ] Task 7: Preserve architecture fitness and package boundaries (AC: 3, 6, 7)
  - [ ] Do not add public REST controllers, Swagger/OpenAPI, or MCP tools to `src/Hexalith.Parties` for this story.
  - [ ] Verify observable list/search behavior through existing query/search/client boundaries only; do not expose projection actor internals, aggregate stream details, or new public service surfaces.
  - [ ] Do not move query/list behavior into the aggregate or server project. Projections remain read-side infrastructure.
  - [ ] Do not make Contracts depend on Projections, Server, Dapr, MediatR, FluentValidation, UI, or infrastructure packages.
  - [ ] Do not add package versions to `.csproj`; keep versions in `Directory.Packages.props`.
  - [ ] If architecture fitness tests fail, fix the boundary violation rather than weakening the test.
  - [ ] Do not recursively initialize or update nested submodules.

- [ ] Task 8: Run focused validation (AC: 1, 2, 3, 4, 5, 6, 7)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyIndexProjectionHandler`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyIndexProjectionActor`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~LocalFuzzyPartySearchProvider`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartySearchServiceBoundary`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitecturalFitnessTests` if project references, Dapr boundaries, handler dependencies, REST/MCP exposure, or package references change.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if public contracts, project references, query contracts, or package configuration change.

## Dev Notes

### Current Implementation Context

- This is not a greenfield projection story. `Hexalith.Parties.Projections` already contains `PartyIndexProjectionHandler`, `PartyIndexProjectionActor`, `IPartyIndexProjectionActor`, `IIndexPartitionStrategy`, `SingleKeyPartitionStrategy`, `ProjectionOptions`, rebuild services, and tests.
- `PartyIndexProjectionHandler.Apply(...)` is currently pure and static. It accepts `string partyId`, an `IEventPayload`, and nullable `PartyIndexEntry` state, then returns a new entry or `null` for no mutation.
- Current handler behavior already covers create, display-name update, deactivate/reactivate, contact channel add/update/remove, identifier add/remove, duplicate create preservation, unknown event no-op, and null-state child-event no-op.
- `PartyIndexProjectionActor` stores tenant index dictionaries through Dapr actor state, uses `{tenant}:party-index:{partitionKey}` with `default` from `SingleKeyPartitionStrategy`, batches persistence through `ProjectionOptions`, keeps per-party sequence checkpoints, and supports serialized event dispatch.
- The actor currently has corruption/rebuild behavior, last-known in-memory fallback, `GetEntriesAsync()`, `GetEntriesJsonAsync()`, explicit `EraseAsync(partyId)`, and redacted/unreadable payload handling. Preserve those behaviors unless a test proves they violate this story.
- `PartyProjectionUpdateOrchestrator` delivers ordered aggregate events to both detail and index projection actors and then best-effort indexes the latest index entry into Memories search when configured.
- `IPartiesQueryClient.ListPartiesAsync(...)` and `SearchPartiesAsync(...)` already define consumer-facing query client shapes for pagination, type/status/date filters, and search.
- `LocalPartySearchService` requires `AuthorizedPartyIds`, drops erased entries, materializes entries once, clamps page/page size through request normalization, and aligns metadata with current page items.
- `LocalFuzzyPartySearchProvider` currently searches display name, type text, contact channels, and identifiers. This may exceed the MVP story language that reserves email/identifier search for later dedicated search. Reconcile the accepted current behavior with the story instead of widening public scope silently.
- Timestamp evidence has four different concepts: source event time, projected `LastModifiedAt`, actor replay/checkpoint position, and persistence/flush timing. Tests must assert the right concept instead of treating wall-clock processing time as domain evidence.
- Batching and replay are product-visible because burst delivery must converge to one correct tenant-scoped result set, duplicate delivery must not duplicate visible results, and stale values must stop matching after summary changes.

### Architecture Patterns and Constraints

- Read projections are Dapr actor-managed JSON state persisted to the Dapr state store. The index actor key convention is `{tenant}:party-index:{tenantId}` in architecture prose and `{tenant}:party-index:{partitionKey}` in current code, with `default` as the v1.0 partition key.
- Projection granularity is hybrid: party detail is per party; tenant party index is per tenant. This story owns the index projection and must not regress Story 2.1 detail projection behavior.
- The partition strategy abstraction is the architecture boundary for scale. V1.0 uses single-key state; multi-key partitioning remains a future strategy unless explicitly implemented with actor state-management tests.
- Projection logic belongs in pure handler classes. Actors are thin wrappers that handle Dapr state, serialized event dispatch, checkpointing, batching, reminders, and rebuild/degraded mode.
- EventStore remains the durable write-side source of truth. Query/read models must not rehydrate aggregates for every list or search request, and aggregate handlers must not depend on projection state.
- Projection-side tenant isolation is a Parties responsibility. Read models, search, admin views, and tenant event consumption must fail closed when tenant state cannot be proven.
- Event contracts are additive and forward-compatible. Avoid changing event shapes to satisfy timestamp or sorting preferences unless the change is additive and justified by this story.
- Rejection events are persisted and replayed. They are not exceptions, and projection replay must stay compatible with historical streams containing them.
- Keep personal data out of logs, telemetry dimensions, exception messages, state-key names, query metadata, and test snapshots. `DisplayName` is personal data and may appear in authorized product results, but not operational diagnostics.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs
src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs
src/Hexalith.Parties.Contracts/Models/PagedResult.cs
src/Hexalith.Parties.Contracts/Events/PartyCreated.cs
src/Hexalith.Parties.Contracts/Events/PartyDisplayNameDerived.cs
src/Hexalith.Parties.Contracts/Events/ContactChannelAdded.cs
src/Hexalith.Parties.Contracts/Events/ContactChannelUpdated.cs
src/Hexalith.Parties.Contracts/Events/ContactChannelRemoved.cs
src/Hexalith.Parties.Contracts/Events/IdentifierAdded.cs
src/Hexalith.Parties.Contracts/Events/IdentifierRemoved.cs
src/Hexalith.Parties.Contracts/Events/PartyDeactivated.cs
src/Hexalith.Parties.Contracts/Events/PartyReactivated.cs
src/Hexalith.Parties.Projections/Handlers/PartyIndexProjectionHandler.cs
src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs
src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs
src/Hexalith.Parties.Projections/Abstractions/IIndexPartitionStrategy.cs
src/Hexalith.Parties.Projections/Strategies/SingleKeyPartitionStrategy.cs
src/Hexalith.Parties.Projections/Configuration/ProjectionOptions.cs
src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs
src/Hexalith.Parties/Search/LocalPartySearchService.cs
src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs
src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs
src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs
tests/Hexalith.Parties.Projections.Tests/Handlers/PartyIndexProjectionHandlerTests.cs
tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs
tests/Hexalith.Parties.Tests/Search/LocalFuzzyPartySearchProviderTests.cs
tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs
tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs
```

### Previous Story Intelligence

- Story 2.1 established the projection replay pattern: pure handlers own deterministic state mutation, actor wrappers own Dapr state, serialized dispatch, checkpoints, rebuild/degraded mode, and metadata-only diagnostics.
- Story 2.1 also clarified that handler no-op results must preserve existing projection state; accepted no-op events and invalid dropped events need distinct checkpoint and state-write expectations.
- Stories 1.4 and 1.5 established contact-channel and identifier ids as stable collection keys. Index idempotency must deduplicate by those ids, not by personal values.
- Story 1.6 kept deactivate/reactivate as lifecycle toggles, not deletion or erasure. The index should toggle `IsActive` and preserve listable data unless erasure semantics apply.
- Story 1.7 clarified rejection/no-op semantics. Rejections must not carry or imply successful state changes.
- Story 1.8 reinforced personal-data marking and log safety. Projection/search code must not log raw party details, display names, contact values, identifiers, or serialized event payloads.
- Story 1.9 reinforced `PartyDetail` as the canonical complete party shape. Avoid adding duplicate detail DTOs through index work.

### Testing Requirements

- Use xUnit v3 and Shouldly assertions. Use NSubstitute where actor state manager, timer/reminder manager, proxy factory, or rebuild service collaborators are mocked.
- Keep pure projection handler tests infrastructure-free. They should directly call `PartyIndexProjectionHandler.Apply(...)` and assert exact state transitions.
- Use existing `Hexalith.Parties.Testing` helpers such as `PartyTestData` where they fit. Prefer obvious synthetic placeholder values; do not introduce real personal data.
- Avoid snapshotting full personal-data-bearing JSON. Assert selected structural fields, counts, status flags, and metadata.
- Actor tests may use Dapr `ActorHost.CreateForTest`, reflection only where needed for protected lifecycle hooks, and mocked `IActorStateManager`.
- Search/list tests should verify returned page items and metadata arrays together so score/source metadata cannot drift from `Items`.

### Anti-Patterns To Avoid

- Do not recreate `PartyIndexProjectionHandler`, `PartyIndexProjectionActor`, `PartyIndexEntry`, or query/search clients under new names.
- Do not put Dapr references in projection handler classes.
- Do not add query APIs, REST controllers, OpenAPI/Swagger, or MCP tools in this story.
- Do not fake idempotency by dropping all repeated event types. Deduplicate collection additions by stable ids and use per-party sequence checkpoints where sequence is available.
- Do not change existing public event contracts only to improve projection timestamps or sorting.
- Do not treat rejection events as deserialization failures or delete them from replay.
- Do not log personal data, raw event payloads, serialized index entries, contact values, identifiers, display names, raw tenant membership payloads, or tokens.
- Do not expose projection actor internals directly to clients. Use the accepted query/client boundaries.
- Do not weaken project boundary or architectural fitness tests to make projection code compile.
- Do not recursively initialize or update nested submodules.

### Deferred Decisions

- Multi-key partitioning strategy beyond `SingleKeyPartitionStrategy` remains deferred unless this story explicitly implements and tests actor state management for multiple partition keys.
- Public freshness/degradation response shape belongs to later Epic 2 query/freshness stories. This story may preserve actor rebuild/degraded primitives but should not design new public response contracts.
- Dedicated semantic search, rich Memories search policy, email search, and identifier search beyond accepted current local fallback behavior remain deferred unless a separate planning decision changes MVP scope.
- Projection schema versioning, migration/backfill strategy, and cross-projection consistency guarantees remain deferred until a later operational or compatibility story requires them.
- Automated drift detection and operational rebuild runbooks are broader Story 2.8 concerns. Preserve current rebuild hooks without expanding scope unnecessarily.
- Freshness/degradation public response shape remains deferred. This story may preserve existing actor degraded/rebuild primitives and accepted local fallback behavior, but it must not design a new public freshness contract.
- New REST/OpenAPI/MCP surfaces for party index queries are out of scope; use the accepted query/search/client boundaries only.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-2.2-Build-Tenant-Party-Index-Projection] - Story statement and BDD acceptance criteria.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2-Tenant-Safe-Party-Search-and-Retrieval] - Epic goal and cross-story context for detail, index, query, freshness, rebuild, and deferred search.
- [Source: _bmad-output/planning-artifacts/architecture.md#D1-Projection-Data-Store] - Dapr actor-managed JSON state decision.
- [Source: _bmad-output/planning-artifacts/architecture.md#D4-Projection-Actor-Granularity] - Per-party detail and per-tenant index projection split.
- [Source: _bmad-output/planning-artifacts/architecture.md#D5-Index-Actor-State-Management] - Partition strategy abstraction and single-key v1.0 strategy.
- [Source: _bmad-output/planning-artifacts/architecture.md#D16-Index-Actor-Batch-Event-Processing] - Batch size/time window persistence requirement.
- [Source: _bmad-output/planning-artifacts/architecture.md#D18-Projection-Testability] - Pure handler classes and thin actor wrapper requirement.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - Actor host, EventStore ownership, projection-side tenant safety, privacy, testing, and submodule guardrails.
- [Source: src/Hexalith.Parties.Projections/Handlers/PartyIndexProjectionHandler.cs] - Existing pure tenant index projection handler.
- [Source: src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs] - Existing Dapr actor wrapper, serialized event dispatch, batching, checkpointing, degraded cache, and rebuild reminder behavior.
- [Source: src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs] - Existing lightweight index result shape and `[PersonalData]` display-name marker.
- [Source: src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs] - Existing delivery of aggregate events to detail and index projections.
- [Source: src/Hexalith.Parties/Search/LocalPartySearchService.cs] - Existing local search boundary, authorized party id gate, erased-entry filtering, and metadata behavior.
- [Source: src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs] - Existing consumer query client contract for get/list/search.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Party-Mode Review

- Date: 2026-05-17T14:44:05+02:00
- Selected story key: `2-2-build-tenant-party-index-projection`
- Command/skill invocation used: `/bmad-party-mode 2-2-build-tenant-party-index-projection; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - Winston recommended `needs-story-update` until tenant fail-closed behavior, partition/state-key privacy, rejection replay, batching/checkpoint preservation, and search fallback scope were explicit.
  - Amelia recommended `needs-story-update` for clearer AC-to-test traceability, handler purity, rejection/child-before-create no-op coverage, sort-name fallback, privacy evidence, stable-id deduplication, timestamp preservation, and validation evidence.
  - Murat recommended `ready-for-dev` with clarifications for AC-to-test rows, no-PII diagnostics, tenant negative cases, rejection replay assertions, timestamp-source distinctions, deterministic actor timer/reminder tests, and pinned validation commands.
  - John recommended `needs-story-update` until user-visible tenant-scoped list/search proof, stale-value proof, erasure/privacy proof, no new public surface guardrails, and deferred-boundary wording were strengthened.
- Changes applied:
  - Added AC7 fail-closed tenant behavior and no actor-internal or aggregate-stream leakage language.
  - Added AC-to-test evidence table mapping every acceptance criterion to focused handler, actor, search, and fitness evidence.
  - Clarified sort-name fallback, erasure privacy, no-op timestamp classes, handler purity, deterministic actor timing tests, checkpoint/no-mutation distinction, tenant negative cases, and two-tenant user-visible proof.
  - Strengthened boundaries against semantic/email/identifier search expansion, public freshness/degradation response design, migration/backfill work, rebuild runbooks, REST/OpenAPI/MCP additions, and actor-internal public exposure.
  - Added dev notes distinguishing source event time, projected `LastModifiedAt`, replay/checkpoint position, and persistence timing.
- Findings deferred:
  - Multi-key partitioning beyond `SingleKeyPartitionStrategy`.
  - Public freshness/degradation response shape.
  - Semantic search, email search, and identifier search beyond accepted current local fallback behavior.
  - Projection schema versioning, migration/backfill, and operational rebuild runbooks.
  - Any new external/public API surface for party index queries.
- Final recommendation: ready-for-dev

## Change Log

- 2026-05-17: Story created by BMAD pre-dev context workflow with existing tenant index projection analysis, partition/batching guardrails, search/list boundary guidance, privacy-safe diagnostics, and focused validation commands.
- 2026-05-17: Party-mode review applied low-risk clarifications for tenant fail-closed behavior, AC-to-test traceability, no-op/rejection replay evidence, privacy-safe diagnostics, deterministic actor tests, and deferred public-scope boundaries.
- 2026-05-17: Advanced elicitation applied low-risk clarifications for tenant authorization evidence gates, search fallback scope, malformed/replay/corruption branch separation, structural privacy assertions, and no-op/write-boundary proof.

## Advanced Elicitation

- Date: 2026-05-17T17:02:38+02:00
- Selected story key: `2-2-build-tenant-party-index-projection`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-2-build-tenant-party-index-projection`
- Batch 1 method names:
  - Red Team vs Blue Team
  - Failure Mode Analysis
  - Security Audit Personas
  - Self-Consistency Validation
  - Architecture Decision Records
- Reshuffled Batch 2 method names:
  - Pre-mortem Analysis
  - Chaos Monkey Scenarios
  - User Persona Focus Group
  - Critique and Refine
  - Expand or Contract for Audience
- Findings summary:
  - Red-team/security review found that tenant context, authorized party ids, cursor/search state, and actor state needed to be treated as fail-closed evidence gates rather than advisory hints.
  - Failure-mode and chaos review found that malformed payloads, unreadable actor state, stale sequences, duplicate no-ops, and rejection replay needed separate evidence so state mutation, checkpoint advancement, cache fallback, and rebuild scheduling cannot be conflated.
  - Self-consistency and ADR review confirmed the existing story direction: keep `SingleKeyPartitionStrategy`, preserve pure handler boundaries, avoid new public query surfaces, and leave broader partitioning, freshness, search, migration, and rebuild policy decisions deferred.
  - User/persona and audience review found that developer-facing tasks needed privacy-safe, structural assertion guidance and clearer wording around current local search behavior that may exceed the MVP public search promise.
- Changes applied:
  - Added an `Advanced Elicitation Clarifications` subsection covering authorization evidence gates, local fuzzy search scope, malformed/replay/corruption branch separation, structural privacy assertions, no-op/write-boundary evidence, and anti-scope-expansion guardrails.
  - Added a change-log entry for the elicitation pass.
- Findings deferred:
  - Multi-partition routing beyond `SingleKeyPartitionStrategy`.
  - Projection schema migration/backfill and operational rebuild runbooks.
  - Public freshness/degradation contracts.
  - Semantic search and public contact/identifier search promises beyond accepted current local fallback behavior.
  - New REST/OpenAPI/MCP surfaces or EventStore authorization workarounds.
- Final recommendation: ready-for-dev
