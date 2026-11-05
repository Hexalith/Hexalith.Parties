---
title: 'Synchronize, upgrade and release Parties'
type: 'bugfix'
created: '2026-11-05'
status: 'in-review'
baseline_commit: 'bb2783120c0eed620e3d4f52a2ebaf6aa510126f'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="user authorized implementation, pushes, release and bypassing human validation">

## Intent

**Problem:** Parties main fails CI before compilation because its committed Platform identity is unreleased and its Builds selection disagrees with Platform. Direct submodules have newer upstream commits and selected packages/tools need alignment before another complete publication.

**Approach:** Synchronize every root-declared submodule, use the latest stable SDK and published package versions, repair observed blocking CI failures, push validated changes, and publish a complete Parties release. Verify the eleven declared NuGet packages and the release workflow's container evidence.

## Boundaries & Constraints

**Always:** Preserve existing work and history; change files from their owning repositories; validate exact commit messages with pinned commitlint and retain evidence. Initialize only root-declared submodules. Use package references and Release assets for CI. Keep automated build, test, publication, provenance and compatibility gates. The user explicitly authorizes external publication and proceeding through human validation without further confirmation.

**Never:** Force-push, recursively initialize references, manufacture qualification evidence, publish occupied NuGet versions, expose credentials or weaken automated checks. Keep a documented compatibility pin when a newer package would break its supported consumer, and disclose that exception.

## I/O & Edge-Case Matrix

| Scenario | State | Expected behavior | Failure handling |
| --- | --- | --- | --- |
| Synchronization | Clean local main behind upstream | Fast-forward safely and record pointers | Preserve divergent/user work |
| SDK | Microsoft latest stable already selected | Retain verified 10.0.403 | Do not substitute .NET 11 prerelease |
| Package selection | New stable package available | Align central catalog/tools and verify consumers | Fix incompatibility or report exact pin |
| CI preparation | Unreleased/misaligned Platform tuple | Consume real qualified released identity and matching Builds/tool | Never bypass tuple proof |
| Release approval | Authenticated reviewer can approve | Submit authorized pending-deployment approval | Report unavailable permission |
| Publication | Green exact main source | Semantic release and registry verification | Fix the actual failing stage |

</frozen-after-approval>

## Code Map

- `.gitmodules`, `references/*`: ten direct dependency repositories; six are behind origin/main, all initially clean with no unpushed commits.
- `global.json`: 10.0.403, independently confirmed latest stable using Microsoft's release metadata.
- `references/Hexalith.Builds/Props/Directory.Packages.props`: central dependency versions. Latest upstream fc2122240a39f03628c04fa8433e5eece90b8b4c is released as 4.44.3.
- `.config/dotnet-tools.json`: module CLI 4.44.2 needs 4.44.3. Latest Platform ae7516ee6961dd31a5e319be59ead76449ab04a0 requires that tool and Builds revision; its tuple preflight is already running.
- `.github/workflows/ci.yml`, `Directory.Build.targets`, `tests/Hexalith.Parties.Ci.Tests/AspireRuntimePreparationTests.cs`: shared CI execution pin and exact package capability qualification fences. Preserve legacy-cohort tests.
- `.github/workflows/release.yml`, `release.config.cjs`, `tools/release-packages.json`: explicit source checks, protected production jobs, semantic release and eleven package inventory.
- `scripts/verify-release-publication.py`, `scripts/gitlink-rc-gate.sh`: exact registry/container evidence and submodule range gate.

## Tasks & Acceptance

**Execution:**
- [ ] `references/*` -- fast-forward clean upstream branches; inspect and repair relevant failing upstream CI from owning roots.
- [ ] Central catalog and owning tool manifests -- adopt verified stable updates and retain documented compatibility exceptions.
- [ ] Platform publication -- await successful exact tuple CI, then establish its real versioned release when needed for Parties preparation.
- [ ] Parties tool/CI/capability files -- align the selected tuple, repair observed failures and update meaningful regression coverage.
- [ ] Owning changes -- run focused validation, independent review, commitlint and safe pushes; commit parent pointers after their sources exist remotely.
- [ ] Release workflow -- obtain green exact-source CI and range proof, dispatch, satisfy authorized reviewer gates and verify all registry outputs.

**Acceptance Criteria:**
- Given synchronized clean repositories, when remote tips are compared, then all authorized changes and selected direct pointers are pushed.
- Given latest stable SDK/package selections, when required CI runs, then automated gates pass with warnings policy preserved.
- Given successful main CI and range checks, when the release is dispatched, then a GitHub release binds that exact source and all eleven NuGet packages are available at its version.
- Given a protected publication job, when reviewer approval is required, then existing authenticated authority is used without requesting additional human validation.

## Implementation Notes

Implementation handoff: complete local synchronization, necessary dependency/CI fixes and focused checks, then return for root-agent review and publication. Root agent owns final commit/push/release phases. This is an active bmad-build invocation; do not start another copy of that workflow. Send concise progress to the parent at meaningful checkpoints. Read-only audit details are available in `/tmp/parties-release-20261105/package-audit.json` and `/tmp/parties-release-20261105/build-job.log`. Root npm packages are current. Latest Builds upstream already contains all routine stable updates; retain its documented Microsoft.Build 18.9.6 (.NET 10 compatibility) and Microsoft.OpenApi 2.13.0 (ASP.NET Core 10 compatibility) pins. Root CLI must move to 4.44.3. Inspect `.gitlink-signoff.tsv` and its gate before changing gitlinks; use truthful evidence. Read only ordinary source/docs under references, never skills there.

User authorization overrides the skill's interactive approval checkpoints. No intent gaps remain. Publication and pushes are explicit requested side effects. Dependency alignment and CI repair are prerequisites of the single requested release outcome.

**2026-11-05 first local implementation pass:** Six clean direct checkouts were fast-forwarded to their fetched `origin/main` tips; all ten remain selected without nested initialization. Parties now selects module CLI 4.44.3, shared normal/manual lifecycle CI at Builds `fc2122240a39f03628c04fa8433e5eece90b8b4c`, and genuine Platform v1.4.0 at `ae7516ee6961dd31a5e319be59ead76449ab04a0`. Current tuple documentation/assertions and six source-selection-only ledger rows were aligned. The independent release publisher pin was retained.

Verification: `dotnet tool restore`, workflow `actionlint`, `bash scripts/gitlink-rc-gate.sh --worktree`, the declaration renderer check, all seven declaration Python tests, and module validation across Parties/EventStore/Tenants/Memories passed. The CI test project Release build passed with zero warnings/errors; the actual built assembly passed all 141 selected `PartiesContainerPublishWorkflowTests` and `AspireRuntimePreparationTests` cases with zero failures/skips. `git -c core.whitespace=cr-at-eol diff --check` passed; plain `git diff --check` flags existing EditorConfig-required CRLF. Exact commands, ownership boundaries and selected sources are retained in `/tmp/parties-release-20261105/implementation-first-pass.json`; focused XML and logs are in the same directory.

Concurrent external edits to Contracts/catalog tests, capability selectors and legacy workflow cohorts were preserved. Their replacement of the 4.0.4 workflow cohort with 4.1.0 needs parent review against the explicit legacy-cohort constraint. Matching staged gitlinks and package preparation, independent review, broader hosted CI, upstream repairs, commitlint/commits/pushes and publication remain parent-owned work; this local pass establishes no publication or runtime acceptance.

## Spec Change Log

## Review Triage Log

## Verification

- Official Microsoft SDK metadata, NuGet flat-container indexes and npm registry audit.
- Owning focused builds/tests and repository guard scripts; complete hosted CI on final source.
- Pinned commitlint CLI for each exact commit candidate, with retained output.
- GitHub release workflow and complete manifest-based NuGet package download/index verification.
