---
title: Sprint Change Proposal — Recover the Story 8.7 Automation Baseline Without Bypassing G5
date: 2026-08-04
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: approved
approval_required: true
approval: approved
approved_by: Administrator
approved: 2026-08-04T23:00:08+02:00
trigger: >
  Story 8.7 build automation halted at its clean-working-tree precondition while
  the repository contained unfinished Story 8.6 review work, planning-tool
  changes, generated artifacts, and four root-submodule pointer changes. The
  recovery must preserve that work without falsely treating Story 8.7's missing
  G5 shared payload-protection provider as delivered.
baseline:
  parties_commit: a252c38
  parties_branch: main
  eventstore_recorded_gitlink: 7854f8e51ce9b852bb6c3cac6012670122e93792
  eventstore_selected_gitlink: 1d6e9321acfc416768c1c78e9facf573c9c41f71
  eventstore_selected_release: v3.91.0
  builds_recorded_gitlink: a53166539bf4441d5e33d04281b14c2d59e950c3
  builds_selected_gitlink: 824d7ef100455423aabbcd399c8364074000b2e0
related:
  - _bmad-output/implementation-artifacts/bmad-build-auto-result-8-7-data-protection-extraction.md
  - _bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/spec-8-7-data-protection-extraction.md
  - _bmad-output/implementation-artifacts/8-7-data-protection-extraction.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/epic-8-context.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-02-story-8-6-eventstore-identity-authorization-backfill.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01.md
---

# Sprint Change Proposal — Recover the Story 8.7 Automation Baseline Without Bypassing G5

## 1. Issue Summary

`bmad-build-auto 8.7` halted before implementation because the root working tree
was not clean. Its result artifact records only:

```text
Status: blocked
Blocking condition: dirty working tree
```

The dirty tree is not one coherent Story 8.7 change. It combines:

1. unfinished Story 8.6 adversarial-review patches across projection, Memories,
   GDPR/UI mapping, tests, the Epic 8 spine, and evidence artifacts;
2. exact dependency-pointer changes needed by that work for EventStore
   `v3.91.0` and the Builds catalog that selects EventStore package `3.91.0`;
3. a sprint-planning generator/test change plus Epic 8 planning edits;
4. unrelated FrontComposer and Memories gitlink drift;
5. regenerated Epic 8 context and the untracked automation result.

The tree therefore cannot be made safely clean by treating all changes as one
Story 8.7 unit or by discarding them. It first needs an approved provenance and
checkpoint plan.

The review also found two independent semantic gates:

- Story 8.6 was changed to `done` in both its story document and
  `sprint-status.yaml`, while the same story says it remains `in-progress` and
  still lists acceptance-relevant open findings, including operator visibility
  for corrupt/unresolved events, rebuild/live-write concurrency, and parity-test
  gaps.
- Story 8.7 still lacks the G5 shared payload-protection provider. EventStore
  `v3.91.0` adds the shared-projection rebuild completion contract needed by
  Story 8.6, not `pdenc-v2`, `IPersonalDataPolicy`,
  `IErasureStateProvider`, or the G5 closure packet required by Story 8.7.

### Evidence verified on 2026-08-04

- Root repository: `main` at `a252c38`, tracking `origin/main`, with 35 tracked
  modified paths, four changed gitlinks, and the untracked build-auto result.
- `references/Hexalith.EventStore` is clean at exact tag `v3.91.0`, SHA
  `1d6e9321acfc416768c1c78e9facf573c9c41f71`.
- `references/Hexalith.Builds` is clean at SHA
  `824d7ef100455423aabbcd399c8364074000b2e0`; its package catalog selects
  `HexalithEventStoreVersion=3.91.0`.
- The official NuGet v3 feed lists
  `Hexalith.EventStore.DomainService` `3.91.0`.
- After a current package-mode restore, the narrow canonical projection build
  passed with zero warnings and zero errors:

  ```text
  dotnet restore src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj \
    -p:UseHexalithProjectReferences=false \
    -p:HexalithEventStoreFromSource=false \
    -p:NuGetAudit=false \
    -p:MinVerVersionOverride=1.0.0

  dotnet build src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj \
    --configuration Release --no-restore -nr:false -m:1 \
    -p:UseHexalithProjectReferences=false \
    -p:HexalithEventStoreFromSource=false \
    -p:NuGetAudit=false \
    -p:MinVerVersionOverride=1.0.0
  ```

- EventStore Epic 8 Stories 8.2 through 8.11 remain backlog; the G5 runtime
  package and Story 8.11 closure packet are absent.
- `git diff --check` currently fails because the regenerated
  `epic-8-context.md` uses CRLF line endings that Git reports as trailing
  whitespace.

The change trigger is therefore a workflow/provenance failure plus stale
planning evidence. It is not authorization to bypass Story 8.6 completion or
the Story 8.7 G5 prerequisite.

## 2. Impact Analysis

### Epic impact

- Epic 8 remains viable and post-MVP.
- No epic is added, removed, redefined, or reprioritized.
- The authoritative sequence remains `8.6 -> 8.7`. Once Story 8.6 is genuinely
  complete, Story 8.7 still remains gated by G5 until EventStore Story 8.11
  records the exact approved provider and closure proof.
- No later epic becomes obsolete. Later Epic 8 cleanup must continue to respect
  the existing dependency-identity, parity, privacy, and rollback invariants.

### Story impact

- **Story 8.6:** restore an honest `in-progress` status until every
  acceptance-relevant open review item is fixed or explicitly accepted and
  routed as deferred work. The prior package-mode handoff sub-block is now
  resolved by the observed `3.91.0` restore/build, but that result alone does not
  close the remaining story findings.
- **Story 8.7:** keep `blocked`. A clean working tree removes only the automation
  precondition failure; it does not provide the G5 runtime engine or authorize
  deletion of the Parties-local protection path.
- **Story 8.3:** keep the matrix story `done`, but refresh its living
  projection/DataProtection identities and G5 validation receipt to the current
  exact EventStore pin.
- **Story 8.11:** retain the planning/tooling change as an independent work unit;
  it must not be bundled into Story 8.6 or used to imply Story 8.7 readiness.

### Artifact conflicts

- **PRD:** no conflict and no edit. This remains post-MVP maintenance with no new
  functional requirement.
- **Architecture:** no architectural replan. The pending permanent-Party-ID spine
  clarification belongs with Story 8.6 evidence and remains subject to its
  validation/checkpoint.
- **UX:** no change. Existing typed unavailable/redacted/erased behavior and
  honest privacy messaging remain compatibility requirements.
- **Implementation tracking:** Story 8.6 status/evidence, the Story 8.3 matrix,
  sprint status, deferred-work ledger, Epic 8 context, and the build-auto result
  require reconciliation.
- **Working-tree provenance:** Builds/EventStore are Story 8.6 dependencies;
  FrontComposer/Memories gitlink movements are unrelated and require a separate
  owner-approved disposition.

### Technical impact

- No Story 8.7 production source change is authorized by this proposal.
- No Parties crypto/key-management MOVE, KEEP, or adapter-seam file may be
  deleted while G5 remains `needs-additive-api`.
- The EventStore and Builds gitlinks must be recorded together because the
  source surface and package catalog are two forms of the same Story 8.6
  dependency identity.
- The current G5 matrix validation command hard-pins EventStore `v3.90.0` /
  `7854f8e5`; it must be refreshed to `v3.91.0` / `1d6e9321` while preserving
  the negative conclusion that G5 is absent.
- A clean baseline requires separate, reviewable checkpoints; it must not be
  manufactured by reverting or silently absorbing unrelated work.

## 3. Recommended Approach

### Decision: Direct Adjustment

Use a four-stage recovery within the existing Epic 8 plan:

1. **Reconcile Story 8.6 governance and evidence.** Ratify the exact
   EventStore/Builds identities already selected by the Administrator, record
   the now-green package-mode projection build, and restore Story 8.6 to
   `in-progress` while material findings remain.
2. **Refresh the Story 8.7 prerequisite receipt without promoting it.** Update
   the G5 matrix's exact current pin and reproducible commands, keep its status
   `needs-additive-api`, keep the retention action open, and keep Story 8.7
   `blocked`.
3. **Separate the dirty tree into owned work units.** Keep Story 8.6 code,
   tests, evidence, EventStore, and Builds together; keep the sprint-planning
   generator/epics change separate; require explicit owner disposition for the
   unrelated FrontComposer/Memories gitlinks; retain and normalize generated
   evidence rather than deleting it.
4. **Create a clean baseline only after validation and explicit checkpoint
   authorization.** Then rerun automation. A rerun may proceed past the clean
   tree check, but production Story 8.7 work must still halt at G5 unless the
   EventStore closure evidence has changed.

### Alternatives considered

- **Potential rollback:** not viable. It would discard validated Story 8.6
  review fixes and dependency work while leaving G5 unresolved. Effort high;
  risk high.
- **Treat every dirty path as one Story 8.7 change:** not viable. It destroys
  provenance, mixes unrelated gitlink drift into a privacy migration, and could
  falsely imply Story 8.7 readiness. Effort low; risk high.
- **PRD/MVP review:** not applicable. Epic 8 is approved post-MVP maintenance and
  the product goals are unchanged.

### Effort, risk, and schedule

- **Scope:** Moderate — backlog/evidence reconciliation plus coordinated
  Developer, Product Owner, Test Architect, and EventStore-owner handoff.
- **Effort:** Medium. The artifact edits are small, but Story 8.6's remaining
  material findings and clean-checkpoint validation require focused work.
- **Risk if followed:** Low to medium. Existing behavior and rollback are
  preserved; the principal risk is validation time.
- **Risk if bypassed:** High. Premature Story 8.7 work could make persisted data
  unreadable, remove the only rollback path, or bury unfinished Story 8.6 and
  unrelated dependency drift in one checkpoint.
- **Timeline:** no MVP impact. Story 8.7 production implementation remains on the
  existing EventStore G5 critical path.

## 4. Detailed Change Proposals

### 4.1 Story 8.6 status and completion evidence

Artifacts:

- `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:**

```text
Status: done
8-6-projection-and-query-sdk-migration: done
```

The same story currently also states:

```text
Story status remains `in-progress` ... earlier unresolved ... findings ...
were not part of the approved twelve-patch set.
```

**NEW:**

```text
Status: in-progress
8-6-projection-and-query-sdk-migration: in-progress
```

Replace the stale package-handoff blocker with a dated evidence entry recording
the successful current package-mode restore and zero-warning/zero-error build at
EventStore package `3.91.0`. Keep the material open review findings visible.
Advance to `review` or `done` only after those findings are fixed or an explicit
owner decision accepts and routes them without contradicting the acceptance
criteria.

**Rationale:** status must reflect the story's own completion gate. The newly
green package build closes one sub-block; it does not silently dispose of the
remaining correctness and verification items.

### 4.2 Story 8.6 dependency-identity authorization

Artifact:
`_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

**OLD:** the projection/query SDK and EventStore DataProtection rows describe
Story 8.6 adoption at EventStore `v3.89.0` / `c590590b`, then warn that a later
`4bcf2484` pin has not been revalidated. The current `v3.91.0` adoption has no
formal backfill proposal.

**NEW:** keep both rows `available` and record:

```text
Story 8.6 selected EventStore v3.91.0
(1d6e9321acfc416768c1c78e9facf573c9c41f71) under the
Administrator's recorded direct authorization. This Sprint Change Proposal
formalizes that identity. The matching Builds catalog identity is
824d7ef100455423aabbcd399c8364074000b2e0 and selects
HexalithEventStoreVersion=3.91.0. A current package-mode restore and Release
projection build completed with zero warnings and zero errors on 2026-08-04.
```

Refresh the validation commands to assert the exact root gitlinks, clean
submodule checkouts, EventStore tag, Builds package property, and current
package-mode projection build.

**Rationale:** this mirrors the approved 2026-08-02 identity-backfill precedent
and makes source-mode and package-mode provenance explicit without weakening any
consumer parity gate.

### 4.3 Story 8.7 G5 prerequisite receipt

Artifacts:

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:** the G5 row's validation receipt expects root/EventStore identity
`7854f8e5` and exact tag `v3.90.0`. The current tree is `1d6e9321` /
`v3.91.0`, so the reproducibility fitness test is red.

**NEW:** update only the current-identity receipt and validation commands:

```text
Revalidated for Story 8.7 at matching root gitlink and clean EventStore
checkout 1d6e9321acfc416768c1c78e9facf573c9c41f71 (v3.91.0).
The release adds Story 8.6 shared-projection rebuild completion mechanics. It
does not provide the G5 runtime engine, pdenc-v2, IPersonalDataPolicy,
IErasureStateProvider, the production key-backend package, or the Story 8.11
closure packet. G5 remains needs-additive-api; Story 8.7 remains blocked; the
retention action remains open; no Parties protection-path deletion is
authorized.
```

Keep all seven existing G5 exit-proof groups and the MOVE/KEEP/seam retention
inventory unchanged.

**Rationale:** make the evidence reproducible at the current dependency without
confusing a projection release with delivery of a security provider.

### 4.4 Sprint sequence and automation result

Artifacts:

- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/bmad-build-auto-result-8-7-data-protection-extraction.md`

**OLD:** Story 8.6 is `done`; Story 8.7 is `blocked`; the automation result names
only the dirty tree.

**NEW:** Story 8.6 is `in-progress`; Story 8.7 stays `blocked`. Expand the
automation result after the next run to distinguish:

```text
Operational precondition: clean working tree.
Sequence prerequisite: Story 8.6 complete.
Production prerequisite: G5 available at an exact approved identity with the
Story 8.11 closure proof.
```

The current two-line result remains valid historical evidence and must not be
rewritten to claim that implementation occurred.

**Rationale:** a clean tree is necessary for unattended automation, but it is
not sufficient authorization for the Story 8.7 migration.

### 4.5 Working-tree ownership and checkpoint boundaries

The implementation handoff must use these boundaries:

1. **Story 8.6 work unit:** Story 8.6 projection/Memories/GDPR source and tests,
   its story/deferred evidence, the permanent-ID spine clarification, and the
   exact EventStore plus Builds gitlinks.
2. **Planning-tool work unit:** `sprint_plan.py`, its tests, and the related Epic
   8 planning additions. Validate this independently; do not include it in the
   Story 8.6 evidence claim merely because it is already dirty.
3. **Unrelated dependency work unit:** FrontComposer and Memories root gitlinks.
   Preserve them. Include them only in a separately justified and validated
   dependency checkpoint after explicit owner confirmation; otherwise leave
   their disposition to the user rather than reverting them.
4. **Generated evidence work unit:** retain the build-auto result and regenerated
   Epic 8 context. Normalize the context to repository-compatible LF output and
   rerun `git diff --check` before any checkpoint.

No staging, commit, push, branch, reset, checkout, or dependency update is
authorized by this proposal itself. Those operations require the normal
repository workflow and the user's explicit authorization.

### 4.6 Explicit non-changes

- No PRD, product scope, epic sequence, UI/UX flow, or accessibility change.
- No Story 8.7 production implementation or status promotion.
- No G5 status promotion, retention-action closure, crypto/key deletion,
  provider switch, or key-material operation.
- No silent rollback or deletion of user-owned worktree changes.
- No FrontComposer/Memories adoption claim based only on their current checkout.

## 5. Implementation Handoff

### Classification and recipients

**Moderate** — the plan stays intact, but backlog/evidence coordination and
cross-owner dependency closure are required.

| Recipient | Responsibility |
| --- | --- |
| Product Owner | Approve the status/evidence correction and preserve the `8.6 -> 8.7` sequence. Decide when Story 8.6's material open findings are accepted for review. |
| Parties Developer | Apply the matrix/story/sprint edits, finish or explicitly route Story 8.6's acceptance-relevant findings, preserve the work-unit boundaries, normalize generated context, and run the repository gates. Do not create checkpoints without explicit authorization. |
| Test Architect | Reproduce the current package-mode build, focused Story 8.6 tests, G5 matrix fitness tests, `git diff --check`, warning-override guard, and the narrowest required broader lanes before status advancement. |
| EventStore owner and Security Reviewer | Deliver EventStore Stories 8.2–8.11, publish the G5 provider at an exact identity, and approve the seven-group proof packet. Only Story 8.11 may close G5. |
| Administrator | Decide the separate disposition of FrontComposer/Memories gitlink drift and explicitly authorize any later checkpoint/commit operation. |

### Success criteria

1. Story 8.6 and sprint status agree on `in-progress` until all material findings
   have an explicit disposition and required gates pass.
2. The Story 8.3 projection/DataProtection rows identify EventStore `v3.91.0`,
   Builds `824d7ef`, and the observed green package-mode projection build.
3. The G5 row is reproducible at `v3.91.0` but remains
   `needs-additive-api`; Story 8.7 remains `blocked`; the retention action stays
   open.
4. No Story 8.7 crypto/key-management file is deleted and the local rollback
   path remains usable.
5. Story 8.6, planning-tool, unrelated-gitlink, and generated-evidence changes
   have explicit ownership and are not bundled under a false Story 8.7 claim.
6. `git diff --check` and the relevant repository validation gates pass before
   any user-authorized checkpoint.
7. The root tree becomes clean without discarding user work. A subsequent
   build-auto run gets past the operational clean-tree check and still honors
   sequence/G5 prerequisites.

## Change-Analysis Checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Story 8.7 build automation exposed the dirty-tree/provenance problem; Story 8.6 evidence exposed the status contradiction. |
| 1.2 Core problem | [x] Done | Workflow/provenance failure plus stale status and dependency evidence; not a new product requirement. |
| 1.3 Supporting evidence | [x] Done | Git state, submodule identities, story/matrix text, package feed, package-mode restore/build, EventStore backlog, and diff-check failure recorded. |
| 2.1 Current epic viable | [x] Done | Yes, with Story 8.6 honestly completed before Story 8.7 and G5 preserved. |
| 2.2 Epic-level changes | [N/A] Skip | No epic scope or acceptance-criteria change. |
| 2.3 Remaining epics | [x] Done | No future epic invalidated; existing dependency gates remain authoritative. |
| 2.4 New epic required | [N/A] Skip | EventStore Epic 8 already owns G5 delivery. |
| 2.5 Priority/order | [x] Done | Preserve `8.6 -> 8.7`; no reprioritization. |
| 3.1 PRD | [N/A] Skip | No product/MVP conflict or edit. |
| 3.2 Architecture | [x] Done | No replan; preserve existing identity, parity, privacy, and rollback invariants. |
| 3.3 UX | [N/A] Skip | No user-facing flow or accessibility change. |
| 3.4 Other artifacts | [!] Action-needed | Story/matrix/sprint/deferred/generated evidence and worktree provenance require the edits in §4. |
| 4.1 Direct adjustment | [x] Viable | Selected; medium effort, low-to-medium risk. |
| 4.2 Potential rollback | [x] Not viable | High effort and risk; would discard validated work without delivering G5. |
| 4.3 MVP review | [N/A] Skip | Epic 8 is post-MVP maintenance. |
| 4.4 Recommended path | [x] Done | Reconcile, separate, validate, checkpoint only with authorization, then rerun automation without bypassing gates. |
| 5.1 Issue summary | [x] Done | Trigger, discovery, and evidence recorded. |
| 5.2 Impact | [x] Done | Epic, story, artifact, technical, and worktree impacts recorded. |
| 5.3 Recommendation | [x] Done | Direct Adjustment and rejected alternatives documented. |
| 5.4 MVP/action plan | [x] Done | No MVP impact; four-stage action plan defined. |
| 5.5 Handoff | [x] Done | PO, Developer, Test Architect, EventStore owner, Security Reviewer, and Administrator responsibilities defined. |
| 6.1 Checklist review | [x] Done | All applicable analysis items addressed; pending work is explicit. |
| 6.2 Proposal accuracy | [x] Done | Cross-checked against current repository and dependency evidence. |
| 6.3 Explicit approval | [x] Done | Administrator approved the complete proposal on 2026-08-04T23:00:08+02:00. |
| 6.4 Sprint-status update | [N/A] Skip | No epic was added, removed, or renumbered. The approved Story 8.6 status correction is routed to implementation rather than applied by this change-management workflow. |
| 6.5 Handoff confirmation | [x] Done | Moderate-scope implementation is routed to the Product Owner, Parties Developer, Test Architect, and EventStore/Security owners defined in §5. |

## Workflow Execution Log

- Mode: Batch.
- Recommended approach: Direct Adjustment.
- Scope: Moderate.
- Proposal state: Approved by Administrator on 2026-08-04T23:00:08+02:00;
  implementation not started by this workflow.
- Source/config changes made by this workflow: none.
- Proposal artifact created:
  `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-04.md`.
- Diagnostic validation: current package-mode projection restore/build passed;
  `git diff --check` remains red on CRLF-generated Epic 8 context.
- Handoff: Product Owner and Parties Developer apply the approved artifact and
  backlog corrections; the Test Architect owns validation; EventStore and
  Security owners retain the G5 delivery/closure dependency.
