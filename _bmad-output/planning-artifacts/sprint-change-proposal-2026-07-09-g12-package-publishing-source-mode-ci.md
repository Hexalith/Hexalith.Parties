---
title: Sprint Change Proposal - G12 Package Publishing / Source-Mode CI Decision
date: 2026-07-09
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: routed
approval_basis: "Administrator priority directive on 2026-07-09"
related:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-platform-prerequisite-routing.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md
  - _bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md
  - _bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md
---

# Sprint Change Proposal - G12 Package Publishing / Source-Mode CI Decision

## 1. Issue Summary

Story 8.3 produced the platform API prerequisite matrix and left one row with
status `blocked`: **G12 Package publishing / source-mode CI**. That row requires a
release-owner decision from **Hexalith.Commons** and **Hexalith.Tenants**:

- Publish `Hexalith.Commons.Http`.
- Publish `Hexalith.Commons.ServiceDefaults`.
- Publish `Hexalith.Tenants.Client`.
- Publish `Hexalith.Tenants.Testing`.
- Or explicitly bless source-mode CI as the approved strategy for this repository
  until package publication is available.

This is not a Parties implementation problem. It is a cross-repo release-mode
decision that hard-blocks Story 8.8, Story 8.10, and the package-mode build
blocker recorded by Story 8.1. The G12 handoff is therefore sequenced first among
Epic 8 owner-routing actions.

Evidence:

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`
  marks G12 as the only `blocked` row.
- `_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md`
  records package-mode/default build failure on missing `Hexalith.Tenants.Client`,
  `Hexalith.Tenants.Testing`, and `Hexalith.Commons.ServiceDefaults`.
- `_bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md`
  has an explicit Block If for G12.
- `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`
  cannot close Epic 8 without owner-assigned release blockers or deferrals.

## 2. Impact Analysis

Epic impact:

- Epic 8 only. Epics 1-7 remain unchanged.
- No PRD functional requirement coverage changes; Epic 8 remains post-MVP
  maintenance.
- Story order remains `8.1 -> 8.10`; this change sequences G12 first among owner
  handoffs, not as a new story.

Story impact:

- Story 8.1: package-mode/default build remains blocked until G12 resolves.
- Story 8.8: must not start while G12 is `blocked`.
- Story 8.10: must record the G12 resolution or a clear owner deferral before
  closing Epic 8.
- Stories 8.6, 8.7, and 8.9 are not directly unblocked by G12, but they still
  inherit the matrix review gate.

Artifact impact:

- `sprint-status.yaml`: the existing G12 action item is moved to `in-progress`
  with this proposal as the routing artifact.
- Story 8.3 matrix: unchanged until Commons/Tenants owners provide the release or
  source-mode decision proof.
- PRD, epics, architecture, and UX: unchanged.

Technical impact:

- No source migration.
- No submodule edits.
- No package-mode claim is made in Parties until owner proof exists.

## 3. Recommended Approach

Selected path: **Direct Adjustment - route and track G12 first**.

Send this decision brief to the Commons and Tenants release owners and keep the
G12 action `in-progress` until one of the two accepted decisions is recorded:

1. **Package publication path:** release owners publish all four package IDs to the
   configured NuGet feed and provide package versions.
2. **Source-mode CI path:** release owners explicitly bless source-mode CI as the
   temporary or permanent strategy for Parties, including the properties or shared
   workflow settings that make it intentional rather than an accidental local
   fallback.

Rollback is not applicable because no code changes are made. MVP review is not
applicable because this does not alter Epics 1-5.

Effort: low in this repository; owner-side effort depends on release pipeline
state.

Risk: medium until the decision lands, because Story 8.8 and Story 8.10 cannot
legitimately progress in package mode and broad validation remains blocked.

## 4. Detailed Change Proposals

### 4.1 Release Owner Decision Brief

Send the following ask as the G12 routing package:

**Decision requested:** Commons and Tenants release owners must choose exactly one
of these paths for the Parties Epic 8 package-mode gate:

- Publish the required packages:
  - `Hexalith.Commons.Http`
  - `Hexalith.Commons.ServiceDefaults`
  - `Hexalith.Tenants.Client`
  - `Hexalith.Tenants.Testing`
- Or bless source-mode CI for Parties and document the approved restore/build
  mode.

**Why this is first:** G12 is the only Story 8.3 row still marked `blocked`, and
it blocks Story 8.8, Story 8.10, and the Story 8.1 package-mode/default build.

**Proof required back to Parties:**

- Package path: released package versions and successful package-mode restore or
  build evidence for Parties.
- Source-mode path: owner-approved CI/build-mode note, exact MSBuild properties or
  workflow settings, and the scope/duration of the exception.

**Known owner-side evidence surfaces in this checkout:**

- Commons release config exists at `references/Hexalith.Commons/.releaserc.json`.
- Commons pack/validation scripts already enumerate `Hexalith.Commons.Http` and
  `Hexalith.Commons.ServiceDefaults`:
  `references/Hexalith.Commons/scripts/pack-release-packages.py` and
  `references/Hexalith.Commons/scripts/validate-nuget-packages.py`.
- Tenants release config exists at `references/Hexalith.Tenants/.releaserc.json`.
  The previously referenced `references/Hexalith.Tenants/release.config.cjs` is
  stale in this checkout and should not be used as the proof path.
- Tenants pack/validation scripts already enumerate `Hexalith.Tenants.Client` and
  `Hexalith.Tenants.Testing`:
  `references/Hexalith.Tenants/scripts/pack-release-packages.py`,
  `references/Hexalith.Tenants/scripts/validate-nuget-packages.py`, and
  `references/Hexalith.Tenants/scripts/validate-consumer-package-references.py`.

### 4.2 Tracking Change

Update `_bmad-output/implementation-artifacts/sprint-status.yaml`:

OLD:

```yaml
  - epic: 8
    action: "PRIORITY (G12, only `blocked` 8.3 row): route to Commons + Tenants release owners a decision to publish Hexalith.Commons.Http, Hexalith.Commons.ServiceDefaults, Hexalith.Tenants.Client, Hexalith.Tenants.Testing, OR bless source-mode CI. Hard-blocks 8.8/8.10 and the Story 8.1 package-mode build. Sequence first."
    owner: "Winston (Architect) + Hexalith.Commons & Hexalith.Tenants release owners"
    status: open
```

NEW:

```yaml
  - epic: 8
    action: "PRIORITY (G12, only `blocked` 8.3 row): route to Commons + Tenants release owners a decision to publish Hexalith.Commons.Http, Hexalith.Commons.ServiceDefaults, Hexalith.Tenants.Client, Hexalith.Tenants.Testing, OR bless source-mode CI. Hard-blocks 8.8/8.10 and the Story 8.1 package-mode build. Sequence first."
    owner: "Winston (Architect) + Hexalith.Commons & Hexalith.Tenants release owners"
    # Routed 2026-07-09 via:
    # _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09-g12-package-publishing-source-mode-ci.md.
    # Still waiting on owner decision: publish the package set or bless source-mode CI.
    status: in-progress
```

Rationale: the handoff has been routed, but the blocker is not closed until owners
decide and proof is recorded.

## 5. Implementation Handoff

Scope classification: **Moderate**. It is a cross-repo release decision and
backlog tracking adjustment, not a Parties implementation change.

Recipients:

- Winston / Architect: route this brief to Commons and Tenants release owners,
  keep G12 sequenced first, and record the owner decision.
- Commons release owners: confirm package publication for `Hexalith.Commons.Http`
  and `Hexalith.Commons.ServiceDefaults`, or approve source-mode CI as the
  release strategy dependency for Parties.
- Tenants release owners: confirm package publication for `Hexalith.Tenants.Client`
  and `Hexalith.Tenants.Testing`, or approve source-mode CI as the release
  strategy dependency for Parties.
- Developer / QA: do not start Story 8.8 package-mode migration or Story 8.10
  closure until the G12 decision is recorded in the 8.3 matrix or explicitly
  deferred with owner proof.

Success criteria:

- The G12 action item is first among Epic 8 owner-routing work and is
  `in-progress`.
- Commons and Tenants owners return a package publication decision or a source-mode
  CI blessing.
- The Story 8.3 matrix G12 row remains `blocked` until that proof exists, then is
  updated by the consuming story with exact versions or CI-mode evidence.
- Story 8.8 and Story 8.10 remain blocked on G12 until proof is recorded.

## 6. Checklist Summary

| Item | Status |
|---|---|
| Trigger and evidence identified | Done |
| Epic impact assessed | Done |
| PRD / architecture / UX conflicts checked | Done |
| Path forward selected | Done - direct route-and-track |
| Specific artifact edit proposed | Done |
| Handoff recipients named | Done |
| User approval | Done - priority directive on 2026-07-09 |
| Sprint status updated | Done |
