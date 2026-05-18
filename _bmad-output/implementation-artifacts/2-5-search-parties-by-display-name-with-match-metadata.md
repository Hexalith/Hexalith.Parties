# Story 2.5: Search Parties by Display Name with Match Metadata

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a consumer resolving party identity,
I want to search parties by display name with match metadata,
so that humans and AI agents can rank candidates confidently in MVP name-based lookup scenarios.

## Acceptance Criteria

1. **Tenant-authenticated search uses the accepted query boundary**
   - Given a valid authenticated request with tenant context and a display-name search term,
   - When the consumer searches parties through the accepted query/client boundary,
   - Then the service searches the tenant party index by display name,
   - And returns only matching entries for the current tenant,
   - And no aggregate event stream scan, detail projection fan-out, retired REST endpoint, AdminPortal-only path, Memories semantic search path, or cross-tenant lookup is performed.

2. **Exact display-name matches carry exact match metadata**
   - Given the search term exactly matches a party display name after the accepted normalization policy,
   - When the search result is returned,
   - Then the result includes match metadata with `MatchedField = "displayName"` and `MatchType = "exact"`,
   - And the exact match ranks ahead of prefix, contains, fuzzy, or weaker matches for the same query.

3. **Prefix and contains display-name matches are deterministic**
   - Given the search term is a prefix or contained text within one or more display names,
   - When the search result is returned,
   - Then each matching result includes `displayName` match metadata with the documented match type,
   - And results are ordered by the implemented ranking rules with a deterministic tie-breaker.

4. **MVP search does not claim unavailable fields**
   - Given a consumer expects email, contact-channel, identifier, semantic, graph, or duplicate-detection search in MVP,
   - When the search query is evaluated through the MVP `PartySearch` query contract,
   - Then the public result does not search or report those fields as matched,
   - And no result reports `email`, `contactChannel`, `identifier`, `semantic`, `graph`, duplicate, `type`, party-type, Memories-only, or other future-field match metadata,
   - And the response contract reserves `email`, `identifier`, semantic, graph, and duplicate advisory metadata for later accepted search stories.

5. **Empty and unmatched searches are bounded**
   - Given the query is empty, whitespace, too short for the accepted policy, or matches no current-tenant display names,
   - When the search result is returned,
   - Then the response is an empty bounded result set with valid paging metadata,
   - And no cross-tenant existence, personal data, raw query payload, actor key, or projection internals leak through responses, errors, logs, or telemetry.

6. **Stale, rebuilding, degraded, or corrupt index state fails closed**
   - Given the tenant index actor is stale, rebuilding, degraded, corrupt, malformed, unreadable, or partially cached,
   - When the consumer searches parties,
   - Then the response uses the accepted bounded query/search behavior for unavailable or local-only index reads,
   - And only entries with proven current-tenant provenance and safe erasure filtering may be returned,
   - And personal data, raw index JSON, raw query payloads, tenant membership payloads, tokens, actor state keys, stream names, and infrastructure exception text are not logged or returned.

7. **Focused tests verify search behavior**
   - Given search tests run,
   - When they cover exact, prefix, contains, fuzzy policy if retained, empty/whitespace, inactive, pagination, degraded/corrupt state, cancellation, erased-entry exclusion, field-reservation behavior, and cross-tenant cases,
   - Then display-name match metadata and tenant isolation behavior are verified against the tenant index projection through the accepted query/client boundary.

## Acceptance Evidence and Traceability

| AC | Required evidence before review |
| --- | --- |
| AC1 | Client/gateway/query tests prove `IPartiesQueryClient.SearchPartiesAsync(...)` posts `PartySearch` through `api/v1/queries` and reads only the current tenant's index entries. |
| AC2 | Search provider or query tests prove exact display-name matches emit `displayName` / `exact` metadata and rank above weaker matches. |
| AC3 | Tests prove prefix and contains display-name matches emit the correct match type, use deterministic ranking, and keep paging metadata aligned to the filtered result set. |
| AC4 | Tests prove MVP public search results do not search or emit `email`, `contactChannel`, `identifier`, `semantic`, `graph`, duplicate-detection, `type`, party-type, Memories-only, or other future-field match metadata unless a later accepted contract explicitly enables that mode. |
| AC5 | Tests prove empty/whitespace/no-match queries return bounded empty results and privacy-safe diagnostics. |
| AC6 | Actor/search/query tests prove stale/rebuilding/degraded/corrupt index state maps to bounded outcomes with provenance-safe cached data rules. |
| AC7 | Focused validation commands cover typed client request shape, EventStore gateway authorization, local display-name matching, degraded behavior, privacy, and architecture fitness guardrails. |

## Response Outcome Boundaries

| Scenario | Expected public outcome |
| --- | --- |
| Authorized tenant with readable index and exact display-name match | Success: `PagedResult<PartySearchResult>` from that tenant's index, match metadata contains `displayName` / `exact`. |
| Authorized tenant with prefix or contains display-name matches | Success with match metadata identifying `displayName` and the implemented match type; ordering is deterministic by match strength, normalized display name, then party id unless an existing accepted contract defines a stricter tie-breaker. |
| Missing tenant, unauthorized tenant, or unauthorized domain | EventStore query boundary blocks routing before Parties reads index actor state. |
| Empty, whitespace, or below-minimum query | Success with empty `Items` and bounded metadata, or bounded validation error if the accepted boundary already requires rejection. |
| No current-tenant display-name match | Success with empty `Items`; no fallback scans or cross-tenant probing. |
| Inactive party matches | Preserve accepted search behavior and active filter semantics; do not silently hide inactive parties when the caller requests all or inactive parties. |
| Erased party in index state | Excluded from search results and match metadata. |
| Email/contact/identifier/type-only match | Not searched or reported as an MVP public match; reserved for later accepted search-model work. |
| Semantic, graph, duplicate, or Memories-only match | Out of scope for MVP display-name search unless routed through a later accepted rich-search contract. |
| Cross-tenant or reused query/page state | Fail closed or return no results; never construct, probe, serialize, or log another tenant's index actor key. |
| Stale/rebuilding/degraded index with safe cached entries | Preserve accepted bounded local-only/degraded behavior only when current-tenant provenance, cache currency/version, and erasure filtering are proven. |
| Corrupt, malformed, null, or unreadable index payload | Bounded unavailable/degraded result or safe empty/not-accessible result; no raw actor/storage details. |
| Cancellation before or during search | Cancellation is honored and no aggregate replay, detail fan-out, Memories expansion, retired REST call, or retry work starts afterward. |

## Tasks / Subtasks

- [ ] Task 1: Audit and reuse current search/query surfaces before editing (AC: 1, 4, 7)
  - [ ] Start with `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs` and `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`; `SearchPartiesAsync(...)` already posts an EventStore `SubmitQueryRequest` with query type `PartySearch`.
  - [ ] Inspect `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`; it already pins basic search payload shape, optional mode/case id, request customization, response deserialization, and error mapping.
  - [ ] Inspect `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs`; it already proves EventStore query gateway routing and auth-before-routing behavior for party-domain queries.
  - [ ] Inspect `src/Hexalith.Parties/Search/LocalPartySearchService.cs`, `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs`, `src/Hexalith.Parties/Search/PartySearchBoundary.cs`, `src/Hexalith.Parties/Search/BasicPartySearchProvider.cs`, and any query router/adapter that resolves `PartySearch`.
  - [ ] Inspect `src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs`, `src/Hexalith.Parties.Contracts/Models/MatchMetadata.cs`, and `src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs` before changing public result shapes.
  - [ ] Treat the current EventStore-fronted query path as the accepted public boundary. Do not create `GET /api/v1/parties/search`, list/search REST controllers, OpenAPI generation, or in-process MCP tools in `src/Hexalith.Parties`.

- [ ] Task 2: Reconcile display-name-only MVP search behavior (AC: 1, 2, 3, 4)
  - [ ] Confirm where the EventStore query router resolves the party-domain `PartySearch` query. If the route is already implemented, harden tests instead of duplicating it.
  - [ ] If a Parties-owned resolver/adapter is missing, add the narrow adapter needed for `PartySearch` only, deriving the index actor id from the authenticated tenant and never from caller-supplied payload metadata.
  - [ ] Use `IPartyIndexProjectionActor.GetEntriesAsync()` or the accepted projection abstraction for index reads; do not deserialize state-store internals directly in query code.
  - [ ] Search only `PartyIndexEntry.DisplayName` for the MVP public `PartySearch` result. If an existing local provider currently matches `SearchableContactChannels`, `SearchableIdentifiers`, type text, semantic, graph, or Memories fields, either constrain that behavior behind a non-MVP rich-search mode or update it so public MVP results emit only display-name metadata.
  - [ ] Remove or disable MVP matching against contact channels, identifiers, type text, email, semantic, graph, duplicate signals, and Memories-only fields; negative tests must prove those fields cannot participate in matching or metadata.
  - [ ] Define and test the normalization policy used for exact, prefix, contains, and fuzzy matching. Preserve diacritic/case normalization only if tests pin it as accepted behavior.
  - [ ] Ensure `MatchMetadata.MatchedField` values for this story are `displayName` only. Do not emit future-reserved field names from the MVP path.
  - [ ] Ensure `MatchMetadata.MatchType` values are bounded and documented by tests, such as `exact`, `prefix`, `contains`, and optionally `fuzzy` if the implementation intentionally retains fuzzy display-name matching.
  - [ ] If fuzzy matching is retained in this story, it must be display-name-only, use `MatchedField = "displayName"`, emit no future-field metadata, rank below exact/prefix/contains, and use deterministic tie-breakers.
  - [ ] Rank exact above prefix, prefix above contains, and any retained fuzzy match below deterministic lexical matches. Use stable tie-breakers: normalized display name, then party id, unless an existing accepted contract defines a stricter order.

- [ ] Task 3: Preserve tenant, paging, inactive, and erasure semantics (AC: 1, 3, 5, 6)
  - [ ] Keep gateway authorization ownership in EventStore. Missing/unauthorized tenant or domain must fail before Parties constructs or reads an index actor key.
  - [ ] Treat EventStore as the source of public request tenant authentication and authorization. Parties may consume the authenticated tenant context for projection lookup/filtering only; do not add Parties-owned public request authorization or tenant validators.
  - [ ] Derive actor id strictly from authenticated/request tenant context. Never accept tenant id from query payload, page state, case id, graph context ids, actor ids, projection payload, UI state, or client-supplied metadata.
  - [ ] Apply erased-entry exclusion before matching, scoring, paging metadata, score metadata, and source metadata are calculated.
  - [ ] Preserve active filter behavior from `PartySearchRequest.ActiveFilter` and existing accepted query conventions. Do not silently hide inactive parties unless the caller requested active-only.
  - [ ] Normalize or reject page/page-size values consistently with current search/list conventions. `LocalPartySearchService` currently clamps page/page size to `1..100`; preserve that policy unless a stronger accepted boundary already exists.
  - [ ] Calculate `TotalCount`, `TotalPages`, returned page items, score metadata, and source metadata from the same tenant-authorized, erased-filtered, display-name-matched result set.
  - [ ] Ensure page numbers, future cursors/tokens, `caseId`, `GraphContextPartyId`, `GraphContextMemoryUnitId`, partition keys, and client metadata cannot select a tenant, partition, actor key, alternate data source, or Memories graph scope.

- [ ] Task 4: Bound degraded behavior and diagnostics (AC: 5, 6, 7)
  - [ ] Preserve current actor degraded/cache behavior where safe. If cached entries are returned during rebuilding, they must have proven tenant id provenance, cache currency/version, and erasure filtering before leaving the boundary.
  - [ ] If cached/degraded state provenance, cache currency/version, partition completeness, or erasure filtering cannot be proven, return the current bounded unavailable/degraded/empty outcome instead of merging speculative cache data with live reads.
  - [ ] Map corrupt, malformed, null, or unreadable index actor state to bounded unavailable/degraded or safe empty/not-accessible behavior according to the current EventStore query boundary.
  - [ ] Keep log messages, exception details, ProblemDetails details, query metadata, telemetry dimensions, score metadata, and source metadata metadata-only. Display names may appear only inside authorized result rows, not diagnostics.
  - [ ] Do not echo raw query strings, raw query payloads, serialized index entries, contact values, identifiers, tenant membership payloads, tokens, actor/storage keys, stream names, stack traces, infrastructure exception text, or connection strings.
  - [ ] Make cancellation terminal. Once cancellation is observed, no fallback aggregate replay, detail fan-out, Memories search, cache refresh, retired REST call, or retry work starts afterward.

- [ ] Task 5: Keep adjacent surfaces bounded (AC: 1, 4, 7)
  - [ ] Preserve `IPartiesQueryClient.SearchPartiesAsync(...)` as the typed client search shape unless a separate accepted contract update requires an additive overload.
  - [ ] Keep AdminPortal and picker behavior on their accepted query/client boundaries if touched. This story does not redesign AdminPortal search, picker typeahead, durable selection, localization, or accessibility behavior.
  - [ ] Do not add contact search, identifier search, semantic search, graph search, duplicate advisory warnings, AI disambiguation workflows, MCP tool behavior, REST/OpenAPI search endpoints, or Memories indexing changes unless needed only to prevent the current MVP public path from leaking future-reserved metadata.
  - [ ] Keep `Hexalith.Parties.Contracts` free of dependencies on Projections, Server, Dapr, MediatR, FluentValidation, UI, Memories, or infrastructure packages.
  - [ ] Preserve architectural fitness tests for actor-host boundaries, retired public surfaces, client dependency direction, and privacy inventory.

- [ ] Task 6: Add or harden focused tests (AC: 1-7)
  - [ ] Extend `HttpPartiesQueryClientTests` for omitted optional search fields, cancellation, malformed payload handling if not already covered, and field reservation expectations if response samples are updated.
  - [ ] Extend `LocalFuzzyPartySearchProviderTests` or add a display-name-specific provider test suite proving exact, prefix, contains, optional display-name-only fuzzy, deterministic ranking, tie-breaking, pagination, empty query, and no future-reserved fields in MVP mode.
  - [ ] Add negative provider/query tests proving contact channels, identifiers, email, type text, semantic, graph, duplicate, and Memories-only fields do not produce matches or match metadata in the MVP `PartySearch` path.
  - [ ] Extend `PartySearchServiceBoundaryTests` for `AuthorizedPartyIds` enforcement, erased-entry exclusion before metadata, score/source metadata alignment to current page, page bounds, cancellation, and metadata-only local/degraded status.
  - [ ] Extend EventStore gateway/query tests to prove `PartySearch` routes through `api/v1/queries`, uses authenticated tenant context, and fails before query routing on unauthorized tenant/domain.
  - [ ] Add cross-tenant tests where tenants share display names and party ids differ. Tenant B must not infer tenant A entries, counts, actor keys, source metadata, or match metadata.
  - [ ] Add degraded/corrupt/rebuilding tests against `PartyIndexProjectionActor` or its accepted query adapter so unsafe cached state does not leak.

## Required Test Matrix

| Scenario | Expected proof |
| --- | --- |
| Client search request shape | `HttpPartiesQueryClientTests` pins `QueryType = "PartySearch"`, `AggregateId = "parties"`, `EntityId = "parties"`, payload fields, camelCase JSON, optional `mode`/`caseId`, omitted nulls, and request customization. |
| Authorized tenant search | Gateway/query tests prove routing uses the EventStore query gateway and the tenant-scoped index actor only. |
| Missing or unauthorized tenant/domain | Gateway tests prove no query routing or index actor read happens before authorization failure. |
| Exact display-name match | Search tests prove `displayName` / `exact` metadata and highest ranking. |
| Prefix display-name match | Search tests prove `displayName` / `prefix` metadata and deterministic ordering. |
| Contains display-name match | Search tests prove `displayName` / `contains` metadata and deterministic ordering. |
| Optional fuzzy display-name match | If retained, tests prove it applies only to display names, uses `MatchedField = "displayName"`, is ranked below exact/prefix/contains, ignores digit-heavy false positives, and emits bounded metadata. |
| Future-reserved fields | Tests prove MVP public results do not search or emit `email`, `contactChannel`, `identifier`, `semantic`, `graph`, duplicate, `type`, party-type, or Memories-only match metadata. |
| Empty or whitespace query | Empty bounded result or accepted validation error with metadata-only diagnostics. |
| No match | Empty bounded result with no fallback scans or cross-tenant probing. |
| Active filter | Active-only, inactive-only, and all-status behavior follows accepted search/list policy. |
| Pagination and metadata | Page items, `TotalCount`, `TotalPages`, score metadata, and source metadata are calculated from the same post-filter result set. |
| Cross-tenant isolation | Same display name in tenant A and B never leaks entries, counts, actor keys, source metadata, or match metadata across tenants. |
| Erased entries | `IsErased == true` entries are excluded before matching and metadata calculation. |
| Degraded/corrupt index read | Actor/query tests prove unavailable/degraded behavior is bounded and logs stay metadata-only. |
| Cancellation | Client, gateway, and local search paths honor cancellation without starting secondary lookup work. |

## Dev Notes

### Current Implementation Context

- This is not a greenfield search story. `IPartiesQueryClient.SearchPartiesAsync(...)`, `HttpPartiesQueryClient`, `PartySearchResult`, `MatchMetadata`, `PartyIndexEntry`, `LocalPartySearchService`, `LocalFuzzyPartySearchProvider`, `BasicPartySearchProvider`, rich/Memories search code, and search tests already exist.
- `HttpPartiesQueryClient.SearchPartiesAsync(...)` currently posts to `api/v1/queries` with `Domain = "party"`, `AggregateId = "parties"`, `QueryType = "PartySearch"`, `EntityId = "parties"`, and a typed payload carrying query, page, page size, mode, and case id.
- `PartySearchResult` currently returns a `PartyIndexEntry`, `IReadOnlyList<MatchMetadata>`, and `RelevanceScore`. `MatchMetadata` carries `MatchedField`, `MatchType`, and optional `Score`.
- `PartyIndexEntry.DisplayName` is marked `[PersonalData]`; it may appear in authorized result rows, but it must not appear in diagnostics. `SearchableContactChannels` and `SearchableIdentifiers` are `[JsonIgnore]` and must not become public payloads through this story.
- `LocalFuzzyPartySearchProvider` currently evaluates display names, contact channels, identifiers, and type text, and can emit field names such as `email`, `contactChannel`, `identifier`, and `type`. That is broader than Story 2.5's MVP display-name public contract. Reconcile this deliberately: the MVP `PartySearch` path must evaluate `DisplayName` only and must not emit future-field metadata.
- `LocalPartySearchService` already materializes entries once, requires `AuthorizedPartyIds`, drops erased entries, clamps page/page size to `1..100`, aligns score/source metadata to current page items, and reports `LocalOnly` without a degraded reason. Preserve those safety properties if the search adapter changes.
- Existing Memories/semantic/graph search classes may be present from earlier exploration. They are not the accepted MVP display-name public behavior for this story unless routed behind an explicit rich-search contract and tested separately.

### Architecture Patterns and Constraints

- Read projections are Dapr actor-managed JSON state persisted to the Dapr state store. Search reads target the per-tenant party index projection, not aggregate streams.
- EventStore remains the public query gateway and durable write-side source of truth. Search queries must not rehydrate aggregate state on demand or bypass the EventStore gateway.
- Projection-side tenant isolation is a Parties responsibility, but public request-path tenant/RBAC authorization remains EventStore-owned. Do not wire `ITenantValidator`, `IRbacValidator`, or retired request-path denial translators into Parties.
- The main `src/Hexalith.Parties` project is an actor host plus EventStore gateway integration. Do not add public REST controllers, Swagger/OpenAPI endpoints, or in-process MCP tools for this story.
- Personal data may be returned only in authorized search result rows. Operational diagnostics, exception messages, ProblemDetails details, query metadata, telemetry dimensions, source metadata, and test snapshots must not include raw display names, contact values, identifiers, raw query payloads, or serialized index JSON.
- Public failure categories must stay coarse enough to avoid existence leaks. Not-found/not-accessible is terminal for the request; unavailable/rebuilding/degraded may be retryable only if the current query boundary already exposes that distinction.

### Previous Story Intelligence

- Story 2.1 established the projection replay pattern: pure handlers own deterministic state mutation, actor wrappers own Dapr state, serialized dispatch, checkpoints, rebuild/degraded mode, and metadata-only diagnostics.
- Story 2.2 established the tenant index projection, `PartyIndexEntry` shape, partition strategy abstraction, batching/checkpoint behavior, erased-entry exclusion, and user-visible list/search proof obligations.
- Story 2.3 established the EventStore-fronted query gateway as the accepted read boundary and clarified auth-before-actor-read ordering, no alternate-key probing, bounded degraded/corrupt payload behavior, and terminal cancellation.
- Story 2.4 established index-only list/filter semantics, post-filter metadata consistency, untrusted page/cursor state handling, UTC metadata filtering, degraded cache provenance, bounded validation short-circuiting, and terminal cancellation.
- Story 1.7 clarified bounded typed failures and privacy-safe error details. Search failures must not expose personal data or cross-tenant existence.
- Story 1.8 reinforced personal-data marking and log safety. Search diagnostics must remain metadata-only.
- Story 1.9 is currently in active implementation. Do not depend on unmerged 1.9 source changes unless they are already in the working tree and the implementing dev intentionally coordinates them.
- L08 in the story-creation lessons ledger says party-mode review and advanced elicitation are separate dated traces. This story has not yet been reviewed by party mode or advanced elicitation.

### Latest Technical Notes

- Local source of truth for package versions is `Directory.Packages.props`: .NET SDK `10.0.300`, `net10.0`, Dapr packages `1.17.9`, Aspire `13.3.3`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`, and Microsoft.NET.Test.Sdk `18.5.1`.
- Use `Hexalith.Parties.slnx` for solution-level build validation.
- Do not add package versions to individual project files; central package management is enabled.
- Do not recursively initialize or update nested submodules. Root-level submodules are enough unless explicitly requested.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs
src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs
src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs
src/Hexalith.Parties.Contracts/Models/MatchMetadata.cs
src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs
src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs
src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs
src/Hexalith.Parties/Search/LocalPartySearchService.cs
src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs
src/Hexalith.Parties/Search/BasicPartySearchProvider.cs
src/Hexalith.Parties/Search/PartySearchBoundary.cs
src/Hexalith.Parties/Search/MemoriesPartySearchService.cs
src/Hexalith.Parties/Search/SemanticPartySearchProvider.cs
tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs
tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs
tests/Hexalith.Parties.Tests/Search/LocalFuzzyPartySearchProviderTests.cs
tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs
tests/Hexalith.Parties.Tests/Search/BasicPartySearchProviderTests.cs
tests/Hexalith.Parties.Tests/Search/MemoriesPartySearchServiceTests.cs
tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs
tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs
```

### Testing Requirements

- Use xUnit v3 and Shouldly. Use NSubstitute for actor proxy, state manager, query router, logging, and HTTP collaborators where the existing tests already do.
- Keep projection handler tests infrastructure-free. Query/gateway tests can use the existing WebApplicationFactory and fake query router patterns.
- Use synthetic placeholder personal data and assert selected structural fields rather than snapshotting full `PartySearchResult` or `PartyIndexEntry` JSON.
- Verify cancellation tokens in any new async client/query code. New async test code should pass `TestContext.Current.CancellationToken` where practical.
- For tenant safety, prove both "not routed before auth failure" and "wrong-tenant index does not leak entries/counts/metadata" paths.
- When adding page calculations, use long arithmetic or explicit bounds so large page/page-size inputs cannot overflow into negative skip values.

### Anti-Patterns To Avoid

- Do not add retired `GET /api/v1/parties/search`, search REST controllers, Swagger/OpenAPI, or MCP tools in this story.
- Do not read aggregate streams, rehydrate `PartyState`, fan out to detail projections, or query Memories/semantic/graph search to fabricate MVP display-name results.
- Do not accept tenant id from page state, case id, graph context ids, actor ids, payload fields, UI state, or client-supplied metadata.
- Do not expose projection actor internals directly to clients.
- Do not make Contracts depend on Projections, Server, Dapr, MediatR, FluentValidation, UI, Memories, or infrastructure packages.
- Do not log personal data, raw query payloads, serialized index JSON, contact values, identifiers, tokens, tenant membership payloads, actor storage keys, stream names, or infrastructure secrets.
- Do not expand this story into semantic search, Memories search, contact search, identifier search, duplicate detection, MCP `find_parties`, AdminPortal UI redesign, picker behavior, or query language work.
- Do not weaken existing architectural fitness tests or privacy inventory tests to make search behavior compile.

### Deferred Decisions

- Email, contact-channel, identifier, semantic, graph, and duplicate-detection search remain deferred from the MVP public display-name search contract unless a separate accepted rich-search story changes the contract.
- Public freshness/degradation response shape remains deferred unless the current EventStore query boundary already defines it.
- Multi-key partitioning, cursor design, continuation tokens, source metadata semantics, and projection schema migration/backfill remain deferred.
- Canonical adopter-facing sort order remains a product/architecture decision unless existing search behavior already defines it; implementation must still be deterministic and test-pinned.
- Whether fuzzy display-name matching is part of the MVP public contract remains an implementation/product decision if not already accepted; exact, prefix, and contains matching are required by this story. If retained now, fuzzy matching is display-name-only and must not imply contact, identifier, semantic, graph, duplicate, or party-type intelligence.
- Exact minimum query length and empty-query behavior remain tied to the accepted query/client boundary; implementation must pin the behavior in tests rather than infer it silently.
- MCP `find_parties` AI-specific orchestration belongs to Epic 4. Story 2.5 provides the underlying display-name search contract and match metadata only.
- Localized user-entered search normalization beyond current case/diacritic handling remains client/UI concern unless a future accepted query contract adds locale-aware server behavior.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-2.5-Search-Parties-by-Display-Name-with-Match-Metadata] - Story statement and BDD acceptance criteria.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2-Tenant-Safe-Party-Search-and-Retrieval] - Epic goal and cross-story context for detail, index, list, search, freshness, rebuild, and deferred search.
- [Source: _bmad-output/planning-artifacts/prd.md#Party-Discovery-Search-MVP] - FR15 display-name search, FR17 match metadata, FR39-FR41 tenant/security context, and FR64 graceful degradation.
- [Source: _bmad-output/planning-artifacts/prd.md#MVP-Minimum-Viable-Product] - MVP boundary that email, identifier, semantic search, and duplicate detection are deferred.
- [Source: _bmad-output/planning-artifacts/architecture.md#D1-Projection-Data-Store] - Dapr actor-managed JSON state decision.
- [Source: _bmad-output/planning-artifacts/architecture.md#D4-Projection-Actor-Granularity] - Per-party detail and per-tenant index projection split.
- [Source: _bmad-output/planning-artifacts/architecture.md#D5-Index-Actor-State-Management] - Partition strategy abstraction and single-key v1.0 strategy.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - Actor host, EventStore ownership, projection-side tenant safety, privacy, testing, and submodule guardrails.
- [Source: _bmad-output/implementation-artifacts/2-4-list-and-filter-parties.md] - Prior list/filter story and hardening trace.
- [Source: src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs] - Current typed HTTP query client and `SearchPartiesAsync` request shape.
- [Source: src/Hexalith.Parties/Search/LocalPartySearchService.cs] - Current local search boundary, authorized-id gate, erased-entry filtering, page clamping, and metadata alignment.
- [Source: src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs] - Current local matching implementation and future-field metadata drift to reconcile.
- [Source: src/Hexalith.Parties.Contracts/Models/MatchMetadata.cs] - Current match metadata contract.
- [Source: tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs] - Current typed client search request and error mapping tests.
- [Source: tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs] - Current search boundary metadata tests.
- [Source: _bmad-output/process-notes/story-creation-lessons.md#L08-Party-Review-vs-Elicitation] - Trace semantics for later hardening passes.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Party-Mode Review

- Date/time: 2026-05-18T21:06:37+02:00
- Selected story key: `2-5-search-parties-by-display-name-with-match-metadata`
- Command/skill invocation used: `/bmad-party-mode 2-5-search-parties-by-display-name-with-match-metadata; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary: All reviewers initially recommended `needs-story-update`, not `blocked`. The shared risk was contract drift from the existing broad `LocalFuzzyPartySearchProvider`: implementation could accidentally preserve contact-channel, identifier, type, semantic, graph, duplicate, or Memories matching and emit future-field metadata in the MVP display-name path. Reviewers also called out fuzzy-scope ambiguity, deterministic ranking/tie-breaker gaps, EventStore-versus-Parties tenant ownership, degraded-cache provenance proof, privacy-safe diagnostics, and negative field-reservation tests.
- Changes applied: Tightened AC4 and AC evidence so MVP `PartySearch` searches and reports `displayName` only; added explicit negative metadata requirements for email, contact, identifier, semantic, graph, duplicate, type, party-type, and Memories-only fields; defined deterministic ordering as match strength, normalized display name, then party id unless a stricter accepted contract exists; clarified optional fuzzy matching as display-name-only with `MatchedField = "displayName"` and lower rank than exact/prefix/contains; clarified EventStore owns public request tenant authorization while Parties consumes authenticated tenant context for projection lookup/filtering; strengthened degraded-cache wording to require tenant provenance, cache currency/version, and erasure filtering before returning cached entries; added negative provider/query tests for future-field non-participation.
- Findings deferred: Email/contact/identifier/semantic/graph/duplicate search, broader fuzzy strategy, duplicate/candidate clustering metadata, ranking explainability beyond basic match metadata, public freshness/degradation response shape, rebuild recovery strategy, and any public REST/OpenAPI/MCP exposure remain deferred to later accepted stories or architecture decisions.
- Final recommendation: `ready-for-dev`

## Change Log

- 2026-05-18: Party-mode review applied low-risk clarifications for display-name-only search, optional fuzzy scope, deterministic ordering, EventStore tenant ownership, degraded-cache provenance, and future-field negative tests.
- 2026-05-18: Story created by BMAD pre-dev hardening automation with existing typed search client, EventStore query gateway, tenant index projection, display-name match metadata, MVP future-field reservation, degraded-state, privacy, and focused validation guidance.
