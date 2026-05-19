---
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-04c-aggregate
lastStep: step-04c-aggregate
lastSaved: 2026-05-19
storyId: '2.5'
storyKey: 2-5-search-parties-by-display-name-with-match-metadata
storyFile: _bmad-output/implementation-artifacts/2-5-search-parties-by-display-name-with-match-metadata.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-2-5-search-parties-by-display-name-with-match-metadata.md
detectedStack: backend
testFramework: xUnit v3 + Shouldly + NSubstitute
generationMode: ai-generation
generatedTestFiles:
  - tests/Hexalith.Parties.Tests/Search/MvpDisplayNameSearchContractTests.cs
inputDocuments:
  - _bmad-output/implementation-artifacts/2-5-search-parties-by-display-name-with-match-metadata.md
  - _bmad-output/test-artifacts/test-design/test-design-epic-2.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad/tea/config.yaml
  - _bmad-output/project-context.md
  - src/Hexalith.Parties.Contracts/Models/MatchMetadata.cs
  - tests/Hexalith.Parties.Tests/Search/LocalFuzzyPartySearchProviderTests.cs
  - tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/data-factories.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-levels-framework.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-priorities-matrix.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-quality.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-healing-patterns.md
---

# ATDD Checklist — Story 2.5

## Step 01 — Preflight & Context

- **Stack:** backend (.NET 10, xUnit v3, Shouldly, NSubstitute; no Playwright in Parties)
- **Story status:** ready-for-dev (party-mode review + advanced elicitation complete on 2026-05-18)
- **Test design risk anchor:** Epic 2 R-06 (SortName additive), R-08 (MVP search scope creep), R-21 (tie-break determinism)
- **Existing coverage avoided:** `LocalFuzzyPartySearchProviderTests`, `PartySearchServiceBoundaryTests`, `HttpPartiesQueryClientTests`, `EventStoreGatewayRoutingTests`

## Step 02 — Generation Mode

- **Mode:** AI generation from source + story-pinned ACs (backend rule)
- **Recording:** N/A

## Step 03 — Test Strategy

| AC | Scenario | Tier | Priority | Risk |
|---|---|---|---|---|
| AC2/AC4 | MVP exact match emits `displayName`/`exact` only | Tier-1 Unit | P1 | R-08 |
| AC4 | Contact-channel query → no `contactChannel` match field | Tier-1 Unit | P1 | R-08 |
| AC4 | Identifier query → no `identifier` match field | Tier-1 Unit | P1 | R-08 |
| AC3 | Exact ranks above prefix; ties break by party id | Tier-1 Unit | P1 | R-21 |
| AC5 | Whitespace query returns bounded empty | Tier-1 Unit | P2 | — |
| AC6 | Erased entry excluded before metadata calc | Tier-1 Unit | P1 | R-04 |
| AC6 | Cancellation propagates without fallback work | Tier-1 Unit | P2 | R-15 |
| AC2/AC4 | `MatchType` vocabulary allowlist | Tier-1 Unit | P1 | R-08 |

## Step 04 — Generated Tests (RED PHASE)

- `tests/Hexalith.Parties.Tests/Search/MvpDisplayNameSearchContractTests.cs` — 8 facts, all `[Fact(Skip = "...")]` until dev-story activation. Targets the highest-leverage RED scaffolds; broader fitness/topology tests (R-08 provider reachability `2.5-FIT-073`, contracts additivity `R-06`) deferred to dev-story per epic-2 plan.

## Step 04C — Aggregation

- Compilation should pass — facts skipped but signatures use real types (`LocalFuzzyPartySearchProvider`, `LocalPartySearchService`, `PartySearchRequest`, `PartySearchResponse`, `MatchMetadata`).
- No active passing tests added (TDD red phase respected).
- One scaffold file generated; remaining test-design rows (`2.5-FIT-051/072/073`, `2.5-GTW-074/082/142`, `2.5-UNIT-200` tie-breaker fitness across all three providers) deferred to dev-story.

## Dev-Story Handoff

When activating: remove `Skip = "..."` per `[Fact]`, run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~MvpDisplayNameSearchContractTests`, expect RED until the MVP `PartySearch` adapter is constrained to display-name-only and tie-breaker is added to `LocalFuzzyPartySearchProvider`.
