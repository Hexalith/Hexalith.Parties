# Sprint Change Proposal - Use Hexalith.Memories for Parties Search

**Project:** Hexalith.Parties  
**Date:** 2026-05-02  
**Mode:** Batch  
**Status:** Approved - story split applied  
**Change Trigger:** Use `Hexalith.Memories` for Parties semantic, graph, and lexical search.

## 1. Issue Summary

### Trigger

Hexalith.Parties search should use `Hexalith.Memories` as the dedicated search capability for semantic, graph, and lexical retrieval instead of continuing with a Parties-owned "semantic" fuzzy/token provider or a future Elasticsearch/OpenSearch adapter.

### Current Problem

The current Parties plan says email, identifier, and semantic search are deferred to a dedicated search capability, but Story 9.5 in implementation review introduced an in-process `SemanticPartySearchProvider` over the Parties DAPR actor index. That implementation improves fuzzy matching, but it is not true semantic search and duplicates capability already delivered by `Hexalith.Memories`.

This creates four conflicts:

- Parties is building a local search stack while `Hexalith.Memories` already owns syntactic/BM25, semantic/vector, graph traversal, hybrid fusion, score explanations, index health, and per-tenant search telemetry.
- Architecture Decision D2 says search is a separate concern with a dedicated engine, but Story 9.5 keeps the default search implementation inside Parties.
- The PRD still uses placeholder language like "dedicated search capability" and "Elasticsearch/OpenSearch" instead of naming the actual Hexalith ecosystem module.
- GDPR erasure verification now needs to include the external Memories party-search corpus, not only Parties projections/caches.

### Evidence

- `Hexalith.Memories` README describes the module as providing syntactic and semantic search and exposes three-axis search through `memories search query`.
- `Hexalith.Memories` architecture defines tiered latency targets: syntactic <200ms, semantic <500ms, hybrid <1s, graph <2s.
- Memories contracts include `SearchRequest`, `HybridSearchRequest`, `SearchResult`, `HybridSearchResult`, `ScoredResult`, `FusedScoredResult`, `SourceType.Event`, `MemoryUnit`, and graph traversal DTOs.
- Memories client exposes `MemoriesClient.SearchAsync`, `HybridSearchAsync`, `TraverseAsync`, `IngestAsync`, `GetMemoryUnitAsync`, consistency verification, and repair methods.
- Parties Story 9.5 currently registers `IPartySearchProvider -> SemanticPartySearchProvider`, uses fuzzy/name matching, and documents Elasticsearch/OpenSearch as the future swap target.
- Parties code already has temporal name query work in Story 9.5; that part is still a Parties read-model concern and should be preserved.

## 2. Impact Analysis

### Checklist Status

| Item | Status | Notes |
|---|---:|---|
| 1.1 Triggering story | [x] | Story 9.5: Semantic Search & Temporal Name Queries. |
| 1.2 Core problem | [x] | Parties is duplicating search instead of delegating semantic/graph/lexical search to Hexalith.Memories. |
| 1.3 Evidence | [x] | Parties PRD/architecture/story/code and Memories README/contracts/client/architecture reviewed. |
| 2.1 Current epic impact | [x] | Epic 9 remains valid, but Story 9.5 must be split: keep temporal name queries, replace Parties-owned search with Memories integration. |
| 2.2 Epic-level changes | [x] | Add a focused Memories search integration story/epic or revise Story 9.5 before it moves from review to done. |
| 2.3 Remaining epic impact | [x] | Epic 10 admin search and party picker should consume the same Memories-backed search when enabled. |
| 2.4 Future epic validity | [x] | No epic is obsolete. The future Elasticsearch/OpenSearch language should be removed or demoted to a Memories backend concern. |
| 2.5 Priority/order | [x] | Apply before Epic 9 closure and before Epic 10 UI work. |
| 3.1 PRD conflicts | [x] | FR16/FR20/NFR16/GDPR erasure wording must name Hexalith.Memories. |
| 3.2 Architecture conflicts | [x] | D2 and solution structure need a Memories integration boundary, not a local semantic provider as the default. |
| 3.3 UX conflicts | [x] | Future admin portal and party picker need a baseline-vs-Memories-enabled search state. |
| 3.4 Other artifacts | [!] | Story 9.5 file, sprint status, tests, docs, deployment validation, and AppHost composition need follow-up edits after approval. |
| 4.1 Direct adjustment | Viable | Revise Story 9.5 and add a small integration story. Effort: Medium. Risk: Medium. |
| 4.2 Potential rollback | Viable, limited | Revert or disable only the local semantic provider pieces from Story 9.5. Preserve temporal name query work. |
| 4.3 MVP review | Not viable | MVP scope remains valid. This is a module-boundary correction, not a scope reduction. |
| 4.4 Recommended path | [x] | Hybrid: direct adjustment plus targeted rollback/supersession of local semantic provider. |
| 5.1-5.5 Proposal components | [x] | Captured below. |
| 6.1-6.5 Final review/handoff | [!] | Pending user approval. Do not update sprint status or backlog until approved. |

### Epic Impact

**Epic 3: Party Discovery & Search**

No change to completed MVP baseline. Keep display-name list/search, projection health, match metadata, and date filtering as the resilient local fallback. Do not expand the Epic 3 index into email/identifier/semantic search.

**Epic 5: AI Agent Party Management**

`find_parties` should keep simple display-name search/list behavior when Memories search is disabled or degraded. When Memories search is enabled, `find_parties` should delegate semantic/lexical/hybrid search to Memories and map the resulting memory units back to party summaries/details.

**Epic 9: GDPR Compliance**

Story 9.5 must be revised. Temporal name queries remain in Parties. Semantic/lexical/graph search becomes a Memories integration:

- Ingest party search documents/memory units into Memories from Parties events or projection changes.
- Query Memories from REST/MCP search surfaces.
- Purge or tombstone party memory units during erasure verification.
- Surface degraded search behavior when Memories is unavailable.

**Epic 10: Administration & Frontend**

Admin browse/search and embeddable party picker should offer:

- Baseline display-name search from Parties local projection.
- Rich search when Memories integration is active: lexical, semantic, hybrid, and graph-assisted results.

### Technical Impact

- Add `Hexalith.Memories.Client.Rest` and `Hexalith.Memories.Contracts` references only to integration-facing projects, likely `Hexalith.Parties.CommandApi`, `Hexalith.Parties.Projections` or a new integration project, AppHost, and tests.
- Keep `Hexalith.Parties.Contracts` free of Memories dependencies.
- Introduce a Parties-owned abstraction such as `IPartySearchService` or `IPartySearchBackend` that can use either local projection search or Memories.
- Add a party-to-memory indexing adapter that maps party events/projections into Memories `IngestionInput` with `SourceType.Event`, stable `SourceUri`, and metadata such as party id, tenant id, event type, aggregate id, display name, party type, contact channel values, identifier values, active/erased status, and timestamps.
- Store a mapping from party id and/or party event id to Memories memory unit ids so results can hydrate `PartySearchResult` and so erasure can purge all indexed units for a party.
- Use `MemoriesClient.HybridSearchAsync` for default rich search; use `SearchAsync(axis: "syntactic")` for lexical-only and `SearchAsync(axis: "semantic")` for semantic-only cases. Use `TraverseAsync` for graph exploration when a party memory unit is already known.
- Add health/readiness and deployment validation for Memories endpoint, tenant/case provisioning, index health, and degraded fallback behavior.
- Update tests to use a fake `MemoriesClient` or fake search backend for fast unit tests, plus integration tests when a Memories test fixture is available.

## 3. Recommended Approach

### Selected Path: Hybrid

Use a hybrid of direct adjustment and limited rollback:

1. Keep Story 9.5 temporal name query implementation and tests.
2. Stop treating `SemanticPartySearchProvider` as the approved v1.1 semantic search path.
3. Replace the default rich search plan with a Memories-backed integration story.
4. Keep `PartySearchResultsBuilder` or a local provider as fallback for MVP display-name search.
5. Update PRD, architecture, epics, and story files so `Hexalith.Memories` is the named dedicated search capability.

### Rationale

Hexalith.Memories already owns the exact capability Parties needs: lexical search, vector semantic search, graph traversal, hybrid fusion, score explanation, tenant isolation, and search telemetry. Parties should own party lifecycle, projections, authorization, and result hydration, but not the search engine.

This preserves previous Parties work that is still valid while avoiding a second search platform inside Parties.

### Effort, Risk, Timeline

- **Effort:** Medium. The change touches Story 9.5, API/MCP search paths, event/projection integration, AppHost/config, deployment validation, docs, and tests.
- **Risk:** Medium. The main risks are eventual consistency between Parties and Memories, PII/GDPR cleanup across service boundaries, and degraded behavior when Memories is unavailable.
- **Timeline impact:** Do before closing Epic 9. If Story 9.5 has already landed code, add a corrective follow-up story before Epic 9 retrospective.
- **Scope classification:** Moderate. Requires backlog reorganization and developer work, but no fundamental PRD replan.

## 4. Detailed Change Proposals

### PRD Changes

#### PRD - Search Result Quality

**OLD:**

```text
Search result quality: search_parties results include display-name match metadata (matched field, match type) sufficient for AI agents to rank candidates in simple name-based cases. Email, identifier, and semantic search are deferred to the dedicated search capability.
```

**NEW:**

```text
Search result quality: baseline Parties search returns display-name match metadata sufficient for simple name-based cases. Rich party search - lexical, semantic, hybrid, and graph-assisted retrieval across names, contact channels, identifiers, party type, and event context - is provided through Hexalith.Memories as the dedicated search capability.
```

**Rationale:** Names the module that owns advanced search.

#### PRD - MVP Included Search Scope

**OLD:**

```text
Read projection: paginated list, display-name search with match metadata, filter by type; eventual consistency < 2 seconds (email, identifier, and semantic search deferred to the dedicated search capability)
```

**NEW:**

```text
Read projection: paginated list, display-name search with match metadata, filter by type; eventual consistency < 2 seconds. Email, identifier, lexical full-text, semantic, hybrid, and graph-assisted search are not implemented inside the Parties projection; they are provided by Hexalith.Memories when the Memories integration is enabled.
```

**Rationale:** Prevents the Parties projection from becoming a second search engine.

#### PRD - FR15, FR16, FR17, FR20

**OLD:**

```text
FR15: Consumer can search parties by display name in MVP. Email and identifier search are deferred to the dedicated search capability because the v1.0 index projection does not store those searchable fields.
FR16: (Deferred to v1.1) Consumer can perform semantic search across parties. Display-name exact/prefix/contains search (FR15) + match metadata (FR17) are sufficient for MVP name-based lookup scenarios. Semantic search ships as a pluggable projection in v1.1.
FR17: Search results include match metadata (matched field, match type) to support disambiguation by AI agents and humans. MVP emits displayName; email and identifier are reserved for the future search model.
FR20: AI agent can search and resolve parties by display name via a dedicated AI-optimized interface in MVP. Email and identifier resolution require candidate retrieval or the future dedicated search capability.
```

**NEW:**

```text
FR15: Consumer can search parties by display name in the local Parties projection. Email, identifier, lexical full-text, semantic, hybrid, and graph-assisted search are provided by Hexalith.Memories when the integration is enabled.
FR16: Consumer can perform semantic, lexical, hybrid, and graph-assisted party search through Hexalith.Memories. Parties publishes or indexes party search documents/memory units into Memories and maps Memories results back to party search results.
FR17: Search results include match metadata and scoring sufficient for disambiguation by AI agents and humans. Baseline Parties search emits displayName matches; Memories-backed results can include lexical, semantic, graph, and hybrid scores plus source memory-unit metadata.
FR20: AI agent can resolve parties through the same AI-optimized interface. In baseline mode it searches display names from the Parties projection; when Memories is enabled it delegates rich search to Hexalith.Memories and hydrates matching parties from Parties read models.
FR76: Parties erasure verification purges or tombstones all party-related Hexalith.Memories memory units and search indexes before reporting erasure complete.
```

**Rationale:** Keeps baseline search local and makes Memories the advanced search provider.

#### PRD - v2 Scale & Intelligence

**OLD:**

```text
Advanced search via pluggable Elasticsearch/OpenSearch projection
```

**NEW:**

```text
Advanced search through Hexalith.Memories. Backend evolution such as Elasticsearch/OpenSearch, Qdrant, Redis Vector, or graph backend replacement is owned by the Memories module and consumed by Parties through Memories contracts.
```

**Rationale:** Backend selection belongs to Memories, not Parties.

### Architecture Changes

#### Architecture - D2 Search Decision

**OLD:**

```text
Decision: Basic key-lookup and list/filter in v1.0. Dedicated search engine (Elasticsearch or similar) in v1.1
Rejected: Building search into v1.0 projection model
Rationale: Keeps projection model clean; search is a distinct infrastructure concern
Consequence: MCP find_parties limited to exact/prefix match in v1.0. Index actor schema designed search-ready for v1.1 extensibility
```

**NEW:**

```text
Decision: Basic key-lookup and list/filter remain in the local Parties projection. Advanced party search is delegated to Hexalith.Memories, which owns syntactic/lexical search, semantic/vector search, graph traversal, hybrid fusion, score explanations, index health, and backend evolution.
Rejected: Building semantic/vector/graph search directly into Parties; adding a Parties-owned Elasticsearch/OpenSearch projection; making Hexalith.Parties.Contracts depend on Memories.
Rationale: Search is a distinct ecosystem capability already implemented by Hexalith.Memories. Parties should publish searchable party memory units and hydrate results, not own the search engine.
Consequence: Parties has a local baseline search fallback plus a Memories-backed rich search path. Memories outage degrades rich search but does not block get-by-id, list, or baseline display-name search.
```

**Rationale:** Aligns architecture with the requested module boundary.

#### Architecture - Add Integration Components

**ADD:**

```text
src/Hexalith.Parties.CommandApi
  Search/
    IPartySearchService.cs
    LocalPartySearchService.cs
    MemoriesPartySearchService.cs
    PartySearchResultHydrator.cs

src/Hexalith.Parties.Projections or src/Hexalith.Parties.SearchIntegration
  Memories/
    PartyMemoryUnitMapper.cs
    PartyMemoryIndexingSubscriber.cs
    PartyMemorySearchOptions.cs
    PartyMemoryUnitRegistry.cs

tests/Hexalith.Parties.CommandApi.Tests
  Search/
    MemoriesPartySearchServiceTests.cs
    PartySearchResultHydratorTests.cs

tests/Hexalith.Parties.IntegrationTests
  Search/
    MemoriesPartySearchIntegrationTests.cs
```

**Rationale:** Provides clear ownership without leaking Memories dependencies into Parties contracts.

#### Architecture - Search Data Flow

**ADD:**

```text
Party command -> Party events -> Parties projections
Party events/projection changes -> PartyMemoryUnitMapper -> Hexalith.Memories ingestion/indexing
REST/MCP search -> IPartySearchService
  -> local display-name fallback OR Memories hybrid/syntactic/semantic search
  -> PartySearchResultHydrator loads PartyDetail/PartyIndexEntry from Parties projections
  -> response includes Memories scores and match/source metadata when available
```

**Rationale:** Separates indexing, search, and result hydration.

### Epic Changes

#### Epic 9 Summary

**OLD:**

```text
Includes semantic search (FR16) and temporal name queries (FR72) deferred from MVP.
```

**NEW:**

```text
Includes temporal name queries (FR72), Memories-backed party search integration (FR16), and erasure verification across both Parties projections and Hexalith.Memories memory units/search indexes.
```

**Rationale:** Keeps temporal search in Parties but moves advanced search to Memories.

#### Story 9.5 Split

**OLD:**

```text
Story 9.5: Semantic Search & Temporal Name Queries
As a consumer,
I want semantic search across parties and the ability to query historical names,
So that I can find parties by meaning (not just exact match) and audit name history.
```

**NEW:**

```text
Story 9.5: Temporal Name Queries
As a consumer,
I want to query historical party names at a point in time,
So that I can support legal and audit workflows without replaying the event stream on the request path.

Story 9.6: Hexalith.Memories-Backed Party Search
As a consumer and AI agent,
I want Parties search to use Hexalith.Memories for lexical, semantic, hybrid, and graph-assisted retrieval,
So that party discovery uses the shared Hexalith memory/search module instead of a Parties-local search engine.
```

**Rationale:** Avoids mixing a local temporal query with external search integration.

#### New Story 9.6 Acceptance Criteria

```text
Given Hexalith.Memories integration is enabled
When party events or projection changes occur
Then Parties indexes searchable party memory units into Memories using SourceType.Event or an agreed party-specific source shape
And the memory-unit metadata includes tenant id, party id, aggregate id, party type, display name, contact channel values, identifier values, active/erased state, event type, timestamps, and correlation/causation identifiers when available.

Given an AI agent calls find_parties with a natural-language query
When Memories integration is healthy
Then Parties uses Memories hybrid search by default
And maps matching memory units back to PartySearchResult/PartyDetail data from Parties projections
And includes relevance, lexical, semantic, graph, and composite scores where Memories provides them.

Given a consumer requests lexical-only or semantic-only search
When the search mode is specified through REST or MCP
Then Parties calls Memories single-axis search with axis "syntactic" or "semantic".

Given a graph-assisted search starts from a known party or memory unit
When graph context is requested
Then Parties uses Memories graph traversal or graph-scoped search and hydrates related party results.

Given Memories is unavailable or degraded
When a search request arrives
Then Parties falls back to local display-name search where possible
And returns a degraded indicator explaining that rich Memories-backed search was unavailable.

Given a party erasure is triggered
When erasure verification runs
Then all party-related Memories memory units and indexes are purged or tombstoned
And erasure is not reported complete until Memories cleanup succeeds or is explicitly recorded as blocked.

Given Hexalith.Parties.Contracts is reviewed
When dependency boundaries are checked
Then it has no dependency on Hexalith.Memories packages.
```

### Story 9.5 Implementation Review Changes

#### Local Semantic Provider

**OLD:**

```text
Register IPartySearchProvider -> SemanticPartySearchProvider in DI.
SemanticPartySearchProvider implements enhanced fuzzy/token search over PartyIndexEntry.
```

**NEW:**

```text
Register the default search service as a composition over local fallback and Memories-backed rich search:
- Local display-name search remains available for baseline/fallback behavior.
- MemoriesPartySearchService is the rich search path when enabled and healthy.
- The local fuzzy/token SemanticPartySearchProvider is not the approved semantic search implementation. If retained, it must be named and documented as a local fuzzy fallback, not semantic search.
```

**Rationale:** Prevents a misleading "semantic" label and avoids duplicate search ownership.

#### Temporal Name Query

**KEEP:**

```text
NameHistoryEntry, TemporalNameResult, GET /api/v1/parties/{id}/name, GET /api/v1/parties/{id}/name-history, get_party_name_at, and projection-side NameHistory tracking.
```

**Rationale:** Temporal name lookup is a Parties read-model/audit concern and does not need Memories.

## 5. Implementation Handoff

### Scope Classification

**Moderate**

This change requires backlog/story correction and integration work across search, events/projections, deployment validation, docs, and tests. It does not invalidate Parties domain modeling, GDPR work, or the baseline projection search.

### Route To

- Product Owner / Developer: approve Story 9.5 split and add Story 9.6 to sprint status.
- Developer agent: implement Memories-backed search integration and remove/rename misleading local semantic search.
- Architect: review the service boundary, event-to-memory mapping, erasure cleanup contract, and degraded search behavior.
- Test Architect: add contract, integration, and degraded-mode coverage for Memories search.

### Developer Responsibilities

- Inspect the current `Hexalith.Memories.Client.Rest` public surface before coding against assumptions.
- Keep Memories references out of `Hexalith.Parties.Contracts`.
- Use `MemoriesClient.HybridSearchAsync` for default rich search and single-axis `SearchAsync` for explicit syntactic/semantic modes.
- Use `TraverseAsync` for graph-assisted exploration when a starting memory unit or party context is known.
- Design a stable `SourceUri` and metadata contract for party memory units.
- Preserve the local display-name search as a fallback and as the MVP baseline.
- Ensure erasure verification includes Memories cleanup.
- Add health/readiness/deployment validation for Memories integration.

### Success Criteria

- `find_parties` can return Memories-backed lexical/semantic/hybrid results when Memories is enabled.
- Graph-assisted party discovery works from a known party or memory unit.
- Search results hydrate back to authoritative Parties projection data.
- Memories outage degrades rich search without breaking local list/get/display-name search.
- Party erasure blocks completion until Memories party-search artifacts are cleaned up or explicitly reported as blocked.
- No Memories dependency is introduced into `Hexalith.Parties.Contracts`.
- Story 9.5 no longer claims in-process fuzzy matching is semantic search.

### Sprint Status Update Applied

Applied to `_bmad-output/implementation-artifacts/sprint-status.yaml`:

```yaml
  # Epic 9: GDPR Compliance (v1.1) (6 stories)
  9-5-temporal-name-queries: review
  9-6-hexalith-memories-backed-party-search: ready-for-dev
```

The former mixed `9-5-semantic-search-and-temporal-name-queries.md` story was split into:

- `9-5-temporal-name-queries.md`
- `9-6-hexalith-memories-backed-party-search.md`

## Approval

Approved by Jérôme on 2026-05-02.
