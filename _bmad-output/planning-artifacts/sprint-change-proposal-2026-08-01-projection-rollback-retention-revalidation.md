---
title: Sprint Change Proposal — Projection Rollback Retention After Exact EventStore Pin
date: 2026-08-01
author: Administrator
workflow: bmad-correct-course
mode: incremental
scope_classification: minor
status: implemented
approval_required: false
approval: approved
approved_at: '2026-08-01T00:27:31+02:00'
approved_by: Administrator
trigger: >
  Keep projection rollback-only paths until Epic 8 proves EventStore SDK
  projection/query parity, GDPR processing-record reads, rebuild behavior, and
  rollback replacement.
baseline:
  parties_commit: 8c7b4e6ddb2bce9a0d3041cc2e8f3d6153e45c3c
  eventstore_gitlink: fa2d1c9910f8976553adb33dcdb1c9ff2ea75594
  eventstore_checkout: fa2d1c9910f8976553adb33dcdb1c9ff2ea75594
  eventstore_authorizing_commit: 1b219d39cfa8f0349175c356001ba539bfb4aa92
related:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-projection-rollback-retention.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11-eventstore-owner-sdk-parity-evidence-gate.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-31.md
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - references/Hexalith.EventStore/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
---

# Sprint Change Proposal — Projection Rollback Retention After Exact EventStore Pin

## 1. Issue Summary

The approved 2026-07-07 projection rollback-retention proposal remains the
governing deletion guardrail for Story 8.6. Parties-local projection, query,
rebuild, adapter, health, and fallback paths must remain until Epic 8 proves:

1. EventStore SDK projection/query parity.
2. GDPR Art. 30 processing-record reads and related no-leak behavior.
3. Full rebuild behavior verified against aggregate replay.
4. A tested rollback replacement for the SDK path.

The owner prerequisite and the consumer deletion gate are separate. EventStore
authorizing commit `1b219d39cfa8f0349175c356001ba539bfb4aa92`
records `final_decision: available` and `authorize_consumer_migration: true` for
exact source and tested-runtime SHA
`fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`, with exact package and container
identities. Parties commit `8c7b4e6ddb2bce9a0d3041cc2e8f3d6153e45c3c`
now selects that exact source. The root gitlink and current checkout both equal
`fa2d1c9...` (`v3.82.0-24-gfa2d1c99`). The dependency-identity selection gate is
therefore satisfied.

That selection does not prove the Parties consumer boundary. Concurrent Story
8.6 work adds exact SDK ACL routes and query handlers, but no accepted evidence
yet proves the deployed routing/security topology; coordinated erasure remains
unproven; and no Parties evidence packet yet records parity, GDPR reads/no-leak
behavior, rebuild-vs-replay, or rollback. Story 8.6 may be `in-progress`, but
rollback-path retirement and deletion remain blocked by those evidence gates.

### Current repository evidence (verified 2026-08-01)

- Root EventStore gitlink and checkout:
  `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`.
- EventStore source description: `v3.82.0-24-gfa2d1c99`.
- EventStore authorizing commit: `1b219d39...`; its packet approves only the
  exact `fa2d1c9...` source, package, and container identities.
- All cited projection/query and DataProtection surfaces exist at `fa2d1c9...`.
- Story 8.6 is `in-progress`; this is execution status, not deletion authority.
- The Epic 7 rollback-retention action is `open`.
- The Epic 8 consumer-proof action is `in-progress`.
- All 18 governed rollback artifacts remain present.
- All 10 `catch (NotImplementedException)` fallback branches remain present:
  seven in `PartyDetailProjectionActorExtensions` and three in
  `PartyIndexProjectionQueryActor`.

## 2. Impact Analysis

### Epic and story impact

- Epic 8 remains viable, post-MVP maintenance work, and `in-progress`.
- No epic or story is added, removed, redefined, or resequenced.
- Story 8.6 remains `in-progress`; adoption work may proceed, but rollback-path
  retirement and deletion remain closed until consumer evidence is recorded.
- Stories 8.7–8.10 retain their existing independent prerequisites.
- No new PRD functional requirement or MVP scope is introduced.

### Artifact impact

- **PRD:** no conflict and no edit.
- **Epics:** no semantic conflict and no edit; Story 8.6 already names the
  deletion preconditions.
- **Architecture:** no conflict and no edit; spine invariants I3, I4, I8, I9,
  and I10 govern dependency identity, parity, GDPR reads, rebuild, and rollback.
- **UX:** not applicable; no user flow, component, accessibility, or regulated
  copy changes.
- **Story/spec:** no semantic edit required. The existing Story 8.6 artifacts
  already prohibit deletion before parity and rebuild evidence.
- **Story 8.3 matrix:** two evidence cells require current exact-identity facts.
- **Sprint status:** two projection-specific comments require current gate facts;
  their action statuses remain unchanged.

### Technical impact

- No production source or test code change is authorized by this proposal.
- No EventStore source edit, checkout movement, or further gitlink change.
- No projection/query/rebuild artifact deletion or fallback-branch removal.
- No state-store, read-model, build, deployment, API, or DAPR ACL change.

## 3. Recommended and Approved Approach

**Selected: Direct Adjustment — reconcile exact-identity evidence and keep the
consumer deletion gate closed.**

Update only the two EventStore rows in the Story 8.3 prerequisite matrix and the
two projection-specific sprint-status comments. Record that exact dependency
selection is complete, while retaining every consumer proof and rollback exit
criterion. This removes stale identity evidence without treating owner-side
availability as Parties-side parity.

- **Effort:** Low.
- **Risk:** Low while the retention guardrail remains closed; high if the exact
  pin is mistaken for consumer cutover or deletion authority.
- **Timeline impact:** No MVP impact. Story 8.6 can execute adoption and evidence
  work, but rollback retirement and deletion remain gated.

Alternatives considered:

- **Potential rollback:** not viable. Nothing unsafe has migrated, and the
  retained local implementation is the rollback mechanism.
- **MVP review:** not applicable. Epic 8 is post-MVP maintenance.
- **Immediate migration or deletion:** rejected. The Parties consumer evidence
  and transport topology are not complete.

## 4. Detailed Change Proposals

### 4.1 Story 8.3 — EventStore projection/query SDK row

Artifact:
`_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

Keep the row status `available`, meaning the owner surface exists. Replace the
stale `e4618d9...` identity-mismatch statement with:

```text
Parties now selects root gitlink and clean checkout
fa2d1c9910f8976553adb33dcdb1c9ff2ea75594
(v3.82.0-24-gfa2d1c99), exactly matching the source/tested-runtime identity
authorized by EventStore commit 1b219d39cfa8f0349175c356001ba539bfb4aa92.
The dependency-identity gate is satisfied. Owner availability does not authorize
rollback-path retirement or deletion: Story 8.6 must still prove deployed
command/query/projection/rebuild routing and security, coordinated erasure,
projection/query parity, GDPR processing-record reads and no-leak behavior,
rebuild-vs-replay, and rollback.
```

Keep the rollback decision unchanged: all local actors, rebuild services,
adapters, freshness fallback, and registrations remain until the consumer gates
pass.

### 4.2 Story 8.3 — EventStore DataProtection row

Keep the row status `available`. Replace the stale `e4618d9...` identity with
`fa2d1c9...` and record that these three surfaces were verified at that object:

- `EventStoreDataProtectionServiceCollectionExtensions.cs`
- `DaprXmlRepository.cs`
- `QueryCursorCodecServiceCollectionExtensions.cs`

Keep the local cursor path and deletion guard closed while Story 8.6 adopts the
surfaces behind the rollback seam and proves cursor purpose and scope
compatibility, persisted DAPR key-ring behavior, projection/query transport
topology, and rollback.

### 4.3 Sprint-status projection annotations

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

- Keep the Epic 7 action `open`. Record the exact `fa2d1c9...` selection, all
  18 retained rollback artifacts, all 10 fallback branches, and the remaining
  consumer proof gates.
- Keep the Epic 8 reconciliation action `in-progress`. Record identity selection
  as complete and parity, GDPR reads/no-leak, rebuild-vs-replay, routing,
  coordinated erasure, and rollback as open.
- Preserve Story 8.6 `in-progress` and all unrelated status edits.

### 4.4 Explicit no-change decisions

- Do not edit the PRD, epics, architecture, UX, Story 8.6, or `spec-8-6`.
- Do not close the Epic 7 rollback-retention action.
- Do not mark the Epic 8 consumer-proof action `done`.
- Do not move the EventStore gitlink or checkout in this workflow.
- Do not delete, narrow, or unregister any governed rollback-only path.

## 5. Implementation Handoff

**Scope classification: Minor.** Implementation is limited to evidence and
tracking text. No backlog reorganization or production change is authorized.

| Recipient | Responsibility |
| --- | --- |
| Parties Developer (Amelia) | Apply only the approved matrix and sprint-comment updates; preserve every rollback-only path. |
| Architect (Winston) | Confirm the exact pin remains selected and approve the distributed SDK transport topology before cutover. |
| Test Architect (Murat) | Verify projection/query parity, Art. 30 and no-leak reads, rebuild-vs-replay, coordinated erasure, and rollback before deletion. |
| Product Owner | Keep the Epic 7 action `open` and Epic 8 consumer-proof action `in-progress` until all exit criteria pass. |

### Success criteria

1. The matrix records exact EventStore source selection without conflating it
   with consumer validation.
2. Projection/query and DataProtection rows remain `available` as owner-surface
   classifications.
3. Story 8.6 remains `in-progress`; rollback-path retirement and deletion remain
   blocked by explicit consumer gates.
4. The Epic 7 action remains `open`; the Epic 8 action remains `in-progress`.
5. All 18 governed artifacts and all 10 fallback branches remain intact.
6. No deletion occurs before parity, GDPR reads/no-leak, rebuild-vs-replay,
   topology, coordinated-erasure, and rollback evidence is green and recorded.
7. No PRD functional coverage, UX, or MVP classification changes.

## Change-Analysis Checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Story 8.6 and its open Epic 7 retention action. |
| 1.2 Core problem | [x] Done | Exact owner identity is selected, but consumer evidence and safe deletion are incomplete. |
| 1.3 Supporting evidence | [x] Done | Authorizing packet, SHAs, tag, surface inventory, rollback inventory, fallback count, and sprint statuses verified. |
| 2.1 Current epic viability | [x] Done | Epic 8 remains viable through consumer validation. |
| 2.2 Epic-level changes | [N/A] Skip | No epic scope change. |
| 2.3 Remaining epics | [x] Done | No future epic is invalidated. |
| 2.4 New epic required | [N/A] Skip | Existing Epic 8 actions own the work. |
| 2.5 Priority/order | [x] Done | Exact identity precedes consumer validation; consumer proof precedes deletion. |
| 3.1 PRD | [N/A] Skip | No product or MVP conflict. |
| 3.2 Architecture | [x] Done | I3/I4/I8/I9/I10 remain controlling. |
| 3.3 UX | [N/A] Skip | No user-facing change. |
| 3.4 Other artifacts | [x] Done | Two matrix rows and two sprint comments were reconciled after final approval. |
| 4.1 Direct adjustment | [x] Viable | Selected; tracking evidence only. |
| 4.2 Potential rollback | [x] Not viable | The retained local implementation is the rollback mechanism. |
| 4.3 MVP review | [x] Not viable | Epic 8 is post-MVP maintenance. |
| 4.4 Recommended path | [x] Done | Reconcile identity evidence and retain all consumer gates. |
| 5.1–5.5 Proposal components | [x] Done | Issue, impact, edits, action plan, and handoff are defined. |
| 6.1–6.2 Final review | [x] Done | Proposal is evidence-backed and internally consistent. |
| 6.3 Explicit approval | [x] Done | Administrator approved the whole proposal on 2026-08-01. |
| 6.4 Sprint-status update | [x] Done | Comment-only updates applied; statuses stayed unchanged. |
| 6.5 Handoff | [x] Done | Approved edits applied, validated, and routed. |

## Incremental Review Record

- Edit 1, projection/query row: approved, then rebaselined after exact pin.
- Edit 2, DataProtection row: approved, then rebaselined after exact pin.
- Edit 3, sprint-status comments: approved, then rebaselined after exact pin.
- Combined rebaselined amendment: approved by Administrator on 2026-08-01.
- Whole-proposal approval: approved by Administrator on 2026-08-01.

## Workflow Execution Log

- Approved approach: Direct Adjustment.
- Change scope: Minor.
- Applied artifacts: Story 8.3 prerequisite matrix, sprint-status projection
  annotations, and this finalized proposal.
- Revalidated without edits: PRD, epics, architecture spine, UX artifacts,
  Story 8.6, and `spec-8-6`.
- Preserved concurrent work: Story 8.6 status/spec/context, SDK query handlers,
  ACL and host changes, readiness maintenance-scope edits, and the separate
  crypto/key-management proposal.
- Validation: matching `fa2d1c9...` gitlink/checkout; matrix rows remain
  `available`; Story 8.6 remains `in-progress`; Epic 7 action remains `open`;
  Epic 8 action remains `in-progress`; all 18 rollback artifacts and all 10
  fallback branches remain; sprint YAML parses; `git diff --check` passes.
- Handoff: Developer retains rollback paths, Architect governs topology and
  cutover, Test Architect owns consumer evidence, and Product Owner retains the
  open/in-progress gates until the success criteria pass.
