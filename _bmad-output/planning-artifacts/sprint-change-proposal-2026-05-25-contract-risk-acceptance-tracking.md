---
date: '2026-05-25'
project: 'Hexalith.Parties'
author: 'Jérôme (via bmad-correct-course)'
trigger: 'implementation-readiness-report-2026-05-25-v2.md — the single "risk-acceptance to track" (recommendation #3)'
change_scope: 'Minor — establish durable closure tracking for an already-approved risk acceptance; no code, no backlog reorg, no replan'
selected_path: 'Option 1 — Direct Adjustment (make the closure obligation explicit, owned, and discoverable)'
status: 'applied (pending final approval)'
artifacts_modified:
  - _bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
---

# Sprint Change Proposal — EventStore-Fronted Contract Risk-Acceptance Closure Tracking

**Date:** 2026-05-25
**Project:** Hexalith.Parties
**Mode:** Incremental
**Triggering input:** `implementation-readiness-report-2026-05-25-v2.md` — findings tally `riskAcceptanceToTrack: 1`, recommendation #3.

---

## Section 1 — Issue Summary

This change does **not** address a defect. The as-built behavior is correct and the underlying
decisions are formally approved. The issue is a **governance/tracking gap**:

The v1.2 admin portal (Epic 7) and embeddable party picker (Epic 8) were delivered against a
**temporary bridge**, not the full EventStore-fronted Parties client/gateway contract:

- **GDPR bridge:** `IAdminPortalGdprClient` / `HttpAdminPortalGdprClient` / `AdminPortalGdprRoutes`.
- **Query/picker bridge:** `Hexalith.Parties.Picker` + `IPartiesQueryClient` / `HttpPartiesQueryClient`.

The full contract was **never globally `Satisfied`**. Instead, Story 7.7 and all six Epic 8 picker
stories (8.1–8.6) shipped under **three scoped, project-lead-approved risk acceptances**:

- `sprint-change-proposal-2026-05-23.md` (Story 7.7)
- `sprint-change-proposal-2026-05-23-epic8-picker-gate.md` (Story 8.1)
- `sprint-change-proposal-2026-05-24-epic8-picker-gate-remaining.md` (Stories 8.2–8.6)

The Story 7.6 fail-closed capability gate (UX-DR11) **correctly** keeps two GDPR operations —
**erasure-certificate** and **retry-verification** — disabled, with the exact bounded blocker
`Blocked on accepted EventStore-fronted Parties client/gateway contract`. Story 7.7's
`ProvisionalBridge()` capability surface is honest: 7 genuinely-working ops enabled, those 2 disabled.

**The gap:** the obligation to *formally close* these acceptances when the real contract lands was
scattered across five+ documents (the dependency record, three SCPs, Story 7.6, and the readiness
report) with **no single durable, actionable closure tracker** and **no consolidated closure
checklist**. That is precisely how a tracked-but-unowned risk acceptance silently rots.

---

## Section 2 — Impact Analysis

### Epic Impact
- **Epics 7 & 8:** Complete (all stories `done`). No implementation impact. The temporary bridge and
  fail-closed gate are the intended, accepted as-built state.
- **No other epic affected.** No epic added, removed, renumbered, or redefined.

### Story Impact
- **No story re-opened or re-scoped.** Story 7.6 stays `done`; its gate stays fail-closed. The change
  records *when and how* to revisit it, not a re-scope.

### Artifact Conflicts
- **PRD / Architecture / UX:** No conflict. FR65–FR67 and the contract gate (UX-DR11, ADR D20) are
  consistent with the as-built state.
- **Dependency record:** Was missing a consolidated closure checklist and a frontmatter closure flag —
  now added.
- **deferred-work.md / sprint-status.yaml:** Lacked a discoverable open item / audit trail for this
  acceptance — now added.

### Technical Impact
- **None.** No code, infrastructure, test, or deployment change. Documentation/tracking only.

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment (make the closure obligation explicit, owned, and discoverable).**

| Option | Verdict | Reason |
|---|---|---|
| 1 — Direct Adjustment | **Selected** | The decisions are sound and approved; only their *trackability* drifted. Add a consolidated closure procedure + a single discoverable open item + an audit comment. Effort **Low**, Risk **Low**, fully reversible. |
| 2 — Close the acceptances now / build the real contract | Rejected (premature) | No accepted EventStore-fronted contract exists yet. Closing now would require faking the two disabled GDPR ops — explicitly forbidden by the binding acceptance conditions. |
| 3 — Roll back the temporary bridge | Rejected | Would revert delivered, reviewed, retro'd Epic 7/8 work for zero benefit; the bridge is the accepted interim state. |

**Explicitly NOT done:** the dependency record's `status` is **left as `Risk Accepted (scoped)`**, and
**not** flipped to `Satisfied`. The contract has not landed; flipping it would be false.

---

## Section 4 — Detailed Change Proposals (all APPLIED)

### 4.1 — Dependency record: consolidated "Closure Procedure"
**File:** `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`
Added a `## Closure Procedure` section: an OPEN-status banner, the closure **trigger** (a real contract
satisfying the Dependency Definition), and an 8-item checklist — flip status to `Satisfied`; reconcile
the GDPR bridge; reconcile the picker/query bridge (incl. the `PartyPickerSelection` ↔ DOM payload
divergence); re-evaluate the Story 7.6 gate to enable erasure-certificate + retry-verification (or keep
them bounded if still unsupported, never faked); refresh the Story 7.7 `ProvisionalBridge()` surface;
preserve all fail-closed/privacy/accessibility guardrails + add contract-available tests; verify
focused suites + Release build; append a closure audit comment and remove the deferred-work item.
**Owner named:** Jérôme + whoever lands the contract.

### 4.2 — Dependency record: frontmatter closure flag
Added `closure_status: OPEN — …` and `closure_tracking_source: <this SCP>` so the open obligation is
visible at the top of the canonical dependency document.

### 4.3 — deferred-work.md: discoverable open item
Added a top-of-file section **"Open risk acceptance to formally close (not from a code review) —
EventStore-fronted Parties client/gateway contract"** that summarizes the acceptance, names the two
disabled GDPR ops and the bridges, and points to the Closure Procedure as the source of truth.

### 4.4 — sprint-status.yaml: audit comment
Prepended a `# last_updated touched 2026-05-25 by correct-course …` comment recording this tracking
action. **No status transitions** — all Epic 7/8 stories remain `done`.

---

## Section 5 — Implementation Handoff

- **Change scope classification:** **Minor** — documentation/tracking reconciliation. No backlog
  reorganization, no replan, no code.
- **Executor:** Developer agent (changes already applied incrementally during this workflow).
- **Remaining handoff actions:**
  1. Commit the dependency-record + deferred-work + sprint-status edits and this SCP when the user is
     ready (no commit performed without explicit request).
  2. **Future trigger (not now):** when an accepted EventStore-fronted Parties client/gateway contract
     lands, execute the Closure Procedure checklist and then mark this SCP and the three source SCPs
     closed.
- **Success criteria:**
  - The closure obligation is discoverable from three independent surfaces (dependency record,
    deferred-work tracker, sprint-status audit trail) and has a named owner and an unambiguous checklist.
  - The dependency record still reads `Risk Accepted (scoped)` / not globally `Satisfied`; the Story 7.6
    fail-closed gate is untouched; the two disabled GDPR operations remain bounded-blocked.

---

## Appendix — Evidence

- `implementation-readiness-report-2026-05-25-v2.md`: `riskAcceptanceToTrack: 1`; recommendation #3.
- Dependency record `…-2026-05-17.md`: three scoped acceptances (2026-05-23, 2026-05-24); status
  "Risk Accepted (scoped); dependency NOT globally Satisfied".
- `sprint-status.yaml`: `7-6-…: done`, `7-7-…: done`, `8-1…8-6: done`; Epic 7/8 risk-acceptance
  comment trail.
- Story 7.6 file: AC1 bounded blocker `Blocked on accepted EventStore-fronted Parties client/gateway
  contract`; Story 7.7 `ProvisionalBridge()` honest capability surface.
