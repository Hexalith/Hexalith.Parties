---
date: '2026-05-25'
project: 'Hexalith.Parties'
author: 'Jérôme (via bmad-correct-course)'
trigger: 'implementation-readiness-report-2026-05-24.md (status: NEEDS WORK, 10 issues)'
change_scope: 'Minor — documentation reconciliation of planning artifacts; no code, no backlog reorg, no replan'
selected_path: 'Option 1 — Direct Adjustment (reconcile epics.md to as-built)'
status: 'applied (pending final approval + sprint-status audit note)'
artifacts_modified:
  - _bmad-output/planning-artifacts/epics.md
---

# Sprint Change Proposal — Implementation-Readiness Reconciliation

**Date:** 2026-05-25
**Project:** Hexalith.Parties
**Mode:** Incremental
**Triggering input:** `_bmad-output/planning-artifacts/implementation-readiness-report-2026-05-24.md` (overall status **NEEDS WORK**, 10 issues, 2 flagged **blocking**, both in Epic 9)

---

## Section 1 — Issue Summary

The 2026-05-24 implementation-readiness assessment (run by `bmad-check-implementation-readiness`)
returned **NEEDS WORK** with 10 issues across traceability, UX dependency visibility, epic
sequencing/dependencies, and story quality. Its two **blocking** issues were both in Epic 9:
forward story dependencies, and MVP-vs-deferred sequencing confusion (Epic 9 appears after the
v1.1/v1.2 epics).

**Key discovery during analysis:** the readiness checker reads **planning artifacts only**
(`epics.md`, PRD, architecture, UX) and assessed Epic 9 **as if implementation had not started**
— its own words: *"Address the 2 critical issues before proceeding with Epic 9 implementation."*

The source of truth tells a different story. Per `_bmad-output/implementation-artifacts/sprint-status.yaml`:

- **Epic 9 v2 is fully delivered** — stories `9-1` through `9-8` are all `done`, dev'd and
  code-reviewed between 2026-05-21 and 2026-05-24, and `epic-9-retrospective: done`.
- **Every epic (1–9) has all stories `done`** with retrospectives complete/optional. The project
  is feature-complete against the current epic plan.
- The delivered artifacts exist on disk and were verified: `deploy/k8s/` (publish.ps1, teardown.ps1,
  `_lib/`, the 7 Aspirate service folders + `redis`/`keycloak` carve-outs), all 10 `deploy/dapr/`
  CRs from Story 9.4's exact file set, `deploy/validate-deployment.ps1`, the canonical
  `docs/kubernetes-deployment-architecture.md`, and `tests/Hexalith.Parties.DeployValidation.Tests/`.

Therefore the report's *blocking* premise is stale. The real, residual problem is **documentation
drift**: the canonical planning document (`epics.md`) still describes Epic 9 with future-tense,
forward-referencing acceptance criteria and presents MVP Epic 9 after deferred epics, and the FR
Coverage Map omits `FR31a`. The remedy is to **reconcile `epics.md` to the as-built reality**, not
to restructure work that is already delivered, reviewed, and retro'd.

### Reclassification of the two "blocking" issues

| Report's blocking issue | As-built reality | Reclassified |
|---|---|---|
| Epic 9 forward story dependencies | Stories shipped in backward-safe `blocked_by` order (9-1→9-2→9-3→9-4→{9-5,9-6}→9-7, +9-8); all `done` | **Not a blocker** → planning-doc wording hygiene |
| Epic 9 MVP-after-v1.1/v1.2 sequencing | All MVP *and* deferred epics already built; nothing left to mis-schedule | **Not a blocker** → traceability clarity |

---

## Section 2 — Impact Analysis

### Epic Impact
- **Epic 9:** Complete. No implementation impact. Corrective action is documentation reconciliation only.
- **Epics 1–8:** Complete. No changes beyond the FR Coverage Map / UX-DR / Story 1.1 cleanups.
- **No epic added, removed, renumbered, or redefined.** Epic numbers are intentionally preserved
  because they are referenced throughout `sprint-status.yaml`, commit history, story files, and
  retrospectives — renumbering would break those references.

### Story Impact
- No story re-opened or re-scoped. Story 1.1 wording is tightened for future reference-service
  authors, anchored to what actually shipped (delivered story; not a re-scope).
- Prepared-deferred stories (2.9, 5.6, 5.7) and NFR-coverage story (6.10) remain distinct from
  runtime delivery — already recorded in the 2026-05-17 implementation-readiness cleanup note in
  `sprint-status.yaml`; reinforced by the new as-built phase table and the existing coverage legend.

### Artifact Conflicts
- **PRD:** No conflict. 100% FR coverage; the `FR31a` gap was in the epics map, not the PRD.
- **Architecture:** No conflict. ADRs D-K8s-1/2/3 and D20 are decided and reflected on disk.
- **UX:** No functional conflict. `UX-DR1–32` are numbered only in `epics.md`; resolved by declaring
  `epics.md` the canonical numbered source.
- **Deploy scripts / IaC / tests / CI / docs:** Delivered and green per code-review closure notes;
  these are the source of truth we reconcile *to*, not artifacts to edit.

### Technical Impact
- **None.** No code, infrastructure, or deployment change. Documentation-only.

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment (documentation reconciliation of `epics.md`).**

| Option | Verdict | Reason |
|---|---|---|
| 1 — Direct Adjustment | **Selected** | Code is done & reviewed; only the plan drifted. Lowest risk; restores traceability; prevents future readers (human or AI) from being misled by forward-referencing ACs. Effort **Low**, Risk **Low**. |
| 2 — Rollback | Rejected | Would revert delivered, reviewed, retro'd Epic 9 code for zero benefit; high cost/risk. |
| 3 — MVP Review | Rejected / N/A | MVP already delivered; no scope to reduce or redefine. |

**Decisions on the report's remaining (Major) recommendations — deliberately NOT actioned, with rationale:**

- **Reframe/rename Epic 9 title around operator value (report Major #1):** Not renamed. The epic
  *description* already leads with operator value (*"Operators and developers deploy the full…
  topology… via a single `publish.ps1` command"*), and the new as-built header marks it DELIVERED.
  Renaming a delivered epic's title would ripple into `sprint-status.yaml` and retrospective references.
- **Split oversized Epic 9 stories 9.1/9.4/9.5/9.7 (report Major #2):** Not split. Each was dev'd,
  code-reviewed, and closed. Splitting completed stories has no implementation value and would
  fragment the audit trail.
- **Resolve Story 7.6/7.7 external dependency before scheduling (report Major #3):** Already resolved.
  Scoped risk-acceptance SCPs (2026-05-23, 2026-05-24) against
  `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` unblocked the work; 7.6/7.7 are
  `done`. The global contract remains tracked as not-globally-satisfied.
- **Keep prepared-deferred stories distinct (report Major #4):** Already tracked (2026-05-17
  cleanup note); reinforced by the new as-built phase table.

---

## Section 4 — Detailed Change Proposals (all APPLIED, file: `epics.md`)

### 4.1 — Epic 9 as-built status header  *(resolves Critical #1)*
Added an "Implementation status (as-built, 2026-05-25): DELIVERED" block plus a reading note after
the Epic 9 `**Supersedes:**` line. The note reframes the inter-story `see Story 9.x` references as
**cross-references within a completed epic delivered in backward-safe `blocked_by` order**, not unmet
forward dependencies — neutralizing the report's forward-dependency finding at the epic level without
rewriting ~30 individual ACs.

### 4.2 — As-built Implementation Status & Sequence section  *(resolves Critical #2)*
Inserted a new `## Implementation Status & Sequence (as-built, 2026-05-25)` H2 before `## Epic List`,
stating all nine epics are delivered, providing a **Phase-authoritative** lane table (MVP: 1,2,3,4,5,9
· v1.1: 6 · v1.2: 7,8), and a note that Epic 9 is MVP despite its numeric position — sort by `Phase`,
never by epic number. Explicitly records that epic numbers are **not** renumbered.

### 4.3 — `FR31a` added to FR Coverage Map  *(resolves Minor #1)*
Added `FR31a: Epic 9 - Kubernetes Deployment Platform` after the `FR31` line, so the map total matches
the PRD's 75-FR inventory.

### 4.4 — Story 1.1 wording tightened  *(resolves Minor #3)*
- "represented only as needed for subsequent stories" → "exist as compiling stubs with correct
  dependency directions; infrastructure-bearing projects are fleshed out by the later stories that
  first require them".
- "built enough to support the first domain story" → "restores and builds green (zero warnings under
  `TreatWarningsAsErrors`), enabling Story 1.2 (Create Party Aggregate with Stable Identity) to begin".

### 4.5 — Canonical UX-DR traceability note  *(resolves Minor #2 / UX-traceability)*
Added a note under `### UX Design Requirements` declaring the `UX-DR1–32` identifiers in `epics.md`
the canonical machine-traceable source, with the prose UX docs retained for rationale/visuals.

---

## Section 5 — Implementation Handoff

- **Change scope classification:** **Minor** — documentation reconciliation of planning artifacts.
  No backlog reorganization, no replan, no code.
- **Executor:** Developer agent (changes already applied incrementally during this workflow).
- **Remaining handoff actions:**
  1. Append a `# last_updated touched 2026-05-25 by correct-course …` audit comment to
     `sprint-status.yaml` (no status transitions; records this reconciliation per repo convention).
  2. Commit the `epics.md` reconciliation + this SCP + the readiness report when the user is ready
     (no commit performed without explicit request).
- **Success criteria:**
  - `epics.md` no longer presents Epic 9 as pre-implementation or as last-in-sequence; the FR
    Coverage Map includes `FR31a`; UX-DR canonical source is declared; Story 1.1 ACs are verifiable.
  - Re-running `bmad-check-implementation-readiness` would still flag the *planning-doc structure*
    observations, but a reader now has the as-built header/table establishing the issues are
    resolved-by-delivery and the document reflects reality.

---

## Appendix — Evidence

- `sprint-status.yaml`: `9-1 … 9-8 = done`; `epic-9-retrospective: done`; all epics' stories `done`.
- On-disk: `deploy/k8s/**`, `deploy/dapr/**` (10 CRs), `deploy/validate-deployment.ps1`,
  `docs/kubernetes-deployment-architecture.md`, `tests/Hexalith.Parties.DeployValidation.Tests/`.
- Git log: `feat(story-8.6)`, `feat(story-8.5)`, `feat(story-8.4)`, `chore(epic-8)`,
  `chore(epic-9): add retrospective`.
