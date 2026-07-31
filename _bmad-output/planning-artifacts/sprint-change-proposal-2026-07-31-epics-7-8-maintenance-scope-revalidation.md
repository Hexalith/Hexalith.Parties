---
project: parties
date: 2026-07-31
workflow: bmad-correct-course
mode: batch
status: proposed
change_scope: minor
approval: pending
---

# Sprint Change Proposal — Epics 7 and 8 Maintenance-Scope Revalidation

## 1. Issue Summary

The planning corpus must keep Epics 7 and 8 explicitly outside PRD functional
coverage. Both epics are post-MVP maintenance work: Epic 7 is completed
platform-alignment maintenance, and Epic 8 is domain-focus refactoring and
platform-extraction maintenance. Neither epic introduces, covers, or changes a
PRD functional requirement.

The approved 2026-07-16 course correction established this invariant in the
canonical PRD, canonical epics document, and the completed 2026-07-07 readiness
assessment. A later readiness draft dated 2026-07-17 contains only its initial
header and does not repeat the boundary. If that draft is later completed without
the invariant, readiness totals could again misclassify Epics 7 or 8 as product
feature coverage.

Evidence:

- The PRD assigns all nine functional requirements to Epics 1-5 and explicitly
  excludes Epics 7 and 8 from new PRD functional coverage.
- `epics.md` contains the shared invariant and separately states that Epics 7 and
  8 cover no new PRD functional requirements.
- The completed 2026-07-07 readiness report contains a prominent scope boundary
  excluding both epics from MVP and product-feature functional coverage.
- Epic 7 final readiness says Epic 7 adds no PRD functional requirement coverage.
- The 2026-07-17 readiness draft was created after the prior correction and lacks
  the shared scope boundary.

## 2. Impact Analysis

### Epic and Story Impact

- Epic 7 remains completed post-MVP platform maintenance.
- Epic 8 remains post-MVP domain-focus maintenance.
- No epic or story scope, acceptance criterion, dependency, sequence, priority,
  or status changes.
- No new epic or story is required.

### Artifact Impact

- PRD: no edit required; the canonical invariant is already explicit.
- Epics: no edit required; the canonical invariant and both epic classifications
  are already explicit.
- Readiness: add the shared boundary to the 2026-07-17 draft so every current
  readiness assessment begins with the same classification rule.
- Historical readiness reports remain unchanged as historical records.
- Architecture, UX, source code, infrastructure, build, deployment, tests, and
  `sprint-status.yaml` are unaffected.

### Technical Impact

None. This is documentation-only scope hygiene and does not change implemented
behavior, release gates, or validation lanes.

## 3. Recommended Approach

Use **Direct Adjustment**: add one scope-boundary paragraph to the current
2026-07-17 readiness draft and revalidate the existing PRD and epics wording.

- Effort: Low
- Risk: Low
- Timeline impact: None beyond documentation review
- MVP impact: None; Epics 1-5 remain the complete nine-FR feature baseline
- Rollback: Revert the single readiness wording edit

Rollback of completed implementation is not viable or useful because no behavior
caused the issue. PRD MVP review is unnecessary because the functional baseline
does not change.

## 4. Detailed Change Proposals

### PRD

**Artifact:** `_bmad-output/planning-artifacts/parties-ui-prd.md`

No edit proposed. The existing `Current Implementation Evidence` section already
states:

> **Scope invariant:** Epics 7 and 8 are maintenance scope only. Neither epic
> introduces or covers a new PRD functional requirement, and neither may be
> counted as MVP or product-feature functional coverage.

### Epics

**Artifact:** `_bmad-output/planning-artifacts/epics.md`

No edit proposed. The existing `Implementation Scope Classification` section
already states:

> **Scope invariant:** Epics 7 and 8 are post-MVP maintenance scope. Neither epic
> introduces or covers a new PRD functional requirement, and neither may be used
> as MVP or product-feature functional-coverage evidence.

### Implementation Readiness

**Artifact:**
`_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-17.md`
**Section:** report header

OLD:

> **Date:** 2026-07-17
> **Project:** parties

NEW:

> **Date:** 2026-07-17
> **Project:** parties
>
> **Scope boundary:** Epics 7 and 8 are maintenance scope only. Neither epic adds
> or covers a new PRD functional requirement. Readiness totals must exclude both
> from MVP and product-feature functional coverage and assess them only on the
> maintenance/platform track.

Rationale: this is the exact symmetric wording already approved in the completed
2026-07-07 readiness assessment. Reusing it prevents later readiness work from
counting either maintenance epic as functional coverage.

### Architecture and UX

No edits proposed. The classification does not change system design, user flows,
screens, components, or accessibility requirements.

## 5. Implementation Handoff

**Classification:** Minor
**Recipient:** Developer/documentation maintainer
**Status:** Pending approval

Responsibilities after approval:

1. Apply only the proposed wording to the 2026-07-17 readiness draft.
2. Preserve all epic/story scope, IDs, sequencing, and statuses.
3. Verify the same invariant remains present in the PRD, epics, and current
   readiness documents.
4. Leave `sprint-status.yaml` unchanged because no backlog item changes.

Success criteria:

- [x] The PRD explicitly classifies Epics 7 and 8 as maintenance with no new PRD
  functional coverage.
- [x] The epics document explicitly classifies Epics 7 and 8 as maintenance with
  no new PRD functional coverage.
- [ ] The 2026-07-17 readiness draft contains the symmetric scope boundary.
- [ ] A corpus check confirms neither epic can be counted as MVP or
  product-feature functional coverage.
- [x] Epics 1-5 remain the complete nine-FR feature baseline.

## Change Navigation Checklist Record

### 1. Understand the Trigger and Context

- [N/A] 1.1 No triggering implementation story; this is planning-scope hygiene.
- [x] 1.2 Core problem defined as readiness-document drift after the prior
  approved correction.
- [x] 1.3 Evidence collected from the PRD, epics, completed readiness assessment,
  Epic 7 final readiness, prior correction, and later readiness draft.

### 2. Epic Impact Assessment

- [x] 2.1 Epics 7 and 8 remain completable under their approved maintenance scope.
- [x] 2.2 Wording clarification only; no epic or acceptance-criteria change.
- [x] 2.3 Remaining epics and dependencies reviewed; no change required.
- [N/A] 2.4 No epic invalidated and no new epic required.
- [N/A] 2.5 No priority or sequencing change.

### 3. Artifact Conflict and Impact Analysis

- [x] 3.1 PRD functional scope remains Epics 1-5; no PRD edit required.
- [N/A] 3.2 No architecture impact.
- [N/A] 3.3 No UI/UX impact.
- [x] 3.4 The later readiness draft requires the established scope boundary; all
  technical artifacts remain unchanged.

### 4. Path Forward Evaluation

- [x] 4.1 Direct Adjustment is viable: low effort, low risk, no timeline impact.
- [N/A] 4.2 Rollback of completed work is unnecessary.
- [N/A] 4.3 PRD MVP review is unnecessary.
- [x] 4.4 Direct Adjustment selected and proposed.

### 5. Sprint Change Proposal Components

- [x] 5.1 Issue summary complete.
- [x] 5.2 Epic and artifact impacts documented.
- [x] 5.3 Recommended path and alternatives documented.
- [x] 5.4 MVP impact and action plan documented.
- [x] 5.5 Minor-scope Developer/documentation handoff defined.

### 6. Final Review and Handoff

- [x] 6.1 Applicable checklist items addressed.
- [x] 6.2 Proposal checked for consistency with the approved 2026-07-16 change.
- [!] 6.3 Explicit Administrator approval required before implementation.
- [N/A] 6.4 No `sprint-status.yaml` update required.
- [!] 6.5 Handoff completes after approval, edit, and corpus validation.

