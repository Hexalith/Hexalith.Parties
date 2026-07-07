---
title: Sprint Change Proposal — Epic 8 Platform-API Prerequisite Routing to Owners
date: 2026-07-07
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
trigger: >
  implementation-readiness-report-2026-07-07 §"Recommended Next Steps" +
  sprint-change-proposal-2026-07-07 §5 handoff (open PM/Architect duty:
  confirm 8.3 platform-API owners/readiness before 8.6/8.7 spec dev).
status: approved
approved: 2026-07-07T19:25:05+02:00
related:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/spec-8-3-platform-api-prerequisites.md
  - _bmad-output/implementation-artifacts/epic-8-context.md
---

# Sprint Change Proposal — Epic 8 Platform-API Prerequisite Routing to Owners

## 1. Issue Summary

The approved 2026-07-07 reconciliation proposal
(`sprint-change-proposal-2026-07-07.md`) closed the "missing Epic 8 architecture
spine" blocker and gated Stories 8.6–8.10 behind the spine's §4 per-spec
readiness gate. It left exactly **one** explicit remaining PM/Architect duty in
its §5 handoff:

> "Remaining duty: confirm platform-API owners/readiness (8.3 matrix) before
> 8.6/8.7 spec dev."

**This proposal discharges that duty.** Story 8.3 (done) produced an
evidence-and-no-migration matrix
(`story-8-3-platform-api-prerequisite-matrix.md`) that maps every platform
surface Epic 8 wants to delete-and-adopt to an owner repo, a status, the proof
required, and the dependent Epic 8 stories. The matrix records **what** each
owner must deliver — but it does **not** route or track that owner work. Without
an explicit routing, Stories 8.6–8.10 cannot legitimately begin: the matrix's
own Review Gate forbids any Parties source migration for a row until owner
evidence, release/pin proof, rollback, and validation are recorded in that row.

Precisely stated:

- The deletion-heavy migrations (8.6–8.10) are blocked on **owner-side delivery**
  in five external repos (EventStore / Commons / Tenants / FrontComposer /
  Builds), not on any Parties work.
- The matrix carries a **single `blocked` row — G12 Package publishing /
  source-mode CI** — which needs an owner *decision* (publish packages vs. bless
  source-mode CI), hard-blocks Stories **8.8 and 8.10** plus the package-mode
  build, and has the longest lead time. It therefore leads the routing.

### Evidence (repo-verified 2026-07-07)

From the 8.3 matrix `<!-- platform-api-prerequisite-matrix -->` block, by status:

| Status | Rows | Notes |
|---|---|---|
| `blocked` | **1** — Package publishing/source-mode CI (G12) | Owners Commons + Tenants; only unblockable by an owner release/CI decision |
| `needs-additive-api` | **7** — projection/query SDK (G3/G10/G6); degraded response + DAPR health (G1/G2); payload-protection engine (G5); client envelopes/freshness/error codes (G6); tenant claims (G7/G9); Aspire publish helpers (G8); FrontComposer UI primitives (G4); MCP/deep-link/search probes (G11) | Owner additive/approved API required before any Parties deletion |
| `available` | **4** — EventStore domain-service host (8.5-proven); EventStore DataProtection; Commons HTTP helpers; Builds shared props/targets | Source exists; only release or submodule-pin validation needed at consumption |

(Row count is 12 surfaces; the seven `needs-additive-api` surfaces span EventStore
and FrontComposer, several co-owned with Commons.)

### Change-Analysis Checklist Status

| # | Item | Status |
|---|---|---|
| 1.1–1.3 | Trigger, core problem, evidence | ✅ Done |
| 2.1 | Epic 8 completable as planned? | ❗ Only after owner prerequisites land + are proven in the matrix |
| 2.2 | Epic-level change | ✅ Modify Epic 8 tracking only (no new/removed epic) |
| 2.3–2.4 | Other/future epics affected | ✅ N/A (Epics 1–7 done; Epic 8 only) |
| 2.5 | Resequencing | ✅ None — `8.1→8.10` preserved; G12 sequenced *first among owner handoffs* |
| 3.1 | PRD conflict | ✅ None — no FR change |
| 3.2 | Architecture | ✅ None — spine + §4 gate already govern 8.6–8.10 |
| 3.3 | UX conflict | ✅ N/A — Epic 8 is conformance, no new UX |
| 3.4 | Other artifacts | ❗→✅ `sprint-status.yaml` action_items add the owner routing |
| 4.1 | Direct Adjustment (route-and-track) | ✅ **Viable — selected** |
| 4.2 | Rollback | ⛔ N/A — nothing to roll back |
| 4.3 | MVP review | ✅ N/A — MVP (Epics 1–5) complete/unaffected |
| 4.4 | Path selected | ✅ Direct Adjustment (route-and-track, G12-first) |

## 2. Impact Analysis

### Epic Impact
- **Epic 8 only.** Epics 1–7 done and unaffected. No epic added, removed, or
  re-sequenced. `8.1→8.10` order unchanged. Class C maintenance classification
  (zero new PRD FRs) preserved.

### Story Impact
- **8.1–8.5:** done. Unaffected.
- **8.6–8.10:** stay `backlog`. This proposal adds no new gate — it makes the
  *existing* §4/matrix prerequisite an explicitly **owned and tracked** handoff.
  Each remaining story stays blocked until its upstream owner row is delivered
  and the proof is recorded in the matrix.
  - 8.6 waits on EventStore projection/query (G3/G10/G6) + DataProtection.
  - 8.7 waits on EventStore payload-protection engine package (G5).
  - 8.8 waits on **G12** + client envelopes (G6) + Aspire (G8) + Commons HTTP +
    Builds + MCP/search (G11).
  - 8.9 waits on FrontComposer UI primitives (G4).
  - 8.10 closes/defers whatever remains with owner proof.

### Artifact Conflicts (resolved by this proposal)
- **`sprint-status.yaml`:** no owner-routing tracking for the 8.3 prerequisites →
  add Epic 8 `action_items` (G12 first) that name each owner and dependent story.
- **8.3 matrix:** **untouched.** It is a done Story 8.3 artifact and the
  fitness-guarded (`PlatformApiPrerequisitesTests`) evidence source; routing is
  recorded here + in `action_items`, not by rewriting the matrix.
- **spine / `epics.md` / `epic-8-context.md`:** unchanged — they already gate
  8.6–8.10 via §4; no edit needed.
- **PRD / UX:** no conflict, no change.

### Technical Impact
- **No Parties code change. No submodule edits.** The 8.3 spec's Block-If
  explicitly forbids editing EventStore/Commons/FrontComposer/Builds/Tenants
  source or consuming a not-yet-approved API from this repo.
- Owner deliverables land in the **external owner repos**, outside this repo's
  authority. This proposal produces the *routing + tracking*; filing the actual
  owner tickets/PRs in those repos is downstream follow-through (see §5).
- Local rollback paths (projection, query, crypto, release recovery, HTTP
  helpers, AppHost wiring, UI primitives) **stay in place** until each owner API
  proves parity — per matrix rollback columns and spine invariants I3/I4.

## 3. Recommended Approach

**Selected: Option 1 — Direct Adjustment (route-and-track, G12-first).**

Register each 8.3 matrix prerequisite as a tracked owner handoff in
`sprint-status.yaml`, sequenced so **G12 leads**, then the `needs-additive-api`
owner work (grouped by owner repo), then the lighter `available` release/pin
validation.

**Rationale for G12-first:**
- It is the **only `blocked` row** — the matrix cannot even name enough proof
  detail for migration planning until Commons/Tenants owners decide.
- It requires an owner **decision**, not just code: publish the missing packages
  (`Hexalith.Commons.Http`, `Hexalith.Commons.ServiceDefaults`,
  `Hexalith.Tenants.Client`, `Hexalith.Tenants.Testing`) **or** bless source-mode
  CI. That decision has the **longest lead time**.
- It **hard-blocks Story 8.8 and 8.10** and is entangled with the Story 8.1
  package-mode build blocker. Everything else is either owner API work that can
  proceed in parallel or is already `available`.

Rollback (§4.2) is N/A — nothing to revert. MVP review (§4.3) is N/A — Epics 1–5
are complete and untouched.

- **Effort:** Low (planning + tracking; no code).
- **Risk:** Low for this proposal. Residual delivery risk stays owned by the §4
  gate + owner delivery, contained by the matrix rollback columns.
- **Timeline impact:** None to the MVP. Unblocks 8.6/8.7 **spec** creation as
  soon as owners confirm their rows; unblocks 8.6–8.10 **dev** only as each owner
  prerequisite lands with proof.

## 4. Detailed Change Proposals

### 4.1 Owner routing table (G12-first)

Each surface below is routed to its owning repo. "Dep." = dependent Epic 8
stories from the matrix. Parties files no owner code; it consumes only after the
owner deliverable lands and proof is recorded in the matrix row.

| # | Surface (gap) | Owner repo(s) | Status | Dep. | Owner must deliver | Proof gate before Parties deletes local code |
|---|---|---|---|---|---|---|
| **1** | **Package publishing / source-mode CI (G12)** | **Hexalith.Commons + Hexalith.Tenants** | **blocked** | **8.8, 8.10** | **Decision + action:** publish `Hexalith.Commons.Http`, `Hexalith.Commons.ServiceDefaults`, `Hexalith.Tenants.Client`, `Hexalith.Tenants.Testing`, **or** bless source-mode CI in the Commons/Tenants release pipelines | Published package versions **or** recorded source-mode CI blessing; resolves the Story 8.1 package-mode build blocker |
| 2 | Projection/query SDK (G3 erasure hooks, G10 index batching, G6 freshness) | Hexalith.EventStore | needs-additive-api | 8.6, 8.10 | Additive/approved parity for read-model erasure hooks, index batching, Parties freshness mapping, duplicate/out-of-order replay, full-rebuild verification, cursor scope | Parity evidence + rollback recorded; local projection/query actors + rebuild stay until proven |
| 3 | Payload-protection engine package (G5) | Hexalith.EventStore | needs-additive-api | 8.7, 8.10 | Additive **shared payload-protection engine** package (not ASP.NET Data Protection naming): `pdenc-v2` AAD binding + `json+pdenc-v1` read, `IPersonalDataPolicy`, `IErasureStateProvider`, key storage/wrapping/rotation/audit/retry/circuit-breaker, typed-unreadable outcomes, moved golden compatibility harnesses | Golden-harness parity + rollback; `Hexalith.Parties.Security` stays until proven |
| 4 | Client envelopes / freshness / error codes (G6) | Hexalith.EventStore | needs-additive-api | 8.6, 8.8, 8.9, 8.10 | Additive/approved adapter for `Current/Stale/Rebuilding/Degraded/Unavailable/LocalOnly` freshness, warning codes, ProblemDetails reason mapping, typed command/query outcomes | Compatibility evidence; local envelopes/paging/freshness stay until proven |
| 5 | Degraded response + DAPR health checks (G1/G2) | Hexalith.EventStore | needs-additive-api | 8.8, 8.10 | Additive degraded-response middleware + `AddEventStoreDaprHealthChecks` parity (status-header behavior, tag policy) | Parity evidence; local middleware + DAPR health checks stay until proven |
| 6 | Aspire publish helpers (G8) | Hexalith.EventStore.Aspire + AppHost owners | needs-additive-api | 8.5, 8.8, 8.10 | `WithEventStoreJwtAuthentication(audience)` (or documented `WithJwtBearerSecurity(…, audience)` replacement) + granular typed-client registration; **confirm the Parties AppHost topology/deploy owner** | Topology + publish/deploy parity; local AppHost wiring stays until proven |
| 7 | Tenant claims transformation (G7/G9) | Hexalith.EventStore + Hexalith.Commons | needs-additive-api | 8.4, 8.8, 8.10 | **Architecture-owner decision** that EventStore owns the reusable claims transformation + public `eventstore:tenant` constant; additive `AggregateIdentity.IsValid(string)` (EventStore.Contracts) + `UniqueIdHelper.IsValidUlid(string)` (Commons) predicates — **or an explicit redirect** | Owner approval recorded; `Hexalith.Parties.Authentication` stays until an approved shared surface exists |
| 8 | FrontComposer UI primitives (G4) | Hexalith.FrontComposer | needs-additive-api | 8.8, 8.9, 8.10 | Additive/approved `FcEntityPicker<T>`, per-record freshness indicator, live-region politeness, file/JSON download service, typed-name destructive-dialog mode, skip links (WCAG 2.2 AA / ARIA parity per spine I13) | UX/ARIA parity evidence; local UI primitives stay until proven |
| 9 | MCP / deep-link / search probes (G11) | Hexalith.FrontComposer + Hexalith.Commons | needs-additive-api | 8.8, 8.10 | MCP auth + tenant header relay handler, EventStore Admin UI deep-link builder, search-capability health probe | Parity evidence; local MCP/search plumbing stays until proven |
| 10 | EventStore domain-service host | Hexalith.EventStore | available *(8.5-proven)* | 8.4, 8.5, 8.10 | Release or submodule-pin availability of the SDK host shape (already exercised by Story 8.5) | Recorded release/pin at consumption |
| 11 | EventStore DataProtection | Hexalith.EventStore | available | 8.6, 8.10 | Release or submodule-pin availability; cursor purpose stability + DAPR key-ring persistence validation | Recorded release/pin at consumption |
| 12 | Commons HTTP helpers | Hexalith.Commons | available | 8.8, 8.10 | Release or submodule-pin availability; endpoint/bounded-ProblemDetails/correlation behavior validation | Recorded release/pin at consumption |
| 13 | Builds shared props/targets | Hexalith.Builds | available | 8.8, 8.10 | Release or submodule-pin availability; centralized versions, source/package mode, CI/release templates, no-warning-override gates | Recorded release/pin at consumption |

### 4.2 Priority item — G12 owner brief (route first)

**Ask (to Commons + Tenants release owners):** Resolve the single `blocked` 8.3
row by either **(a)** publishing the four packages
(`Hexalith.Commons.Http`, `Hexalith.Commons.ServiceDefaults`,
`Hexalith.Tenants.Client`, `Hexalith.Tenants.Testing`) through the existing
Commons/Tenants `semantic-release` pipelines, **or (b)** recording an explicit
source-mode-CI blessing as the approved package strategy.

**Why first:** only `blocked` row; needs an owner decision with the longest lead
time; hard-blocks Stories 8.8 and 8.10 and is entangled with the Story 8.1
package-mode build blocker; all other rows are parallelizable or already
`available`.

**Evidence pointers (read-only, in-repo):**
`references/Hexalith.Commons/package.json`,
`references/Hexalith.Tenants/package.json`,
`references/Hexalith.Tenants/release.config.cjs`,
`references/Hexalith.Commons/src/libraries/Hexalith.Commons.ServiceDefaults/Directory.Build.props`.

**Definition of done for G12:** published package versions (or a recorded
source-mode blessing) exist; the Story 8.1 package-mode/default-build blocker in
the matrix "Residual Blockers Preserved" list can be closed or explicitly
re-scoped; the matrix G12 row is updated by the consuming story (8.8) with the
release/CI proof before any migration starts.

### 4.3 `sprint-status.yaml` change (applied with this proposal)

Add six Epic 8 owner-routing `action_items` (G12 first) and bump `last_updated`.
Full YAML is applied in the same session as this proposal; the story rows and
epic status are **unchanged** (no re-sequencing).

## 5. Implementation Handoff

**Scope classification: Moderate.** Backlog/tracking reorganization plus
cross-repo owner coordination — no fundamental replan, no code.

| Recipient | Responsibility |
|---|---|
| PM / Architect (Winston) | Own the routing. File/confirm the owner handoffs in the five external repos, **G12 first**. Make the two owner *decisions* that only the architecture owner can make: G7/G9 tenant-claims ownership, and the G8 AppHost topology/deploy owner. Track each to closure in `action_items`. |
| Platform owners (EventStore / Commons / Tenants / FrontComposer / Builds) | Deliver their rows: G12 publish/bless decision (Commons+Tenants); additive APIs (EventStore G1/G2/G3/G5/G6/G8/G10, FrontComposer G4/G11); release/pin availability for the four `available` rows. |
| Product Owner / Developer (Alice / Amelia) | Do **not** start 8.6–8.10 dev until the upstream owner row is delivered and proof is recorded in the 8.3 matrix. Create specs 8.6–8.10 **in order**, each satisfying the spine §4 gate; split/hard-gate broad 8.6/8.7/8.8. |
| Tech Writer (Paige) | Keep PRD/epics/readiness docs explicit that Epics 6–8 are maintenance with no new PRD FR coverage (open Epic-7 item). |

### Success Criteria
1. Every 8.3 prerequisite has a **named owner and a tracked `action_items` entry**
   — met by §4.3.
2. **G12 is sequenced first** and carries an explicit publish-vs-source-mode
   owner decision — met by §4.2 + the first `action_items` entry.
3. **No submodule edits, no Parties source migration** in this proposal — met.
4. Stories 8.6–8.10 remain `backlog` until owner proof is recorded in the matrix
   row — governance preserved (spine §4, matrix Review Gate).
5. No PRD FR coverage change; Epics 1–5 remain the feature-readiness baseline —
   met.

### Deferred / out of scope for this proposal
- **Filing the actual owner tickets/PRs inside the external repos** — that is
  cross-repo execution outside `Hexalith.Parties`' authority; this proposal
  routes and tracks it.
- Authoring specs 8.6–8.10 (routed to the spec/create-story workflow, per the
  spine §4 gate).
- Production KMS provisioning (operational prerequisite before real regulated EU
  personal data — unchanged; not an Epic 8 blocker).
- Open Epic-6/Epic-7 action items (File-List pre-review gate, root-gitlink RC
  gate, validation-ladder tooling) — still tracked in `sprint-status.yaml`.
