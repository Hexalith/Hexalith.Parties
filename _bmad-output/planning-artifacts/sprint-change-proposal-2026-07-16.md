---
title: Sprint Change Proposal — Preserve Projection Rollback Paths Through Epic 8 Parity Closure
date: 2026-07-16
author: Administrator
workflow: bmad-correct-course
mode: batch
mode_note: "Batch assumed after no mode preference was supplied; no implementation edits are applied before approval."
scope_classification: moderate
status: approved
approval_required: false
approved_at: 2026-07-16T00:29:58+02:00
handoff_status: documented
trigger: >
  Keep projection rollback-only paths until Epic 8 proves EventStore SDK
  projection/query parity, GDPR processing-record reads, rebuild behavior, and
  rollback replacement.
supersedes:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11-eventstore-owner-sdk-parity-evidence-gate.md
related:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-projection-rollback-retention.md
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - references/Hexalith.EventStore/_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md
  - references/Hexalith.EventStore/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md
---

# Sprint Change Proposal — Preserve Projection Rollback Paths Through Epic 8 Parity Closure

## 1. Issue Summary

Epic 7 deliberately retained the Parties-local projection, query, rebuild, and
adapter implementation as a rollback-only path. The approved 2026-07-07
projection-retention proposal made deletion conditional on four proofs:

1. EventStore SDK projection/query parity.
2. GDPR Art.30 processing-record read parity, with related Art.20 and no-leak
   behavior preserved.
3. Full detail-and-index rebuild equivalence against canonical aggregate replay.
4. A proven rollback replacement for the EventStore SDK path.

Story 8.6 and its spec already encode those conditions. The current technical
state still does not satisfy them. The course correction is therefore to keep
Story 8.6 blocked, retain every governed rollback path, and refresh the owner
handoff/tracking to the active EventStore parity-closure work without weakening
the separate Parties deletion gate.

### Trigger and classification

- **Triggering story:** Epic 8 Story 8.6, Projection and Query SDK Migration.
- **Issue type:** technical limitation and incomplete owner evidence discovered
  during implementation readiness/proof work.
- **Problem statement:** EventStore platform work has advanced beyond the
  historical Story 1.8 `still blocked` packet, but no owner-approved successor
  packet with final decision `available` and an exact consumable runtime identity
  exists. Parties cannot resume Story 8.6 or delete its rollback-only paths.

### Current evidence — verified 2026-07-16

- Parties Story 8.6 is `blocked` in `sprint-status.yaml`.
- The Story 8.3 matrix row **EventStore projection/query SDK** remains
  `needs-additive-api`.
- The historical EventStore Story 1.8 owner packet still concludes
  **`still blocked`**.
- EventStore remediation has advanced through successor stories. The active
  closure identity is Story **1.20**, `ready-for-dev`; its predecessor Story
  **1.19** is in `review`; EventStore Epic 1 remains `in-progress`.
- No `1-20-owner-approved-parity-closure-proof-packet.md` exists. Therefore no
  named owner has approved a final `available` decision or exact source/package/
  container identity for Parties consumption.
- The Parties superproject gitlink and checked-out EventStore commit now both
  equal `82ed167c1c78d4ff50d3f8eab43850bb6abd0fe7`. Checkout consistency is
  necessary but not sufficient: no Story 1.20 approved runtime identity exists,
  so this current SHA is not approved migration evidence.
- All 18 grouped rollback-only artifacts listed below are present.
- All 10 governed `catch (NotImplementedException)` fallback branches remain.
- There is no Parties `src/` or `tests/` projection/query migration in progress.

### Existing governance remains authoritative

- Epic 8 architecture invariants I3, I8, I9, and I10 require rollback retention,
  GDPR read/no-leak compatibility, replay/idempotency parity, and rebuild proof.
- Story 8.6 AC1 blocks source migration while the matrix row is
  `needs-additive-api`; AC4 covers processing-record queries; AC6 requires
  detail/index rebuild equivalence before deletion; AC8 allows cleanup only
  after parity.
- `spec-8-6` explicitly says every “DELETE only after parity” file stays until
  the parity harness is green and rebuild is verified.
- The approved 2026-07-07 retention proposal remains the governing deletion
  guardrail. This proposal updates the later owner-evidence handoff to current
  EventStore Story 1.20 rather than duplicating or replacing that guardrail.

## 2. Impact Analysis

### Epic impact

- **Epic 8 remains viable but blocked at Story 8.6.** No epic is added, removed,
  redefined, or resequenced.
- Sequencing remains `8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`.
- Epic 8 remains Class C post-MVP maintenance with zero new PRD functional
  requirements.
- Story 8.10 must not close the projection/query retirement item unless the
  two gates below have both completed.

### Story impact

- **Story 8.6 stays `blocked`.** No production source migration resumes from
  this proposal.
- EventStore Story 1.20 owns the owner-approved closure packet and exact runtime
  identity. Story creation or `ready-for-dev` status is not owner proof.
- If Story 1.20 eventually returns `available`, Story 8.6 resumes by building
  and running the Parties parity harness. Resumption does not authorize deletion.
- Stories 8.7-8.9 retain their independent prerequisite and rollback gates.

### Artifact conflicts

- **PRD:** no conflict and no edit. Epics 1-5 remain the feature/MVP baseline.
- **Epics:** no semantic conflict and no edit. Story 8.6 already contains the
  required acceptance conditions.
- **Architecture:** no semantic conflict and no edit. I3/I8/I9/I10 already
  govern the decision.
- **UX:** not applicable. No screen, flow, copy, component, or accessibility
  behavior changes.
- **Story/spec:** no edit. Both correctly fail closed.
- **Story 8.3 prerequisite matrix:** factual owner-evidence and identity update
  required; row status remains `needs-additive-api`.
- **Sprint status:** revalidation/comment and owner-action update required;
  Story 8.6 remains `blocked`, rollback action remains `open`, owner action
  remains `in-progress`.
- **EventStore submodule:** no edit in this workflow. Story 1.20 and its
  prerequisites remain owner-repository work.

### Technical impact

- No Parties production code change.
- No EventStore submodule content or gitlink change.
- No state-store/read-model migration.
- No rollback-only file deletion, narrowing, registration removal, health-check
  removal, or fallback-branch removal.
- The eventual implementation effort remains medium-to-high in EventStore and
  high-risk in Parties unless both gates remain enforced.

## 3. Recommended Approach

**Selected: Direct Adjustment with two sequential evidence gates.**

1. **Owner resume gate — EventStore responsibility.** Story 8.6 remains blocked
   until EventStore Story 1.20 produces a named-owner-approved proof packet whose
   final decision is exactly `available`, all parity rows have production-path
   evidence, and it publishes exact approved source/package/container identities.
2. **Consumer deletion gate — Parties responsibility.** Parties verifies its
   chosen EventStore consumption identity, then runs the Story 8.6 parity harness,
   GDPR read/no-leak checks, full rebuild-versus-replay comparison, and rollback
   proof. Only then may the corresponding rollback-only files be deleted.

This is a direct tracking/handoff correction, not a replan.

- **Parties effort now:** Low — documentation/tracking only after approval.
- **EventStore owner effort:** Medium-to-high — finish, review, and approve the
  successor parity closure on an exact runtime identity.
- **Parties migration effort later:** High — dual-path parity, persisted rebuild
  evidence, GDPR verification, cleanup, and rollback proof.
- **Risk:** Low while both gates remain closed; high if `available` owner status
  is mistaken for Parties deletion authorization.
- **Timeline:** Story 8.6 remains paused; no MVP impact.

### Alternatives considered

- **Potential rollback:** not viable. The retained local implementation is the
  rollback mechanism, and no unsafe Story 8.6 production migration occurred.
- **MVP review:** not viable/not applicable. Epic 8 is post-MVP maintenance and
  product requirements remain achievable and already delivered.
- **New epic or resequencing:** unnecessary. Existing EventStore and Parties
  stories own the work in the correct order.

## 4. Detailed Change Proposals

### 4.1 Governing retention set — no deletion authorized

The following paths remain rollback-only and must not be deleted, narrowed, or
have their fallback registrations/control flow removed until the owner resume
gate and the applicable Parties deletion proofs are both complete:

- Projection actors/resolution:
  `PartyDetailProjectionActor.cs`, `PartyIndexProjectionActor.cs`,
  `PartyEventTypeResolver.cs`.
- Rebuild:
  `ProjectionRebuildService.cs`, `IProjectionRebuildService.cs`,
  `PartyProjectionRebuildCheckpoint.cs`, `PartyProjectionRebuildScope.cs`.
- Platform adapters/freshness:
  `IPartyProjectionPlatformAdapter.cs`,
  `LocalPartyProjectionPlatformAdapter.cs`,
  `PartyProjectionPlatformFreshness.cs`,
  `PartyProjectionPlatformAdapterMode.cs`,
  `EventStorePartyProjectionPlatformAdapter.cs`,
  `PartyProjectionUpdateOrchestrator.cs`.
- Remoting/fallback/health:
  `PartyDetailProjectionActorExtensions.cs`,
  `ProjectionActorsHealthCheck.cs`.
- Query actors:
  `PartyDetailProjectionQueryActor.cs`,
  `PartyIndexProjectionQueryActor.cs`,
  `IPartyProjectionQueryActor.cs`.
- The 10 current `catch (NotImplementedException)` fallback branches.

The exact authoritative paths remain those in the approved 2026-07-07
projection-retention proposal and Story 8.6 code map.

### 4.2 Story 8.3 prerequisite matrix — factual evidence refresh

Artifact:
`_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

Section: `EventStore projection/query SDK` row.

**OLD (current evidence summary):**

```text
Story 8.6 start-gate observation on 2026-07-09 recorded current EventStore pin
0f428d0...; no owner-approved parity proof was locally available.
```

**NEW (proposed evidence summary):**

```text
Revalidated 2026-07-16. The historical EventStore Story 1.8 packet remains
`still blocked`. Owner remediation has advanced to active closure Story 1.20
(`ready-for-dev`), superseding historical Story 1.15; predecessor Story 1.19 is
in `review`, EventStore Epic 1 remains `in-progress`, and no Story 1.20 closure
packet exists. No named owner-approved `available` decision or approved runtime
identity is available. The Parties gitlink and checked-out EventStore commit
both equal 82ed167c..., but that SHA has not been approved by a Story 1.20
closure packet. The absence of an approved identity keeps Story 8.6 blocked.
Status remains `needs-additive-api`.
```

Replace the validation-cell hard-coded old pin result with both current checks:

```text
git ls-tree HEAD references/Hexalith.EventStore -> 82ed167c...
git -C references/Hexalith.EventStore rev-parse HEAD -> 82ed167c...
Result: gitlink and checkout match, but no Story 1.20 packet has approved this
SHA as the migration runtime identity.
```

Keep the row status `needs-additive-api`, its rollback column, required proof
items, and review gate unchanged.

**Rationale:** owner work has progressed, but progress is not approval. The
matrix must cite the active closure identity and lack of an approved consumption
identity without converting `ready-for-dev` into `available`.

### 4.3 Sprint rollback-retention action — revalidation only

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

Section: open Epic 7 projection-retention action.

**OLD:**

```yaml
# Revalidated 2026-07-11: all rollback-only projection/query files and 10 fallback
# branches remain intact; Story 8.6 is blocked at the needs-additive-api gate,
# and no production projection/query migration has occurred.
```

**NEW (proposed):**

```yaml
# Revalidated 2026-07-16: all governed projection/query/rebuild/adapter files and
# 10 fallback branches remain intact. Story 8.6 is blocked; the 8.3 row remains
# needs-additive-api. EventStore active closure Story 1.20 is ready-for-dev,
# predecessor 1.19 is in review, and no owner-approved available packet exists.
# Parties gitlink and checkout both equal 82ed167c..., but no Story 1.20 packet
# has approved that SHA, so no source migration or rollback-only deletion is
# authorized.
```

Keep the action text, owner, and `status: open` unchanged.

### 4.4 Sprint EventStore-owner action — make the two gates explicit

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

Section: Epic 8 EventStore-owner prerequisite action.

**OLD:**

```yaml
action: "Route EventStore additive-API prerequisites to Hexalith.EventStore owners: projection/query parity ... Additive/approved before any Parties deletion."
owner: "Winston (Architect) + Hexalith.EventStore owners"
status: in-progress
```

**NEW (proposed):**

```yaml
# Active owner closure: EventStore Story 1.20. Its ready-for-dev status is not
# proof-result approval. Gate 1 opens only when a named owner approves a packet
# with final decision `available` and exact source/package/container identities.
# Gate 2 remains Parties-owned: after identity verification, Story 8.6 must prove
# SDK parity, GDPR reads/no-leak behavior, rebuild-vs-replay, and rollback before
# deleting any rollback-only path.
action: "EventStore owners must complete Story 1.20 with an owner-approved `available` parity-closure packet and exact approved runtime identities before Parties Story 8.6 resumes; Parties must then pass its consumer parity and rollback gates before deleting any rollback-only path."
owner: "Winston (Architect) + Hexalith.EventStore owners"
status: in-progress
```

Keep Story 8.6 `blocked`. Update `last_updated` only when these approved tracking
edits are applied.

### 4.5 Story, spec, architecture, epics, PRD, and UX — no text change

- Story 8.6 AC1/AC4/AC6/AC8 already encode the intended behavior.
- `spec-8-6` already contains the required readiness mapping, non-goals, parity
  checklist, and delete-only-after-proof rule.
- Epic 8 spine I3/I8/I9/I10 already supplies the architectural invariant.
- `epics.md` already requires parity, processing-record reads, rebuild evidence,
  and rollback replacement before deletion.
- PRD and UX are unaffected.

Adding duplicate acceptance text would create multiple sources of truth without
strengthening the gate.

## 5. Implementation Handoff

**Scope classification: Moderate.** Parties changes after approval are factual
tracking edits only; the blocking technical delivery remains coordinated owner
and consumer work.

| Recipient | Responsibility |
| --- | --- |
| Hexalith.EventStore owners | Finish/review Story 1.19 prerequisites, execute Story 1.20, publish the exact-SHA/identity closure packet, and record a named-owner decision of `available` or `still blocked`. Do not modify Parties or its rollback code. |
| Architect (Winston) | Keep the owner resume gate closed unless the packet is fully approved and exact identities are consumable; keep the Parties deletion gate separate. |
| Parties Developer (Amelia) | After owner approval, verify the selected EventStore identity and update the 8.3 matrix; build/run the Parties parity harness before touching any retention-set path. |
| Test Architect (Murat) | Verify production-handler idempotency, persisted detail/index parity, Art.20/Art.30/no-leak behavior, full rebuild-versus-aggregate-replay, cursor compatibility, and tested rollback. |
| Product Owner | Keep Story 8.6 `blocked`, the projection-retention action `open`, and the owner action `in-progress` until their independent exit criteria are met. |

### Success criteria

1. Story 8.6 remains blocked while EventStore Story 1.20 is incomplete,
   unapproved, `still blocked`, or identity-mismatched.
2. The Story 8.3 projection/query SDK row remains `needs-additive-api` until the
   named owner resume gate is satisfied and recorded.
3. Parties verifies its consumption identity before any source migration.
4. Story 8.6 resumption starts with the dual-path parity harness and authorizes
   no deletion by itself.
5. Rollback-only paths remain until projection/query parity, GDPR processing-
   record reads and no-leak behavior, detail/index rebuild equivalence, and
   rollback replacement are green and recorded.
6. No PRD/UX scope change and no new product functional requirement.

## 6. Change-Analysis Checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Story 8.6 is the blocked deletion-heavy migration. |
| 1.2 Core problem | [x] Done | Owner parity closure and exact runtime approval are incomplete. |
| 1.3 Evidence | [x] Done | 8.6 blocked; matrix `needs-additive-api`; no 1.20 packet or approved runtime identity; retention set intact. |
| 2.1 Current epic viability | [x] Done | Epic 8 remains viable after the two gates. |
| 2.2 Epic-level change | [N/A] Skip | No epic scope change. |
| 2.3 Remaining epics/stories | [x] Done | 8.10 must observe the gate; other stories retain independent prerequisites. |
| 2.4 New epic needed | [N/A] Skip | Existing EventStore Epic 1 and Parties Epic 8 own the work. |
| 2.5 Priority/order | [x] Done | EventStore 1.20 -> Parties identity verification/parity -> deletion. |
| 3.1 PRD | [N/A] Skip | MVP/product requirements unchanged. |
| 3.2 Architecture | [x] Done | Existing I3/I8/I9/I10 already govern. |
| 3.3 UX | [N/A] Skip | No user-facing change. |
| 3.4 Other artifacts | [x] Done | Matrix and sprint tracking received the approved factual updates. |
| 4.1 Direct adjustment | [x] Viable | Selected; tracking/handoff only now. |
| 4.2 Potential rollback | [x] Not viable | Retained local code is the rollback path. |
| 4.3 MVP review | [x] Not viable | Epic 8 is post-MVP maintenance. |
| 4.4 Recommended path | [x] Done | Two sequential evidence gates. |
| 5.1-5.5 Proposal components | [x] Done | Issue, impact, edits, scope, and handoff are explicit. |
| 6.1-6.2 Final review | [x] Done | Proposal is consistent with current repo and owner evidence. |
| 6.3 Explicit approval | [x] Done | Administrator approved the proposal on 2026-07-16. |
| 6.4 Sprint-status update | [x] Done | Approved rollback-retention and owner-gate updates applied. |
| 6.5 Handoff | [x] Done | Responsibilities remain routed to EventStore owners, Winston, Amelia, Murat, and the Product Owner. |

## Approval Record

Administrator approved this proposal on 2026-07-16 at 00:29:58 +02:00. The
authorized Story 8.3 matrix and sprint-tracking updates in §§4.2-4.4 were
applied, and the responsibilities in §5 remain the execution handoff. No
production source, test, dependency, package, submodule-content, or gitlink
change was authorized or applied by this workflow. Story 8.6 remains `blocked`,
the prerequisite row remains `needs-additive-api`, and every rollback-only path
stays until the two independent proof gates pass.
