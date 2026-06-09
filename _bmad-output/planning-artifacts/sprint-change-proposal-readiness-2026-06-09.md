# Sprint Change Proposal — Implementation-Readiness Course Correction

- **Date:** 2026-06-09
- **Author:** Administrator (via Correct Course workflow)
- **Status:** ✅ Approved & applied to `epics.md` (2026-06-09)
- **Mode:** Batch
- **Scope classification:** **Moderate** (backlog reorganization — story splits + renumbering + AC additions; no PRD/architecture change, no code yet)
- **Triggering input:** `implementation-readiness-report-2026-06-09.md` (verdict: 🟡 NEEDS WORK — Conditional GO)
- **Target artifact:** `_bmad-output/planning-artifacts/epics.md` (only)

---

## Section 1 — Issue Summary

**Trigger.** The Implementation Readiness Assessment returned **Conditional GO**. It found **0 plan-quality
blockers** — exemplary ACs, 100% FR traceability, a clean epic DAG, a mature independently-reviewed UX.
The "conditional" verdict comes from a small number of **named, planned gaps** that need the plan
artifacts tightened so the GO slices (Epics 1–2, most of 3) can proceed and the HOLD slices (Epics 4–5,
Story 3.5) get unblocked in the right sequence.

**Discovery context.** Surfaced by the readiness review (acting PM / traceability lens), not during
implementation. This is a planning-tightening pass, handed off to Correct Course by `bmad-help`.

**Decisions taken at navigation start (this run):**

- **Edit mode:** Batch.
- **In-scope corrections (4):** M1 (split Story 4.1), D7 (isolate the backend contract story), m3
  (phone-reflow AC on Story 2.3), mock-fidelity note on UI stories.
- **No-PRD single-source risk:** **Accept consciously** — recorded here as a deliberate decision; no new
  PRD work commissioned.
- **KMS gate:** No edit — already correctly tracked as a release gate in Story 1.10 / AR-Gap-KMS.

---

## Section 2 — Impact Analysis

- **Epic impact:** Two epics each contain **one story that bundles enabling/decision work with the
  consuming implementation**, defeating estimability and clean sequencing:
  - **Epic 4 / Story 4.1** bundles an *undecided design choice* (binding mechanism) with its build →
    blocks estimation of all of Epics 4–5 (readiness **M1**).
  - **Epic 3 / Story 3.5** bundles a cross-submodule *backend contract* (approval-gated) with the UI
    report that consumes it → couples the report's schedule to an external approval (readiness **D7**).
  - Epic **DAG is unchanged** (`1→{2,4}`, `2→3`, `4→5`); only intra-epic story granularity changes.
- **Story impact:** Net **+2 stories** (28 → 30). Two splits, one renumber cascade in Epic 4, one AC
  addition (2.3), one cross-reference fix (1.4), and one document-level convention note.
- **Artifact conflicts:**
  - `epics.md` — all edits land here (Section 4).
  - `architecture.md` — **no change**; it already names AR-D7 / AR-Gap-Binding / AR-Gap-KMS. The splits
    *consume* the architecture's own gap analysis rather than contradict it.
  - UX design set — **no change**; the spine is already authoritative. The only issue was stories
    *citing* not-fully-patched mocks → resolved by a normative note, not a UX edit.
  - `sprint-status.yaml` — **does not exist yet** (sprint planning not run) → checklist item 6.4 is N/A;
    the future sprint plan will be generated from the corrected `epics.md`.
- **Technical impact:** None yet (planning only). Downstream: Epics 4–5 cannot be estimated until the
  Story 4.1 ADR lands; Story 3.6 (report UI) cannot complete until Story 3.5 (D7 backend) is approved
  and landed.

---

## Section 3 — Recommended Approach

**Direct Adjustment** (Option 1) — modify/split stories within the existing epic structure. Rollback
(Option 2) is N/A (nothing built). MVP-review (Option 3) is N/A (scope unchanged; MVP still achievable).

**Rationale.** Every finding is a *granularity/traceability* tightening, not a scope or direction change.
Splitting decision/enabling work out of the consuming story is the minimal edit that (a) makes the
HOLD slices estimable, (b) isolates the one external-approval dependency (D7) into a single clearly-gated
story, and (c) preserves the clean DAG and exemplary ACs the review credited.

- **Effort:** Low (planning-doc edits). **Risk:** Low. **Timeline impact:** None to the GO slices; the
  HOLD slices gain a clear, sequenced unblock path.

---

## Section 4 — Detailed Change Proposals

> All edits target `_bmad-output/planning-artifacts/epics.md`. Old → New shown per change.

### Change 1 — M1: Split Story 4.1 into a decision spike + an implementation story; renumber Epic 4

**Rationale.** Story 4.1's first AC is *"the team selects a provisioning mechanism…"* — a design decision,
not an implementable story with a predetermined outcome. Because Epics 4–5 both depend on 4.1, the entire
Consumer half cannot be estimated until the mechanism is chosen. Split into **4.1 (decide → ADR)** +
**4.2 (implement)**, then renumber the rest of Epic 4.

**OLD — `### Story 4.1` (single story):**

```
### Story 4.1: Consumer identity → `party_id` binding provisioning (AR-Gap-Binding)
… As a product owner … When the team selects a provisioning mechanism … the decision is recorded …
the verified party_id claim is issued and/or stored … never in the event stream … integration tests …
```

**NEW — two stories:**

```
### Story 4.1: Decide the Consumer identity → `party_id` binding mechanism (design spike → ADR)

As a product owner / architect,
I want a decided, recorded mechanism for binding a consumer's identity to their party,
So that the Consumer area can be estimated and built against a known design.

**Acceptance Criteria:**

**Given** the undesigned binding gap (AR-Gap-Binding) that blocks Epics 4–5
**When** the options — **admin-link · self-registration · IdP federation** — are evaluated against
tenancy, fail-closed resolution, provisioning effort, and where the verified `party_id` is held (IdP
claim and/or a small binding store — **never the event stream**)
**Then** exactly one mechanism is **selected** and the decision, its alternatives, and trade-offs are
**recorded in an ADR**, including the binding-store shape (if any) and the provisioning/onboarding flow.

**Given** the recorded decision
**When** Story 4.2's acceptance criteria are written
**Then** they are derived directly from the chosen option (no open design questions remain), and the
ADR is referenced as the source.

**And** this is a **decision spike**, not an implementation story; it produces a decision artifact only
and is the **predecessor of Story 4.2** and all of Epics 4–5. _(Resolves readiness finding M1.)_

### Story 4.2: Implement the chosen `party_id` binding-provisioning mechanism

As a product owner,
I want the binding mechanism chosen in Story 4.1 implemented end-to-end,
So that consumers can be provisioned and the Consumer area becomes reachable.

**Acceptance Criteria:**

**Given** the mechanism selected in Story 4.1 (ADR)
**When** it is implemented
**Then** a verified `party_id` claim is issued and/or stored per the ADR (IdP claim and/or binding store
— **never in the event stream**), and a consumer can be provisioned through the defined flow.

**Given** a newly provisioned consumer
**When** they sign in
**Then** their `party_id` claim resolves (consumed by Story 1.4) and they reach `/me`; an **unbound**
consumer sees the `NoPartyBinding` onboarding/error UX rather than any data screen.

**And** integration tests cover a bound consumer (reaches `/me`) and an unbound one (fails closed).
_This story unblocks the rest of Epic 4 and Epic 5._
```

**Renumber cascade (Epic 4):**

| Old | New | Title (unchanged) |
|---|---|---|
| Story 4.2 | **Story 4.3** | Stand up the ConsumerPortal RCL and Consumer area |
| Story 4.3 | **Story 4.4** | My profile (FR-Consumer-1) |
| Story 4.4 | **Story 4.5** | Edit my profile (FR-Consumer-2) |

### Change 2 — D7: Split Story 3.5 into an approval-gated backend contract + the report UI

**Rationale.** Story 3.5 bundles the cross-submodule EventStore contract (needs explicit approval) with
the UI report that consumes it, coupling the report's schedule to an external approval. Isolate the
backend contract as its own predecessor story so the rest of Epic 3 ships without it, and the report UI
becomes a normal story with a clean dependency.

**OLD — `### Story 3.5` (single story):**

```
### Story 3.5: EventStore erasure-verification contract and report (D7 — cross-submodule)
… the EventStore contract … is defined AND the Parties-side wiring implemented … the report shows …
gated on cross-submodule approval …
```

**NEW — two stories:**

```
### Story 3.5: EventStore erasure-verification contract (backend, cross-submodule — approval-gated)

As an EventStore maintainer,
I want a defined contract for erasure certification and verification retry,
So that the Parties tier can prove a party was shredded across projections instead of stubbing it.

**Acceptance Criteria:**

**Given** explicit approval for the cross-submodule change
**When** the EventStore contract for `GetErasureCertificate` / `RetryErasureVerification` is defined and
the Parties-side wiring implemented
**Then** the inert **501 stubs are replaced**, `ContractUnavailable` no longer faults, and
`IAdminPortalGdprClient` exposes a real erasure-certificate result the UI can consume.

**Given** an erased party
**When** the certificate is produced
**Then** it carries stable erased/verification state **without** exposing destroyed-key/cryptographic-
exception text, stale display names, contact values, identifiers, or raw payloads.

**And** this story is explicitly **gated on cross-submodule approval** and sequenced as a **predecessor
to Story 3.6**; the rest of Epic 3 ships without either. _(AR-D7 / AR-Gap-D7.)_

### Story 3.6: Admin erasure-verification report (UI — consumes the D7 contract)

As a DPO,
I want a verification report proving a party was shredded across projections,
So that I can prove the right to erasure was honored, not merely assert it.

**Acceptance Criteria:**

**Given** the D7 contract from Story 3.5 is in place
**When** I open the erasure-verification report for an erased party
**Then** the Admin report shows the record **confirmed shredded across projections**, mapping
`GetErasureCertificate` / `RetryErasureVerification` outcomes through the canonical StatusKind→UI states
with correct politeness.

**Given** the D7 contract has **not** yet landed
**When** the report surface is reached
**Then** it degrades to a clear "verification not yet available" state (no fault, no PII), and the rest
of the GDPR page remains fully usable.

**Given** an erased party
**When** the report renders
**Then** it shows stable erased/verification state **without** exposing destroyed-key/cryptographic-
exception text, stale display names, contact values, identifiers, or raw payloads.
```

### Change 3 — m3: Add a phone-reflow + sheet-focus AC to Story 2.3

**Rationale.** NFR5's highest-reflow-risk surface (Admin master-detail on phone) currently threads only
implicitly; UX-DR10's sheet focus contract lives in prose with no testable AC. Add one explicit AC.

**INSERT — new Given/When/Then appended to `### Story 2.3` (after the erased/Gone block):**

```
**Given** a phone-width viewport (NFR5 master-detail reflow — the highest-reflow-risk surface)
**When** I open a party from the list
**Then** the desktop two-pane master-detail **collapses to a sheet / full-screen detail**, focus
**moves into the sheet** on open and **returns to the originating row** on back/close (UX-DR10 focus
contract); content reflows to a single column with no loss at 320px width / 200% zoom, and a non-color
cue marks the active row.
```

### Change 4 — Mock-fidelity: one normative convention note (DRY) rather than per-story edits

**Rationale.** The readiness review recommended adding "spine wins; mock is illustrative only" to *each*
UI story. Repeating one sentence across ~15 stories is noisy and drift-prone. **Recommendation:** a single
authoritative, normative callout in the Overview that binds *every* UI story, backstopped by the existing
Story 1.9 a11y gate. (If you prefer the literal per-story phrasing instead, say so in review and I'll
fan it out.)

**INSERT — after the "Scope:" paragraph in `## Overview`, before `## Requirements Inventory`:**

```
> **Mockup fidelity (normative — applies to every UI story below).** The UX **spine
> (`EXPERIENCE.md`) and the resolved UX-DRs are authoritative**; the HTML mockups are
> **illustrative only** and may still contain pre-fix review violations (unlabeled typed-confirm,
> non-semantic consent toggle, default-On marketing, sub-13px microcopy). **Where any mockup
> conflicts with the spine, the spine wins.** Implement against the spine + UX-DRs; the Story 1.9
> a11y gate (bUnit + Playwright WCAG 2.2 AA) is the backstop that fails the build on any reintroduced
> defect. _(Resolves the readiness §4 mock-fidelity risk.)_
```

### Change 5 — Supporting cross-reference & header updates (consistency)

| Location | OLD | NEW |
|---|---|---|
| Story 1.4 closing note | "…AR-Gap-Binding, designed in Story 4.1; this story only consumes an existing claim." | "…AR-Gap-Binding — **decided** in Story 4.1 and **implemented** in Story 4.2; this story only consumes an existing claim, and its happy path is end-to-end verifiable once 4.2 lands." _(also addresses M2 sequencing clarity)_ |
| Epic 3 detail header | "…and the D7 backend (AR-Gap-D7)." | "…and the D7 EventStore contract — split into an approval-gated backend story (**3.5**) and the report UI that consumes it (**3.6**)." |
| Epic 4 detail header | "Includes the `party_id` binding-provisioning design + build (AR-Gap-Binding)…" | "Includes the `party_id` binding-provisioning **decision (4.1, ADR) + build (4.2)** (AR-Gap-Binding)…" |
| Epic Dependencies | (graph only) | append: "**Intra-epic sequencing:** Story 4.2 depends on Story 4.1 (binding decision/ADR); Story 3.6 depends on Story 3.5 (D7 backend contract, approval-gated)." |
| Frontmatter | `storyCount: 28` | `storyCount: 30` + one-line `correctCourse` note |

### Recorded decision — No-PRD single-source-of-truth risk (accepted)

**Decision:** Proceed on the brownfield **UX-as-requirements** basis. The same `EXPERIENCE.md` is both the
requirement and the design; nothing independently challenged the requirements capture. This residual
single-source risk is **consciously accepted** for this initiative — no PRD is commissioned. Mitigation:
the architecture independently re-derived the same FR/NFR taxonomy (internal-consistency check), and the
GO slices proceed under synthetic data. (No artifact edit; recorded here.)

---

## Section 5 — Implementation Handoff

- **Scope classification:** **Moderate** (backlog reorganization).
- **Primary executor:** **Product Owner / Developer** — apply Changes 1–5 to `epics.md`.
- **Follow-on owners:**
  1. **Architect / PO** — run the **Story 4.1 binding-mechanism spike** and record the ADR *before* any
     Epic 4–5 estimation (gates the Consumer half).
  2. **EventStore maintainer / approver** — open and approve the **Story 3.5 D7 contract** in parallel so
     it does not block the Story 3.6 report UI later.
  3. **Ops / release owner** — honor the **production-KMS gate** (Story 1.10 / AR-Gap-KMS) before any
     real EU PII; use synthetic data until then. _(No story change.)_
- **Success criteria:**
  - `epics.md` reflects 30 stories: Epic 3 = 3.1–3.6, Epic 4 = 4.1–4.5; phone-reflow AC on 2.3;
    mock-fidelity note present; all cross-references consistent; frontmatter `storyCount: 30`.
  - Epics 1–2 and Epic 3 (minus 3.5/3.6) remain **GO now**.
  - Epics 4–5 estimable **only after** the 4.1 ADR; Story 3.6 completable **only after** 3.5 approval.
- **Sequencing unchanged at the epic level:** `1 → {2, 4}` · `2 → 3` · `4 → 5`.

---

## Section 6 — Approval

- [x] **Approved** — Changes 1–5 applied to `epics.md` on 2026-06-09 (Change 4 = single normative note).
- [ ] ~~Revise~~

**Applied result (verified):** `epics.md` now has **30 stories** (`storyCount: 30`) — Epic 1: 1.1–1.10 ·
Epic 2: 2.1–2.5 · Epic 3: **3.1–3.6** · Epic 4: **4.1–4.5** · Epic 5: 5.1–5.4. Phone-reflow AC on Story
2.3, the normative mock-fidelity note in the Overview, the Story 1.4 cross-reference fix, the Epic 3/4
headers, and the intra-epic sequencing note are all in place.

_Routed to: Product Owner / Developer (edits applied ✓) + Architect (4.1 binding spike) + EventStore
approver (3.5 D7 contract)._
