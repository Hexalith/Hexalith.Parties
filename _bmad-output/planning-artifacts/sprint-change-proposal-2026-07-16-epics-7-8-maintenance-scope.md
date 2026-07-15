---
project: parties
date: 2026-07-16
workflow: bmad-correct-course
mode: batch
status: implemented
change_scope: minor
approval: approved
approved_by: Administrator
approved_on: 2026-07-16
---

# Sprint Change Proposal — Epic 7 and Epic 8 Maintenance-Scope Clarity

## 1. Issue Summary

The planning corpus must keep Epics 7 and 8 explicitly outside PRD functional
coverage. Both epics are post-MVP maintenance work: Epic 7 is platform-alignment
maintenance, and Epic 8 is domain-focus refactoring and platform extraction.
Neither epic introduces, covers, or changes a PRD functional requirement.

The artifacts already preserved this intent, but not always with identical
wording. Epic 7 commonly used the exact phrase "no new PRD functional requirement
coverage," while some Epic 8 passages said only that it must not be reported as
product-feature coverage. That wording was directionally correct but left room
for a readiness tool or reviewer to count Epic 8 as additional functional
coverage.

Evidence:

- The PRD assigns all nine functional requirements to Epics 1-5.
- `epics.md` already stated separately that Epics 7 and 8 cover no new PRD FRs.
- The 2026-07-07 readiness report identified scope hygiene as the main readiness
  risk and excluded Epics 6-8 from product-feature FR coverage.
- Epic 7 final readiness says Epic 7 adds no PRD functional requirement coverage.
- The Epic 8 architecture spine says Epic 8 adds zero PRD functional requirements.

## 2. Impact Analysis

### Epic and Story Impact

- Epic 7 remains completed post-MVP platform maintenance.
- Epic 8 remains post-MVP domain-focus maintenance.
- No epic/story scope, acceptance criterion, dependency, sequence, priority, or
  status changed.
- No new epic or story was required.

### Artifact Impact

- PRD: added a shared invariant and made the Epic 8 bullet use exact PRD
  functional-coverage wording.
- Epics: added a shared invariant at the implementation-scope classification.
- Readiness: added a prominent shared boundary and symmetric Epic 7/Epic 8
  wording to the latest assessment.
- Epic 7 final readiness and the Epic 8 architecture spine required no edit;
  their existing wording already satisfies the invariant.
- Historical readiness reports remain unchanged as historical records.
- Architecture, UX, source code, infrastructure, build, deployment, and tests
  are unaffected.

## 3. Recommended and Approved Approach

**Direct Adjustment** was approved and implemented as a narrow wording change to
the canonical PRD, canonical epics document, and latest readiness report.

- Effort: Low
- Risk: Low
- Timeline impact: None beyond documentation review
- MVP impact: None; Epics 1-5 remain the complete PRD feature-coverage baseline
- Rollback: Revert the three wording edits

Rollback of completed implementation and PRD MVP redefinition were unnecessary
because no implemented behavior or approved scope changed.

## 4. Detailed Changes Implemented

### PRD

**Artifact:** `_bmad-output/planning-artifacts/parties-ui-prd.md`
**Section:** `Current Implementation Evidence` → `Post-MVP maintenance status`

OLD:

> Post-MVP maintenance status:
>
> - Epic 7 is completed partial platform-alignment scope. Its final readiness
>   record preserves rollback paths and deferred deletion-safe cleanup. It carries
>   no new PRD functional requirement coverage.
> - Epic 8, approved by `sprint-change-proposal-2026-07-06.md`, is domain-focus
>   refactoring and platform extraction. It is post-MVP maintenance only and must
>   not be reported as product-feature coverage.

NEW:

> Post-MVP maintenance status:
>
> **Scope invariant:** Epics 7 and 8 are maintenance scope only. Neither epic
> introduces or covers a new PRD functional requirement, and neither may be
> counted as MVP or product-feature functional coverage.
>
> - Epic 7 is completed partial platform-alignment scope. Its final readiness
>   record preserves rollback paths and deferred deletion-safe cleanup. It carries
>   no new PRD functional requirement coverage.
> - Epic 8, approved by `sprint-change-proposal-2026-07-06.md`, is domain-focus
>   refactoring and platform extraction. It is post-MVP maintenance only, carries
>   no new PRD functional requirement coverage, and must not be reported as
>   product-feature delivery.

### Epics

**Artifact:** `_bmad-output/planning-artifacts/epics.md`
**Section:** `Implementation Scope Classification (2026-06-29 readiness)`

OLD:

> ### Implementation Scope Classification (2026-06-29 readiness)
>
> - **PRD feature scope:** Epics 1-5. These cover all 9 PRD FRs and are the only
>   product-feature epics used as MVP implementation-readiness evidence.

NEW:

> ### Implementation Scope Classification (2026-06-29 readiness)
>
> **Scope invariant:** Epics 7 and 8 are post-MVP maintenance scope. Neither epic
> introduces or covers a new PRD functional requirement, and neither may be used
> as MVP or product-feature functional-coverage evidence.
>
> - **PRD feature scope:** Epics 1-5. These cover all 9 PRD FRs and are the only
>   product-feature epics used as MVP implementation-readiness evidence.

### Implementation Readiness

**Artifact:** `_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md`
**Section:** report header

OLD:

> **Date:** 2026-07-07
> **Project:** parties

NEW:

> **Date:** 2026-07-07
> **Project:** parties
>
> **Scope boundary:** Epics 7 and 8 are maintenance scope only. Neither epic adds
> or covers a new PRD functional requirement. Readiness totals must exclude both
> from MVP and product-feature functional coverage and assess them only on the
> maintenance/platform track.

**Section:** `PRD Analysis` → `Additional Requirements`

OLD:

> - Epic 7 is completed partial platform-alignment scope and carries no new PRD functional requirement coverage.
> - Epic 8 is post-MVP domain-focus refactoring and platform extraction, and must not be reported as product-feature coverage.

NEW:

> - Epic 7 is completed post-MVP platform-alignment maintenance and carries no new PRD functional requirement coverage.
> - Epic 8 is post-MVP domain-focus refactoring and platform-extraction maintenance, carries no new PRD functional requirement coverage, and must not be reported as product-feature delivery.

## 5. Implementation Handoff

**Classification:** Minor
**Recipient:** Developer/documentation maintainer
**Status:** Complete

Responsibilities completed:

1. Applied only the approved wording changes to the three canonical/current files.
2. Preserved epic/story scope, IDs, sequencing, and statuses.
3. Verified the shared invariant and symmetric Epic 7/Epic 8 wording.
4. Left `sprint-status.yaml` unchanged because no epic/story entry changed.

Success criteria:

- [x] PRD, epics, and latest readiness report classify Epics 7 and 8 as maintenance.
- [x] Each affected artifact says neither epic adds or covers new PRD functional requirements.
- [x] Neither epic can be counted as MVP or product-feature functional coverage.
- [x] Epics 1-5 remain the complete nine-FR feature baseline.
- [x] No architecture, UX, implementation, or sprint-status behavior changed.

## Change Navigation Checklist Record

### 1. Understand the Trigger and Context

- [N/A] 1.1 No triggering implementation story; this was planning-scope hygiene.
- [x] 1.2 Core problem defined as inconsistent explicitness in requirements/readiness wording.
- [x] 1.3 Evidence collected from the PRD, epics, current readiness report, Epic 7 final readiness, and Epic 8 architecture spine.

### 2. Epic Impact Assessment

- [x] 2.1 Epics 7 and 8 remain completable under their approved maintenance scope.
- [x] 2.2 Wording clarification only; no scope or acceptance-criteria changes.
- [x] 2.3 Remaining epics and dependencies reviewed; no changes required.
- [N/A] 2.4 No epic invalidated and no new epic needed.
- [N/A] 2.5 No priority or sequencing change.

### 3. Artifact Conflict and Impact Analysis

- [x] 3.1 PRD functional scope remains Epics 1-5.
- [N/A] 3.2 No architecture impact.
- [N/A] 3.3 No UI/UX impact.
- [x] 3.4 Latest readiness wording made symmetric; all technical artifacts unchanged.

### 4. Path Forward Evaluation

- [x] 4.1 Direct Adjustment completed: low effort, low risk, no timeline impact.
- [N/A] 4.2 Rollback of completed work unnecessary.
- [N/A] 4.3 PRD MVP review unnecessary.
- [x] 4.4 Direct Adjustment selected and completed.

### 5. Sprint Change Proposal Components

- [x] 5.1 Issue summary complete.
- [x] 5.2 Epic and artifact impacts documented.
- [x] 5.3 Recommended path and alternatives documented.
- [x] 5.4 MVP impact and action plan documented.
- [x] 5.5 Minor-scope handoff completed.

### 6. Final Review and Handoff

- [x] 6.1 Applicable checklist items addressed.
- [x] 6.2 Proposal verified for consistency.
- [x] 6.3 Administrator approved implementation on 2026-07-16.
- [N/A] 6.4 No sprint-status update required.
- [x] 6.5 Handoff complete; success criteria verified.

## Workflow Execution Log

- Change trigger: keep PRD, epics, and readiness explicit that Epics 7 and 8 are maintenance scope with no new PRD functional coverage.
- Approval: Administrator, 2026-07-16.
- Change scope: Minor.
- Artifacts modified: canonical PRD, canonical epics document, latest implementation-readiness report.
- Artifacts produced: finalized Sprint Change Proposal with before/after edits and handoff record.
- Routed to: Developer/documentation maintainer.
- Result: implemented and verified.
