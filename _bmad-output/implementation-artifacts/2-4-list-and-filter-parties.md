# Story 2.4: List and Filter Parties

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a consumer browsing party records,
I want to list parties with pagination and filters,
so that I can navigate a tenant's party directory efficiently.

## Acceptance Criteria

1. **Tenant-authenticated list query returns index-backed results**
   - Given a valid authenticated request with tenant context,
   - When the consumer lists parties through the accepted query/client boundary,
   - Then the service reads the tenant party index projection,
   - And returns `PagedResult<PartyIndexEntry>` for the current tenant only,
   - And no aggregate event stream scan, detail projection fan-out, retired REST endpoint, or cross-tenant lookup is performed.

2. **Party type filter is tenant-scoped**
   - Given the consumer provides a party type filter,
   - When the list query is executed,
   - Then the result includes only person or organization entries matching that filter,
   - And the filter is applied after tenant authorization and within the current tenant's index entries only.

3. **Active status filter includes inactive parties deliberately**
   - Given the consumer provides an active status filter,
   - When the list query is executed,
   - Then the result includes only entries matching the requested active or inactive status,
   - And deactivated parties are not silently hidden when the caller requests inactive or all parties.

4. **Date range filters use indexed metadata**
   - Given the consumer provides creation-date or last-modified-date range filters,
   - When the list query is executed,
   - Then the result includes only entries whose `CreatedAt` and `LastModifiedAt` metadata match the requested range,
   - And invalid or contradictory date ranges return a bounded validation error without reading alternate tenant state or raw projection payloads.

5. **Pagination is bounded and stable**
   - Given a tenant has more results than one page,
   - When the consumer requests a page using the documented paging contract,
   - Then the response returns only the requested page plus page metadata,
   - And the service does not return unbounded result sets, negative page metadata, overflowed offsets, or reusable cross-tenant cursor state.

6. **Stale, rebuilding, or degraded index state is bounded**
   - Given the tenant index actor is stale, rebuilding, corrupted, unreadable, or degraded,
   - When the consumer lists parties,
   - Then the response uses the accepted bounded query/search behavior for unavailable or local-only index reads,
   - And only data proven to belong to the current tenant is returned,
   - And personal data, raw index JSON, raw query payloads, tenant membership payloads, tokens, actor state keys, and infrastructure exception text are not logged or returned.

7. **Focused tests verify list/filter behavior**
   - Given list/filter tests run,
   - When they cover pagination, type filter, active filter, date filters, invalid ranges, empty results, stale/degraded index state, cancellation, erased-entry exclusion, and cross-tenant isolation,
   - Then all list behavior is verified against the tenant index projection through the accepted query/client boundary.

## Acceptance Evidence and Traceability

| AC | Required evidence before review |
| --- | --- |
| AC1 | Client/gateway/query tests prove `IPartiesQueryClient.ListPartiesAsync(...)` posts `PartyIndex` through `api/v1/queries` and reads only the current tenant's index entries. |
| AC2 | List/filter tests prove `PartyType.Person` and `PartyType.Organization` filters are applied inside the authenticated tenant result set. |
| AC3 | Tests prove active, inactive, and all-status list requests behave deliberately and do not hide inactive parties by default. |
| AC4 | Tests cover created/modified lower bounds, upper bounds, inclusive/exclusive policy as implemented, malformed values, and start-after-end validation. |
| AC5 | Tests prove page/page-size bounds, deterministic ordering, metadata calculated from the same filtered result set, empty pages, overflow-safe skip calculation, untrusted page state handling, and no unbounded result set. |
| AC6 | Actor/search/query tests prove stale/rebuilding/degraded/corrupt index state maps to bounded outcomes with privacy-safe diagnostics and tenant/partition provenance before any cached entries are returned. |
| AC7 | Focused validation commands cover typed client request shape, gateway authorization, local list/filter logic, actor read degradation, and privacy/fitness guardrails. |

## Response Outcome Boundaries

| Scenario | Expected public outcome |
| --- | --- |
| Authorized tenant with readable index | Success: `PagedResult<PartyIndexEntry>` from that tenant's index projection. |
| Missing tenant, unauthorized tenant, or unauthorized domain | EventStore query boundary blocks routing before Parties reads index actor state. |
| Tenant has no parties | Success with empty `Items`, bounded page metadata, and no fallback scans. |
| Type or active filter excludes all entries | Success with empty `Items` and metadata aligned to the filtered result set. |
| Invalid page or page size | Normalize or reject according to the accepted list boundary; never overflow offsets or return unbounded data. |
| Invalid date range | Bounded validation error with metadata-only diagnostics. |
| Cross-tenant or reused cursor/query state | Fail closed or return no results; never construct, probe, serialize, or log another tenant's index actor key. |
| Erased party in index state | Excluded from list results unless a later accepted erasure-list contract explicitly says otherwise. |
| Partial, stale, or mixed-provenance cached index state | Return only entries whose current-tenant and partition provenance is proven; otherwise use the current bounded unavailable/degraded behavior. |
| Stale/rebuilding/degraded index with safe cached entries | Preserve accepted current behavior; expose only metadata-safe degradation/local-only status if the public boundary already defines it. |
| Corrupt, malformed, or unreadable index payload | Bounded unavailable/degraded result or safe empty/not-accessible result; no raw actor/storage details. |
| Cancellation before or during index read/filtering | Cancellation is honored and no aggregate replay, detail fan-out, search expansion, or retired REST fallback starts afterward. |

## Party-Mode Review Clarifications

- The list query must derive tenant context only from the authenticated EventStore query request context. Request payloads, page state, cursors/page tokens if later introduced, AdminPortal UI state, actor ids, partition ids, and client metadata are never authoritative tenant or authorization inputs.
- `PagedResult<PartyIndexEntry>` must remain index-only. Do not enrich list rows from `PartyDetail`, aggregate state, command status records, Memories search, contact/identifier search internals, or AdminPortal-specific view models.
- Active filtering must cover explicit active-only, inactive-only, and all-status/null-filter behavior. If current accepted query behavior already defines the default, preserve it and pin it in tests; otherwise record default active visibility as a deferred product decision rather than inventing it during implementation.
- Date filters use UTC `CreatedAt` and `LastModifiedAt` metadata from `PartyIndexEntry`. Tests must pin the implemented boundary behavior for lower bounds, upper bounds, one-sided ranges, combined created/modified ranges, malformed values, and start-after-end validation before review.
- Pagination must be deterministic, bounded, and overflow-safe. Preserve any existing accepted list ordering; if the implementation must choose an order, document and test the selected index-backed sort key plus deterministic tie-breaker in this story before review. Page state and any future cursor/token value must not carry trusted tenant identity or sensitive projection internals.
- Stale, rebuilding, degraded, corrupt, malformed, null, or unreadable index state must map to existing accepted typed query outcomes where available. Do not create new public health/freshness categories, partial-result metadata, or retry semantics here unless they already exist at the EventStore query boundary.
- Diagnostics for list failures may include coarse operation name, bounded failure category, current-tenant-safe correlation metadata, and non-sensitive counts only when already accepted by existing boundaries. They must not include display names, contact values, identifiers, search terms, raw query payloads, raw index JSON, tenant membership payloads, actor/storage keys, stream names, tokens, stack traces, infrastructure exception text, or connection strings.

## Required Test Matrix

| Scenario | Expected proof |
| --- | --- |
| Client list request shape | `HttpPartiesQueryClientTests` pins `QueryType = "PartyIndex"`, `AggregateId = "parties"`, `EntityId = "parties"`, payload fields, camelCase JSON, string enum type value, and omitted null filters. |
| Authorized tenant list | Gateway/query tests prove routing uses the EventStore query gateway and the tenant-scoped index actor only. |
| Missing or unauthorized tenant/domain | Gateway tests prove no query routing or index actor read happens before authorization failure. |
| Cross-tenant isolation | Tests prove tenant B cannot infer tenant A's index contents, counts, page metadata, actor key, or existence of a matching party. |
| Type filter | Person and organization filters return only matching entries and metadata counts align to the filtered set. |
| Active filter | `active=true`, `active=false`, and `active=null` requests include the expected active/inactive entries. |
| Date filters | Created/modified after/before ranges filter using `PartyIndexEntry.CreatedAt` and `PartyIndexEntry.LastModifiedAt`. |
| Invalid date filters | Malformed values and start-after-end ranges map to bounded validation errors without raw payload echo. |
| Pagination | Page 1, later pages, empty later page, page-size clamp/rejection, zero/negative values, and large values are covered without overflow. |
| Page metadata consistency | Total count, total pages, and empty-page behavior are computed after tenant authorization, erasure exclusion, and all filters; unfiltered or cross-tenant counts never leak. |
| Untrusted page state | Page numbers, future cursors/tokens, partition keys, and UI/client metadata cannot select a tenant, partition, actor key, or alternate data source. |
| Erased entries | `IsErased == true` entries are excluded and do not leak display names, contacts, identifiers, or stale metadata. |
| Degraded/corrupt index read | Actor/query tests prove unavailable/degraded behavior is bounded and logs stay metadata-only. |
| Cancellation | Client, gateway, and local filter paths honor cancellation without starting secondary lookup work. |

## Advanced Elicitation Clarifications

- List/filter results must be a single deterministic view over authorized current-tenant index entries after erased entries and all filters are applied. `TotalCount`, `TotalPages`, empty-page behavior, and page items must come from that same filtered collection so stale unfiltered counts or cross-tenant totals cannot leak.
- Page/page-size is the only accepted paging state for this story unless an existing boundary already defines more. Any future cursor, token, partition key, UI state, or client metadata is untrusted input and must not influence tenant identity, actor selection, partition selection, authorization, or index provenance.
- Date filters must be culture-invariant UTC instant comparisons against `PartyIndexEntry.CreatedAt` and `PartyIndexEntry.LastModifiedAt`. Local-time display, localization, and user-entered date parsing belong outside the server-side list/filter contract unless an accepted client boundary already normalizes them before submission.
- Degraded or cached index reads must prove current-tenant and partition provenance before returning cached entries. If the actor cannot prove provenance, partition completeness, or safe erasure filtering, the query must use the current bounded unavailable/degraded/empty behavior rather than mixing cached entries with speculative reads.
- Bounded validation failures for malformed dates, contradictory ranges, invalid paging values, and unsupported payload shapes should short-circuit before actor reads when the current boundary allows it. Error details must remain metadata-only and must not echo raw query payloads, filters containing personal data, actor keys, storage details, or infrastructure exceptions.
- Cancellation is terminal for this story. Once cancellation is observed, implementation must not start fallback aggregate replay, detail fan-out, search expansion, cache refresh, retired REST calls, or retry work to complete the list request.

## Tasks / Subtasks

- [x] Task 1: Audit and reuse current list/query surfaces before editing (AC: 1, 5, 7)
  - [x] Start with `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs` and `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`; `ListPartiesAsync(...)` already posts an EventStore `SubmitQueryRequest` with query type `PartyIndex`.
  - [x] Inspect `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`; it already pins basic list payload shape, optional filter omission, typed payload deserialization, and query error mapping.
  - [x] Inspect `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs`; it already pins EventStore query gateway routing and auth-before-routing behavior for party-domain queries.
  - [x] Inspect `src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs` and `src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs`; they expose `GetEntriesAsync`, JSON fallback, rebuilding state, erasure, and cached degraded behavior.
  - [x] Inspect `src/Hexalith.Parties/Search/LocalPartySearchService.cs`, `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs`, and `src/Hexalith.Parties/Search/PartySearchBoundary.cs`; reuse current filtering/paging primitives where they fit rather than creating a parallel list service.
  - [x] Treat the current EventStore-fronted query path as the accepted public boundary. Do not create `GET /api/v1/parties`, list REST controllers, OpenAPI generation, or in-process MCP tools in `src/Hexalith.Parties`.

- [x] Task 2: Implement or reconcile the PartyIndex list query read path (AC: 1, 2, 3, 4, 5)
  - [x] Confirm where the EventStore query router resolves the party-domain `PartyIndex` query. If the route is already implemented, harden tests instead of duplicating it.
  - [x] If a Parties-owned resolver/adapter is missing, add the narrow adapter needed for `PartyIndex` list only, deriving the index actor id from the authenticated tenant and never from caller-supplied payload metadata.
  - [x] Use `IPartyIndexProjectionActor.GetEntriesAsync()` or the accepted extension/fallback path for index reads; do not deserialize state-store internals directly in query code.
  - [x] Apply type, active, created-date, and modified-date filters to the tenant-scoped index entries before pagination metadata is calculated.
  - [x] Define and test date-range inclusivity using the implemented behavior. Do not change public date semantics silently after tests pin them.
  - [x] Apply date filters as UTC/culture-invariant instant comparisons over `PartyIndexEntry.CreatedAt` and `PartyIndexEntry.LastModifiedAt`; do not depend on server locale or UI display formatting.
  - [x] Normalize or reject page/page-size values consistently with current query/list conventions. If `LocalPartySearchService` clamps to `1..100`, either reuse that policy or document why list queries intentionally differ.
  - [x] Calculate `TotalCount`, `TotalPages`, empty-page behavior, and returned page items from the same tenant-authorized, erased-filtered, fully filtered collection.
  - [x] Preserve existing accepted ordering. If implementation must choose an order, use a deterministic index-backed sort key plus stable tie-breaker and pin it in tests before review.
  - [x] Return `PagedResult<PartyIndexEntry>` only from index entries. Do not synthesize list rows from aggregate state, detail projection fan-out, Memories search, or command status records.

- [x] Task 3: Enforce tenant fail-closed behavior (AC: 1, 2, 6, 7)
  - [x] Keep gateway authorization ownership in EventStore. Missing/unauthorized tenant or domain must fail before Parties constructs or reads an index actor key.
  - [x] For projection-side reads, derive actor id strictly from authenticated/request tenant context. Never accept tenant id from query payload, page cursor, actor id, projection payload, UI state, or client-supplied metadata.
  - [x] Add tests where tenant A has index entries and tenant B lists with identical filters. Tenant B must receive only its own entries or a closed response, with no tenant A counts, actor key, names, or metadata.
  - [x] Add a fake/probed actor registry, proxy factory, state manager, or equivalent assertion so tests fail if `{otherTenant}:party-index` or `{otherTenant}:party-index:{partitionKey}` is constructed, read, serialized, or logged.
  - [x] Treat page numbers, future cursors/tokens, partition keys, AdminPortal page state, and client metadata as untrusted; none may override the authenticated tenant or choose an alternate partition/actor.
  - [x] Treat missing tenant context, malformed partition keys, and reused pagination/search state from another tenant as fail-closed cases.
  - [x] Ensure failures use bounded ProblemDetails/client exception fields and do not include display names, contact values, identifiers, raw query payloads, tenant membership payloads, tokens, actor storage keys, stream names, stack traces, infrastructure exception text, or serialized actor state.

- [x] Task 4: Preserve inactive, erased, degraded, and privacy-safe semantics (AC: 3, 6)
  - [x] Listing with no active filter should follow the accepted product behavior in tests; do not silently hide inactive parties if callers need a complete directory view.
  - [x] Listing with `active=false` must return inactive entries that are still authorized and not erased.
  - [x] Exclude `PartyIndexEntry.IsErased == true` entries unless a later accepted erasure-list contract explicitly defines an erased status list.
  - [x] Preserve current actor degraded/cache behavior where safe. If cached entries are returned during rebuilding, they must be tenant-scoped and filtered for erasure before leaving the boundary.
  - [x] If cached/degraded state provenance, partition completeness, or erasure filtering cannot be proven, return the current bounded unavailable/degraded/empty outcome instead of merging speculative cache data with live reads.
  - [x] Map corrupt, malformed, null, or unreadable index actor state to bounded unavailable/degraded or safe empty/not-accessible behavior according to the current EventStore query boundary.
  - [x] Ensure log messages, exception details, ProblemDetails details, query metadata, and telemetry dimensions stay metadata-only. Names, contact values, identifiers, raw JSON, serialized index entries, and secrets are not acceptable diagnostics.

- [x] Task 5: Keep AdminPortal, picker, search, and future MCP scope bounded (AC: 1, 5, 7)
  - [x] Preserve `IPartiesQueryClient.ListPartiesAsync(...)` as the typed client list shape unless a separate accepted contract update requires an additive overload.
  - [x] Keep `PartiesAdminPortalApiClient.ListPartiesAsync(...)` on the accepted FrontComposer query service path. It may call the typed client but must not call projection actors, state stores, aggregate streams, or retired REST endpoints directly.
  - [x] Do not add display-name search, contact/identifier search promises, semantic search, Memories ranking, picker behavior, or MCP `find_parties` behavior to this story. Dedicated search and AI stories own those surfaces.
  - [x] If current local search code is reused for filtering/paging, keep contact/identifier matching as an internal search implementation detail and do not make it a public list/filter promise.
  - [x] Do not expose `SearchableContactChannels` or `SearchableIdentifiers` through serialized list payloads, durable callbacks, logs, or metadata. They are `[JsonIgnore]` on `PartyIndexEntry` and should remain non-public implementation details.
  - [x] Do not add public freshness/degradation response shapes, operational rebuild runbooks, schema migrations, multi-partition routing, or projection drift detection here. Those remain later Epic 2 stories unless already accepted.

- [x] Task 6: Strengthen focused tests (AC: 1, 2, 3, 4, 5, 6, 7)
  - [x] Extend `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs` for list payload bounds, null optional filters, date serialization, query error mapping, malformed success payloads, success without payload, and cancellation.
  - [x] Extend `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` for party index query routing, missing tenant, unauthorized tenant/domain, router not-found, router forbidden, and no query routing before auth failure.
  - [x] Add or extend query/list tests around the accepted list adapter for type, active, created-date, modified-date, invalid date range, empty result, page metadata, and overflow-safe page calculations.
  - [x] Add tests proving page metadata is computed after authorization, erasure exclusion, and all filters, including cases where unfiltered counts would otherwise differ from filtered counts.
  - [x] Add tests proving untrusted page/cursor/token/partition/UI/client metadata cannot alter tenant identity, actor key construction, partition selection, or data source selection.
  - [x] Add or extend `tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs` for stale/rebuilding/degraded/corrupt reads and metadata-only diagnostics.
  - [x] Add or extend `tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs` or an adjacent list-filter test for tenant-authorized entries, erased-entry exclusion, active/type filter composition, metadata alignment, and page bounds if the local search/list service owns those mechanics.
  - [x] Add privacy-safety assertions for response/error/log text touched by this story. Assert absence of synthetic raw names, contact values, identifiers, serialized index JSON, query payloads, bearer/access tokens, tenant secrets, actor storage keys, stream names, stack traces, and infrastructure connection strings.
  - [x] Add cancellation coverage for client cancellation and cancellation during projection actor read/filtering, with assertions that no aggregate replay, detail fan-out, search expansion, or retired REST lookup starts afterward.
  - [x] Add tests or fitness assertions that fail if list behavior introduces `GET /api/v1/parties`, REST/OpenAPI/MCP surfaces, aggregate replay fallback, detail projection fan-out, Memories-only dependency, or Parties-side tenant/RBAC validators.

- [x] Task 7: Run focused validation (AC: 1, 2, 3, 4, 5, 6, 7)
  - [x] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~HttpPartiesQueryClientTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~EventStoreGatewayRoutingTests`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyIndexProjectionActor`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartySearchServiceBoundary`.
  - [x] Run `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --filter FullyQualifiedName~PartiesAdminPortalApiClientTests` if AdminPortal list/query contract mapping is touched.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitecturalFitnessTests` if boundaries, project references, Dapr exposure, REST/MCP exposure, or authorization ownership changes.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if public contracts, project references, gateway routing, or package configuration change.

## Dev Notes

### Current Implementation Context

- This is not a greenfield list story. `IPartiesQueryClient.ListPartiesAsync(...)`, `HttpPartiesQueryClient`, `PartyIndexEntry`, `PagedResult<T>`, `PartyIndexProjectionActor`, `IPartyIndexProjectionActor`, local search/list filtering code, AdminPortal list client code, and client tests already exist.
- `HttpPartiesQueryClient.ListPartiesAsync(...)` currently posts to `api/v1/queries` with `Domain = "party"`, `AggregateId = "parties"`, `QueryType = "PartyIndex"`, `EntityId = "parties"`, and a typed payload carrying page, page size, type, active, created-after/before, and modified-after/before values.
- `HttpPartiesQueryClientTests` already prove the basic `PartyIndex` request shape and omitted null filter behavior. This story should expand those tests for validation/error/cancellation behavior rather than replacing the client.
- `PartyIndexEntry` currently exposes `Id`, `Type`, `IsActive`, `[PersonalData] DisplayName`, `CreatedAt`, `LastModifiedAt`, and `IsErased`. `SearchableContactChannels` and `SearchableIdentifiers` are `[JsonIgnore]` and should not leak through list payloads.
- `PartyIndexProjectionActor` owns index actor state, cached fallback, serialized index reads, rebuilding state, corruption handling, erasure, batching, and partitioned state-key behavior. Query/list code should consume its public actor methods rather than reaching into Dapr state internals.
- `LocalPartySearchService` already materializes entries once, requires `AuthorizedPartyIds`, drops erased entries, clamps page/page size to `1..100`, and aligns score/source metadata to returned page items. Reuse its safety lessons if list filtering is implemented separately.
- `LocalFuzzyPartySearchProvider` currently searches display name, type text, contact channels, and identifiers. That broader search behavior belongs to search stories; list/filter must not promote contact/identifier matching into this story's public contract.
- `PartiesAdminPortalApiClient.ListPartiesAsync(...)` already routes through the typed query client and applies AdminPortal bounds. Keep AdminPortal on that accepted path if touched.

### Architecture Patterns and Constraints

- Read projections are Dapr actor-managed JSON state persisted to the Dapr state store. Detail-by-id reads target per-party detail actors; list/filter reads target the per-tenant index actor.
- The partition strategy abstraction is the architecture boundary for index scale. V1.0 uses the single-key strategy; multi-key routing and migration remain deferred unless explicitly implemented and tested by a later story.
- EventStore remains the public query gateway and durable write-side source of truth. List/filter queries must not scan aggregate streams or rehydrate aggregate state on demand.
- Projection-side tenant isolation is a Parties responsibility, but public request-path tenant/RBAC authorization remains EventStore-owned. Do not wire `ITenantValidator`, `IRbacValidator`, or retired request-path denial translators into Parties.
- The main `src/Hexalith.Parties` project is an actor host plus EventStore gateway integration. Do not add public REST controllers, Swagger/OpenAPI endpoints, or in-process MCP tools for this story.
- Personal data may be returned in authorized `PartyIndexEntry.DisplayName` results, but operational diagnostics, exception messages, ProblemDetails details, query metadata, telemetry dimensions, and test snapshots must not include raw display names, contact values, identifiers, raw query payloads, or serialized index JSON.
- Public failure categories must stay coarse enough to avoid existence leaks. Not-found/not-accessible is terminal for the request; unavailable/rebuilding/degraded may be retryable only if the current query boundary already exposes that distinction.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs
src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs
src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs
src/Hexalith.Parties.Contracts/Models/PagedResult.cs
src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs
src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs
src/Hexalith.Parties.Projections/Handlers/PartyIndexProjectionHandler.cs
src/Hexalith.Parties.Projections/Abstractions/IIndexPartitionStrategy.cs
src/Hexalith.Parties.Projections/Strategies/SingleKeyPartitionStrategy.cs
src/Hexalith.Parties/Search/LocalPartySearchService.cs
src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs
src/Hexalith.Parties/Search/PartySearchBoundary.cs
src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs
src/Hexalith.Parties.AdminPortal/Services/AdminPortalListRequest.cs
src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryBounds.cs
tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs
tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs
tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs
tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs
tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs
tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs
```

### Previous Story Intelligence

- Story 2.1 established the projection replay pattern: pure handlers own deterministic state mutation, actor wrappers own Dapr state, serialized dispatch, checkpoints, rebuild/degraded mode, and metadata-only diagnostics.
- Story 2.2 established the tenant index projection, `PartyIndexEntry` shape, partition strategy abstraction, batching/checkpoint behavior, erased-entry exclusion, and user-visible list/search proof obligations.
- Story 2.3 established the EventStore-fronted query gateway as the accepted read boundary and clarified auth-before-actor-read ordering, no alternate-key probing, bounded degraded/corrupt payload behavior, and terminal cancellation.
- Story 1.6 kept deactivation/reactivation as lifecycle toggles, not deletion. List/filter must preserve inactive status behavior unless an active filter or later erasure policy excludes an entry.
- Story 1.7 clarified bounded typed failures and privacy-safe error details. List failures must not expose personal data or cross-tenant existence.
- Story 1.8 reinforced personal-data marking and log safety. Index list diagnostics must remain metadata-only.
- Story 1.9 reinforced `PartyDetail` as the complete party shape. Do not turn list/index rows into a parallel detail DTO or detail-query replacement.
- L08 in the story-creation lessons ledger says party-mode review and advanced elicitation are separate dated traces. This story now carries a party-mode trace and this pass adds the separate advanced elicitation trace.

### Latest Technical Notes

- Local source of truth for package versions is `Directory.Packages.props`: .NET SDK `10.0.103`, `net10.0`, Dapr packages `1.17.9`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`, and Microsoft.NET.Test.Sdk `18.5.1`.
- Use `Hexalith.Parties.slnx` for solution-level build validation.
- Do not add package versions to individual project files; central package management is enabled.
- Do not recursively initialize or update nested submodules. Root-level submodules are enough unless explicitly requested.

### Testing Requirements

- Use xUnit v3 and Shouldly. Use NSubstitute for actor proxy, state manager, query router, logging, and HTTP collaborators where the existing tests already do.
- Keep projection handler tests infrastructure-free. Query/gateway tests can use the existing WebApplicationFactory and fake query router patterns.
- Use synthetic placeholder personal data and assert selected structural fields rather than snapshotting full `PartyIndexEntry` JSON.
- Verify cancellation tokens in any new async client/query code. New async test code should pass `TestContext.Current.CancellationToken` where practical.
- For tenant safety, prove both "not routed before auth failure" and "wrong-tenant index does not leak entries/counts" paths.
- When adding page calculations, use long arithmetic or explicit bounds so large page/page-size inputs cannot overflow into negative skip values.

### Anti-Patterns To Avoid

- Do not add retired `GET /api/v1/parties`, list REST controllers, Swagger/OpenAPI, or MCP tools in this story.
- Do not read aggregate streams, rehydrate `PartyState`, fan out to detail projections, or query Memories search to fabricate list results.
- Do not accept tenant id from page cursors, party ids, actor ids, payload fields, UI state, or client-supplied projection metadata.
- Do not expose projection actor internals directly to clients.
- Do not make Contracts depend on Projections, Server, Dapr, MediatR, FluentValidation, UI, or infrastructure packages.
- Do not log personal data, raw query payloads, serialized index JSON, contact values, identifiers, tokens, tenant membership payloads, actor storage keys, or infrastructure secrets.
- Do not expand list/filter into semantic search, contact search, identifier search, richer match metadata, AdminPortal UI redesign, picker behavior, or MCP tool behavior.
- Do not weaken existing architectural fitness tests or privacy inventory tests to make list behavior compile.

### Deferred Decisions

- Public freshness/degradation response shape remains deferred unless the current EventStore query boundary already defines it.
- Multi-key partitioning, cursor design, continuation tokens, and projection schema migration/backfill remain deferred.
- Default inactive-party visibility when no active filter is supplied remains a product decision unless already pinned by the accepted query/client behavior.
- Canonical adopter-facing sort order remains a product/architecture decision unless existing list behavior already defines it; implementation must still be deterministic and test-pinned.
- Surfacing rebuild/degraded/corrupt index state as result metadata versus typed privacy-safe failure remains deferred unless the current EventStore query boundary already defines it.
- Dedicated display-name search with match metadata is Story 2.5. Semantic/Memories search, contact search, identifier search, and AI-oriented find behavior are later stories unless a separate planning decision changes scope.
- Operational rebuild, health monitoring, and drift detection details are Story 2.8 concerns. This story may preserve actor degraded/rebuild primitives but should not design new runbooks.
- Exact AdminPortal list UI copy, density, column behavior, and empty-state design remain outside this server/client story unless existing tests require contract alignment.
- Whether inactive parties are included by default beyond the current accepted query behavior remains a product decision to preserve in tests rather than infer silently.
- New REST/OpenAPI/MCP surfaces for party index list queries are out of scope; use the accepted EventStore query/client boundaries only.
- Localized user date-entry semantics and display formatting remain client/UI concerns unless a future accepted query contract explicitly adds server-side localized date parsing.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-2.4-List-and-Filter-Parties] - Story statement and BDD acceptance criteria.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2-Tenant-Safe-Party-Search-and-Retrieval] - Epic goal and cross-story context for detail, index, list, search, freshness, rebuild, and deferred search.
- [Source: _bmad-output/planning-artifacts/prd.md#Party-Discovery-Search-MVP] - FR14 list/filter retrieval, FR39-FR41 tenant/security context, FR64 graceful degradation, and FR68 date filtering.
- [Source: _bmad-output/planning-artifacts/architecture.md#D1-Projection-Data-Store] - Dapr actor-managed JSON state decision.
- [Source: _bmad-output/planning-artifacts/architecture.md#D4-Projection-Actor-Granularity] - Per-party detail and per-tenant index projection split.
- [Source: _bmad-output/planning-artifacts/architecture.md#D5-Index-Actor-State-Management] - Partition strategy abstraction and single-key v1.0 strategy.
- [Source: _bmad-output/planning-artifacts/architecture.md#D15-Projection-Health-Monitoring-with-Auto-Rebuild-on-Corruption] - Degraded/corruption behavior for projections.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - Actor host, EventStore ownership, projection-side tenant safety, privacy, testing, and submodule guardrails.
- [Source: _bmad-output/implementation-artifacts/2-2-build-tenant-party-index-projection.md] - Prior tenant index projection story and hardening trace.
- [Source: _bmad-output/implementation-artifacts/2-3-query-party-details-by-id.md] - Prior query-boundary story and hardening trace.
- [Source: src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs] - Current typed HTTP query client and `ListPartiesAsync` request shape.
- [Source: src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs] - Existing Dapr actor wrapper, state read, degraded cache, and erasure behavior.
- [Source: src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs] - Existing lightweight index result shape and `[PersonalData]` display-name marker.
- [Source: src/Hexalith.Parties/Search/LocalPartySearchService.cs] - Existing authorized entry gate, erased-entry filtering, page bounds, and metadata alignment behavior.
- [Source: tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs] - Current typed client request and error mapping tests.
- [Source: tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs] - Current EventStore query gateway routing and fail-closed authorization coverage.
- [Source: _bmad-output/process-notes/story-creation-lessons.md#L08-Party-Review-vs-Elicitation] - Trace semantics for later hardening passes.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-19: Confirmed red phase with `dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyIndexProjectionQueryActorTests`; failure was missing `PartyIndexProjectionQueryActor`.
- 2026-05-19: Focused validation passed for `HttpPartiesQueryClientTests`, `EventStoreGatewayRoutingTests`, `PartyIndexProjectionActor`, `PartySearchServiceBoundary`, `BasicPartySearchProviderTests`, and `ArchitecturalFitnessTests`.
- 2026-05-19: `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.
- 2026-05-19: Full serialized regression passed with `dotnet test Hexalith.Parties.slnx --configuration Release --no-build -m:1` (1,317 passed, 6 Dapr-sidecar integration skips).

### Completion Notes List

- Added a Parties-owned `PartyIndexProjectionQueryActor` for EventStore-fronted `PartyIndex` list queries. It validates the query route, derives `tenant:party-index` only from the authenticated EventStore tenant context, reads through `IPartyIndexProjectionActor.GetEntriesAsync()`, and returns `PagedResult<PartyIndexEntry>`.
- Updated the typed HTTP query client to route list queries with `queryType = PartyIndex`, `projectionType = party-index`, `projectionActorType = PartyIndexProjectionQueryActor`, and `entityId = parties`.
- Hardened `PartySearchResultsBuilder.BuildPagedList(...)` for erased-entry exclusion, type/active/date filtering before metadata, inclusive UTC instant date comparisons, deterministic sort ordering, `1..100` page-size bounds, and overflow-safe skip calculation.
- Added focused tests for client request shape/cancellation, EventStore gateway routing, tenant-safe index actor key construction, invalid date ranges, active/inactive filtering, erased exclusion, metadata-after-filtering, overflow-safe paging, privacy-safe adapter logs, cancellation propagation, and architecture boundaries.

### File List

- _bmad-output/implementation-artifacts/2-4-list-and-filter-parties.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs
- src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs
- src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs
- src/Hexalith.Parties/Search/PartySearchResultsBuilder.cs
- tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs
- tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs
- tests/Hexalith.Parties.Tests/Gateway/PartyIndexProjectionQueryActorTests.cs
- tests/Hexalith.Parties.Tests/Search/BasicPartySearchProviderTests.cs

#### Incidental changes acknowledged in commit `8ada863` (P11, from code-review D1)

The following changes were carried in the same commit as the story 2.4 implementation. They are not part of the story 2.4 acceptance scope but are accepted as incidental rather than reverted:

- `Hexalith.EventStore` submodule pointer bumped (`8d740e5 → 2e68330`).
- `Hexalith.Tenants` submodule pointer bumped (`e5b03b9 → c5469fb`).
- `_bmad-output/process-notes/predev-preflight-2026-05-19T130159Z.json` (new).
- `_bmad-output/process-notes/predev-preflight-2026-05-19T140131Z.json` (new).
- `_bmad-output/process-notes/predev-preflight-latest.json` (modified).

`project-context.md` discourages submodule edits unless the task crosses that boundary; these pointer bumps did not affect any consumed Parties surface (`IPartyIndexProjectionActor`, `QueryEnvelope`, `QueryActorIdHelper` are unmodified) and the full Release regression suite remained green.

## Change Log

- 2026-05-19: Code-review closed (3 layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor). 14 patches applied (P1 strict PartyType allowlist, P2 reject empty payload, P3 reject negative Page/PageSize, P4 ToUniversalTime defensive guard, P5 malformed-date test, P6 active=null test, P7 combined+one-sided range tests, P8 strengthened PII negative-assertion with PII-laden exception message, P9 static readonly JsonOptions, P10 defensive normalization inside CreatePagedResult, P11 incidental submodule bump acknowledgment in File List, P12 relaxed ISO-8601 parser via TryParse + RoundtripKind, P13 UnmappedMemberHandling.Disallow with retargeted cross-tenant test, P14 ActorId stripped from log templates). 9 items deferred to deferred-work.md (1 new: Story 2.3 log-redaction consistency). 8 dismissed as verified false positives or pinned-by-test intentional behavior. 4 decisions resolved (D1 submodule bumps accepted+documented, D2 best-practice ISO-8601 acceptance, D3 strict JSON, D4 strict log redaction). Focused tests: PartyIndexProjectionQueryActorTests + BasicPartySearchProviderTests 22/22, EventStoreGatewayRoutingTests + PartySearchServiceBoundary + ArchitecturalFitnessTests + PartyIndexProjectionActor 49/49, HttpPartiesQueryClientTests 14/14 — 85 tests green.
- 2026-05-19: Implemented PartyIndex list query adapter, tenant-scoped filtering/paging hardening, focused tests, and full regression validation.
- 2026-05-18: Story created by BMAD pre-dev hardening automation with existing typed list client, EventStore query gateway, tenant index projection, list/filter/paging, degraded-state, privacy, and focused validation guidance.
- 2026-05-18: Party-mode review applied low-risk clarifications for tenant-source authority, index-only result shape, active/date filter semantics, deterministic pagination, degraded-state outcome boundaries, privacy-safe diagnostics, and deferred default/sort/freshness decisions.
- 2026-05-18: Advanced elicitation applied low-risk clarifications for metadata consistency, untrusted page state, UTC date filtering, degraded cache provenance, validation short-circuiting, and terminal cancellation.

## Party-Mode Review

- Date/time: 2026-05-18T09:12:45+02:00
- Selected story key: `2-4-list-and-filter-parties`
- Command/skill invocation used: `/bmad-party-mode 2-4-list-and-filter-parties; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - All reviewers initially recommended `needs-story-update`, not blocked.
  - Architecture risk centered on boundary drift: tenant identity must come from the authenticated EventStore query context, list results must come only from the tenant index projection, and no REST/OpenAPI/MCP, aggregate replay, detail fan-out, or Memories fallback should be introduced.
  - Implementation and test risk centered on ambiguous active/default behavior, date filter boundaries, deterministic pagination, degraded/corrupt index outcome mapping, and privacy-safe diagnostics.
  - Product/adopter risk centered on inactive-party discoverability, stale/rebuilding user outcomes, and admin trust in stable pagination.
- Changes applied:
  - Added `Party-Mode Review Clarifications` covering tenant-source authority, index-only `PagedResult<PartyIndexEntry>` results, active/date filter proof, deterministic bounded pagination, accepted degraded-state outcomes, and metadata-only diagnostics.
  - Extended deferred decisions for inactive default visibility, canonical sort order, and degraded/rebuild result metadata versus typed failure.
  - Added this dated party-mode trace and a change-log row.
- Findings deferred:
  - Exact default inactive-party visibility when no active filter is supplied.
  - Canonical adopter-facing sort order if no accepted ordering already exists.
  - Exact public stale/rebuilding/degraded/corrupt response shape beyond current EventStore query boundary behavior.
  - Cursor/continuation-token design and expiry, unless a later accepted paging contract introduces it.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-18T10:02:05+02:00
- Selected story key: `2-4-list-and-filter-parties`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-4-list-and-filter-parties`
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
  - The story was directionally ready after party-mode review, but residual risks remained around stale metadata counts, future cursor/page-state tenant poisoning, locale-sensitive date comparisons, unsafe degraded cache reuse, and fallback work after validation failure or cancellation.
  - The strongest hardening point is to treat the list result as one deterministic, current-tenant index view: authorize first, remove erased entries, apply filters, compute metadata, then page.
  - No elicitation method found a blocker or required product-scope expansion once cursor design, localized date semantics, richer freshness metadata, and public degraded-state categories remain deferred.
- Changes applied:
  - Added `Advanced Elicitation Clarifications` covering filtered metadata consistency, untrusted paging state, UTC/culture-invariant date filtering, degraded cache provenance, bounded validation short-circuiting, and terminal cancellation.
  - Strengthened acceptance evidence and test matrix rows for deterministic pagination, provenance-safe degraded reads, post-filter metadata, and untrusted cursor/page state.
  - Added implementation tasks for post-filter metadata calculation, stable ordering, UTC date comparison, untrusted page state, and cache provenance fail-closed behavior.
  - Added focused test expectations for filtered metadata consistency and page/cursor/token/partition/client metadata not altering tenant or actor selection.
- Findings deferred:
  - Cursor and continuation-token design, including expiry and trusted server-side state, remains deferred.
  - Localized date-entry parsing and display semantics remain client/UI concerns unless a future accepted query contract adds server-side localized parsing.
  - Canonical adopter-facing sort order remains deferred if no existing accepted list ordering already defines it.
  - Public freshness/degradation response categories and rebuild progress metadata remain deferred beyond current EventStore query boundary behavior.
- Final recommendation: ready-for-dev

## Review Findings

Date/time: 2026-05-19 (code-review)
Reviewers: Blind Hunter, Edge Case Hunter, Acceptance Auditor (parallel adversarial layers)
Diff scope: commit `8ada863` (PartyIndexProjectionQueryActor + builder hardening + tests)

### Decision Needed (resolved)

- [x] [Review][Decision] D1: Submodule pointer bumps `Hexalith.EventStore` and `Hexalith.Tenants` in commit `8ada863` — **Resolved: accept as incidental and document in story File List.** Becomes P11.
- [x] [Review][Decision] D2: `TryParseInstant` strict `"O"` format — **Resolved: relax to `DateTimeOffset.TryParse(..., DateTimeStyles.RoundtripKind)` per best practice (matches System.Text.Json default ISO-8601 acceptance).** Becomes P12.
- [x] [Review][Decision] D3: `JsonSerializerDefaults.Web` does not disallow unknown JSON fields — **Resolved: set `UnmappedMemberHandling.Disallow` and retarget cross-tenant test to assert structured rejection.** Becomes P13.
- [x] [Review][Decision] D4: ActorId in log templates — **Resolved: strip ActorId from logs in this file; add deferred item to apply consistent pattern to `PartyDetailProjectionQueryActor` (Story 2.3).** Becomes P14. Story 2.3 consistency added to deferred-work.md.

### Patches

- [x] [Review][Patch] P1: Reject `PartyType.Unknown` and numeric-string party types in `TryParsePartyType`. `Enum.TryParse(..., ignoreCase: true, ...)` accepts `"0"`, `"Unknown"`, etc. as valid because `Unknown` is a defined enum member. AC2 says the type filter must include only Person or Organization entries — `Unknown` is neither. Use a strict allowlist (switch on `"Person"`/`"Organization"`) or reject when input starts with a digit. [src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs:173-188]
- [x] [Review][Patch] P2: Reject empty/whitespace/non-object payload as `InvalidEnvelope` instead of silently defaulting. Currently `payloadBytes.Length == 0` returns wire defaults. The list query has `EntityId="parties"` (Tier 1 routing) so the gateway emits a non-empty payload — an empty payload is a bug or attack probe and should fail closed. [src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs:130-134]
- [x] [Review][Patch] P3: Reject negative `Page` or `PageSize` in `TryParsePayload` rather than silently clamping in `BuildPagedList`. The wire DTO accepts negatives and propagates them as defaults; the boundary should fail closed so client bugs surface. [src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs:161-169]
- [x] [Review][Patch] P4: Wrap `parsed.ToUniversalTime()` in a guarded try-catch (or check for `DateTimeOffset.MaxValue` with negative offset / `MinValue` with positive offset) so a parseable-but-overflowing instant returns `InvalidEnvelope` rather than escaping as `ArgumentOutOfRangeException` to the outer `catch (Exception)` → `ActorException`. [src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs:208]
- [x] [Review][Patch] P5: Add `TryParseInstant` malformed-string test in `PartyIndexProjectionQueryActorTests` — current test suite covers start-after-end inversion but not unparseable date values. Spec Required Test Matrix calls for "Malformed values and start-after-end ranges map to bounded validation errors without raw payload echo." [tests/Hexalith.Parties.Tests/Gateway/PartyIndexProjectionQueryActorTests.cs]
- [x] [Review][Patch] P6: Add `Active = null` test pinning that both active and inactive entries are returned (the current "all-status" implicit default). Party-Mode Clarification line 91 says active/null behavior must be pinned in tests OR recorded as deferred — implementation invented "all" without pinning. [tests/Hexalith.Parties.Tests/Gateway/PartyIndexProjectionQueryActorTests.cs]
- [x] [Review][Patch] P7: Add a combined `createdAfter` + `modifiedBefore` (or one-sided-range) test. Party-Mode Clarification line 92 requires "combined created/modified ranges" be test-pinned. [tests/Hexalith.Parties.Tests/Search/BasicPartySearchProviderTests.cs]
- [x] [Review][Patch] P8: Strengthen `QueryAsync_LogMessages_ContainOnlyBoundedMetadataOnReadFailureAsync` PII negative-assertion. Currently asserts `"Ada"` and `"ada@example.test"` are not in log output but the seed data in this specific test does not contain "Ada" — assertion passes vacuously. Seed display name `"Ada Lovelace"` into the input so a future log-template regression is actually caught. [tests/Hexalith.Parties.Tests/Gateway/PartyIndexProjectionQueryActorTests.cs:735-796]
- [x] [Review][Patch] P9: Convert `private static JsonSerializerOptions JsonOptions => new(JsonSerializerDefaults.Web);` (auto-property returning new instance per call) to `private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);` to avoid per-call allocation and warming the System.Text.Json metadata cache repeatedly. [tests/Hexalith.Parties.Tests/Gateway/PartyIndexProjectionQueryActorTests.cs:849]
- [x] [Review][Patch] P10: Add defensive guard in `PartySearchResultsBuilder.CreatePagedResult` so `pageSize <= 0` is clamped to ≥1 inside the helper. The new `BuildPagedList` normalizes upstream but the older `BuildSearchResults` entry point (untouched) currently depends on caller `LocalPartySearchService` to clamp. The helper itself remains division-by-zero-prone if a future caller forgets to normalize. [src/Hexalith.Parties/Search/PartySearchResultsBuilder.cs:279-295]
- [x] [Review][Patch] P11 (from D1): Add note in `File List` section acknowledging incidental `Hexalith.EventStore` and `Hexalith.Tenants` submodule pointer bumps from commit `8ada863`. No code revert.
- [x] [Review][Patch] P12 (from D2): Replace `DateTimeOffset.TryParseExact(value, "O", ...)` with `DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal, out parsed)` so any well-formed ISO-8601 form is accepted (matches System.Text.Json default behavior). [src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs:198-205]
- [x] [Review][Patch] P13 (from D3): Configure `s_jsonOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow` so payloads with unknown JSON fields return `InvalidEnvelope`. Retarget `QueryAsync_PayloadTenantCannotInfluenceIndexActorKeyAsync` to send the payload WITHOUT unknown fields and assert routing still derives the actor key from envelope tenant (the route-resolution test is what truly proves tenant authority); add a new test asserting unknown-field payload is rejected. [src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs:29; tests/Hexalith.Parties.Tests/Gateway/PartyIndexProjectionQueryActorTests.cs:735-746]
- [x] [Review][Patch] P14 (from D4): Remove `ActorId={ActorId}` from the three log templates (`PartyIndexQueryRouting`, `PartyIndexProjectionNotFound`, `PartyIndexProjectionReadFailed`) and remove the `string actorId` parameter from each `LoggerMessage`. Update call sites and the log-safety test (`QueryAsync_LogMessages_ContainOnlyBoundedMetadataOnReadFailureAsync`) to assert ActorId text is NOT present. Story 2.3 `PartyDetailProjectionQueryActor` consistency item added to deferred-work.md. [src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs:248-283]

### Deferred (pre-existing or out of current scope)

- [x] [Review][Defer] M1: `IsProjectionActorNotFound` matches Dapr "actor not found" via locale-sensitive exception-message string sniffing. Fragile across SDK upgrades but the same pattern is established in `PartyDetailProjectionQueryActor` (Story 2.3). Replace with typed Dapr exception/HTTP/gRPC status code in a follow-up. [src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs:212-226]
- [x] [Review][Defer] M2: `QueryAdapterFailureReason.InvalidEnvelope` collapses unauthorized routing, malformed payload, invalid party type, and inverted date range into one code. Distinct reasons would require a contract change in EventStore. [src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs:42,47,152,158]
- [x] [Review][Defer] M3: Date range comparisons are inclusive on both ends (`>=` and `<=`). The choice is undocumented in the wire contract; AC4 leaves "inclusive/exclusive policy as implemented" intentionally open. Document in wire DTO / OpenAPI in a follow-up. [src/Hexalith.Parties/Search/PartySearchResultsBuilder.cs:118-136]
- [x] [Review][Defer] I1: `IPartyIndexProjectionActor.GetEntriesAsync()` has no `CancellationToken`. The query actor's `OperationCanceledException` rethrow only fires if the proxy itself propagates a sidecar-side cancellation. Adding the token requires a cross-submodule interface change in `Hexalith.Parties.Projections`. [src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs:59]
- [x] [Review][Defer] I3: `EventStoreGatewayRoutingTests.PartyIndexQuery_*` covers the happy-path only. 404 / cross-tenant / malformed-payload paths are exercised by `PartyIndexProjectionQueryActorTests` in isolation, but not at the gateway+actor wiring level. [tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs:493-538]
- [x] [Review][Defer] MIN1: Magic strings `"PartyIndexProjectionQueryActor"`, `"party-index"`, `"PartyIndex"`, `"parties"`, `"party"` are duplicated between `HttpPartiesQueryClient` and `PartyIndexProjectionQueryActor`. Drift risk. Move to shared Contracts constants in a follow-up. [src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs:16-25, src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs:24-28]
- [x] [Review][Defer] A4: No dedicated test that proves page metadata is computed strictly post-filtering when unfiltered counts would otherwise leak. Indirectly covered by `QueryAsync_PartyIndex_ReadsTenantScopedIndexAndFiltersBeforePagingAsync` and `BuildPagedList_FiltersErasedTypeActiveAndDateRangesBeforeMetadata`. [tests/Hexalith.Parties.Tests/Gateway/PartyIndexProjectionQueryActorTests.cs, tests/Hexalith.Parties.Tests/Search/BasicPartySearchProviderTests.cs]
- [x] [Review][Defer] A5: `PartyIndexProjectionActorCorruptionTests` not extended for the query actor's degraded path. The query actor surfaces whatever `GetEntriesAsync()` returns/throws, and the underlying actor's stale/rebuilding/cached behavior is tested in its own test class. Add a query-actor-level integration once corruption-mode contracts solidify. [tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs]
- [x] [Review][Defer] A7: Cross-tenant probe-registry asserts only `{otherTenant}:party-index` is not constructed, not the partitioned `{otherTenant}:party-index:{partitionKey}` form. The partitioned form is unreachable in the current `SingleKeyPartitionStrategy` v1.0; defer until multi-key partitioning lands. [tests/Hexalith.Parties.Tests/Gateway/PartyIndexProjectionQueryActorTests.cs:735-746]

### Dismissed (verified false positives or noise)

- B1: TryParsePayload `byte[]` signature vs `JsonElement` — verified: `QueryEnvelope.Payload` IS `byte[]` per `Hexalith.EventStore.Contracts.Queries.QueryEnvelope:85`. `SubmitQueryRequest.Payload: JsonElement` is the client-side wire DTO that the gateway serializes to bytes.
- B2: Actor route ID format mismatch — verified: `QueryActorIdHelper.DeriveActorId("party-index", tenant, "parties", payload)` produces `"party-index:{tenant}:parties"` which matches the actor's `TryResolveActorRoute` segment expectations.
- M8: New tertiary `.ThenBy(e => e.Id, StringComparer.Ordinal)` sort is intentional for deterministic pagination per spec line 144 ("use a deterministic index-backed sort key plus stable tie-breaker").
- M9: `ArgumentNullException.ThrowIfNull` inside an exception-filter helper is unreachable — the `catch (Exception ex)` clause filters non-null exceptions only.
- MIN3: `totalPages = 1` when `totalCount == 0` is cosmetic and consistent with the search-result helper pattern.
- I2: `CorrelationId` logged cleartext — correlation IDs are not PII; standard observability practice.
- I4: `CancellationHandler` reference — verified the type exists in `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs` (referenced from another test in the same project).
- E7: `BuildPagedList` returns un-normalized `Page` in metadata — intentional; pinned by `BuildPagedList_UsesOverflowSafeSkipForLargePage` test.


