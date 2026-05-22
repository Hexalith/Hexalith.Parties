---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-05-19'
mode: 'epic-level'
scope: 'Epic 2 — Tenant-Safe Party Search and Retrieval'
---

# Test Design: Epic 2 — Tenant-Safe Party Search and Retrieval

**Date:** 2026-05-19
**Author:** Jérôme (Master Test Architect — Murat persona)
**Status:** Draft

---

## Executive Summary

**Scope:** Full Epic-Level test design for Epic 2 (9 stories: 2.1–2.9), covering tenant-safe party detail/index/list/search/freshness/rebuild read paths through the EventStore-fronted query gateway, plus deferred-capability guardrails.

**Risk Summary:**

- **Total risks identified:** 21
- **High-priority risks (score ≥ 6):** 10 — 2 BLOCK (P0) + 8 MITIGATE (P1)
- **Critical categories:** SEC (cross-tenant leakage, diagnostics PII) and DATA (erased-PII resurfacing, mixed-provenance cache, metadata leakage, contract additivity)

**Coverage Summary:**

- **P0 scenarios:** 15 (R-01 cross-tenant + R-02 diagnostics) — ~35–55 hours
- **P1 scenarios:** ~30 (R-03 through R-10) — ~50–75 hours
- **P2 scenarios:** ~20 + 2 capture-only baselines (R-11 through R-18) — ~20–35 hours
- **P3 scenarios:** 4 + 1 documented defer (R-19, R-20, R-21) — ~4–8 hours
- **Total effort:** ~110–175 hours (~3–5 weeks at 1 FTE)

**Headline:** Epic 2 is fundamentally a tenant-safety and read-path privacy surface change. 62% of risks are SEC or DATA; both P0 risks span the entire read chain (Client → EventStore gateway → Parties query adapter → projection actor → static cache). Mitigation concentrates Tier-2 coverage at the **query-adapter actor boundary** (`PartyDetailProjectionQueryActor`, `PartyIndexProjectionQueryActor`) and Tier-1 coverage at the **static-cache + rebuild surface**, while Topology Fitness codifies the never-add-X rules (REST/MCP/AdminPortal surfaces, log scrubs, contract additivity, freshness disclosure).

---

## Not in Scope

| Item | Reasoning | Mitigation |
|------|-----------|------------|
| **EventStore submodule internals** (query gateway authorization, command routing, idempotency, snapshot, event envelope) | Owned upstream by `Hexalith.EventStore`. project-context.md rule: "Before changing EventStore-owned behavior, check whether the correct fix belongs in the EventStore submodule rather than Parties." | EventStore submodule has its own test suite; Parties tests assume the gateway delivers authenticated tenant context. |
| **Tenants submodule internals** (membership lookup, RBAC validators, tenant lifecycle) | Owned upstream by `Hexalith.Tenants`. Parties consumes the authenticated tenant from the EventStore query envelope. | Tenants submodule has its own test suite; Parties Tier-3 tests use the deployed Tenants service via Aspire topology. |
| **AdminPortal UI rendering, accessibility, localization** | Covered by Epic 7 (Administration Console). Stories 2.4–2.6 explicitly defer AdminPortal UI redesign. | Epic 7 owns bUnit component tests for `Hexalith.Parties.AdminPortal`. |
| **Embeddable Party Picker UI** | Covered by Epic 8 (Embeddable Party Picker). | Epic 8 owns bUnit component tests for `Hexalith.Parties.Picker`. |
| **MCP `find_parties` / `get_party` tool orchestration** | Covered by Epic 4 (AI Agent Party Management). Story 2.5 explicitly defers MCP-specific AI disambiguation. | Epic 4 owns MCP-thin-host tests in `Hexalith.Parties.Mcp.Tests`. |
| **GDPR erasure, encryption, consent, restriction, portability semantics** | Covered by Epic 6 (GDPR Compliance Operations). Epic 2 preserves existing erasure behavior without redesigning it. | Epic 6 owns the public erased-party response shape and Erase+rebuild race tests beyond the static-cache eviction test included here (R-04). |
| **Multi-key partitioning beyond `SingleKeyPartitionStrategy`** | Deferred per all Story 2.x decisions. V1.0 is single-key only. | Story 2.x architecture pins `SingleKeyPartitionStrategy`; future partition stories will add multi-key rebuild orchestration. |
| **Semantic / graph / contact / identifier / email search** | Story 2.5 reserves these as future-only; Story 2.9 keeps them as deferred. | Story 2.9 preparation: `PartySearchMode.Semantic`/`Graph` enum values must be unreachable from MVP paths (R-18). |
| **Public typed freshness contract** | Story 2.7 explicitly defers final typed freshness model; only minimal additive degraded headers are in scope. | R-10 tests pin bounded vocabulary; final typed contract is a later cross-epic decision. |
| **Browser E2E (Playwright)** | .NET stack uses bUnit for component coverage. Epic 7/8 own UI-level tests; Epic 2 read paths are exercised through typed client + Aspire integration. | `Hexalith.Parties.AdminPortal.Tests` and `Hexalith.Parties.Picker.Tests` use bUnit. |
| **Production load testing** | NFR follow-up; Epic 2 captures performance baselines only (R-13). | `LocalFuzzySearchPerformanceBenchmarkTests` and `SemanticSearchPerformanceBenchmarkTests` exist as benchmarks; this design adds rebuild-while-querying baselines. |

---

## Risk Assessment

Risks scored on probability × impact (1–9 scale). Categories: TECH / SEC / PERF / DATA / BUS / OPS.

Threshold rules: 1–3 DOCUMENT (P3), 4–5 MONITOR (P2), 6–8 MITIGATE (P1), 9 BLOCK (P0).

### High-Priority Risks (Score ≥ 6)

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner | Timeline |
|---------|----------|-------------|---|---|-------|------------|-------|----------|
| **R-01** | SEC | Cross-tenant data leakage through projection reads (detail wrong-tenant probe, index list cross-tenant counts, search cross-tenant matches, static cache fallback returning prior-tenant entries, malformed-actor-id probing into other tenant) | 3 | 3 | **9** | Tenant-isolation suite at every level: Tier-1 actor key derivation, Tier-2 query-adapter construction-time + read-time guards via proxy-factory spy, Tier-3 Aspire two-tenant disjoint-result, Topology Fitness for caller-supplied tenant override | QA Lead | Story 2.6 completion |
| **R-02** | SEC | Personal data / token / raw payload / actor-key / state-key / stream-name leakage through diagnostics across detail, list, search, rebuild, and degraded paths | 3 | 3 | **9** | FakeLogger-based scrub assertions across `PartyDetailProjectionQueryActor`, `PartyIndexProjectionQueryActor`, projection actor corruption paths, rebuild service, search providers, degraded middleware; Roslyn deny-list fitness for forbidden log message patterns | QA Lead | Story 2.6 completion |
| R-03 | SEC | Tenant identity accepted from untrusted source — payload tenant-like fields, page cursors, `caseId`, graph context ids, actor ids, partition keys, UI/client metadata override authenticated EventStore tenant context | 2 | 3 | 6 | Before-construction proof tests; payload-tenant-like-field tests confirming caller cannot override authenticated tenant; Topology Fitness banning `EventStoreQueryContext` payload property access from query adapters | Backend Dev + QA | Story 2.6 |
| R-04 | DATA | Erased-party PII resurfaces through search/list/detail/rebuild/static cache (missed `IsErased` filter on cache fallback, name-history projection, semantic provider, AdminPortal local fallback) | 2 | 3 | 6 | Erased-state replay tests through query adapters; static cache + `EraseAsync` coordination tests (post-erase cache miss); rebuild-after-erasure tests proving no PII resurfaces | Backend Dev + QA | Story 2.6 / 2.8 |
| R-05 | DATA | Stale / degraded / rebuilding cache returns mixed-provenance entries — `static s_lastKnownDetails` and `s_lastKnownEntries` dictionaries have no eviction and are not coordinated with `EraseAsync` for entries from prior actor incarnations | 2 | 3 | 6 | Positive-provenance proof tests: cached entry only returnable if tenant + projection kind + party/partition + erasure status + cache-currency match current actor context; otherwise bounded empty/unavailable | Backend Dev + QA | Story 2.7 |
| R-06 | DATA | `PartyDisplayNameDerived.SortName` re-becomes `required` (or other event-contract additive guarantee regresses) → legacy event deserialization breaks → EventStore rehydration fails on historical streams | 2 | 3 | 6 | Tier-1 historical-payload deserialization test; Contracts fitness rejecting any `required` property added post-genesis; `GetSortableName` fallback to `DisplayName` parity across all three search providers | Backend Dev | Story 2.5 |
| R-07 | DATA | Rejection-event Apply ordering regression — refactor/auto-format reorders `PartyState.Apply` methods → EventStore suffix-match misrouting → silent state corruption on rehydration | 2 | 3 | 6 | PRESERVE existing `PartyStateApplyOrderingFitnessTests`, `PartyStateRejectionApplyEndToEndTests`, `PartyDetailProjectionHandlerRejectionFitnessTests` | Backend Dev + QA | Ongoing (cross-cutting invariant) |
| R-08 | SEC | MVP search scope creep — `LocalFuzzyPartySearchProvider` already matches contact channels and identifiers; if MVP `PartySearch` path emits `email`/`identifier`/`contactChannel`/`semantic`/`graph`/`duplicate`/`type` match metadata, the public contract claims unavailable capability and leaks future-reserved metadata | 3 | 2 | 6 | Provider-level negative tests for future-field non-participation; Topology Fitness banning broader-provider reachability from MVP `PartySearch` path; contract fitness asserting `MatchType` / `MatchedField` allowlist | Backend Dev + QA | Story 2.5 / 2.9 |
| R-09 | DATA | List/filter/search metadata (`TotalCount`, `TotalPages`, empty-page behavior, match metadata, score metadata, source metadata) leaks cross-tenant counts because computed before authorization, erasure filtering, or full filter chain | 2 | 3 | 6 | Tier-2 tests proving metadata is calculated AFTER authorization + erasure filter + full filter chain; Tier-3 two-tenant overlap test for exact-count equality with authorized set | Backend Dev + QA | Story 2.4 / 2.5 / 2.6 |
| R-10 | BUS | Freshness/degradation metadata leaks cross-tenant existence — sequence positions, cache ages, rebuild state, partition keys, projection positions, health-check details, or response headers reveal another tenant's activity | 2 | 3 | 6 | Bounded freshness vocabulary tests; cross-tenant freshness probe asserting tenant B never sees tenant A degraded markers; Topology Fitness denying freshness-disclosure additions to public payloads | Backend Dev + QA | Story 2.7 / 2.8 |

### Medium-Priority Risks (Score 4–5)

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner |
|---------|----------|-------------|---|---|-------|------------|-------|
| R-11 | TECH | Display-name normalization drift between aggregate `PartyAggregate.DeriveDisplayName` and projection `PartyIndexProjectionHandler.DeriveSortName` | 2 | 2 | 4 | PRESERVE Story 2.2 patch P1; add re-emergence test if Story 2.5 implements additional normalization | Backend Dev |
| R-12 | TECH | REST/MCP/AdminPortal-only surface reintroduction in `src/Hexalith.Parties` actor host | 2 | 2 | 4 | PRESERVE / extend `ArchitecturalFitnessTests`; deny `MapGet("/api/v1/parties*")`, `[ApiController]`, `MapMcp`, `AdminPortal` → `Projections.*` direct refs | Backend Dev + QA |
| R-13 | PERF | Projection rebuild latency stalls query path during rebuild | 2 | 2 | 4 | Tier-3 capture-only baseline (no numeric threshold); rebuild-while-querying degraded-response rate captured as Nightly artifact | DevOps + QA |
| R-14 | OPS | Failed/canceled/write-failure rebuild leaves projection stuck in rebuilding-degraded forever | 2 | 2 | 4 | Tier-1 rebuild service failure-path tests; Tier-3 chaos rebuild-kill (Weekly only); explicit retry path in metadata | Backend Dev + DevOps |
| R-15 | TECH | Cancellation not terminal — fallback work continues after CT cancel | 2 | 2 | 4 | Tier-2 CT pre-cancel adapter tests; Topology Fitness CT-flow scan; framework-level Dapr CT plumbing assertion | Backend Dev + QA |
| R-16 | BUS | Active filter default behavior inconsistency between stories | 2 | 2 | 4 | Tier-2 test pins current `active=null` default behavior so future stories cannot silently flip it | Backend Dev + QA + PM |
| R-17 | DATA | Date filter / paging overflow — large pageSize → negative skip; locale ambiguity on date parsing; start-after-end validation drift | 2 | 2 | 4 | Tier-2 paging-overflow + UTC-instant + validation tests; long arithmetic for skip calc | Backend Dev |
| R-18 | BUS | Story 2.9 temporal/semantic capability surfaces accidentally exposed in MVP | 2 | 2 | 4 | Unsupported-capability negative tests; contract fitness denying `TemporalNameResult` / `NameHistory` from public endpoints; `[PersonalData]` inventory for `NameHistoryEntry` | Backend Dev + QA |

### Low-Priority Risks (Score 1–3)

| Risk ID | Category | Description | P | I | Score | Action |
|---------|----------|-------------|---|---|-------|--------|
| R-19 | OPS | Health/readiness endpoints become alternate authorization surface — `IsRebuildingAsync` probed without auth context; `/health` reveals tenant existence through degraded markers | 1 | 3 | 3 | Explicit health-output bounded-vocabulary tests (R-19 lift to confidence 7) |
| R-20 | DATA | `HandleProcessingRestricted` exact-time `DateTimeOffset` dedup misses clock-skew / replay-recompute cases (pre-existing pattern deferred from Story 2.1) | 1 | 3 | 3 | Document in `deferred-work.md`; add test if Epic 6 GDPR pulls processing-restriction into scope |
| R-21 | DATA | Pagination tie-break non-deterministic across providers — `LocalFuzzyPartySearchProvider.results.Sort` uses unstable introsort vs. stable `OrderBy.ThenBy` chains in other providers | 2 | 1 | 2 | Add `Id` as final tie-breaker across all three providers (closes Story 2.2 review defer) |

### Risk Category Legend

- **TECH**: Technical/Architecture — boundary drift, contract additivity, surface reintroduction
- **SEC**: Security — cross-tenant leakage, diagnostics PII, untrusted tenant identity, MVP scope creep
- **PERF**: Performance — rebuild latency, query-path throughput
- **DATA**: Data Integrity — erased-PII resurfacing, mixed-provenance cache, metadata leakage, contract additivity, rejection-event ordering, sort determinism
- **BUS**: Business Impact — freshness/degradation leakage, default behavior drift, deferred-capability exposure
- **OPS**: Operations — rebuild stuckness, health-endpoint authorization surface

### Risk Distribution

| Tier | Count | IDs |
|------|-------|-----|
| **P0 — BLOCK** (score 9) | 2 | R-01, R-02 |
| **P1 — MITIGATE** (score 6–8) | 8 | R-03, R-04, R-05, R-06, R-07, R-08, R-09, R-10 |
| **P2 — MONITOR** (score 4–5) | 8 | R-11, R-12, R-13, R-14, R-15, R-16, R-17, R-18 |
| **P3 — DOCUMENT** (score 1–3) | 3 | R-19, R-20, R-21 |

### Category Distribution

| Category | Count | Highest Score |
|----------|-------|---------------|
| SEC | 4 | 9 (R-01, R-02) |
| DATA | 9 | 6 (R-04, R-05, R-06, R-09) |
| BUS | 3 | 6 (R-10) |
| TECH | 3 | 4 (R-11, R-12, R-15) |
| PERF | 1 | 4 (R-13) |
| OPS | 1 | 4 (R-14) |

### Confidence Gate Resolution

| Risk | Initial Confidence | Final Confidence | Action |
|------|--------------------|--------------------|--------|
| R-13 PERF rebuild latency | 5 | 7 | Treat as **capture-only baseline** (2.8-INT-120/121); set threshold after 5 nightly runs |
| R-19 OPS health endpoints as alt-auth | 5 | 7 | Explicit health-output bounded-vocabulary tests (2.8-GTW-180, 2.8-UNIT-181) + Topology Fitness deny-list |
| R-16 BUS active filter default | 6 | 6 | Resolved by 2.4-GTW-150 pinning current behavior |

No risk falls below confidence 5 after gate application; no STOP-and-ASK triggered.

---

## Entry Criteria

- [ ] Stories 2.5, 2.6, 2.7, 2.8 acceptance criteria reviewed and pinned in story files (currently `ready-for-dev`)
- [ ] Story 2.4 review patches landed (`review` → `done`) before Tier-2 query-adapter test extensions
- [ ] EventStore submodule pinned at version compatible with `EventStoreQueryContext.TenantId` exposure (Epic 12 baseline)
- [ ] Tenants submodule pinned at version compatible with two-tenant Aspire fixture
- [ ] FakeLogger pattern available in `Hexalith.Parties.Tests` test harness (extend `Microsoft.Extensions.Logging.Testing` from prior Story 2.3 P3 patch)
- [ ] Existing `PartiesAspireTopologyFixture` extended for two-tenant scenarios (R-01 / R-04 / R-09 Tier-3 coverage)
- [ ] `deferred-work.md` cross-references current static-cache lifecycle gap (Story 2.3 D2 carry-forward)

## Exit Criteria

- [ ] All P0 tests passing (R-01, R-02 — 15 net-new scenarios)
- [ ] All P1 tests passing (or failures triaged with PM/Tech Lead approval) — ~30 net-new scenarios
- [ ] No open high-severity bugs against tenant isolation, diagnostics privacy, erased-PII resurfacing, or freshness metadata leakage
- [ ] Topology Fitness suite at 100% — Roslyn deny-lists (R-02-FIT-014, R-08-FIT-072/073, R-10-FIT-093, R-12-FIT-110/111/112, R-15-FIT-143)
- [ ] Rebuild-latency baseline captured for 5 consecutive Nightly runs (R-13)
- [ ] Health-output bounded-vocabulary tests passing (R-19)
- [ ] `MEMORY.md` and `_bmad/tea/config.yaml` carry-forward items from Epic 12 progress resolved (Playwright/Pact flags → false)

---

## Test Coverage Plan

**Note on priority labels:** P0/P1/P2/P3 below indicate *risk-based priority*, not execution timing. Execution timing (PR / Nightly / Weekly) is handled in the separate Execution Strategy section.

### P0 (Critical) — Tenant Isolation & Diagnostics Privacy

**Criteria:** Blocks GDPR / multi-tenant trust contract + score 9 + no workaround

| Requirement (Risk) | Test Level | Risk Link | Test Count | Owner | Notes |
|--------------------|------------|-----------|------------|-------|-------|
| Query-adapter actor tenant-key derivation | Tier-2 (Gateway) | R-01 | 2 | QA + Backend | `PartyDetail/IndexProjectionQueryActor` proxy-factory spy fails on `{otherTenant}:*` key construction |
| Cross-tenant probe responses | Tier-2 (Gateway) | R-01 | 3 | QA | Detail, list, search — non-enumerating bounded outcomes |
| Projection actor malformed/wrong-tenant id | Tier-1 (Unit) | R-01 | 2 | Backend | Detail + index actor corruption tests |
| Two-tenant disjoint Aspire topology | Tier-3 (Integration) | R-01 | 1 | QA | Real EventStore + Dapr + Memories; overlapping display names |
| Tenant-source authority fitness | Topology Fitness | R-01 | 1 | Backend | Roslyn scan banning literal tenant strings and payload-tenant accessors in query adapters |
| FakeLogger diagnostics scrub — adapter actors | Tier-2 (Gateway) | R-02 | 2 | QA + Backend | Both query adapters across success / not-found / corrupt / degraded / cancellation |
| FakeLogger diagnostics scrub — actor corruption | Tier-1 (Unit) | R-02 | 1 | Backend | Detail + index corruption + rebuild service log assertions |
| FakeLogger diagnostics scrub — rebuild service | Tier-1 (Unit) | R-02 | 1 | Backend | All rebuild failure paths |
| Log deny-list fitness | Topology Fitness | R-02 | 1 | Backend | Roslyn scan of all `LogXxx` calls in `src/Hexalith.Parties*` |
| ProblemDetails PII inventory fitness | Topology Fitness | R-02 | 1 | Backend | Reflection cross-check against `[PersonalData]` inventory |

**Total P0:** 15 scenarios, ~35–55 hours

### P1 (High) — Cross-Cutting Invariants & MVP Discipline

**Criteria:** Critical paths + score 6–8 + common workflows

| Requirement (Risk) | Test Level | Risk Link | Test Count | Owner | Notes |
|--------------------|------------|-----------|------------|-------|-------|
| Tenant identity from untrusted source | Tier-2 (Gateway) | R-03 | 3 | Backend + QA | Payload-tenant-like fields ignored across detail/list/search |
| Before-construction tenant gate proof | Tier-2 (Gateway) | R-03 | 1 | QA | NSubstitute spy on auth-failure paths |
| Tenant-source fitness | Topology Fitness | R-03 | 1 | Backend | `EventStoreQueryContext` property allow-list |
| Erased-party handler purity | Tier-1 (Unit) | R-04 | 2 | Backend | Detail + index handlers clear PII on `PartyErased` |
| Erased-party query response | Tier-2 (Gateway) | R-04 | 1 | QA | Post-erase `Success=true` + `IsErased=true` with no PII (Epic 6 ownership of public shape) |
| Erased-party list/search exclusion | Tier-3 (Integration) | R-04 | 2 | QA | Real state store, real index actor — erased excluded from `Items` + metadata |
| Static cache + `EraseAsync` coordination | Tier-1 (Unit) | R-04 | 1 | Backend | Closes Story 2.3 D2 defer |
| Erase-then-rebuild race | Tier-3 (Integration) | R-04 | 1 | QA | Rebuild reminder cannot resurrect cleared PII |
| Static cache provenance proof | Tier-1 (Unit) | R-05 | 3 | Backend | Detail + index + mixed-provenance tests |
| Degraded read provenance | Tier-2 (Gateway) | R-05 | 1 | QA | `local-only`/`stale`/`rebuilding` only with proven provenance |
| Historical-payload deserialization | Tier-1 (Unit) | R-06 | 1 | Backend | `PartyDisplayNameDerived` without `sortName` field |
| Contracts additivity fitness | Topology Fitness | R-06 | 1 | Backend | Reject any post-genesis `required` property on `Hexalith.Parties.Contracts.Events.*` |
| `GetSortableName` parity | Tier-1 (Unit) | R-06 | 3 | Backend | All three search providers fall back to DisplayName when SortName empty |
| `PartyState.Apply` ordering | Tier-1 (Unit) | R-07 | 3 | Backend | PRESERVE existing fitness + reflection tests |
| MVP search scope — display-name only | Tier-1 (Unit) | R-08 | 2 | Backend | Provider-level positive + negative future-field |
| MVP search scope — provider reachability fitness | Topology Fitness | R-08 | 2 | Backend | Semantic/Memories providers unreachable from MVP path; `MatchType`/`MatchedField` allowlist |
| Search query payload pin | Tier-2 (Client) | R-08 | 1 | Backend | `HttpPartiesQueryClient.SearchPartiesAsync` shape |
| List/search metadata calculation | Tier-2 (Gateway) | R-09 | 3 | Backend + QA | `TotalCount`/`TotalPages` calculated post-authorization + post-erasure-filter + post-filter-chain |
| Two-tenant overlap metadata | Tier-3 (Integration) | R-09 | 1 | QA | 1000+ entries per tenant, exact count equality with authorized set |
| Freshness vocabulary bounded | Tier-2 (Gateway) | R-10 | 1 | Backend | `X-Service-Degraded`/`X-Stale-Data-Age` bucketed values only |
| Cross-tenant freshness probe | Tier-2 (Gateway) | R-10 | 1 | QA | Tenant B never sees tenant A degraded markers |
| `IsRebuildingAsync` bounded response | Tier-1 (Unit) | R-10 | 1 | Backend | Boolean only, no progress / counts / positions |
| Freshness-disclosure fitness | Topology Fitness | R-10 | 1 | Backend | Deny `Sequence`/`Position`/`Offset`/`StreamName`/`PartitionKey`/`CacheAge` additions to `PagedResult<T>` / `PartyDetail` |

**Total P1:** ~30 scenarios, ~50–75 hours

### P2 (Medium) — Drift Prevention & Capture-Only Baselines

**Criteria:** Secondary flows + score 4–5 + edge cases + capture-only NFR

| Requirement (Risk) | Test Level | Risk Link | Test Count | Owner | Notes |
|--------------------|------------|-----------|------------|-------|-------|
| Display-name normalization parity | Tier-1 (Unit) | R-11 | 2 | Backend | PRESERVE Story 2.2 P1 + replay idempotency |
| REST/MCP/AdminPortal surface fitness | Topology Fitness | R-12 | 3 | Backend | PRESERVE `ArchitecturalFitnessTests` + `MapGet` Roslyn scan + AdminPortal/Picker boundary |
| Rebuild latency baseline | Tier-3 (Integration) | R-13 | 2 | DevOps + QA | Capture-only; Nightly artifact (no threshold yet) |
| Rebuild failure paths | Tier-1 (Unit) | R-14 | 3 | Backend | Write failure, mid-flight cancel, checkpoint-delete failure |
| Rebuild chaos kill | Tier-3 (Integration) | R-14 | 1 | DevOps | Weekly only |
| CT pre-cancel adapter tests | Tier-2 (Gateway) | R-15 | 2 | Backend | Detail + index query adapters short-circuit before proxy creation |
| CT propagation client | Tier-2 (Client) | R-15 | 1 | Backend | Search cancellation terminal |
| CT-flow fitness | Topology Fitness | R-15 | 1 | Backend | Roslyn scan of `QueryAsync` adapters |
| Active filter default pin | Tier-2 (Gateway) | R-16 | 1 | Backend + PM | Pins current `active=null` behavior |
| Active filter explicit | Tier-2 (Gateway) | R-16 | 2 | Backend | `active=true`/`active=false` |
| Page/date overflow + locale | Tier-2 (Gateway) | R-17 | 2 | Backend | `pageSize=int.MaxValue` clamp + invalid date range |
| UTC instant comparison | Tier-1 (Unit) | R-17 | 1 | Backend | `PartySearchResultsBuilder.BuildPagedList` |
| Temporal/semantic unsupported-capability | Tier-1 (Unit) | R-18 | 1 | Backend | `PartySearchMode.Semantic`/`Graph` → bounded unsupported |
| Temporal contract fitness | Topology Fitness | R-18 | 1 | Backend | `TemporalNameResult`/`PartyDetail.NameHistory` unreachable from public API |
| NameHistory `[PersonalData]` inventory | Tier-1 (Unit) | R-18 | 1 | Backend | Extend `PersonalDataInventoryTests` |
| NameHistory post-erase clear | Tier-1 (Unit) | R-18 | 1 | Backend | Extend `PartyDetailProjectionHandlerNameHistoryTests` |

**Total P2:** ~25 scenarios, ~20–35 hours

### P3 (Low) — Documented Defers & Determinism Polish

**Criteria:** Nice-to-have + exploratory + score 1–3

| Requirement (Risk) | Test Level | Test Count | Owner | Notes |
|--------------------|------------|------------|-------|-------|
| Health endpoint bounded vocabulary | Tier-2 (Gateway) | 1 | QA | `/health`, `/alive`, `/ready` payloads |
| `ProjectionActorsHealthCheck` description | Tier-1 (Unit) | 1 | Backend | Bounded category text only |
| Pagination tie-break determinism | Tier-1 (Unit) | 1 | Backend | Closes Story 2.2 defer — add `Id` as final tie-breaker across all 3 providers |
| `HandleProcessingRestricted` clock-skew | (documented) | 1 | (deferred to Epic 6) | Document in `deferred-work.md` |

**Total P3:** 4 scenarios, ~4–8 hours

---

## Execution Strategy

**Philosophy:** Run everything in PRs unless infrastructure overhead makes it expensive. Tier-1, Tier-2, and Topology Fitness are fast enough to gate every PR. Tier-3 Aspire topology spin-up (~10–30 s per test) gates Nightly. Chaos / capture-only baselines run Weekly.

| Pipeline | Scope | Target Wall Time |
|----------|-------|-------------------|
| **PR** | Tier-1 (all) + Tier-2 (all) + Topology Fitness (all) | ≤ 6 minutes |
| **Nightly** | PR scope + full Tier-3 Aspire topology suite (two-tenant isolation 2.6-INT-008, erase-rebuild race 2.6-INT-036, rebuild baselines 2.8-INT-120/121, list/search count equality 2.6-INT-083, post-erase exclusion 2.6-INT-033/034) | ≤ 25 minutes |
| **Weekly** | Nightly scope + chaos rebuild kill (2.8-INT-133) + large-tenant search benchmarks (existing `*PerformanceBenchmarkTests`) | ≤ 60 minutes |

Tier-3 topology spin-up is the slowest step (~10–30 s per test fixture); keeping it off PR keeps developer feedback fast while still gating Nightly before merges to `main`. Topology fitness tests run in PR because they are fast Roslyn/reflection-only and catch the highest-leverage drift (REST/MCP reintroduction, log PII regression, freshness disclosure) early.

---

## Resource Estimates

### Test Development Effort

| Priority | Net-New Scenarios | Estimate (range) | Notes |
|----------|-------------------|-------------------|-------|
| **P0** | 15 | ~35–55 hours | Cross-tenant + diagnostics — high test setup complexity (proxy-factory spies, FakeLogger plumbing, Roslyn-based fitness) |
| **P1** | ~30 | ~50–75 hours | Mostly Tier-2/Tier-1 extensions to existing test suites |
| **P2** | ~25 | ~20–35 hours | Mix of fitness scans + capture-only baselines |
| **P3** | 4 | ~4–8 hours | Small focused additions + 1 documented defer |
| **Total** | **~74** | **~110–175 hours** | **~3–5 weeks at 1 FTE** |

**Estimate caveats:**

- Assumes post-Epic-12 test infrastructure is in place: Tier-2 WebApplicationFactory, Tier-3 Aspire fixtures, projection actor harnesses, NSubstitute proxy-factory pattern.
- If FakeLogger plumbing is partial in projection actor / rebuild service / health check projects, add ~10–15 hours for harness work.
- If two-tenant Aspire fixture extension is needed (likely for R-01 / R-04 / R-09 Tier-3), add ~8–12 hours.
- Does not include AdminPortal/Picker bUnit tests — those belong to Epic 7/8.

### Prerequisites

**Test Data:**
- Existing `PartyTestData` helpers in `Hexalith.Parties.Testing`
- Two-tenant fixture builder (extension of `PartiesAspireTopologyFixture` for tenant A / tenant B with overlapping display names + party ids)
- Synthetic personal data only — no real PII

**Tooling:**
- xUnit v3 (`3.2.2`) — test framework
- Shouldly (`4.3.0`) — assertions
- NSubstitute (`5.3.0`) — proxy / state manager / health check mocks
- `Microsoft.Extensions.Logging.Testing` FakeLogger — diagnostics scrubs (R-02 P0 pattern)
- bUnit (`2.7.2`) — out-of-scope for Epic 2 (Epic 7/8)
- Testcontainers (`4.10.0`) — Tier-3 Dapr/EventStore/Memories sidecars
- Aspire (`13.2.2`) — Tier-3 topology
- Roslyn workspaces — Topology Fitness Roslyn scans

**Environment:**
- Aspire AppHost (`src/Hexalith.Parties.AppHost`) with EventStore + Memories + Tenants submodules pinned
- Dapr 1.16.1+ sidecars
- Keycloak realm for OIDC (already provisioned by AppHost)
- Docker Desktop for Testcontainers

---

## Quality Gate Criteria

### Pass/Fail Thresholds

- **P0 pass rate**: 100% — release blocked on any failure (R-01 cross-tenant + R-02 diagnostics privacy are non-negotiable)
- **P1 pass rate**: ≥ 95% — investigate any flake immediately; waivers require Tech Lead + PM approval
- **P2 pass rate**: ≥ 90% — informational; investigate trends
- **P3 pass rate**: best effort

### Coverage Targets

- **All Epic 2 ACs mapped to ≥ 1 test scenario**: 100% (verified in Step-5 traceability checklist below)
- **Security scenarios (SEC category)**: 100% — R-01, R-02, R-03, R-08
- **Cross-tenant scenarios**: 100% — every projection read path has a Tier-2 cross-tenant probe and Tier-3 disjoint-result test
- **Erased-party scenarios**: 100% — every public read path excludes erased entries before metadata
- **MVP search scope**: 100% — every future-reserved field has a negative test

### Non-Negotiable Requirements

- [ ] All P0 tests pass
- [ ] No high-risk (≥ 6) items unmitigated
- [ ] Topology Fitness suite 100% — R-02 log deny-list, R-08 provider reachability, R-10 freshness disclosure, R-12 surface guard, R-15 CT-flow scan
- [ ] FakeLogger scrub assertions on every public log path touched by Epic 2 stories
- [ ] Two-tenant Aspire fixture exists and Tier-3 cross-tenant disjoint test passes

---

## Mitigation Plans (Top-5 by Score)

### R-01: Cross-tenant data leakage through projection reads (Score: 9, P0)

**Mitigation Strategy:**
1. Dedicated tenant-isolation test suite spanning Tier-1 (actor key derivation), Tier-2 (query-adapter construction-time + read-time proxy-factory/state-manager spy), Tier-3 (two-tenant Aspire disjoint-result), Topology Fitness (caller-supplied tenant override deny-list).
2. NSubstitute spy pattern: fail the test if any `IActorProxyFactory.CreateActorProxy<T>(ActorId, ...)` is called with an actor id whose tenant segment differs from the authenticated `EventStoreQueryContext.TenantId`.
3. Two-tenant Aspire fixture: tenant A and tenant B with overlapping display names, identical party-id formats, and intentionally-similar surface area; assert tenant B requests never read tenant A actor state.

**Owner:** QA Lead + Backend Dev
**Timeline:** Story 2.6 completion (currently `ready-for-dev`, target sprint after 2.5 lands)
**Status:** Planned
**Verification:** P0 pass rate 100% for tests 2.6-GTW-001..005, 2.6-UNIT-006..007, 2.6-INT-008, 2.6-FIT-009; verified by retro after Story 2.6 ships.

### R-02: PII / token / raw payload leakage through diagnostics (Score: 9, P0)

**Mitigation Strategy:**
1. FakeLogger-based scrub assertions across every Parties read path: query adapter actors, projection actor corruption paths, rebuild service, search providers, degraded middleware.
2. Roslyn-based Topology Fitness deny-list: forbidden property-name substrings (`DisplayName`, `ContactValue`, `Identifier`, `Authorization`, `payload`, `stateKey`, `streamName`) in `LogXxx` calls and direct `Exception.Message`/`Exception.ToString()` interpolation.
3. ProblemDetails inventory fitness: reflection cross-check against `[PersonalData]` to assert `PartiesClientException.Detail`/`Extensions` never carries PII-bearing fields.

**Owner:** QA Lead + Backend Dev
**Timeline:** Stories 2.5/2.6 completion (covers all 9 stories' diagnostic surfaces)
**Status:** Planned
**Verification:** P0 pass rate 100% for tests 2.6-GTW-010..011, 2.6-UNIT-012..013, 2.6-FIT-014..015; verified by FakeLogger snapshot retro after each story closes.

### R-03: Tenant identity accepted from untrusted source (Score: 6, P1)

**Mitigation Strategy:**
1. Before-construction proof tests asserting no `IPartyDetailProjectionActor`, `IPartyIndexProjectionActor`, `IIndexPartitionStrategy`, search provider, cache, state manager, or projection read adapter is invoked before authenticated tenant gate succeeds.
2. Payload-tenant-like-field tests: explicit attempts to set `payload.tenantId`, `payload.actorId`, `payload.partitionKey`, `caseId`, `mode`, `graphContextPartyId`, `graphContextMemoryUnitId` all ignored by adapters.
3. Topology Fitness: Roslyn scan denying property reads on `EventStoreQueryContext` other than `TenantId`/`CorrelationId`/`Authorization` from query adapter source.

**Owner:** Backend Dev + QA
**Timeline:** Story 2.6
**Status:** Planned
**Verification:** P1 pass rate ≥ 95% for tests 2.6-GTW-020..022, 2.6-FIT-023.

### R-04: Erased-party PII resurfaces through search/list/detail/rebuild/static cache (Score: 6, P1)

**Mitigation Strategy:**
1. Handler-level erasure tests for `PartyDetailProjectionHandler` and `PartyIndexProjectionHandler` proving `PartyErased` clears all PII fields.
2. Tier-3 two-tenant integration tests asserting post-erase list/search responses exclude erased entries from `Items` AND `TotalCount`/`TotalPages` metadata.
3. Static cache + `EraseAsync` coordination: tests that exercise `s_lastKnownDetails` / `s_lastKnownEntries` post-erase and assert cache lookup returns bounded empty/unavailable (closes Story 2.3 D2 carry-forward).
4. Erase-then-rebuild race: Tier-3 test triggering rebuild reminder after `EraseAsync` and asserting no PII resurfaces.

**Owner:** Backend Dev + QA
**Timeline:** Stories 2.6 / 2.8 (rebuild path)
**Status:** Planned
**Verification:** P1 pass rate ≥ 95% for tests 2.6-UNIT-030..031, 2.6-GTW-032, 2.6-INT-033..036.

### R-05: Stale/degraded cache mixed-provenance entries (Score: 6, P1)

**Mitigation Strategy:**
1. Positive-provenance proof tests: cached entry returnable only if tenant id + projection kind + party/partition key + erasure status + cache-currency version match current actor context.
2. Mixed-provenance test: two tenants exercise the same static dictionary; tenant B activation cannot return tenant A cached entries via shared static state.
3. Degraded read provenance: cached entries returned with bounded `local-only`/`stale`/`rebuilding` signal only when provenance proven; otherwise bounded unavailable.

**Owner:** Backend Dev + QA
**Timeline:** Story 2.7
**Status:** Planned
**Verification:** P1 pass rate ≥ 95% for tests 2.7-UNIT-040..042, 2.7-GTW-043.

(Mitigation plans for R-06 through R-21 follow the same pattern; full details are in the coverage matrix above.)

---

## Assumptions and Dependencies

### Assumptions

1. EventStore submodule version pinned in the working tree exposes `EventStoreQueryContext.TenantId` from the authenticated query path (Epic 12 baseline assumption).
2. Tenants submodule provides multi-tenant access state through the existing Aspire wiring; two-tenant fixtures are extensions of `PartiesAspireTopologyFixture`, not greenfield infrastructure.
3. `Microsoft.Extensions.Logging.Testing` FakeLogger pattern from Story 2.3 P3 patch is reusable across all Parties test projects.
4. Memories submodule is reachable through `MemoriesPartySearchService` and `MemoriesSearchHealthCheck`; Tier-3 search tests assume it; if unavailable, tests must explicitly degrade rather than fail (per Story 2.4 AC4 / Story 2.7 AC4).
5. Story 2.4 review patches land before extending its tests; current `review` status is treated as authoritative-with-pending-patches.
6. Epic 6 (GDPR) owns the public erased-party response shape contract; this design covers Epic 2's tenant-safe / privacy-preserving behavior but not the response-shape redesign.
7. Epic 7/8 own AdminPortal/Picker UI tests; this design's R-12 fitness boundary tests assert the AdminPortal/Picker do not bypass `IPartiesQueryClient`, but do not redesign UI behavior.

### Dependencies

1. **Story 2.4 → done** — Required before R-09 list metadata extensions; currently in `review`.
2. **Story 2.5 → ready-for-dev** — Drives R-06, R-08, R-21 implementation; pre-dev hardening complete.
3. **Story 2.6 → ready-for-dev** — Drives R-01, R-02, R-03, R-04 implementation; pre-dev hardening complete.
4. **Story 2.7 → ready-for-dev** — Drives R-05, R-10 implementation; pre-dev hardening complete.
5. **Story 2.8 → ready-for-dev** — Drives R-13, R-14, R-19 implementation; pre-dev hardening complete.
6. **Story 2.9 → ready-for-dev** — Drives R-18 implementation; preparation-only scope.
7. **`_bmad/tea/config.yaml` cleanup** — Carry-forward from Epic 12 progress: set `tea_use_playwright_utils=false`, `tea_use_pactjs_utils=false`, clear `tea_pact_mcp`. Required before this design's recommendations interact with the wider TEA tooling chain.

### Risks to Plan

- **Risk:** Two-tenant Aspire fixture extension is non-trivial — `Hexalith.Tenants` may need additional seed data fixtures.
  - **Impact:** Could delay R-01 / R-04 / R-09 Tier-3 coverage by 1–2 days.
  - **Contingency:** Start with R-01 Tier-2 proxy-factory spy coverage (already feasible without two-tenant fixture); Tier-3 can land in a follow-up PR if fixture work overruns.

- **Risk:** Story 2.4 patches may shift `PartySearchResultsBuilder` API surface, changing how R-17 paging/date tests attach.
  - **Impact:** Re-wire 2 tests.
  - **Contingency:** Wait for 2.4 `done` status before extending its tests; in the meantime, focus on R-06 / R-08 (Story 2.5 prep) and R-01 / R-02 (Story 2.6 prep).

- **Risk:** FakeLogger pattern may not extend cleanly to projection actors that use Dapr `ActorHost.CreateForTest` host-internal logger.
  - **Impact:** R-02 Tier-1 corruption-path scrub tests require additional harness work.
  - **Contingency:** Use NSubstitute on `ILogger<T>` directly with `Received()` filter assertions; same evidence, different mechanism.

---

## Follow-on Workflows (Manual)

- `bmad-testarch-atdd` — draft failing P0 tests for R-01 cross-tenant + R-02 diagnostics privacy (start with `2.6-GTW-001/002/010/011`).
- `bmad-testarch-automate` — generate P1 implementation skeletons once Stories 2.5/2.6/2.7 land harness changes.
- `bmad-testarch-trace` — formal quality-gate decision after P0 + P1 green; produces traceability matrix for Epic 2 retrospective.
- `bmad-testarch-nfr` — formalize rebuild-latency SLO (R-13) after 5 Nightly baselines.
- `bmad-correct-course` — if Epic 6 GDPR scoping changes the public erased-party response shape during Epic 2 implementation.

---

## Approval

**Test Design Approved By:**

- [ ] Product Manager: __________ Date: __________
- [ ] Tech Lead: __________ Date: __________
- [ ] QA Lead (Murat): __________ Date: 2026-05-19 (draft)

**Comments:**

_Pre-dev hardening for Stories 2.5–2.9 already incorporates the AC-to-test mappings called out in this design. Cross-check this design against any party-mode or advanced-elicitation traces added after 2026-05-19 before final approval._

---

## Interworking & Regression

| Service / Component | Impact | Regression Scope |
|---------------------|--------|------------------|
| **Hexalith.EventStore (submodule)** | Authenticates request tenant via query gateway; emits authenticated `EventStoreQueryContext.TenantId` to Parties query adapters | EventStore's own query gateway + tenant authentication tests must remain green; Parties Tier-2 gateway routing tests pin the contract (`EventStoreGatewayRoutingTests`). |
| **Hexalith.Tenants (submodule)** | Provides tenant access state / membership probes | Tenants service health-check passes during Tier-3 Aspire fixtures (`TenantsIntegrationHealthCheckTests` is already a smoke probe). |
| **Hexalith.Memories (submodule)** | Backs `MemoriesPartySearchService`; degraded reads when unreachable | `MemoriesSearchHealthCheckTests` + `MemoriesPartySearchServiceTests` (existing) must remain green; this design's R-05 degraded-provenance tests exercise the Memories-unreachable path. |
| **Hexalith.FrontComposer (submodule)** | Hosts AdminPortal Razor components that consume `IPartiesQueryClient.GetPartyAsync/ListPartiesAsync/SearchPartiesAsync` | AdminPortal contract tests in `Hexalith.Parties.Client.Tests/AdminPortal` must remain green; R-12 fitness asserts AdminPortal does not bypass `IPartiesQueryClient`. |
| **Hexalith.Parties.Mcp** | Thin MCP host consuming `IPartiesQueryClient` per Epic 12 | `PartiesMcpProjectFitnessTests` must remain green; R-12 fitness asserts MCP path-equivalence. |
| **Hexalith.Parties.AdminPortal, Hexalith.Parties.Picker** | Blazor components consuming typed queries | bUnit tests in `*.AdminPortal.Tests` / `*.Picker.Tests` belong to Epic 7/8; this design's R-12 fitness asserts they remain on `IPartiesQueryClient`. |

---

## Appendix

### Knowledge Base References

- `risk-governance.md` — risk classification framework, gate decision engine, mitigation tracking, traceability matrix
- `probability-impact.md` — probability/impact scale (1–9), DOCUMENT/MONITOR/MITIGATE/BLOCK thresholds
- `test-levels-framework.md` — unit/integration/E2E selection rules, anti-patterns
- `test-priorities-matrix.md` — P0–P3 criteria, coverage targets, execution order

### Related Documents

- **Epic:** [Epic 2 in `_bmad-output/planning-artifacts/epics.md`](../../planning-artifacts/epics.md)
- **PRD:** [`_bmad-output/planning-artifacts/prd.md`](../../planning-artifacts/prd.md) — FR14 list/filter, FR15 display-name search, FR17 match metadata, FR18 detail retrieval, FR19 projection consistency, FR39–FR41 tenant/security, FR64 graceful degradation, FR68 date filtering, FR71 health/readiness, FR72 deferred-capability boundaries, NFR21 stale-read indicator
- **Architecture:** [`_bmad-output/planning-artifacts/architecture.md`](../../planning-artifacts/architecture.md) — D1 projection data store, D4 projection actor granularity, D5 index actor state management, D15 projection health monitoring with auto-rebuild, D16 index actor batch event processing, D18 projection testability
- **Project Context:** [`_bmad-output/project-context.md`](../../project-context.md) — EventStore ownership, projection-side tenant safety, privacy, testing, and actor-host guardrails
- **Stories 2.1–2.9:** [`_bmad-output/implementation-artifacts/2-*.md`](../../implementation-artifacts/)
- **Prior Epic 12 test design (reference):** [`_bmad-output/test-artifacts/test-design/test-design-epic-12.md`](test-design-epic-12.md)
- **Working notes / progress:** [`_bmad-output/test-artifacts/test-design-progress.md`](../test-design-progress.md)

### Acceptance Evidence Traceability

| Story | AC Theme | Primary Test IDs |
|-------|----------|------------------|
| 2.1 | Detail projection handler purity, rejection no-ops, no-op timestamp preservation | 2.1-UNIT-060..062 (PRESERVE) |
| 2.2 | Tenant index projection handler, sortable names, replay idempotency | 2.2-UNIT-100..101 |
| 2.3 | Detail query through EventStore-fronted gateway, no aggregate replay, terminal cancellation | 2.3-GTW-140 |
| 2.4 | Index list query, post-filter metadata, tenant-only paging | 2.4-GTW-080..081, 2.4-GTW-141, 2.4-GTW-150..151, 2.4-GTW-160..161, 2.4-UNIT-162 |
| 2.5 | Display-name MVP search, match metadata, deterministic ranking | 2.5-UNIT-050, 2.5-UNIT-052, 2.5-UNIT-070..071, 2.5-UNIT-200, 2.5-FIT-051, 2.5-FIT-072..073, 2.5-GTW-074, 2.5-GTW-082, 2.5-GTW-142 |
| 2.6 | Cross-tenant fail-closed across all read paths | 2.6-GTW-001..005, 2.6-UNIT-006..007, 2.6-INT-008, 2.6-FIT-009, 2.6-GTW-010..013, 2.6-FIT-014..015, 2.6-GTW-020..023, 2.6-UNIT-030..036, 2.6-INT-083, 2.6-FIT-110..112, 2.6-FIT-143 |
| 2.7 | Freshness / degradation bounded vocabulary, provenance proof | 2.7-UNIT-040..042, 2.7-GTW-043, 2.7-GTW-090..092, 2.7-FIT-093 |
| 2.8 | Projection rebuild safety, health classification | 2.8-INT-120..121, 2.8-UNIT-130..132, 2.8-INT-133, 2.8-GTW-180, 2.8-UNIT-181 |
| 2.9 | Deferred-capability guardrails | 2.9-UNIT-170, 2.9-FIT-171, 2.9-UNIT-172..173 |

---

**Generated by**: BMad TEA Agent — Test Architect Module (Murat persona)
**Workflow**: `bmad-testarch-test-design` (Epic-Level Mode)
**Version**: 5.0 (Step-File Architecture)
