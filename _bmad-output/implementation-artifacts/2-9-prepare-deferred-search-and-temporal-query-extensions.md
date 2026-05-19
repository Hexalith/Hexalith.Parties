# Story 2.9: Prepare Deferred Search and Temporal Query Extensions

Status: ready-for-dev

## Story

As a future maintainer of Parties search and audit features, I want MVP read model contracts to reserve explicit extension points for semantic search and temporal name queries, so v1.1 can add capabilities without breaking existing consumers.

## Acceptance Criteria

1. MVP display-name search responses include match metadata and continue to treat `displayName` as the only active searchable/matched field in MVP. Email and identifier match fields may be named only as reserved future extension points and must not claim current support.
2. Semantic search remains deferred. Contracts and architecture identify semantic search as a pluggable projection/search extension without requiring an MVP semantic backend, vector store, embedding model, graph provider, or new runtime dependency.
3. Temporal name queries remain deferred. Party name-changing events and projections preserve enough name-history information for a future temporal query API, while MVP exposes no misleading temporal endpoint, client method, MCP tool, REST route, or admin UI behavior.
4. Future placeholders are additive and documented. Compatibility tests prove existing MVP consumers remain source-compatible and no future search engine dependency is required.
5. If an unsupported semantic or temporal query reaches the MVP read/query surface, it returns a bounded unsupported-capability outcome that points to documented deferred behavior without leaking internals, tenant data, personal data, or implementation details.
6. The story produces preparation artifacts and guardrails only. It must not broaden MVP search beyond display-name matching or ship active semantic/temporal runtime behavior.

## Acceptance Evidence

| AC | Evidence to provide |
| --- | --- |
| 1 | Search result contract/tests show active match metadata is limited to `displayName`; email and identifier fields are reserved-only documentation or additive placeholders. |
| 2 | Architecture/contract notes identify semantic search as a future pluggable provider; tests prove MVP wiring does not require semantic provider packages or dependencies. |
| 3 | Name-history projection tests show name changes are retained for future temporal reads and erased with party erasure; no active temporal query surface is exposed. |
| 4 | Compatibility or source-level tests cover additive contract changes and existing client/query flows. |
| 5 | Unsupported semantic/temporal requests are bounded, deterministic, tenant-safe, and log-safe. |
| 6 | Code review confirms no REST/MCP/AdminPortal/picker/Memories expansion and no semantic/temporal feature is advertised as MVP-ready. |

## Tasks

- [ ] Audit current search and temporal contract surfaces.
  - [ ] Confirm `PartySearchResult`, `MatchMetadata`, and related DTOs preserve display-name match metadata without enabling email or identifier search behavior.
  - [ ] Review `PartySearchMode`, `PartySearchBoundary`, `SemanticPartySearchProvider`, and query/client wiring for feature flags or bounded unsupported behavior.
  - [ ] Review `TemporalNameResult`, `NameHistoryEntry`, and `PartyDetail.NameHistory` for personal-data markings and erasure behavior.
- [ ] Reserve future search extension points without broadening MVP behavior.
  - [ ] Document `email`, `identifier`, semantic, graph, and hybrid match concepts as future/deferred where they appear.
  - [ ] Add or tighten tests that fail if MVP display-name search emits future-field match metadata.
  - [ ] Ensure any future-mode enum values are either unreachable from MVP paths or return bounded unsupported-capability responses.
- [ ] Preserve temporal-query preparation without exposing a temporal feature.
  - [ ] Keep event/projection history sufficient for future name-as-of reads.
  - [ ] Verify erased parties clear or suppress name history according to existing privacy rules.
  - [ ] Avoid adding public temporal query APIs, client methods, REST routes, MCP tools, or UI affordances unless the only behavior is explicit unsupported-capability handling.
- [ ] Add compatibility and architecture guardrails.
  - [ ] Cover additive DTO compatibility and existing `IPartiesQueryClient` search behavior.
  - [ ] Add architecture fitness checks if needed to prevent required semantic backend dependencies in MVP packages.
  - [ ] Confirm no personal data is written to logs, telemetry, unsupported responses, or test diagnostics.
- [ ] Update developer-facing documentation.
  - [ ] Record deferred semantic search and temporal query behavior in the relevant architecture or implementation notes.
  - [ ] Explain which placeholders are reserved and which fields are active in MVP.

## Dev Notes

This is a preparation-only story. It reserves extension points and hardens compatibility boundaries for v1.1 work; it does not deliver semantic search or temporal name-query runtime behavior.

Story 2.5 established that public MVP search is display-name-only and must not emit email, contact-channel, identifier, semantic, graph, duplicate, or type metadata as active matches. Current source surfaces include future-looking types and providers, including `PartySearchMode.Semantic`, `PartySearchMode.Graph`, `SemanticPartySearchProvider`, `TemporalNameResult`, `NameHistoryEntry`, and `PartyDetail.NameHistory`. Treat these as extension surfaces to gate, document, or test, not as proof that the deferred features are ready.

The active worktree at story creation contained in-progress Story 2.4 changes touching list/search/query actor paths. Coordinate carefully before editing those same files. In particular, `PartySearchResultsBuilder` currently has active development changes that may match contact channels and identifiers, and `SemanticPartySearchProvider` contains broader matching behavior. This story should reconcile that drift against the MVP rule: reserved fields may exist, but active MVP responses must not claim future-field matches.

Temporal preparation must preserve privacy. `NameHistoryEntry.DisplayName` and `NameHistoryEntry.SortName` are personal data, and existing projection tests cover initialization, append behavior, chronological ordering, same-name no-ops, and erasure clearing. If `TemporalNameResult` is touched, ensure display and sort names receive equivalent personal-data treatment or remain non-public/deferred.

Unsupported-capability behavior should be bounded and boring: a stable result or rejection that identifies the capability as deferred, with no backend names, query internals, tenant data, personal data, stack traces, or provider-selection details.

Do not add public REST controllers, OpenAPI routes, MCP tools, AdminPortal UI, picker UI, Memories UI, semantic backend packages, vector stores, embedding clients, graph providers, or event-store temporal query integrations as part of this story.

## Current Code Surfaces

- `src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs`
- `src/Hexalith.Parties.Contracts/Models/MatchMetadata.cs`
- `src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs`
- `src/Hexalith.Parties.Contracts/Models/TemporalNameResult.cs`
- `src/Hexalith.Parties.Contracts/Models/NameHistoryEntry.cs`
- `src/Hexalith.Parties.Contracts/Models/PartySearchBoundary.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
- `src/Hexalith.Parties/Search/PartySearchResultsBuilder.cs`
- `src/Hexalith.Parties/Search/BasicPartySearchProvider.cs`
- `src/Hexalith.Parties/Search/SemanticPartySearchProvider.cs`
- `src/Hexalith.Parties.Projections/Models/PartyDetail.cs`
- `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs`
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerNameHistoryTests.cs`

## Suggested Validation

```powershell
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~BasicPartySearchProviderTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~LocalFuzzyPartySearchProviderTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~SemanticPartySearchProviderTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartySearchServiceBoundaryTests
dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionHandlerNameHistoryTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitecturalFitnessTests
dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~HttpPartiesQueryClientTests
dotnet build Hexalith.Parties.slnx --configuration Release
```

## Anti-Patterns

- Treating enum values such as `Semantic`, `Graph`, or `Hybrid` as shipped MVP support.
- Returning empty success results for unsupported future capabilities when the caller needs a clear unsupported-capability outcome.
- Emitting email, identifier, contact-channel, type, semantic, or graph match metadata from MVP display-name search.
- Adding dependencies on a semantic backend, embedding model, vector store, graph provider, or temporal event-store query surface.
- Logging unsupported search terms, names, identifiers, contact values, or provider internals.

## Deferred Decisions

- Semantic backend selection, embedding model, vector schema, and ranking strategy remain v1.1 or later decisions.
- Temporal query API shape, authorization model, audit metadata, and GDPR export behavior remain v1.1 or Epic 6 decisions.
- Email and identifier search semantics remain deferred and must not be inferred from reserved match-field names.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 2.9 definition and acceptance criteria.
- `_bmad-output/planning-artifacts/prd.md` - FR15, FR16, FR17, and FR72 deferred/preparation boundaries.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-17.md` - preparation-only scope for Story 2.9.
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-05-17.md` - Story 2.9 readiness classification.
- `_bmad-output/project-context.md` - architecture, privacy, package, and submodule constraints.
- `_bmad-output/implementation-artifacts/2-5-search-parties-by-display-name-with-match-metadata.md`
- `_bmad-output/implementation-artifacts/2-6-enforce-tenant-safe-projection-reads.md`
- `_bmad-output/implementation-artifacts/2-7-handle-projection-freshness-and-graceful-degradation.md`
- `_bmad-output/implementation-artifacts/2-8-projection-rebuild-and-health-monitoring.md`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

TBD

### Completion Notes

TBD

### File List

TBD

### Change Log

- 2026-05-19: Story created by BMAD pre-dev hardening automation as a ready-for-dev preparation-only story for deferred search and temporal query extension guardrails.
