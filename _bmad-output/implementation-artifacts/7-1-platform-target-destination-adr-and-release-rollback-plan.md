---
baseline_commit: 0de3cf1
---

# Story 7.1: Platform Target-Destination ADR and Release/Rollback Plan

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a product manager and architect,
I want every Class B item mapped to a target owner, package/reference path, release order, compatibility strategy, and rollback path,
so that later developer stories do not make unowned cross-submodule changes.

## Acceptance Criteria

1. Given the Epic 7 Class B B1-B11 inventory, when this story is complete, then an accepted ADR maps every item to an owning repo/project, intended public API surface, package or project-reference path, release order, rollback path, and required test evidence.
2. Given Parties may consume shared code either through root `references/` project references or released packages, when the ADR is written, then it decides whether Parties adds a `HexalithCommonsRoot` project-reference path or consumes released Commons packages, while preserving Central Package Management, `.slnx`, no `Version=` attributes in `.csproj` files, and root-only non-recursive submodule rules.
3. Given some shared APIs may be missing or insufficient, when the ADR inventory finds a gap, then the gap is routed to an additive owner-submodule story before Parties adoption and no Parties code is allowed to reference unreleased or unowned APIs.
4. Given Epic 7 is post-MVP platform maintenance, when the release/rollback plan is complete, then it preserves EventStore gateway routing, projection idempotency, stale/degraded fallback, GDPR erasure guarantees, crypto-shredding guarantees, PII-free logs/telemetry, and public Parties contract compatibility unless a separate breaking-change plan approves otherwise.
5. Given this story is a planning and decision story, when implementation completes, then no production code migration, no package upgrade, no submodule pointer change, no local infrastructure deletion, and no cross-submodule source edit has been made by this story.

## Tasks / Subtasks

- [x] Create the target-destination ADR (AC: 1-3)
  - [x] Add `_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md`.
  - [x] Use status `Accepted` only after every B1-B11 item is mapped; use `Proposed` until the map is complete.
  - [x] Include sections: Context, Decision, Target Destination Matrix, Dependency and Reference Strategy, Missing Shared API Stories, Compatibility Strategy, Rollback Strategy, Test Evidence Requirements, Consequences.
  - [x] For each B1-B11 row, record owner repo/project, destination API surface, package/reference path, Parties compatibility adapter or facade, release order, rollback path, and required evidence.

- [x] Decide the Commons consumption path (AC: 2)
  - [x] Inspect current `Directory.Build.props` root properties and existing submodule reference style.
  - [x] Decide either `HexalithCommonsRoot` project references under root `references/Hexalith.Commons` or released `Hexalith.Commons.*` package versions.
  - [x] If project references are chosen, specify the exact root property shape and the future story responsible for adding it.
  - [x] If packages are chosen, specify that versions belong only in `Directory.Packages.props`; `.csproj` files must remain versionless.
  - [x] Do not make the reference/package change in this story.

- [x] Create the release and rollback plan (AC: 1, 3, 4)
  - [x] Add `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md`.
  - [x] Sequence stories as `7.1 -> 7.2/7.3 -> 7.4 -> 7.5 -> 7.6 -> 7.7 -> 7.8`, preserving the approved dependency gates.
  - [x] For each adoption cluster, name the release unit, prerequisite owner-submodule story, Parties adoption story, validation lane, rollback switch or pointer, and data/contract compatibility condition.
  - [x] Explicitly require Story 7.8 to pin final submodule commits or package versions and to record final readiness evidence.

- [x] Validate planning consistency (AC: 1-5)
  - [x] Cross-check the ADR and release plan against `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md` and the Epic 7 architecture spine.
  - [x] Verify every B1-B11 item appears exactly once in the target matrix and has no owner of `TBD`.
  - [x] Verify every missing API is routed to an additive story in the owning submodule before any Parties adoption story consumes it.
  - [x] Verify the story did not edit production code, project files, package versions, submodule pointers, or submodule source.
  - [x] Run `git diff --check`.

## Dev Notes

### Story Classification

- This is a decision/planning story for Epic 7. It creates executable decision artifacts for later developer stories but does not perform platform migration itself.
- Epic 7 is post-MVP platform maintenance and covers no new PRD functional requirements. Do not report it as MVP feature delivery.
- This story exists because implementation-readiness found Epic 7 approved at plan level but not developer-executable until detailed `7-*` story files exist. [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-06-29.md#Final-Implementation-Readiness-Decision]

### Approved Epic 7 Context

- Approved approach: adapter-first strangler migration. Prove target ownership, package graph, compatibility, and rollback before code migration; introduce Parties-compatible adapters over shared primitives; run parity tests; remove local infrastructure only after evidence proves behavior and rollback. [Source: _bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Approved-Approach]
- Sequencing: `7.1 -> 7.2 -> 7.3 -> 7.4 -> 7.5 -> 7.6 -> 7.7 -> 7.8`. Stories 7.2 and 7.3 may run after 7.1 in parallel if they do not require the same submodule release; 7.5 requires 7.4; 7.7 requires 7.6; 7.8 runs last. [Source: _bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Sequencing]
- Architecture spine rule AD-5 is binding: if a needed shared API is missing, land it additively in the owning submodule first, validate that submodule's gates, update the root submodule pointer or package reference through Central Package Management, then adopt it in Parties. [Source: _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-5---Release-Sequencing-Before-References]

### Required Target Destination Matrix Inputs

Use the approved B1-B11 inventory as the ADR baseline. Do not invent new Class B items without documenting why they belong to Epic 7.

| ID | Scope | Approved target direction | Current evidence to inspect |
| --- | --- | --- | --- |
| B1 | Projection sequence checkpoint and replay dedupe | `Hexalith.EventStore.Server.Projections` through a Parties adapter | `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs`, `src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs`, `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs` |
| B2 | Resumable projection rebuild/replay-from-zero | `Hexalith.EventStore.Server.Projections` rebuild primitives after parity tests | `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs`, `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/IProjectionRebuildOrchestrator.cs`, `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/IProjectionRebuildCheckpointStore.cs` |
| B3 | AES-GCM payload protection | EventStore payload-protection contracts plus provider package, gated by Story 7.6 | `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs`, `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`, `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadata.cs` |
| B4 | Party key-management subsystem | Shared security only for generic key-provider mechanics; Parties retains legal policy | `src/Hexalith.Parties.Security/PartyKeyManagementService.cs`, `src/Hexalith.Parties.Contracts/Security/IPartyKeyManagementService.cs`, `src/Hexalith.Parties.Security/PartyErasureOrchestrator.cs` |
| B5 | ServiceDefaults | `Hexalith.Commons.ServiceDefaults` or thin Parties wrapper | `src/Hexalith.Parties.ServiceDefaults/Extensions.cs`, `references/Hexalith.Commons/src/libraries/Hexalith.Commons.ServiceDefaults/HexalithServiceDefaults.cs` |
| B6 | Correlation accessor/middleware | Commons metadata/diagnostics or additive Commons middleware | `src/Hexalith.Parties/Middleware/CorrelationIdMiddleware.cs`, `src/Hexalith.Parties.Security/CorrelationContextAccessor.cs`, `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Metadatas/` |
| B7 | ProblemDetails/global exception mapping | Commons/ServiceDefaults where available; Parties domain rejections remain local | Search Parties and Commons for `ProblemDetails`; do not move domain rejection semantics into Commons. |
| B8 | Jaro-Winkler and diacritic normalization | Commons for pure text helpers; Memories only for search-specific scoring | `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs`, `src/Hexalith.Parties/Search/LocalPartySearchService.cs`, `references/Hexalith.Commons/src/libraries/Hexalith.Commons/Strings/StringHelper.cs`, relevant Memories scoring helpers if adopted |
| B9 | Projection freshness vocabulary | EventStore freshness/query primitives plus Parties compatibility mapping | `src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessMetadata.cs`, `src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessStatus.cs`, UI `StatusKind` mapping from prior stories |
| B10 | `PagedResult<T>` | Commons generic paging behind adapter; public Parties shape stays compatible | `src/Hexalith.Parties.Contracts/Models/PagedResult.cs`, Parties client/UI callers, any Commons paging candidate found by inventory |
| B11 | Mixed primitives | Split by owner: EventStore, Commons, FrontComposer, or Parties policy | `src/Hexalith.Parties.Security/DecryptionCircuitBreaker.cs`, `src/Hexalith.Parties.Projections/Actors/PartyEventTypeResolver.cs`, `src/Hexalith.Parties.Projections/Abstractions/IIndexPartitionStrategy.cs`, FrontComposer lifecycle/orchestration primitives |

### Current Files Being Modified - Required Reading

- `_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md` (NEW)
  - Current state: absent.
  - What this story changes: create the ADR that closes ownership, dependency, reference, compatibility, missing-API, and rollback decisions for B1-B11.
  - Preserve: use planning-artifact placement; do not put this decision in `docs/` until it is intentionally promoted to published documentation.
- `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md` (NEW)
  - Current state: absent.
  - What this story changes: create the sequencing and rollback control plan for 7.2-7.8.
  - Preserve: the approved implementation plan remains the source of story scope; this file makes release/rollback execution concrete.
- `_bmad-output/implementation-artifacts/7-1-platform-target-destination-adr-and-release-rollback-plan.md` (UPDATE during implementation)
  - Current state: ready-for-dev story file.
  - What this story changes: update Dev Agent Record, task checkboxes, completion notes, file list, and validation summary only.
  - Preserve: acceptance criteria and guardrails unless an approved story correction explicitly changes them.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (UPDATE during dev-story workflow only)
  - Current state after create-story: Story 7.1 is `ready-for-dev`.
  - What this story changes during implementation: dev-story may move this story through `in-progress`, `review`, and later `done`.
  - Preserve: comments, status definitions, and all unrelated story statuses.

### Architecture Guardrails

- No production code migration in this story. The ADR may name future code files to touch, but this story must not change production source, project references, package versions, submodule pointers, or submodule contents.
- No public Parties host API. Commands and queries still enter through the Hexalith.EventStore gateway; EventStore invokes Parties over DAPR at `POST /process`. [Source: _bmad-output/project-context.md#Framework-Specific-Rules-Event-Sourcing--CQRS--DAPR-behind-EventStore]
- Public contracts evolve additively only. Removing or renaming contract fields, enum values, command/query shapes, or metadata is outside Epic 7 unless a separate breaking-change plan approves it. [Source: _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#Consistency-Conventions]
- Projection adoption must preserve replay from sequence zero, at-least-once tolerance, duplicate/out-of-order safety, per-actor checkpoints, stale/degraded fallback, and erased-party exclusion. [Source: _bmad-output/project-context.md#Read-side-projections--CQRS]
- Crypto/key-management decisions must preserve irreversible erasure, unreadable-payload classification, party-specific legal policy, no-PII logs, export/processing-record redaction, and erasure certificate behavior. Story 7.6 owns proof before migration. [Source: _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-3---Crypto-Placement-Gate]
- Do not re-open Epic 6 Class A anchors already routed to `Hexalith.Parties.Contracts` or `Hexalith.Parties.Authentication`. Epic 7 is for shared-platform consumption, not redoing in-repo consolidation. [Source: _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#Inherited-Invariants]

### Dependency and Submodule Guardrails

- Root-declared submodules only. Never use `git submodule update --init --recursive`; initialize only root `references/` paths when explicitly needed. [Source: AGENTS.md#Git-Submodules]
- `references/Hexalith.EventStore` and `references/Hexalith.Tenants` are project references, not NuGet packages. `Hexalith.Memories` is optional. `Hexalith.Commons` is present under `references/Hexalith.Commons` but Parties has no `HexalithCommonsRoot` property yet. [Source: _bmad-output/project-context.md#Technology-Stack--Versions] [Source: Directory.Build.props]
- Central Package Management is enabled. If the ADR chooses packages, versions go in `Directory.Packages.props`; project files use versionless `PackageReference` only. [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- `.slnx` is the solution format. Do not create or rely on classic `.sln` files. [Source: _bmad-output/project-context.md#Technology-Stack--Versions]

### Previous Story Intelligence

- Story 6.6 and Story 6.7 established the pattern for maintenance consolidation: define shared anchors once, keep host-specific composition outside the shared package, avoid new semantics, update focused tests, and correct overstated validation blockers through actual focused re-runs. [Source: _bmad-output/implementation-artifacts/6-6-shared-role-arrays-and-policy-names-a9.md] [Source: _bmad-output/implementation-artifacts/6-7-shared-portal-display-formatters-a10.md]
- Recent commits show Epic 6 finished Class A in-repo consolidation through shared display formatters and role/policy anchors. Story 7.1 must not refactor those completed anchors or reclassify Epic 6 work as Epic 7. [Source: git log -5]

### Testing and Validation Guidance

- Required for this story:
  - `git diff --check`
  - `rg -n "B1|B2|B3|B4|B5|B6|B7|B8|B9|B10|B11" _bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md`
  - `rg -n "Version=|PackageVersion|HexalithCommonsRoot|submodule|rollback|7\\.8" _bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md _bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md`
  - `git diff --name-only -- src references Directory.Build.props Directory.Packages.props Hexalith.Parties.slnx` should produce no entries for this story unless the story is explicitly corrected and re-approved.
- Build/test lanes are not required if the story remains documentation-only. If any source, project, package, or submodule pointer changes occur, stop and explain the scope violation before proceeding.
- No external latest-version research is required for this story because it must use local approved planning, current submodule sources, and pinned local versions. Do not upgrade libraries as part of this story.

### Out of Scope

- Do not implement Stories 7.2 through 7.8.
- Do not add `HexalithCommonsRoot`, project references, package references, or package versions.
- Do not update submodule pointers or edit files under `references/`.
- Do not delete Parties-local projection, ServiceDefaults, security, search, paging, correlation, or ProblemDetails code.
- Do not migrate crypto/key-management behavior; Story 7.6 owns the ADR and compatibility harness.
- Do not change public Parties contracts or UI behavior.
- Do not change EventStore gateway routing, DAPR ACLs, tenant/party authorization, or privacy copy.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-7.1-Platform-target-destination-ADR-and-release-rollback-plan]
- [Source: _bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Story-7.1---Platform-Target-Destination-ADR-And-Release-Rollback-Plan]
- [Source: _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#Architecture-Spine---Epic-7-Platform-Alignment]
- [Source: _bmad-output/planning-artifacts/epic-7-planning-approval-2026-06-29.md#Fulfillment]
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-06-29.md#Final-Implementation-Readiness-Decision]
- [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- [Source: Directory.Build.props]
- [Source: Directory.Packages.props]
- [Source: references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/]
- [Source: references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/]
- [Source: references/Hexalith.Commons/src/libraries/Hexalith.Commons.ServiceDefaults/]
- [Source: references/Hexalith.Commons/src/libraries/Hexalith.Commons.Metadatas/]

## Validation Summary

- Source discovery loaded project context facts, sprint status, canonical epics, Epic 7 implementation plan, Epic 7 architecture spine and reviews, Epic 7 planning approval, current root build/package reference files, previous Epic 6 story intelligence, current Parties and submodule inventory paths, and recent git history.
- Checklist fixes applied before finalizing: scoped this story to planning artifacts only, named exact ADR and release-plan outputs, added B1-B11 owner/API/reference/rollback/test-evidence requirements, required Commons reference strategy decision without implementing it, routed missing APIs to owner-submodule stories, and added no-code/no-package/no-submodule-change validation.
- Latest-technology review found no external dependency requirement. The story must rely on local approved planning, current submodule sources, and pinned project versions.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-29T13:23:25+02:00 - Marked sprint status for Story 7.1 as `in-progress`; baseline commit already existed and was preserved.
- 2026-06-29T13:23:25+02:00 - Red-phase document existence checks confirmed the ADR and release-plan artifacts were absent before implementation.
- 2026-06-29T13:23:25+02:00 - Inspected `Directory.Build.props`, `Directory.Packages.props`, root submodule declarations, Epic 7 implementation plan, Epic 7 architecture spine, and target submodule surfaces.
- 2026-06-29T13:23:25+02:00 - Focused validations passed: ADR matrix has 11 rows with B1-B11 exactly once each, required ADR sections exist, `git diff --check` passes, and no `src`, `references`, `Directory.Build.props`, `Directory.Packages.props`, or `Hexalith.Parties.slnx` diff entries exist.
- 2026-06-29T13:31:04+02:00 - Attempted `pwsh scripts/test.ps1 -Lane unit`; the wrapper printed repeated `Build failed with exit code: 1` messages while returning process exit code 0.
- 2026-06-29T13:31:04+02:00 - Direct `dotnet build Hexalith.Parties.slnx -c Release --no-restore` and `dotnet test --project tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --no-restore` failed before compilation with 0 warnings and 0 errors.
- 2026-06-29T13:31:04+02:00 - Diagnostic isolation found an out-of-scope submodule/tooling failure: `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Hexalith.FrontComposer.Contracts.csproj` returns exit code 1 for aggregate `GetTargetFrameworks` with 0 errors, while explicit `net10.0` and `netstandard2.0` builds succeed. Story 7.1 did not edit production code or submodules.

### Completion Notes List

- Created the accepted Epic 7 platform target-destination ADR with owner repo/project, destination API surface, package/reference path, Parties adapter/facade, release order, rollback path, and required evidence for B1-B11.
- Chose future Commons consumption through a `HexalithCommonsRoot` project-reference path under root `references/Hexalith.Commons`; Story 7.2 owns adding the property and first references.
- Created the Epic 7 release/rollback plan with the approved `7.1 -> 7.2/7.3 -> 7.4 -> 7.5 -> 7.6 -> 7.7 -> 7.8` sequence, dependency gates, adoption clusters, rollback sets, and Story 7.8 readiness evidence.
- Verified Story 7.1 stayed documentation-only: no production code migration, package upgrade, project-reference change, solution change, submodule pointer change, or submodule source edit.
- Scoped documentation validations passed. The broader unit/build lane was attempted and is blocked by an existing out-of-scope FrontComposer target-framework evaluation issue, not by Story 7.1 changes.
- Added a test-only Playwright validation spec, `tests/e2e/specs/story-7-1-platform-planning-artifacts.spec.ts`, that asserts the ADR/release-plan structure, the B1-B11 mapping, the Commons project-reference decision, and the documentation-only scope guard. This is test code (no production source, project reference, package version, solution, or submodule change), green at 5/5 with `PLAYWRIGHT_SKIP_WEBSERVER=1`, and TypeScript `tsc --noEmit` clean.

### File List

- `_bmad-output/implementation-artifacts/7-1-platform-target-destination-adr-and-release-rollback-plan.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md`
- `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md`
- `tests/e2e/specs/story-7-1-platform-planning-artifacts.spec.ts`

### Change Log

- 2026-06-29 - Added the Epic 7 accepted target-destination ADR and release/rollback plan; selected Commons project-reference consumption for future stories; recorded documentation-only validation evidence.
- 2026-06-29 - Senior Developer Review (AI): reconciled documentation with git reality. Added the previously undocumented validation spec to the File List and Completion Notes, and updated its `ALLOWED_STORY_FILE_LIST` to the true 5-file scope so the self-consistency assertion no longer under-reports. Re-ran the spec (5/5) and `tsc --noEmit` (clean). Status set to done.

## Senior Developer Review (AI)

**Reviewer:** Administrator on 2026-06-29

**Outcome:** Approve (after auto-fix).

**Scope reviewed:** This is a planning/decision story; the deliverables are the Epic 7 ADR and release/rollback plan plus the story's own bookkeeping. The only application-source change in the tree is the new Playwright validation spec.

**Acceptance Criteria:** AC1-AC5 all validated as implemented. The ADR maps B1-B11 with owner, destination API surface, package/reference path, adapter/facade, release order, rollback path, and required evidence (11 rows, 9 attributes, no `TBD`). The Commons consumption decision (project references via a future `HexalithCommonsRoot`, owned by Story 7.2) preserves CPM, `.slnx`, versionless `.csproj`, and root-only non-recursive submodule rules. Missing APIs are routed to owner-submodule stories before adoption. The release plan preserves EventStore gateway routing, projection idempotency, stale/degraded fallback, GDPR erasure, crypto-shredding, PII-free logs/telemetry, and public-contract compatibility. No production code, package, project-reference, solution, submodule-pointer, or submodule-source change was made (`git diff --name-only -- src references Directory.Build.props Directory.Packages.props Hexalith.Parties.slnx` is empty).

**Tasks:** Every `[x]` task verified genuinely done against the produced artifacts. No false completion claims.

**Verification performed:** Confirmed all "Existing" reference paths cited in the ADR exist on disk; ran the validation spec (5/5 passing, `PLAYWRIGHT_SKIP_WEBSERVER=1`); ran `tsc --noEmit` (clean); checked all new untracked artifacts for trailing-whitespace/hard-tabs (none).

**Findings and fixes:**

- [MEDIUM][fixed] The new source file `tests/e2e/specs/story-7-1-platform-planning-artifacts.spec.ts` was missing from the File List, Change Log, and Completion Notes — the story under-reported what it changed in the repo. Added it to all three.
- [MEDIUM][fixed] The spec's `ALLOWED_STORY_FILE_LIST` (and its `toEqual` self-assertion) enumerated only the four `_bmad-output` files while the spec itself was a fifth repo change, so the test colluded with the File List to under-report scope. Updated the allowlist to the true 5-file set; the assertion now reflects reality and stays green.
- [LOW][fixed] Change Log / Completion Notes were silent on the validation spec; both now document it.

**Note:** The story-required `git diff --check` is blind to untracked files (all four new artifacts are untracked); an intent-to-add re-check still reported clean, so this is a process caveat rather than a defect.
