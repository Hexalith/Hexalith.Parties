---
title: Sprint Change Proposal — Projection Rollback Guardrail Revalidation
date: 2026-07-11
author: Administrator
workflow: bmad-correct-course
mode: incremental
scope_classification: minor
status: approved
approved: 2026-07-11T11:54:10+02:00
trigger: >
  Keep projection rollback-only paths until Epic 8 proves EventStore SDK
  projection/query parity, GDPR processing-record reads, rebuild behavior, and
  rollback replacement.
supersedes: null
revalidates:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-projection-rollback-retention.md
related:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
---

# Sprint Change Proposal — Projection Rollback Guardrail Revalidation

## 1. Issue Summary

The requested course correction is already an approved project decision. The
2026-07-07 projection rollback-retention proposal established that Parties-local
projection, query, rebuild, adapter, health, and fallback paths remain
rollback-only until Story 8.6 records four proofs:

1. EventStore SDK projection/query parity.
2. GDPR Art.30 processing-record read parity.
3. Full rebuild behavior verified against aggregate replay.
4. A proven rollback replacement for the EventStore SDK path.

This run revalidates that decision against the repository as of 2026-07-11. It
does not reopen, replace, or duplicate the approved July 7 proposal. The only
identified inconsistency is a stale tracking comment that still describes Story
8.6 as `backlog`; Story 8.6 is now correctly `blocked` at its platform prerequisite
gate.

### Current evidence

- `sprint-change-proposal-2026-07-07-projection-rollback-retention.md` is
  `approved` and remains the governing decision.
- Epic 8 spine invariants I3, I8, I9, and I10 retain parity-before-deletion,
  processing-record, replay/idempotency, freshness, rebuild, and rollback rules.
- Story 8.6 and `spec-8-6` require the parity harness and rebuild-vs-replay proof
  before deleting any rollback path.
- The Story 8.3 `EventStore projection/query SDK` row remains
  `needs-additive-api`; Story 8.6 is therefore `blocked`.
- The Parties-local actor, query, rebuild, adapter, freshness, and health-check
  rollback artifacts remain present.
- Ten projection/query `catch (NotImplementedException)` fallback branches remain
  present: seven in `PartyDetailProjectionActorExtensions` and three in
  `PartyIndexProjectionQueryActor`.
- Story 8.6 records that no production source migration occurred when the
  prerequisite gate was checked on 2026-07-09.

## 2. Impact Analysis

### Epic impact

- Epic 8 remains in progress and completable after owner-approved EventStore SDK
  parity is recorded.
- No epic is added, removed, redefined, or resequenced.
- Stories 8.7 through 8.10 retain their existing readiness gates and sequence.
- Epics 1 through 5 remain the completed PRD feature scope; Epic 8 continues to
  deliver zero new PRD functional requirements.

### Story impact

- Story 8.6 remains `blocked`; this proposal does not unblock it.
- Story 8.6 must not edit production projection/query source until its Story 8.3
  prerequisite row records owner-approved additive parity or explicit
  already-available proof.
- No Story 8.6 acceptance criterion, task, spec boundary, or validation lane needs
  a semantic change; the required deletion guardrails are already explicit.

### Artifact conflicts

- **PRD:** no conflict and no edit.
- **Epics:** no conflict and no edit.
- **Architecture:** no conflict and no edit; the Epic 8 spine already governs.
- **UX:** not applicable; no component, user flow, accessibility, or regulated-copy
  behavior changes.
- **Story/spec/prerequisite matrix:** no semantic edit; all already encode the
  required gate.
- **Sprint tracking:** one comment is stale and should be revalidated from the
  July 7 backlog state to the July 11 blocked state.
- **Brownfield documentation:** correctly describes the local actor/query/rebuild
  path because that path remains active and retained; no edit is required before
  Story 8.6 migration succeeds.

### Technical impact

- No production code change.
- No submodule change or gitlink movement.
- No projection/query path deletion, narrowing, or fallback-branch removal.
- No state-store or read-model migration.
- No build or test execution is required for the tracking-only edit; repository
  inspection is the evidence for this revalidation.

## 3. Recommended Approach

**Selected: Direct Adjustment — revalidate the existing guardrail and refresh the
stale sprint-tracking annotation.**

This is the smallest accurate course correction. The desired constraint is already
approved and enforced across the architecture spine, Story 8.6, its spec, the
prerequisite matrix, and sprint action items. Creating a second governing decision
would add ambiguity. Revalidation records the current evidence while leaving the
July 7 proposal authoritative.

- **Effort:** Low.
- **Risk:** Low.
- **Timeline impact:** None to MVP or Epic 8 sequencing.
- **Delivery risk:** Story 8.6 remains blocked on EventStore owner proof; this
  proposal neither increases nor conceals that dependency.

Alternatives rejected:

- **Potential rollback:** not applicable. The retained local paths are themselves
  the rollback mechanism, and no unsafe Story 8.6 migration occurred.
- **MVP review:** not applicable. Epic 8 is post-MVP maintenance and changes no PRD
  feature coverage.

## 4. Detailed Change Proposal

### 4.1 Sprint-status annotation

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

Current text:

```yaml
# Verified 2026-07-07: all rollback-only projection/query files intact; 8.6 backlog.
```

Proposed text:

```yaml
# Revalidated 2026-07-11: all rollback-only projection/query files and 10 fallback
# branches remain intact; Story 8.6 is blocked at the needs-additive-api gate,
# and no production projection/query migration has occurred.
```

The file's `last_updated` comment and field will be refreshed when this proposal is
approved. No development-status row, story sequence, action text, owner, or action
status changes.

### 4.2 Retention and exit criteria reaffirmed

The approved July 7 retention set remains unchanged. Deletion stays unauthorized
until all of the following are recorded in the Story 8.3 matrix and Story 8.6 test
evidence:

| Proof | Required evidence before deletion |
|---|---|
| EventStore SDK projection/query parity | Detail and index results match; replay-from-zero, duplicate delivery, out-of-order delivery, erased-party exclusion, index batching, cursor scope, and freshness behavior are proven. |
| GDPR processing-record reads | `GetProcessingRecords` returns equivalent bounded `ProcessingActivityRecord[]`; Art.20/Art.30 and no-leak behavior remain intact. |
| Rebuild behavior | Full detail and index rebuilds are executed and compared with aggregate replay; any divergence blocks deletion. |
| Rollback replacement | A named, tested SDK rollback mechanism or revertable migration is recorded before the local actor/query/rebuild/fallback set is removed. |

## 5. Implementation Handoff

**Scope classification: Minor.** This is a tracking-only revalidation with one
approved comment/timestamp edit. It does not reorganize the backlog or require a
fundamental replan.

| Recipient | Responsibility |
|---|---|
| Developer agent | Apply the approved `sprint-status.yaml` comment and timestamp edit only; do not modify production projection/query source. |
| Architect | Keep the existing Epic 7 action open until all four Story 8.6 proofs are recorded; continue owning the July 7 governing guardrail. |
| EventStore owner | Supply or approve the additive projection/query SDK parity evidence required by the Story 8.3 matrix before Story 8.6 resumes. |
| Test Architect | Require the parity harness and rebuild-vs-aggregate-replay evidence before approving any deletion. |

### Success criteria

1. The July 7 approved rollback-retention proposal remains the governing decision.
2. Sprint tracking reflects Story 8.6's current `blocked` state and July 11
   revalidation evidence.
3. The Epic 7 projection rollback action remains `open`.
4. All rollback-only artifacts and ten fallback branches remain intact.
5. No production code, submodule, PRD, epic, architecture, UX, story, spec, or
   prerequisite-matrix semantics change.

## Change-Analysis Checklist

| Item | Status | Finding |
|---|---|---|
| 1.1–1.3 Trigger, problem, evidence | [x] Done | Story 8.6; incomplete owner-approved SDK parity; repository evidence revalidated. |
| 2.1–2.5 Epic impact | [x] Done | Epic 8 and sequencing unchanged; no new epic or story. |
| 3.1 PRD | [x] Done | No conflict or edit. |
| 3.2 Architecture | [x] Done | Existing spine invariants already govern. |
| 3.3 UX | [N/A] Skip | No user-facing change. |
| 3.4 Other artifacts | [x] Done | Sprint-status comment and timestamp refreshed after approval. |
| 4.1 Direct Adjustment | [x] Viable | Selected; low effort and risk. |
| 4.2 Potential Rollback | [N/A] Skip | Nothing to revert. |
| 4.3 MVP Review | [N/A] Skip | MVP unaffected. |
| 4.4 Recommended path | [x] Done | Revalidate and refresh tracking only. |
| 5.1–5.5 Proposal components | [x] Done | Issue, impact, recommendation, scope, and handoff documented. |
| 6.1–6.2 Review readiness | [x] Done | Proposal is internally consistent and actionable. |
| 6.3 Explicit approval | [x] Done | Administrator approved on 2026-07-11. |
| 6.4 Sprint-status update | [x] Done | Tracking comment and timestamp updated; no status rows changed. |
| 6.5 Handoff | [x] Done | Minor tracking change applied; continuing guardrail ownership confirmed. |

## Workflow Execution Log

- **Approved by:** Administrator
- **Approved at:** 2026-07-11T11:54:10+02:00
- **Issue addressed:** projection/query rollback-only path retention pending Story
  8.6 parity, GDPR processing-record, rebuild, and rollback proof.
- **Change scope:** Minor, tracking-only revalidation.
- **Artifacts modified:** this proposal and `sprint-status.yaml`.
- **Routed to:** Developer for the completed tracking edit; Architect and
  EventStore owner retain the continuing Story 8.6 guardrail and prerequisite work.
