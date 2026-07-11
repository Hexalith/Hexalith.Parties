---
title: Sprint Change Proposal — EventStore Owner SDK Parity Evidence Gate
date: 2026-07-11
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: proposed
approval_required: true
trigger: >
  EventStore owners must provide the required SDK parity evidence before Story
  8.6 resumes or any rollback-only path is deleted.
related:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-projection-rollback-retention.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11.md
  - _bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - references/Hexalith.EventStore/_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-parity-proof.md
  - references/Hexalith.EventStore/_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md
---

# Sprint Change Proposal — EventStore Owner SDK Parity Evidence Gate

## 1. Issue Summary

Story 8.6 is correctly blocked, but the current Parties tracking does not record
the outcome of the EventStore owner-proof work completed on 2026-07-10. That
proof packet returned **`still blocked`**, not `available`, and its approval is
still pending. Story 8.6 therefore must not resume production migration, and no
Parties-local projection/query rollback path may be deleted.

This proposal makes the two sequential gates explicit:

1. **Resume gate — EventStore owner responsibility.** Story 8.6 may resume only
   after EventStore owners produce and approve a proof packet whose final
   decision is `available`, every AC1 item is satisfied, and the Parties checkout
   uses the approved EventStore SHA.
2. **Deletion gate — Parties responsibility after resumption.** Even after the
   owner packet opens the resume gate, rollback-only paths remain until the
   Parties parity harness, GDPR read checks, full rebuild-vs-aggregate-replay
   comparison, and rollback replacement are green and recorded.

### Current evidence

- EventStore Story 1.8 is `done` as an inspection/proof task, but its proof
  packet final decision is **`still blocked`**.
- Owner proof-result approval is pending: no PR, reviewer, or approval date is
  recorded.
- Required items currently classified `blocked`:
  - G3 read-model erasure plus coordinated checkpoint erasure;
  - G10 index batching or an explicitly approved equivalent;
  - G6 mapping for all Parties freshness semantics;
  - duplicate/out-of-order idempotency through the projection-handler path;
  - full rebuild verification against canonical aggregate replay.
- Additional blocking SDK constraints are recorded:
  - the synchronous projection-handler contract cannot safely use the async
    read-model persistence seam;
  - domain-only projection routing and a single response cannot produce both
    Parties detail and index projections.
- Cursor scope compatibility and the packet's intended runtime pin are the only
  required items classified `already available`.
- The proof packet records intended runtime SHA
  `f31777ae8dd3902f65a27777a04ee49d790a6e8f`; the currently checked-out
  `references/Hexalith.EventStore` SHA is
  `596c7c504293dfad147bb637655376973f64ae0e`. A consumer-pin match is therefore
  not established by the current record.
- The Story 8.3 matrix row remains `needs-additive-api`; Story 8.6 remains
  `blocked`.
- All governed rollback-only files remain present, all ten
  `catch (NotImplementedException)` fallback branches remain present, and the
  current working-tree diff contains no Parties `src/` or `tests/` migration.

## 2. Impact Analysis

### Epic impact

- Epic 8 remains viable and in progress.
- No epic is added, removed, redefined, or resequenced.
- Story order remains `8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`.
- Epic 8 remains post-MVP maintenance and adds no PRD functional requirement.

### Story impact

- Story 8.6 stays `blocked`; this proposal does not authorize source migration.
- EventStore owners must close the proof packet's blocked classifications and
  obtain proof-result approval before the Story 8.3 row can become `available`.
- After the resume gate opens, Story 8.6 starts with its Parties parity harness.
  Opening the resume gate does not authorize deletion.
- Stories 8.7–8.10 retain their existing independent prerequisites.

### Artifact conflicts

- **PRD:** no conflict and no edit. Product scope and MVP coverage are unchanged.
- **Epics:** no semantic conflict and no edit. Epic 8 already encodes the owner
  prerequisite and parity-before-deletion rule.
- **Architecture:** no conflict and no edit. Spine invariants I3, I4, I8, I9,
  and I10 already govern both gates.
- **UX:** not applicable; no user flow, component, accessibility, or regulated
  copy changes.
- **Story/spec:** no semantic edit. Story 8.6 AC1 and `spec-8-6` already require
  the owner evidence and retention behavior.
- **Prerequisite matrix:** needs a factual evidence update while retaining
  `needs-additive-api`.
- **Sprint tracking:** needs the owner action clarified and linked to the current
  `still blocked` packet.
- **EventStore submodule:** no edit in this workflow. Additive SDK implementation
  and approval remain owner-repository work.

### Technical impact

- No Parties production code change.
- No EventStore submodule edit or gitlink movement.
- No rollback-only file deletion or fallback-branch removal.
- No state-store/read-model migration.
- Owner-side effort is medium-to-high because several generic SDK behaviors and
  contracts remain missing or unproven.

## 3. Recommended Approach

**Selected: Direct Adjustment with owner handoff.** Keep Story 8.6 blocked,
record the current EventStore proof result in the prerequisite matrix, and make
the existing sprint action explicitly require an owner-approved `available`
packet before resumption. Preserve the separate Parties deletion gate.

This is a moderate coordination change: the Parties edits are documentation and
tracking only, but the condition they enforce requires EventStore owner backlog,
implementation, validation, and approval work.

- **Parties effort:** Low.
- **EventStore owner effort:** Medium-to-high; additive API and focused proof
  work is required.
- **Risk:** Medium if ownership drifts; low while both gates remain closed.
- **Timeline impact:** Story 8.6 remains paused until EventStore owners deliver.
  No MVP impact.

Alternatives:

- **Potential rollback:** not viable. The retained local implementation is the
  rollback mechanism and no unsafe Story 8.6 migration occurred.
- **MVP review:** not applicable. Epic 8 is post-MVP maintenance.

## 4. Detailed Change Proposals

### 4.1 Story 8.6 status and acceptance criteria

Artifact: `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`

**OLD:** `Status: blocked`; AC1 halts migration while the matrix row is
`needs-additive-api`.

**NEW:** No text change. Keep `Status: blocked` and AC1 unchanged.

**Rationale:** The story already encodes the correct behavior. Editing it would
duplicate the evidence source and risk weakening the gate.

### 4.2 Story 8.3 prerequisite matrix evidence

Artifact: `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

Section: `EventStore projection/query SDK` row.

**OLD (proof summary):**

```text
Story 8.6 start-gate observation on 2026-07-09 recorded pin 0f428d0...;
no owner-approved parity proof was locally available.
```

**NEW (proposed proof summary):**

```text
EventStore owner proof packet inspected 2026-07-11. Final decision: still
blocked; proof-result approval pending. Intended runtime SHA f31777a... does not
match the current Parties checkout SHA 596c7c5.... G3 erasure/checkpoint
cleanup, G10 batching/equivalent, G6 freshness mapping, projection-path
duplicate/out-of-order idempotency, and full rebuild-vs-aggregate-replay remain
blocked. The synchronous persistence seam and one-domain/one-response projection
routing also block Parties detail+index adoption. Status remains
needs-additive-api. Story 8.6 may not resume until a later owner-approved packet
returns available and the checked-out pin matches it.
```

Keep the row status **`needs-additive-api`** and retain its rollback column.
Add validation references to the owner proof packet, its final decision, and the
consumer SHA check.

**Rationale:** This replaces stale "no proof locally available" wording with the
actual proof outcome without pretending the inspection story delivered parity.

### 4.3 Sprint owner action and two-gate annotation

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

Section: existing Epic 8 EventStore-owner action.

**OLD:**

```yaml
action: "Route EventStore additive-API prerequisites to Hexalith.EventStore owners: projection/query parity ... Additive/approved before any Parties deletion."
owner: "Winston (Architect) + Hexalith.EventStore owners"
status: in-progress
```

**NEW (proposed):**

```yaml
# EventStore Story 1.8 proof packet inspected 2026-07-11: final decision
# `still blocked`; proof-result approval pending. G3, G10, G6,
# duplicate/out-of-order projection delivery, and full rebuild parity remain
# blocked, with async persistence and multi-projection routing gaps also named.
# Gate 1: Story 8.6 remains blocked until an owner-approved packet returns
# `available`, covers every AC1 item, and the Parties EventStore pin matches.
# Gate 2: after resume, no rollback-only path is deleted until the Parties
# parity harness, GDPR reads, rebuild-vs-replay, and rollback proof are green.
action: "EventStore owners must deliver an owner-approved `available` projection/query SDK parity packet before Story 8.6 resumes; Parties must then prove its consumer parity and rollback gates before deleting any rollback-only path."
owner: "Winston (Architect) + Hexalith.EventStore owners"
status: in-progress
```

Keep Story 8.6 `blocked` and keep the existing Epic 7 rollback-retention action
`open`.

**Rationale:** One sentence currently conflates owner API readiness with
consumer deletion approval. The revised action names both gates and owners.

### 4.4 EventStore owner deliverable

No EventStore submodule file is changed by this proposal. Route a follow-up in
the EventStore owner repository that produces a reviewed packet containing:

1. EventStore commit SHA intended for Parties consumption.
2. PR/reviewer/approval date.
3. Source paths, focused tests, exact commands, and results for every AC1 item.
4. Generic fixes/evidence for G3, G10, G6, projection-path duplicate/out-of-order
   idempotency, and full rebuild parity.
5. Resolution of the async projection persistence seam and multi-projection
   routing/fan-out constraints.
6. Rollback note and known limitations.
7. Final decision `available` only if every required item is satisfied.

After that owner packet exists, a Parties recorder session verifies the SHA and
updates the matrix. It does not mark Story 8.6 complete and does not authorize
deletion before the Parties parity harness runs.

## 5. Implementation Handoff

**Scope classification: Moderate.** Parties changes are tracking-only, but the
blocking work requires coordinated EventStore owner implementation and approval.

| Recipient | Responsibility |
| --- | --- |
| Hexalith.EventStore owners | Implement/prove the missing generic SDK behaviors, review the proof result, and publish an `available` packet with an approved SHA. |
| Architect (Winston) | Keep the resume gate closed; reject status-only matrix changes and evidence packets without approval or full AC1 coverage. |
| Parties Developer (Amelia) | After owner delivery, verify the SHA and record the packet; then build the Parties parity harness before touching rollback-only paths. |
| Test Architect (Murat) | Verify focused owner tests, projection delivery idempotency, rebuild-vs-replay evidence, GDPR reads, and rollback proof. |
| Product Owner | Keep Story 8.6 `blocked` and the rollback action `open` until their respective gates are met. |

### Success criteria

1. Story 8.6 remains `blocked` while the owner packet is `still blocked`, pending
   approval, incomplete, or SHA-mismatched.
2. The Story 8.3 row remains `needs-additive-api` until an owner-approved
   `available` packet covers every AC1 item.
3. Resumption requires a matching checked-out EventStore SHA.
4. Resumption starts with the Parties parity harness; it does not authorize
   deletion.
5. Rollback-only files and ten fallback branches remain until Parties parity,
   GDPR reads, rebuild-vs-replay, and rollback evidence are green.
6. No Parties production code, EventStore submodule content, gitlink, PRD,
   architecture, epics, or UX artifact changes in this course-correction step.

## Change-Analysis Checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Story 8.6 AC1 exposed the owner evidence gate. |
| 1.2 Core problem | [x] Done | Evidence/approval gap plus missing generic SDK behavior. |
| 1.3 Supporting evidence | [x] Done | Owner packet is `still blocked`; matrix is `needs-additive-api`; Story 8.6 is `blocked`; rollback paths remain. |
| 2.1 Current epic viability | [x] Done | Epic 8 remains viable after owner delivery. |
| 2.2 Epic-level changes | [N/A] Skip | No epic scope change. |
| 2.3 Remaining epics | [x] Done | No future epic invalidated. |
| 2.4 New epic required | [N/A] Skip | EventStore follow-up belongs in its existing owner backlog. |
| 2.5 Priority/order | [x] Done | Owner proof precedes Story 8.6; consumer parity precedes deletion. |
| 3.1 PRD | [N/A] Skip | No product/MVP conflict. |
| 3.2 Architecture | [x] Done | Existing I3/I4/I8/I9/I10 already govern. |
| 3.3 UX | [N/A] Skip | No user-facing change. |
| 3.4 Other artifacts | [!] Action-needed | Matrix and sprint tracking update after approval; EventStore owner follow-up required. |
| 4.1 Direct adjustment | [x] Viable | Selected; low Parties effort, medium-to-high owner effort. |
| 4.2 Potential rollback | [x] Not viable | Retained local code is the rollback mechanism. |
| 4.3 MVP review | [x] Not viable | Epic 8 is post-MVP maintenance. |
| 4.4 Recommended path | [x] Done | Two sequential gates with explicit ownership. |
| 5.1–5.5 Proposal components | [x] Done | Issue, impact, edits, handoff, and success criteria included. |
| 6.1–6.2 Final review | [x] Done | Proposal is evidence-backed and internally consistent. |
| 6.3 Explicit approval | [!] Action-needed | Awaiting Administrator approval. |
| 6.4 Sprint-status update | [!] Action-needed | Apply only after approval. |
| 6.5 Handoff | [!] Action-needed | Route after approval. |

## Approval Request

Approve this proposal to apply the matrix and sprint-tracking edits and route
the missing SDK parity work to EventStore owners. Until approval and subsequent
owner delivery, current behavior remains unchanged: Story 8.6 is blocked and all
rollback-only paths stay.
