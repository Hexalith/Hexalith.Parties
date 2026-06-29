---
project_name: parties
user_name: Administrator
date: 2026-06-29
workflow: bmad-correct-course
change_scope: moderate
status: approved-implemented
mode: batch
approved_by: Administrator
approved_at: 2026-06-29T07:30:25+02:00
trigger_report: _bmad-output/planning-artifacts/implementation-readiness-report-2026-06-29.md
requirements_basis: "Canonical PRD: _bmad-output/planning-artifacts/parties-ui-prd.md; final UX design set; architecture.md; epics.md; sprint-status.yaml"
---

# Sprint Change Proposal - Implementation Readiness Scope Hygiene

## 1. Issue Summary

The 2026-06-29 implementation-readiness assessment returned **NEEDS WORK** even
though product requirements are traceable and stable:

- PRD: 9/9 functional requirements covered.
- UX: present, final, and aligned with PRD and architecture.
- Architecture: supports the intended experience and already distinguishes Class A
  shared-anchor maintenance from Class B platform alignment.
- Implementation evidence: Epics 1-5 and their stories are marked `done`.

The readiness blocker is **scope hygiene in planning artifacts**, not missing
product scope or a failed implementation approach:

1. **Epic 7 must remain excluded from implementation-ready scope.** It is a
   deferred, architect-gated Class B platform-alignment placeholder with no
   implementation stories. Keeping `epic-7: backlog` in sprint status makes it look
   developer-executable.
2. **Epic 6 must be treated as maintenance/change-proposal scope.** It supports
   NFR9 and maintainability, but it covers no new PRD FRs and must not be reported
   as product feature delivery.
3. **Epic 2 story order must be normalized.** The text already declares the correct
   build order `2.1 -> 2.2 -> 2.3 -> 2.5 -> 2.4`, but `epics.md` and
   `sprint-status.yaml` still list Story 2.4 before Story 2.5.

Evidence: `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-29.md`
documents 1 critical violation, 3 major issues, and 3 minor concerns. The critical
and two active major issues are the three items above. Story 4.2 oversizing,
historical status notes in story specs, and Epic 1 enabler density are process
residuals that do not require rework of completed stories.

## 2. Impact Analysis

### Epic Impact

- **Epics 1-5:** Ready from a PRD feature-traceability perspective and already
  `done`. No scope, ordering, or acceptance-criteria changes are proposed.
- **Epic 2:** No scope or behavior change. Reorder the Story 2.5 section before
  Story 2.4 and list 2.5 before 2.4 in sprint status. Story IDs remain unchanged
  because implementation artifacts and completed evidence are keyed by those IDs.
- **Epic 6:** Keep as **conditional maintenance scope**, not PRD feature delivery.
  The story files can remain `ready-for-dev`, but the epic wording and sprint-status
  comments should make the maintenance classification explicit.
- **Epic 7:** Keep only as a deferred planning placeholder in `epics.md`. Remove it
  from `development_status` until PM/Architect planning produces approved
  implementation stories.

### Story Impact

- Existing completed story records are not changed.
- Story 2.5 remains `done`; Story 2.4 remains `done`.
- Only the presentation order changes so future humans/tools do not execute or reason
  about Story 2.4 before the picker work it references.
- No `7-*` story files should be created.

### Artifact Conflicts

- **PRD:** No change. The PRD already states Epic 6/7 cover no new PRD FRs and
  maps FRs only to Epics 1-5.
- **Architecture:** No change. `architecture.md` already records the Class A
  shared-anchor boundary and the Class B platform-consumption boundary.
- **UX:** No change. The final UX package remains authoritative and aligned.
- **Epics:** Needs scope classification and Epic 2 section-order cleanup.
- **Sprint status:** Needs Epic 2 ordering cleanup and removal of `epic-7: backlog`.
- **Readiness report:** Optionally add a disposition note after approval so the
  NEEDS WORK verdict is visibly routed through this proposal.

### Technical Impact

None. No code, public contract, deployment, EventStore submodule, DAPR ACL, data
model, or UI behavior changes are proposed.

## 3. Recommended Approach

**Selected path: Direct Adjustment.**

| Option | Verdict | Rationale |
|---|---|---|
| Direct Adjustment | **Chosen** | The issue is planning hygiene. Explicit classification plus order cleanup resolves the readiness blockers with low risk. |
| Rollback | Not viable | Completed Epics 1-5 are not the problem; reverting work would remove delivered value. |
| MVP Review | Not viable | MVP feature scope is fully covered by Epics 1-5. Epic 6 is maintenance; Epic 7 is deferred. No product scope reduction is needed. |

**Effort:** Low.
**Risk:** Low.
**Timeline impact:** One planning-artifact patch, then a readiness re-run.

## 4. Detailed Change Proposals

### 4.1 - `epics.md` frontmatter `correctCourse`

**Section:** YAML frontmatter.

OLD:

```yaml
correctCourse: '2026-06-09 - readiness course-correction: split Story 4.1 (decision spike + impl) and Story 3.5 (D7 backend + report UI); +phone-reflow AC on 2.3; +mock-fidelity rule. 2026-06-28 - readiness remediation for sprint-change-proposal-2026-06-28: add Epic 6 implementation-ready Class A consolidation stories, add Epic 7 deferred architecture placeholder, and pin A3/A8 decisions.'
```

NEW:

```yaml
correctCourse: '2026-06-09 - readiness course-correction: split Story 4.1 (decision spike + impl) and Story 3.5 (D7 backend + report UI); +phone-reflow AC on 2.3; +mock-fidelity rule. 2026-06-28 - readiness remediation for sprint-change-proposal-2026-06-28: add Epic 6 implementation-ready Class A consolidation stories, add Epic 7 deferred architecture placeholder, and pin A3/A8 decisions. 2026-06-29 - readiness scope hygiene: classify Epics 1-5 as PRD feature scope, Epic 6 as conditional maintenance scope, Epic 7 as excluded/deferred planning scope, and normalize Epic 2 Story 2.5 before Story 2.4.'
```

Rationale: Preserve history while recording the new readiness cleanup.

### 4.2 - `epics.md` scope classification section

**Section:** after `### FR Coverage Map`, before `## Epic List`.

OLD:

```text
_All 9 FRs mapped. NFRs (esp. NFR1 accessibility, NFR2 eventual-consistency, NFR4 GDPR
honesty) and the UX-DRs thread through every epic's stories; the shared enablers
(domain components, a11y gate, StatusKind->UI map, SignalR freshness) are established
once in Epic 1._

## Epic List
```

NEW:

```text
_All 9 FRs mapped. NFRs (esp. NFR1 accessibility, NFR2 eventual-consistency, NFR4 GDPR
honesty) and the UX-DRs thread through every epic's stories; the shared enablers
(domain components, a11y gate, StatusKind->UI map, SignalR freshness) are established
once in Epic 1._

### Implementation Scope Classification (2026-06-29 readiness)

- **PRD feature scope:** Epics 1-5. These cover all 9 PRD FRs and are the only
  product-feature epics used as MVP implementation-readiness evidence.
- **Conditional maintenance scope:** Epic 6. This is approved Class A
  in-repository consolidation supporting NFR9 and maintainability. It covers no new
  PRD FRs and must not be reported as product feature delivery.
- **Excluded deferred planning scope:** Epic 7. This is a Class B
  PM/Architect-gated platform-alignment placeholder. It is not implementation-ready,
  is not a dependency for Epic 6, and must not create `7-*` story files or sprint
  status entries until a separate approved plan exists.

## Epic List
```

Rationale: Makes readiness classification explicit before the epic list is consumed
by humans or automation.

### 4.3 - `epics.md` Epic 6 summary

**Section:** `### Epic 6: Internal Code Consolidation (Class A)`.

OLD:

```text
A maintainer can remove in-repo duplication without changing user-visible behavior, so
future features reuse one shared source for claim types, wire JSON, projection naming,
role/policy names, GDPR helper mappings, export filenames, and display helpers. This is
the approved Class A scope from `sprint-change-proposal-2026-06-28.md`.

**FRs covered:** no new PRD FRs. Supports NFR9 build/quality gates, architecture
maintainability, and the shared-anchor boundary in `architecture.md`.
```

NEW:

```text
A maintainer can remove in-repo duplication without changing user-visible behavior, so
future features reuse one shared source for claim types, wire JSON, projection naming,
role/policy names, GDPR helper mappings, export filenames, and display helpers. This is
the approved Class A scope from `sprint-change-proposal-2026-06-28.md`.

**Implementation classification:** conditional maintenance scope. Epic 6 is
developer-executable only as approved in-repository consolidation; it is not PRD
feature delivery and must not be used as evidence of additional functional coverage.

**FRs covered:** no new PRD FRs. Supports NFR9 build/quality gates, architecture
maintainability, and the shared-anchor boundary in `architecture.md`.
```

Rationale: Keeps Epic 6 available for maintenance delivery while preventing it from
being treated as a normal product epic.

### 4.4 - `epics.md` Epic 7 summary in the epic list

**Section:** `### Epic 7: Platform Alignment - adopt Commons/EventStore (Class B, deferred)`.

OLD:

```text
### Epic 7: Platform Alignment - adopt Commons/EventStore (Class B, deferred)

Architects and product leadership have a visible placeholder for the cross-repository
platform-alignment work, without handing it to development prematurely. This epic is
deferred and architect-gated: no implementation story files should be created until the
cross-submodule design, release sequencing, and generic-vs-domain split are approved.

**FRs covered:** no new PRD FRs. Supports long-term maintainability and platform
convergence only after PM/Architect approval.
```

NEW:

```text
### Deferred Platform Placeholder: Epic 7 - Platform Alignment (Class B, excluded)

Architects and product leadership have a visible placeholder for the cross-repository
platform-alignment work, without handing it to development prematurely. This entry is
**excluded from implementation-ready scope** and remains architect-gated: no `7-*`
implementation story files and no `epic-7` sprint-status entry should exist until the
cross-submodule design, release sequencing, and generic-vs-domain split are approved by
PM/Architect planning.

**FRs covered:** no new PRD FRs. Supports long-term maintainability and platform
convergence only after PM/Architect approval.
```

Rationale: Keeps the planning placeholder visible while removing any implication that
it is a backlog item ready for developer execution.

### 4.5 - `epics.md` Epic dependencies

**Section:** `### Epic Dependencies`.

OLD:

```text
`1 -> {2, 4}` . `2 -> 3` . `4 -> 5`. Epics 1-5 are complete and are prerequisites for
the post-MVP consolidation sweep in Epic 6. Epic 7 is not a developer dependency for Epic
6; it is a deferred platform-planning placeholder that requires PM/Architect approval
before implementation stories are created.
Current status note, 2026-06-27: `sprint-status.yaml` marks Epics 1-5 and all listed
stories as `done`. Historical sequencing remains: Story 4.2 (binding build) followed
Story 4.1 (binding decision / ADR), and Story 3.6 (verification report UI) followed
Story 3.5 (D7 backend contract). Do not treat those dependencies as active blockers
after the 2026-06-10 completed implementation story records.
```

NEW:

```text
`1 -> {2, 4}` . `2 -> 3` . `4 -> 5`. Epics 1-5 are complete PRD feature scope and are
prerequisites for the post-MVP maintenance sweep in Epic 6. Epic 6 is conditional
maintenance scope, not PRD feature delivery. Epic 7 is excluded from developer
execution: it is not a dependency for Epic 6, must not appear in `development_status`,
and requires PM/Architect approval before implementation stories are created.
Current status note, 2026-06-29: `sprint-status.yaml` marks Epics 1-5 and all listed
stories as `done`; Epic 6 story files are `ready-for-dev` as maintenance scope; Epic 7
is tracked only as a deferred planning placeholder. Historical sequencing remains:
Story 4.2 (binding build) followed Story 4.1 (binding decision / ADR), and Story 3.6
(verification report UI) followed Story 3.5 (D7 backend contract). Do not treat those
dependencies as active blockers after the 2026-06-10 completed implementation story
records.
```

Rationale: Aligns dependency language with readiness scope.

### 4.6 - `epics.md` Story 2 section order

**Section:** Epic 2 story sections.

OLD order:

```text
### Story 2.1: Embed the Admin area behind the Admin policy
### Story 2.2: Parties list with search, filters, and paging (FR-Admin-1)
### Story 2.3: Party detail (FR-Admin-2)
### Story 2.4: Create and edit a party with validation (FR-Admin-3)
### Story 2.5: Party picker re-skin + full WAI-ARIA combobox (D11 / UX-DR7)
```

NEW order:

```text
### Story 2.1: Embed the Admin area behind the Admin policy
### Story 2.2: Parties list with search, filters, and paging (FR-Admin-1)
### Story 2.3: Party detail (FR-Admin-2)
### Story 2.5: Party picker re-skin + full WAI-ARIA combobox (D11 / UX-DR7)
### Story 2.4: Create and edit a party with validation (FR-Admin-3)
```

Rationale: The epic-level build order already says 2.5 precedes 2.4. Reordering
the sections removes the remaining contradiction while preserving story IDs and
completed implementation evidence.

### 4.7 - `epics.md` Story 2.4 sequencing note

**Section:** Story 2.4 sequencing note.

OLD:

```text
_(Sequencing: this story's core create/edit acceptance is self-contained. Picker-backed **relationship linking** embeds the accessible party picker delivered by Story 2.5, so build 2.5 before 2.4's linking - or narrow 2.4 to author/validation only until 2.5 lands. The route id stays authoritative on edit regardless of picker availability.)_
```

NEW:

```text
_(Sequencing: this story's core create/edit acceptance is self-contained. Picker-backed **relationship linking** embeds the accessible party picker delivered by Story 2.5, which is intentionally listed immediately before this story. The route id stays authoritative on edit regardless of picker availability.)_
```

Rationale: Once the section order is corrected, the note should describe the resolved
ordering rather than a workaround.

### 4.8 - `epics.md` full Epic 7 heading and exclusion note

**Section:** full Epic 7 section near the end of the document.

OLD:

```text
## Epic 7: Platform Alignment - adopt Commons/EventStore (Class B, deferred)

Epic 7 is a deferred, architect-gated planning placeholder for moving generic technical
infrastructure toward shared Hexalith platform libraries. It is not implementation-ready
from this document alone, and no `7-*` implementation story files should be created until
PM/Architect planning expands and approves the work.
```

NEW:

```text
## Deferred Platform Planning Placeholder: Epic 7 - Platform Alignment (Class B, excluded)

Epic 7 is a deferred, architect-gated planning placeholder for moving generic technical
infrastructure toward shared Hexalith platform libraries. It is **excluded from
implementation-ready scope** from this document alone: no `7-*` implementation story
files and no `epic-7` sprint-status entry should be created until PM/Architect planning
expands and approves the work.
```

Rationale: Makes the full detailed section match the scope classification.

### 4.9 - `sprint-status.yaml` Epic 2 ordering

**Section:** Epic 2 status entries.

OLD:

```yaml
  2-3-party-detail-fr-admin-2: done
  2-4-create-and-edit-a-party-with-validation-fr-admin-3: done
  2-5-party-picker-re-skin-full-wai-aria-combobox-d11-ux-dr7: done
  epic-2-retrospective: done
```

NEW:

```yaml
  2-3-party-detail-fr-admin-2: done
  2-5-party-picker-re-skin-full-wai-aria-combobox-d11-ux-dr7: done
  2-4-create-and-edit-a-party-with-validation-fr-admin-3: done
  epic-2-retrospective: done
```

Rationale: Status order should match build/document order. No status value changes.

### 4.10 - `sprint-status.yaml` Epic 6 comments

**Section:** Epic 6 status comments.

OLD:

```yaml
  # In-repo deduplication; shared anchors land in Hexalith.Parties.Contracts
  # (+ a new Hexalith.Parties.Authentication lib for A3). No cross-repo work.
  # Readiness remediation 2026-06-28 added canonical epics.md detail and story files.
  epic-6: backlog
```

NEW:

```yaml
  # In-repo deduplication; shared anchors land in Hexalith.Parties.Contracts
  # (+ a new Hexalith.Parties.Authentication lib for A3). No cross-repo work.
  # Conditional maintenance scope only: supports NFR9/maintainability and covers
  # no new PRD functional requirements.
  # Readiness remediation 2026-06-28 added canonical epics.md detail and story files.
  epic-6: backlog
```

Rationale: Allows Epic 6 to stay executable while making the maintenance classification
clear.

### 4.11 - `sprint-status.yaml` Epic 7 exclusion

**Section:** Epic 7 status block.

OLD:

```yaml
  # -- Epic 7: Platform Alignment - adopt Commons/EventStore (Class B) ----
  # Deferred / Major / Architect-gated. Cross-repo moves into the technical
  # submodules; no developer-executable story entries until PM/Architect planning
  # expands and approves the work.
  epic-7: backlog
```

NEW:

```yaml
  # -- Epic 7: Platform Alignment - adopt Commons/EventStore (Class B) ----
  # Excluded from development_status until PM/Architect planning approves
  # target destinations, package/versioning, migration compatibility, and rollback.
  # Do not add epic-7 or 7-* keys here before an approved implementation plan exists.
```

Rationale: `backlog` is a developer-executable status in this file. Removing the key
keeps Epic 7 visible in comments while preventing automation from treating it as work
ready to start.

### 4.12 - `implementation-readiness-report-2026-06-29.md` disposition note

**Section:** frontmatter after `overallReadinessStatus: NEEDS WORK`.

OLD:

```yaml
overallReadinessStatus: NEEDS WORK
stepsCompleted:
```

NEW:

```yaml
overallReadinessStatus: NEEDS WORK
disposition:
  status: routed-to-correct-course
  via: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-29.md
  note: "Product scope remains ready for Epics 1-5. Correct course proposal addresses scope hygiene: Epic 7 excluded, Epic 6 maintenance-only, Epic 2 Story 2.5 ordered before Story 2.4. Report verdict is preserved until readiness is re-run."
stepsCompleted:
```

Rationale: Keeps the readiness report historically accurate while showing how the
NEEDS WORK verdict is being handled.

## 5. Implementation Handoff

**Scope classification:** Moderate. This is backlog/planning-artifact reorganization,
not code implementation.

**Recipients and responsibilities:**

- **Developer agent:** patched `epics.md`, `sprint-status.yaml`, and the readiness
  report exactly as approved; preserved all story IDs and statuses except removing
  the `epic-7` key.
- **Product Owner / Developer:** treat Epic 6 as approved Class A maintenance only.
  Do not use it as PRD feature-scope evidence.
- **Product Manager / Architect:** own any future Epic 7 planning. No developer story
  creation until target destinations, package/versioning, migration compatibility,
  and rollback are approved.

**Implementation tasks completed after approval:**

1. Patch `epics.md` with scope classification, Epic 6 maintenance language, Epic 7
   exclusion language, and Story 2.5-before-2.4 ordering.
2. Patch `sprint-status.yaml` so Story 2.5 is listed before Story 2.4, Epic 6 comments
   identify maintenance scope, and `epic-7: backlog` is removed.
3. Patch the readiness report disposition note.
4. Re-run implementation readiness and expect the critical Epic 7 issue, Epic 6
   product-epic framing issue, and Epic 2 ordering issue to clear.

**Success criteria:**

- Epics 1-5 remain the only PRD feature readiness evidence.
- Epic 6 remains executable only as maintenance scope.
- Epic 7 is absent from developer-executable sprint status and has no `7-*` stories.
- Epic 2 document/status order matches declared build order: `2.1 -> 2.2 -> 2.3 -> 2.5 -> 2.4`.
- No story IDs are renumbered.
- No code, architecture, PRD, or UX behavior changes are introduced.

## 6. Approval and Routing

- **Approved by:** Administrator
- **Approval date:** 2026-06-29
- **Approved scope:** Moderate planning-artifact reorganization.
- **Implementation completed:** `epics.md`, `sprint-status.yaml`, and
  `implementation-readiness-report-2026-06-29.md` were patched after approval.
- **Routed to:** Developer agent for document patching; Product Owner / Developer for
  Epic 6 maintenance execution discipline; Product Manager / Architect for any future
  Epic 7 planning.

## Checklist Summary

- [x] 1.1 Triggering issue identified: 2026-06-29 implementation-readiness report.
- [x] 1.2 Core problem defined: planning scope hygiene and story-order mismatch,
  category = misunderstanding/imprecision in planning artifacts.
- [x] 1.3 Evidence gathered: readiness report, PRD, epics, architecture, final UX
  package, sprint-status.yaml, and relevant Story 2.4/2.5 implementation records.
- [x] 2.1 Current epic containing trigger: Epic 7 in readiness scope / sprint status;
  cannot be completed as originally implied because it is deferred and architect-gated.
- [x] 2.2 Epic-level changes: classify Epics 1-5 as PRD feature scope, Epic 6 as
  conditional maintenance, Epic 7 as excluded/deferred planning.
- [x] 2.3 Remaining epics reviewed: Epic 2 ordering cleanup; Epics 1, 3, 4, 5 no
  change; Epic 6 maintenance-only; Epic 7 excluded.
- [x] 2.4 No new epics required; Epic 7 remains a placeholder only.
- [x] 2.5 Epic/story order adjustment required for Epic 2 without renumbering.
- [x] 3.1 PRD conflict: none.
- [x] 3.2 Architecture conflict: none; current boundaries already support Class A and
  Class B distinctions.
- [x] 3.3 UI/UX conflict: none; final UX remains authoritative.
- [x] 3.4 Secondary artifacts: sprint-status.yaml and readiness-report disposition.
- [x] 4.1 Direct Adjustment selected as viable; effort low, risk low.
- [N/A] 4.2 Rollback rejected; no completed work is invalid.
- [N/A] 4.3 MVP review rejected; MVP feature scope remains fully covered.
- [x] 4.4 Recommended path documented.
- [x] 5.1 Issue summary created.
- [x] 5.2 Epic impact and artifact adjustment needs documented.
- [x] 5.3 Recommended path and rationale documented.
- [x] 5.4 MVP impact: no MVP scope change; action plan and sequencing documented.
- [x] 5.5 Handoff plan documented.
- [x] 6.1 Checklist completion reviewed; action-needed items were approval and post-approval patching.
- [x] 6.2 Proposal accuracy reviewed against artifacts.
- [x] 6.3 User approval received from Administrator on 2026-06-29.
- [x] 6.4 sprint-status.yaml updated: Story 2.5 ordered before 2.4, Epic 6 labeled maintenance scope, and `epic-7` removed from development status.
- [x] 6.5 Final handoff plan confirmed: Epic 6 remains maintenance scope; Epic 7 routes to PM/Architect before any implementation stories.
