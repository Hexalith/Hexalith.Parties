---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-05-19'
epic: 12
epic_title: 'EventStore-Fronted Architecture Pivot'
mode: 'epic-level'
designLevel: 'Epic-Level'
---

# Test Design: Epic 12 — EventStore-Fronted Architecture Pivot

**Date:** 2026-05-19
**Author:** Jérôme (with Murat — Master Test Architect)
**Status:** Draft

> P0 / P1 / P2 / P3 labels denote **priority** (risk-driven), not execution timing. Execution timing is handled separately in the Execution Strategy section.

---

## Executive Summary

**Scope:** Epic-Level test design for Epic 12 (11 stories: 12.0 – 12.10), covering the architectural pivot that moves command/query ingress from in-process Parties controllers to the EventStore HTTP gateway, with Parties recomposed as a thin actor + projection host, plus a separate `parties-mcp` consumer host, a rebuilt Admin Portal on FrontComposer, and a rewritten Picker.

**Pivot lens:** The pivot moves the request boundary, validation point, and tenant authorization between services. 60 % of P1+ risks are SEC or DATA — this is fundamentally a security and data-integrity surface change, not a refactor.

**Risk Summary:**

- Total risks identified: **20**
- BLOCK / P0 (score 9): **1**
- MITIGATE / P1 (score 6-8): **9**
- MONITOR / P2 (score 4-5): **6**
- DOCUMENT / P3 (score 1-3): **4**
- Dominant categories: **SEC (6), OPS (5), DATA (4)**

**Coverage Summary:**

- P0 scenarios: 7 (~25-40 hours)
- P1 scenarios: ~38 (~60-90 hours)
- P2 scenarios: ~8 + 1 traceability audit (~15-25 hours)
- P3 scenarios: 4 (~4-8 hours)
- **Total effort**: ~105-165 hours (~3-5 weeks)

Estimates assume the post-pivot test harnesses (Tier-2 WebApplicationFactory + Tier-3 Aspire fixtures + bUnit harnesses) are already in place per Story 12.4. If any harness is partial, add ~15-25 hours for harness work.

**Status of Epic 12 stories:** All 11 (`12.0` – `12.10`) are marked `done` at the per-story level. This test design therefore serves two purposes: (1) **retrospective coverage assessment** to surface gaps in the landed pivot, and (2) **forward-looking quality gates** for downstream Epic 7 (Administration Console) and Epic 8 (Embeddable Party Picker), which are contractually gated on the accepted EventStore-fronted Parties client/gateway contract per `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.

> **Documentation note (non-blocker):** `_bmad-output/implementation-artifacts/sprint-status.yaml`'s `development_status` block stops at `epic-9`. Epic 12 stories are all `done` in their own files but Epic 12 is not surfaced in the rollup. Recommend reconciling in a follow-on housekeeping pass.

---

## Not in Scope

| Item | Reasoning | Mitigation |
|------|-----------|------------|
| EventStore submodule source changes | Per Story 12.0 / 12.1 scope rules, EventStore is a sibling submodule; Parties must not edit its source | EventStore-side concerns become platform tickets, not Parties test items |
| FrontComposer shell internals | FrontComposer is a sibling submodule; the Admin Portal *consumes* it, doesn't own it | Coverage of FrontComposer integration is limited to *Parties domain manifest registration and routing* (12.7) |
| Tenants service internals | Tenants is a sibling submodule | Coverage limited to *Parties consumption of Tenants events and tenant ACL wiring* (existing IntegrationTests/Tenants) |
| Production load-tier performance | No production-grade load testing in scope; latency baseline is capture-only | NFR work tracked separately if SLO thresholds are formalized |
| Browser E2E (Playwright/Selenium) | Admin Portal & Picker have bUnit component coverage; full-browser E2E would require Playwright .NET and is out of scope for Epic 12 | Risk accepted: bUnit covers component behavior; visual regression deferred |
| JS Pact contract testing | `tea_use_pactjs_utils=true` flag in `_bmad/tea/config.yaml` is JS-stack specific and does not apply here | Consumer/provider contract is enforced by typed `IPartiesCommandClient` / `IPartiesQueryClient` interfaces + fitness tests (R-18) |

---

## Risk Assessment

Scoring: probability × impact, each on a 1-3 scale (1=Low/Unlikely, 2=Medium/Possible, 3=High/Likely). Threshold rules: 1-3 DOCUMENT (P3), 4-5 MONITOR (P2), 6-8 MITIGATE (P1), 9 BLOCK (P0).

### Critical Risks (Score 9 — BLOCK / P0)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner | Timeline |
|---------|----------|-------------|-------------|--------|-------|------------|-------|----------|
| **R-01** | SEC | Personal data / token / raw payload leakage through error responses, logs, telemetry, storage keys, URLs, filenames across the multi-hop path (Client → EventStore gateway → DAPR → Parties sidecar → actor) | 3 | 3 | **9** | Dedicated PII-leak coverage at every error category (validation / unauthorized / forbidden / not-found / gone / degraded / timeout / malformed) across Client, MCP, AdminPortal, Picker; structured-log scrubbing; fitness scans for forbidden log keys and DeployValidation output | Backend + QA | Before Epic 7/8 scheduling |

### High-Priority Risks (Score 6-8 — MITIGATE / P1)

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner | Timeline |
|---------|----------|-------------|---|---|-------|------------|-------|----------|
| R-02 | SEC | Validation bypass: invalid command reaches `PartyAggregate.Handle` without invoker-path validation; malformed events persist (immutable) | 2 | 3 | 6 | Tier-1 invoker pipeline tests + Tier-2 gateway tests asserting validation envelope and zero-event persistence | Backend | Before Epic 7/8 |
| R-03 | SEC | Tenant authorization regression: window where neither EventStore `ITenantValidator`/`IRbacValidator` nor retired Parties `ITenantAccessService` enforces gating | 2 | 3 | 6 | Fitness tests removing `ITenantAccessService` from command/query paths; Tier-2/3 unauthorized-tenant/user/role rejection tests; `AggregateActor` tenant-mismatch guard preserved | Backend | Before Epic 7/8 |
| R-04 | DATA | Cross-tenant data leakage through projection reads under stale state, missing tenant access state, or projection rebuild | 2 | 3 | 6 | Tier-1 projection fail-closed unit tests + Tier-3 cross-tenant integration tests under rebuild and degraded states | Backend + QA | Before Epic 7/8 |
| R-05 | SEC | XSS / encoding bypass in Admin Portal & Picker via `MarkupString`, `AddMarkupContent`, raw HTML, JS interop, `innerHTML`, unsafe markdown | 2 | 3 | 6 | bUnit XSS regression tests for display names, GDPR details, ProblemDetails text; fitness scans of `.razor` source for forbidden render patterns | Frontend + QA | Before Epic 7/8 (Picker) and Epic 7 (AdminPortal) |
| R-06 | OPS | DAPR access-control YAML drift: missing files, wildcard app-id, wildcard `/**` path, or deny-by-default not enforced | 2 | 3 | 6 | `AppHostDaprTopologyValidationTests` extended per AC 12.10.3; deny-list for wildcards; single-file invocation policy enforced for `parties` sidecar | DevOps | Continuous |
| R-07 | DATA | Rejection-event Apply ordering regression: auto-formatters reorder `PartyState.Apply` causing EventStore suffix-match misrouting → silent state corruption on rehydration | 2 | 3 | 6 | PRESERVE existing rejection-event fitness tests + rehydration replay tests; do not relax under any refactor | Backend | Continuous |
| R-08 | OPS | Deployment validation gap: manifests missing required EventStore-fronted service / DAPR component / Tenants manifest pass validation | 2 | 3 | 6 | `TenantsDeploymentValidationTests` + `DeploymentValidationTests` extended per AC 12.10.2 / 12.10.7 to cover all five services + state-store + pub/sub config | DevOps | Before Epic 7/8 |
| R-09 | BUS | Composite command guard bypass: max-sub-op count, duplicate detection, erasure-status, processing-restriction, idempotent-no-op behavior bypassed by new invocation path | 2 | 3 | 6 | Tier-1 composite-aggregate tests preserved; new Tier-2 gateway tests asserting applied/skipped/rejected outcomes and emitted event order | Backend | Before Epic 7/8 |
| R-10 | DATA | GDPR / Memories / temporal-name / tenant-isolation scenarios lose coverage during Tier-2/3 rewrite | 2 | 3 | 6 | Traceability audit + new/extended Tier-3 tests for erasure, key lifecycle, consent, restriction, portability, Memories search, temporal name, health/readiness | QA | Before Epic 7/8 |

### Medium-Priority Risks (Score 4-5 — MONITOR / P2)

| Risk ID | Category | Description | P | I | Score | Mitigation |
|---------|----------|-------------|---|---|-------|------------|
| R-11 | TECH | Public REST / MCP surface reintroduction into `Hexalith.Parties` actor host | 2 | 2 | 4 | `ArchitecturalFitnessTests` deny-list preserved per AC 12.2.5 / 12.4.5 / 12.10.4 |
| R-12 | PERF | Latency regression from two extra hops (Client → EventStore gateway → DAPR → Parties sidecar) | 2 | 2 | 4 | Capture-only baseline at Tier-3; numeric SLO set after 5 nightly runs |
| R-13 | OPS | Aspire startup dependency ordering drift across `eventstore → admin → ui → tenants → parties → parties-mcp` | 2 | 2 | 4 | `AppHostTenantsTopologyTests` dependency edge assertions per AC 12.10.1 |
| R-14 | SEC | Keycloak OIDC drift across `eventstore`, `eventstore-admin`, `parties`, `tenants` (realm/audience/client) | 2 | 2 | 4 | Topology fitness scanning environment variable wiring per AC 12.1.5 |
| R-15 | OPS | Test rewrite coverage loss: Tier-2/3 tests retired without replacement | 2 | 2 | 4 | Traceability audit per AC 12.4.8 |
| R-16 | TECH | Spike-shaped code (deterministic spike-only tenant/actor values; bypassed auth) persists in production | 2 | 2 | 4 | Fitness grep for spike-only literals + Tier-3 realistic-auth scenarios (token expiry, malformed claims, audience mismatch) |

### Low-Priority Risks (Score 1-3 — DOCUMENT / P3)

| Risk ID | Category | Description | P | I | Score | Action |
|---------|----------|-------------|---|---|-------|--------|
| R-17 | BUS | MCP `get_party_name_at` compatibility decision (AC 12.6.10) silently dropped | 1 | 2 | 2 | Trace AC 12.6.10 to recorded product decision; canonical MCP tool tests |
| R-18 | TECH | `Hexalith.Parties.Client` package boundary violation (Server/Projections/DAPR/MediatR/FluentValidation/MVC pulled in transitively) | 1 | 2 | 2 | Existing `Client.Tests/FitnessTests` is sufficient |
| R-19 | DATA | Encryption / crypto-shredding boundaries misplaced; right-to-erasure not propagating to all read paths in new chain | 1 | 3 | 3 | Spot-check Tier-3 erasure tests after pivot |
| R-20 | OPS | Plaintext secrets in AppHost output, launchSettings, generated config, or loggable deployment evidence | 1 | 3 | 3 | Fitness scan per AC 12.1.8 / 12.10.5 / 12.10.8 |

### Risk Category Legend

- **TECH**: Technical / Architecture (boundary violations, package coupling, fragility)
- **SEC**: Security (access control, auth, data exposure, XSS, injection)
- **PERF**: Performance (latency, scalability, resource utilisation)
- **DATA**: Data Integrity (corruption, leakage, cross-tenant, GDPR)
- **BUS**: Business Impact (logic errors, surface regressions affecting consumers)
- **OPS**: Operations (deployment, monitoring, topology, drift)

---

## Entry Criteria

- [ ] Story 12.4 Tier-1/Tier-2 test harness is in place and green (post-pivot baseline)
- [ ] Aspire AppHost can spin up the five canonical resources (`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `tenants`) plus `parties-mcp`
- [ ] DAPR access-control YAMLs (`accesscontrol.eventstore-admin.yaml`, `accesscontrol.tenants.yaml`, `accesscontrol.parties.yaml`, base `accesscontrol.yaml`, `resiliency.yaml`) exist under `src/Hexalith.Parties.AppHost/DaprComponents/`
- [ ] Test-data factories for Parties domain (`PartyId`, `TenantId`, encrypted personal-data payloads) are available in `Hexalith.Parties.Testing`
- [ ] `Hexalith.Parties.Client` thin-wrapper surface is the consumer-facing contract under test

## Exit Criteria

- [ ] R-01 (PII leakage) P0 coverage at 100 % pass rate before Epic 7 / 8 scheduling
- [ ] All P1 risks (R-02 to R-10) have ≥ 95 % pass rate on Nightly
- [ ] All seven downstream contract clauses (see *Downstream Gate* below) have at least one P0 or P1 test backing them
- [ ] No open high-severity bug against the EventStore gateway path, the Parties actor invoker, or the projection read path
- [ ] Latency baseline (R-12) captured on 5 consecutive Nightly runs

---

## Test Coverage Plan

Test ID format: `12.{Story}-{LEVEL}-{SEQ}` where `LEVEL ∈ {UNIT, GTW, INT, COMP, FIT}` for *Tier-1 Unit*, *Tier-2 Gateway*, *Tier-3 Topology Integration*, *Component (bUnit)*, *Topology Fitness*.

### P0 (Critical)

**Criteria**: Blocks core journey AND high risk (score ≥ 6) AND no workaround. Currently bounded to **R-01 (PII / token / raw-payload leakage)** because of the multi-hop blast radius.

| Test ID | Requirement / Risk | Test Level | Test Count | Owner | Notes |
|---------|--------------------|------------|------------|-------|-------|
| 12.5-GTW-001 | R-01 — Client error envelope strips raw payload, tokens, `[PersonalData]` across 8 error categories | Tier-2 | 1 | Backend + QA | Extends `Hexalith.Parties.Client.Tests`. Cover validation / unauthorized / forbidden / not-found / conflict / degraded / timeout / malformed. |
| 12.6-GTW-002 | R-01 — MCP host error responses are sanitized agent-actionable envelopes; log assertions scrub tokens, raw command JSON, PII | Tier-2 | 1 | Backend + QA | New under `Hexalith.Parties.Mcp.Tests`. |
| 12.7-COMP-003 | R-01 — AdminPortal degraded/timeout/malformed/ProblemDetails components render encoded text only; no tokens / PII / raw payload in DOM | Component (bUnit) | 1 | Frontend + QA | Extends `Hexalith.Parties.AdminPortal.Tests/Components`. |
| 12.8-COMP-004 | R-01 — Picker error and degraded state components do not surface tokens / tenant context / PII in DOM, storage keys, URLs, filenames, telemetry dimensions, or DOM event names | Component (bUnit) | 1 | Frontend + QA | Extends `Hexalith.Parties.Picker.Tests/Components`. |
| 12.5-UNIT-005 | R-01 — `PartiesClientException` serializer strips `[PersonalData]` fields from `ToString()` and structured-log payload | Tier-1 | 1 | Backend | New under `Hexalith.Parties.Client.Tests`. |
| 12.10-FIT-006 | R-01 — Fitness scan of Client / MCP / AdminPortal / Picker source for `LogError`/`LogWarning` calls referencing token, Authorization, payload keys (deny-list) | Topology Fitness | 1 | Backend | New under `Hexalith.Parties.Tests/FitnessTests`. |
| 12.10-FIT-007 | R-01 — DeployValidation console + JSON output strips operator secrets, tokens, connection strings, Keycloak credentials, DAPR internals beyond file/check names | Topology Fitness | 1 | DevOps | Extends `Hexalith.Parties.DeployValidation.Tests` per AC 12.10.5. |

**Total P0**: 7 scenarios

### P1 (High)

**Criteria**: Important features OR medium-high risk (score 6-8) OR common workflows. Maps to **R-02 through R-10**.

| Test ID | Requirement / Risk | Test Level | Test Count | Owner | Notes |
|---------|--------------------|------------|------------|-------|-------|
| 12.3-UNIT-010 | R-02 — Invoker validation pipeline runs validators before `PartyAggregate.Handle` for every command type | Tier-1 | 1 | Backend | Extends `Hexalith.Parties.Server.Tests/Aggregates`. |
| 12.3-GTW-011 | R-02 — Invalid payload via EventStore gateway returns EventStore platform validation envelope and persists zero events | Tier-2 | 1 | Backend | New under `Hexalith.Parties.Tests/Gateway` or `Validation`. |
| 12.3-INT-012 | R-02 — End-to-end invalid payload: zero stream entries, no pub/sub event, no projection write | Tier-3 | 1 | QA | Extends `Hexalith.Parties.IntegrationTests/Events`. |
| 12.3-FIT-013 | R-02 — Every Contracts command has at least one validator referenced from the invoker registration | Topology Fitness | 1 | Backend | New. |
| 12.3-FIT-020 | R-03 — `ITenantAccessService` and `TenantAccessDenialTranslator` not referenced from command/query request paths | Topology Fitness | 1 | Backend | Per AC 12.3.4 / 12.3.7. |
| 12.3-GTW-021 | R-03 — Unauthorized tenant rejected before actor invocation; EventStore platform forbidden envelope | Tier-2 | 1 | Backend | New under `Authorization`. |
| 12.3-GTW-022 | R-03 — Unauthorized role rejected at gateway before actor invocation | Tier-2 | 1 | Backend | |
| 12.3-INT-023 | R-03 — Cross-tenant command attempt rejected; no events persisted; no projection mutation | Tier-3 | 1 | QA | Extends `IntegrationTests/Tenants`. |
| 12.3-INT-024 | R-03 — `AggregateActor` tenant-mismatch guard intact with forged actor-id | Tier-3 | 1 | QA | Per AC 12.3.6. |
| 12.4-UNIT-030 | R-04 — `PartyDetailProjectionHandler` fail-closed when tenant context missing | Tier-1 | 1 | Backend | Extends `Projections.Tests/Handlers`. |
| 12.4-UNIT-031 | R-04 — `PartyIndexProjectionHandler` fail-closed under stale tenant access state | Tier-1 | 1 | Backend | |
| 12.4-INT-032 | R-04 — Search during projection rebuild returns degraded response, never cross-tenant entries | Tier-3 | 1 | QA | Extends `IntegrationTests/Search`. |
| 12.4-INT-033 | R-04 — Admin browse during missing tenant access state returns bounded empty / degraded state | Tier-3 | 1 | QA | Extends `IntegrationTests/Admin`. |
| 12.7-COMP-040 | R-05 — AdminPortal renders malicious payloads (`<script>`, `javascript:`, `<img onerror>`, entity-escaped variants) as encoded text only | Component (bUnit) | 1 | Frontend | Per AC 12.7.8. |
| 12.8-COMP-041 | R-05 — Picker renders malicious display name, contact value, identifier, consent text, degraded reason, ProblemDetails payloads as encoded text only | Component (bUnit) | 1 | Frontend | Per AC 12.8.7. |
| 12.7-FIT-042 | R-05 — AdminPortal `.razor` source scan deny-list: `MarkupString`, `AddMarkupContent`, raw HTML fragments, `innerHTML`, `Html.Raw`, unsafe markdown | Topology Fitness | 1 | Frontend | New. |
| 12.8-FIT-043 | R-05 — Same fitness scan for Picker `.razor` source | Topology Fitness | 1 | Frontend | New. |
| 12.10-FIT-050 | R-06 — Each receiving sidecar uses correct `accesscontrol.*.yaml`, deny-by-default posture, explicit app-id matrix, `parties → eventstore` + `eventstore → parties /process` permissions | Topology Fitness | 1 | DevOps | Extends `AppHostDaprTopologyValidationTests`. |
| 12.10-FIT-051 | R-06 — DAPR ACL deny-list: no wildcard app-id (`*`), no wildcard path (`/**`) | Topology Fitness | 1 | DevOps | Per AC 12.10.3. |
| 12.10-FIT-052 | R-06 — Parties actor sidecar references only `accesscontrol.parties.yaml` for DAPR invocation policy | Topology Fitness | 1 | DevOps | Per AC 12.10.4. |
| 12.2-UNIT-060 | R-07 — `PartyState.Apply` rejection-event overloads precede success-event overloads (reflection or source-order check) | Tier-1 | 1 | Backend | PRESERVE existing `Server.Tests/Aggregates` test. |
| 12.2-UNIT-061 | R-07 — Each `IRejectionEvent` Apply is a concrete instance no-op method (not static, not missing) | Tier-1 | 1 | Backend | PRESERVE existing fitness test. |
| 12.2-UNIT-062 | R-07 — Rehydration replay with rejection events produces state identical to success-only history | Tier-1 | 1 | Backend | PRESERVE. |
| 12.10-FIT-070 | R-08 — DeployValidation rejects manifests missing `eventstore`, `eventstore-admin`, `parties`, `tenants`, `parties-mcp`, shared `statestore`, shared `pubsub` | Topology Fitness | 1 | DevOps | Per AC 12.10.2. |
| 12.10-FIT-071 | R-08 — State-store config (`actorStateStore=true`, `keyPrefix=none` where required), pub/sub scopes for Tenants + subscribers, dead-letter settings, explicit scopes for the four app-ids | Topology Fitness | 1 | DevOps | Per AC 12.10.7. |
| 12.10-FIT-072 | R-08 — Topology validation distinguishes user-facing gateway readiness from internal actor-host liveness, Tenants dependency health, EventStore admin wiring, parties-mcp host | Topology Fitness | 1 | DevOps | Per AC 12.10.6. |
| 12.3-UNIT-080 | R-09 — Composite command guards via invoker path: max sub-op count, duplicate detection, erasure-status, processing-restriction, idempotent no-op | Tier-1 | 1 | Backend | PRESERVE / extend `Server.Tests`. |
| 12.3-GTW-081 | R-09 — Composite via EventStore gateway returns expected applied/skipped/rejected sub-results and emitted event order | Tier-2 | 1 | Backend | New. |
| 12.3-INT-082 | R-09 — Composite durability: EventStore stream evidence confirms event ordering, no partial atomicity | Tier-3 | 1 | QA | Extends `IntegrationTests/Events`. |
| 12.4-INT-090 | R-10 — Right-to-erasure via crypto-shredding: post-erasure reads return privacy-preserving placeholder; ProblemDetails preserved | Tier-3 | 1 | QA | Extends `IntegrationTests/Security`. |
| 12.4-INT-091 | R-10 — Key lifecycle: per-party rotation preserves post-rotation reads; pre-rotation tokens fail closed | Tier-3 | 1 | QA | |
| 12.4-INT-092 | R-10 — Consent per-channel per-purpose: opt-out propagates to projections; no-consent query degrades | Tier-3 | 1 | QA | |
| 12.4-INT-093 | R-10 — Processing restriction: restricted parties no-op on mutating commands; emit `PartyProcessingRestricted` | Tier-3 | 1 | QA | |
| 12.4-INT-094 | R-10 — Portability export: exported package excludes other tenants' data; only authorized fields | Tier-3 | 1 | QA | |
| 12.4-INT-095 | R-10 — Memories-backed search tenant-scoped; degrades cleanly when Memories unreachable | Tier-3 | 1 | QA | |
| 12.4-INT-096 | R-10 — Temporal name query returns historical name at given timestamp from EventStore stream | Tier-3 | 1 | QA | |
| 12.4-INT-097 | R-10 — Health/readiness reflects EventStore gateway availability vs. internal actor-host liveness independently | Tier-3 | 1 | QA | Per AC 12.4.4 + 12.10.6. |

**Total P1**: 38 scenarios

### P2 (Medium)

**Criteria**: Secondary risk (score 4-5), monitor without blocking.

| Test ID | Requirement / Risk | Test Level | Test Count | Owner | Notes |
|---------|--------------------|------------|------------|-------|-------|
| 12.2-FIT-100 | R-11 — `ArchitecturalFitnessTests` denies `[ApiController]`, `ControllerBase`, `MapControllers`, `AddMcpServer`, `WithToolsFromAssembly`, `MapMcp`, `[McpServerTool]` in `Hexalith.Parties` | Topology Fitness | 1 | Backend | PRESERVE per AC 12.2.5 / 12.4.5 / 12.10.4. |
| 12.5-INT-110 | R-12 — Capture-only command + query latency through EventStore gateway (p50, p95, p99) | Tier-3 | 1 | QA | New. Numeric threshold deferred until baseline. |
| 12.6-INT-111 | R-12 — MCP find/get/create tool round-trip latency under realistic payload sizes | Tier-3 | 1 | QA | New. Verify against any PRD MCP latency target. |
| 12.10-FIT-120 | R-13 — `AppHostTenantsTopologyTests` asserts startup dependency edges: `eventstore → admin → ui → tenants → parties → parties-mcp` | Topology Fitness | 1 | DevOps | Extends per AC 12.10.1. |
| 12.1-FIT-130 | R-14 — Topology scan for OIDC realm URL + audience + client consistency across `eventstore`, `eventstore-admin`, `parties`, `tenants` | Topology Fitness | 1 | DevOps | Per AC 12.1.5. |
| Trace-150 | R-15 — Traceability audit: every retired pre-pivot Tier-2/3 test has documented replacement; gaps flagged | Traceability | 1 | QA | Audit per AC 12.4.8. |
| 12.3-INT-160 | R-16 — Realistic auth scenarios: token expiry, missing tenant claim, malformed claims, audience mismatch, signature failure → forbidden/unauthorized at gateway, no Parties invocation | Tier-3 | 1 | Backend + QA | New. |
| 12.3-FIT-161 | R-16 — Source grep deny-list for spike-only literal tenant/actor values (e.g. `"spike-tenant"`, `"deterministic-actor-id"`) | Topology Fitness | 1 | Backend | New. |

**Total P2**: 8 scenarios + 1 traceability audit

### P3 (Low)

**Criteria**: Documented but lower priority.

| Test ID | Requirement / Risk | Test Level | Test Count | Owner | Notes |
|---------|--------------------|------------|------------|-------|-------|
| 12.6-UNIT-170 | R-17 — `Hexalith.Parties.Mcp` registers canonical tool names; `get_party_name_at` decision recorded | Tier-1 | 1 | Backend | Per AC 12.6.2 / 12.6.10. |
| 12.5-FIT-180 | R-18 — `Hexalith.Parties.Client` has no project reference to Server / Projections / DAPR / MediatR / FluentValidation / MVC | Topology Fitness | 1 | Backend | PRESERVE per AC 12.5.7. |
| 12.4-INT-190 | R-19 — Read paths through new gateway return crypto-shredded data after erasure; no decrypted-cache leaks | Tier-3 | 1 | QA | Spot-check existing `IntegrationTests/Security`. |
| 12.10-FIT-200 | R-20 — Plaintext-secret scan of AppHost output, launchSettings, `*.cs/*.yaml`: Keycloak client secrets, DAPR tokens, JWT signing keys | Topology Fitness | 1 | DevOps | Per AC 12.1.8 / 12.10.5 / 12.10.8. |

**Total P3**: 4 scenarios

---

## Downstream Gate — Epic 7 / Epic 8 Acceptance

Per `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`, Epic 7 stories 7.6 / 7.7 and Epic 8 stories 8.1 / 8.2 / 8.3 / 8.6 cannot be scheduled until the EventStore-fronted Parties client/gateway contract is *accepted*. This test design enforces a 1:1 mapping from each contract clause to at least one P0 or P1 test:

| Contract Clause | Test ID(s) |
|-----------------|------------|
| Typed query methods (admin browse / search / detail / picker typeahead / selected-display) | 12.5-UNIT-005, 12.4-INT-032, 12.4-INT-033, 12.4-INT-095, 12.4-INT-096 |
| Typed command methods for GDPR operations | 12.4-INT-090, 12.4-INT-091, 12.4-INT-092, 12.4-INT-093, 12.4-INT-094 |
| Capability detection (unavailable / partial / malformed / stale / tenant-switch) | 12.5-GTW-001, 12.6-GTW-002, 12.7-COMP-003, 12.8-COMP-004 |
| FrontComposer route support for `/admin/parties`, `/admin/parties/{partyId}`, `/admin/parties/{partyId}/gdpr` | 12.7-COMP-003, 12.7-COMP-040 (covered as Epic 7 enters) |
| Failure semantics (unauthorized / forbidden / not-found / gone / degraded / timeout / malformed / contract-unavailable) | 12.5-GTW-001, 12.6-GTW-002, 12.3-GTW-021, 12.3-GTW-022, 12.4-INT-097 |
| Boundary rules (no retired REST / admin / actor / projection-actor / search-service / controller calls) | 12.2-FIT-100, 12.5-FIT-180, 12.6-UNIT-170 |
| Privacy rules (tokens, tenant context, party data not in logs, telemetry, storage keys, URLs, filenames, callbacks) | 12.5-GTW-001, 12.6-GTW-002, 12.7-COMP-003, 12.8-COMP-004, 12.5-UNIT-005, 12.10-FIT-006, 12.10-FIT-007 |

**Gate rule:** the dependency status moves from `Required` to `Satisfied` only when all listed test IDs are green on Nightly for 5 consecutive runs. Otherwise it requires explicit `Risk Accepted` sign-off from product and architecture owners (per the 2026-05-17 dependency document).

---

## Execution Strategy

Single principle: **run everything in PRs unless infrastructure overhead makes it expensive.**

| Pipeline | Scope | Target Wall Time |
|----------|-------|-------------------|
| **PR** | Tier-1 (all) + Tier-2 (all) + Component (bUnit, all) + Topology Fitness (all) | ≤ 8 minutes |
| **Nightly** | PR scope + full Tier-3 Aspire topology suite + DeployValidation full manifest set + latency baseline capture (R-12) | ≤ 35 minutes |
| **Weekly** | Nightly scope + chaos / fault-injection scenarios (DAPR sidecar restart, EventStore pause, projection actor crash) | ≤ 90 minutes |

Rationale: Tier-3 Aspire topology spin-up dominates wall time (~10-30 s per test). Keeping it off PR keeps developer feedback fast while still gating before merge via Nightly. Chaos and fault-injection are too noisy for Nightly and live on Weekly.

---

## Resource Estimates

Net-new effort beyond what already exists post-pivot. Ranges expressed deliberately to avoid false precision.

| Priority | Scenario Count | Effort Range |
|----------|----------------|--------------|
| P0 (R-01 surface) | 7 | ~25-40 hours |
| P1 (R-02 to R-10) | 38 | ~60-90 hours |
| P2 (R-11 to R-16) | 8 + 1 traceability audit | ~15-25 hours |
| P3 (R-17 to R-20) | 4 | ~4-8 hours |
| **Total** | **57 + 1 audit** | **~105-165 hours (~3-5 weeks)** |

**Assumptions**:

- Tier-2 WebApplicationFactory boot harness and Tier-3 Aspire fixtures from Story 12.4 are in place and reusable. If any harness is partial, add ~15-25 hours.
- bUnit XSS payload helpers can be factored into `Hexalith.Parties.Testing` once and reused across AdminPortal and Picker tests.
- Topology fitness reflection scanners can share infrastructure across `FIT-006`, `FIT-007`, `FIT-042`, `FIT-043`, `FIT-200`.

### Prerequisites

**Test Data:**

- `PartyId`, `TenantId`, and encrypted-personal-data payload factories in `Hexalith.Parties.Testing`
- Malicious-payload library (XSS strings, oversized payloads, encoding edge cases) for R-05 component tests
- Multi-tenant fixtures (tenant A / tenant B) for R-03 / R-04 cross-tenant tests

**Tooling:**

- xUnit v3, Shouldly, NSubstitute (existing)
- bUnit (existing) for AdminPortal + Picker component tests
- Testcontainers + Aspire fixtures for Tier-3
- WebApplicationFactory wired against the new EventStore-fronted host for Tier-2
- Reflection / Roslyn-based fitness scanners (existing pattern in `FitnessTests/`)

**Environment:**

- Local Aspire run (`dotnet aspire run --project src/Hexalith.Parties.AppHost`) must succeed before Tier-3 work begins
- Keycloak realm + test users for tenant A / tenant B / admin / unauthorized role
- EventStore Admin UI accessible for evidence capture during Tier-3 failures

---

## Quality Gate Criteria

| Gate | Threshold |
|------|-----------|
| P0 pass rate | **100 %** — release blocked on any failure |
| P1 pass rate | **≥ 95 %** — investigate any flake immediately |
| P2 pass rate | **≥ 90 %** |
| P3 pass rate | Best effort |
| Tier-3 wall time | ≤ 35 s per test, p95 ≤ 60 s |
| Latency baseline (R-12) | Captured-only on first cycle; threshold set after 5 nightly runs |
| Epic 12 AC coverage | 100 % of Epic 12 AC mapped to at least one test in this design (verified during Step 5 traceability matrix) |
| Downstream gate (Epic 7/8) | All seven contract clauses backed by at least one P0/P1 test, all green on 5 consecutive Nightlies |

### Non-Negotiable

- [ ] R-01 (PII leakage) green at 100 % before any Epic 7/8 story enters `ready-for-dev`
- [ ] R-07 (rejection-event Apply ordering) fitness tests must never be removed or weakened
- [ ] No DAPR ACL deny-list (`*`, `/**`) waivers without architecture sign-off

---

## Mitigation Plans

Detailed plans for high-priority risks (score ≥ 6). Lower-priority risks are documented in the Risk Assessment table above.

### R-01 — PII / Token / Raw-Payload Leakage (Score 9)

**Strategy**:

1. Inventory every error-mapping site across `Hexalith.Parties.Client`, `*.Mcp`, `*.AdminPortal`, `*.Picker` and pair each with a sanitization assertion (12.5-GTW-001, 12.6-GTW-002, 12.7-COMP-003, 12.8-COMP-004).
2. Author a unit test (12.5-UNIT-005) confirming `PartiesClientException.ToString()` and structured-log payload omit `[PersonalData]` fields.
3. Add fitness scanners (12.10-FIT-006, 12.10-FIT-007) that grep source and DeployValidation output for forbidden patterns (raw `Authorization`, `Bearer`, command-payload variable names in log calls, plaintext token formats).
4. Wire the malicious-payload + token-leak helper into `Hexalith.Parties.Testing` so future stories reuse the same assertion library.

**Owner**: Backend + QA
**Timeline**: Before any Epic 7/8 story enters `ready-for-dev`
**Status**: Planned
**Verification**: All P0 tests green; manual log review of one full Nightly run; sign-off from architecture.

### R-02 — Validation Bypass (Score 6)

**Strategy**:

1. Tier-1 invoker pipeline test (12.3-UNIT-010) parameterized across every Parties command type to confirm validator runs before `PartyAggregate.Handle`.
2. Tier-2 gateway test (12.3-GTW-011) asserting platform validation envelope shape and zero-event persistence.
3. Tier-3 end-to-end (12.3-INT-012) checking EventStore stream + projection + pub/sub all show zero side-effects.
4. Fitness (12.3-FIT-013) ensuring every command in `Hexalith.Parties.Contracts` is wired to at least one validator via DI.

**Owner**: Backend
**Timeline**: Before Epic 7/8 scheduling
**Verification**: All R-02 test IDs green; manual review of validator DI registration.

### R-03 — Tenant Authorization Regression (Score 6)

**Strategy**:

1. Fitness test (12.3-FIT-020) banning `ITenantAccessService` / `TenantAccessDenialTranslator` references from command/query request paths.
2. Tier-2 gateway tests (12.3-GTW-021, 12.3-GTW-022) for unauthorized tenant + unauthorized role rejection at the gateway, never reaching the actor.
3. Tier-3 cross-tenant integration (12.3-INT-023) confirming no event persistence on the forbidden path.
4. Tier-3 actor-id forgery test (12.3-INT-024) preserving `AggregateActor` tenant-mismatch guard.

**Owner**: Backend
**Timeline**: Before Epic 7/8 scheduling
**Verification**: Fitness scan clean; gateway tests reject pre-invocation; `AggregateActor` test confirms tenant header is authoritative.

### R-04 — Cross-Tenant Projection Read Leak (Score 6)

**Strategy**:

1. Tier-1 fail-closed unit tests (12.4-UNIT-030, 12.4-UNIT-031) on both detail and index projection handlers.
2. Tier-3 search-under-rebuild (12.4-INT-032) confirming degraded response, never cross-tenant entries.
3. Tier-3 admin-browse-with-missing-tenant-state (12.4-INT-033) confirming bounded empty / degraded state.

**Owner**: Backend + QA
**Timeline**: Before Epic 7/8 scheduling
**Verification**: All R-04 tests green; manual review confirms `null` / missing tenant context never returns raw projection data.

### R-05 — XSS / Encoding Bypass (Score 6)

**Strategy**:

1. bUnit XSS regression tests (12.7-COMP-040, 12.8-COMP-041) using the malicious-payload helper across display names, GDPR details, ProblemDetails text.
2. Fitness scans of `.razor` source (12.7-FIT-042, 12.8-FIT-043) for forbidden render patterns.

**Owner**: Frontend + QA
**Timeline**: Before Epic 7 (AdminPortal) and Epic 8 (Picker) ramp
**Verification**: bUnit assertions confirm encoded text; fitness scan clean.

### R-06 — DAPR Access-Control YAML Drift (Score 6)

**Strategy**:

1. Extend `AppHostDaprTopologyValidationTests` (12.10-FIT-050) to assert each receiving sidecar's correct ACL file, deny-by-default posture, and explicit caller matrix.
2. Add wildcard deny-list (12.10-FIT-051) banning `*` app-id and `/**` path.
3. Confirm `accesscontrol.parties.yaml` is the only invocation policy referenced by the Parties actor sidecar (12.10-FIT-052).

**Owner**: DevOps
**Timeline**: Continuous
**Verification**: Fitness suite green; manual YAML diff review on any ACL change.

### R-07 — Rejection-Event Apply Ordering (Score 6)

**Strategy**:

1. PRESERVE the existing `Server.Tests/Aggregates` test asserting source-order of rejection-event Apply overloads (12.2-UNIT-060).
2. PRESERVE the existing reflection-based fitness ensuring every `IRejectionEvent` Apply is a concrete instance no-op (12.2-UNIT-061).
3. PRESERVE the rehydration replay test (12.2-UNIT-062).
4. Codify: any PR that touches `PartyState.cs` requires manual review of these tests.

**Owner**: Backend
**Timeline**: Continuous
**Status**: Mitigation already in place — risk is *regression prevention*, not new coverage.
**Verification**: Tests green on every PR; CODEOWNERS or review automation enforces manual review on `PartyState.cs` changes.

### R-08 — Deployment Validation Gap (Score 6)

**Strategy**:

1. Extend `TenantsDeploymentValidationTests` + `DeploymentValidationTests` (12.10-FIT-070, 12.10-FIT-071) for all five canonical services + state-store + pub/sub config.
2. Distinguish gateway readiness from internal liveness in topology fitness (12.10-FIT-072).

**Owner**: DevOps
**Timeline**: Before Epic 7/8 scheduling
**Verification**: Run validation against a deliberately broken manifest (CI dry-run) and confirm rejection.

### R-09 — Composite Command Guard Bypass (Score 6)

**Strategy**:

1. PRESERVE Tier-1 composite-aggregate tests (12.3-UNIT-080).
2. New Tier-2 gateway test (12.3-GTW-081) asserting applied/skipped/rejected outcomes and emitted event order through EventStore.
3. New Tier-3 (12.3-INT-082) asserting durability and full-atomicity event order in the stream.

**Owner**: Backend
**Timeline**: Before Epic 7/8 scheduling
**Verification**: All composite-related tests green; manual review of guard precedence (erasure → restriction → duplicate → no-op).

### R-10 — GDPR / Memories Scenario Coverage Retention (Score 6)

**Strategy**:

1. Build a coverage traceability matrix mapping each AC 12.4.4 scenario (erasure, key lifecycle, consent, restriction, portability, encryption, temporal name, tenant isolation, health/readiness) to a Tier-3 test ID.
2. Extend `IntegrationTests/Security` and `IntegrationTests/Tenants` to fill any gap (12.4-INT-090 through 12.4-INT-097).

**Owner**: QA (with Backend support on test fixtures)
**Timeline**: Before Epic 7/8 scheduling
**Verification**: Traceability matrix shows ≥ 1 test per AC 12.4.4 scenario; all green on Nightly.

---

## Assumptions and Dependencies

### Assumptions

1. The Story 12.4 Tier-1 / Tier-2 test harness is in place, green, and idiomatic. If it isn't, this design's resource estimates need to grow by ~15-25 hours for harness work.
2. EventStore submodule remains a sibling dependency that Parties must not edit. Any fix that crosses that line becomes a platform ticket, not a Parties test item.
3. The Story 12.0 spike conclusion's working domain/app-id convention (`party` vs. `parties` vs. `Parties`) is final. Tests assert one canonical convention only.
4. `Hexalith.Parties.Client` is the consumer-facing typed surface. MCP host and Admin Portal both use it; the test design treats it as the single contract under verification.
5. Test runs assume xUnit v3 cancellation-token plumbing (`TestContext.Current.CancellationToken`) for any new async test; existing `xUnit1051` debt is acknowledged.

### Dependencies

1. **Tier-3 Aspire fixtures from Story 12.4** — required before P1 / P2 Tier-3 tests can be authored.
2. **Malicious-payload helper in `Hexalith.Parties.Testing`** — required before R-05 component tests (estimated half a day of net-new work).
3. **Keycloak local test realm with tenant A / tenant B / admin / no-role users** — required before R-03 / R-14 / R-16 scenarios.
4. **Latency capture infrastructure (R-12)** — Tier-3 baseline capture requires a side-channel (e.g. structured-log scraping or dedicated CI artifact). Decide before P2 work starts.

### Risks to Plan

- **Risk**: Story 12.4 harness has unrecorded gaps that surface during P1 authoring.
  - **Impact**: +15-25 hours, schedule slip into Epic 7/8 prep window.
  - **Contingency**: Reserve a half-week buffer; treat harness gaps as a separate housekeeping ticket if uncovered.
- **Risk**: PRD has no explicit MCP latency SLO, so R-12 baseline cannot be evaluated against a target.
  - **Impact**: R-12 stays in capture-only mode indefinitely; latency regressions go undetected unless someone reads the baseline.
  - **Contingency**: Schedule an NFR follow-up (talk to PM John) to set MCP and gateway latency SLOs based on captured baselines after 5 Nightly runs.

---

## Interworking & Regression

| Service / Component | Impact | Regression Scope |
|---------------------|--------|------------------|
| **`Hexalith.EventStore`** (submodule) | Pivot routes all command/query traffic through EventStore's `POST /api/v1/commands` and `/api/v1/queries` | EventStore-side gateway tests remain platform-owned; Parties tests verify integration boundary only |
| **`Hexalith.Tenants`** (submodule) | Parties consumes Tenants events for access-state projections | Existing `IntegrationTests/Tenants` must remain green; tenant event-consumption path is unchanged |
| **`Hexalith.FrontComposer`** (submodule) | Admin Portal rebuilt on FrontComposer shell | Coverage limited to Parties domain manifest registration; FrontComposer internals are platform-owned |
| **`Hexalith.Memories`** (submodule) | Memories-backed search remains a Parties capability via EventStore query path | `IntegrationTests/Search` (Memories scenarios) must remain green |
| **`Hexalith.Parties.Mcp`** (new) | Net-new project; thin MCP host calling `Hexalith.Parties.Client` only | New `Hexalith.Parties.Mcp.Tests` covers canonical tool surface; old in-process MCP tests retired |
| **`Hexalith.Parties.AdminPortal`** (rebuilt) | Consolidates Stories 10.1 / 10.1.1 / 10.2 | bUnit components + Tier-3 admin browse / GDPR scenarios |
| **`Hexalith.Parties.Picker`** (rewritten) | All reads via `IPartiesQueryClient`; no REST/actor/search-service calls remain | bUnit components + Picker integration tests |

---

## Follow-on Workflows (Manual)

- Run `bmad-testarch-atdd` to draft failing P0 tests against the EventStore gateway and PII-leakage surface (separate workflow; not auto-run).
- Run `bmad-testarch-automate` once Story 12.4 harness is confirmed complete, to generate the P1 implementation skeletons in parallel.
- Run `bmad-testarch-trace` after P0/P1 are green to issue the formal quality gate decision against Epic 12 AC and the downstream Epic 7/8 contract gate.
- Run `bmad-testarch-nfr` to formalise MCP and gateway latency SLOs once 5 Nightly latency baselines exist.

---

## Open Assumptions Flagged for Resolution

| Item | Why It Matters | Suggested Resolution |
|------|----------------|----------------------|
| MCP / gateway latency SLO | R-12 stays in capture-only without numeric thresholds; latency drift undetectable | NFR workshop with PM after 5 Nightly baselines |
| `get_party_name_at` MCP tool fate (AC 12.6.10) | If silently dropped, AI consumers lose temporal-query capability | Product decision recorded in a dated note; R-17 P3 test references the decision |
| `tea_use_playwright_utils=true` and `tea_use_pactjs_utils=true` in config | JS-stack flags on a .NET project mislead future agent runs | One-line config update to `_bmad/tea/config.yaml` after this design lands |
| Epic 12 missing from `sprint-status.yaml` `development_status` | Rollup inconsistency; stories `done` but epic invisible | Append `epic-12` block in the next sprint-status housekeeping pass |

---

## Approval

**Test Design Approved By:**

- [ ] Product Manager: _____ Date: ______
- [ ] Tech Lead: _____ Date: ______
- [ ] QA Lead: _____ Date: ______

**Comments:**

---

## Appendix

### Knowledge Base References

- `risk-governance.md` — Risk classification framework, gate decision engine
- `probability-impact.md` — Probability/impact 1-9 scoring scale
- `test-levels-framework.md` — Test level selection (Unit / Integration / E2E)
- `test-priorities-matrix.md` — P0-P3 criteria and coverage targets
- `confidence-gate.md` — Stop-and-ask discipline for risk classification

### Related Documents

- **Epic source**: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md`
- **Downstream gate**: `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`
- **Project rules**: `_bmad-output/project-context.md`
- **Sprint status**: `_bmad-output/implementation-artifacts/sprint-status.yaml`
- **Story files**: `_bmad-output/implementation-artifacts/12-0..12-10-*.md`
- **PRD**: `_bmad-output/planning-artifacts/prd.md`
- **Architecture**: `_bmad-output/planning-artifacts/architecture.md`
- **Working notes**: `_bmad-output/test-artifacts/test-design-progress.md`

---

**Generated by**: BMad TEA Agent — Master Test Architect (Murat)
**Workflow**: `bmad-testarch-test-design`
**Version**: 4.0 (BMad v6)
