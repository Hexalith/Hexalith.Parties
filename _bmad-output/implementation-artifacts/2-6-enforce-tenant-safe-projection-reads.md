# Story 2.6: Enforce Tenant-Safe Projection Reads

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a tenant-scoped consumer,
I want every read-side query to fail closed when tenant context is missing or mismatched,
so that party data cannot leak across tenants through projections, search, or degraded states.

## Acceptance Criteria

1. **Missing tenant context fails before projection reads**
   - Given a read query is submitted without a valid tenant identity,
   - When the query reaches the EventStore-fronted party read path,
   - Then the query is rejected fail-closed before Parties constructs a projection actor id, state key, partition key, cache key, or search/list entry set,
   - And no party detail projection, tenant index projection, local search provider, Memories provider, aggregate stream, retired REST endpoint, AdminPortal-only path, picker path, or MCP tool path is read.

2. **Authenticated tenant context is the only tenant source**
   - Given a query payload, page state, search metadata, actor id, partition key, projection payload, UI state, or client-supplied metadata includes a tenant-like value,
   - When detail, list, filter, or search projection reads are resolved,
   - Then actor ids and partition/state keys are derived only from the authenticated EventStore query tenant context,
   - And tenant identity is never accepted from request payload filters, party ids, cursors, `caseId`, graph context ids, source metadata, result metadata, headers outside the accepted gateway context, or serialized projection state.

3. **Cross-tenant detail queries are non-enumerating**
   - Given a party detail projection exists for tenant A,
   - When tenant B queries the same or similar party id through the accepted detail query path,
   - Then the service returns the accepted bounded forbidden, not-found, unavailable, or not-accessible result for tenant B,
   - And the response does not confirm tenant A existence, actor key shape, state key, projection payload, display name, contact value, identifier, or freshness status.

4. **Cross-tenant list and filter queries never leak counts or rows**
   - Given multiple tenants have overlapping display names, party ids, active statuses, party types, created dates, and modified dates,
   - When one tenant lists or filters parties,
   - Then the result set, `TotalCount`, `TotalPages`, page items, empty-page behavior, diagnostics, and errors are calculated only from entries proven to belong to the authenticated tenant,
   - And no other tenant's rows, counts, actor keys, partition keys, cached entries, or filter metadata can be inferred.

5. **Cross-tenant search and degraded states fail closed**
   - Given display-name search runs while index state is stale, rebuilding, corrupt, malformed, unreadable, partially cached, or degraded,
   - When tenant-safe checks cannot prove current-tenant provenance, partition completeness, and erasure filtering for every returned entry,
   - Then the query returns the accepted bounded unavailable, degraded, empty, forbidden, not-found, or not-accessible result instead of speculative data,
   - And match metadata, score metadata, source metadata, and page metadata are derived only after tenant authorization, erasure filtering, and current-tenant provenance checks.

6. **Projection actors and caches guard their own key shape**
   - Given a projection actor receives a malformed actor id, mismatched party id, wrong projection segment, reused partition key, or corrupted state payload,
   - When actor methods such as detail reads, index reads, serialized event handling, erasure, rebuild, cache fallback, or JSON helper reads are invoked,
   - Then the actor fails closed or returns the accepted bounded local result without reading or returning another tenant's state,
   - And cached fallback dictionaries are keyed and checked so stale or static cache data cannot cross tenant, projection, partition, or party boundaries.

7. **Diagnostics remain metadata-only**
   - Given application logging, telemetry, ProblemDetails, client exceptions, degraded responses, or test assertions record projection read activity,
   - When tenant-safe reads succeed or fail,
   - Then diagnostics may include bounded metadata such as operation, query type, tenant-safe outcome, projection kind, coarse failure category, and correlation id,
   - And diagnostics do not include personal data, raw query payloads, raw search terms, display names, contact values, identifiers, tenant membership payloads, tokens, actor keys, state keys, partition keys, stream names, serialized projection JSON, stack traces, infrastructure exception text, or connection strings.

## Acceptance Evidence and Traceability

| AC | Required evidence before review |
| --- | --- |
| AC1 | Gateway/query tests prove missing or unauthorized tenant/domain fails before query routing and before projection actor/state access. |
| AC2 | Query adapter, actor proxy, or routing tests prove tenant, actor id, partition key, and cache key derivation use authenticated EventStore context only. |
| AC3 | Detail query tests prove wrong-tenant party id reads do not reveal existence, detail payloads, keys, diagnostics, or degradation state. |
| AC4 | List/filter tests prove rows, counts, page metadata, empty pages, date filters, type filters, and active filters are calculated from the authenticated tenant's index entries only. |
| AC5 | Search/degraded tests prove match, score, source, and paging metadata are computed after tenant provenance and erasure filtering, with unsafe degraded state mapped to bounded outcomes. |
| AC6 | Projection actor tests prove malformed actor ids, party id mismatches, wrong projection segments, corrupted state, static cache fallback, rebuild, and erasure paths cannot cross tenant or partition boundaries. |
| AC7 | Logger, ProblemDetails, and client exception tests prove diagnostics are metadata-only and do not echo personal data, raw payloads, actor/state keys, stream names, or infrastructure exception text. |

## Response Outcome Boundaries

| Scenario | Expected public outcome |
| --- | --- |
| Authorized tenant with readable detail projection | Success: current tenant's `PartyDetail` through the EventStore query boundary. |
| Authorized tenant with readable index projection | Success: current tenant's `PagedResult<PartyIndexEntry>` or `PagedResult<PartySearchResult>` through the accepted list/search query boundary. |
| Missing tenant context | Fail closed before Parties constructs projection actor ids or reads actor state. |
| Unauthorized tenant or unauthorized domain | EventStore query boundary returns the accepted auth failure before Parties routing. |
| Tenant B probes tenant A party id | Bounded forbidden/not-found/not-accessible style outcome; no existence, key, count, payload, or degradation leak. |
| Tenant B lists with filters matching tenant A data | Empty or tenant-B-only result; counts and page metadata exclude tenant A. |
| Tenant B searches for tenant A display name | Empty or tenant-B-only result; match/score/source metadata exclude tenant A. |
| Client payload contains tenant id or actor/partition key | Ignored or rejected according to existing query validation; never authoritative for actor or state selection. |
| Actor id malformed or projection segment wrong | Fail closed before state write/read, or bounded null/empty result where current actor behavior defines that outcome. |
| Party id mismatch on detail actor | Fail before state write/read/checkpoint advancement. |
| Rebuilding index/detail actor with safe cached state | May return only data whose tenant/projection/partition/party provenance is proven by the actor's current key context. |
| Corrupt, malformed, unreadable, or mixed-provenance state | Bounded unavailable/degraded/empty/not-accessible result; no raw state, key, or exception text. |
| Cancellation before or during query/actor read | Cancellation is honored and no fallback aggregate replay, detail fan-out, search expansion, cache refresh, retired REST call, or retry work starts afterward. |

## Tasks / Subtasks

- [ ] Task 1: Audit the accepted read boundaries before editing (AC: 1, 2, 7)
  - [ ] Start with `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs` and `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`; detail, list, and search already post EventStore `SubmitQueryRequest` messages to `api/v1/queries`.
  - [ ] Inspect `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`; it already pins `PartyDetail`, `PartyIndex`, and `PartySearch` request shapes plus query error mapping and cancellation coverage.
  - [ ] Inspect `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs`; it already proves EventStore gateway routing and auth-before-routing behavior for party-domain queries.
  - [ ] Locate the Parties-owned query resolver/adapter that turns `PartyDetail`, `PartyIndex`, and `PartySearch` into projection actor reads. If the route already exists, harden tests instead of duplicating query plumbing.
  - [ ] Treat the EventStore query gateway as the only accepted public read boundary. Do not add `GET /api/v1/parties`, `GET /api/v1/parties/search`, public REST controllers, OpenAPI generation, or in-process MCP tools in `src/Hexalith.Parties`.

- [ ] Task 2: Enforce tenant-source authority in query adapters (AC: 1, 2, 3, 4, 5)
  - [ ] Derive detail actor ids from authenticated tenant plus requested party id only after EventStore authorization succeeds.
  - [ ] Derive index actor ids and partition keys from authenticated tenant plus accepted partition strategy only after EventStore authorization succeeds.
  - [ ] Ignore or reject any tenant-like payload field, page/cursor state, `caseId`, graph context id, source metadata, query mode, client header, UI state, actor id, or partition key that attempts to choose a tenant or projection store.
  - [ ] Ensure detail, list, filter, and search adapters do not probe alternate actor ids, alternate partitions, aggregate streams, command status records, detail fan-out, Memories search, retired REST calls, or AdminPortal-specific paths after a tenant failure.
  - [ ] Preserve EventStore ownership for public request tenant/RBAC authorization. Parties may consume authenticated context for projection lookup and projection-side filtering only; do not add Parties-owned public request authorization validators.

- [ ] Task 3: Harden detail projection read safety (AC: 1, 2, 3, 6)
  - [ ] Inspect `src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs` and `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs`.
  - [ ] Preserve actor id shape `{tenant}:party-detail:{partyId}` and state key shape `{tenant}:party-detail:{partyId}`.
  - [ ] Keep malformed actor id, wrong projection segment, and party id mismatch handling fail-closed before state reads, state writes, checkpoint writes, or cached fallback reads.
  - [ ] Verify `GetDetailAsync`, `GetSerializedDetailAsync`, and `GetDetailJsonAsync` do not become public bypasses that reveal another tenant's detail or raw serialized state.
  - [ ] Ensure degraded/cached detail reads cannot return static cache entries for the wrong tenant or party.

- [ ] Task 4: Harden tenant index read safety (AC: 1, 2, 4, 5, 6)
  - [ ] Inspect `src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs`, `src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs`, `src/Hexalith.Parties.Projections/Abstractions/IIndexPartitionStrategy.cs`, and `src/Hexalith.Parties.Projections/Strategies/SingleKeyPartitionStrategy.cs`.
  - [ ] Preserve actor id shape `{tenant}:party-index` and state key shape `{tenant}:party-index:{partitionKey}` for v1.0 single-key partitioning.
  - [ ] Ensure `ResolveStateKey`, `ResolveSequenceKey`, `GetEntriesAsync`, `GetEntriesJsonAsync`, `EraseAsync`, rebuild, and static cache fallback derive tenant and partition context from the actor id, not from caller payload.
  - [ ] Add or harden tests proving malformed actor id, wrong projection segment, reused partition key, corrupted state, and static cache fallback cannot leak tenant A entries through tenant B.
  - [ ] Preserve erasure filtering before entries leave list/search query boundaries. If actor methods expose raw entries internally, the public query adapter must still exclude `IsErased == true` before result, metadata, and diagnostics calculation.

- [ ] Task 5: Keep list/filter/search metadata tenant-scoped (AC: 4, 5, 7)
  - [ ] For list/filter, calculate `TotalCount`, `TotalPages`, empty-page behavior, date/type/active filters, and returned items from one authenticated-tenant, erased-filtered collection.
  - [ ] For search, calculate match metadata, relevance scores, score metadata, source metadata, `TotalCount`, `TotalPages`, and page items after authenticated-tenant provenance and erasure filtering.
  - [ ] Preserve `LocalPartySearchService` safety properties: materialize entries once, require `AuthorizedPartyIds`, drop erased entries, clamp page/page size to `1..100`, sanitize scores, and align score/source metadata to current page items.
  - [ ] Ensure `LocalFuzzyPartySearchProvider`, `BasicPartySearchProvider`, `MemoriesPartySearchService`, or semantic/graph providers cannot reintroduce another tenant's entries when local fallback or rich search degrades.
  - [ ] Treat cross-tenant page numbers, future cursors/tokens, graph context ids, `caseId`, Memories scopes, source URIs, and client metadata as untrusted data that cannot choose a tenant, actor, partition, index, or alternate source.

- [ ] Task 6: Bound diagnostics and adjacent surfaces (AC: 1, 7)
  - [ ] Review logger messages in projection actors, query adapters, search services, gateway tests, and client error mapping for personal data or infrastructure leaks.
  - [ ] Keep diagnostic values coarse: operation, query type, projection kind, bounded failure category, tenant-safe outcome, status code, and correlation id.
  - [ ] Do not log or return display names, search terms, contact values, identifiers, raw query payloads, tenant membership payloads, tokens, actor/state/partition keys, stream names, serialized projection JSON, stack traces, infrastructure exception text, or connection strings.
  - [ ] Keep AdminPortal and picker on their accepted typed client/query service boundaries if touched; do not let either surface call projection actors, state stores, aggregate streams, or retired endpoints directly.
  - [ ] Keep `Hexalith.Parties.Contracts` free of dependencies on Projections, Server, Dapr, MediatR, FluentValidation, UI, Memories, or infrastructure packages.

- [ ] Task 7: Add or harden focused tests (AC: 1-7)
  - [ ] Extend gateway tests for missing tenant, unauthorized tenant, unauthorized domain, and query-type coverage across `PartyDetail`, `PartyIndex`, and `PartySearch`; assert no query routing or actor read happens before authorization failure.
  - [ ] Add query adapter or actor proxy tests proving authenticated tenant context is the only source for detail/index actor id and partition key construction.
  - [ ] Add detail tests where tenant B probes tenant A party ids and receives no payload, no existence signal, and no key/diagnostic leak.
  - [ ] Add list/filter tests where tenants share display names, dates, active states, and party types; assert rows, counts, total pages, empty pages, and diagnostics are tenant-scoped.
  - [ ] Add search tests where tenants share display names and future-field metadata; assert matches, scores, source metadata, counts, and degraded outcomes are tenant-scoped.
  - [ ] Extend `PartyIndexProjectionActorCorruptionTests` and `PartyDetailProjectionActorCorruptionTests` for malformed ids, wrong projection segments, mismatch failures, cache fallback, rebuild, and corrupted-state behavior.
  - [ ] Add privacy-safe diagnostics assertions for logs, ProblemDetails/client exceptions, degraded reasons, and telemetry dimensions when tenant checks fail.

## Required Test Matrix

| Scenario | Expected proof |
| --- | --- |
| Missing tenant query | Gateway returns accepted auth failure before query routing and before actor id construction. |
| Unauthorized tenant query | Gateway returns accepted 403-style outcome before Parties query adapter or projection actor access. |
| Unauthorized domain query | Gateway returns accepted 403-style outcome before Parties query adapter or projection actor access. |
| Detail wrong tenant | Tenant B cannot infer tenant A party existence, payload, actor key, state key, or degraded state. |
| Detail actor malformed id | Actor returns bounded null/empty or throws accepted internal failure before state read/write. |
| Detail party id mismatch | Actor fails before detail state write/read and before sequence checkpoint advancement. |
| Index wrong tenant | Tenant B cannot infer tenant A entries, counts, total pages, partition keys, or cached state. |
| Index actor malformed id | Actor fails closed or returns empty without state reads from `unknown`, default, or caller-provided tenant keys. |
| Static cache fallback | Cached detail/index entries are returned only for the same tenant/projection/partition/party key context. |
| Rebuilding state | Safe cached entries require proven current-tenant provenance; otherwise bounded degraded/empty/unavailable behavior. |
| Corrupt or unreadable state | No raw actor state, serialized JSON, keys, or infrastructure exception text reaches public responses or logs. |
| List/filter metadata | `TotalCount`, `TotalPages`, items, filters, and empty pages are calculated after tenant and erasure filtering. |
| Search metadata | match metadata, score metadata, source metadata, counts, and page metadata are calculated after tenant and erasure filtering. |
| Untrusted request context | payload tenant-like fields, `caseId`, graph ids, page/cursor state, source URIs, and metadata cannot choose actor, partition, tenant, Memories scope, or alternate index. |
| Erased entries | `IsErased == true` entries are excluded before list/search results and metadata. |
| Diagnostics | logs, ProblemDetails, exceptions, and telemetry are metadata-only and contain no personal data, raw payloads, actor/state keys, stream names, or infrastructure details. |
| Cancellation | cancellation prevents fallback aggregate replay, detail fan-out, search expansion, cache refresh, retired REST calls, or retry work. |

## Dev Notes

### Current Implementation Context

- This is not a greenfield authorization story. EventStore already owns public query authentication and authorization; Parties owns projection-side tenant isolation after the authenticated tenant context reaches Parties.
- `HttpPartiesQueryClient.GetPartyAsync(...)`, `ListPartiesAsync(...)`, and `SearchPartiesAsync(...)` already post `SubmitQueryRequest` messages to `api/v1/queries` with `Domain = "party"` and query types `PartyDetail`, `PartyIndex`, and `PartySearch`.
- `HttpPartiesQueryClientTests` already pin basic detail/list/search query request shape, typed response payload handling, query error mapping, malformed payload handling, and `GetPartyAsync` cancellation.
- `EventStoreGatewayRoutingTests` already cover party-domain command/query routing plus unauthorized tenant/domain behavior before routing. Extend this existing harness rather than inventing a parallel gateway test stack.
- `PartyDetailProjectionActor` uses actor/state key shape `{tenant}:party-detail:{partyId}` and has tests for malformed actor ids, party id mismatch, corruption, rebuild, and rejection no-op behavior.
- `PartyIndexProjectionActor` uses actor id `{tenant}:party-index`, state key `{tenant}:party-index:{partitionKey}`, sequence key `{tenant}:party-index:{partyId}:last-sequence`, static last-known-entry cache, rebuilding state, corruption handling, erasure, batching, and partitioned state-key behavior.
- `LocalPartySearchService` already requires an explicit `AuthorizedPartyIds` set, drops erased entries, clamps page/page size, sanitizes invalid scores, and aligns score/source metadata to current page items.
- `PartyIndexEntry.DisplayName` and `PartyDetail.DisplayName` are `[PersonalData]`. They may appear in authorized result rows, but they must not appear in diagnostics, raw logs, ProblemDetails details, or telemetry dimensions.

### Architecture Patterns and Constraints

- Read projections are Dapr actor-managed JSON state persisted to the Dapr state store. Detail-by-id reads target per-party detail actors; list/filter/search reads target the per-tenant index actor.
- The partition strategy abstraction is the architecture boundary for index scale. V1.0 uses single-key partitioning; multi-key routing, cursor design, and migration/backfill stay deferred unless a later accepted story implements them.
- EventStore remains the public query gateway and durable write-side source of truth. Projection reads must not scan aggregate streams or rehydrate aggregate state on demand.
- Projection-side tenant isolation is a Parties responsibility, but public request-path tenant/RBAC authorization remains EventStore-owned. Do not wire `ITenantValidator`, `IRbacValidator`, or retired request-path denial translators into Parties.
- The main `src/Hexalith.Parties` project is an actor host plus EventStore gateway integration. Do not add public REST controllers, Swagger/OpenAPI endpoints, or in-process MCP tools for this story.
- Public failure categories must stay coarse enough to avoid existence leaks. Not-found/not-accessible is terminal for the request; unavailable/rebuilding/degraded may be retryable only if the current query boundary already exposes that distinction.

### Previous Story Intelligence

- Story 2.1 established the projection replay pattern: pure handlers own deterministic state mutation, actor wrappers own Dapr state, serialized dispatch, checkpoints, rebuild/degraded mode, and metadata-only diagnostics.
- Story 2.2 established the tenant index projection, `PartyIndexEntry` shape, partition strategy abstraction, batching/checkpoint behavior, erased-entry exclusion, and user-visible list/search proof obligations.
- Story 2.3 established the EventStore-fronted query gateway as the accepted read boundary and clarified auth-before-actor-read ordering, no alternate-key probing, bounded degraded/corrupt payload behavior, and terminal cancellation.
- Story 2.4 established index-only list/filter semantics, post-filter metadata consistency, untrusted page/cursor state handling, UTC metadata filtering, degraded cache provenance, bounded validation short-circuiting, and terminal cancellation.
- Story 2.5 established MVP display-name-only search, match metadata after current-tenant and erasure filtering, degraded-cache provenance gates, negative future-field tests, and terminal cancellation.
- Story 1.7 clarified bounded typed failures and privacy-safe error details. Tenant failures must not expose personal data or cross-tenant existence.
- Story 1.8 reinforced personal-data marking and log safety. Projection read diagnostics must remain metadata-only.
- Story 1.9 reinforced `PartyDetail` as the complete returned-state shape. Do not turn index rows into a parallel detail-query replacement.
- L08 in the story-creation lessons ledger says party-mode review and advanced elicitation are separate dated traces. This story has not yet received either trace.

### Latest Technical Notes

- Local source of truth for package versions is `Directory.Packages.props`: .NET SDK `10.0.300` via `global.json`, target framework `net10.0`, Dapr packages `1.17.9`, Aspire `13.3.3`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`, and Microsoft.NET.Test.Sdk `18.5.1`.
- Use `Hexalith.Parties.slnx` for solution-level build validation.
- Do not add package versions to individual project files; central package management is enabled.
- Do not recursively initialize or update nested submodules. Root-level submodules are enough unless explicitly requested.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs
src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs
src/Hexalith.Parties.Contracts/Models/PartyDetail.cs
src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs
src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs
src/Hexalith.Parties.Contracts/Models/MatchMetadata.cs
src/Hexalith.Parties.Contracts/Models/PagedResult.cs
src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs
src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs
src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs
src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs
src/Hexalith.Parties.Projections/Abstractions/IIndexPartitionStrategy.cs
src/Hexalith.Parties.Projections/Strategies/SingleKeyPartitionStrategy.cs
src/Hexalith.Parties/Search/LocalPartySearchService.cs
src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs
src/Hexalith.Parties/Search/BasicPartySearchProvider.cs
src/Hexalith.Parties/Search/PartySearchBoundary.cs
src/Hexalith.Parties/Search/MemoriesPartySearchService.cs
tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs
tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs
tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs
tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs
tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs
tests/Hexalith.Parties.Tests/Search/LocalFuzzyPartySearchProviderTests.cs
tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs
```

### Testing Requirements

- Use xUnit v3 and Shouldly. Use NSubstitute for actor proxy, state manager, query router, logging, and HTTP collaborators where existing tests already do.
- Keep projection handler tests infrastructure-free. Query/gateway tests can use the existing WebApplicationFactory and fake query router patterns.
- Use synthetic placeholder personal data and assert selected structural fields rather than snapshotting full `PartyDetail`, `PartyIndexEntry`, `PartySearchResult`, or projection JSON payloads.
- Verify cancellation tokens in any new async client/query code. New async test code should pass `TestContext.Current.CancellationToken` where practical.
- For tenant safety, prove both "not routed before auth failure" and "wrong-tenant projection does not leak entries/counts/metadata" paths.
- When adding page calculations, use long arithmetic or explicit bounds so large page/page-size inputs cannot overflow into negative skip values.

### Suggested Validation Commands

```text
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~EventStoreGatewayRoutingTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionActorCorruptionTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyIndexProjectionActorCorruptionTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartySearchServiceBoundaryTests
dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~HttpPartiesQueryClientTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitecturalFitnessTests
dotnet build Hexalith.Parties.slnx --configuration Release
```

### Anti-Patterns To Avoid

- Do not add retired list/search/detail REST endpoints, Swagger/OpenAPI, or MCP tools in this story.
- Do not read aggregate streams, rehydrate `PartyState`, fan out from index rows to detail projections, query command status records, or query Memories to prove tenant-safe projection reads.
- Do not accept tenant id from party ids, page/cursor state, query payloads, actor ids, partition keys, UI state, source metadata, graph context ids, `caseId`, or client-supplied metadata.
- Do not expose projection actor internals directly to clients.
- Do not make Contracts depend on Projections, Server, Dapr, MediatR, FluentValidation, UI, Memories, or infrastructure packages.
- Do not log personal data, raw query payloads, serialized index/detail JSON, contact values, identifiers, tokens, tenant membership payloads, actor storage keys, stream names, stack traces, or infrastructure secrets.
- Do not expand this story into projection rebuild operations, public freshness response redesign, AdminPortal UI redesign, picker behavior, semantic search, contact/identifier search, duplicate detection, MCP `find_parties`, or query language work.
- Do not weaken existing architectural fitness tests or privacy inventory tests to make tenant-safe reads compile.

### Deferred Decisions

- Public freshness/degradation response shape remains deferred unless the current EventStore query boundary already defines it.
- Multi-key partitioning, cursor design, continuation-token trust model, and projection schema migration/backfill remain deferred.
- Public exposure of rebuild progress, health monitoring, and operational repair controls belongs to Story 2.8 unless an existing boundary already exposes a safe read-only signal.
- Semantic/Memories search, graph search, contact search, identifier search, duplicate advisory metadata, and AI-oriented party finding remain deferred to later accepted stories.
- Exact AdminPortal and picker user-facing copy for missing tenant or not-accessible states remains outside this server/client hardening story unless existing UI tests require contract alignment.
- Tenant authorization policy changes remain outside Parties unless EventStore architecture changes explicitly move that responsibility.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-2.6-Enforce-Tenant-Safe-Projection-Reads] - Story statement and BDD acceptance criteria.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2-Tenant-Safe-Party-Search-and-Retrieval] - Epic goal and cross-story context for detail, index, list, search, freshness, rebuild, and deferred search.
- [Source: _bmad-output/planning-artifacts/prd.md#Party-Discovery-Search-MVP] - FR39-FR41 tenant/security context, FR14-FR18 read behavior, and FR64 graceful degradation context.
- [Source: _bmad-output/planning-artifacts/architecture.md#D1-Projection-Data-Store] - Dapr actor-managed JSON state decision.
- [Source: _bmad-output/planning-artifacts/architecture.md#D4-Projection-Actor-Granularity] - Per-party detail and per-tenant index projection split.
- [Source: _bmad-output/planning-artifacts/architecture.md#D5-Index-Actor-State-Management] - Partition strategy abstraction and single-key v1.0 strategy.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - EventStore ownership, projection-side tenant safety, privacy, testing, and actor-host guardrails.
- [Source: _bmad-output/implementation-artifacts/2-3-query-party-details-by-id.md] - Prior detail query boundary and hardening trace.
- [Source: _bmad-output/implementation-artifacts/2-4-list-and-filter-parties.md] - Prior list/filter tenant index and hardening trace.
- [Source: _bmad-output/implementation-artifacts/2-5-search-parties-by-display-name-with-match-metadata.md] - Prior display-name search, metadata, and degraded-state hardening trace.
- [Source: src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs] - Current typed HTTP query client and EventStore query request shapes.
- [Source: src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs] - Existing detail actor key shape, detail read, degraded cache, and corruption behavior.
- [Source: src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs] - Existing index actor key shape, state read, degraded cache, erasure, and corruption behavior.
- [Source: src/Hexalith.Parties/Search/LocalPartySearchService.cs] - Existing authorized entry gate, erased-entry filtering, page bounds, and metadata alignment behavior.
- [Source: tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs] - Current EventStore query gateway routing and fail-closed authorization coverage.
- [Source: tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs] - Current detail actor corruption and key-shape tests.
- [Source: tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs] - Current index actor corruption tests.
- [Source: _bmad-output/process-notes/story-creation-lessons.md#L08-Party-Review-vs-Elicitation] - Trace semantics for later hardening passes.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-05-19: Story created by BMAD pre-dev hardening automation with existing EventStore query gateway, detail/index projection actors, tenant-source authority, degraded-state, privacy-safe diagnostics, and focused validation guidance.
