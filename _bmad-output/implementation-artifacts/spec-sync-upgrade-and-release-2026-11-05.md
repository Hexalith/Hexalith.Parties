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
- [ ] `references/*` -- direct fast-forwards complete; inspect and repair relevant failing upstream CI from owning roots.
- [x] Central catalog and owning tool manifests -- adopt verified stable updates and retain documented compatibility exceptions.
- [x] Platform publication -- await successful exact tuple CI, then establish its real versioned release when needed for Parties preparation.
- [ ] Parties tool/CI/capability files -- align the selected tuple, repair observed failures and update meaningful regression coverage.
- [ ] Owning changes -- run focused validation, independent review, commitlint and safe pushes; commit parent pointers after their sources exist remotely.
- [ ] Release workflow -- obtain green exact-source CI and range proof, dispatch, satisfy authorized reviewer gates and verify all registry outputs.

**Acceptance Criteria:**
- Given synchronized clean repositories, when remote tips are compared, then all authorized changes and selected direct pointers are pushed.
- Given latest stable SDK/package selections, when required CI runs, then automated gates pass with warnings policy preserved.
- Given successful main CI and range checks, when the release is dispatched, then a GitHub release binds that exact source and all eleven NuGet packages are available at its version.
- Given a protected publication job, when reviewer approval is required, then existing authenticated authority is used without requesting additional human validation.

## Implementation Notes

- Integrated upstream owner correction b34fa6a6e39b942858c3756d2b09d68b00e0d292 while preserving its explicit per-route selector declarations, declaration-source assertion and exact HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS fence. Retained our canonical roundtrip, actual gateway boundary cases and unconditional transport/selector checks under that same fence.

Implementation handoff: complete local synchronization, necessary dependency/CI fixes and focused checks, then return for root-agent review and publication. Root agent owns final commit/push/release phases. This is an active bmad-build invocation; do not start another copy of that workflow. Send concise progress to the parent at meaningful checkpoints. Read-only audit details are available in `/tmp/parties-release-20261105/package-audit.json` and `/tmp/parties-release-20261105/build-job.log`. Root npm packages are current. Latest Builds upstream already contains all routine stable updates; retain its documented Microsoft.Build 18.9.6 (.NET 10 compatibility) and Microsoft.OpenApi 2.13.0 (ASP.NET Core 10 compatibility) pins. Root CLI must move to 4.44.3. Inspect `.gitlink-signoff.tsv` and its gate before changing gitlinks; use truthful evidence. Read only ordinary source/docs under references, never skills there.

User authorization overrides the skill's interactive approval checkpoints. No intent gaps remain. Publication and pushes are explicit requested side effects. Dependency alignment and CI repair are prerequisites of the single requested release outcome.

**2026-11-05 first local implementation pass:** Six clean direct checkouts were fast-forwarded to their fetched `origin/main` tips; all ten remain selected without nested initialization. Parties now selects module CLI 4.44.3, shared normal/manual lifecycle CI at Builds `fc2122240a39f03628c04fa8433e5eece90b8b4c`, and genuine Platform v1.4.0 at `ae7516ee6961dd31a5e319be59ead76449ab04a0`. Current tuple documentation/assertions and six source-selection-only ledger rows were aligned. The independent release publisher pin was retained.

Verification: `dotnet tool restore`, workflow `actionlint`, `bash scripts/gitlink-rc-gate.sh --worktree`, the declaration renderer check, all seven declaration Python tests, and module validation across Parties/EventStore/Tenants/Memories passed. The CI test project Release build passed with zero warnings/errors; the actual built assembly passed all 141 selected `PartiesContainerPublishWorkflowTests` and `AspireRuntimePreparationTests` cases with zero failures/skips. `git -c core.whitespace=cr-at-eol diff --check` passed; plain `git diff --check` flags existing EditorConfig-required CRLF. Exact commands, ownership boundaries and selected sources are retained in `/tmp/parties-release-20261105/implementation-first-pass.json`; focused XML and logs are in the same directory.

Concurrent external edits to Contracts/catalog tests, capability selectors and legacy workflow cohorts were preserved. Their replacement of the 4.0.4 workflow cohort with 4.1.0 needs parent review against the explicit legacy-cohort constraint. Matching staged gitlinks and package preparation, independent review, broader hosted CI, upstream repairs, commitlint/commits/pushes and publication remain parent-owned work; this local pass establishes no publication or runtime acceptance.

**2026-11-05 review repair pass:** Added a separate exact EventStore 4.1.0/source selector capability, restored the three captured owner mappings, and kept older APIs free of new selector members. Retained the 4.0.4 offline and Memories 3.0.18/SDK 4.0.4 lanes and added a separate latest-cohort lane. Added unconditional real-target transport/selector evaluation, canonical selector byte/digest controls, and actual gateway-router legacy/foreign boundary tests. Updated all four fallback defaults and their real precedence tests, appended the dated ten-source/current-package observation, and refreshed current getting-started/lifecycle instructions. Historical matrix bodies and immutable-consumption refusal helpers remain preserved.

Focused verification passed 78 tests with zero failures/skips: legacy catalog 3, current catalog 10, workflow/capability 34, gateway/fallback 8 and current observations/refusal controls 23. Relevant Release builds had zero warnings/errors. Six freshly packed local consumer packages passed the existing package-only client and portal smoke verifier, which observed 41 owner routes, 41 schemas and 28 canonical adapters. Workflow lint, CRLF-aware whitespace and historical-body preservation checks passed. Commands/results and logs are retained in `/tmp/parties-release-20261105/review-fixes-verification.json` and `review-*.log/xml`. No commit, push, hosted/native qualification or publication was performed by this repair pass; full verification and external selector-proposal reconciliation remain parent-owned.

## Spec Change Log

- Review pass 1: preserve the working tuple/ledger updates and all concurrent edits; patch exact newer-API guards, retained old/new package lanes, current fitness observations and unconditional capability proof. Preserve every historical qualification boundary. User explicitly confirmed integration of all current edits.

## Review Triage Log

- BH-01: medium / patch — The submitted compatibility property and matcher are absent from supported 4.0.4/3.x Contracts; guard production and tests with a separately qualified 4.1/source symbol.
- BH-02: medium / patch — Replacing the old offline and Memories SDK combinations removes meaningful retained coverage; preserve them and add a distinct latest-cohort lane.
- BH-03: medium / patch — Real fallback evaluation returns the changed version while its existing test expects 3.132.2; update the expected fallback alongside the props.
- BH-04: high / patch — Current-selection fitness checks compare effective package versions and committed gitlinks with obsolete identities, so the full CI suite fails. Refresh current observations and their matrix only; retain dated historical receipts and refusal assertions.
- BH-05: false — The architecture sentence refers to release preparation and its nested container publisher; both still use 11a3a79. CI deliberately selects a separately qualified execution revision. Clarify wording if needed, but no mismatched release pin exists.
- BH-06: low / patch — Current getting-started and lifecycle instructions still name the previous tool/Platform tuple; directly correct those current instructions.
- BH-07: medium / patch — Existing new tests inspect declarations and matcher directly; exercise canonical serialization to establish that the declared selector survives the actual catalog boundary.
- BH-08: medium / patch — Actual legacy ingress and foreign-selector refusal require gateway-level coverage; add a focused regression through existing gateway/catalog test infrastructure or retain exact hosted runtime proof before claiming qualification.
- EC-01: medium / patch — Independently confirms BH-01: unconditional 4.1-only APIs break older supported package consumers; use the exact capability fence and exercise both cohorts.
- VG-01: medium / patch — Confirmed broken verification: production and transport tests share the same conditional symbol. Add unconditional real-target evaluation for 4.1.0 so removing the branch cannot make the check disappear.
- VG-02: medium / patch — Independently confirms BH-03: the real missing-first-catalog test has a stale fallback expectation.

## Verification

- Official Microsoft SDK metadata, NuGet flat-container indexes and npm registry audit.
- Owning focused builds/tests and repository guard scripts; complete hosted CI on final source.
- Pinned commitlint CLI for each exact commit candidate, with retained output.
- GitHub release workflow and complete manifest-based NuGet package download/index verification.
