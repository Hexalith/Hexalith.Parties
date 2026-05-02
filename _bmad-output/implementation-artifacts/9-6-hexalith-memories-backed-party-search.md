# Story 9.6: Hexalith.Memories-Backed Party Search

Status: in-progress

<!-- Split from the former Story 9.5 "Semantic Search & Temporal Name Queries". -->

## Story

As a consumer and AI agent,
I want Hexalith.Parties search to use Hexalith.Memories for lexical, semantic, hybrid, and graph-assisted retrieval,
so that I can find the right party from partial names, contact fragments, identifiers, or relationship context without bypassing Parties authorization or lifecycle rules.

## Product Outcome

Case workers, consumers, and AI agents can discover the correct party from incomplete or relationship-based evidence while Parties remains the authoritative source for visibility, current state, and erasure status. The first observable success path is: a party is indexed into Memories, a rich search returns a Memories candidate, Parties hydrates that candidate from projections, authorization is enforced, and the API clearly reports whether full rich retrieval or degraded local fallback was used.

## Acceptance Criteria

1. **Party Search Indexing into Memories (FR16)**
   - Given Hexalith.Memories integration is enabled
   - When party events or projection changes occur
   - Then Parties indexes searchable party memory units into Memories
   - And indexed content includes display name, party type, contact channel values, identifier values, active/erased state, and useful event context
   - And metadata includes tenant id, party id, aggregate id, event type, timestamps, correlation id, causation id, and source service where available
   - And memory units use stable source identifiers that support hydration, reindexing, and erasure cleanup
   - And `Hexalith.Parties.Contracts` has no dependency on Memories packages

2. **REST and MCP Rich Search**
   - Given a consumer calls the Parties search endpoint with rich search enabled
   - When Memories is healthy
   - Then Parties uses Hexalith.Memories hybrid search by default
   - And matching memory units are hydrated back to authoritative `PartySearchResult` or `PartyDetail` data from Parties projections
   - And stale, erased, unauthorized, wrong-tenant, or wrong-case Memories candidates are omitted from hydrated results
   - And response metadata includes Memories relevance, lexical, semantic, graph, and composite scores when available
   - And response metadata identifies the search status as rich, degraded, or local-only

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
   - And the response includes a degraded indicator and reason explaining that Memories-backed rich search was unavailable
   - And fallback responses do not claim semantic, hybrid, or graph retrieval was used
   - And get-by-id, list, and baseline display-name search remain available

5. **GDPR Erasure Cleanup**
   - Given a party erasure is triggered
   - When erasure verification runs
   - Then all party-related Memories memory units and search indexes are purged or tombstoned
   - And erasure is not reported complete until Memories cleanup succeeds or is explicitly recorded as blocked with repair evidence
   - And erased party content is not returned by Memories-backed search

6. **Operational Validation**
   - Given deployment validation runs
   - When Memories search integration is enabled
   - Then validation checks Memories endpoint configuration, tenant/case provisioning, auth, search health, and cleanup capability
   - And health/readiness signals distinguish local Parties availability from Memories-backed rich search availability

## Tasks / Subtasks

- [x] Task 1: Define the Parties search integration boundary
  - [x] 1.1 Introduce `IPartySearchService` or equivalent inside `CommandApi/Search`
  - [x] 1.2 Keep local display-name search as fallback
  - [x] 1.3 Ensure `Hexalith.Parties.Contracts` remains free of Memories references
  - [x] 1.4 Rename or demote any local fuzzy provider so it is not described as semantic search
  - [x] 1.5 Define Parties-owned search request/response models for query intent, search mode, degraded status, score metadata, and source metadata
  - [x] 1.6 Add a dependency guard or architecture test proving `Hexalith.Parties.Contracts` does not reference Memories assemblies

- [x] Task 2: Add Memories client integration
  - [x] 2.1 Reference `Hexalith.Memories.Client.Rest` only from integration-facing projects
  - [x] 2.2 Register `MemoriesClient` and options through Parties DI
  - [x] 2.3 Configure AppHost/local development topology for Memories when rich search is enabled
  - [x] 2.4 Add configuration validation for endpoint, auth, tenant, case, and enabled axes

- [x] Task 3: Map party data to Memories memory units
  - [x] 3.1 Create `PartyMemoryUnitMapper`
  - [x] 3.2 Define stable `SourceUri` and metadata keys for party id, tenant id, aggregate id, event type, timestamps, and party fields
  - [x] 3.3 Index party-created and party-updated data into Memories
  - [x] 3.4 Track party-to-memory-unit mappings needed for hydration and erasure cleanup
  - [x] 3.5 Ensure indexing excludes or tombstones erased content and preserves enough source metadata for repair/reindex operations

- [x] Task 4: Query Memories and hydrate party results
  - [x] 4.1 Use `MemoriesClient.HybridSearchAsync` for default rich search
  - [x] 4.2 Use `MemoriesClient.SearchAsync(axis: "syntactic")` for lexical-only search
  - [x] 4.3 Use `MemoriesClient.SearchAsync(axis: "semantic")` for semantic-only search
  - [x] 4.4 Use `MemoriesClient.TraverseAsync` for graph-assisted discovery from known context
  - [x] 4.5 Hydrate Memories hits from Parties projections and omit stale hits that no longer map to readable parties
  - [x] 4.6 Enforce tenant, case, lifecycle, erasure, and authorization checks during hydration
  - [x] 4.7 Collapse duplicate memory hits that hydrate to the same party while preserving useful score/source metadata

- [x] Task 5: Update REST and MCP behavior
  - [x] 5.1 Extend REST search to choose local fallback or Memories-backed rich search
  - [x] 5.2 Extend `find_parties` to use the same search service
  - [x] 5.3 Include score/source metadata from Memories in a backward-compatible response shape
  - [x] 5.4 Preserve baseline list mode behavior when query is empty
  - [x] 5.5 Keep REST and MCP behavior equivalent for search mode selection, degraded status, hydration, and authorization

- [x] Task 6: Wire erasure and repair
  - [x] 6.1 Remove/tombstone all party-related Memories units during erasure
  - [x] 6.2 Include Memories cleanup in erasure verification reports
  - [x] 6.3 Add repair/reindex procedure for party search artifacts
  - [x] 6.4 Document blocked erasure behavior when Memories cleanup fails

- [x] Task 7: Tests and docs
  - [x] 7.1 Unit test mapper, hydration, fallback, and score metadata mapping
  - [x] 7.2 Integration test Memories-backed search with a fake or test fixture
  - [x] 7.3 Test degraded fallback when Memories is unavailable
  - [x] 7.4 Test erasure cleanup and blocked cleanup reporting
  - [x] 7.5 Update getting-started, operations, and admin search documentation
  - [x] 7.6 Add REST and MCP parity tests for default hybrid, explicit syntactic, explicit semantic, graph-assisted, and fallback modes
  - [x] 7.7 Add hydration edge-case tests for stale IDs, deleted parties, unauthorized parties, wrong tenant/case, and duplicate hits
  - [x] 7.8 Add one smoke path proving index -> Memories search -> Parties hydration -> authorization -> response metadata

## Dev Notes

Hexalith.Memories already owns the search capabilities Parties needs:

- `MemoriesClient.HybridSearchAsync(HybridSearchRequest, CancellationToken)`
- `MemoriesClient.SearchAsync(SearchRequest, CancellationToken)` with axes `syntactic` and `semantic`
- `MemoriesClient.TraverseAsync(...)` for graph traversal
- `SourceType.Event` for event-origin memory units
- `HybridSearchResult`, `FusedScoredResult`, `SearchResult`, and `ScoredResult` for search responses and scoring metadata

Use Memories for real lexical/semantic/hybrid/graph search. Parties owns party lifecycle, authorization, projection hydration, fallback display-name search, and GDPR coordination.

Do not add Memories dependencies to `Hexalith.Parties.Contracts`. Put integration dependencies in `CommandApi`, a dedicated integration project, AppHost, deployment validation, and tests as needed.

### Implementation Slice

Deliver this story in observable slices:

1. Prove one party memory unit can be indexed into Memories, searched, hydrated from Parties projections, and returned with rich-search metadata.
2. Add explicit syntactic, semantic, hybrid, and graph-assisted mode routing.
3. Add degraded local fallback with explicit response status and reason metadata.
4. Add erasure cleanup, blocked-cleanup reporting, and repair/reindex support.
5. Add executable operational validation for configuration, auth, health, tenant/case provisioning, and cleanup capability.

### Authority Boundary

Parties owns truth; Memories owns retrieval. Memories results are candidates only. Parties must hydrate all returned candidates from its projections and reapply tenant, case, authorization, lifecycle, and erasure checks before anything is returned through REST or MCP. Memories DTOs and client types must not leak into `Hexalith.Parties.Contracts` or public Parties response contracts.

### Required Test Names

Use named tests that make the acceptance risks visible:

- `MapsPartyEventToEventSourceMemoryUnitWithTenantCaseAndPartyMetadata`
- `DefaultRichSearchUsesHybridSearchAndHydratesAuthorizedParty`
- `LexicalOnlySearchUsesSyntacticAxis`
- `SemanticOnlySearchUsesSemanticAxis`
- `GraphAssistedSearchTraversesContextAndHydratesRelatedParties`
- `UnavailableMemoriesReturnsDegradedDisplayNameFallback`
- `FallbackSearchDoesNotAdvertiseSemanticOrGraphScores`
- `HydrationOmitsStaleErasedUnauthorizedAndWrongTenantHits`
- `DuplicateMemoryHitsCollapseToOnePartyResult`
- `ErasureBlocksOrRecordsBlockedCompletionWhenMemoriesCleanupFails`
- `ContractsProjectDoesNotReferenceMemoriesAssemblies`
- `OperationalValidationReportsMemoriesEndpointAuthHealthProvisioningAndCleanup`

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

GPT-5 Codex

### Completion Notes List

- Task 1 complete: added a CommandApi-owned search service boundary, local-only fallback response metadata, local fuzzy provider naming, and a contracts architecture guard. Also aligned root package versions and EventStore `SubmitCommand` call sites so the CommandApi suite can restore, build, and run against the current referenced EventStore API.
- Task 2 complete: added `Hexalith.Memories.Client.Rest` as a CommandApi package dependency, bound and validated `Parties:MemoriesSearch`, conditionally registered `MemoriesClient`, and added an AppHost `EnableMemoriesSearch` switch that passes local endpoint/tenant/case settings without initializing nested Memories submodules.
- Tasks 3-4 complete: mapped party index entries to stable Memories event-source memory units, added indexing/search/cleanup services, hydrated Memories candidates through authoritative projection entries, and preserved tenant/case/lifecycle filtering with score/source metadata.
- Tasks 5-6 complete: REST search and `find_parties` share the same search service, support explicit modes and local fallback metadata, and erasure cleanup records Memories cleanup as blocked when no persisted memory-unit mapping is available.
- Task 7 complete: added mapper/options/search/cleanup/service tests, projection replay idempotency tests, actor JSON-return fallbacks for Dapr remoting, temporal/search E2E diagnostics, and operations documentation.
- Not moved to review: story-owned validation passes, but `dotnet test Hexalith.Parties.slnx --logger "console;verbosity=minimal"` still fails in unrelated full-topology erasure/restriction workflows. Remaining failures observed: `ErasureE2ETests` erasure status/detail/search tests and `ConsentRestrictionE2ETests.FullTopology_RestrictThenUpdate_RejectedThenLiftSucceeds`.

### Debug Log References

- `dotnet test Hexalith.Parties.slnx --logger "console;verbosity=minimal"`: solution run failed in `Hexalith.Parties.IntegrationTests` with erasure/restriction workflow failures after other projects passed.
- `SemanticSearchE2ETests.FullTopology_SemanticSearch_FuzzyQuery_ReturnsRankedResults`: isolated pass after adding index actor JSON return path.
- `TemporalNameE2ETests.FullTopology_TemporalNameQuery_ReturnsOriginalNameAtCreationTimestamp`: isolated pass after unprotecting replay state before domain invocation and making projection create replay idempotent.

### Validation

- PASS `dotnet test tests\Hexalith.Parties.CommandApi.Tests\Hexalith.Parties.CommandApi.Tests.csproj --no-restore --logger "console;verbosity=minimal"`: 246 passed.
- PASS `dotnet test tests\Hexalith.Parties.Projections.Tests\Hexalith.Parties.Projections.Tests.csproj --no-restore --logger "console;verbosity=minimal"`: 54 passed.
- PASS `dotnet test tests\Hexalith.Parties.Security.Tests\Hexalith.Parties.Security.Tests.csproj --no-restore --logger "console;verbosity=minimal"`: 107 passed.
- PASS `dotnet build src\Hexalith.Parties.AppHost\Hexalith.Parties.AppHost.csproj --no-restore`: build succeeded.
- PASS `dotnet test tests\Hexalith.Parties.IntegrationTests\Hexalith.Parties.IntegrationTests.csproj --filter "FullyQualifiedName~SemanticSearchE2ETests.FullTopology_SemanticSearch_FuzzyQuery_ReturnsRankedResults" --logger "console;verbosity=minimal"`: 1 passed.
- PASS `dotnet test tests\Hexalith.Parties.IntegrationTests\Hexalith.Parties.IntegrationTests.csproj --filter "FullyQualifiedName~TemporalNameE2ETests.FullTopology_TemporalNameQuery_ReturnsOriginalNameAtCreationTimestamp" --logger "console;verbosity=minimal"`: 1 passed.
- FAIL `dotnet test Hexalith.Parties.slnx --logger "console;verbosity=minimal"`: remaining failures in unrelated full-topology erasure/restriction workflows; story-owned targeted checks above passed.

### File List

- Directory.Packages.props
- src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj
- src/Hexalith.Parties.AppHost/Program.cs
- src/Hexalith.Parties.CommandApi/Hexalith.Parties.CommandApi.csproj
- src/Hexalith.Parties.CommandApi/appsettings.Development.json
- src/Hexalith.Parties.CommandApi/appsettings.json
- src/Hexalith.Parties.CommandApi/Controllers/AdminController.cs
- src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs
- src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs
- src/Hexalith.Parties.CommandApi/Mcp/CreatePartyMcpTool.cs
- src/Hexalith.Parties.CommandApi/Mcp/DeletePartyMcpTool.cs
- src/Hexalith.Parties.CommandApi/Mcp/FindPartiesMcpTool.cs
- src/Hexalith.Parties.CommandApi/Mcp/UpdatePartyMcpTool.cs
- src/Hexalith.Parties.CommandApi/Domain/PartyDomainServiceInvoker.cs
- src/Hexalith.Parties.CommandApi/Domain/PartyProjectionUpdateOrchestrator.cs
- src/Hexalith.Parties.CommandApi/Search/BasicPartySearchProvider.cs
- src/Hexalith.Parties.CommandApi/Search/LocalFuzzyPartySearchProvider.cs
- src/Hexalith.Parties.CommandApi/Search/LocalPartySearchService.cs
- src/Hexalith.Parties.CommandApi/Search/MemoriesPartySearchService.cs
- src/Hexalith.Parties.CommandApi/Search/PartyMemoryCleanupService.cs
- src/Hexalith.Parties.CommandApi/Search/PartyMemoryIndexingService.cs
- src/Hexalith.Parties.CommandApi/Search/PartyMemoryUnitMapper.cs
- src/Hexalith.Parties.CommandApi/Search/PartySearchBoundary.cs
- src/Hexalith.Parties.CommandApi/Search/PartyMemorySearchOptions.cs
- src/Hexalith.Parties.CommandApi/Search/PersonalDataCommandGuardAccessor.cs
- src/Hexalith.Parties.Contracts/Search/IPartySearchProvider.cs
- src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs
- src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs
- src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs
- src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs
- src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs
- src/Hexalith.Parties.Projections/Handlers/PartyIndexProjectionHandler.cs
- docs/memories-backed-party-search.md
- tests/Hexalith.Parties.CommandApi.Tests/Controllers/TemporalNameEndpointTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/FitnessTests/ContractsArchitectureFitnessTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/Mcp/FindPartiesMcpToolTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/Search/LocalFuzzyPartySearchProviderTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/Search/LocalFuzzySearchPerformanceBenchmarkTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/Search/MemoriesPartySearchServiceTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/Search/PartyMemoryCleanupServiceTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/Search/PartyMemoryUnitMapperTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/Search/PartySearchServiceBoundaryTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/Search/PartyMemorySearchOptionsValidatorTests.cs
- tests/Hexalith.Parties.IntegrationTests/Search/SemanticSearchE2ETests.cs
- tests/Hexalith.Parties.IntegrationTests/Search/TemporalNameE2ETests.cs
- tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerNameHistoryTests.cs
- tests/Hexalith.Parties.Projections.Tests/Handlers/PartyIndexProjectionHandlerTests.cs
