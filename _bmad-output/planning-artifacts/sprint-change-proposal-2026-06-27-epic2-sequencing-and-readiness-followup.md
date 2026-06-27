---
project_name: parties
user_name: Administrator
date: 2026-06-27
workflow: bmad-correct-course
change_scope: moderate
status: approved-implemented
mode: batch
trigger_report: _bmad-output/planning-artifacts/implementation-readiness-report-2026-06-27.md
supersedes: none
relatedTo: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-27-readiness-reconciliation.md
requirements_basis: "Canonical PRD: _bmad-output/planning-artifacts/parties-ui-prd.md (extracted 2026-06-27 from brownfield docs/, the ux-parties-2026-06-09 design set, and architecture.md's FR/NFR inventory)."
---

# Sprint Change Proposal — Epic 2 AC Sequencing & Readiness Follow-up

## 1. Issue Summary

The 2026-06-27 implementation-readiness **re-run**
(`implementation-readiness-report-2026-06-27.md`) returned **NEEDS WORK** with
**0 critical, 2 major, 3 minor, 3 warnings/residuals, 2 validation limitations**.

This re-run already reflects the prior course-correction
(`sprint-change-proposal-2026-06-27-readiness-reconciliation.md`, status
`approved-implemented`), which created the canonical PRD, added the UX `index.md`,
and reconciled the Story 1.4 / 3.5 / 3.6 / 4.1 / 4.2 dependency/status wording.
Coverage is clean: the PRD exists, all 9 FRs are covered by Epics 1–5, and UX
aligns with PRD and Architecture. Every epic and story is `done` in
`_bmad-output/implementation-artifacts/sprint-status.yaml`.

The reason the verdict is still NEEDS WORK is **planning-artifact quality**, not
requirements coverage and not unimplemented work. The one genuinely-new, actionable
finding is:

- **M1 — Epic 2 contains forward-linked story acceptance criteria.** A story can be
  marked done while its acceptance text depends on a surface a *later* story owns.
  Specifically: Story 2.2 row-activation lands on detail owned by Story 2.3; Story
  2.3 exposes Edit/GDPR entry points owned by Story 2.4 and Epic 3 / Story 3.1; and
  Story 2.4 relationship-linking embeds the accessible picker delivered by Story 2.5.

The remaining findings are residual or already mitigated:

- **M2 (Story 4.2 oversized)** — already complete and reviewed; do not retroactively
  split. Lesson already recorded by the prior reconciliation. No new action.
- **m1 (Epic 1 enabler-heavy)** — framing/status-reporting nudge only.
- **m2 (historical forward references in story notes)** — Story 1.4 and Story 3.6
  already carry resolving status notes; the only residue is a stale finding-number
  tag on Story 4.1.
- **m3 (Story 4.1 still reads as a "decision spike / decision artifact only")** — the
  prior pass prepended a status note but left the *title* and AC wording as spike
  language, so the re-run re-flagged it.
- **Warnings** — architecture still carries three stale "no formal PRD" strings
  (frontmatter + two body lines); UX nested-folder discovery; Blazor Server scaling
  (future); TD-1 (mitigated, non-blocking).
- **2 validation limitations** — Playwright could not bind Kestrel in the sandbox;
  `scripts/test.ps1 -Lane unit` printed opaque post-restore build-failed lines while
  returning exit code 0. These are environment/test follow-ups, not planning edits.

**Context of discovery.** Surfaced by the automated readiness assessor
(`bmad-check-implementation-readiness`) reconciling planning text against
`sprint-status.yaml` and completed implementation story records.

## 2. Impact Analysis

### Epic Impact

- **Epic 2 — Admin Party Records Management:** No scope or implementation change.
  The epic outcome (search/view/create/edit/link) is delivered and `done`. The defect
  is *AC ownership clarity*: several stories' acceptance text reads as if it owns a
  surface a sibling story owns. Remediation is documentation-only — clarify ownership
  and state an explicit build order — with **no story renumbering** (numbers are load
  bearing for `sprint-status.yaml`).
- **Epic 1:** No change. Add a one-line "user outcome" framing so status reporting
  foregrounds sign-in / role landing / NoPartyBinding / self-scope / freshness / a11y
  rather than "infrastructure complete" (m1).
- **Epic 4:** No change. Retitle Story 4.1 from "design spike" to "completed discovery
  prerequisite" and de-spike its closing AC so it is not counted as Phase 4 capacity
  (m3); fix its stale `(Resolves readiness finding M1)` tag (m2 residue).
- **Epics 3 & 5:** No change. Story 3.5/3.6 D7 wording already reconciled.

### Story Impact

Implementation story records remain the source of truth for completion. Only the
**planning** epic file (`epics.md`) is edited, as a readiness surface. Affected
stories: **2.2, 2.3, 2.4** (AC-ownership notes + epic-level build order), **4.1**
(retitle/de-spike/tag fix), **Epic 1 intro** (user-outcome framing). No ACs are
deleted; ownership clarifications are additive.

### Artifact Conflicts

- `epics.md` — Epic 2 forward-linked ACs (M1); Story 4.1 spike wording (m3); Epic 1
  framing (m1).
- `architecture.md` — three stale "no formal PRD" strings (frontmatter line 48; body
  lines 67–68 and 74). The readiness/handoff status block (line 826) is already
  reconciled and is **left as is**.
- `implementation-readiness-report-2026-06-27.md` — add a `disposition` note so the
  residuals are visibly tracked by this proposal, not mistaken for open blockers.
  The `needs-work` verdict itself is **kept** (accurate as a planning-hygiene state).

### Technical Impact

**None.** No code, infrastructure, deployment, EventStore submodule, DAPR ACL, or UX
behavior change. This is a planning/readiness traceability correction over completed
work.

## 3. Recommended Approach

**Direct Adjustment** (Option 1).

- **Rollback (Option 2): Not viable / rejected.** Epics 1–5 are complete and reviewed;
  reverting clarity-only defects would destroy shipped value.
- **MVP Review (Option 3): Not viable / rejected.** MVP scope is intact and fully
  covered; nothing to reduce or redefine.
- **Direct Adjustment (Option 1): Viable — selected.** The defects are documentation
  clarity. Clarify AC ownership, state an explicit build order, de-spike Story 4.1,
  retire stale architecture PRD wording, and annotate the report's disposition.

Crucially, fix sequencing **without renumbering**: change the *build order* and the
*ownership annotations*, not the story IDs. Renumbering would desynchronize
`sprint-status.yaml` and the completed implementation records for zero benefit.

- **Effort:** Low.
- **Risk:** Low (documentation churn only). Control: additive ownership notes; never
  rewrite or delete completed-work ACs.
- **Timeline impact:** One planning-cleanup pass, then a readiness re-run.

## 4. Detailed Change Proposals

> All edits are additive clarifications. Story numbers are preserved.

### 4.1 — `epics.md` · Epic 2 heading: AC-ownership & build-order note (M1)

**Section:** `## Epic 2: Admin — Party Records Management` (after the intro paragraph).

**OLD**

```text
An admin/tenant-owner can search, filter, view, create, edit, and link Person &
Organization records within their tenant. **Covers FR-Admin-1, FR-Admin-2, FR-Admin-3**;
delivers D11/UX-DR7 and threads NFR1/NFR2/NFR5. All work lives in `AdminPortal` + `Picker`.
```

**NEW**

```text
An admin/tenant-owner can search, filter, view, create, edit, and link Person &
Organization records within their tenant. **Covers FR-Admin-1, FR-Admin-2, FR-Admin-3**;
delivers D11/UX-DR7 and threads NFR1/NFR2/NFR5. All work lives in `AdminPortal` + `Picker`.

**AC ownership & build order (resolves readiness finding M1 — forward-linked ACs).**
Each story's acceptance is verified by the surface that story *owns*; a reference to
another story's surface is a navigation/binding **affordance**, not acceptance of that
target. Build order within this epic: **2.1 → 2.2 → 2.3 → 2.5 → 2.4**.
- **2.2** owns the list and the row-activation **trigger** (route intent to
  `/admin/parties/{id}`); the Party Detail surface it lands on is owned by **2.3**.
- **2.3** owns Party Detail and exposes **Edit** and **GDPR** entry **affordances**;
  the Edit form is owned by **2.4** and the GDPR page is owned by **Epic 3 / Story 3.1**.
- **2.4** owns Create/Edit; its picker-backed **relationship linking** depends on the
  accessible picker delivered by **2.5**, so 2.5 ships before 2.4 — or 2.4 ships
  without relationship linking until 2.5 lands.
```

**Rationale:** States ownership and order once, at the epic level, so each story can be
completed and reviewed against only what it owns.

---

### 4.2 — `epics.md` · Story 2.2 ownership note (M1)

**Section:** Story 2.2 final `**And**` acceptance line.

**OLD**

```text
**And** erased parties are excluded or shown only as an erased status; arrow-key row navigation + `Enter` opens detail; type-ahead focuses search.
```

**NEW**

```text
**And** erased parties are excluded or shown only as an erased status; arrow-key row navigation + `Enter` opens detail; type-ahead focuses search. _(Sequencing: this story owns the list and the row-activation trigger to `/admin/parties/{id}`; the Party Detail surface that route lands on is owned by Story 2.3. Acceptance here verifies that activation issues the correct navigation, not the rendered detail.)_
```

---

### 4.3 — `epics.md` · Story 2.3 ownership note (M1)

**Section:** Story 2.3 first `**Then**` acceptance line.

**OLD**

```text
**Then** the full `PartyDetail` renders with the party-state badge and freshness indicator, and entry buttons to **Edit** and **GDPR**.
```

**NEW**

```text
**Then** the full `PartyDetail` renders with the party-state badge and freshness indicator, and entry buttons to **Edit** and **GDPR**. _(Sequencing: the Edit/GDPR controls are navigation affordances owned here; the Edit form they open is owned by Story 2.4 and the GDPR page is owned by Epic 3 / Story 3.1. Acceptance here verifies the affordances route correctly, not the target surfaces.)_
```

---

### 4.4 — `epics.md` · Story 2.4 ownership note (M1)

**Section:** after Story 2.4's final `**Then**` acceptance line.

**OLD**

```text
**Then** the view reflects the change **optimistically** with a `role=status` "Saved — updating…" and reconciles silently on projection confirm.
```

**NEW**

```text
**Then** the view reflects the change **optimistically** with a `role=status` "Saved — updating…" and reconciles silently on projection confirm.

_(Sequencing: this story's core create/edit acceptance is self-contained. Picker-backed **relationship linking** embeds the accessible party picker delivered by Story 2.5, so build 2.5 before 2.4's linking — or narrow 2.4 to author/validation only until 2.5 lands. The route id stays authoritative on edit regardless of picker availability.)_
```

---

### 4.5 — `epics.md` · Story 4.1 retitle + de-spike + tag fix (m3, m2)

**Section:** Story 4.1 heading and closing `**And**` line.

**OLD (heading)**

```text
### Story 4.1: Decide the Consumer identity → `party_id` binding mechanism (design spike → ADR)
```

**NEW (heading)**

```text
### Story 4.1: Decide the Consumer identity → `party_id` binding mechanism (completed discovery prerequisite)
```

**OLD (closing line)**

```text
**And** this is a **decision spike**, not an implementation story; it produces a decision artifact only and is the **predecessor of Story 4.2** and all of Epics 4–5. _(Resolves readiness finding M1.)_
```

**NEW (closing line)**

```text
**And** this is a **completed discovery prerequisite** (not Phase 4 implementation capacity); it produced the accepted ADR and is the **predecessor of Story 4.2** and all of Epics 4–5. List it separately from implementation stories in readiness/capacity views. _(Resolves readiness finding m3 — Story 4.1 classification; corrects a stale `M1` tag.)_
```

**Rationale:** The existing status note above the story already says "completed"; this
aligns the title and AC so the readiness assessor stops counting it as an active spike,
and repairs the finding-number tag that no longer matches the current report.

---

### 4.6 — `epics.md` · Epic 1 user-outcome framing (m1)

**Section:** `## Epic 1: App Foundation & Secure Sign-In` (after the intro paragraph).

**OLD**

```text
Stand up the `parties-ui` host and deliver secure sign-in with role-based landing, plus
the shared security, freshness, accessibility, and shell foundation every later epic
consumes. **Covers FR-Shell** and establishes AR-Starter, D1, D2, D3, D5, D6, D10,
AR-StatusMap, UX-DR1/2/3/4/5/6/8/10/12, and the D9 a11y gate.
```

**NEW**

```text
Stand up the `parties-ui` host and deliver secure sign-in with role-based landing, plus
the shared security, freshness, accessibility, and shell foundation every later epic
consumes. **Covers FR-Shell** and establishes AR-Starter, D1, D2, D3, D5, D6, D10,
AR-StatusMap, UX-DR1/2/3/4/5/6/8/10/12, and the D9 a11y gate.

**User outcome (report this, not "infrastructure complete" — resolves readiness finding m1).**
The epic is *done* when a user can sign in, land in the correct role area, be sent
fail-closed to `NoPartyBinding` when unbound, operate only on their own scope, see
freshness state, and pass the WCAG 2.2 AA a11y gate. Stories 1.1, 1.6, 1.8, 1.9, and
1.10 are enablers of that outcome, not the outcome itself.
```

---

### 4.7 — `architecture.md` · retire stale "no formal PRD" wording (warning)

**Spot A — frontmatter `requirementsBasis` (line 48)**

**OLD**

```text
requirementsBasis: 'Brownfield docs/ + UX design (no formal PRD exists — per docs/index.md brownfield note).'
```

**NEW**

```text
requirementsBasis: 'Canonical PRD: _bmad-output/planning-artifacts/parties-ui-prd.md (extracted 2026-06-27 from brownfield docs/, the ux-parties-2026-06-09 design set, and this architecture FR/NFR inventory).'
```

**Spot B — body "Requirements basis" (lines 67–68)**

**OLD**

```text
**Requirements basis:** brownfield `docs/` set + the UX design (no formal PRD;
following the brownfield note in `docs/index.md`).
```

**NEW**

```text
**Requirements basis:** the canonical PRD
`_bmad-output/planning-artifacts/parties-ui-prd.md` (extracted 2026-06-27 from the
brownfield `docs/` set, the UX design, and this document's FR/NFR inventory). This
architecture predates the PRD (architecture dated 2026-06-09); the PRD consolidates
the same brownfield + UX basis into a canonical requirements source.
```

**Spot C — "Functional Requirements" header (line 74)**

**OLD**

```text
**Functional Requirements (derived from UX `EXPERIENCE.md` — no formal PRD):**
```

**NEW**

```text
**Functional Requirements (derived from UX `EXPERIENCE.md`; now consolidated in the canonical PRD `parties-ui-prd.md`):**
```

**Rationale:** The reconciliation already created the PRD and fixed the architecture
*handoff* status; these three remaining strings are the deferred residue that the
warning calls out. The historical 2026-06-09 date is preserved as provenance.

---

### 4.8 — `implementation-readiness-report-2026-06-27.md` · disposition note (report handling)

**Section:** frontmatter, after `issueCounts`. The `overallReadinessStatus: needs-work`
verdict is **kept** — it is an accurate planning-hygiene state, not superseded.

**OLD**

```text
issueCounts:
  critical: 0
  major: 2
  minor: 3
  warnings: 3
  validationLimitations: 2
---
```

**NEW**

```text
issueCounts:
  critical: 0
  major: 2
  minor: 3
  warnings: 3
  validationLimitations: 2
disposition:
  status: residuals-tracked
  via: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-27-epic2-sequencing-and-readiness-followup.md
  note: "needs-work verdict accepted as a planning-hygiene state. M1 (Epic 2 forward-linked ACs) remediated as AC-ownership clarifications; M2 + minors + warnings reconciled or tracked as residuals; the 2 validation limitations are environment/test follow-ups. Epics 1-5 are `done` in sprint-status.yaml; no code/behavior change."
---
```

## 5. Implementation Handoff

**Scope classification: Moderate** — backlog/planning-artifact reorganization, no code.

**Recipients & responsibilities:**

- **Developer agent** — apply edits 4.1–4.8 to `epics.md`, `architecture.md`, and the
  readiness report after approval; verify story numbers are unchanged.
- **Product Owner / PM** — confirm the no-renumber decision and that completed Story
  4.2 is not retroactively split.
- **Architect** — confirm the architecture PRD-basis rewording preserves provenance and
  does not hide remaining future enhancements (production KMS, gateway self-principal,
  Blazor Server scaling).
- **Test/Dev (validation follow-ups, separate from doc edits)** — (1) re-run the
  Playwright route/a11y suite where Kestrel can bind locally; (2) investigate the opaque
  `scripts/test.ps1 -Lane unit` post-restore build-failed output despite exit code 0.

**Implementation tasks (on approval):**

1. Patch `epics.md`: Epic 2 ownership/build-order note + Stories 2.2/2.3/2.4 notes (M1).
2. Patch `epics.md`: Story 4.1 retitle/de-spike/tag fix (m3/m2) and Epic 1 user-outcome
   framing (m1).
3. Patch `architecture.md`: three stale "no formal PRD" strings (warning).
4. Patch the readiness report frontmatter: `disposition` note (report handling).
5. Re-run implementation readiness with `sprint-status.yaml` + implementation artifacts
   included; expect M1 cleared and the minors/warnings closed or down to accepted
   residuals.

**Success criteria:**

- Each Epic 2 story's acceptance is verifiable against only the surface it owns; the
  epic states an explicit build order.
- Readiness no longer flags Story 4.1 as an active spike or Epic 1 as "infrastructure
  only"; no stale finding-number tags remain.
- Architecture no longer asserts "no formal PRD."
- The readiness report visibly records its disposition.
- No code, deployment, EventStore submodule, DAPR ACL, or UX behavior changes; no story
  renumbering; `sprint-status.yaml` untouched.

## Checklist Summary

- [x] 1.1 Triggering issue identified: 2026-06-27 readiness re-run, finding M1.
- [x] 1.2 Core problem defined: Epic 2 forward-linked story ACs (plus minor/residual
  planning-hygiene items); category = misread/imprecise planning text over completed
  work, not a technical limitation.
- [x] 1.3 Supporting evidence gathered: readiness report, epics.md (Epic 2, Stories
  2.2–2.5, 4.1, Epic 1 intro), architecture.md (lines 48/67/74/826), sprint-status.yaml,
  prior reconciliation proposal.
- [x] 2.1 Epic 2 completable as-is; defect is AC clarity, not scope.
- [x] 2.2 Epic-level change = additive AC-ownership note + build order; no scope change.
- [x] 2.3 Remaining epics reviewed (1 framing, 4 retitle); 3 & 5 unaffected.
- [x] 2.4 No epic obsoleted; no new epic required.
- [x] 2.5 No renumbering; build order stated without changing story IDs.
- [x] 3.1 PRD conflict: none (PRD exists and is canonical); architecture PRD wording
  retired instead.
- [x] 3.2 Architecture conflict: three stale "no formal PRD" strings to update.
- [x] 3.3 UX conflict: none; design content final and aligned.
- [x] 3.4 Secondary artifact: readiness report disposition note; validation follow-ups
  logged for Test/Dev.
- [x] 4.1 Direct Adjustment selected as viable.
- [N/A] 4.2 Rollback rejected; no completed work reverted.
- [N/A] 4.3 MVP Review rejected; MVP intact and fully covered.
- [x] 4.4 Recommended path: Direct Adjustment (documentation-only, no renumbering).
- [x] 5.1 Issue summary created.
- [x] 5.2 Epic impact + artifact needs documented.
- [x] 5.3 Recommended path + rationale documented.
- [x] 5.4 MVP unaffected; action plan + sequencing documented.
- [x] 5.5 Handoff plan documented (Dev / PO-PM / Architect / Test).
- [x] 6.3 User approval received (Administrator, 2026-06-27): "Approve & apply all 8"; edits 4.1–4.8 applied.
- [N/A] 6.4 sprint-status.yaml update not required (no epics/stories added, removed, or
  renumbered).
- [x] 6.5 Next steps + success criteria defined.
