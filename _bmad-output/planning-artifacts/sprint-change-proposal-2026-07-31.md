---
title: Sprint Change Proposal — Projection Rollback Retention Revalidation After EventStore Story 1.20
date: 2026-07-31
author: Administrator
workflow: bmad-correct-course
mode: incremental
scope_classification: minor
status: approved
approval_required: false
approved: 2026-07-31T19:23:49+02:00
approved_by: Administrator
trigger: >
  Keep projection rollback-only paths until Epic 8 proves EventStore SDK
  projection/query parity, GDPR processing-record reads, rebuild behavior, and
  rollback replacement.
related:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-projection-rollback-retention.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11-eventstore-owner-sdk-parity-evidence-gate.md
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - references/Hexalith.EventStore/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
---

# Sprint Change Proposal — Projection Rollback Retention Revalidation After EventStore Story 1.20

## 1. Issue Summary

The approved 2026-07-07 projection rollback-retention proposal remains the
governing deletion guardrail for Story 8.6. It requires Parties-local projection,
query, rebuild, adapter, health, and fallback paths to remain until Epic 8 proves:

1. EventStore SDK projection/query parity.
2. GDPR Art.30 processing-record reads and related no-leak behavior.
3. Full rebuild behavior verified against aggregate replay.
4. A proven rollback replacement for the SDK path.

The trigger for this revalidation is a material owner-side evidence change.
Hexalith.EventStore Story 1.20 is now `done`, and its owner-approved proof packet
records `final_decision: available` and `authorize_consumer_migration: true` for
the exact tested source SHA
`fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` and its recorded package/container
identities. However, Parties currently selects EventStore source
`e4618d9114c8824fd50fdfc8d135438aa261377c` (`v3.86.0`). The selected commit is
a descendant of the approved source, but the Story 1.20 packet explicitly grants
authority only to its exact approved identities. Parties therefore has not yet
reconciled its selected consumption identity with that packet.

The Story 8.3 prerequisite matrix still contains older 2026-07-16 evidence saying
Story 1.20 is `ready-for-dev`, no closure packet exists, and Parties selects
`82ed167c...`. That evidence is now factually stale. The matrix row must be
reconciled without weakening the consumer-side deletion gate.

### Current repository evidence (verified 2026-07-31)

- Root EventStore gitlink: `e4618d9114c8824fd50fdfc8d135438aa261377c`.
- EventStore checkout: `e4618d9114c8824fd50fdfc8d135438aa261377c`, tag
  `v3.86.0`; root gitlink and checkout match.
- EventStore Story 1.20 packet: exact source/tested runtime
  `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`, final decision `available`,
  consumer migration authorized for the packet's exact identities.
- `fa2d1c9...` is an ancestor of `e4618d9...`; ancestry does not substitute for
  the packet's exact-identity requirement.
- Story 8.6 remains `blocked`.
- All 18 governed projection/query/rebuild/adapter artifacts are present.
- All 10 `catch (NotImplementedException)` rollback branches remain present
  (seven in `PartyDetailProjectionActorExtensions`, three in
  `PartyIndexProjectionQueryActor`).
- The Parties worktree was clean before this proposal was created.

## 2. Impact Analysis

### Epic impact

- Epic 8 remains viable and `in-progress`.
- No epic is added, removed, redefined, or resequenced.
- The sequence `8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10` remains unchanged.
- Epic 8 remains post-MVP maintenance and adds no PRD functional requirement.

### Story impact

- Story 8.6 remains `blocked`.
- Story 8.6 acceptance criteria already distinguish the owner prerequisite from
  the Parties parity/rebuild/deletion gate; no story edit is required.
- EventStore Story 1.20 closes the historical missing-owner-packet condition only
  for its exact approved identities. It does not provide Parties consumer parity,
  GDPR read parity, a Parties rebuild comparison, or Parties rollback proof.
- Stories 8.7–8.10 retain their existing independent prerequisites.

### Artifact conflicts

- **PRD:** no conflict and no edit. MVP scope and feature coverage are unchanged.
- **Epics:** no semantic conflict and no edit. Story 8.6 already names the four
  deletion preconditions.
- **Architecture:** no conflict and no edit. Spine invariants I3, I4, I8, I9,
  and I10 already govern exact dependency identity, parity, rebuild, GDPR reads,
  and rollback.
- **UX:** not applicable. No user flow, component, accessibility, or regulated
  copy changes.
- **Story/spec:** no edit. Story 8.6 and `spec-8-6` already halt on an unresolved
  matrix/identity gate and prohibit deletion before parity and rebuild evidence.
- **Sprint status:** no semantic edit. It already records the Story 1.20 packet,
  the `fa2d1c9...` versus `e4618d9...` mismatch, Story 8.6 `blocked`, the Epic 7
  action `open`, and the Epic 8 reconciliation action `in-progress`.
- **Story 8.3 matrix:** factual evidence is stale and requires the single edit in
  Section 4.

### Technical impact

- No production source or test code changes.
- No EventStore submodule content edit, checkout movement, or root gitlink change.
- No projection/query/rebuild artifact deletion or fallback-branch removal.
- No state-store or read-model migration.
- No build, dependency, deployment, API, or DAPR ACL change.

## 3. Recommended Approach

**Selected: Direct Adjustment — reconcile the prerequisite evidence while keeping
the existing retention guardrail closed.**

Update only the Story 8.3 `EventStore projection/query SDK` matrix row so it
records the completed Story 1.20 owner packet and the current Parties consumption
identity. Keep the row `needs-additive-api`, Story 8.6 `blocked`, and the rollback
action `open` until an exact approved Parties dependency identity is selected and
the Parties consumer gates pass.

This is the lowest-risk course because it removes factual drift without treating
owner evidence as consumer proof. The existing 2026-07-07 approved proposal
continues to govern the retention set and exit criteria.

- **Effort:** Low.
- **Risk:** Low while the guardrail remains closed; high if owner evidence is
  incorrectly treated as deletion authority.
- **Timeline impact:** No MVP impact. Story 8.6 remains paused pending identity
  reconciliation and consumer evidence.

Alternatives considered:

- **Potential rollback:** not viable. Nothing unsafe has migrated; the retained
  local implementation is the rollback mechanism.
- **MVP review:** not applicable. Epic 8 is post-MVP maintenance.
- **Immediate row promotion/deletion authorization:** rejected. The packet's exact
  identity is not the Parties-selected identity, and none of the four Parties
  consumer deletion proofs has been recorded.

## 4. Detailed Change Proposals

### 4.1 Story 8.3 projection/query prerequisite row

Artifact:
`_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

Section: `EventStore projection/query SDK` row, evidence and validation cells.

**OLD evidence summary:**

```text
Revalidated 2026-07-16: the historical Story 1.8 packet remains `still
blocked`; owner remediation advanced to active Story 1.20 (`ready-for-dev`),
predecessor Story 1.19 is in `review`, and no Story 1.20 closure packet or named
owner-approved `available` decision with exact runtime identities exists. The
Parties gitlink and checked-out EventStore commit both equal `82ed167c...`, but
Story 1.20 has not approved that SHA, so the row remains `needs-additive-api`.
```

**NEW evidence summary:**

```text
Revalidated 2026-07-31: EventStore Story 1.20 is `done`; its owner-approved
proof packet records `final_decision: available` and
`authorize_consumer_migration: true` for exact source/tested-runtime SHA
`fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` and its recorded package/container
identities. Parties currently selects root gitlink and checkout
`e4618d9114c8824fd50fdfc8d135438aa261377c` (`v3.86.0`). Although
`fa2d1c9...` is an ancestor of `e4618d9...`, the packet authorizes only its
exact identities; Parties has not selected or validated an exact approved
consumption identity. Keep the row `needs-additive-api` and Story 8.6 blocked
until identity reconciliation is recorded. Owner availability does not
authorize deletion: Story 8.6 must still prove Parties projection/query parity,
GDPR processing-record and no-leak behavior, rebuild-vs-aggregate-replay, and
rollback before removing any rollback-only path.
```

Replace the stale validation observations with exact references/checks for:

- the Story 1.20 proof packet decision and approved source SHA;
- `git ls-tree HEAD references/Hexalith.EventStore` -> `e4618d9...`;
- `git -C references/Hexalith.EventStore rev-parse HEAD` -> `e4618d9...`;
- `git -C references/Hexalith.EventStore describe --tags --always --dirty` ->
  `v3.86.0`;
- the verified ancestor relationship, explicitly noted as insufficient for the
  packet's exact-identity requirement.

Keep the owner, touched-story, rollback, and proof-requirement cells unchanged.

**Rationale:** The row must reflect the real owner outcome while staying
fail-closed on the unresolved Parties identity and consumer deletion gates.

### 4.2 Explicit no-change decisions

- Do not change the PRD, epics, architecture, UX, Story 8.6, or `spec-8-6`.
- Do not change Story 8.6 from `blocked`.
- Do not close the Epic 7 rollback-retention action.
- Do not mark the Epic 8 identity/parity reconciliation action `done`.
- Do not move the EventStore gitlink or checkout in this workflow.
- Do not delete, narrow, or unregister any governed rollback-only path.

## 5. Implementation Handoff

**Scope classification: Minor.** The approved implementation is one factual
matrix-row reconciliation. No backlog reorganization or production change is
authorized.

| Recipient | Responsibility |
| --- | --- |
| Parties Developer (Amelia) | Apply the approved matrix-row evidence update only. Preserve all rollback-only paths and Story 8.6 `blocked`. |
| Architect (Winston) | Select or approve an exact EventStore consumption identity before allowing Story 8.6 source migration; do not accept ancestry alone as packet identity proof. |
| Test Architect (Murat) | After identity reconciliation, verify the Parties parity harness, Art.30/no-leak reads, rebuild-vs-replay result, and rollback proof before deletion. |
| Product Owner | Keep the Epic 7 action `open` and the Epic 8 reconciliation action `in-progress` until their respective exit criteria are met. |

### Success criteria

1. The matrix row accurately records the Story 1.20 `available` decision and its
   exact approved identity.
2. The matrix row accurately records Parties' current `e4618d9...`/`v3.86.0`
   identity and explains why it is not yet an exact packet match.
3. The row stays `needs-additive-api`; Story 8.6 stays `blocked`.
4. All 18 governed artifacts and all 10 fallback branches remain intact.
5. No deletion occurs before Parties projection/query parity, GDPR reads/no-leak,
   rebuild-vs-replay, and rollback proof are green and recorded.
6. No PRD functional coverage or MVP classification changes.

## Change-Analysis Checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Story 8.6 and its open Epic 7 retention action. |
| 1.2 Core problem | [x] Done | Stale owner evidence could be mistaken for either continued owner absence or deletion authority. |
| 1.3 Supporting evidence | [x] Done | Story 1.20 packet, exact SHAs, current tag, rollback-set inventory, fallback count, and sprint status verified. |
| 2.1 Current epic viability | [x] Done | Epic 8 remains viable after identity reconciliation and consumer proof. |
| 2.2 Epic-level changes | [N/A] Skip | No epic scope change. |
| 2.3 Remaining epics | [x] Done | No future epic is invalidated. |
| 2.4 New epic required | [N/A] Skip | Existing Epic 8 actions own the work. |
| 2.5 Priority/order | [x] Done | Exact identity precedes Story 8.6 migration; consumer proof precedes deletion. |
| 3.1 PRD | [N/A] Skip | No product or MVP conflict. |
| 3.2 Architecture | [x] Done | I3/I4/I8/I9/I10 already govern. |
| 3.3 UX | [N/A] Skip | No user-facing change. |
| 3.4 Other artifacts | [x] Done | Story 8.3 matrix evidence reconciled after final approval. |
| 4.1 Direct adjustment | [x] Viable | Selected; factual evidence reconciliation only. |
| 4.2 Potential rollback | [x] Not viable | The retained local path is the rollback mechanism. |
| 4.3 MVP review | [x] Not viable | Epic 8 is post-MVP maintenance. |
| 4.4 Recommended path | [x] Done | Reconcile evidence and keep both deletion gates closed. |
| 5.1–5.5 Proposal components | [x] Done | Issue, impact, edit, action plan, and handoff are defined. |
| 6.1–6.2 Final review | [x] Done | Proposal is evidence-backed and internally consistent. |
| 6.3 Explicit approval | [x] Done | Administrator approved on 2026-07-31. |
| 6.4 Sprint-status update | [N/A] Skip | No epic/story/action status or numbering change. |
| 6.5 Handoff | [x] Done | Matrix reconciliation applied; future identity and parity responsibilities routed below. |

## Approval and Handoff Execution Log

- Specific matrix-row edit approved during incremental review.
- Complete Sprint Change Proposal approved by Administrator on
  `2026-07-31T19:23:49+02:00`.
- Story 8.3 projection/query matrix evidence reconciled to the completed
  EventStore Story 1.20 packet and current Parties EventStore identity.
- Story 8.6 remains `blocked`; the matrix row remains `needs-additive-api`.
- Epic 7 rollback-retention action remains `open`; Epic 8 identity/parity action
  remains `in-progress`.
- Handoff routed to the Parties Developer for preservation, the Architect for
  exact identity selection, the Test Architect for consumer parity evidence,
  and the Product Owner for status hygiene.
