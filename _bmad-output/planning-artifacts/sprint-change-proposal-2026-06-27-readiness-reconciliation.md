---
project_name: parties
user_name: Administrator
date: 2026-06-27
workflow: bmad-correct-course
change_scope: moderate
status: approved-implemented
mode: batch
trigger_report: _bmad-output/planning-artifacts/implementation-readiness-report-2026-06-27.md
requirements_basis: "Brownfield architecture + UX + docs; create or bless a PRD-shaped canonical requirements source before the next readiness run."
---

# Sprint Change Proposal - Implementation Readiness Reconciliation

## 1. Issue Summary

The 2026-06-27 implementation-readiness report concluded that the project is **not
ready for full Phase 4 implementation**. The report found ten issues, with these
main blockers:

- No PRD or formally blessed canonical requirements source.
- Story 4.2 is oversized and gates all Consumer work.
- Story 1.4 has a forward dependency on Story 4.2.
- Story 4.1 is a completed decision spike still listed as implementation work.
- Story 3.5/3.6 dependency and approval handling is ambiguous.

After loading the planning artifacts, sprint status, and affected implementation
story records, the current state is more precise:

- The PRD/canonical-source problem is real. No `*prd*.md` or requirements document
  exists under `_bmad-output/planning-artifacts`.
- The UX design set exists and is final, but it lacks a standard `index.md`, so
  discovery tools must special-case it.
- `sprint-status.yaml` marks Epics 1-5 and all implementation stories as `done`.
- Story 1.4 is done and proves resolver behavior with synthetic claims.
- Story 4.1 is done and produced the accepted Consumer identity binding ADR.
- Story 4.2 is done and implemented admin-link identity binding provisioning.
- Story 3.5 is done, records approval, and implemented D7 through existing
  projection-query and command seams without EventStore submodule or DAPR ACL
  changes.
- Story 3.6 is done and implemented the bounded Admin erasure-verification report.

The issue is therefore not that Phase 4 implementation should be blocked today.
The issue is that planning/readiness artifacts are out of sync with completed
implementation evidence, and the missing PRD-shaped canonical requirements source
keeps automated readiness checks from tracing FR/NFR coverage cleanly.

## 2. Impact Analysis

### Epic Impact

Epic 1: No product-scope change. The planning text for Story 1.4 should stop
reading as if end-to-end claim issuance is still future-blocked by Story 4.2.

Epic 3: No implementation change. Story 3.5 and Story 3.6 are complete. The
planning text should clarify that the D7 approval gate was satisfied, that D7 was
implemented without EventStore submodule changes, and that Story 3.6's unavailable
state is a defensive runtime fallback, not a dependency ambiguity.

Epic 4: No implementation change. Story 4.1 is completed discovery, not active
implementation work. Story 4.2 was large, but it is already implemented and reviewed;
splitting it now would create historical churn rather than reduce implementation risk.

Epic 5: No direct change. Its dependency on Epic 4 is now satisfied in sprint status.

### Story Impact

Current implementation story records should remain the source of truth for actual
completion. The planning epic file should be corrected as a planning/readiness
surface so future checks do not rediscover already-resolved blockers.

Affected story references:

- Story 1.4: clarify synthetic-claim test proof and remove active forward-blocker
  interpretation.
- Story 3.5: mark the D7 approval gate as satisfied and implementation complete.
- Story 3.6: clarify fallback as defensive behavior when capability is unavailable.
- Story 4.1: mark as completed prerequisite discovery retained for traceability.
- Story 4.2: mark as completed provisioning implementation retained for traceability;
  do not retroactively split unless a future regression requires new follow-up work.

### Artifact Conflicts

- Missing PRD-shaped source blocks requirements extraction.
- `architecture.md` still contains readiness/handoff wording that says the Consumer
  path needs Story 4.2, even though Story 4.2 is done.
- `epics.md` contains planning-era dependency wording that the readiness report reads
  as active blockers.
- UX docs are final but not discoverable through the standard sharded `index.md`
  pattern.
- The 2026-06-27 readiness report itself should be superseded after reconciliation
  because it did not reconcile planning findings against sprint status and completed
  implementation story files.

### Technical Impact

No code, infrastructure, or deployment behavior needs to change for this
course-correction. This is a planning and readiness traceability correction.

## 3. Recommended Approach

Recommended path: **Direct Adjustment**.

Do not roll back completed work. Do not reopen Story 4.2 solely to split a story
that is already complete. Do not reduce MVP scope. Instead:

1. Create a lightweight PRD-shaped canonical requirements document, for example
   `_bmad-output/planning-artifacts/parties-ui-prd.md`, extracted from the existing
   architecture requirements inventory, UX spines, and epics FR map.
2. Add an `index.md` to the final UX design folder so standard artifact discovery
   can find the UX set.
3. Patch `epics.md` and `architecture.md` to distinguish historical planned
   sequencing from current implementation status.
4. Mark the 2026-06-27 readiness report as superseded by a follow-up readiness run
   after these artifact updates.
5. Re-run implementation readiness with sprint status and implementation artifacts
   included as evidence, not only planning text.

Effort: Low to medium.

Risk: Low. The risk is mostly documentation churn. The main control is to avoid
rewriting completed implementation story records and to keep the PRD document
traceable to existing architecture/UX/epics text.

Timeline impact: One planning cleanup pass plus one readiness rerun.

## 4. Detailed Change Proposals

### Planning Artifact: New PRD-Shaped Canonical Requirements Source

Artifact: `_bmad-output/planning-artifacts/parties-ui-prd.md`

OLD:

```text
No PRD document exists under _bmad-output/planning-artifacts.
```

NEW:

```text
# Parties UI PRD

Status: Canonical requirements source for the parties-ui implementation-readiness
workflow.

Basis:
- _bmad-output/planning-artifacts/architecture.md
- _bmad-output/planning-artifacts/epics.md
- _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md
- _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md
- docs/index.md and linked brownfield docs

Functional Requirements:
- FR-Shell
- FR-Admin-1 through FR-Admin-4
- FR-Consumer-1 through FR-Consumer-4

Non-Functional Requirements:
- NFR1 through NFR9, copied from the architecture/epics requirements inventory.

Traceability:
- Each FR maps to the existing Epic FR Coverage Map in epics.md.
- UX-DR1 through UX-DR16 are implementation constraints attached to the FR/NFR set.
```

Rationale: The brownfield basis is legitimate, but tooling expects a PRD-shaped
artifact. Creating this file avoids treating "no formal PRD" as a blocker every
time readiness runs.

### Planning Artifact: UX Index

Artifact: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/index.md`

OLD:

```text
UX folder exists, but has no index.md.
```

NEW:

```text
# Parties UI UX Design Index

- DESIGN.md
- EXPERIENCE.md
- validation-report.md
- review-accessibility.md
- review-regulated-language.md
- review-rubric.md
- .decision-log.md
- mockups/signin.html
- mockups/admin-parties.html
- mockups/create-edit-party.html
- mockups/consumer-profile.html
- mockups/consumer-privacy.html

Status: final. DESIGN.md and EXPERIENCE.md are authoritative; mockups are
illustrative where conflicts exist.
```

Rationale: The UX set is final and aligned. The only change is making it easy for
standard readiness/document-discovery flows to load it.

### Epics: Requirements Basis Header

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: Overview / requirements basis.

OLD:

```text
There is no formal PRD - this is a brownfield .NET 10 system; per the docs/index.md
brownfield note the requirements basis is the docs/ baseline plus the UX design,
which architecture.md already consolidated into the FR/NFR set reproduced below.
```

NEW:

```text
Canonical requirements source: _bmad-output/planning-artifacts/parties-ui-prd.md.
This is a brownfield .NET 10 system; the PRD-shaped source is extracted from the
docs/ baseline, the final UX design set, and architecture.md's consolidated FR/NFR
inventory. When tracing implementation readiness, use the PRD-shaped source plus
this epics document and the current sprint-status.yaml.
```

Rationale: Keeps the brownfield basis while giving future readiness checks a clear
canonical source.

### Epics: Story 1.4 Forward Dependency

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: Story 1.4 acceptance criteria note.

OLD:

```text
The mechanism that issues the claim is AR-Gap-Binding - decided in Story 4.1 and
implemented in Story 4.2; this story only consumes an existing claim, and its happy
path is end-to-end verifiable once 4.2 lands.
```

NEW:

```text
This story consumes an existing `party_id` claim and proves fail-closed resolver
behavior with injected principals and DI tests. Story 4.1 selected the issuing
mechanism through the accepted ADR, and Story 4.2 implemented admin-link claim
provisioning. Do not treat Story 1.4 as blocked by future claim issuance work.
```

Rationale: The original wording was acceptable before Story 4.2 existed, but it now
reads as an active forward dependency.

### Epics: Story 4.1 Classification

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: Story 4.1.

OLD:

```text
### Story 4.1: Decide the Consumer identity -> `party_id` binding mechanism
(design spike -> ADR)
...
this is a decision spike, not an implementation story; it produces a decision
artifact only and is the predecessor of Story 4.2 and all of Epics 4-5.
```

NEW:

```text
### Story 4.1: Decide the Consumer identity -> `party_id` binding mechanism
(completed discovery prerequisite)

Status note: completed on 2026-06-10. The accepted ADR is
_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md. Retain this story
for traceability, but do not count it as active Phase 4 implementation work.
```

Rationale: Keeps the decision record visible while removing it from the active
implementation backlog.

### Epics: Story 4.2 Sizing Finding

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: Story 4.2.

OLD:

```text
This story unblocks the rest of Epic 4 and Epic 5.
```

NEW:

```text
Status note: completed on 2026-06-10. This story was an oversized implementation
slice, but it is already complete and reviewed. Do not retroactively split it for
readiness hygiene. Future identity-provisioning enhancements should be split into
smaller stories using the service/store/IdP adapter boundaries introduced here.
```

Rationale: A split would have been the right pre-implementation correction. After
completion, the safer correction is to record the lesson and prevent the readiness
tool from treating the completed story as a current blocker.

### Epics: Story 3.5 / 3.6 D7 Dependency

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: Story 3.5 and Story 3.6.

OLD:

```text
Story 3.5 is explicitly gated on cross-submodule approval and sequenced as a
predecessor to Story 3.6.

Given the D7 contract has not yet landed, when the report surface is reached, then
it degrades to a clear "verification not yet available" state.
```

NEW:

```text
Status note: Story 3.5 completed on 2026-06-10 after approval was recorded. The
chosen implementation used existing projection-query and command seams; no
EventStore submodule route, `/query` endpoint, or DAPR ACL expansion was required.

Story 3.6 completed on 2026-06-10. Its "verification not yet available" state is
defensive runtime behavior for capability-unavailable/provisional conditions, not
an active dependency on an unlanded D7 contract.
```

Rationale: Resolves the ambiguity without changing the completed implementation
story records.

### Architecture: Gap Analysis and Readiness Status

Artifact: `_bmad-output/planning-artifacts/architecture.md`

Sections: Gap Analysis Results / Architecture Readiness Assessment / Implementation
Handoff.

OLD:

```text
The Consumer path needs Story 4.2 to implement the accepted D2 admin-link binding ADR.

Overall Status: READY WITH KNOWN IMPLEMENTATION DEPENDENCIES.
```

NEW:

```text
Implementation status note, 2026-06-27: Story 4.2 implemented the accepted D2
admin-link binding ADR on 2026-06-10. Story 3.5 and Story 3.6 implemented D7
backend/report behavior on 2026-06-10. For readiness checks after that date, treat
D2 and D7 as implemented and validate regressions against sprint-status.yaml and
the implementation story records.

Overall Status: IMPLEMENTED FOR EPICS 1-5; PLANNING TRACEABILITY RECONCILIATION
REQUIRED.
```

Rationale: Architecture is still useful, but its handoff language should not
describe completed dependencies as future work.

### Readiness Report Handling

Artifact: `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-27.md`

OLD:

```text
overallReadinessStatus: not-ready
```

NEW:

```text
supersededBy: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-27-readiness-reconciliation.md
supersessionReason: "Report read planning-era dependency text without reconciling
current sprint-status.yaml and completed implementation story records."
```

Rationale: The report should remain as evidence, but it should not be treated as
the current implementation readiness verdict after reconciliation.

## 5. Implementation Handoff

Scope classification: **Moderate**.

Recommended recipients:

- Product Owner / PM: approve the PRD-shaped canonical requirements source and the
  decision not to retroactively split completed Story 4.2.
- Developer agent: apply the planning artifact patches after approval.
- Architect: review the architecture readiness wording to ensure it does not hide
  remaining future enhancements such as production KMS or gateway self-principal.

Implementation tasks completed after approval:

1. Added `_bmad-output/planning-artifacts/parties-ui-prd.md`.
2. Added `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/index.md`.
3. Patched `epics.md` with status and dependency clarifications.
4. Patched `architecture.md` with current implementation status and updated readiness
   wording.
5. Added a supersession note to the 2026-06-27 readiness report.

Remaining follow-up:

1. Re-run implementation readiness with sprint status and implementation artifacts
   included.

Success criteria:

- Future readiness discovery finds a PRD-shaped canonical requirements source.
- UX discovery finds the final UX design set through `index.md`.
- Readiness no longer flags Story 1.4, 3.5, 3.6, 4.1, or 4.2 as active blockers
  when their implementation story files and sprint status are included.
- The remaining readiness risks are real current risks, not stale planning text.
- No code, deployment, EventStore submodule, DAPR ACL, or UX behavior changes are
  introduced by this course-correction.

## Checklist Summary

- [x] 1.1 Triggering issue identified: 2026-06-27 readiness report.
- [x] 1.2 Core problem defined: missing canonical requirements source plus stale
  planning/readiness interpretation of completed story dependencies.
- [x] 1.3 Supporting evidence gathered: readiness report, epics, architecture, UX,
  sprint status, Story 1.4, Story 3.5, Story 3.6, Story 4.1, Story 4.2.
- [x] 2.1 Current epic impact assessed.
- [x] 2.2 Required epic-level changes identified as planning traceability updates.
- [x] 2.3 Remaining epics reviewed for dependency impact.
- [x] 2.4 No new epic required.
- [x] 2.5 No epic priority change required.
- [x] 3.1 PRD conflict addressed by creating `_bmad-output/planning-artifacts/parties-ui-prd.md`.
- [x] 3.2 Architecture conflict identified: stale handoff/readiness wording.
- [x] 3.3 UX conflict identified: discoverability only; design content remains final.
- [x] 3.4 Secondary artifacts identified: readiness report supersession and rerun.
- [x] 4.1 Direct Adjustment selected as viable.
- [N/A] 4.2 Rollback rejected; no completed work should be reverted.
- [N/A] 4.3 MVP Review rejected; MVP scope does not need reduction.
- [x] 4.4 Recommended path selected: direct planning/readiness reconciliation.
- [x] 5.1 Issue summary created.
- [x] 5.2 Impact and artifact needs documented.
- [x] 5.3 Recommended path and rationale documented.
- [x] 5.4 PRD/MVP impact and action plan documented.
- [x] 5.5 Handoff plan documented.
- [x] 6.3 User approval received and approved changes implemented.
- [N/A] 6.4 Sprint status update not required; current sprint-status already marks the
  relevant stories done.
- [x] 6.5 Next steps and success criteria defined.
