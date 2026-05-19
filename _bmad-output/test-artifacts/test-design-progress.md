---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-05-19'
mode: 'epic-level'
scope: 'Epic 2 — Tenant-Safe Party Search and Retrieval'
finalOutput: '_bmad-output/test-artifacts/test-design/test-design-epic-2.md'
inputDocuments:
  - '_bmad-output/project-context.md'
  - '_bmad-output/planning-artifacts/architecture.md'
  - '_bmad-output/planning-artifacts/prd.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/implementation-artifacts/sprint-status.yaml'
  - '_bmad-output/implementation-artifacts/2-1-build-party-detail-projection.md'
  - '_bmad-output/implementation-artifacts/2-2-build-tenant-party-index-projection.md'
  - '_bmad-output/implementation-artifacts/2-3-query-party-details-by-id.md'
  - '_bmad-output/implementation-artifacts/2-4-list-and-filter-parties.md'
  - '_bmad-output/implementation-artifacts/2-5-search-parties-by-display-name-with-match-metadata.md'
  - '_bmad-output/implementation-artifacts/2-6-enforce-tenant-safe-projection-reads.md'
  - '_bmad-output/implementation-artifacts/2-7-handle-projection-freshness-and-graceful-degradation.md'
  - '_bmad-output/implementation-artifacts/2-8-projection-rebuild-and-health-monitoring.md'
  - '_bmad-output/implementation-artifacts/2-9-prepare-deferred-search-and-temporal-query-extensions.md'
---

# Test Design Progress — Epic 2 (Tenant-Safe Party Search and Retrieval)

## Step 01 — Mode Detection & Prerequisites

### Mode
**Epic-Level Mode** confirmed by explicit user intent (selected via mode-determination prompt: "Create — new test design (Epic 2)").

### Scope
Epic 2: Tenant-Safe Party Search and Retrieval, comprising 9 stories (2.1–2.9) per `_bmad-output/implementation-artifacts/sprint-status.yaml`:

| Story | Title | Status |
|-------|-------|--------|
| 2.1 | Build Party Detail Projection | done |
| 2.2 | Build Tenant Party Index Projection | done |
| 2.3 | Query Party Details by ID | done |
| 2.4 | List and Filter Parties | review |
| 2.5 | Search Parties by Display Name with Match Metadata | ready-for-dev |
| 2.6 | Enforce Tenant-Safe Projection Reads | ready-for-dev |
| 2.7 | Handle Projection Freshness and Graceful Degradation | ready-for-dev |
| 2.8 | Projection Rebuild and Health Monitoring | ready-for-dev |
| 2.9 | Prepare Deferred Search and Temporal Query Extensions | ready-for-dev |

### Prerequisites Check
- ✅ Epic and per-story requirements with acceptance criteria available under `_bmad-output/implementation-artifacts/2-*.md` (9 stories)
- ✅ Architecture context: `_bmad-output/planning-artifacts/architecture.md`, `prd.md`, `epics.md`
- ✅ Sprint status surface: `_bmad-output/implementation-artifacts/sprint-status.yaml`
- ✅ Project rules captured in `project-context.md` (Parties + Commons + EventStore submodule contexts)

### Mode Rationale
Stories 2.1, 2.2, 2.3 are landed (`done`); 2.4 is `review`; 2.5–2.9 are `ready-for-dev`. Test design serves two purposes:
1. **Forward-looking quality gates** for the 5 stories about to enter implementation (2.5–2.9), where projection-freshness, rebuild, tenant fail-closed, search ranking, and deferred-capability seams concentrate the highest residual risk.
2. **Retrospective coverage assessment** for the 4 landed/in-review stories, focused on cross-tenant leakage, projection idempotency, and the read-path contract surfaced by the EventStore-fronted gateway (Epic 12 dependency).

### Observations / Open Items
- Stories 2.9, 5.6, 5.7 are explicitly preparation-only per the 2026-05-17 implementation-readiness cleanup note in `sprint-status.yaml` — their tests must not assert behavior of deferred capabilities.
- Story 2.4 is mid-review with patches in flight (cf. 2026-05-19 dev-story note in sprint-status.yaml). Treat its acceptance criteria as authoritative but flag any patch-derived behavior changes in the risk table.
- Tenant-safe read enforcement (Story 2.6) is a cross-cutting concern; its risks will appear in scenarios for 2.1–2.5, 2.7, 2.8 as well.

---

## Step 02 — Context & Knowledge Loaded

### Stack Detection
- **Detected stack**: `fullstack` — .NET 10 backend (12 src csproj) + Blazor frontend (AdminPortal, Picker via bUnit). No JS toolchain (no `playwright.config.*`, `cypress.config.*`, or root `package.json`).
- **Config alignment issue (carried from prior Epic 12 run, non-blocking)**: `_bmad/tea/config.yaml` flags `tea_use_playwright_utils=true`, `tea_use_pactjs_utils=true`, `tea_pact_mcp=mcp`. These are JS-stack flags and not applicable here; Playwright Utils, Pact.js Utils, and Pact MCP knowledge fragments were intentionally skipped.

### Epic 2 Story Acceptance Criteria Summary

Cross-cutting AC themes across 9 stories (2.1–2.9):

| Theme | Stories | Cross-Cutting Behaviour |
|-------|---------|-------------------------|
| Pure-handler projection replay | 2.1, 2.2 | `PartyDetailProjectionHandler` and `PartyIndexProjectionHandler` are infrastructure-free; rejection events are projection no-ops; idempotency by stable ids; no-op timestamp preservation |
| Actor wrapper key shape | 2.1, 2.2, 2.3, 2.4, 2.6, 2.8 | Detail actor `{tenant}:party-detail:{partyId}`; index actor `{tenant}:party-index` with state key `{tenant}:party-index:{partitionKey}`; malformed-id fail-closed |
| EventStore-fronted query boundary | 2.3, 2.4, 2.5, 2.6, 2.7 | All public reads go through `api/v1/queries` via `IPartiesQueryClient`; no REST/MCP/AdminPortal-only paths in `src/Hexalith.Parties` |
| Query-adapter actors | 2.3, 2.4 | `PartyDetailProjectionQueryActor` + `PartyIndexProjectionQueryActor` map `PartyDetail`/`PartyIndex` queries onto projection-side reads |
| Tenant fail-closed | 2.3, 2.4, 2.5, 2.6, 2.7, 2.8 | Tenant identity is derived ONLY from authenticated EventStore query context; payload/cursor/actor-id/UI/cache values are never authoritative; no alternate-key probing |
| Active/erased/inactive filtering | 2.4, 2.5, 2.6 | `IsErased == true` excluded before metadata; inactive parties remain inspectable unless erased; active-filter behaviour pinned by tests |
| Display-name MVP search | 2.5, 2.9 | MVP `PartySearch` searches only `DisplayName`; no email/identifier/contact/semantic/graph/duplicate match metadata; deterministic ranking (exact > prefix > contains > optional fuzzy) |
| Degraded / freshness boundaries | 2.3, 2.4, 2.5, 2.6, 2.7, 2.8 | Cached reads require proven tenant/projection/partition/party/erasure-filter/cache-currency provenance; bounded vocabulary {current, stale, rebuilding, degraded, local-only, unavailable}; cross-tenant non-enumeration |
| Rebuild safety | 2.8 | `ProjectionRebuildService` replays via pure handlers; same actor id/state key shapes; failed/unfinished rebuilds fail closed; same-tenant cache only |
| Privacy-safe diagnostics | 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8 | No display names, contact values, identifiers, raw JSON, actor/state keys, stream names, tokens, infrastructure exception text in logs/ProblemDetails/telemetry/test snapshots |
| MVP scope discipline | 2.4, 2.5, 2.9 | No `GET /api/v1/parties/*`, no MCP `find_parties`/`get_party`, no Memories/semantic/graph search promised in MVP; `Semantic`/`Graph` enum values reserved-only |
| Temporal preparation | 2.9 | `NameHistoryEntry` retains chronology for future temporal queries; erasure clears history; no public temporal API in MVP |

### Test Surface Inventory (Existing — Epic-2-relevant)

| Project / Folder | Tier | Existing Coverage |
|---|---|---|
| `Hexalith.Parties.Projections.Tests/Handlers` | Tier-1 | `PartyDetailProjectionHandlerTests`, `PartyDetailProjectionHandlerNameHistoryTests`, `PartyDetailProjectionHandlerRejectionFitnessTests`, `PartyIndexProjectionHandlerTests` (post-2.1/2.2 hardened) |
| `Hexalith.Parties.Projections.Tests/Actors` | Tier-1 | `PartyEventTypeResolverTests` |
| `Hexalith.Parties.Client.Tests` | Tier-1/2 | `HttpPartiesQueryClientTests` (detail/list/search request shape + error mapping + cancellation); `ClientArchitecturalFitnessTests` (thin-wrapper boundary) |
| `Hexalith.Parties.Tests/Gateway` | Tier-2 (WebApplicationFactory) | `EventStoreGatewayRoutingTests`, `PartyDetailProjectionQueryActorTests`, `PartyIndexProjectionQueryActorTests`, `PartiesProcessEndpointTests` |
| `Hexalith.Parties.Tests/Projections` | Tier-1/2 | `PartyDetailProjectionActorCorruptionTests`, `PartyDetailProjectionActorExtensionsTests`, `PartyIndexProjectionActorCorruptionTests`, `ProjectionRebuildServiceTests` |
| `Hexalith.Parties.Tests/HealthChecks` | Tier-1/2 | `DegradedResponseMiddlewareTests`, `ProjectionActorsHealthCheckTests`, `Dapr{PubSub,Sidecar,StateStore}HealthCheckTests`, `HealthEndpointIntegrationTests`, `MemoriesSearchHealthCheckTests`, `TenantsIntegrationHealthCheckTests` |
| `Hexalith.Parties.Tests/Search` | Tier-1/2 | `BasicPartySearchProviderTests`, `LocalFuzzyPartySearchProviderTests` (+ perf benchmark), `MemoriesPartySearchServiceTests`, `PartyMemoryCleanupServiceTests`, `PartyMemorySearchOptionsValidatorTests`, `PartyMemoryUnitMapperTests`, `PartySearchServiceBoundaryTests`, `SemanticPartySearchProviderTests` (+ perf benchmark) |
| `Hexalith.Parties.Tests/FitnessTests` | Topology fitness | `ArchitecturalFitnessTests`, `ContractsArchitectureFitnessTests`, `AppHostTenantsTopologyTests`, `PartyStateApplyOrderingFitnessTests`, `PartyStateRejectionApplyEndToEndTests` |
| `Hexalith.Parties.IntegrationTests/Gateway` | Tier-3 (Aspire) | `EventStoreGatewayE2ETests` |
| `Hexalith.Parties.IntegrationTests/HealthChecks` | Tier-3 (Aspire) | `HealthEndpointE2ETests` |

### Knowledge Fragments Loaded
- ✅ `risk-governance.md` (core) — risk scoring matrix, gate decision engine, mitigation tracking, traceability matrix
- ✅ `probability-impact.md` (core) — probability/impact scale (1-9), DOCUMENT/MONITOR/MITIGATE/BLOCK thresholds
- ✅ `test-levels-framework.md` (core) — unit/integration/E2E selection rules, anti-patterns
- ✅ `test-priorities-matrix.md` (core) — P0/P1/P2/P3 criteria, coverage targets, execution order

### Knowledge Fragments Skipped (Justified)
- ❌ `pactjs-utils-*` and `contract-testing.md` — JS Pact tooling; .NET stack uses xUnit + WebApplicationFactory + Testcontainers
- ❌ `pact-mcp.md` — JS Pact MCP not in scope
- ❌ Playwright Utils — JS testing utilities not applicable
- ❌ `playwright-cli.md` — Blazor Server admin portal/picker use bUnit; E2E browser tests not in Epic 2 scope

### Persistent Facts (loaded via customize.toml `persistent_facts`)
- `_bmad-output/project-context.md` (PartyState Apply ordering rule, REST/MCP no-go in actor host, EventStore-owned gateway auth, projection-side tenant fail-closed, `[PersonalData]` marking, central package management, no recursive submodules)

---

## Step 03 — Risk Assessment

Risks scored on probability × impact (1-9 scale). Categories: TECH (architecture fragility), SEC (security), PERF (performance), DATA (integrity / GDPR), BUS (business logic), OPS (operations / observability).

Threshold rules: 1-3 DOCUMENT (P3), 4-5 MONITOR (P2), 6-8 MITIGATE (P1), 9 BLOCK (P0).

### Risk Matrix (Sorted by Score Descending)

| ID | Cat | Risk | P | I | Score | Action | Stories | Confidence | Rationale Source |
|----|-----|------|---|---|-------|--------|---------|------------|-------------------|
| **R-01** | SEC | Cross-tenant data leakage through projection reads (detail wrong-tenant probe, index list cross-tenant counts, search cross-tenant matches, static cache fallback returning prior-tenant entries, malformed-actor-id probing into other tenant) | 3 | 3 | **9** | **BLOCK (P0)** | 2.3, 2.4, 2.5, 2.6, 2.7 | 9 | Story 2.6 AC1-5; Story 2.3 AC2, 2.4 AC1, 2.5 AC1; project-context.md tenant fail-closed rule |
| **R-02** | SEC | Personal data / token / raw payload / actor-key / state-key / stream-name leakage through diagnostics: `LogError`/`LogWarning`/ProblemDetails/telemetry dimensions/exception messages/test snapshots across detail, list, search, rebuild, and degraded paths | 3 | 3 | **9** | **BLOCK (P0)** | 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8 | 8 | Story 2.6 AC7; Story 2.3 AC5; Story 2.4 AC6; Story 2.5 AC5/6; Story 2.7 AC3; Story 2.8 AC6/7; project-context.md `[PersonalData]` rule |
| R-03 | SEC | Tenant identity accepted from untrusted source — payload tenant-like fields, page cursors, `caseId`, graph context ids, actor ids, partition keys, UI/client metadata, source metadata, projection payloads override authenticated EventStore tenant context | 2 | 3 | 6 | MITIGATE (P1) | 2.3, 2.4, 2.5, 2.6 | 9 | Story 2.6 AC2; Story 2.3 advanced elicitation; Story 2.4 AC1; Story 2.5 AC1 |
| R-04 | DATA | Erased-party PII resurfaces through search, list, detail, rebuild, or static cache (missed `IsErased` filter on cache fallback, name-history projection, semantic provider, AdminPortal local fallback) | 2 | 3 | 6 | MITIGATE (P1) | 2.2, 2.4, 2.5, 2.6, 2.8 | 8 | Story 2.4 AC, Story 2.5 boundaries, Story 2.6 AC4-6; Story 2.3 review defer "Erase+rebuild race may resurrect erased payload via static cache" |
| R-05 | DATA | Stale / degraded / rebuilding cache returns mixed-provenance entries — `static s_lastKnownDetails` and `s_lastKnownEntries` dictionaries have no eviction and are not coordinated with `EraseAsync` for entries from prior actor incarnations | 2 | 3 | 6 | MITIGATE (P1) | 2.3, 2.6, 2.7, 2.8 | 7 | Story 2.7 Party-Mode clarifications "trusted degraded reads must prove tenant/projection/partition/party/erasure/cache-currency provenance"; Story 2.3 review deferred static-cache lifecycle |
| R-06 | DATA | `PartyDisplayNameDerived.SortName` re-becomes `required` (or other event-contract additive guarantee regresses) → legacy event deserialization breaks → EventStore rehydration fails on historical streams | 2 | 3 | 6 | MITIGATE (P1) | 2.2 (patched, regression risk for 2.5, 2.9) | 8 | Story 2.2 patch P3 "additive default empty"; Story 2.5/2.9 may touch contracts; project-context.md "Contracts/events must remain additive" |
| R-07 | DATA | Rejection-event Apply ordering regression — refactor/auto-format reorders `PartyState.Apply` methods → EventStore suffix-match misrouting → silent state corruption on rehydration | 2 | 3 | 6 | MITIGATE (P1) | (cross-cutting invariant, all 9 stories) | 9 | project-context.md explicit rule; existing `PartyStateApplyOrderingFitnessTests` + `PartyStateRejectionApplyEndToEndTests` |
| R-08 | SEC | MVP search scope creep — `LocalFuzzyPartySearchProvider` already matches contact channels and identifiers; if MVP `PartySearch` path emits `email`/`identifier`/`contactChannel`/`semantic`/`graph`/`duplicate`/`type` match metadata, the public contract claims unavailable capability and leaks future-reserved metadata | 3 | 2 | 6 | MITIGATE (P1) | 2.5, 2.9 | 9 | Story 2.5 AC4; Story 2.9 AC1, 2, 5; existing `LocalFuzzyPartySearchProvider`/`SemanticPartySearchProvider` already broader; Story 2.2 review patch P4 codifies bounded match metadata |
| R-09 | DATA | List/filter/search metadata (`TotalCount`, `TotalPages`, empty-page behavior, match metadata, score metadata, source metadata) leaks cross-tenant counts because computed before authorization, erasure filtering, or full filter chain | 2 | 3 | 6 | MITIGATE (P1) | 2.4, 2.5, 2.6 | 8 | Story 2.4 advanced elicitation "filter-before-metadata ordering"; Story 2.5 AC6; Story 2.6 AC4-5 |
| R-10 | BUS | Freshness/degradation metadata leaks cross-tenant existence — sequence positions, cache ages, rebuild state, partition keys, projection positions, health-check details, or response headers reveal another tenant's activity | 2 | 3 | 6 | MITIGATE (P1) | 2.7, 2.8 | 8 | Story 2.7 AC6 non-enumerating; Party-Mode Clarifications bounded vocabulary; Story 2.8 AC7 |
| R-11 | TECH | Display-name normalization drift between aggregate (`PartyAggregate.DeriveDisplayName`) and projection (`PartyIndexProjectionHandler.DeriveSortName`) → `state.SortName == e.SortName` fails on every replay → `LastModifiedAt` bumps incorrectly → AC2.2.5 idempotency broken | 2 | 2 | 4 | MONITOR (P2) | 2.2 (patched), 2.5 (re-emergence risk) | 9 | Story 2.2 review patch P1; risk re-emerges if 2.5 implements additional normalization |
| R-12 | TECH | REST/MCP/AdminPortal-only surface reintroduction in `src/Hexalith.Parties` — future stories accidentally re-add `MapControllers`, `MapMcp`, `[ApiController]`, `[McpServerTool]`, AdminPortal-direct calls, or `GET /api/v1/parties{,/search,/{id}}` endpoints | 2 | 2 | 4 | MONITOR (P2) | 2.3, 2.4, 2.5, 2.6, 2.8 (anti-pattern in all) | 9 | project-context.md "do not reintroduce public REST/Swagger/MCP"; existing `ArchitecturalFitnessTests` |
| R-13 | PERF | Projection rebuild latency stalls query path — long rebuild blocks query throughput; static cache cannot refresh during rebuild; tenant-safe degraded reads fail closed for rebuild duration | 2 | 2 | 4 | MONITOR (P2) | 2.7, 2.8 | 5 → 7 | No explicit perf AC; **capture-only baseline** rather than threshold (confidence-gate lift) |
| R-14 | OPS | Failed / canceled / write-failure rebuild leaves projection stuck in rebuilding-degraded forever — `ProjectionRebuildService` write failure, checkpoint-delete failure, mid-flight cancellation, or actor-host crash mid-rebuild | 2 | 2 | 4 | MONITOR (P2) | 2.8 | 7 | Story 2.8 AC6 explicit fail-closed; existing `ProjectionRebuildServiceTests` partial coverage |
| R-15 | TECH | Cancellation not terminal — after CT cancel observed, fallback aggregate replay / detail fan-out / Memories expansion / retired REST calls / cache refresh / retries kick off | 2 | 2 | 4 | MONITOR (P2) | 2.3, 2.4, 2.5, 2.6, 2.7, 2.8 | 8 | Stories 2.3-2.8 all require terminal cancellation; current adapter actors lack CT plumbing through Dapr framework |
| R-16 | BUS | Active filter default behavior inconsistency — silently hiding inactive parties on `active=null`/no-filter; AdminPortal trust impact | 2 | 2 | 4 | MONITOR (P2) | 2.4 | 6 | Story 2.4 advanced elicitation "preserve accepted default in tests"; default not currently pinned |
| R-17 | DATA | Date filter overflow / locale ambiguity — large page/page-size values overflow into negative skip; server-locale vs UTC date parsing inconsistency; start-after-end validation drift | 2 | 2 | 4 | MONITOR (P2) | 2.4 | 8 | Story 2.4 AC4, AC5; advanced elicitation "UTC/culture-invariant" |
| R-18 | BUS | Story 2.9 temporal/semantic capability surfaces accidentally exposed in MVP — `PartySearchMode.Semantic`/`Graph`/`Hybrid` enum reachable in production path; `TemporalNameResult`/`NameHistoryEntry` exposed via public API | 2 | 2 | 4 | MONITOR (P2) | 2.5, 2.9 | 7 | Story 2.9 AC2, 3, 5, 6; existing `SemanticPartySearchProvider` is broader than MVP contract |
| R-19 | OPS | Health / readiness endpoints become an alternate authorization surface — `IsRebuildingAsync` probed without auth context; `/health`, `/alive`, `/ready` reveal tenant existence indirectly through degraded markers; `ProjectionActorsHealthCheck` response leaks projection internals | 1 | 3 | 3 | DOCUMENT (P3) | 2.7, 2.8 | 5 → 7 | Story 2.7 deferred; Story 2.8 AC7 bounded status; **lift via explicit health-output PII scrub test** |
| R-20 | DATA | `HandleProcessingRestricted` exact-time `DateTimeOffset` dedup misses clock-skew / replay-recompute cases (pre-existing pattern, deferred from Story 2.1 review) | 1 | 3 | 3 | DOCUMENT (P3) | (cross-cutting) | 6 | Story 2.1 review deferred; needs design call on no-op vs repair |
| R-21 | DATA | Pagination tie-break non-deterministic across providers — `LocalFuzzyPartySearchProvider.results.Sort` uses unstable introsort; `SemanticPartySearchProvider` uses stable `OrderBy.ThenBy`; identical score + identical SortName + identical DisplayName produces non-deterministic pagination | 2 | 1 | 2 | DOCUMENT (P3) | 2.4, 2.5 | 7 | Story 2.2 review deferred; fix is "add `Id` as final tie-breaker across all providers" |

### Risk Distribution

| Tier | Count | IDs |
|------|-------|-----|
| **P0 — BLOCK** (score 9) | 2 | R-01, R-02 |
| **P1 — MITIGATE** (score 6-8) | 8 | R-03, R-04, R-05, R-06, R-07, R-08, R-09, R-10 |
| **P2 — MONITOR** (score 4-5) | 8 | R-11, R-12, R-13, R-14, R-15, R-16, R-17, R-18 |
| **P3 — DOCUMENT** (score 1-3) | 3 | R-19, R-20, R-21 |
| **Total** | **21** | |

### Category Distribution

| Category | Count | Highest Score |
|----------|-------|---------------|
| SEC | 4 | 9 (R-01, R-02) |
| DATA | 9 | 6 (R-04, R-05, R-06, R-09) |
| BUS | 3 | 6 (R-10) |
| TECH | 3 | 4 (R-11, R-12, R-15) |
| PERF | 1 | 4 (R-13) |
| OPS | 1 | 4 (R-14) |

### Confidence Gate Application

Risks below confidence-7 are flagged for evidence-gathering before final mitigation plans:

- **R-13 (PERF latency, confidence 5)**: No explicit performance AC in Epic 2 stories. The PRD MCP latency target (Epic 4) and projection consistency target (FR19) need verification before we can set a numeric SLO. **Action: treat as capture-only baseline; rebuild-stall scenarios assert "no degraded-success masquerade", not numeric latency thresholds. Lifts to confidence 7.**
- **R-19 (Health endpoints alternate auth, confidence 5)**: Story 2.7 alludes to "freshness metadata is itself sensitive when it could reveal another tenant's existence", but no explicit health-output PII fitness test exists. **Action: explicit fitness/integration test asserting `/health`, `/alive`, `/ready`, and `ProjectionActorsHealthCheck` response payloads contain only bounded category strings — no tenant ids, projection counts, partition keys, sequence positions, raw exception text. Lifts to confidence 7.**
- **R-16 (Active filter default, confidence 6)**: Story 2.4 says "preserve accepted current behavior" without naming it. **Action: a Tier-2 query test pins the current `active=null` default behavior so future stories cannot silently flip it.**
- **R-20 (`HandleProcessingRestricted` clock-skew, confidence 6)**: Pre-existing pattern; risk depends on whether `RestrictedAt` ever carries replay-recompute drift. **Action: document the gap in `deferred-work.md` and add a clock-skew test only if Epic 6 GDPR work pulls processing-restriction into scope.**

No risk falls below confidence-5, so no STOP-and-ASK is triggered. All confidence-6 risks proceed with the noted disambiguation.

### Highest-Priority Mitigations (Top 5 by Score)

1. **R-01 / P0 / SEC** — Cross-tenant data leakage: dedicated tenant-isolation test suite at every level — Tier-1 actor key derivation (detail + index), Tier-2 query adapter (`PartyDetailProjectionQueryActor`, `PartyIndexProjectionQueryActor`) construction-time and read-time guards (no `{otherTenant}:*` key construction/serialization/logging via proxy-factory or state-manager spy), Tier-3 Aspire two-tenant disjoint-result tests, Topology Fitness scanning for caller-supplied tenant override.
2. **R-02 / P0 / SEC** — PII/token/payload leakage in diagnostics: FakeLogger-based scrub assertions across `PartyDetailProjectionQueryActor`, `PartyIndexProjectionQueryActor`, projection actor corruption paths, rebuild service, search providers, degraded middleware. Fitness deny-list of forbidden log keys (`partyName`, `displayName`, `contactValue`, `identifier`, `Authorization`, `payload`, `state-key`, `stream-name`, `exception.Message`).
3. **R-03 / P1 / SEC** — Tenant identity from untrusted source: before-construction proof tests that fail if any projection actor, partition strategy, search provider, cache, state manager, or projection read adapter is invoked before the authenticated tenant gate succeeds; payload-tenant-like-field tests confirming caller cannot override authenticated tenant.
4. **R-04 / P1 / DATA** — Erased-party PII resurfaces: erased-state replay tests through the new query adapters; static cache + `EraseAsync` coordination tests (post-erase static-cache lookup must miss); rebuild-after-erasure tests proving no PII resurfaces.
5. **R-05 / P1 / DATA** — Stale/degraded cache mixed-provenance: positive-provenance proof tests for `_cachedDetail`, `_entries`, `s_lastKnownDetails`, `s_lastKnownEntries` — cached entry only returnable if tenant id, projection kind, party/partition key, erasure status, and cache-currency version match current actor context; otherwise bounded empty/unavailable.

### Risk Findings — Headline

Epic 2 is fundamentally **a tenant-safety and read-path privacy surface change**. 13 of 21 risks (62%) are SEC or DATA; both P0 risks are SEC and span the entire read chain (Client → EventStore gateway → Parties query adapter → projection actor → static cache). The mitigation discipline mirrors Epic 12: concentrate coverage at the **query-adapter actor boundary** (Tier-2) and the **static-cache + rebuild surface** (Tier-1 + actor-corruption tests), preserve the unchanged Tier-1 invariants (handler purity, rejection no-ops, idempotency), and use **Topology Fitness** for the never-add-X rules (REST/MCP/AdminPortal surfaces, log scrubs, contract additivity). Performance (R-13) and operability risks (R-14, R-16) are real but secondary — they need watch-lists and capture-only baselines, not blocking coverage.

The Epic-12 pivot already delivered the EventStore-fronted query gateway plumbing this epic depends on; Epic 2's residual surface is the **projection-side** of the same boundary: index actor partition state, detail actor key shape, rebuild service event replay, freshness/degradation metadata, and MVP search scope discipline.

---

## Step 04 — Coverage Strategy & Test Levels

### Test Level Glossary (this project)

| Level | Tooling | Scope | Project Home |
|-------|---------|-------|--------------|
| **Tier 1 (Unit)** | xUnit v3 + Shouldly + NSubstitute, infra-free | Pure projection handlers, pure handler invariants, contract additivity, validators, exception serializer | `Hexalith.Parties.Projections.Tests`, `*.Contracts.Tests`, `*.Server.Tests` |
| **Tier 2 (Gateway)** | xUnit + WebApplicationFactory, in-process EventStore-fronted host | EventStore gateway → Parties query-adapter actors → projection actor reads; tenant authorization mapping; degraded/cancellation behavior | `Hexalith.Parties.Tests/Gateway`, `*.Tests/Projections`, `*.Tests/Search`, `*.Tests/HealthChecks`, `*.Client.Tests` |
| **Tier 3 (Topology)** | xUnit + Aspire + Testcontainers + Dapr sidecars + real EventStore | End-to-end query through full topology; multi-tenant isolation; projection rebuild under realistic state-store; health/readiness wiring | `Hexalith.Parties.IntegrationTests` |
| **Topology Fitness** | xUnit + Roslyn/reflection + static manifest scan | REST/MCP/AdminPortal surface guards, package boundaries, log-deny-list scans, contract additivity, Dapr ACL YAMLs | `Hexalith.Parties.Tests/FitnessTests`, `*.Client.Tests/FitnessTests` |

### Coverage Matrix (Risk → Scenarios → Level → Priority)

Test IDs follow `2.{Story}-{LEVEL}-{SEQ}` where `LEVEL ∈ {UNIT, INT, COMP, GTW, TOP, FIT}`.

#### R-01 — Cross-tenant data leakage (P0)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.6-GTW-001 | `PartyDetailProjectionQueryActor` constructs detail actor id strictly from authenticated EventStore tenant context (NSubstitute proxy-factory spy fails if `{otherTenant}:party-detail:{partyId}` is ever constructed for lookup) | Tier-2 | P0 | extend `PartyDetailProjectionQueryActorTests` |
| 2.6-GTW-002 | `PartyIndexProjectionQueryActor` constructs index actor id strictly from authenticated tenant context (proxy-factory spy + spy assertions on rejected payload-tenant overrides) | Tier-2 | P0 | extend `PartyIndexProjectionQueryActorTests` |
| 2.6-GTW-003 | Tenant B probes tenant A party id via `PartyDetail` query → bounded not-found, no payload, no key, no degraded indicator, no diagnostics leak | Tier-2 | P0 | new |
| 2.6-GTW-004 | Tenant B lists with filters matching tenant A entries → returns only tenant B's rows; `TotalCount`/`TotalPages`/empty-page metadata calculated from tenant B authorized index only | Tier-2 | P0 | new |
| 2.6-GTW-005 | Tenant B searches for tenant A display name → returns only tenant B matches; match/score/source metadata excludes tenant A | Tier-2 | P0 | new |
| 2.6-UNIT-006 | `PartyDetailProjectionActor` malformed actor id (`{tenant}:wrong-projection:{partyId}`, `unknown`, empty, segment-count != 3, wrong projection segment) fails before state read, before state write, before checkpoint advancement | Tier-1 | P0 | extend `PartyDetailProjectionActorCorruptionTests` |
| 2.6-UNIT-007 | `PartyIndexProjectionActor` valid-but-wrong-tenant actor id cannot read/write rows, manifest, sequence keys, or `_entries` from another tenant | Tier-1 | P0 | extend `PartyIndexProjectionActorCorruptionTests` |
| 2.6-INT-008 | Two-tenant Aspire topology: tenant A and tenant B with overlapping display names + party ids → disjoint detail/list/search results; cross-tenant probes fail-closed | Tier-3 | P0 | new under `IntegrationTests/Tenants` |
| 2.6-FIT-009 | Architectural fitness asserts `PartyDetailProjectionQueryActor` and `PartyIndexProjectionQueryActor` derive tenant only via `EventStoreQueryContext.TenantId` (Roslyn scan rejects literal tenant strings, payload-tenant field reads, query-payload tenant accessors) | Topology Fitness | P0 | new |

#### R-02 — PII/token/payload leakage in diagnostics (P0)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.6-GTW-010 | FakeLogger captures `PartyDetailProjectionQueryActor` logs across success / not-found / corrupt / degraded / cancellation paths → assert no display name, contact value, identifier, raw query payload, serialized `PartyDetail` JSON, actor key, state key, stream name, or `Exception.Message` | Tier-2 | P0 | extend `PartyDetailProjectionQueryActorTests` (already partial per Story 2.3 P3 patch) |
| 2.6-GTW-011 | Same FakeLogger discipline for `PartyIndexProjectionQueryActor` — success / empty / filter-invalid / not-found / degraded / cancellation | Tier-2 | P0 | new |
| 2.6-UNIT-012 | `PartyDetailProjectionActor` / `PartyIndexProjectionActor` corruption-path diagnostics: malformed-id, malformed-JSON, redacted-payload, rebuild-scheduling → FakeLogger asserts metadata-only | Tier-1 | P0 | extend `PartyDetail/IndexProjectionActorCorruptionTests` |
| 2.6-UNIT-013 | `ProjectionRebuildService` logs across happy path, redacted-payload-skip, unknown-event-skip, write-failure, checkpoint-delete-failure, cancellation → no party names, no raw payloads, no `Exception.Message` | Tier-1 | P0 | extend `ProjectionRebuildServiceTests` |
| 2.6-FIT-014 | Roslyn fitness scans `src/Hexalith.Parties` / `src/Hexalith.Parties.Projections` / `src/Hexalith.Parties.Client` for forbidden log message patterns: deny-list of property names (`DisplayName`, `ContactValue`, `Identifier`, `Authorization`, `payload`, `stateKey`, `streamName`) and direct `Exception.Message` / `Exception.ToString()` interpolation in `LogXxx` calls | Topology Fitness | P0 | new |
| 2.6-FIT-015 | ProblemDetails fitness asserts `Detail` / `Extensions` of `PartiesClientException` payload never includes PII-bearing fields from `PartyDetail` / `PartyIndexEntry` / `NameHistoryEntry` (reflection-based `[PersonalData]` inventory cross-check) | Topology Fitness | P0 | new |

#### R-03 — Tenant identity from untrusted source (P1)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.6-GTW-020 | Payload-tenant-like field on detail query (e.g. `payload.tenantId`, `payload.actorId`, `payload.partitionKey`) ignored — adapter uses authenticated tenant only | Tier-2 | P1 | new |
| 2.6-GTW-021 | Same for list query (`PartyIndex`) and search query (`PartySearch`): cursor/`caseId`/`mode`/`GraphContextPartyId`/`GraphContextMemoryUnitId` cannot override tenant or choose actor | Tier-2 | P1 | new |
| 2.6-GTW-022 | Before-construction proof: no `IPartyDetailProjectionActor`, `IPartyIndexProjectionActor`, `IIndexPartitionStrategy`, `IPartySearchProvider`, cache, or projection read adapter is invoked before EventStore tenant gate succeeds (NSubstitute spy assertion on auth-failure paths) | Tier-2 | P1 | new |
| 2.6-FIT-023 | Architectural fitness denies access to `EventStoreQueryContext` properties other than `TenantId` / `CorrelationId` / `Authorization` from query adapters; payload tenant property reads are banned | Topology Fitness | P1 | new |

#### R-04 — Erased-party PII resurfaces (P1)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.6-UNIT-030 | `PartyDetailProjectionHandler.Apply(PartyErased, …)` returns redacted/cleared state per existing erasure semantics — display name, sort name, person/org details, contact values, identifiers, name history are empty/cleared | Tier-1 | P1 | extend `PartyDetailProjectionHandlerTests` |
| 2.6-UNIT-031 | `PartyIndexProjectionHandler.Apply(PartyErased, …)` marks `IsErased=true` and strips `DisplayName`/`SortName` to empty in projection state | Tier-1 | P1 | extend `PartyIndexProjectionHandlerTests` |
| 2.6-GTW-032 | Post-erase detail query through `PartyDetailProjectionQueryActor` returns bounded `Success=true` with `IsErased=true`+`ErasedAt` shape (deferred per 2.3 D2) but payload contains no PII; FakeLogger contains no PII | Tier-2 | P1 | extend per Story 2.3 P2 patch |
| 2.6-INT-033 | Post-erase list query excludes erased entries from `Items`, `TotalCount`, `TotalPages`, and page metadata (Tier-3 Aspire to exercise real state store) | Tier-3 | P1 | new under `IntegrationTests/Security` |
| 2.6-INT-034 | Post-erase search query (display-name match) excludes erased entries from `PagedResult<PartySearchResult>` `Items` and match metadata | Tier-3 | P1 | new |
| 2.6-UNIT-035 | Static `s_lastKnownDetails` / `s_lastKnownEntries` are evicted on `EraseAsync(partyId)` for matching key context — assert post-erase lookup misses cache and returns bounded empty/unavailable | Tier-1 | P1 | new (closes Story 2.3 review defer "Erase+rebuild race") |
| 2.6-INT-036 | Erase-then-rebuild race: rebuild reminder firing after `EraseAsync` does not resurrect cleared PII into `s_lastKnownDetails`/`s_lastKnownEntries` | Tier-3 | P1 | new |

#### R-05 — Stale/degraded cache mixed-provenance (P1)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.7-UNIT-040 | `PartyDetailProjectionActor` static cache returns entry only when tenant id + projection kind + party id + erasure status + cache-currency version match current actor context; otherwise bounded null | Tier-1 | P1 | extend `PartyDetailProjectionActorCorruptionTests` |
| 2.7-UNIT-041 | `PartyIndexProjectionActor` static cache returns entries only when tenant id + projection kind + partition key + erasure filter + cache-currency version match; otherwise bounded empty | Tier-1 | P1 | extend `PartyIndexProjectionActorCorruptionTests` |
| 2.7-UNIT-042 | Mixed-provenance test: two tenants exercise same static dictionary; tenant B activation cannot return tenant A cached entries via shared static state | Tier-1 | P1 | new |
| 2.7-GTW-043 | Degraded list/search response: cached entries returned only with bounded `local-only`/`stale`/`rebuilding` signal AND proven same-tenant provenance; if any provenance check fails → bounded unavailable | Tier-2 | P1 | extend `PartySearchServiceBoundaryTests` |

#### R-06 — Event-contract additive regression (`required SortName` redux) (P1)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.5-UNIT-050 | Deserialize a historical `PartyDisplayNameDerived` JSON payload (no `sortName` field) → succeeds; `SortName` falls back to empty string | Tier-1 | P1 | new in `Contracts.Tests` |
| 2.5-FIT-051 | Contracts fitness reflects over all `Hexalith.Parties.Contracts.Events` types and rejects any property declared `required` that wasn't part of the original event contract (allowlist + diff check) | Topology Fitness | P1 | new in `ContractsArchitectureFitnessTests` |
| 2.5-UNIT-052 | Projection handler tolerates empty `SortName` by falling back to `DisplayName` in sort tie-breaker (`GetSortableName` parity across `BasicPartySearchProvider`, `LocalFuzzyPartySearchProvider`, `SemanticPartySearchProvider`) | Tier-1 | P1 | extend `BasicPartySearchProviderTests`, `LocalFuzzyPartySearchProviderTests`, `SemanticPartySearchProviderTests` |

#### R-07 — Rejection-event Apply ordering (P1)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.1-UNIT-060 | `PartyStateApplyOrderingFitnessTests` — assert all `IRejectionEvent` `Apply` overloads precede success-event `Apply` overloads via source-order reflection (PRESERVE existing) | Tier-1 | P1 | PRESERVE existing |
| 2.1-UNIT-061 | `PartyStateRejectionApplyEndToEndTests` — replay history including rejection events → state matches success-only history (PRESERVE existing) | Tier-1 | P1 | PRESERVE existing |
| 2.1-UNIT-062 | `PartyDetailProjectionHandlerRejectionFitnessTests` — reflection over all `IRejectionEvent` types asserts `Apply` returns null for both null and populated state (PRESERVE existing, extend to `PartyIndexProjectionHandler` if not already covered) | Tier-1 | P1 | PRESERVE / extend |

#### R-08 — MVP search scope creep (P1)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.5-UNIT-070 | `LocalFuzzyPartySearchProvider` (or its MVP wrapper) in MVP mode emits `MatchedField=="displayName"` ONLY for exact/prefix/contains/fuzzy display-name matches | Tier-1 | P1 | extend `LocalFuzzyPartySearchProviderTests` per Story 2.2 P4 patch |
| 2.5-UNIT-071 | Negative tests: payload seeded with `info@acme.com` contact and `FR11111111111` identifier produces zero MVP matches (or zero `displayName` matches); fitness check that no `MatchMetadata` row in the result has `MatchedField IN {email, identifier, contactChannel, semantic, graph, duplicate, type}` | Tier-1 | P1 | extend |
| 2.5-FIT-072 | Architectural fitness denies `SemanticPartySearchProvider`, `MemoriesPartySearchService`, `BasicPartySearchProvider.SearchContactsAndIdentifiers(...)` (or equivalent broader methods) from being reachable via the MVP `PartySearch` query path | Topology Fitness | P1 | new |
| 2.5-FIT-073 | Contract fitness asserts the only `MatchType` values constructible from `PartySearchResultsBuilder` MVP path are `{exact, prefix, contains, fuzzy}` and the only `MatchedField` value is `displayName` | Topology Fitness | P1 | new |
| 2.5-GTW-074 | `HttpPartiesQueryClient.SearchPartiesAsync` payload pins `QueryType="PartySearch"`, omits future-mode fields, and round-trips bounded match metadata only | Tier-2 | P1 | extend `HttpPartiesQueryClientTests` |

#### R-09 — List/filter/search metadata leaks cross-tenant counts (P1)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.4-GTW-080 | List query: `TotalCount`, `TotalPages`, empty-page behavior calculated from tenant-authorized + erasure-filtered + filter-chain-applied collection (not pre-filter set, not cross-tenant total) | Tier-2 | P1 | extend `PartyIndexProjectionQueryActorTests` |
| 2.4-GTW-081 | Filter combination test: `type=Person ∧ active=true ∧ created∈[a,b] ∧ modified∈[c,d]` → metadata reflects intersection, not unfiltered count | Tier-2 | P1 | new |
| 2.5-GTW-082 | Search query: `TotalCount`, score metadata, source metadata calculated after authorization + erasure filter + display-name match (not from broader provider result set) | Tier-2 | P1 | extend `PartySearchServiceBoundaryTests` |
| 2.6-INT-083 | Tier-3: two tenants with 1000+ entries each + overlapping display names; tenant B list/search counts match tenant B authorized entries exactly | Tier-3 | P1 | new |

#### R-10 — Freshness metadata leaks cross-tenant existence (P1)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.7-GTW-090 | `X-Service-Degraded` / `X-Stale-Data-Age` headers on safe GET reads contain only coarse bucketed values (e.g. `<1m`, `1-5m`, `5-30m`, `>30m`) — never exact sequence positions, actor keys, or tenant ids | Tier-2 | P1 | extend `DegradedResponseMiddlewareTests` |
| 2.7-GTW-091 | Cross-tenant freshness probe: tenant B probes tenant A party id while tenant A projection is stale → tenant B response contains only tenant B's freshness status (default `current` or `unavailable`), no tenant A degraded marker leaks | Tier-2 | P1 | new |
| 2.7-UNIT-092 | `IsRebuildingAsync` response on `PartyDetailProjectionActor` / `PartyIndexProjectionActor` returns bounded boolean only — no rebuild progress percentage, stream position, or party count leaks through the public actor interface | Tier-1 | P1 | extend corruption tests |
| 2.7-FIT-093 | Roslyn fitness denies `PagedResult<T>`-bearing or `PartyDetail`-bearing public properties of type `long`/`int` with names containing `Sequence`, `Position`, `Offset`, `StreamName`, `PartitionKey`, `CacheAge` (forbidden freshness-disclosure additions) | Topology Fitness | P1 | new |

#### R-11 — Display-name normalization drift (P2)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.2-UNIT-100 | `PartyAggregate.DeriveDisplayName` and `PartyIndexProjectionHandler.DeriveSortName` produce identical output for identical inputs across whitespace, empty parts, diacritic variants (PRESERVE Story 2.2 P1 patch) | Tier-1 | P2 | PRESERVE existing |
| 2.2-UNIT-101 | Replay test: `PartyCreated → PartyDisplayNameDerived` sequence ends with `state.SortName == e.SortName`; second replay no-ops `LastModifiedAt` | Tier-1 | P2 | extend `PartyIndexProjectionHandlerTests` |

#### R-12 — REST/MCP/AdminPortal surface reintroduction (P2)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.6-FIT-110 | `ArchitecturalFitnessTests` denies `[ApiController]`, `ControllerBase`, `MapControllers`, `AddMcpServer`, `WithToolsFromAssembly`, `MapMcp`, `[McpServerTool]` references in `src/Hexalith.Parties` (PRESERVE existing, extend with `MapGet("/api/v1/parties*")` Roslyn scan) | Topology Fitness | P2 | PRESERVE / extend |
| 2.6-FIT-111 | Fitness denies `src/Hexalith.Parties.AdminPortal` direct references to `Hexalith.Parties.Projections.*` actors, `Hexalith.Parties.*` state stores, or `Hexalith.Parties.Server.*` aggregates (must go through `IPartiesQueryClient`) | Topology Fitness | P2 | new |
| 2.6-FIT-112 | Same fitness for `src/Hexalith.Parties.Picker` and `src/Hexalith.Parties.Mcp` (must consume `IPartiesQueryClient` only) | Topology Fitness | P2 | extend per Epic 12 baseline |

#### R-13 — Projection rebuild latency baseline (P2, capture-only)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.8-INT-120 | Tier-3 rebuild-while-querying baseline: trigger detail-projection rebuild for 100 parties; concurrent query throughput captured as artifact (p50, p95, p99 latency, error rate); no numeric threshold in story (capture-only per confidence-gate lift) | Tier-3 | P2 | new — Nightly only |
| 2.8-INT-121 | Index-projection rebuild for 10k entries / single partition; capture rebuild wall-time + concurrent-query degraded-response rate; no numeric threshold (capture-only) | Tier-3 | P2 | new — Nightly only |

#### R-14 — Failed/canceled rebuild stuck degraded (P2)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.8-UNIT-130 | `ProjectionRebuildService.RebuildDetailProjectionAsync` write failure leaves actor in degraded state; subsequent `IsRebuildingAsync` reports true; query path returns bounded unavailable | Tier-1 | P2 | extend `ProjectionRebuildServiceTests` |
| 2.8-UNIT-131 | Mid-flight cancellation during rebuild → no further actor writes, no checkpoint advancement, no recovery work scheduled after CT observed | Tier-1 | P2 | extend |
| 2.8-UNIT-132 | Checkpoint-delete failure after successful rebuild → actor marked degraded, not current; explicit retry path documented in metadata | Tier-1 | P2 | new |
| 2.8-INT-133 | Tier-3 chaos: kill rebuild mid-flight via sidecar restart; rebuild reminder reschedules; eventually consistent state (capture-only, not blocking) | Tier-3 | P2 | new — Weekly only |

#### R-15 — Cancellation not terminal (P2)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.3-GTW-140 | Pre-cancel CT before `PartyDetailProjectionQueryActor.QueryAsync` invocation → adapter short-circuits with `OperationCanceledException` before `actorProxyFactory.CreateActorProxy` is called (per Story 2.3 P5 patch) | Tier-2 | P2 | extend `PartyDetailProjectionQueryActorTests` |
| 2.4-GTW-141 | Same for `PartyIndexProjectionQueryActor` | Tier-2 | P2 | extend `PartyIndexProjectionQueryActorTests` |
| 2.5-GTW-142 | `HttpPartiesQueryClient.SearchPartiesAsync` cancellation propagates terminally — no fallback aggregate replay, detail fan-out, Memories expansion, retried calls | Tier-2 | P2 | extend `HttpPartiesQueryClientTests` |
| 2.6-FIT-143 | Roslyn scan asserts every `async Task<...> QueryAsync(QueryEnvelope ...)` in `src/Hexalith.Parties/Queries/*` has either explicit `cancellationToken.ThrowIfCancellationRequested()` at entry OR exclusively awaits CT-propagating Dapr `IActorProxyFactory`/`IActorProxy` calls (deferred work tracker if pattern doesn't hold) | Topology Fitness | P2 | new |

#### R-16 — Active filter default (P2)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.4-GTW-150 | `ListPartiesAsync(active: null)` returns the pinned current behavior (all-status OR active-only — whichever the implementation chose); test explicitly names the choice so future stories can't flip it silently | Tier-2 | P2 | extend `PartyIndexProjectionQueryActorTests` |
| 2.4-GTW-151 | `active=true` → only active entries; `active=false` → only inactive entries; metadata aligns to the filtered set | Tier-2 | P2 | extend |

#### R-17 — Date filter / paging overflow (P2)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.4-GTW-160 | `pageSize=int.MaxValue` clamped to 100; `page=int.MaxValue` overflow-safe skip (long arithmetic) → bounded empty page or accepted bounds-check rejection | Tier-2 | P2 | extend `PartyIndexProjectionQueryActorTests` |
| 2.4-GTW-161 | `createdAfter > createdBefore` → bounded validation error with metadata-only diagnostics | Tier-2 | P2 | extend |
| 2.4-UNIT-162 | `PartySearchResultsBuilder.BuildPagedList` uses UTC instant comparisons; assert local-time/UTC mixed inputs all yield deterministic UTC-instant filtered results | Tier-1 | P2 | extend |

#### R-18 — Temporal/semantic surfaces exposed in MVP (P2)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.9-UNIT-170 | `PartySearchMode.Semantic` / `Graph` / `Hybrid` requests through MVP `PartySearch` path return bounded unsupported-capability outcome (not empty success, not silent fallback to display-name search) | Tier-1 | P2 | new |
| 2.9-FIT-171 | Contract fitness asserts `TemporalNameResult` and `PartyDetail.NameHistory` are not reachable via any public endpoint, MCP tool, or `IPartiesQueryClient` method | Topology Fitness | P2 | new |
| 2.9-UNIT-172 | `NameHistoryEntry.DisplayName` / `SortName` carry `[PersonalData]` attributes; `PersonalDataInventoryTests` includes them in cryptographic-shred candidate inventory | Tier-1 | P2 | extend `PersonalDataInventoryTests` |
| 2.9-UNIT-173 | Post-erase: `PartyDetail.NameHistory` is cleared; replay through `PartyDetailProjectionHandler` cannot reconstruct historical names | Tier-1 | P2 | extend `PartyDetailProjectionHandlerNameHistoryTests` |

#### R-19 — Health endpoints as alternate auth surface (P3, confidence-lift gate)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.8-GTW-180 | `/health`, `/alive`, `/ready` response payloads contain only bounded category strings — assert no tenant ids, projection counts, partition keys, sequence positions, raw exception text, party names, contact values | Tier-2 | P3 | extend `HealthEndpointIntegrationTests` |
| 2.8-UNIT-181 | `ProjectionActorsHealthCheck.CheckHealthAsync` returns `Description` containing only bounded category text; no actor route, no tenant probe artifact, no raw `ActorMethodInvocationException` text | Tier-1 | P3 | extend `ProjectionActorsHealthCheckTests` |

#### R-20 — `HandleProcessingRestricted` clock-skew (P3, documented)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.x-DOC-190 | Document in `deferred-work.md` with cross-link to Story 2.1 review; add test only if Epic 6 GDPR pulls processing-restriction into scope | (deferred) | P3 | document-only |

#### R-21 — Pagination tie-break non-determinism (P3)

| Test ID | Scenario | Level | Priority | Existing? |
|---------|----------|-------|----------|-----------|
| 2.5-UNIT-200 | Add `Id` (party id) as final tie-breaker in all three search providers (`BasicPartySearchProvider`, `LocalFuzzyPartySearchProvider`, `SemanticPartySearchProvider`) and `PartySearchResultsBuilder`; assert two parties with identical score + SortName + DisplayName have deterministic order across multiple sort invocations | Tier-1 | P3 | new (closes Story 2.2 review defer) |

### Anti-Duplication Discipline

- **Handler purity tests** (Tier-1) cover all `Apply(IEventPayload, State)` invariants ONCE per handler. Tier-2 gateway tests do NOT replicate per-event-type handler branches; they exercise representative happy / sad paths through the adapter actor.
- **Tenant authorization** is tested at Tier-2 boundary (gateway routing + query adapter) and Tier-3 topology (two-tenant Aspire disjoint-result). It is NOT replicated at Tier-1 — the adapter actor's NSubstitute proxy-factory spy IS the tenant-isolation contract proof.
- **Privacy / log scrubs** are tested at Tier-2 (FakeLogger assertions on adapter actor + corruption paths) and codified at Topology Fitness (Roslyn deny-list). Tier-1 handler tests do NOT log, so no scrub assertions belong there.
- **Cancellation** is tested at Tier-2 (CT pre-cancel + framework propagation) and codified at Topology Fitness (CT-flow scan). Tier-3 chaos cancellation is capture-only.
- **MVP search scope** is tested at Tier-1 (provider unit tests with future-field negative assertions), Tier-2 (HttpPartiesQueryClient payload pins), and Topology Fitness (provider-reachability + `MatchType`/`MatchedField` allowlist). No Tier-3 coverage needed; the contract is purely shape-of-result.

### Execution Strategy

| Pipeline | Scope | Target Wall Time |
|----------|-------|-------------------|
| **PR** | Tier-1 (all) + Tier-2 (all) + Topology Fitness (all) | ≤ 6 minutes |
| **Nightly** | PR scope + full Tier-3 Aspire topology suite (two-tenant isolation, rebuild baselines 2.8-INT-120/121, erase-rebuild race 2.6-INT-036) | ≤ 25 minutes |
| **Weekly** | Nightly scope + chaos rebuild kill (2.8-INT-133), large-tenant search benchmarks (existing `*PerformanceBenchmarkTests`) | ≤ 60 minutes |

Rationale: Tier-3 Aspire topology spin-up is the slowest step (~10-30 s per test); keeping it off PR keeps developer feedback fast while still gating Nightly before merges to `main`. Topology fitness tests run in PR because they are fast Roslyn/reflection-only and catch the highest-leverage drift early.

### Resource Estimates (Net New + Extensions)

Existing test surface absorbs much of the work; numbers below estimate net-new effort post-Epic-2.

| Priority | Net-New Scenarios | Estimate (range) |
|----------|-------------------|-------------------|
| **P0** (R-01, R-02) | 15 scenarios across Tier-1, Tier-2, Tier-3, Topology Fitness | ~35–55 hours |
| **P1** (R-03 through R-10) | ~30 scenarios across all levels | ~50–75 hours |
| **P2** (R-11 through R-18) | ~20 scenarios + 2 capture-only baselines | ~20–35 hours |
| **P3** (R-19, R-20, R-21) | 4 scenarios + 1 documented defer | ~4–8 hours |
| **Total** | ~69 net-new test scenarios + 1 deferred document | **~110–175 hours** |

Note: estimates assume the post-Epic-12 test infrastructure (Tier-2 WebApplicationFactory, Tier-3 Aspire fixtures, FakeLogger pattern, projection actor harnesses) is in place. If any FakeLogger plumbing is partial in projection actor / rebuild service / health check projects, add ~10–15 hours for harness work.

### Quality Gates

| Gate | Threshold | Enforcement |
|------|-----------|-------------|
| P0 pass rate (R-01, R-02 — tenant isolation + diagnostics privacy) | 100% — release blocked on any failure | PR pipeline + Nightly |
| P1 pass rate | ≥ 95% — investigate any flake immediately | PR pipeline + Nightly |
| P2 pass rate | ≥ 90% | Nightly |
| P3 pass rate | best effort | Weekly |
| Tier-3 topology spin-up wall time | ≤ 35 s per test, p95 ≤ 60 s | Nightly artifact |
| Rebuild latency baseline (R-13) | Capture-only on first cycle; set threshold after 5 nightly runs | Nightly artifact |
| Coverage of Epic 2 AC | 100% AC → test mapping (Step 5 traceability matrix) | One-time + per-story retrospective |

### Confidence Gate Re-Check (from Step 3)

- **R-13 PERF rebuild latency (confidence 5 → 7)** — Resolved by treating it as **capture-only baseline** (2.8-INT-120/121) rather than a hard threshold. PRD MCP target and FR19 consistency target verified post-baseline.
- **R-19 OPS health endpoints as alternate auth (confidence 5 → 7)** — Resolved by explicit health-output bounded-vocabulary tests (2.8-GTW-180, 2.8-UNIT-181) + Topology Fitness deny-list (R-10's 2.7-FIT-093 covers freshness-disclosure additions to public payloads).
- **R-16 BUS active filter default (confidence 6)** — Resolved by 2.4-GTW-150 pinning current behavior so future stories cannot silently flip it.

All risks now meet the proceed threshold (confidence ≥ 7 effective).

---

## Step 05 — Final Output & Validation

**Final document**: [`_bmad-output/test-artifacts/test-design/test-design-epic-2.md`](test-design/test-design-epic-2.md)

**Execution mode resolution**: `tea_execution_mode=auto` → resolved to `sequential` (single agent producing one Epic-Level document; no subagent / agent-team needed for one-output workflow).

### Checklist Validation (Epic-Level Mode)

#### Risk Assessment Matrix
- [x] All risks have unique IDs (R-01 through R-21)
- [x] Each risk has category assigned (TECH / SEC / PERF / DATA / BUS / OPS)
- [x] Probability values are 1, 2, or 3
- [x] Impact values are 1, 2, or 3
- [x] Scores calculated correctly (P × I)
- [x] High-priority risks (≥ 6) clearly marked
- [x] Mitigation strategies specific and actionable
- [x] Owners assigned (Backend Dev / DevOps / QA / PM)
- [x] Timeline framed against Epic 2 story sequence (2.5/2.6/2.7/2.8 ready-for-dev)

#### Coverage Matrix
- [x] All requirements mapped to test levels (Tier-1 / Tier-2 / Tier-3 / Topology Fitness)
- [x] Priorities assigned to all scenarios
- [x] Risk linkage documented (each test ID maps to one R-ID)
- [x] Test counts realistic (~74 net-new scenarios)
- [x] Owners assigned per row
- [x] Anti-duplication discipline documented (handler purity once at Tier-1; gateway tests only assert envelope/event-count/diagnostic-scrub)

#### Execution Strategy
- [x] Simple PR / Nightly / Weekly structure (not complex P-tier execution mix)
- [x] PR target ≤ 6 min; Nightly ≤ 25 min; Weekly ≤ 60 min
- [x] No redundancy: doesn't re-list tests from coverage plan
- [x] Philosophy stated ("Run everything in PRs unless infrastructure overhead makes it expensive")

#### Resource Estimates
- [x] P0 effort: ~35–55 hours (range, no false precision)
- [x] P1 effort: ~50–75 hours
- [x] P2 effort: ~20–35 hours
- [x] P3 effort: ~4–8 hours
- [x] Total: ~110–175 hours / ~3–5 weeks (range)
- [x] Assumptions about harness completeness called out explicitly

#### Quality Gate Criteria
- [x] P0 pass rate 100%
- [x] P1 pass rate ≥ 95%
- [x] P2 pass rate ≥ 90%
- [x] High-risk mitigations (≥ 6) completion required
- [x] Coverage gate: 100% of Epic 2 AC mapped + cross-tenant scenarios at 100% + erased-party scenarios at 100% + MVP search scope at 100%

#### Priority Section Discipline (Anti-Bloat per Checklist)
- [x] P0/P1/P2/P3 sections contain only **Criteria** (no execution-timing context in priority headers)
- [x] Note at top of Test Coverage Plan clarifies P-labels = priority, not execution timing
- [x] Execution Strategy section is separate and handles timing

#### Evidence-Based Assessment
- [x] Every risk's mitigation cites specific Epic 2 AC or existing project rule
- [x] Confidence-gate applied to R-13 and R-19 (lower-confidence risks) with explicit lift rationale
- [x] No speculation on business impact beyond what AC / project-context.md state

#### Test Level Selection
- [x] Tier-3 (E2E topology) used for cross-service / cross-tenant / erase-rebuild race / rebuild-latency baseline only
- [x] Tier-2 (gateway) used for boundary contracts (query adapter routing, diagnostic scrubs, cancellation, untrusted-tenant-source)
- [x] Tier-1 (unit) used for invariants (handler purity, rejection no-ops, normalization parity, cache provenance, contracts additivity)
- [x] Topology Fitness used for static / source / manifest checks (surface guards, log deny-lists, freshness disclosure)
- [x] Component (bUnit) excluded — out-of-scope for Epic 2 (Epic 7/8 owns it)
- [x] No duplicate coverage across levels (anti-duplication discipline section)

#### Out-of-Scope Items
- [x] EventStore submodule, Tenants submodule, Memories submodule, FrontComposer, AdminPortal UI, Picker UI, MCP tool orchestration, GDPR public response shape explicitly excluded with reasoning
- [x] Multi-key partitioning, semantic/graph/email/contact/identifier search, public typed freshness contract, browser E2E, production load testing all excluded with cross-reference to owning epic

#### Entry/Exit Criteria
- [x] Entry criteria reference Story 2.4 review-patch landing, FakeLogger pattern, two-tenant Aspire fixture extension, `deferred-work.md` carry-forward
- [x] Exit criteria reference R-01/R-02 P0 coverage, P1 pass rate, Topology Fitness 100%, rebuild-latency baseline capture, health-output bounded-vocabulary tests, Epic-12 config carry-forward (`tea_use_playwright_utils=false`, etc.)

#### Document Quality
- [x] Professional tone, no AI slop markers
- [x] No repeated notes — single source of truth for each fact
- [x] Tables aligned, headers consistent
- [x] Cross-references to source docs (story files, architecture sections, project-context.md) not duplications

### Items Skipped (Justified)

- **Component test code examples in `playwright-utils` form**: checklist mentions `@seontechnologies/playwright-utils/api-request/fixtures` imports. Skipped because this is a .NET stack — irrelevant. Examples in document reference idiomatic xUnit v3 + FakeLogger + NSubstitute patterns instead.
- **System-Level handoff document**: not generated (this is Epic-Level mode per Step 1).
- **`{p0_count} tests × 2.0 hours = ...` per-test estimates**: skipped per checklist instruction to avoid false precision; used ranges only.
- **CLI session cleanup**: no Playwright CLI sessions opened during workflow (no browser exploration — .NET stack).
- **Component (bUnit) coverage**: out-of-scope for Epic 2; owned by Epic 7/8.

### Final Outputs

1. **Standalone test design** → `_bmad-output/test-artifacts/test-design/test-design-epic-2.md`
2. **Working notes / progress** → `_bmad-output/test-artifacts/test-design-progress.md` (this file)

### Completion Report

**Mode used:** Epic-Level Mode (Phase 4) → sequential execution (single document)

**Output file paths:**
- Final test design: `_bmad-output/test-artifacts/test-design/test-design-epic-2.md`
- Progress notes: `_bmad-output/test-artifacts/test-design-progress.md`

**Key risks and gate thresholds:**
- **P0 — BLOCK** (2 risks, score 9): R-01 cross-tenant data leakage, R-02 diagnostics PII leakage → 100% pass-rate gate
- **P1 — MITIGATE** (8 risks, score 6–8): R-03 untrusted tenant source, R-04 erased PII resurfacing, R-05 mixed-provenance cache, R-06 contract additivity regression, R-07 rejection Apply ordering, R-08 MVP search scope creep, R-09 metadata cross-tenant leakage, R-10 freshness disclosure → ≥ 95% pass-rate gate
- **P2 — MONITOR** (8 risks, score 4): includes capture-only rebuild latency baseline (R-13) and chaos rebuild kill (R-14, Weekly only)
- **P3 — DOCUMENT** (3 risks, score 1–3): includes one fully deferred item (R-20) and one cross-provider determinism polish (R-21)

**Open assumptions:**
- Two-tenant Aspire fixture extension is straightforward (`Hexalith.Tenants` likely needs additional seed data — flagged as a 1–2-day risk-to-plan)
- FakeLogger pattern extends to projection actors that use Dapr `ActorHost.CreateForTest` (fallback: NSubstitute `ILogger<T>` with `Received()` filter)
- Story 2.4 review patches land before extending its tests (currently `review`)

### Recommended Next Workflows

- `bmad-testarch-atdd` — draft failing P0 tests for R-01 + R-02 (start with `2.6-GTW-001/002/010/011`)
- `bmad-testarch-automate` — generate P1 implementation skeletons once Stories 2.5/2.6/2.7 land harness changes
- `bmad-testarch-trace` — formal quality-gate decision after P0/P1 green
- `bmad-testarch-nfr` — formalize rebuild-latency SLO (R-13) after 5 Nightly baselines

### Open Items Surfaced for User Action

1. **Story 2.4 patches:** Confirm `review` → `done` transition before extending its tests (currently in flight per 2026-05-19 sprint-status note).
2. **`_bmad/tea/config.yaml`:** Set `tea_use_playwright_utils=false`, `tea_use_pactjs_utils=false`, clear `tea_pact_mcp` (carry-forward from Epic 12 progress).
3. **`sprint-status.yaml`:** Add Epic 12 to the `development_status` rollup if not already present (carry-forward from Epic 12 progress).
4. **`deferred-work.md`:** Add R-20 (`HandleProcessingRestricted` clock-skew) cross-link if not already tracked.
5. **Two-tenant Aspire fixture:** Plan / scope extension of `PartiesAspireTopologyFixture` for Tier-3 cross-tenant scenarios (R-01 / R-04 / R-09).
6. **Epic 6 ownership confirmation:** Confirm with PM that public erased-party response shape redesign remains Epic 6 (current Story 2.3 D2 deferral preserved).

---




