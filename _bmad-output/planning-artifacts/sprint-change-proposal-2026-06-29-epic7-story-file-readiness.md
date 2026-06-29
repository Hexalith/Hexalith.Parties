---
project_name: parties
user_name: Administrator
date: 2026-06-29
workflow: bmad-correct-course
change_scope: moderate
status: approved-routed
mode: batch
approved_by: Administrator
approved_at: 2026-06-29T08:33:51+02:00
routed_to: story-creation-workflow
trigger_report: _bmad-output/planning-artifacts/implementation-readiness-report-2026-06-29.md
related_artifacts:
  - _bmad-output/planning-artifacts/parties-ui-prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md
  - _bmad-output/planning-artifacts/epic-7-planning-approval-2026-06-29.md
  - _bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
note: "Default output file sprint-change-proposal-2026-06-29.md already records an earlier approved and implemented same-day scope-hygiene proposal. This file is a follow-up for the latest readiness assessment disposition."
---

# Sprint Change Proposal - Epic 7 Story File Readiness

## 1. Issue Summary

The 2026-06-29 implementation-readiness assessment returned **NEEDS WORK** for
the full planning set, but the problem is scoped.

- Epics 1-5 are ready/complete and cover 100% of PRD functional requirements.
- Epic 6 is ready only as approved Class A maintenance and must not be counted as
  product feature delivery.
- Epic 7 has an approved PM/Architect implementation plan and backlog, but it is
  not developer-executable yet because no detailed `7-*` implementation story
  files exist.

The triggering issue is therefore not requirements discovery, architecture
rework, or UX rework. It is execution gating: Epic 7 moved from planning-only to
approved backlog, but the handoff stopped one step before developer-ready story
files.

Evidence:

- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-29.md`
  names Epic 7's missing story files as a major issue.
- `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md`
  approves story backlog 7.1 through 7.8, but states developer execution still
  starts with dedicated story files.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` lists Epic 7 and
  all 7.x items as `backlog`.
- `_bmad-output/implementation-artifacts/` contains no `7-*` story files.

## 2. Impact Analysis

### Epic Impact

- **Epics 1-5:** No change. They remain the only PRD feature readiness evidence.
- **Epic 6:** No change to scope or status. It remains executable only as Class A
  maintenance, with existing `6-*` story files marked `ready-for-dev`.
- **Epic 7:** Needs story-file creation before developer execution. It remains
  post-MVP platform maintenance and covers no PRD functional requirements.

### Story Impact

Create detailed implementation story files for the approved Epic 7 backlog:

1. `7-1-platform-target-destination-adr-and-release-rollback-plan.md`
2. `7-2-commons-service-defaults-correlation-problemdetails-and-paging.md`
3. `7-3-search-normalization-and-frontcomposer-ui-orchestration-convergence.md`
4. `7-4-projection-platform-compatibility-adapter.md`
5. `7-5-projection-checkpoint-rebuild-migration-and-local-code-removal.md`
6. `7-6-crypto-key-management-adr-and-compatibility-harness.md`
7. `7-7-crypto-key-management-migration-behind-eventstore-provider-contracts.md`
8. `7-8-release-rollback-cleanup-and-readiness-gate.md`

Each story file must be created from the approved plan and architecture spine,
with full sections matching the existing story-file pattern: `Status`, `Story`,
`Acceptance Criteria`, `Tasks / Subtasks`, `Dev Notes`, `Guardrails`, and
`References`.

### Artifact Conflicts

- **PRD:** No change. Epic 7 covers no new PRD functional requirements.
- **Architecture:** No change. The Epic 7 architecture spine already provides the
  adapter-first, compatibility, ownership, release, and rollback invariants.
- **UX:** No change. The final UX spines remain binding for any touched UI or
  FrontComposer orchestration path.
- **Epics:** No required scope change. `epics.md` already says developer execution
  starts only when detailed `7-*` story files are created.
- **Sprint status:** Must be updated after story files are created so each 7.x
  entry moves from `backlog` to `ready-for-dev`. `epic-7` can remain `backlog`
  until actual development starts, matching the existing Epic 6 maintenance
  pattern.

### Technical Impact

No source code, package reference, submodule pointer, deployment manifest, data
model, API contract, or UI behavior change is authorized by this proposal. The
only implementation impact is planning handoff: creating story files and updating
story statuses.

## 3. Recommended Approach

**Selected path: Direct Adjustment.**

| Option | Verdict | Rationale |
| --- | --- | --- |
| Direct Adjustment | Chosen | The approved Epic 7 plan already exists. Story creation and status alignment close the developer-execution gap without changing product scope. |
| Rollback | Not viable | No completed work caused the issue. Rolling back the Epic 7 plan would discard approved PM/Architect planning. |
| MVP Review | Not viable | MVP feature scope is already complete through Epics 1-5. Epic 7 is post-MVP maintenance. |

Effort: Medium planning effort, no code.

Risk: Medium if story files are too thin, because Epic 7 touches cross-submodule
platform boundaries. Low if each story carries explicit adapter-first, parity,
rollback, submodule, and no-PII guardrails.

Timeline impact: One story-creation pass plus sprint-status update before Epic 7
development can start.

## 4. Detailed Change Proposals

### 4.1 - Create Epic 7 story files

**Artifact:** `_bmad-output/implementation-artifacts/7-*.md`

OLD:

```text
No 7-* implementation story files exist.
```

NEW:

```text
Create eight story files under _bmad-output/implementation-artifacts/:

- 7-1-platform-target-destination-adr-and-release-rollback-plan.md
- 7-2-commons-service-defaults-correlation-problemdetails-and-paging.md
- 7-3-search-normalization-and-frontcomposer-ui-orchestration-convergence.md
- 7-4-projection-platform-compatibility-adapter.md
- 7-5-projection-checkpoint-rebuild-migration-and-local-code-removal.md
- 7-6-crypto-key-management-adr-and-compatibility-harness.md
- 7-7-crypto-key-management-migration-behind-eventstore-provider-contracts.md
- 7-8-release-rollback-cleanup-and-readiness-gate.md
```

Each file must include:

- `Status: ready-for-dev`.
- Story text copied and sharpened from the approved Epic 7 implementation plan.
- Testable Given/When/Then acceptance criteria.
- Task/subtask checklists tied to acceptance criteria.
- Dev Notes that cite the architecture spine, approved plan, relevant project
  contexts, and affected repo areas.
- Guardrails for no product-scope change, no PRD FR coverage claim, no recursive
  submodule operations, no unowned submodule edits, no public Parties actor-host
  API, no EventStore gateway boundary change, no PII in logs/telemetry/errors, and
  explicit rollback.

Rationale: The readiness blocker is the absence of developer-executable story
context. Creating the files through the story workflow closes that gap without
authorizing source-code changes.

### 4.2 - Required story-specific handoff content

**Artifact:** new `7-*` story files.

OLD:

```text
Epic 7 story detail exists only as plan-level backlog sections in:
_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md
```

NEW:

```text
Story 7.1 must produce the target-destination ADR and release/rollback plan,
including B1-B11 ownership, API surface, package/reference path, release order,
rollback path, and test evidence.

Story 7.2 must cover Commons ServiceDefaults, bounded correlation,
ProblemDetails, and paging adoption behind compatibility surfaces.

Story 7.3 must cover shared search normalization and FrontComposer UI
orchestration convergence while preserving local search fallback and existing
StatusKind/aria-live behavior.

Story 7.4 must introduce the EventStore projection platform compatibility adapter
without changing existing Parties read contracts, ProjectionFreshnessMetadata, or
StatusKind mapping.

Story 7.5 must migrate checkpoint/rebuild implementation only after 7.4 adapter
parity and must preserve duplicate/out-of-order replay, resume/cancel, and
last-known fallback.

Story 7.6 must produce the crypto/key-management ADR and compatibility harness,
including readable, unreadable, missing-key, provider-unavailable, erased,
restricted, and legacy unprotected cases.

Story 7.7 must migrate approved generic crypto/provider pieces behind EventStore
contracts only after 7.6 approval and must preserve GDPR erasure and rollback.

Story 7.8 must close release, rollback, cleanup, readiness notes, pinned package
or submodule evidence, and any explicitly deferred removals.
```

Rationale: The plan is coherent, but developer execution needs story-level
constraints and tests. These per-story requirements prevent the story files from
being shallow copies of the backlog.

### 4.3 - Update sprint status after story creation

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

OLD:

```yaml
  # -- Epic 7: Platform Alignment - adopt Commons/EventStore (Class B) ----
  # PM/Architect implementation plan approved 2026-06-29:
  # _bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md.
  # Post-MVP platform maintenance only; no new PRD functional requirements.
  # Story files do not exist yet, so all Epic 7 entries remain backlog.
  epic-7: backlog
  7-1-platform-target-destination-adr-and-release-rollback-plan: backlog
  7-2-commons-service-defaults-correlation-problemdetails-and-paging: backlog
  7-3-search-normalization-and-frontcomposer-ui-orchestration-convergence: backlog
  7-4-projection-platform-compatibility-adapter: backlog
  7-5-projection-checkpoint-rebuild-migration-and-local-code-removal: backlog
  7-6-crypto-key-management-adr-and-compatibility-harness: backlog
  7-7-crypto-key-management-migration-behind-eventstore-provider-contracts: backlog
  7-8-release-rollback-cleanup-and-readiness-gate: backlog
```

NEW:

```yaml
  # -- Epic 7: Platform Alignment - adopt Commons/EventStore (Class B) ----
  # PM/Architect implementation plan approved 2026-06-29:
  # _bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md.
  # Post-MVP platform maintenance only; no new PRD functional requirements.
  # Story files created from the approved plan; ready-for-dev means story context
  # exists, not permission to violate sequence or parallelize dependent stories.
  epic-7: backlog
  7-1-platform-target-destination-adr-and-release-rollback-plan: ready-for-dev
  7-2-commons-service-defaults-correlation-problemdetails-and-paging: ready-for-dev
  7-3-search-normalization-and-frontcomposer-ui-orchestration-convergence: ready-for-dev
  7-4-projection-platform-compatibility-adapter: ready-for-dev
  7-5-projection-checkpoint-rebuild-migration-and-local-code-removal: ready-for-dev
  7-6-crypto-key-management-adr-and-compatibility-harness: ready-for-dev
  7-7-crypto-key-management-migration-behind-eventstore-provider-contracts: ready-for-dev
  7-8-release-rollback-cleanup-and-readiness-gate: ready-for-dev
```

Rationale: Readiness tooling uses story-file existence/status to decide whether
the backlog is developer-executable. Keep the epic itself `backlog` until actual
implementation starts, matching the existing Epic 6 maintenance pattern.

### 4.4 - Optional readiness report disposition note after approval

**Artifact:** `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-29.md`

OLD:

```yaml
disposition:
  feature_scope: ready-complete
  maintenance_scope_epic_6: ready-for-dev-with-maintenance-label
  platform_scope_epic_7: backlog-not-developer-executable
```

NEW:

```yaml
disposition:
  feature_scope: ready-complete
  maintenance_scope_epic_6: ready-for-dev-with-maintenance-label
  platform_scope_epic_7: routed-to-story-creation
  via: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-29-epic7-story-file-readiness.md
  note: "Epic 7 has an approved PM/Architect plan and is routed to creation of detailed 7-* story files before developer execution. Epics 1-5 remain complete PRD feature scope; Epic 6 remains Class A maintenance only."
```

Rationale: This keeps the report historically accurate while making the route to
resolution visible. Because the readiness report is already modified in the
working tree, this proposal does not patch it until approval.

## 5. Implementation Handoff

**Scope classification:** Moderate.

This is backlog/story-file preparation, not source-code implementation. It
requires story workflow execution and sprint-status synchronization, but not a
fundamental PRD, UX, or architecture replan.

**Recipients and responsibilities:**

- **Product Owner / Developer agents:** create the eight Epic 7 implementation
  story files from the approved plan and architecture spine.
- **Developer agent:** after story files are created, update sprint-status 7.x
  entries to `ready-for-dev`, then run `git diff --check`.
- **Product Manager / Architect:** review Story 7.1 and any story that proposes a
  submodule API addition, package/version change, projection migration, or
  crypto/key-management move.

**Success criteria:**

- All eight `7-*` story files exist under `_bmad-output/implementation-artifacts/`.
- Each story has full acceptance criteria, tasks, dev notes, guardrails, and
  references.
- Sprint status marks each 7.x story `ready-for-dev`.
- Epic 7 still states post-MVP platform maintenance and no new PRD FR coverage.
- Dependencies are preserved: `7.5` after `7.4`, `7.7` after `7.6`, and `7.8`
  last.
- No code, package, submodule, PRD, UX, or architecture behavior changes are made
  as part of this proposal.

## Checklist Summary

- [x] 1.1 Trigger identified: 2026-06-29 implementation-readiness report.
- [x] 1.2 Core problem defined: Epic 7 lacks detailed developer-executable story
  files after PM/Architect plan approval.
- [x] 1.3 Evidence gathered: readiness report, PRD, epics, architecture,
  architecture spine, Epic 7 planning approval, Epic 7 implementation plan, UX
  spines, project contexts, and sprint status.
- [x] 2.1 Current impacted epic assessed: Epic 7 cannot be developed directly from
  the plan.
- [x] 2.2 Epic-level change identified: keep Epic 7 as post-MVP maintenance, but
  create story files and move 7.x story statuses to `ready-for-dev`.
- [x] 2.3 Remaining epics reviewed: Epics 1-5 no change; Epic 6 remains Class A
  maintenance only.
- [x] 2.4 No new epic required.
- [x] 2.5 No epic priority change required; Epic 7 sequencing must be preserved.
- [x] 3.1 PRD conflict: none.
- [x] 3.2 Architecture conflict: none; architecture spine provides required
  invariants.
- [x] 3.3 UI/UX conflict: none; UX spines remain guardrails for touched UI paths.
- [!] 3.4 Secondary artifacts: story files and sprint status require changes after
  approval.
- [x] 4.1 Direct Adjustment selected; effort medium, risk medium due platform
  boundaries.
- [N/A] 4.2 Rollback rejected; no completed work is invalid.
- [N/A] 4.3 MVP review rejected; MVP feature scope remains complete.
- [x] 4.4 Recommended path selected.
- [x] 5.1 Issue summary created.
- [x] 5.2 Epic and artifact impacts documented.
- [x] 5.3 Recommended path documented.
- [x] 5.4 MVP impact documented: none.
- [x] 5.5 Handoff plan established.
- [x] 6.1 Checklist reviewed.
- [x] 6.2 Proposal drafted.
- [x] 6.3 User approval received from Administrator on 2026-06-29.
- [!] 6.4 Sprint status update pending story-file creation.
- [x] 6.5 Handoff confirmed: route to story-creation workflow, then update
  sprint status.

## Approval and Routing

Approved by Administrator on 2026-06-29 at 08:33:51+02:00.

Route this proposal to the story-creation workflow for Epic 7 story files 7.1
through 7.8, then update sprint status as described above.
