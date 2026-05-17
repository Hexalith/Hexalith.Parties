---
project: Hexalith.Parties
date: 2026-05-17
source_report: implementation-readiness-report-2026-05-17.md
status: Approval Pending
change_scope: Moderate
recommended_path: Direct Adjustment
mode: Batch
---

# Sprint Change Proposal: Readiness Follow-Up Corrections

## 1. Issue Summary

The Implementation Readiness Assessment completed on 2026-05-17 found Hexalith.Parties **READY for MVP implementation planning** and **NEEDS WORK before v1.1/v1.2 scheduling**.

No critical blockers were found. The assessment identified six issues requiring attention:

- Major: Epic 5 overpromises event ordering in its value statement.
- Major: v1.1 GDPR stories are high-complexity and need refinement/splitting before v1.1 sprint scheduling.
- Major: v1.2 admin and picker stories depend on the accepted EventStore-fronted Parties client/gateway contract.
- Minor: Preparation-only stories must remain artifact-driven and must not become hidden feature work.
- Minor: The admin UX document still references stale Story 12.4/12.5 blocker wording.
- Minor: Story 1.1 is technical setup but justified by the architecture starter requirement.

The trigger is a planning-readiness correction, not an implementation failure. MVP may proceed after the small wording/traceability cleanup below. v1.1 and v1.2 work remain gated by refinement and dependency acceptance.

## 2. Impact Analysis

### Epic Impact

Epic 5 remains valid, but its value statement should stop claiming unconditional ordered delivery. Architecture states that DAPR pub/sub ordering guarantees are broker-dependent, and Story 5.4 already contains the correct mitigation: document supported deployment behavior and require idempotent/order-tolerant subscriber guidance where strict per-aggregate ordering is unavailable.

Epic 6 remains valid as v1.1 scope, but Stories 6.1, 6.3, and 6.10 should carry a clear pre-scheduling refinement gate. Their current acceptance criteria cover security, privacy, EventStore, projection, cache, search, retry, and operational reporting behavior broad enough to exceed normal story size once implementation details are known.

Epics 7 and 8 remain valid as v1.2 scope, but affected stories must link directly to the dependency record at `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` before sprint scheduling.

### Artifact Impact

- `epics.md`: update Epic 5 value statement in both the summary and main Epic 5 section.
- `epics.md`: add a v1.1 refinement/splitting gate to Epic 6 and the three highest-risk GDPR stories.
- `epics.md`: link the existing client/gateway dependency record from Story 7.6 and Epic 8 scheduling metadata.
- `ux-admin-portal-2026-05-10.md`: replace stale Story 12.4/12.5 blocker wording with the accepted bounded blocker text.
- No PRD or architecture change is required.
- No code rollback or implementation change is required.

## 3. Recommended Approach

Recommended path: **Direct Adjustment**.

Rationale:

- MVP scope remains intact.
- Required planning documents exist and all PRD FRs are covered.
- The corrections are local wording, scheduling, and dependency-traceability improvements.
- The architecture already states the correct ordering model.
- The dependency record for the client/gateway contract already exists and only needs to be linked from affected planning surfaces.

Effort estimate: **Low** for MVP cleanup, **Medium** later for v1.1 story refinement.

Risk after correction: **Low** for MVP, **Medium** for v1.1/v1.2 until their refinement/dependency gates are satisfied.

## 4. Detailed Change Proposals

### Proposal A: Reword Epic 5 Event Ordering Value Statement

Artifact: `_bmad-output/planning-artifacts/epics.md`

Sections:

- Epic summary near the top of the document
- Main `## Epic 5: Event-Driven Consumer Integration` section

OLD:

```markdown
Consuming applications can receive ordered, tenant-aware party events and build their own lifecycle-aware read models.
```

NEW:

```markdown
Consuming applications can receive tenant-aware party events with documented ordering behavior and idempotent subscriber guidance, so they can build lifecycle-aware read models safely across supported deployment targets.
```

Rationale:

This aligns Epic 5 with architecture guidance that ordering is broker-dependent while preserving FR73 through Story 5.4's documented ordering and subscriber-pattern acceptance criteria.

### Proposal B: Add an Epic 6 Pre-Scheduling Refinement Gate

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `## Epic 6: GDPR Compliance Operations`

ADD after existing Epic 6 metadata:

```markdown
**Pre-scheduling gate:** Before v1.1 sprint scheduling, refine Epic 6 stories into independently testable security/privacy slices. Split any story whose implementation cannot be completed and tested within one sprint-sized unit.
```

Rationale:

The readiness report found no structural problem with Epic 6, but Stories 6.1, 6.3, and 6.10 have platform/security blast radius large enough to require explicit refinement before scheduling.

### Proposal C: Mark the Highest-Risk v1.1 GDPR Stories for Split Review

Artifact: `_bmad-output/planning-artifacts/epics.md`

Stories:

- Story 6.1: Activate Per-Party Personal Data Encryption
- Story 6.3: Verify Erasure Across Internal Stores
- Story 6.10: Rotate Tenant Encryption Keys

ADD to each story metadata block:

```markdown
**Scheduling gate:** Split/reconfirm before v1.1 sprint scheduling if implementation scope cannot be completed and tested as one independently valuable unit.
```

Likely split points:

- Story 6.1: EventStore encryption primitives, Parties contract markers, snapshot integration, replay compatibility, classification coverage, and missing-key behavior.
- Story 6.3: Aggregate state verification, snapshot verification, projection cleanup verification, cache/search purge verification, retry behavior, and DPO reporting.
- Story 6.10: Tenant key material handling, party-key wrapping, idempotent retry, erased-party preservation, concurrent read behavior, and redacted status reporting.

Rationale:

This keeps v1.1 intent intact while preventing oversized security/privacy stories from being scheduled prematurely.

### Proposal D: Link the Existing Client/Gateway Dependency Record from v1.2 Stories

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story 7.6 OLD:

```markdown
**Dependency:** Accepted EventStore-fronted Parties client/gateway contract. If the dependency is tracked outside this epics document, reference the external dependency record in implementation planning before scheduling Story 7.6.
```

Story 7.6 NEW:

```markdown
**Dependency:** Accepted EventStore-fronted Parties client/gateway contract. Before scheduling Story 7.6 or Story 7.7, confirm `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` is updated to `Satisfied` or `Risk Accepted` and linked from sprint planning.
```

Epic 8 ADD after its phase/coverage metadata:

```markdown
**Dependency:** Before scheduling Epic 8 implementation stories, confirm `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` is updated to `Satisfied` or `Risk Accepted` and linked from sprint planning.
```

Rationale:

The dependency record exists, but affected stories should point to it directly so the v1.2 scheduling gate cannot be missed.

### Proposal E: Replace Stale Admin UX Story 12.4/12.5 Reference

Artifact: `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md`

Section: contract unavailable state table

OLD:

```markdown
| Contract unavailable | Disable unsupported reads/actions and show the exact Story 12.4/12.5 blocker. |
```

NEW:

```markdown
| Contract unavailable | Disable unsupported reads/actions and show the exact bounded blocker `Blocked on accepted EventStore-fronted Parties client/gateway contract`. |
```

Rationale:

The UX behavior is correct, but the stale story-number reference conflicts with the current Epic 7 story model.

### Proposal F: Preserve Preparation-Story Discipline

Artifacts:

- `_bmad-output/planning-artifacts/epics.md`
- Sprint planning metadata for MVP implementation

Current status:

Stories 2.9, 5.6, and 5.7 already carry preparation-only labels and should remain as written.

Scheduling rule to preserve:

```markdown
Preparation-only stories must produce concrete artifacts such as reserved contract extension points, compatibility tests, documented reserved fields, and consumer guidance. They must not activate deferred runtime behavior or be counted as delivered v1.1/v2 feature scope.
```

Rationale:

No additional story rewrite is required if this discipline is preserved during MVP sprint planning.

### Proposal G: Preserve Story 1.1 as the Only Technical Setup Story

Artifact: `_bmad-output/planning-artifacts/epics.md`

Current status:

Story 1.1 is justified by the architecture starter-template requirement and already includes validation that does not require recursive nested submodule initialization.

Scheduling rule to preserve:

```markdown
Story 1.1 remains the only technical setup story. Future setup-like work must be attached to user-observable capability, validation evidence, or a documented architecture requirement.
```

Rationale:

No story edit is required. This is a planning discipline note for MVP execution.

## 5. Checklist Status

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | N/A | Trigger is the readiness assessment, not a single implementation story. |
| 1.2 Core problem | Done | Planning wording, sizing-risk, dependency-linking, and UX traceability cleanup. |
| 1.3 Supporting evidence | Done | `implementation-readiness-report-2026-05-17.md` lists 0 blockers, 3 major issues, and 3 minor issues. |
| 2.1 Current epic impact | Done | Epic 5 wording requires adjustment; Epic 6 needs refinement gate. |
| 2.2 Epic-level changes | Done | No new epic; targeted metadata/value-statement updates only. |
| 2.3 Future epic impact | Done | Epics 6, 7, and 8 require scheduling gates before v1.1/v1.2 work. |
| 2.4 Invalidated epics/new epics | Done | None invalidated; none added. |
| 2.5 Priority/order | Done | MVP may proceed after cleanup; v1.1/v1.2 remain gated. |
| 3.1 PRD conflicts | Done | No PRD conflict. |
| 3.2 Architecture conflicts | Done | No architecture conflict; ordering correction aligns epics to architecture. |
| 3.3 UX conflicts | Done | One stale UX reference requires update. |
| 3.4 Secondary artifacts | Done | Existing dependency record should be linked from story/sprint planning. |
| 4.1 Direct adjustment | Viable | Best fit; low effort for MVP cleanup. |
| 4.2 Rollback | Not viable | No failed implementation exists. |
| 4.3 MVP review | Not needed | MVP scope remains achievable. |
| 4.4 Recommended path | Done | Direct Adjustment. |
| 5.1 Issue summary | Done | Included above. |
| 5.2 Epic/artifact impact | Done | Included above. |
| 5.3 Path forward | Done | Included above. |
| 5.4 MVP impact/action plan | Done | No MVP scope reduction; v1.1/v1.2 gates clarified. |
| 5.5 Handoff plan | Done | Product Owner/Developer for artifact edits; Architect/Product Owner for dependency acceptance. |
| 6.1 Checklist completion | Done | All applicable items addressed. |
| 6.2 Proposal accuracy | Done | Based on report plus current `epics.md`, UX, architecture, and dependency record. |
| 6.3 User approval | Action-needed | Awaiting Jérôme approval before editing planning artifacts. |
| 6.4 Sprint-status update | N/A | No epic/story add/remove/renumber proposed. |
| 6.5 Handoff confirmation | Action-needed | Confirm after approval and artifact updates. |

## 6. Implementation Handoff

Scope classification: **Moderate**.

Handoff recipients:

- Product Owner / Developer agent: apply the approved `epics.md` and UX wording updates.
- Architect / Product Owner: keep the EventStore-fronted Parties client/gateway dependency record current and mark it `Satisfied` or `Risk Accepted` before v1.2 scheduling.
- QA/Test Architect: during v1.1 planning, review Stories 6.1, 6.3, and 6.10 for split boundaries and independently testable acceptance slices.

Success criteria:

- Epic 5 no longer promises unconditional ordered delivery.
- Story 5.4 remains the enforcement point for documented ordering behavior and idempotent/order-tolerant subscriber guidance.
- Epic 6 carries a visible v1.1 refinement/splitting gate.
- Stories 6.1, 6.3, and 6.10 carry split/reconfirm scheduling gates.
- Story 7.6 and Epic 8 link directly to the existing client/gateway dependency record.
- Admin UX contract-unavailable wording no longer references stale Story 12.4/12.5 identifiers.
- Preparation-only stories remain artifact-driven and do not activate deferred runtime behavior.
- Story 1.1 remains the only technical setup story.

## 7. Approval

Approval pending.

If approved, apply Proposals A through E directly to the planning artifacts and preserve Proposals F and G as sprint-planning discipline.
