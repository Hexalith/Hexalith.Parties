# Story 9.6: Hexalith.Memories-Backed Party Search

Status: ready-for-dev

<!-- Split from the former Story 9.5 "Semantic Search & Temporal Name Queries". -->

## Story

As a consumer and AI agent,
I want Hexalith.Parties search to use Hexalith.Memories for lexical, semantic, hybrid, and graph-assisted retrieval,
so that party discovery uses the shared Hexalith search/memory module instead of a Parties-local search engine.

## Acceptance Criteria

1. **Party Search Indexing into Memories (FR16)**
   - Given Hexalith.Memories integration is enabled
   - When party events or projection changes occur
   - Then Parties indexes searchable party memory units into Memories
   - And indexed content includes display name, party type, contact channel values, identifier values, active/erased state, and useful event context
   - And metadata includes tenant id, party id, aggregate id, event type, timestamps, correlation id, causation id, and source service where available
   - And `Hexalith.Parties.Contracts` has no dependency on Memories packages

2. **REST and MCP Rich Search**
   - Given a consumer calls the Parties search endpoint with rich search enabled
   - When Memories is healthy
   - Then Parties uses Hexalith.Memories hybrid search by default
   - And matching memory units are hydrated back to authoritative `PartySearchResult` or `PartyDetail` data from Parties projections
   - And response metadata includes Memories relevance, lexical, semantic, graph, and composite scores when available

3. **Explicit Search Axes**
   - Given a caller requests lexical-only search
   - When the search mode is specified
   - Then Parties calls Memories single-axis search with axis `syntactic`
   - Given a caller requests semantic-only search
   - When the search mode is specified
   - Then Parties calls Memories single-axis search with axis `semantic`
   - Given graph context is requested from a known party or memory unit
   - When graph-assisted search executes
   - Then Parties uses Memories traversal or graph-scoped search and hydrates related party results

4. **Fallback and Degraded Behavior**
   - Given Memories is unavailable, disabled, or partially degraded
   - When a search request arrives
   - Then Parties falls back to local display-name search where possible
   - And the response includes a degraded indicator explaining that Memories-backed rich search was unavailable
   - And get-by-id, list, and baseline display-name search remain available

5. **GDPR Erasure Cleanup**
   - Given a party erasure is triggered
   - When erasure verification runs
   - Then all party-related Memories memory units and search indexes are purged or tombstoned
   - And erasure is not reported complete until Memories cleanup succeeds or is explicitly recorded as blocked
   - And erased party content is not returned by Memories-backed search

6. **Operational Validation**
   - Given deployment validation runs
   - When Memories search integration is enabled
   - Then validation checks Memories endpoint configuration, tenant/case provisioning, auth, search health, and cleanup capability
   - And health/readiness signals distinguish local Parties availability from Memories-backed rich search availability

## Tasks / Subtasks

- [ ] Task 1: Define the Parties search integration boundary
  - [ ] 1.1 Introduce `IPartySearchService` or equivalent inside `CommandApi/Search`
  - [ ] 1.2 Keep local display-name search as fallback
  - [ ] 1.3 Ensure `Hexalith.Parties.Contracts` remains free of Memories references
  - [ ] 1.4 Rename or demote any local fuzzy provider so it is not described as semantic search

- [ ] Task 2: Add Memories client integration
  - [ ] 2.1 Reference `Hexalith.Memories.Client.Rest` only from integration-facing projects
  - [ ] 2.2 Register `MemoriesClient` and options through Parties DI
  - [ ] 2.3 Configure AppHost/local development topology for Memories when rich search is enabled
  - [ ] 2.4 Add configuration validation for endpoint, auth, tenant, case, and enabled axes

- [ ] Task 3: Map party data to Memories memory units
  - [ ] 3.1 Create `PartyMemoryUnitMapper`
  - [ ] 3.2 Define stable `SourceUri` and metadata keys for party id, tenant id, aggregate id, event type, timestamps, and party fields
  - [ ] 3.3 Index party-created and party-updated data into Memories
  - [ ] 3.4 Track party-to-memory-unit mappings needed for hydration and erasure cleanup

- [ ] Task 4: Query Memories and hydrate party results
  - [ ] 4.1 Use `MemoriesClient.HybridSearchAsync` for default rich search
  - [ ] 4.2 Use `MemoriesClient.SearchAsync(axis: "syntactic")` for lexical-only search
  - [ ] 4.3 Use `MemoriesClient.SearchAsync(axis: "semantic")` for semantic-only search
  - [ ] 4.4 Use `MemoriesClient.TraverseAsync` for graph-assisted discovery from known context
  - [ ] 4.5 Hydrate Memories hits from Parties projections and omit stale hits that no longer map to readable parties

- [ ] Task 5: Update REST and MCP behavior
  - [ ] 5.1 Extend REST search to choose local fallback or Memories-backed rich search
  - [ ] 5.2 Extend `find_parties` to use the same search service
  - [ ] 5.3 Include score/source metadata from Memories in a backward-compatible response shape
  - [ ] 5.4 Preserve baseline list mode behavior when query is empty

- [ ] Task 6: Wire erasure and repair
  - [ ] 6.1 Remove/tombstone all party-related Memories units during erasure
  - [ ] 6.2 Include Memories cleanup in erasure verification reports
  - [ ] 6.3 Add repair/reindex procedure for party search artifacts
  - [ ] 6.4 Document blocked erasure behavior when Memories cleanup fails

- [ ] Task 7: Tests and docs
  - [ ] 7.1 Unit test mapper, hydration, fallback, and score metadata mapping
  - [ ] 7.2 Integration test Memories-backed search with a fake or test fixture
  - [ ] 7.3 Test degraded fallback when Memories is unavailable
  - [ ] 7.4 Test erasure cleanup and blocked cleanup reporting
  - [ ] 7.5 Update getting-started, operations, and admin search documentation

## Dev Notes

Hexalith.Memories already owns the search capabilities Parties needs:

- `MemoriesClient.HybridSearchAsync(HybridSearchRequest, CancellationToken)`
- `MemoriesClient.SearchAsync(SearchRequest, CancellationToken)` with axes `syntactic` and `semantic`
- `MemoriesClient.TraverseAsync(...)` for graph traversal
- `SourceType.Event` for event-origin memory units
- `HybridSearchResult`, `FusedScoredResult`, `SearchResult`, and `ScoredResult` for search responses and scoring metadata

Use Memories for real lexical/semantic/hybrid/graph search. Parties owns party lifecycle, authorization, projection hydration, fallback display-name search, and GDPR coordination.

Do not add Memories dependencies to `Hexalith.Parties.Contracts`. Put integration dependencies in `CommandApi`, a dedicated integration project, AppHost, deployment validation, and tests as needed.

### Suggested Files

```text
src/Hexalith.Parties.CommandApi/Search/IPartySearchService.cs
src/Hexalith.Parties.CommandApi/Search/LocalPartySearchService.cs
src/Hexalith.Parties.CommandApi/Search/MemoriesPartySearchService.cs
src/Hexalith.Parties.CommandApi/Search/PartySearchResultHydrator.cs
src/Hexalith.Parties.CommandApi/Search/PartyMemoryUnitMapper.cs
src/Hexalith.Parties.CommandApi/Search/PartyMemorySearchOptions.cs
tests/Hexalith.Parties.CommandApi.Tests/Search/MemoriesPartySearchServiceTests.cs
tests/Hexalith.Parties.CommandApi.Tests/Search/PartyMemoryUnitMapperTests.cs
tests/Hexalith.Parties.IntegrationTests/Search/MemoriesPartySearchIntegrationTests.cs
```

### Superseded Work

The former Story 9.5 local `SemanticPartySearchProvider` work is not the approved semantic search path. If any local fuzzy/token code remains, it must be treated as local fallback behavior only and should be renamed/documented accordingly.

## Dev Agent Record

### Agent Model Used

TBD

### Completion Notes List

### File List

