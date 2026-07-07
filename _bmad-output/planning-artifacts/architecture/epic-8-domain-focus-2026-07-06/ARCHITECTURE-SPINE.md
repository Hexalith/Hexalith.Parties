---
title: Epic 8 Architecture Spine (Reconciliation)
epic: 8
date: 2026-07-07
status: approved-reconciled
classification: post-MVP maintenance (Class C) — zero new PRD FRs
closes-blocker: "Story 8.1 preserved 'missing Epic 8 architecture spine' blocker"
related:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-06.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md
  - _bmad-output/implementation-artifacts/epic-8-context.md
  - _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md
---

# Epic 8 Architecture Spine — Domain-Focus Refactoring & Platform Extraction

## 1. Purpose & Reconciliation Statement

The 2026-07-06 change proposal that created Epic 8 reserved this path for an
architecture spine and made it a prerequisite for the deletion-heavy migration
stories. The spine document was never authored; Story 8.1 correctly *preserved*
"missing Epic 8 architecture spine" as an open blocker, yet Stories 8.2–8.5
shipped against `spec-8-x` files and landed with parity evidence.

This document reconciles that deviation. It does **not** re-derive the design
from scratch — it **ratifies** the artifacts that already carry the spine's
substance and adds the missing piece the readiness assessment asked for: an
explicit invariant set and a per-story readiness gate for the remaining work.

**Authoritative spine artifact set (read together):**
- This document — invariants + readiness gate + blocker closure.
- `epic-8-context.md` — goal, requirements/constraints, technical decisions,
  UX conformance rules, cross-story dependencies.
- Epic 7 spine (`…/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`)
  — the platform-adoption boundary Epic 8 continues from.
- Landed specs 8.1–8.5 (esp. 8.3's platform-API prerequisite matrix) — the
  approved, evidenced starting state.

**Ratification of 8.2–8.5:** accepted as done. The readiness report (2026-07-07)
records them as done with parity evidence; each was zero-risk hygiene (8.2),
additive platform prerequisites (8.3), leaf-project retirement (8.4), or an
SDK host cutover proven by focused + topology tests (8.5). No rollback of
completed work is warranted (Correct Course §4.2 = not viable).

## 2. Target End-State — Domain-Module Contract

Parties conforms to the Hexalith domain-module contract: it keeps domain
substance and sheds reusable platform mechanics.

| Parties KEEPS (domain) | MOVES to platform owner |
|---|---|
| Aggregates, contracts, validators | Service defaults, correlation/ProblemDetails → Commons |
| Projection/query **semantics** (folds, tenant guardrails) | Projection/query **mechanics** (actors, rebuild, cursor codec) → EventStore SDK |
| GDPR **policy** + legal semantics | Generic crypto/key-management engine → EventStore/shared DataProtection |
| Typed domain clients, domain UI, MCP tool **definitions** | Command envelopes, paging/freshness models, MCP plumbing → owning modules |
| Domain samples, **thin** AppHost | Build-root probing, AppHost security/module helpers, platform deploy assets → Builds / platform-ops |

## 3. Invariants — must hold across every remaining migration (8.6–8.10)

**Boundary**
- I1. No public API on the actor host. Traffic enters via the EventStore
  gateway → `POST /process` over DAPR; ACL stays deny-by-default. Migration
  must never add public controllers/endpoints.
- I2. Host target is the EventStore SDK shape (`AddEventStoreDomainService` /
  `UseEventStoreDomainService`); Parties retains only domain registrations,
  Parties-specific policy, and payload-protection hooks the SDK cannot own.

**Deletion-safety**
- I3. Local rollback paths (projection, query, crypto, release recovery) stay
  in place until the replacement API has **parity evidence** and proven
  rollback. `catch (NotImplementedException)` remoting control flow is deleted
  only after parity.
- I4. No Parties source migration starts from an unapproved/checked-out
  submodule API. Every prerequisite is additive or proven-already-available
  (the 8.3 matrix).

**Behavior preservation (stable or intentionally versioned)**
- I5. Public package contracts: `Client` + `Contracts` public shape and the
  three UI RCLs (`Picker`/`AdminPortal`/`ConsumerPortal`).
- I6. Command/query behavior; self-scoped consumer authorization incl.
  `aggregateId == party_id` defense in depth.
- I7. GDPR legal semantics: consent ≠ lawful basis; Art.18 restriction guards
  (consent edits allowed while restricted, rejected while erasure in progress);
  two-front-door erasure + cross-submodule verification (D7).
- I8. Protected-payload compatibility: `json+pdenc-v1`, `json-redacted`, legacy
  unprotected reads, key zeroing, typed-unreadable outcomes, no-leak
  diagnostics, Art.20 exports, Art.30 processing records, erasure
  reports/certificates.

**Projection/query (survive the SDK migration)**
- I9. Replay-from-zero on every delivery; per-actor sequence checkpoints +
  set-based idempotency; duplicate/out-of-order tolerance.
- I10. Stale/degraded reads render last-known (never throw on staleness);
  `ProjectionFreshnessMetadata` on every read; erased parties excluded from the
  index. Target abstractions: `IDomainProjectionHandler`, `IDomainQueryHandler`,
  `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`. A full rebuild
  is executed and verified against aggregate replay before local code deletion.

**Identifier, build, UI, GDPR copy, scope**
- I11. Stop rejecting valid ULID-compatible aggregate IDs; retain replay compat
  for GUID-shaped IDs; use Commons unique-ID helpers where semantics require.
- I12. Build discipline unchanged: .NET 10, `.slnx` only, CPM, warnings-as-
  errors, xUnit v3 / Shouldly / NSubstitute / bUnit, Playwright a11y where UI
  is touched, root submodules only, MinVer.
- I13. UI: Fluent 2 inheritance; purge FAST/v4 tokens; teal accent non-text
  only, filled actions bind AA-safe brand background; WCAG 2.2 AA contracts
  (keyboard/pointer parity, skip links, focus rings, forced-colors,
  reduced-motion, semantic controls, typed destructive confirmation,
  polite/assertive live-region split, no focus-stealing on optimistic updates).
- I14. GDPR copy honesty: no consent dark patterns, no over-promised export
  latency, cancellation-vs-permanence distinction, stale reads show last-known.
- I15. Scope: Epic 8 adds **zero** PRD functional requirements and must never be
  reported as MVP feature delivery.

## 4. Remaining-Work Readiness Gate (mandatory for specs 8.6–8.10)

Each `spec-8-x` (8.6–8.10) is **not** ready for a dev session until its spec
file declares all six, in the spec itself:

1. **Prerequisites** — which 8.3 platform APIs must be landed + owner-approved,
   and which prior stories must be done.
2. **Touched repos/submodules** — Parties + each of EventStore / Commons /
   FrontComposer / Builds / `deploy` that the change edits.
3. **Rollback path** — which local code stays until parity, and how to revert.
4. **Validation lanes** — the specific xUnit v3 assemblies (run directly, not
   `dotnet test --filter`), topology, deploy, and `ui-a11y` lanes, plus the
   **parity evidence** required before any deletion.
5. **Non-goals** — explicit out-of-scope and what must **not** be deleted yet.
6. **Parity-evidence checklist** — the I5–I10/I8 items relevant to that story.

Broad cross-module stories (8.6 projection/query, 8.7 data-protection,
8.8 client/MCP/AppHost/build/deploy) MUST additionally be split or hard-gated
at spec-creation time per readiness Major-issue #3.

## 5. Sequencing & Dependencies

`8.1 → 8.2 → 8.3 → 8.4 → 8.5 → 8.6 → 8.7 → 8.8 → 8.9 → 8.10` (unchanged).
Stories 8.5–8.7 depend on 8.3 platform-API readiness. 8.10 runs last and closes
or explicitly defers remaining work with owners, proof, rollback, and evidence.

## 6. Blocker Closure

**Epic 8 architecture spine — APPROVED (reconciled), 2026-07-07.** Story 8.1's
preserved "missing Epic 8 architecture spine" blocker is **CLOSED for planning
purposes**. Remaining deletion-heavy migrations (8.6–8.10) are henceforth gated
by §4 (per-spec readiness gate) rather than by the absence of this document.
