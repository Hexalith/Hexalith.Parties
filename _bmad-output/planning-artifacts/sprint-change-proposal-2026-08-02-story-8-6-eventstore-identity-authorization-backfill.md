---
title: Sprint Change Proposal — Backfill Story 8.6 EventStore Identity Authorization
date: 2026-08-02
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: minor
status: implemented
approval_required: false
approval: approved
approved_by: Administrator
trigger: >
  Story 8.6's rollback-code deletion was built and validated against an
  EventStore identity that never passed through the formal owner-approval /
  Sprint-Change-Proposal chain. Surfaced during bmad-code-review of Story 8.6;
  the Administrator confirmed the identity pivot was their own real-time
  direction and asked for the record to be reconciled rather than the
  deletion reverted.
baseline:
  parties_commit: 549dac1
  eventstore_gitlink_at_deletion: c590590bc581a3f72ef6e67148eda988ba4b8fe6
  eventstore_gitlink_current: 4bcf2484a09eb26490cb2d32ceb6df8949f90cc6
  eventstore_gitlink_formally_approved: fa2d1c9910f8976553adb33dcdb1c9ff2ea75594
related:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01-projection-rollback-retention-revalidation.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-projection-rollback-retention.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md
  - references/Hexalith.EventStore/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
---

# Sprint Change Proposal — Backfill Story 8.6 EventStore Identity Authorization

## 1. Issue Summary

The 2026-08-01T00:27 proposal (`sprint-change-proposal-2026-08-01-projection-rollback-retention-revalidation.md`)
authorized Parties to select exact EventStore source SHA
`fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` — the only identity with a formal
owner-approval packet (`references/Hexalith.EventStore/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md`,
`authorize_consumer_migration: true` scoped to that exact SHA). That proposal
was explicit that it authorized identity selection only: *"No production
source or test code change is authorized by this proposal"* and *"Do not
delete, narrow, or unregister any governed rollback-only path."*

Consumer compilation against `fa2d1c99...` then failed: the checkout does not
expose `IAsyncDomainSharedProjectionRebuildHandler`,
`DomainSharedProjectionRebuildIdentity`, or
`DomainSharedProjectionRebuildCandidate`, which the Parties tenant-shared
index handler requires. Parties rolled back to the pre-story identity
(commit `64af3bc1`).

Later the same day (2026-08-01T14:20, per Story 8.6's Dev Agent Record), the
Administrator gave explicit real-time direction to use the latest EventStore
release instead of continuing to wait on an `fa2d1c99`-compatible owner
delivery. The session pivoted to EventStore `v3.89.0`
(`c590590bc581a3f72ef6e67148eda988ba4b8fe6`), which does expose the required
tenant-shared rebuild surfaces, and completed Story 8.6's full projection/query
SDK migration on that identity: rebound detail/index/processing-record
projections and all eight query discriminators to SDK handlers, then deleted
all 18 local rollback files (Dapr projection actors, platform adapters, the
rebuild service and its checkpoint/scope types, the update orchestrator, the
actor health check, the query-actor interface) and all 10
`catch (NotImplementedException)` fallback branches. Full regression passed
(452/452 Parties, 15/15 projects) before Story 8.6 advanced to `review`.

No owner-approval packet, Story 8.3 matrix row, or Sprint Change Proposal
authorizes `c590590b`/v3.89.0 specifically. The Story 8.3 matrix's "EventStore
projection/query SDK" and "EventStore DataProtection" rows — last edited in
the same commit that performs the deletion — still read that migration and
rollback-path deletion "remain blocked" for any identity other than one
supplying owner-approved tenant-shared rebuild semantics, and that "no Story
8.6 adoption is authorized in the current checkout." The governance record and
the shipped code diverged.

This proposal formally records the Administrator's authorization after the
fact and reconciles the two Story 8.3 rows and the sprint-status annotations
to match what Story 8.6 actually shipped. The Administrator confirmed during
this review that the pivot was their genuine, intentional direction, and
elected to backfill the record rather than revert the completed migration.

### Current repository evidence (verified 2026-08-02)

- Root EventStore gitlink and checkout at HEAD: `4bcf2484a09eb26490cb2d32ceb6df8949f90cc6`
  (`v3.89.0-7-g4bcf2484`) — this is a *later* commit than the `c590590b`
  identity Story 8.6 was actually built and tested against; it moved again
  during Story 8.7's own 2026-08-01 revalidation of the G5 payload-protection
  row, which already records `4bcf2484` for its own unrelated purpose.
- Story 8.6 is `review`; its Dev Agent Record and File List document the full
  migration and deletion, all under `c590590b`.
- All 18 previously-governed rollback artifacts and all 10
  `NotImplementedException` fallbacks are confirmed removed (bmad-code-review,
  Group 1, 2026-08-02).
- No SCP or matrix row authorizes `c590590b` or `4bcf2484` today.

## 2. Impact Analysis

### Epic and story impact

- Epic 8 remains viable, post-MVP maintenance work, `in-progress`.
- No epic or story is added, removed, redefined, or resequenced.
- Story 8.6 stays `review`; this proposal does not change its status —
  outstanding code-review findings from the same review pass (an erasure
  checkpoint defect, two stale actor-type-name references, a narrow fitness
  guardrail) are tracked separately in the story's Review Findings section and
  are out of scope here.
- Stories 8.7–8.10 retain their existing independent prerequisites; the G5
  payload-protection row's `4bcf2484` identity was already validated
  independently for Story 8.7's own purpose and is unaffected by this
  proposal.

### Artifact impact

- **PRD:** no conflict and no edit. Epic 8 is post-MVP maintenance; no PRD
  functional requirement touches dependency-identity selection.
- **Epics:** no semantic conflict and no edit.
- **Architecture:** no conflict and no edit. Spine invariants I3/I4/I8/I9/I10
  (dependency identity, parity, GDPR reads, rebuild, rollback) remain
  controlling and are satisfied by Story 8.6's recorded parity/rebuild/rollback
  evidence — this proposal ratifies *which* identity satisfies them, it does
  not relax the invariants themselves.
- **UX:** not applicable.
- **Story/spec:** no semantic edit to Story 8.6's acceptance criteria; AC1's
  gate condition ("the row records owner-approved additive parity or explicit
  already-available proof") is satisfied retroactively by this proposal's
  Administrator authorization, recorded in the matrix.
- **Story 8.3 matrix:** two rows (`EventStore projection/query SDK`,
  `EventStore DataProtection`) require a current-identity and
  current-authorization correction.
- **Sprint status:** the Epic 7/8 projection annotations and the Story 8.6
  comment block require the same correction; action statuses are otherwise
  unchanged.

### Technical impact

- No production source or test code change is authorized or required by this
  proposal — Story 8.6's migration and deletion are already complete and
  merged.
- No further EventStore checkout movement is required by this proposal. The
  current pin (`4bcf2484`) is one commit later than what Story 8.6 was tested
  against (`c590590b`) because of Story 8.7's independent, unrelated G5
  revalidation. This proposal flags that gap as a **follow-up action** (open,
  not resolved here): re-run Story 8.6's focused projection/query regression
  at the current pin before treating `4bcf2484` as re-validated for Story
  8.6's purposes. Until that action closes, treat Story 8.6's tested-and-green
  evidence as attached to `c590590b`, not to whatever the shared root gitlink
  currently points to.

## 3. Recommended and Approved Approach

**Selected: Direct Adjustment — ratify the Administrator's real-time
authorization and reconcile the two Story 8.3 rows plus sprint-status
annotations to the identity actually shipped.**

- **Effort:** Low (tracking-text only; the code work is already done).
- **Risk:** Low for the documentation correction itself. The residual risk is
  the process gap this proposal exists to close: real-time deviations from an
  approved identity should be captured as they happen (or immediately after),
  not reconstructed during a later code review. Flagging this pattern so
  future stories log an in-session SCP note at the moment of a direct-verbal
  pivot, rather than relying on a subsequent review to catch the drift.
- **Timeline impact:** None. Story 8.6 remains in `review`; this proposal does
  not block or accelerate its disposition.

Alternatives considered:

- **Revert the deletion, re-run migration once `fa2d1c99`-compatible
  proof exists:** rejected by the Administrator. The migration is complete,
  tested (452/452), and functionally sound; the defect is in the paper trail,
  not the shipped code. Reverting ~9,000 lines of validated deletion and
  redoing the migration later would discard real, working effort to fix a
  documentation gap.
- **MVP review:** not applicable — Epic 8 is post-MVP maintenance.
- **Leave the matrix/SCP trail as-is:** rejected. The matrix text currently
  contradicts the shipped code ("no Story 8.6 adoption is authorized in the
  current checkout"), which would mislead the next reader of that row,
  including automated gate checks in future stories that key off matrix
  status text.

## 4. Detailed Change Proposals

### 4.1 Story 8.3 — EventStore projection/query SDK row

Artifact: `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

Keep the row status `available`. Replace the `fa2d1c99`-blocked narrative with:

```text
Story 8.6 completed its projection/query SDK migration on 2026-08-01 against
EventStore v3.89.0 (c590590bc581a3f72ef6e67148eda988ba4b8fe6), under the
Administrator's explicit direct authorization to use the latest EventStore
release after the formally owner-approved identity fa2d1c9910f8976553
adb33dcdb1c9ff2ea75594 failed to compile (missing tenant-shared rebuild API).
This authorization is recorded in sprint-change-proposal-2026-08-02-story-8-6-
eventstore-identity-authorization-backfill.md, approved 2026-08-02. All 18
previously-governed rollback artifacts and all 10 catch(NotImplementedException)
fallbacks are removed; full regression passed 452/452 at c590590b. The current
root gitlink has since moved to 4bcf2484a09eb26490cb2d32ceb6df8949f90cc6
(Story 8.7's independent G5 revalidation) — Story 8.6's regression evidence
is not yet re-confirmed at that later pin; re-run the focused projection/query
suite before treating 4bcf2484 as validated for Story 8.6's purposes.
```

### 4.2 Story 8.3 — EventStore DataProtection row

Artifact: same file.

Keep the row status `available`. Replace the `fa2d1c99`-only narrative with a
parallel note recording that Story 8.6 adopted the cursor/DataProtection
surfaces at `c590590b` under the same Administrator authorization, with the
same current-pin caveat as 4.1.

### 4.3 Sprint-status projection annotations

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

- Update the Epic 7 action's projection-identity note and the Story 8.6
  comment block to record `c590590b`/v3.89.0 as the Administrator-authorized
  identity Story 8.6 shipped against, referencing this proposal.
- Leave the Epic 7 action's open/closed state and the Story 8.6 development
  status unchanged — this proposal only corrects the identity/authorization
  narrative, not workflow state.

### 4.4 Explicit no-change decisions

- Do not edit the PRD, epics, architecture, UX, Story 8.6's acceptance
  criteria, or `spec-8-6`.
- Do not move the EventStore gitlink or checkout in this workflow.
- Do not delete, narrow, or unregister any code as part of this proposal —
  Story 8.6's deletion is already complete; nothing further is authorized or
  requested here.
- Do not close the follow-up action recorded in §2 (re-validate at the current
  `4bcf2484` pin) — it stays open until someone runs that regression.

## 5. Implementation Handoff

**Scope classification: Minor.** Implementation is limited to evidence and
tracking text.

| Recipient | Responsibility |
| --- | --- |
| Parties Developer (Amelia) | Apply the two matrix-row edits and the sprint-status annotation update exactly as specified in §4. |
| Test Architect (Murat) | Own the open follow-up action: re-run Story 8.6's focused projection/query regression at the current EventStore pin (`4bcf2484` or whatever it is at execution time) and record the result in `tests/test-summary.md`. |
| Product Owner | No action required; Story 8.6 stays `review` pending its separate code-review findings. |

### Success criteria

1. Both matrix rows record `c590590b`/v3.89.0 as the identity Story 8.6
   shipped against, under Administrator authorization, with a pointer to this
   proposal.
2. Neither row claims Story 8.6 is unauthorized or blocked.
3. Sprint-status Epic 7/8 annotations reflect the same identity/authorization
   facts.
4. The current-pin re-validation gap is recorded as an open follow-up action,
   not silently dropped.
5. No PRD, epics, architecture, UX, or Story 8.6 acceptance-criteria edits.
6. No further code, test, or dependency-checkout changes.

## Change-Analysis Checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Story 8.6, surfaced during bmad-code-review. |
| 1.2 Core problem | [x] Done | Approved identity failed to compile; Administrator's real-time pivot to a different identity was never recorded in the SCP/matrix chain. |
| 1.3 Supporting evidence | [x] Done | 2026-08-01 SCP text, matrix row text, Story 8.6 Dev Agent Record, 1-20 proof packet frontmatter, current `git ls-tree` pin — all cited above. |
| 2.1 Current epic viability | [x] Done | Epic 8 remains viable; no change. |
| 2.2 Epic-level changes | [N/A] Skip | No epic scope change. |
| 2.3 Remaining epics | [x] Done | No future epic invalidated. |
| 2.4 New epic required | [N/A] Skip | Existing Epic 8 actions own the work. |
| 2.5 Priority/order | [N/A] Skip | No resequencing. |
| 3.1 PRD | [N/A] Skip | No product or MVP conflict. |
| 3.2 Architecture | [x] Done | I3/I4/I8/I9/I10 remain controlling; this proposal records which identity satisfies them. |
| 3.3 UX | [N/A] Skip | No user-facing change. |
| 3.4 Other artifacts | [x] Done | Two matrix rows and sprint-status annotations identified. |
| 4.1 Direct adjustment | [x] Viable | Selected. |
| 4.2 Potential rollback | [x] Not viable | Rejected by the Administrator; migration is complete and tested. |
| 4.3 MVP review | [x] Not viable | Epic 8 is post-MVP maintenance. |
| 4.4 Recommended path | [x] Done | Ratify authorization, reconcile identity evidence, flag the current-pin re-validation gap. |
| 5.1–5.5 Proposal components | [x] Done | Issue, impact, edits, action plan, handoff defined. |
| 6.1–6.2 Final review | [x] Done | Proposal is evidence-backed and internally consistent. |
| 6.3 Explicit approval | [x] Done | Administrator approved on 2026-08-02. |
| 6.4 Sprint-status update | [x] Done | Annotation-only update applied; statuses unchanged. |
| 6.5 Handoff | [x] Done | Routed above. |

## Workflow Execution Log

- Approved approach: Direct Adjustment.
- Change scope: Minor.
- Applied artifacts: Story 8.3 prerequisite matrix (two rows), sprint-status
  projection annotations, and this finalized proposal.
- Revalidated without edits: PRD, epics, architecture spine, UX artifacts,
  Story 8.6 acceptance criteria, `spec-8-6`.
- Open follow-up: re-validate Story 8.6's projection/query regression at the
  current EventStore pin (moved to `4bcf2484` by unrelated Story 8.7 work
  since Story 8.6 was tested).
- Handoff: Developer applies the matrix/sprint-status text edits; Test
  Architect owns the current-pin re-validation follow-up.
