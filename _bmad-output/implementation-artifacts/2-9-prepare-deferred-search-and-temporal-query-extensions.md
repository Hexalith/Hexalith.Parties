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

## Party-Mode Review Clarifications

- Reserved search modes such as `Semantic`, `Graph`, `Hybrid`, email search, identifier search, and temporal name-query concepts are contract-preparation surfaces only. They must not be documented, returned, routed, ranked, or exposed as adopter-facing MVP behavior.
- Active MVP search metadata is limited to display-name matching. MVP responses must not emit email, identifier, contact-channel, semantic score, graph path, provider name, vector, temporal, hybrid, historical-name, duplicate, or type metadata as active match evidence.
- Unsupported future capabilities require a deterministic unsupported-capability outcome, not empty success, partial fallback expansion, or silent display-name substitution. The outcome must identify the capability as deferred without echoing search terms, names, sort names, email addresses, identifiers, contact values, provider names, vector details, backend names, tenant data, stack traces, or internal capability metadata.
- `SemanticPartySearchProvider` must remain either unreachable from MVP runtime paths or a bounded unsupported placeholder with no outbound calls, embeddings, indexing, persistence changes, vector store, graph provider, temporal query engine, or new runtime dependency.
- `TemporalNameResult`, `NameHistoryEntry`, and `PartyDetail.NameHistory` are additive preparation only. This story may preserve or harden stored name-history data, but it must not add temporal query execution, filtering, sorting, public client methods, REST routes, MCP tools, AdminPortal/picker behavior, or Memories integration.
- Placeholder documentation must use language such as "reserved for future compatibility" or "not available in MVP"; it must not imply roadmap commitment, current availability, or partial support.
- Compatibility proof must cover default/null behavior, serialization round trips, older-client tolerance for additive fields, future enum handling, and no required constructor/property changes for existing consumers.
- Dependency guardrails must prove `Hexalith.Parties.Contracts` does not gain hosting, Dapr, MediatR, FluentValidation, UI, server, projection, actor-host, vector, graph, temporal database, semantic backend, or concrete infrastructure dependencies; the Parties actor host must not gain public REST/OpenAPI/MCP exposure.

## Advanced Elicitation Hardening

- Treat `PartySearchMode.Hybrid`, `PartySearchMode.Semantic`, and `PartySearchMode.Graph` as the highest-risk compatibility seams because their names can be mistaken for active support. This story should either make those modes unreachable from MVP callers or prove they resolve to the same bounded unsupported-capability path every time.
- `PartySearchExecutionStatus.Rich`, score metadata, and source metadata must not be used to imply semantic, graph, hybrid, memory, or temporal enrichment in MVP. If the fields remain for future compatibility, tests should prove MVP display-name search leaves future-only score/source values absent or inert.
- Negative unsupported-mode assertions must verify both the result shape and the absence of side effects: no projection mutation, no provider/network call, no dependency resolution attempt for future backends, no log/telemetry payload containing query text, tenant data, names, contact values, identifiers, provider names, vectors, graph paths, or stack traces.
- `SemanticPartySearchProvider` currently performs enhanced local matching, including contact-channel, identifier, and type-text matches. Development must reconcile that implementation against the story's display-name-only MVP rule rather than assuming the provider name means deferred semantic search is already safely gated.
- Temporal preparation must distinguish retained projection history from public temporal retrieval. `NameHistoryEntry` and `PartyDetail.NameHistory` may remain available where existing detail surfaces already expose them, but this story must not add an `asOf` query contract, route, client method, MCP argument, picker/admin filter, or documentation that describes temporal lookup as callable.
- Compatibility evidence should include source-level or reflection-style guardrails for constructor/init-only changes, enum serialization tolerance, null/default behavior for new optional fields, and package dependency closure so older consumers do not need code or package changes to keep using current MVP search.
- Defer any final decision that requires a new public unsupported-capability contract. If existing boundaries cannot express a bounded unsupported outcome without changing product/API semantics, record the gap instead of inventing a new cross-story policy inside this story.

## AC-to-Test Traceability

| AC | Required focused evidence |
| --- | --- |
| 1 | Unit tests prove display-name search is the only active match path and MVP match metadata excludes email, identifier, contact-channel, semantic, graph, hybrid, temporal, historical-name, provider, vector, duplicate, and type fields. |
| 2 | Architecture/package tests or dependency inspection prove no semantic backend, embedding model, vector store, graph provider, temporal query engine, provider/network call, or new runtime dependency is required by MVP wiring. |
| 3 | Projection/name-history tests prove name changes are retained for future preparation and erased or suppressed per privacy rules, while route/client/UI scans prove no temporal query surface is callable. |
| 4 | Compatibility tests cover additive DTO/enum changes, optional/default-safe fields, serialization round trips, older-client tolerance, source compatibility for current query flows, and package dependency closure. |
| 5 | Negative tests cover `Semantic`, `Graph`, `Hybrid`, temporal, email, and identifier requests returning explicit unsupported outcomes with no empty success, fallback expansion, future-field metadata, personal data, provider internals, or tenant leakage. |
| 6 | Architecture fitness or code-review evidence confirms no REST, OpenAPI, MCP, AdminPortal, picker, Memories, semantic runtime, graph runtime, vector runtime, or temporal-query expansion was introduced. |

## Tasks

- [ ] Audit current search and temporal contract surfaces.
  - [ ] Confirm `PartySearchResult`, `MatchMetadata`, and related DTOs preserve display-name match metadata without enabling email or identifier search behavior.
  - [ ] Review `PartySearchMode`, `PartySearchBoundary`, `SemanticPartySearchProvider`, and query/client wiring for feature flags or bounded unsupported behavior.
  - [ ] Review `TemporalNameResult`, `NameHistoryEntry`, and `PartyDetail.NameHistory` for personal-data markings and erasure behavior.
- [ ] Reserve future search extension points without broadening MVP behavior.
  - [ ] Document `email`, `identifier`, semantic, graph, and hybrid match concepts as future/deferred where they appear.
  - [ ] Add or tighten tests that fail if MVP display-name search emits future-field match metadata.
  - [ ] Ensure any future-mode enum values are either unreachable from MVP paths or return bounded unsupported-capability responses.
  - [ ] Add negative tests for `Semantic`, `Graph`, `Hybrid`, temporal-name, email, and identifier paths that prove unsupported outcomes are deterministic, privacy-safe, and not empty-success fallbacks.
  - [ ] Prove unsupported requests do not invoke semantic, graph, vector, temporal, memory, provider-network, or dependency-resolution paths.
- [ ] Preserve temporal-query preparation without exposing a temporal feature.
  - [ ] Keep event/projection history sufficient for future name-as-of reads.
  - [ ] Verify erased parties clear or suppress name history according to existing privacy rules.
  - [ ] Avoid adding public temporal query APIs, client methods, REST routes, MCP tools, or UI affordances unless the only behavior is explicit unsupported-capability handling.
- [ ] Add compatibility and architecture guardrails.
  - [ ] Cover additive DTO compatibility and existing `IPartiesQueryClient` search behavior.
  - [ ] Add architecture fitness checks if needed to prevent required semantic backend dependencies in MVP packages.
  - [ ] Cover serialization/source compatibility for placeholder fields and future enum values, including default/null behavior for older consumers.
  - [ ] Cover score/source metadata defaults so MVP callers cannot observe semantic, graph, hybrid, memory, provider, or temporal provenance as active data.
  - [ ] Confirm no personal data is written to logs, telemetry, unsupported responses, or test diagnostics.
  - [ ] Confirm Contracts and Parties do not gain semantic/vector/graph/temporal runtime dependencies or public REST/OpenAPI/MCP/AdminPortal/picker/Memories expansion.
- [ ] Update developer-facing documentation.
  - [ ] Record deferred semantic search and temporal query behavior in the relevant architecture or implementation notes.
  - [ ] Explain which placeholders are reserved and which fields are active in MVP, using explicit "not available in MVP" or "reserved for future compatibility" language.

## Dev Notes

This is a preparation-only story. It reserves extension points and hardens compatibility boundaries for v1.1 work; it does not deliver semantic search or temporal name-query runtime behavior.

Story 2.5 established that public MVP search is display-name-only and must not emit email, contact-channel, identifier, semantic, graph, duplicate, or type metadata as active matches. Current source surfaces include future-looking types and providers, including `PartySearchMode.Semantic`, `PartySearchMode.Graph`, `SemanticPartySearchProvider`, `TemporalNameResult`, `NameHistoryEntry`, and `PartyDetail.NameHistory`. Treat these as extension surfaces to gate, document, or test, not as proof that the deferred features are ready.

The active worktree at story creation contained in-progress Story 2.4 changes touching list/search/query actor paths. Coordinate carefully before editing those same files. In particular, `PartySearchResultsBuilder` currently has active development changes that may match contact channels and identifiers, and `SemanticPartySearchProvider` contains broader matching behavior. This story should reconcile that drift against the MVP rule: reserved fields may exist, but active MVP responses must not claim future-field matches.

Temporal preparation must preserve privacy. `NameHistoryEntry.DisplayName` and `NameHistoryEntry.SortName` are personal data, and existing projection tests cover initialization, append behavior, chronological ordering, same-name no-ops, and erasure clearing. If `TemporalNameResult` is touched, ensure display and sort names receive equivalent personal-data treatment or remain non-public/deferred.

Unsupported-capability behavior should be bounded and boring: a stable result or rejection that identifies the capability as deferred, with no backend names, query internals, tenant data, personal data, stack traces, or provider-selection details.

Do not add public REST controllers, OpenAPI routes, MCP tools, AdminPortal UI, picker UI, Memories UI, semantic backend packages, vector stores, embedding clients, graph providers, or event-store temporal query integrations as part of this story.

If UI/admin/picker text is touched only to keep unsupported behavior hidden or clearly unavailable, preserve localization and accessibility conventions and do not introduce new advanced-search affordances.

## Current Code Surfaces

- `src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs`
- `src/Hexalith.Parties.Contracts/Models/MatchMetadata.cs`
- `src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs`
- `src/Hexalith.Parties.Contracts/Models/TemporalNameResult.cs`
- `src/Hexalith.Parties.Contracts/ValueObjects/NameHistoryEntry.cs`
- `src/Hexalith.Parties/Search/PartySearchBoundary.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
- `src/Hexalith.Parties/Search/PartySearchResultsBuilder.cs`
- `src/Hexalith.Parties/Search/BasicPartySearchProvider.cs`
- `src/Hexalith.Parties/Search/SemanticPartySearchProvider.cs`
- `src/Hexalith.Parties.Projections/Models/PartyDetail.cs`
- `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs`
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerNameHistoryTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs`

## Suggested Validation

```powershell
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~BasicPartySearchProviderTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~LocalFuzzyPartySearchProviderTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~SemanticPartySearchProviderTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartySearchServiceBoundaryTests
dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionHandlerNameHistoryTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitecturalFitnessTests
dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~HttpPartiesQueryClientTests
dotnet package list src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj --include-transitive
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
- Final unsupported-capability representation, including typed domain result versus validation failure versus future capability-negotiation metadata, remains a product/architecture decision if the current boundary cannot satisfy the bounded unsupported behavior locally.
- Future AdminPortal, picker, or adopter-facing exposure for advanced search remains out of scope.

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

## Party-Mode Review

- Date/time: 2026-05-20T11:43:31+02:00
- Selected story key: `2-9-prepare-deferred-search-and-temporal-query-extensions`
- Command/skill invocation used: `/bmad-party-mode 2-9-prepare-deferred-search-and-temporal-query-extensions; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - Winston recommended `ready-for-dev` with minor clarifications; Amelia, Murat, and John recommended `needs-story-update`, not blocked, until unsupported-capability behavior, AC-to-test traceability, match-metadata bounds, privacy-safe negative assertions, compatibility evidence, and dependency guardrails were made explicit.
  - Shared risk centered on future-looking contracts or providers being interpreted as shipped semantic, graph, hybrid, email, identifier, or temporal search support.
  - Reviewers agreed the story remains preparation-only and ready after low-risk clarification because it does not require product-scope, architecture-policy, or cross-story contract changes.
- Changes applied:
  - Added `Party-Mode Review Clarifications` defining reserved-only search modes, display-name-only active metadata, explicit unsupported outcomes, placeholder constraints for `SemanticPartySearchProvider`, temporal preparation-only boundaries, documentation wording, compatibility proof, and dependency guardrails.
  - Added `AC-to-Test Traceability` mapping each acceptance criterion to focused tests, architecture checks, dependency inspection, privacy-safe negative assertions, and code-review evidence.
  - Expanded tasks and validation guidance for unsupported-mode negative tests, additive compatibility, package dependency inspection, public-surface guardrails, and localization/accessibility preservation if any UI text is touched.
- Findings deferred:
  - Semantic/vector provider selection, graph/hybrid search behavior, temporal query API/indexing/authorization model, public advanced-search UI exposure, and final unsupported-capability representation remain future product/architecture decisions.
- Final recommendation: `ready-for-dev`

## Advanced Elicitation

- Date/time: 2026-05-20T18:04:25+02:00
- Selected story key: `2-9-prepare-deferred-search-and-temporal-query-extensions`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-9-prepare-deferred-search-and-temporal-query-extensions`
- Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Security Audit Personas; Self-Consistency Validation; Architecture Decision Records
- Reshuffled Batch 2 method names: Pre-mortem Analysis; Chaos Monkey Scenarios; User Persona Focus Group; Critique and Refine; Expand or Contract for Audience
- Findings summary:
  - Future-looking search mode names, score/source metadata, and the existing `SemanticPartySearchProvider` can be misread as active semantic, graph, hybrid, email, identifier, contact-channel, or type search support unless tests prove they are gated or inert for MVP consumers.
  - The story needed sharper no-side-effect evidence for unsupported future capabilities: no backend/provider calls, no dependency resolution, no projection mutation, and no personal or tenant data in unsupported responses, logs, telemetry, or diagnostics.
  - Temporal preparation needed a clearer distinction between preserved name history and a callable temporal query feature, especially because detail surfaces may already expose `PartyDetail.NameHistory`.
  - Current-code references for `PartySearchBoundary` and `NameHistoryEntry` were stale and could send the dev agent to the wrong files.
- Changes applied:
  - Added `Advanced Elicitation Hardening` with explicit guardrails for deferred mode names, score/source metadata, unsupported-mode side effects, `SemanticPartySearchProvider`, temporal exposure, compatibility evidence, and deferred unsupported-capability contract policy.
  - Tightened AC-to-test traceability for provider/network calls, dependency closure, and compatibility evidence.
  - Expanded tasks for unsupported-mode side-effect proof and score/source metadata defaults.
  - Corrected current-code surface paths for `PartySearchBoundary` and `NameHistoryEntry`, and added the personal-data inventory test reference.
- Findings deferred:
  - Final unsupported-capability contract shape remains a product/architecture decision if existing boundaries cannot express the bounded unsupported outcome safely.
  - Semantic/vector provider selection, graph/hybrid ranking, temporal query API shape, and public adopter-facing exposure remain future decisions.
- Final recommendation: `ready-for-dev`

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

- 2026-05-20: Advanced elicitation applied guardrails for deferred mode names, metadata defaults, unsupported-mode side effects, temporal exposure boundaries, compatibility proof, and corrected current-code surface paths.
- 2026-05-20: Party-mode review applied low-risk clarifications for unsupported future capabilities, display-name-only MVP metadata, privacy-safe negative assertions, compatibility evidence, dependency guardrails, and AC-to-test traceability.
- 2026-05-19: Story created by BMAD pre-dev hardening automation as a ready-for-dev preparation-only story for deferred search and temporal query extension guardrails.
