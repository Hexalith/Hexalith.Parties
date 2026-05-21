# Story 2.7: Handle Projection Freshness and Graceful Degradation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a consumer of eventually consistent party data,
I want read responses to expose freshness and degradation status,
so that my application can behave safely when projections lag or infrastructure is partially unavailable.

## Acceptance Criteria

1. **Normal projection catch-up is observable**
   - Given a party mutation has been accepted and published,
   - When the corresponding projection event is processed under normal load,
   - Then the changed party becomes visible through detail, list, and search reads within the documented eventual consistency window,
   - And the proof covers both party detail and tenant index projection paths without aggregate replay as a read fallback.

2. **Current reads do not claim stale or degraded state**
   - Given a detail, list, or search projection is current for the latest known event position available to the read boundary,
   - When the consumer performs the accepted query/client read,
   - Then the response indicates normal/current projection state using the existing bounded contract or headers,
   - And no stale, rebuilding, unavailable, local-only, or degraded warning is included.

3. **Stale or rebuilding projections disclose bounded freshness**
   - Given detail or index projection state is stale, rebuilding, or behind the latest known event position,
   - When the consumer performs detail, list, or search queries,
   - Then the response includes a bounded freshness/degradation indicator,
   - And the result does not silently claim to be fully current,
   - And diagnostics do not expose personal data, raw projection JSON, actor/state keys, stream names, sequence keys, tenant membership payloads, or infrastructure exception text.

4. **Safe degraded reads continue from trusted projection state**
   - Given write-side components, pub/sub delivery, state-store writes, or event processing are unavailable while already-loaded projection state remains readable and tenant-safe,
   - When the consumer performs detail, list, or search reads,
   - Then reads continue only from cached or projection state whose tenant, projection, party or partition, erasure filtering, and currency provenance are proven,
   - And the response includes a bounded degraded, stale, local-only, or current-with-degraded-service indicator instead of hiding infrastructure degradation.

5. **Unsafe projection state fails closed**
   - Given projection state is corrupt, unreadable, mixed-provenance, missing tenant-safe context, malformed, stale beyond the accepted safe window, or unable to prove erasure filtering,
   - When the consumer performs detail, list, or search reads,
   - Then the service returns the accepted bounded unavailable, degraded, empty, not-found, not-accessible, or validation outcome instead of speculative party data,
   - And no fallback aggregate stream scan, detail fan-out, Memories search, retired REST endpoint, AdminPortal-only path, picker path, or MCP tool path is used to fabricate freshness.

6. **Freshness metadata is tenant-scoped and non-enumerating**
   - Given tenants have overlapping party ids, display names, stale projections, cached state, rebuild state, or degraded health,
   - When one tenant performs detail, list, or search reads,
   - Then freshness/degradation status is calculated only from that tenant's authorized projection context,
   - And the response does not reveal another tenant's existence, counts, actor keys, state keys, projection position, cache age, rebuild status, or health state.

7. **Focused tests verify current, stale, degraded, and unsafe states**
   - Given freshness/degradation tests run,
   - When they simulate current state, stale state, rebuilding state, unavailable write-side/pub-sub, unavailable projection actors, unavailable state store, corrupt projection payloads, unsafe cached data, cancellation, and cross-tenant cases,
   - Then each read path returns the documented bounded status and preserves tenant isolation.

## Acceptance Evidence and Traceability

| AC | Required evidence before review |
| --- | --- |
| AC1 | Projection/update tests prove accepted mutation events become visible through detail, list, and search reads within the documented consistency target without query-time aggregate replay. |
| AC2 | Detail/list/search query or client tests prove healthy current reads do not include stale/degraded/local-only indicators. |
| AC3 | Actor/query tests prove stale, rebuilding, and behind-position projections emit bounded freshness/degradation metadata with metadata-only diagnostics. |
| AC4 | Middleware/health/query tests prove safe degraded reads can continue from trusted current-tenant projection or cache state and expose the degraded condition. |
| AC5 | Corruption/provenance tests prove unsafe projection or cache state fails closed with bounded outcomes and no speculative fallback data source. |
| AC6 | Multi-tenant tests prove freshness, cache age, rebuild state, counts, positions, and diagnostics are tenant-scoped and non-enumerating. |
| AC7 | Focused validation commands cover health/degraded middleware, projection actor corruption/rebuild, search boundary, gateway routing, client query behavior, and architecture fitness guardrails. |

## Response Outcome Boundaries

| Scenario | Expected public outcome |
| --- | --- |
| Authorized tenant with current detail projection | Success: current `PartyDetail` through the EventStore query boundary; no stale/degraded warning. |
| Authorized tenant with current index projection | Success: current `PagedResult<PartyIndexEntry>` or `PagedResult<PartySearchResult>`; no stale/degraded warning. |
| Mutation accepted and projection processed normally | Detail, list, and search reflect the change inside the documented eventual consistency target. |
| Pub/sub degraded while projection actors remain reachable | Reads may continue from tenant-safe projection state and expose bounded degraded/stale service metadata. |
| State store write path unavailable but in-memory actor state is already loaded | Reads may continue only from trusted same-tenant actor memory/cache and expose degraded/stale metadata. |
| Projection actor unavailable or sidecar unavailable | Bounded unavailable/degraded outcome; no stale party data is presented as current. |
| Detail or index actor reports rebuilding | Safe cached data may be returned only with proven same-tenant provenance and a rebuilding/stale/degraded indicator; otherwise bounded empty/unavailable behavior. |
| Corrupt or unreadable projection state | Bounded unavailable/degraded/empty/not-accessible result; no raw projection JSON, keys, stack traces, or infrastructure exception details. |
| Mixed-provenance or untrusted cached entries | Fail closed; do not merge or return speculative entries. |
| Tenant B probes tenant A while tenant A projection is stale/degraded | Tenant B receives only tenant-B authorized outcome; no tenant-A freshness, cache age, rebuild status, counts, existence, or health signal leaks. |
| Cancellation before or during freshness/degraded read | Cancellation is honored and no fallback aggregate replay, cache refresh, detail fan-out, Memories expansion, retired REST call, or retry work starts afterward. |

## Party-Mode Review Clarifications

- Minimal freshness contract: Story 2.7 may satisfy freshness visibility through existing additive query-result metadata, existing degraded response headers, or the smallest additive metadata needed by the current EventStore-fronted client surfaces. It must not require a finalized long-term typed public freshness model.
- Bounded status vocabulary for this story is limited to coarse states such as current, stale, degraded, rebuilding, local-only, and unavailable. Exact sequence positions, stream names, actor ids, state keys, partition keys, cursor internals, tenant ids, infrastructure topology, and raw exception text must not be exposed.
- Current means the trusted projection state is within the accepted freshness bound for the authorized tenant and query path. It is not a claim of global EventStore currentness or cross-partition completeness.
- Unknown freshness is unsafe unless the read path can return a metadata-only degraded outcome from trusted tenant-scoped projection or cache state. Missing freshness metadata must not be treated as current by default.
- Trusted degraded reads may return data only when the projection/cache entry was produced by the tenant-safe read path established in Story 2.6 and proves tenant, projection kind, party or partition scope, erasure filtering, and cache currency provenance.
- Unsafe states include missing tenant/access proof, missing trusted projection or cache state, unknown tenant partition, stale state beyond the accepted safe window, rebuilding state without bounded same-tenant data, mixed-provenance cache entries, corrupt projection payloads, and infrastructure failure that prevents provenance validation.
- `DegradedResponseMiddleware` may expose coarse service degradation but must not manufacture freshness, authorize stale data, or turn unavailable infrastructure into a safe read claim.
- Detail, list/filter, and search paths must use identical tenant-safety rules. Search/list metadata, page counts, match metadata, and source metadata must be calculated only after tenant authorization, erasure filtering, freshness/provenance checks, and degraded-state classification.

## Advanced Elicitation Clarifications

- EventStore query-client reads currently use `SubmitQueryRequest` over `POST api/v1/queries`; GET-only degraded headers are not sufficient evidence for typed client freshness unless the accepted gateway path explicitly maps those headers to the query response. If the implementation keeps POST query reads, prove freshness/degradation through an accepted query wrapper, additive response metadata, or another documented EventStore-fronted signal that clients can observe.
- In-memory actor fields and static last-known caches are not provenance by themselves. Cached detail or index data can be returned only when the active actor id, state key/cache key, projection kind, party or partition scope, erasure filtering, and current request tenant are all proven before rows, counts, match metadata, or freshness status are emitted.
- Health status semantics must stay fail-closed: `dapr-sidecar` unhealthy and `projection-actors` unhealthy are unsafe read states, even if stale data exists in memory. `dapr-pubsub` degraded or state-store unavailable may expose stale/degraded read metadata only when projection actors remain reachable and the same-tenant projection/cache provenance check succeeds.
- `PartySearchExecutionStatus.LocalOnly` means the accepted local index/search path was used; it is not automatically a runtime degradation reason. Do not populate degraded reason, freshness warnings, or source metadata from local-only status unless a separate health/freshness check proves the service is degraded for this tenant-safe read.
- Freshness "latest known event position" evidence must identify the local trusted source being compared, such as projection checkpoint state or per-party sequence tracking, without exposing the raw value. If that source is missing, corrupt, canceled, or only partially loaded, the read must not claim `current`.
- Cancellation is terminal for this story's added freshness work. Once cancellation is observed, do not start health probes, projection actor calls, cache refreshes, aggregate replay, detail fan-out, search expansion, or retries to improve freshness classification.

## Tasks / Subtasks

- [x] Task 1: Audit accepted freshness/degradation surfaces before editing (AC: 1, 2, 3, 4, 7)
  - [x] Start with `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs` and `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`; detail, list, and search already post EventStore `SubmitQueryRequest` messages to `api/v1/queries`.
  - [x] Inspect the current public result shapes in `src/Hexalith.Parties.Contracts/Models/PagedResult.cs`, `PartyDetail.cs`, `PartyIndexEntry.cs`, `PartySearchResult.cs`, and any query response wrapper used by EventStore before adding fields.
  - [x] Inspect `src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs`; it already emits `X-Service-Degraded` and `X-Stale-Data-Age` for safe GET responses when health indicates stale reads can continue.
  - [x] Inspect `src/Hexalith.Parties/HealthChecks/ProjectionActorsHealthCheck.cs` and the Dapr sidecar/state-store/pub-sub health checks to understand which degraded states already map to healthy, degraded, or unhealthy.
  - [x] Treat the existing EventStore-fronted query path and degraded response middleware as the accepted boundary unless an existing EventStore query contract already defines a typed freshness metadata envelope. Do not invent a parallel REST response shape in `src/Hexalith.Parties`.
  - [x] Verify whether detail/list/search consumers observe GET responses or `POST api/v1/queries` responses; do not count GET-only degraded headers as client freshness proof for POST query reads unless the gateway explicitly propagates them.

- [x] Task 2: Define the bounded freshness contract without broad public redesign (AC: 2, 3, 4, 5)
  - [x] Reuse existing response metadata or headers when they already provide the freshness/degradation signal; otherwise add the smallest additive contract needed for detail, list, and search reads.
  - [x] Keep freshness values bounded, for example current, stale, rebuilding, degraded, unavailable, or local-only. Do not expose raw sequence numbers, actor state keys, partition keys, stream names, exact state-store keys, broker offsets, or retry exception text.
  - [x] If freshness depends on latest known event position, define the source of that "known" position explicitly and test it. Do not claim global currentness when only a local actor checkpoint is known.
  - [x] Ensure a healthy current projection does not emit stale warnings, and a degraded service condition is not hidden when read data is still returned.
  - [x] Preserve forward-compatible, additive contract changes only. Existing clients must continue deserializing current `PartyDetail`, `PagedResult<PartyIndexEntry>`, and `PagedResult<PartySearchResult>` payloads unless an accepted EventStore wrapper already carries metadata outside payload.
  - [x] Treat absent or untrusted freshness metadata as unsafe unless the path returns a metadata-only degraded/unavailable outcome without party rows, counts, match metadata, or currentness claims.
  - [x] Keep `PartySearchExecutionStatus.LocalOnly` separate from runtime degraded health; local-only status alone must not create degraded reasons, stale warnings, or infrastructure diagnostics.

- [x] Task 3: Harden detail freshness and degraded reads (AC: 1, 2, 3, 4, 5, 6)
  - [x] Inspect `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs` and `src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs`.
  - [x] Preserve actor id/state key shape `{tenant}:party-detail:{partyId}` and the no-aggregate-replay query boundary from Story 2.3.
  - [x] Use `IsRebuildingAsync`, same-actor cached detail, last-known detail, and checkpoint behavior only when the current actor id proves tenant/projection/party provenance.
  - [x] For stale or rebuilding detail reads, expose bounded status without leaking party existence across tenants or raw actor internals.
  - [x] If detail state cannot be read safely or provenance cannot be proven, return the accepted bounded null/not-found/unavailable/degraded outcome instead of falling back to aggregate stream reads.

- [x] Task 4: Harden index/list/search freshness and degraded reads (AC: 1, 2, 3, 4, 5, 6)
  - [x] Inspect `src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs`, `IPartyIndexProjectionActor.cs`, `IIndexPartitionStrategy.cs`, and `SingleKeyPartitionStrategy.cs`.
  - [x] Preserve actor id shape `{tenant}:party-index`, state key shape `{tenant}:party-index:{partitionKey}`, manifest shape, and per-party sequence key shape.
  - [x] Treat `_isRebuilding`, `_entries`, and static last-known entries as safe only for the same tenant/projection/partition key context and only after erasure filtering.
  - [x] Keep list/filter metadata and search match/score/source metadata calculated after tenant authorization, erasure filtering, stale/provenance checks, and degraded-state classification.
  - [x] Ensure unsafe stale or cached index state cannot leak rows, counts, page metadata, match metadata, source metadata, cache age, or rebuild state across tenants.

- [x] Task 5: Align health and middleware behavior with read outcomes (AC: 3, 4, 5, 7)
  - [x] Extend `DegradedResponseMiddleware` only if needed so degraded headers are applied to accepted read/query responses and withheld from unsafe/unavailable or 5xx outcomes.
  - [x] Preserve the current health-check bypass for `/health`, `/alive`, `/ready`, and `/actors/` to avoid recursive actor health invocation.
  - [x] Ensure sidecar-unavailable and projection-actor-unavailable states do not pretend stale reads are safe.
  - [x] Ensure pub/sub degraded and state-store unavailable states expose degraded/stale metadata only when projection actors can still safely serve trusted read state.
  - [x] Keep degraded diagnostics metadata-only: operation, query type, projection kind, coarse health category, stale age bucket if exposed, status code, and correlation id are acceptable.
  - [x] Assert the health registration/failure-status behavior that `DegradedResponseMiddleware` consumes, especially that sidecar-unhealthy and projection-actor-unhealthy states suppress stale-read success headers or metadata.

- [x] Task 6: Preserve adjacent boundaries and non-goals (AC: 1, 5, 6)
  - [x] Do not add public REST controllers, Swagger/OpenAPI endpoints, MCP tools, AdminPortal-only routes, picker-specific logic, or direct projection actor calls from UI surfaces.
  - [x] Do not scan aggregate streams or command status records during query-time reads to synthesize currentness.
  - [x] Do not use Memories, semantic search, graph search, contact search, identifier search, or duplicate-detection flows to fill stale list/search results.
  - [x] Keep `Hexalith.Parties.Contracts` free of dependencies on Projections, Server, Dapr, MediatR, FluentValidation, UI, Memories, or infrastructure packages.
  - [x] Do not loosen tenant-safe read behavior from Story 2.6. Freshness metadata must be subject to the same fail-closed tenant proof as party data.

- [x] Task 7: Add or harden focused tests (AC: 1-7)
  - [x] Extend `tests/Hexalith.Parties.Tests/HealthChecks/DegradedResponseMiddlewareTests.cs` for healthy current reads, safe degraded GET reads, unsafe sidecar/projection actor failures, non-GET behavior, 5xx suppression, and stale-age formatting.
  - [x] Extend `tests/Hexalith.Parties.Tests/HealthChecks/ProjectionActorsHealthCheckTests.cs` and related Dapr health-check tests for degraded versus unhealthy classification that matches read behavior.
  - [x] Extend `PartyDetailProjectionActorCorruptionTests` for rebuilding/current/stale/null/corrupt detail outcomes and privacy-safe diagnostics.
  - [x] Extend `PartyIndexProjectionActorCorruptionTests` for rebuilding/current/stale/static-cache/corrupt index outcomes, manifest fallback, partition provenance, and tenant-safe cache behavior.
  - [x] Extend `PartySearchServiceBoundaryTests`, list/filter tests, and gateway routing tests so freshness/degradation metadata is computed after tenant authorization and never leaks cross-tenant positions, counts, or cache state.
  - [x] Add client/query tests only where the public contract changes, keeping JSON payload and wrapper expectations additive and backward-compatible.
  - [x] Add cancellation tests where new async freshness checks or health probes are introduced.

## Required Test Matrix

| Scenario | Expected proof |
| --- | --- |
| Current detail read | No stale/degraded indicator and current tenant's `PartyDetail` returned. |
| Current list read | No stale/degraded indicator and page/count metadata calculated from current tenant index. |
| Current search read | No stale/degraded indicator and match/score/source metadata calculated from current tenant index. |
| Accepted mutation then projection catch-up | Detail, list, and search observe the changed party inside the documented consistency target. |
| Missing freshness metadata | No current claim is emitted; the read either fails closed or returns only a metadata-only degraded/unavailable outcome from trusted same-tenant state. |
| EventStore POST query read | Freshness/degradation is observable through the accepted query response path, not only GET-only degraded middleware headers. |
| Detail actor rebuilding with same-tenant cache | Bounded stale/rebuilding/degraded status; same-tenant cached detail only if provenance is proven. |
| Index actor rebuilding with same-tenant cache | Bounded stale/rebuilding/degraded status; rows, counts, and metadata only from proven same-tenant cache. |
| Pub/sub degraded | Safe read responses include degraded/stale signal; no data-source fallback begins. |
| State store unavailable with loaded actor state | Safe read responses use loaded same-tenant state and include degraded/stale signal. |
| Projection actor unavailable | Bounded unavailable/degraded outcome; no stale data returned as current. |
| Sidecar unavailable | No stale-read success path; readiness/health classifies the state unsafe. |
| Unknown tenant partition | Fail closed with no tenant existence, partition, count, cache age, or rebuild-state signal. |
| Corrupt detail projection | Bounded unavailable/null/not-found/degraded outcome with metadata-only diagnostics. |
| Corrupt index projection | Bounded unavailable/empty/degraded outcome with no raw JSON, rows, counts, or keys leaked. |
| Mixed-provenance static cache | Fail closed; no party rows or freshness status from another tenant. |
| Cross-tenant stale probe | Tenant B cannot infer tenant A existence, freshness, rebuild state, counts, positions, or cache age. |
| Healthy current after degraded interval | Degraded/stale headers or metadata are cleared for later current reads. |
| 5xx response while degraded | Degraded headers are not added to failed server responses. |
| Local-only search without runtime degradation | `LocalOnly` remains a search execution status with null degraded reason unless a separate trusted health/freshness signal proves degradation. |
| Cancellation | Cancellation prevents health/freshness fallback work, aggregate replay, detail fan-out, search expansion, cache refresh, and retries. |
| Diagnostics | logs, ProblemDetails, exceptions, telemetry, headers, and tests include only bounded metadata and no personal data or raw internals. |

## Dev Notes

### Current Implementation Context

- This is not a greenfield resilience story. Projection actors, projection health checks, degraded response middleware, typed query clients, search boundary services, and corruption/rebuild tests already exist.
- `HttpPartiesQueryClient.GetPartyAsync(...)`, `ListPartiesAsync(...)`, and `SearchPartiesAsync(...)` already post EventStore `SubmitQueryRequest` messages to `api/v1/queries` with `PartyDetail`, `PartyIndex`, and `PartySearch` query types.
- `PagedResult<T>` currently carries `Items`, `Page`, `PageSize`, `TotalCount`, and `TotalPages` only. Additive freshness metadata must either live in an accepted wrapper/header or be introduced carefully so existing clients are not broken.
- `DegradedResponseMiddleware` currently adds `X-Service-Degraded=true` and `X-Stale-Data-Age` on GET responses when health says stale reads can continue, and removes those headers for 5xx responses.
- `ProjectionActorsHealthCheck` currently pings index and detail projection actors through Dapr actor routing and uses the registered failure status for timeout/failure classification.
- `PartyDetailProjectionActor` maintains `_cachedDetail`, static last-known detail cache, `IsRebuildingAsync`, per-party checkpoint state, corruption detection, and a rebuild reminder. Its degraded cache is safe only when actor id shape proves tenant/projection/party context.
- `PartyIndexProjectionActor` maintains `_entries`, static last-known entries, `_isRebuilding`, manifest state, per-party sequence keys, corruption detection, and a rebuild reminder. Its static cache must remain keyed by tenant/projection/partition state key.
- `LocalPartySearchService` already materializes entries once, requires `AuthorizedPartyIds`, drops erased entries, clamps page/page size to `1..100`, aligns score/source metadata to current page items, and emits `LocalOnly` without a runtime degraded reason for local fallback. Preserve those safety properties.
- Story 2.6 has just established that tenant-safe provenance is required before projection data or degraded cache data can be returned. This story must not weaken that boundary while adding freshness visibility.

### Architecture Patterns and Constraints

- Read projections are Dapr actor-managed JSON state persisted to the Dapr state store. Detail-by-id reads target per-party detail actors; list/filter/search reads target the per-tenant index actor.
- EventStore remains the public query gateway and durable write-side source of truth. Projection reads must not scan aggregate streams or rehydrate aggregate state on demand.
- Projection-side freshness/degradation is a Parties responsibility only after EventStore public request authorization succeeds. Do not move gateway tenant/RBAC validation into Parties.
- Public failure categories must stay coarse enough to avoid existence leaks. Not-found/not-accessible is terminal for the request; unavailable/rebuilding/degraded may be retryable only when the current query boundary already exposes that distinction.
- Health/readiness endpoints and degraded response headers are infrastructure signals. They complement, but do not replace, tenant-safe result provenance checks.
- Freshness metadata is itself sensitive when it could reveal another tenant's existence or activity. Treat positions, cache ages, rebuild state, and event processing lag as current-tenant-only metadata.

### Previous Story Intelligence

- Story 2.1 established the projection replay pattern: pure handlers own deterministic state mutation, actor wrappers own Dapr state, serialized dispatch, checkpoints, rebuild/degraded mode, and metadata-only diagnostics.
- Story 2.2 established the tenant index projection, `PartyIndexEntry` shape, partition strategy abstraction, batching/checkpoint behavior, erased-entry exclusion, and user-visible list/search proof obligations.
- Story 2.3 established the EventStore-fronted query gateway as the accepted read boundary and clarified auth-before-actor-read ordering, no alternate-key probing, bounded degraded/corrupt payload behavior, and terminal cancellation.
- Story 2.4 established index-only list/filter semantics, post-filter metadata consistency, untrusted page/cursor state handling, UTC metadata filtering, degraded cache provenance, bounded validation short-circuiting, and terminal cancellation.
- Story 2.5 established MVP display-name-only search, match metadata after current-tenant and erasure filtering, degraded-cache provenance gates, negative future-field tests, and terminal cancellation.
- Story 2.6 established fail-closed tenant-safe projection reads across detail, list/filter, search, degraded cache, actor key shape, and metadata-only diagnostics.
- L08 in the story-creation lessons ledger says party-mode review and advanced elicitation are separate dated traces. This story now carries a completed party-mode trace and still needs a separate advanced elicitation pass before that hardening phase can be considered complete.

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
src/Hexalith.Parties.Contracts/Models/PagedResult.cs
src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs
src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs
src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs
src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs
src/Hexalith.Parties.Projections/Abstractions/IIndexPartitionStrategy.cs
src/Hexalith.Parties.Projections/Strategies/SingleKeyPartitionStrategy.cs
src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs
src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs
src/Hexalith.Parties/HealthChecks/ProjectionActorsHealthCheck.cs
src/Hexalith.Parties/HealthChecks/DaprStateStoreHealthCheck.cs
src/Hexalith.Parties/HealthChecks/DaprSidecarHealthCheck.cs
src/Hexalith.Parties/HealthChecks/DaprPubSubHealthCheck.cs
src/Hexalith.Parties/Search/LocalPartySearchService.cs
src/Hexalith.Parties/Search/PartySearchBoundary.cs
tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs
tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs
tests/Hexalith.Parties.Tests/HealthChecks/DegradedResponseMiddlewareTests.cs
tests/Hexalith.Parties.Tests/HealthChecks/ProjectionActorsHealthCheckTests.cs
tests/Hexalith.Parties.Tests/HealthChecks/DaprStateStoreHealthCheckTests.cs
tests/Hexalith.Parties.Tests/HealthChecks/DaprSidecarHealthCheckTests.cs
tests/Hexalith.Parties.Tests/HealthChecks/DaprPubSubHealthCheckTests.cs
tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs
tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs
tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildServiceTests.cs
tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs
tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs
```

### Testing Requirements

- Use xUnit v3 and Shouldly. Use NSubstitute for actor proxy, health check, state manager, query router, logging, and HTTP collaborators where existing tests already do.
- Keep projection handler tests infrastructure-free. Query/gateway tests can use existing EventStore gateway and fake query router patterns.
- Use synthetic placeholder personal data and assert selected structural fields rather than snapshotting full `PartyDetail`, `PartyIndexEntry`, `PartySearchResult`, health reports, or projection JSON payloads.
- Verify cancellation tokens in any new async client/query/health code. New async test code should pass `TestContext.Current.CancellationToken` where practical.
- For tenant safety, prove both "not routed before auth failure" and "wrong-tenant freshness/degraded state does not leak rows, counts, positions, cache age, or rebuild state" paths.
- When adding age/lag calculations, use UTC `DateTimeOffset`, invariant formatting, and bounded buckets or integer ranges so response values cannot overflow or imply unauthorized exact event positions.

### Suggested Validation Commands

```text
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~DegradedResponseMiddlewareTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ProjectionActorsHealthCheckTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~DaprStateStoreHealthCheckTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~DaprSidecarHealthCheckTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~DaprPubSubHealthCheckTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionActorCorruptionTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyIndexProjectionActorCorruptionTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ProjectionRebuildServiceTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartySearchServiceBoundaryTests
dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~HttpPartiesQueryClientTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitecturalFitnessTests
dotnet build Hexalith.Parties.slnx --configuration Release
```

### Anti-Patterns To Avoid

- Do not add retired detail/list/search REST endpoints, Swagger/OpenAPI, MCP tools, AdminPortal routes, picker routes, or direct UI-to-projection actor calls in this story.
- Do not read aggregate streams, rehydrate `PartyState`, fan out from index rows to detail projections, query command status records, or query Memories/semantic/graph providers to make stale reads look current.
- Do not expose raw event sequence numbers, actor keys, state keys, partition keys, stream names, broker offsets, state-store exception text, stack traces, or serialized projection JSON in public responses or diagnostics.
- Do not accept tenant id, freshness state, projection position, cache age, actor id, partition key, or degraded status from payloads, page/cursor state, query parameters, UI state, source metadata, graph context ids, `caseId`, or client-supplied metadata.
- Do not return stale/cached data unless tenant, projection kind, partition or party id, erasure filtering, and cache currency provenance are all proven.
- Do not weaken existing architectural fitness tests, privacy inventory tests, tenant-safe read tests, or health-check safety tests to make the freshness contract compile.

### Deferred Decisions

- Exact typed public freshness metadata shape remains deferred unless the current EventStore query boundary already provides or accepts an additive wrapper. Existing degraded headers may be sufficient for infrastructure-level degradation.
- Whether degraded headers are the canonical compatibility bridge, an internal transport hint, or replaced by a later typed public freshness model remains deferred. This story may only add the minimal bounded signal needed by current detail, list/filter, search, and client tests.
- Automated projection drift detection, rebuild progress reporting, health dashboards, and operational repair controls belong primarily to Story 2.8 unless a read-only signal already exists and is required for this story's bounded response.
- Multi-key partition freshness, cursor trust model, continuation-token freshness semantics, and partition-specific lag reporting remain deferred until partitioned index routing is accepted.
- Dedicated search-engine freshness, semantic/Memories index freshness, graph-search freshness, contact/identifier search freshness, and duplicate advisory metadata remain deferred to later accepted search stories.
- AdminPortal and picker user-facing copy, localization, focus behavior, and stale-response suppression are downstream UI stories unless existing server/client contract tests require a minimal compatibility signal.
- Tenant authorization policy changes remain outside Parties unless EventStore architecture explicitly moves that responsibility.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-2.7-Handle-Projection-Freshness-and-Graceful-Degradation] - Story statement and BDD acceptance criteria.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2-Tenant-Safe-Party-Search-and-Retrieval] - Epic goal and cross-story context for detail, index, list, search, freshness, rebuild, and deferred search.
- [Source: _bmad-output/planning-artifacts/prd.md#NFR21] - Cached reads with staleness indicator when the event store is unreachable.
- [Source: _bmad-output/planning-artifacts/architecture.md#Cross-Cutting-Architectural-Concerns] - Projection infrastructure, observability, graceful degradation, and projection testing rules.
- [Source: _bmad-output/planning-artifacts/architecture.md#D1-Projection-Data-Store] - Dapr actor-managed JSON state decision.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - EventStore ownership, projection-side tenant safety, privacy, testing, and actor-host guardrails.
- [Source: _bmad-output/implementation-artifacts/2-3-query-party-details-by-id.md] - Prior detail query boundary and degraded/corrupt read guidance.
- [Source: _bmad-output/implementation-artifacts/2-4-list-and-filter-parties.md] - Prior list/filter tenant index and degraded-state hardening.
- [Source: _bmad-output/implementation-artifacts/2-5-search-parties-by-display-name-with-match-metadata.md] - Prior display-name search and degraded-cache provenance guidance.
- [Source: _bmad-output/implementation-artifacts/2-6-enforce-tenant-safe-projection-reads.md] - Immediate predecessor and tenant-safe degraded read guardrails.
- [Source: src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs] - Current typed HTTP query client and EventStore query request shapes.
- [Source: src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs] - Current degraded response headers and safe-read health classification.
- [Source: src/Hexalith.Parties/HealthChecks/ProjectionActorsHealthCheck.cs] - Current projection actor responsiveness health check.
- [Source: src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs] - Existing detail actor key shape, rebuilding state, last-known cache, checkpoint, and corruption behavior.
- [Source: src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs] - Existing index actor key shape, rebuilding state, static cache, manifest, per-party sequence key, and corruption behavior.
- [Source: src/Hexalith.Parties/Search/LocalPartySearchService.cs] - Existing authorized entry gate, erased-entry filtering, page clamping, local-only status, and metadata alignment behavior.
- [Source: tests/Hexalith.Parties.Tests/HealthChecks/DegradedResponseMiddlewareTests.cs] - Current degraded header behavior tests.
- [Source: tests/Hexalith.Parties.Tests/HealthChecks/ProjectionActorsHealthCheckTests.cs] - Current projection actor health classification tests.
- [Source: tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildServiceTests.cs] - Current rebuild service behavior and checkpoint tests.
- [Source: _bmad-output/process-notes/story-creation-lessons.md#L08-Party-Review-vs-Elicitation] - Trace semantics for later hardening passes.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --configuration Release --filter "FullyQualifiedName~PartyDetailProjectionQueryActorTests|FullyQualifiedName~PartyIndexProjectionQueryActorTests|FullyQualifiedName~ProjectionFreshnessAndDegradationTests|FullyQualifiedName~DegradedResponseMiddlewareTests"` - passed 52/52.
- `dotnet test tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~HttpPartiesQueryClientTests` - passed 18/18.
- `dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --configuration Release --filter "FullyQualifiedName~ProjectionActorsHealthCheckTests|FullyQualifiedName~DaprStateStoreHealthCheckTests|FullyQualifiedName~DaprSidecarHealthCheckTests|FullyQualifiedName~DaprPubSubHealthCheckTests|FullyQualifiedName~PartyDetailProjectionActorCorruptionTests|FullyQualifiedName~PartyIndexProjectionActorCorruptionTests|FullyQualifiedName~ProjectionRebuildServiceTests|FullyQualifiedName~PartySearchServiceBoundaryTests|FullyQualifiedName~EventStoreGatewayRoutingTests|FullyQualifiedName~ArchitecturalFitnessTests|FullyQualifiedName~TenantSafeProjectionReadGuardrailsTests|FullyQualifiedName~PartyDetailProjectionActorExtensionsTests"` - passed 103/103.
- `dotnet test tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --configuration Release` - passed 81/81.
- `dotnet test tests\Hexalith.Parties.Contracts.Tests\Hexalith.Parties.Contracts.Tests.csproj --configuration Release` - passed 73/73.
- `dotnet test tests\Hexalith.Parties.Projections.Tests\Hexalith.Parties.Projections.Tests.csproj --configuration Release` - passed 138/138.
- `dotnet build src\Hexalith.Parties.Contracts\Hexalith.Parties.Contracts.csproj --configuration Release` - passed.
- `dotnet build src\Hexalith.Parties.Projections\Hexalith.Parties.Projections.csproj --configuration Release` - passed.
- `dotnet build src\Hexalith.Parties\Hexalith.Parties.csproj --configuration Release` - passed.
- `dotnet build src\Hexalith.Parties.Client\Hexalith.Parties.Client.csproj --configuration Release` - passed.
- `dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --configuration Release` - one unrelated existing failure in `AppHostTenantsTopologyTests.AppHostProgramMapsPartyDomainToPartiesActorHost` expecting the older EventStore registration environment key shape.
- `dotnet build Hexalith.Parties.slnx --configuration Release` - blocked outside story scope by missing nested `Hexalith.Commons` under `Hexalith.Memories` and existing CA2007 warnings-as-errors in `K8sStory93LintTests`.

### Completion Notes List

- Added additive typed projection freshness metadata to party detail and paged query contract payloads, with bounded status and warning-code vocabulary.
- Extended detail and tenant index projection actors with read-result methods that classify current, stale, rebuilding, degraded, unavailable, and local-only states without leaking tenant, projection, stream, checkpoint, or sequence identifiers.
- Updated query actors to propagate typed freshness metadata through EventStore-fronted POST query responses, fail closed when projection state is unsafe, and keep local-only search status separate from runtime degradation.
- Replaced the skipped freshness/degradation header privacy test with an active bounded-header assertion and added focused contract, gateway, client, and privacy inventory coverage.
- Full solution build/test validation is not clean due to unrelated repository issues recorded above; changed projects and story-targeted suites pass.

### File List

- `_bmad-output/implementation-artifacts/2-7-handle-projection-freshness-and-graceful-degradation.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.Contracts/Models/PagedResult.cs`
- `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`
- `src/Hexalith.Parties.Contracts/Models/PartyDetailProjectionReadResult.cs`
- `src/Hexalith.Parties.Contracts/Models/PartyIndexProjectionReadResult.cs`
- `src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessMetadata.cs`
- `src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessStatus.cs`
- `src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs`
- `src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs`
- `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs`
- `src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs`
- `src/Hexalith.Parties/Extensions/PartyDetailProjectionActorExtensions.cs`
- `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs`
- `src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs`
- `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs`
- `tests/Hexalith.Parties.Tests/Gateway/PartyDetailProjectionQueryActorTests.cs`
- `tests/Hexalith.Parties.Tests/Gateway/PartyIndexProjectionQueryActorTests.cs`
- `tests/Hexalith.Parties.Tests/HealthChecks/ProjectionFreshnessAndDegradationTests.cs`
## Party-Mode Review

- Date/time: 2026-05-19T16:04:53+02:00
- Selected story key: `2-7-handle-projection-freshness-and-graceful-degradation`
- Command/skill invocation used: `/bmad-party-mode 2-7-handle-projection-freshness-and-graceful-degradation; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - All reviewers initially recommended `needs-story-update`, not blocked.
  - Shared concerns were the underspecified minimal freshness/degradation contract, ambiguous current/unknown/unsafe state semantics, trusted degraded-cache proof, tenant-scoped non-enumerating metadata, and AC-to-test traceability.
  - Reviewers agreed the story remains viable if it stays additive, EventStore-fronted, metadata-only, tenant-safe, and fail-closed.
- Changes applied:
  - Added `Party-Mode Review Clarifications` covering minimal metadata/header scope, bounded status vocabulary, currentness definition, unknown-freshness handling, trusted degraded-read proof, unsafe-state examples, middleware limits, and consistent tenant-safety rules across detail/list/search.
  - Added required test-matrix rows for missing freshness metadata and unknown tenant partitions.
  - Updated Dev Notes to reflect the completed party-mode trace and remaining separate advanced elicitation pass.
  - Added a deferred decision clarifying that final header-versus-typed-metadata policy remains outside this story.
- Findings deferred:
  - Final typed public freshness metadata model and naming.
  - Whether headers are canonical, transitional, or internal-only.
  - Operational dashboards, drift reporting, repair controls, and rebuild progress UX.
  - Multi-partition freshness and cursor semantics.
  - Dedicated semantic/search-engine freshness model.
  - AdminPortal/picker UI copy, localization, and tenant authorization policy changes.
- Final recommendation: `ready-for-dev`

## Advanced Elicitation

- Date/time: 2026-05-20T16:03:52+02:00
- Selected story key: `2-7-handle-projection-freshness-and-graceful-degradation`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-7-handle-projection-freshness-and-graceful-degradation`
- Batch 1 methods: Red Team vs Blue Team, Failure Mode Analysis, Security Audit Personas, Self-Consistency Validation, Architecture Decision Records
- Batch 2 methods: Pre-mortem Analysis, Chaos Monkey Scenarios, User Persona Focus Group, Critique and Refine, Expand or Contract for Audience
- Findings summary:
  - The main hidden implementation trap is assuming existing GET degraded headers prove freshness for the EventStore `POST api/v1/queries` client path.
  - Cached actor state, static last-known dictionaries, and local-only search status need explicit provenance/status boundaries so stale data does not look authorized or current by accident.
  - Health state classification must distinguish unsafe infrastructure loss from safe degraded service conditions before any stale-read metadata or data is returned.
  - Cancellation and latest-known-position evidence need terminal, metadata-only handling to avoid background fallback work and raw sequence leakage.
- Changes applied:
  - Added `Advanced Elicitation Clarifications` covering POST query observability, cache provenance, sidecar/projection actor fail-closed health semantics, local-only search status, latest-known-position evidence, and terminal cancellation behavior.
  - Added task guidance to verify GET-versus-POST freshness proof, keep local-only separate from runtime degradation, and assert middleware health-registration behavior.
  - Added test-matrix rows for EventStore POST query observability and local-only search without runtime degradation.
- Findings deferred:
  - Final typed public freshness metadata model and naming remain deferred.
  - Whether degraded headers become canonical, transitional, or internal-only remains deferred.
  - Operational dashboards, drift reporting, repair controls, rebuild progress UX, multi-partition freshness, cursor semantics, and UI copy remain deferred to later stories.
- Final recommendation: `ready-for-dev`

## Change Log

- 2026-05-19: Story created by BMAD pre-dev hardening automation with existing EventStore query gateway, projection actors, degraded response middleware, health checks, tenant-safe cache provenance, privacy-safe diagnostics, and focused validation guidance.
- 2026-05-19: Party-mode review applied low-risk freshness-contract, unsafe-state, tenant-safety, and test-traceability clarifications; final recommendation `ready-for-dev`.
- 2026-05-20: Advanced elicitation applied low-risk POST query observability, cache-provenance, health-status, local-only search, latest-known-position, and cancellation clarifications; final recommendation `ready-for-dev`.
- 2026-05-21: Implemented additive projection freshness metadata, degradation classification, query payload propagation, privacy-safe warning codes, and focused validation coverage; moved story to `review`.
- 2026-05-21: Story-automator review (cycle 1) — fixed H1 (cancellation swallowed in stale-fallback catch clauses in `PartyDetailProjectionActor.GetDetailReadAsync` and `PartyIndexProjectionActor.GetEntriesReadAsync`), M1 (rewrote vacuous tenant header test to assert bounded-metadata invariants only), M2 (extracted warning-code constants onto `ProjectionFreshnessMetadata`). Moved story to `done`. Local build/test not re-run in this session because `dotnet` is not on the review shell PATH; targeted suites were green at submission time per Debug Log References.

## Senior Developer Review (AI)

- Reviewer: jpiquot
- Date: 2026-05-21
- Outcome: Approve with applied fixes

### Findings

- **HIGH H1 — Cancellation swallowed by stale-fallback catch clauses.** `PartyDetailProjectionActor.GetDetailReadAsync` and `PartyIndexProjectionActor.GetEntriesReadAsync` used bare `catch when (…cached…)` filters around `TryGetStateAsync`/`FlushAsync`/`LoadStateAsync`. An `OperationCanceledException` would have matched the filter and been converted into a `Stale`/`Degraded` success with cached data, violating the story's terminal-cancellation rule. **Fix applied:** added `ex is not OperationCanceledException` to the catch filters in all three sites.
- **MEDIUM M1 — Vacuous tenant freshness test.** `FreshnessMetadata_TenantScoped_DoesNotEncodeCrossTenantProjectionAgeAsync` set `X-Tenant: tenant-b` on the request, but `DegradedResponseMiddleware` does not read tenant headers, so the test gave false confidence about tenant scoping. **Fix applied:** renamed and rewrote the test as `InvokeAsync_DegradedHeaders_ContainOnlyBoundedMetadataAsync`, asserting bounded-vocabulary header shape and a parseable non-negative `X-Stale-Data-Age`; pointed to query-actor tests as the authoritative cross-tenant coverage.
- **MEDIUM M2 — Duplicated warning-code string literals.** `"projection-rebuilding"`, `"projection-state-store-unavailable"`, `"projection-context-unavailable"`, `"projection-state-unavailable"` appeared as literals across four files. A typo at one site would have silently broken the contract. **Fix applied:** added `ProjectionFreshnessMetadata.WarningProjection*` constants and replaced all literal call sites in the actor and extension/query-actor files.
- **LOW L1 — Fallback shim in `PartyDetailProjectionActorExtensions.ReadDetailWithFreshnessAsync`** can only emit `Current`/`Rebuilding`, not `Stale`. Accepted as a backward-compat path for legacy proxies; live actors return `Stale` via the new `GetDetailReadAsync` contract.

### Validation status

- Targeted test suites passed at submission per Debug Log References (52/52, 18/18, 103/103, 81/81, 73/73, 138/138).
- This review session's edits were not rebuilt locally because `dotnet` is not exposed on the PATH in the review shell. Changes are localized: catch-filter narrowing and string-to-constant substitution.
