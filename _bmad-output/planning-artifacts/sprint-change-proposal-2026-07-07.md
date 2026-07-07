---
title: Sprint Change Proposal — Epic 8 Readiness Reconciliation
date: 2026-07-07
author: Administrator
workflow: bmad-correct-course
mode: incremental
scope_classification: moderate
trigger: implementation-readiness-report-2026-07-07.md
status: approved
related:
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-06.md
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
---

# Sprint Change Proposal — Epic 8 Readiness Reconciliation

## 1. Issue Summary

The Implementation Readiness Assessment (`implementation-readiness-report-2026-07-07.md`)
rated the planning corpus **READY for PRD feature scope (Epics 1–5)** with 0
critical blockers and 100% FR coverage, but **NEEDS WORK before remaining Epic 8
maintenance work**.

This is **not** a feature or MVP-scope change. It is a **process/readiness
reconciliation** for post-MVP maintenance work already in flight. The core
problem, precisely stated:

- The 2026-07-06 proposal that created Epic 8 made an **architecture spine** a
  prerequisite for the deletion-heavy migration stories and reserved the path
  `architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md` for it.
  **That document was never authored.**
- Story 8.1 correctly **preserved** "missing Epic 8 architecture spine" as an
  open blocker, yet Stories **8.2–8.5 proceeded and are marked done** (via the
  dev-auto/bmad-loop workflow, against `spec-8-x` files), leaving a documented
  process deviation to reconcile.
- **No spec files exist for Stories 8.6–8.10** (only `spec-8-1` … `spec-8-5`).
- Several remaining Epic 8 stories (8.6 projection/query, 8.7 data-protection,
  8.8 client/MCP/AppHost/build/deploy) are **broad and cross-module**, and lack
  per-spec hard gates.

### Evidence (repo-verified 2026-07-07)

- `sprint-status.yaml`: `epic-8: in-progress`; `8-1`…`8-5` = `done`; `8-6`…`8-10`
  = `backlog`.
- `find` confirms `spec-8-1` … `spec-8-5` exist; **no** `spec-8-6` … `spec-8-10`.
- The only `ARCHITECTURE-SPINE.md` on disk before this proposal was Epic 7's.
- `spec-8-1-…​.md` lines 60, 86, 133, 182 explicitly record the missing Epic 8
  architecture spine as a preserved blocker.

### Change-Analysis Checklist Status

| # | Item | Status |
|---|---|---|
| 1.1–1.3 | Trigger, core problem, evidence | ✅ Done |
| 2.1 | Epic 8 completable as planned? | ❗ Only after spine reconciled + specs + gates |
| 2.2 | Epic-level change | ✅ Modify Epic 8 (no new/removed epic) |
| 2.3–2.4 | Other/future epics affected | ✅ N/A (Epics 1–7 done; Epic 8 only) |
| 2.5 | Resequencing | ✅ None — `8.1→8.10` preserved |
| 3.1 | PRD conflict | ✅ None — no FR change |
| 3.2 | Architecture | ❗→✅ Spine authored (reconciliation) |
| 3.3 | UX conflict | ✅ N/A — Epic 8 is conformance, no new UX |
| 3.4 | Other artifacts | ✅ epics.md + sprint-status + epic-8-context reconciled |
| 4.1 | Direct Adjustment | ✅ **Viable — selected** |
| 4.2 | Rollback | ⛔ Not viable/unwarranted |
| 4.3 | MVP review | ✅ N/A — MVP (Epics 1–5) complete/unaffected |
| 4.4 | Path selected | ✅ Direct Adjustment (reconcile-then-gate) |

## 2. Impact Analysis

### Epic Impact
- **Epic 8 only.** Epics 1–7 are done and unaffected. No epic added, removed, or
  re-sequenced. Epic 8's `8.1→8.10` order is unchanged.
- Epics 6–8 remain classified **maintenance/platform (Class A/B/C)** with **zero
  new PRD functional requirements** — this classification is preserved, not
  changed (readiness Major-issue #1 is a *hold-the-line* item).

### Story Impact
- **8.1–8.5:** done. **Ratified** as-is — the readiness report accepts them as
  done with parity evidence; rollback (§4.2) buys nothing.
- **8.6–8.10:** stay `backlog`. Each now requires a spec file that satisfies the
  spine §4 readiness gate before it is dev-ready. Broad stories 8.6/8.7/8.8 are
  split or hard-gated at spec-creation time.

### Artifact Conflicts (resolved by this proposal)
- **Architecture:** missing Epic 8 spine → authored as a reconciliation spine
  (invariants + readiness gate + explicit blocker closure).
- **`epics.md`:** stale future-tense scope note ("after the spine is approved")
  → reconciled to point at the approved spine; readiness-gate section added.
- **`sprint-status.yaml`:** stale "blocked until spine approved" comment →
  reconciled to "spine approved; gated by §4."
- **`epic-8-context.md`:** no spine/gate reference → pointer added.
- **PRD / UX:** no conflict, no change.

### Technical Impact
- **No code change in this proposal.** Planning/architecture artifacts only.
- Downstream: specs 8.6–8.10 must carry prerequisites, touched repos, rollback,
  validation lanes + parity evidence, non-goals, and a parity-evidence checklist
  before any deletion-heavy dev session. Local rollback paths (projection, query,
  crypto, release recovery) remain until replacement APIs prove parity.

## 3. Recommended Approach

**Selected: Option 1 — Direct Adjustment (Hybrid: reconcile-then-gate).**

- **Reconcile** the spine blocker now by ratifying the artifacts that already
  carry the spine's substance (`epic-8-context.md`, the Epic 7 spine, landed
  specs 8.1–8.5) into an approved reconciliation spine, rather than re-deriving a
  full new design.
- **Gate** the remaining work: every remaining spec (8.6–8.10) must satisfy the
  spine §4 per-spec readiness gate, and broad cross-module stories are split or
  hard-gated at spec-creation time.

**Rationale:** lowest disruption; preserves momentum; matches what actually
shipped (8.2–8.5 landed with evidence); and is exactly the remediation the
readiness report prescribes. Rollback (§4.2) is unwarranted — completed,
evidenced work would be discarded for no simplification. MVP review (§4.3) does
not apply — the product FR scope (Epics 1–5) is complete and untouched.

- **Effort:** Low–Medium (planning-artifact authoring; spec authoring deferred to
  the spec workflow).
- **Risk:** Low for this proposal; the residual delivery risk (deletion-heavy
  migrations) is contained by the §4 gate.
- **Timeline impact:** None to the MVP. Unblocks 8.6–8.10 spec creation
  immediately.

## 4. Detailed Change Proposals (all applied 2026-07-07, incremental mode)

### Architecture (NEW)
- **`architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`** —
  reconciliation spine: purpose + ratification of 8.2–8.5, target domain-module
  contract (keep/move table), invariants I1–I15, the §4 remaining-work readiness
  gate, sequencing, and an explicit §6 blocker-closure statement
  ("APPROVED (reconciled) 2026-07-07").

### Epics
- **`epics.md`** — Epic 8 scope note reconciled from future-tense
  ("after the spine is approved") to "spine approved (reconciled) 2026-07-07;
  specs 8.6–8.10 must satisfy the §4 gate." Added an **"Epic 8 Remaining-Work
  Readiness Gate"** subsection enumerating the six per-spec requirements.

### Sprint Status
- **`sprint-status.yaml`** — Epic 8 comment reconciled: spine approved; 8.6–8.10
  no longer blocked by a missing spine, now gated by §4. Story rows unchanged
  (no re-sequencing). `last_updated` bumped.

### Implementation Context
- **`epic-8-context.md`** — spine + §4 readiness-gate pointer appended to
  Requirements & Constraints (a future `compile-epic-context` regeneration will
  formalize it from the updated `epics.md`).

### No-change (explicitly)
- **PRD** — no FR change. **UX** — no new scope; picker re-skin / Admin
  phone-reflow debt already tracked in Epic 8 Story 8.9. Raw UX review files
  remain historical; `validation-report.md` + final spines stay authoritative.

## 5. Implementation Handoff

**Scope classification: Moderate.** The architecture deliverable (spine) is
resolved in this session; the remainder is backlog/spec reorganization
coordinated by PO/Developer — no fundamental replan.

| Recipient | Responsibility |
|---|---|
| PM / Architect | Spine authored & reconciled in this session. Remaining duty: confirm platform-API owners/readiness (8.3 matrix) before 8.6/8.7 spec dev. |
| Product Owner / Developer | Create specs 8.6–8.10 via the spec/create-story workflow **in order**, each satisfying the spine §4 gate; split/hard-gate broad stories 8.6/8.7/8.8. |
| Developer agents | Implement only from gated specs; preserve invariants I1–I15; keep local rollback paths until parity is proven. |
| Tech Writer | Keep PRD/epics/readiness docs explicit that Epics 6–8 are maintenance with no new PRD FR coverage (open Epic-7 action item). |

### Success Criteria
1. Epic 8 spine exists at its reserved path with an explicit blocker-closure — **met**.
2. Planning artifacts (`epics.md`, `sprint-status.yaml`, `epic-8-context.md`)
   agree the spine is approved and the §4 gate governs 8.6–8.10 — **met**.
3. Each of specs 8.6–8.10, when created, declares all six §4 gate elements —
   *pending spec workflow*.
4. No PRD FR coverage changes; Epics 1–5 remain the feature-readiness baseline — **met**.

### Deferred / out of scope for this proposal
- Authoring the five detailed specs 8.6–8.10 (routed to the spec/create-story
  workflow).
- Production KMS provisioning (operational prerequisite before real regulated EU
  personal data — unchanged; not an Epic 8 blocker).
- Open Epic-6/Epic-7 action items (File-List pre-review gate, root-gitlink RC
  gate, validation-ladder tooling) — tracked in `sprint-status.yaml` action_items.
