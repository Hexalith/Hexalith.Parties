---
title: Sprint Change Proposal — Validate Available 8.3 Rows at Consumption
date: 2026-07-16
author: Administrator
workflow: bmad-correct-course
mode: batch
mode_note: "Batch assumed after no alternate mode preference was supplied."
status: implemented
approval_required: false
approval: approved
approved_by: Administrator
approved_at: 2026-07-16T00:45:16+02:00
implemented_at: 2026-07-16T00:57:56+02:00
handoff_status: complete
change_scope: minor
trigger: >
  Require each of the four named Story 8.3 rows whose status is `available`
  to record an immutable release or root-submodule-pin availability identity
  before Stories 8.6, 8.8, or 8.10 consume the surface.
related:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/spec-8-5-eventstore-domain-service-sdk-host-cutover.md
  - _bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md
  - _bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md
---

# Sprint Change Proposal — Validate Available 8.3 Rows at Consumption

## 1. Issue Summary

Story 8.3 correctly distinguishes shared surfaces that are already present from
surfaces that still need an additive owner API. Four rows are relevant to this
correction:

1. `EventStore domain-service host`
2. `EventStore DataProtection`
3. `Commons HTTP helpers`
4. `Builds shared props/targets`

All four are marked `available`. That status proves a reviewable source surface
exists and means no additive API is required. It does not by itself identify an
immutable dependency that a consuming story can reproduce. The current fitness
test enforces only that each available row says a release or submodule pin must
be validated later; it does not require the resulting release/version or root
gitlink SHA to be written into the row before consumption.

### Trigger and classification

- **Triggering stories:** 8.6, 8.8, and 8.10; Story 8.5 supplies completed host
  consumption evidence.
- **Issue type:** planning and technical readiness gap discovered before
  deletion-heavy consumption.
- **Problem statement:** an `available` row can currently pass the Story 8.3
  fitness gate while naming only source paths and a future proof obligation.
  That allows a later story to consume a mutable checkout without first
  recording the exact released package or root-declared submodule gitlink that
  contains the surface.

### Evidence verified on 2026-07-16

| Surface | Current evidence | Consumption conclusion |
| --- | --- | --- |
| EventStore domain-service host | Story 8.5 recorded and tested EventStore pin `9f8b54dc161a4d5a9b2e6b1deacf331d1b80f1e0`; that commit contains `EventStoreDomainServiceExtensions.cs`. | Already proven at 8.5 consumption. Preserve the historical pin as the row's validated identity; 8.10 reconciles the final selected EventStore identity. |
| EventStore DataProtection | The Parties root gitlink is `82ed167c1c78d4ff50d3f8eab43850bb6abd0fe7`; the three cited DataProtection/cursor files were verified in that exact git object. The owner checkout advanced concurrently after proposal approval, which is not consumption evidence. | Record the root gitlink as current availability evidence before 8.6. If the selected EventStore identity changes before consumption, 8.6 must update and revalidate the row. |
| Commons HTTP helpers | Root gitlink `b03469b13408530bb757d3d02279c2d772ee4848` is release `v2.28.1`; the cited HTTP helper files exist in that release. | Record release `2.28.1` and its matching root gitlink before 8.8 consumes the package/surface. |
| Builds shared props/targets | Root gitlink `ed75ae3c45425b9610d5e75e6c5ec3e8d5283fe1` is release `v4.18.5`; the cited props/targets/docs exist in that release. | Record release `4.18.5` and its matching root gitlink before 8.8 consumes the shared build path. |

The current EventStore checkout is `v3.67.0-1-g82ed167c`, so the DataProtection
evidence should use the root gitlink SHA rather than imply that unreleased commit
`82ed167c…` is the `v3.67.0` release.

## 2. Impact Analysis

### Epic and story impact

- Epic 8 remains viable, Class C post-MVP maintenance, and in the existing
  sequence.
- No epic or story is added, removed, renumbered, or reprioritized.
- Story 8.5 remains done. Its recorded host pin is historical consumption proof,
  not a requirement to rerun or roll back the host cutover.
- Story 8.6 gains a consumption-identity gate for the available EventStore
  DataProtection/cursor surface. Its separate projection/query
  `needs-additive-api` blocker remains unchanged.
- Story 8.8 gains consumption-identity gates for Commons HTTP and Builds
  props/targets. Its independent additive-API gates remain unchanged.
- Story 8.10 must reconcile the four row identities against what was actually
  consumed or explicitly deferred before closing Epic 8.

### Artifact impact

- **PRD:** no edit. MVP scope and functional coverage are unchanged.
- **Epics:** clarify the Story 8.3 definition of `available` and add the
  consumption gate to Stories 8.6, 8.8, and 8.10.
- **Architecture spine:** strengthen I4 and the remaining-work readiness gate so
  source availability and consumable identity are distinct facts.
- **Story 8.3 matrix:** record the four immutable identities/evidence states in
  their existing rows; no row changes to `needs-additive-api`.
- **Specs:** add exact fail-closed gates and tasks to 8.6, 8.8, and 8.10. Keep
  the completed 8.5 spec unchanged as historical implementation evidence.
- **Fitness tests:** replace wording-only validation with an enforceable
  per-row consumption-identity contract and spec cross-checks.
- **Test summary:** after implementation, record the focused test command and
  evidence inspection results.
- **Sprint status:** no story status changes, so no status edit is required.
- **UX:** no screen, flow, component, accessibility, or copy impact.

### Technical impact

No production source, public contract, submodule content, gitlink, package
reference, build behavior, deployment asset, or runtime behavior changes in this
course correction. The change makes later consumption reproducible and prevents
local rollback-path deletion against an unidentified dependency.

## 3. Recommended Approach

**Selected: Direct Adjustment.** Keep each row `available`, record its immutable
availability identity, and make consumers fail closed if their selected identity
does not match the row at the moment of use.

- **Effort:** Low
- **Risk:** Low
- **Timeline impact:** No sequencing change; a consuming slice pauses only if its
  row is missing or mismatches the selected identity.
- **MVP impact:** None
- **Rollback:** Revert the documentation/spec/test changes; no runtime rollback is
  involved.

### Alternatives considered

- **Additive API work:** not applicable. The four surfaces already exist.
- **Potential rollback:** not viable or useful. Story 8.5 is already proven, and
  8.6/8.8 have not consumed the remaining surfaces under this correction.
- **MVP review:** not applicable. Epic 8 is post-MVP maintenance with zero PRD
  functional requirements.
- **Mark rows `needs-additive-api`:** rejected. It would misclassify an identity
  and release-governance requirement as a missing API.

## 4. Detailed Change Proposals

### 4.1 Story 8.3 matrix — define and record consumption identity

**Artifact:**
`_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

**Section:** `Status vocabulary`

**OLD:**

> `available` means the checked-out owner module already contains a reviewable
> public source surface, but later migration stories still need release or
> submodule-pin validation before consuming it.

**NEW:**

> `available` means the owner surface exists and no additive API is required.
> It becomes consumable only when its matrix row records an immutable
> availability identity selected by the consumer: either a released package
> version containing the cited surface or the exact SHA of the root-declared
> submodule gitlink. The consuming story must verify that its actual dependency
> mode and identity match that row before source migration or local deletion.

Add a short matrix note:

> A working-tree checkout or `git -C ... rev-parse HEAD` alone is not a
> submodule-pin proof. Submodule consumption evidence must include
> `git ls-tree HEAD <root-declared-submodule>` and a matching checkout SHA.
> Completed consumption may retain its historical validated pin; Story 8.10
> reconciles the final identities actually used.

Update only the four named rows' proof cells:

1. **EventStore domain-service host** — label the existing Story 8.5 evidence as
   `Consumption availability validated` at EventStore pin
   `9f8b54dc161a4d5a9b2e6b1deacf331d1b80f1e0`. Preserve all behavioral proof and
   rollback text. Add the root-gitlink inspection form to the validation cell;
   do not rewrite the historical Story 8.5 result as today's pin.
2. **EventStore DataProtection** — record current root gitlink
   `82ed167c1c78d4ff50d3f8eab43850bb6abd0fe7` and exact-git-object surface
   existence as availability evidence. State that Story 8.6 must refresh the row
   before consumption if its selected EventStore identity differs.
3. **Commons HTTP helpers** — record released package/source identity
   `Hexalith.Commons.Http` `2.28.1` / tag `v2.28.1` and matching root gitlink
   `b03469b13408530bb757d3d02279c2d772ee4848`.
4. **Builds shared props/targets** — record release/tag `4.18.5` / `v4.18.5` and
   matching root gitlink
   `ed75ae3c45425b9610d5e75e6c5ec3e8d5283fe1`.

**Rationale:** this preserves the existing status vocabulary while converting a
future-tense obligation into reproducible evidence.

### 4.2 Epic 8 architecture spine — distinguish source presence from consumption

**Artifact:**
`_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`

**Section:** invariant I4

**OLD:**

> I4. No Parties source migration starts from an unapproved/checked-out
> submodule API. Every prerequisite is additive or proven-already-available
> (the 8.3 matrix).

**NEW:**

> I4. No Parties source migration starts from an unapproved or unidentified
> dependency. Every prerequisite is either an owner-approved additive API or an
> already-available surface whose Story 8.3 row records the exact released
> package version or root-declared submodule gitlink SHA selected by the
> consumer. A checked-out source file or `available` status alone is not
> consumption evidence.

In §4 item 1, add that every `available` prerequisite must name its release or
root gitlink and match the consuming story's dependency mode before the dev
session changes source.

### 4.3 Canonical epics — make the gate visible at story level

**Artifact:** `_bmad-output/planning-artifacts/epics.md`

**Story 8.3 — add after the prerequisite-matrix criterion:**

> **And** every row marked `available` records a released package version or
> exact root-declared submodule gitlink SHA before a dependent story consumes
> it; source-path evidence alone is not consumable availability.

**Story 8.6 — add after the SDK-abstraction criterion:**

> **And** before `AddEventStoreDataProtection`, `DaprXmlRepository`, or the query
> cursor codec path is consumed, the EventStore DataProtection row records and
> matches the selected EventStore release or root gitlink.

**Story 8.8 — add after the replacement-shared-API criterion:**

> **And** before Parties-local HTTP or build-root helpers are replaced, the
> Commons HTTP and Builds shared props/targets rows record and match the exact
> package releases or root gitlinks being consumed.

**Story 8.10 — extend the final-readiness identity criterion:**

> Final readiness reconciles the four available-row consumption identities —
> EventStore host, EventStore DataProtection, Commons HTTP, and Builds — against
> the releases/root gitlinks actually consumed or the explicit deferral record.

### 4.4 Story 8.6 spec — gate DataProtection consumption without inventing an API gap

**Artifact:**
`_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md`

Extend `Block If`:

> Also HALT before consuming the `available` EventStore DataProtection/cursor
> row if the matrix does not record a release or root gitlink matching the
> EventStore identity selected by Story 8.6. This is an availability-identity
> gate, not an additive-API request.

Add the first execution task before source changes:

> Verify `git ls-tree HEAD references/Hexalith.EventStore` and the checkout (or
> the released package version), confirm the DataProtection/cursor symbols exist
> at that exact identity, and update the existing 8.3 row if the selected
> identity differs from its recorded value.

Add an acceptance criterion that a missing/mismatched identity leaves 8.6
blocked before registration or cursor migration.

### 4.5 Story 8.8 spec — gate Commons HTTP and Builds consumption

**Artifact:**
`_bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md`

Extend `Block If`:

> For the `available` rows, HALT the relevant slice unless Commons HTTP and
> Builds shared props/targets each record a release or root gitlink matching the
> dependency identity selected by 8.8. These rows need identity validation, not
> additive APIs.

Add an execution task before the HTTP/build slices:

> Reconcile the matrix with the selected `Hexalith.Commons.Http` release/root
> gitlink and `Hexalith.Builds` release/root gitlink; prove the cited files and
> symbols exist at those identities before deleting Parties-local helpers.

Add acceptance criteria that each slice fails closed on missing or mismatched
identity. While editing the spec, reconcile its stale statement that G12 is
`blocked`: the matrix records G12 package publication as `available` with
2026-07-11 package-only restore/Release-build evidence. This factual cleanup does
not change the independent G6/G8/G11/G7-G9 gates.

### 4.6 Story 8.10 spec — close only against actual identities

**Artifact:**
`_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`

Extend `Block If`:

> HALT final closure if any of the four named `available` rows lacks a recorded
> release/root-gitlink identity for an actually consumed surface, if that
> identity differs from the dependency used by Parties, or if an unconsumed
> surface lacks an explicit owner/proof/rollback deferral.

Add a task and acceptance criterion to reconcile the 8.5 historical host proof,
8.6 DataProtection identity, and 8.8 Commons/Builds identities against the final
dependency graph.

### 4.7 Fitness tests — enforce evidence, not future-tense wording

**Artifact:**
`tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs`

Replace or strengthen
`Matrix_AvailableRowsStillRequireReleaseOrSubmoduleProof` so it:

- treats the four named surfaces as the required consumption-identity set;
- requires each row to contain a labeled consumption-availability statement;
- requires a SemVer release/package version or a full 40-character root gitlink
  SHA in that row;
- rejects checkout-only proof by requiring root `git ls-tree` evidence for the
  submodule-pin path;
- checks that 8.6 names the DataProtection identity gate, 8.8 names the Commons
  HTTP and Builds gates, and 8.10 reconciles all four;
- preserves the existing checks for proof wording, rollback paths, owner paths,
  matrix markers, and no Story 8.3 production migration.

After implementation, record the focused test result in
`_bmad-output/implementation-artifacts/tests/test-summary.md`.

## 5. Implementation Handoff

**Scope classification:** Minor

**Recipient:** Parties Developer / documentation maintainer

### Responsibilities

1. Apply the approved matrix, spine, epics, and spec edits without modifying
   submodule contents or gitlinks.
2. Preserve Story 8.5's historical pin proof and record the three current
   availability identities exactly as verified.
3. Strengthen the fitness test so a future consumer cannot pass on wording-only
   evidence.
4. Run the focused `PlatformApiPrerequisitesTests` assembly and `git diff
   --check`; record results in the test summary.
5. Leave all current sprint statuses and independent additive-API blockers
   unchanged.

### Success criteria

- [x] All four named `available` rows record an immutable release or root
  submodule-gitlink identity.
- [x] Story 8.5's already-proven host identity remains historically accurate.
- [x] 8.6 fails closed before DataProtection/cursor consumption on an absent or
  mismatched identity.
- [x] 8.8 fails closed before Commons HTTP or Builds consumption on an absent or
  mismatched identity.
- [x] 8.10 reconciles all four identities or records explicit deferrals.
- [x] No row is misclassified as `needs-additive-api` merely for lacking an
  immutable consumption identity.
- [x] No production source, submodule content, gitlink, PRD, UX, or sprint status
  is changed by this correction.
- [x] Focused fitness tests and whitespace validation pass.

### Implementation result

- The four matrix rows retain `available` and now record the exact historical or
  current release/root-gitlink identities listed in §1.
- Stories 8.6, 8.8, and 8.10 now halt on missing or mismatched consumption
  identities; no additive API was introduced or requested.
- The Release test project built with 0 warnings and 0 errors after pinning the
  validation invocation to currently published dependency versions.
- The two consumption-identity fitness gates passed: 2 tests, 0 failures.
- Targeted `git diff --check` passed. No sprint-status update was required.

## Change Navigation Checklist Record

### 1. Understand the Trigger and Context

- [x] 1.1 Triggering stories identified: 8.6, 8.8, and 8.10; completed 8.5
  supplies host evidence.
- [x] 1.2 Core problem defined as missing immutable consumption identity for
  source-available platform surfaces.
- [x] 1.3 Evidence collected from the matrix, specs, root gitlinks, release tags,
  cited files at those identities, sprint status, and current fitness tests.

### 2. Epic Impact Assessment

- [x] 2.1 Epic 8 remains completable under its approved scope.
- [x] 2.2 Existing stories receive readiness criteria only; no new epic/story.
- [x] 2.3 Remaining Epic 8 dependencies reviewed; only 8.6/8.8/8.10 consume the
  affected outstanding surfaces.
- [N/A] 2.4 No epic is invalidated or made obsolete.
- [N/A] 2.5 No sequencing or priority change.

### 3. Artifact Conflict and Impact Analysis

- [x] 3.1 PRD/MVP remains unchanged.
- [x] 3.2 Architecture I4/readiness gate requires clarification.
- [N/A] 3.3 No UI/UX impact.
- [x] 3.4 Matrix, specs, fitness tests, and test summary require coordinated edits.

### 4. Path Forward Evaluation

- [x] 4.1 Direct Adjustment is viable: low effort, low risk, no sequencing change.
- [N/A] 4.2 Rollback of completed work is unnecessary.
- [N/A] 4.3 PRD MVP review is unnecessary.
- [x] 4.4 Direct Adjustment selected because the APIs exist and only immutable
  consumption evidence is missing.

### 5. Sprint Change Proposal Components

- [x] 5.1 Issue summary complete.
- [x] 5.2 Epic/story/artifact impacts documented.
- [x] 5.3 Recommended path and alternatives documented.
- [x] 5.4 MVP impact and action plan documented.
- [x] 5.5 Minor-scope Developer handoff defined.

### 6. Final Review and Handoff

- [x] 6.1 Applicable analysis items addressed.
- [x] 6.2 Proposal checked for consistency with the matrix, specs, spine, PRD,
  epics, sprint status, and repository evidence.
- [x] 6.3 Explicit Administrator approval received on 2026-07-16.
- [N/A] 6.4 No sprint-status entry changes are proposed.
- [x] 6.5 Final implementation handoff complete.

## Workflow Execution Log

- Change trigger: validate four Story 8.3 `available` rows with immutable
  release/root-gitlink identities before consumption.
- Mode: Batch (assumed; no alternate preference supplied).
- Change scope: Minor.
- Proposal artifact: this document.
- Approval: Administrator approved the proposal on 2026-07-16.
- Implementation state: complete; focused validation and handoff recorded.
- Output-path note: the workflow's date-only default filename already contains a
  separate approved proposal, so this collision-safe descriptive filename was
  used to preserve it.
