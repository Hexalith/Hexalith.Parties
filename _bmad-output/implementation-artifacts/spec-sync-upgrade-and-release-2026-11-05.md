---
title: 'Synchronize, upgrade and release Parties'
type: 'bugfix'
created: '2026-11-05'
status: 'in-review'
baseline_commit: 'bb2783120c0eed620e3d4f52a2ebaf6aa510126f'
route: 'dispatch'
review_loop_iteration: 1
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

**2026-11-05 upstream CI repairs and runtime diagnosis:** Preserved concurrent source commits, including the root pointer synchronization at `1d078dd9`, EventStore tuple alignment and Tenants phantom-detail fix. The user explicitly authorized recorded nested pointer updates where CI requires them, with no nested checkouts. EventStore's tool/Builds/Platform tuple correction is pushed. Tenants v7.0.3 release42023389616 passed publication verification; all five declared NuGet packages were independently downloaded and matched sourcef4129cfe.

Memories repairs retain exact source/payload ownership, failed-workflow proof, fresh retry attempts with stable output identity, predecessor cleanup obligations, tenant/source fences and legacy history support. They also repair fenced failed-unit restoration, elapsed rate-window assertions, JWT fixture mode, Dapr patch alignment and owned fixture startup/stop/port handling. Focused contract/server/inventory/readiness tests pass; actual warm-restart performance passed1/1 without skips at7.73seconds against the unchanged60-second requirement. Full upstream review, committed-gitlink release fixtures and hosted final-source acceptance remain required.

Parties'15 isolated hosted test reports pass, but runtime CI42022130363 exitsHXR020. An exact-source/private-public-cache reproduction confirmed that preflight's actual reader enriches `catalog-versions.json`, then runtime rematerializes it and invalidates its sealed proof. The proposed Platform repair reuses only the current fully verified ordinary Parties package snapshot; changed/missing proof and changed bytes still refuse. All four diagnostic-only owned runs were stopped with empty-resource receipts. Platform's docs-only current revision passed exact tuple42026337115; the real runtime repair still requires its own source checks and genuine release before final Parties acceptance.

The shared NuGet cache contains an unrelated local McpCli.Abstractions2.0.0 build from `/tmp/story-2-14-packages`; it is preserved. Public artifact checks use a private cache and verify the existing signed artifact hashes. McpCli's live21-vector/independent-smoke lane requires genuine newly published Parties artifacts plus Tenants7.0.3 and SDK4.1.0; prospective artifact hashes or frozen operation changes are not authorized substitutes.

**Current-owner runtime qualification correction:** Hosted Parties CI42039092601 and the isolated original-engine run33e0140c confirm an immutable-artifact mismatch: published Parties3.0.0 embeds SDK3.130.2 and cannot verify the current v2 technical HTTP activation digest. Infrastructure is healthy; the owner bootstrap stops its host. Publishing a new image requires successful current-source CI, so the existing public-image profile creates a publication cycle.

Add an explicit `platform-current-package` purpose for the current committed active Parties owner, built in Release with its own measured private package/runtime artifacts. All reference-owner libraries, SDK and images remain the pinned public package cohort. This is an active-owner build already admitted as source in package mapping, never dependency source fallback. Preserve published-consumer profile semantics and historical receipts. A new private in-memory proof must bind actual source/run/manifest/project/archive/loaded-body identities; no version-only or caller-provided artifact authority is accepted. No public artifact/version is replaced, no reserved candidate-policy acceptance is asserted, and qualificationCredit stays false.

Because CLI4.44.3 only makes functional observations mandatory for its older profile names, a separate required root CI job must validate this same run's content-addressed native/functional evidence, exact source/profile/SDK/loaded current-owner proofs, both actual native cases, persisted effects and complete owned cleanup. Missing or changed evidence fails CI. After green exact-source CI, use the existing release workflow, then measure and admit the genuine new public image and verify published-consumer runtime independently. KEEP all verified source fixes, old/public cohort guards, immutable snapshot protection, latest compatible dependency versions and all previous tests.

## Spec Change Log

- Loop1: correct the non-frozen plan for the immutable Parties3.0.0/SDK3.130.2 activation mismatch. Add explicit current-owner Release/package integration and a mandatory evidence gate; preserve the published-image purpose and qualify the genuine new image after publication. All earlier tested fixes and user changes are KEEP inputs; no code or history is reverted.

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

- UBH-01: medium / patch — Current fitness constants and the current matrix predate the four concurrent pointer advances. Refresh them after final upstream commits exist; preserve historical observations. This is a new source selection after the earlier BH-04 repair.
- UBH-02: medium / patch — Confirmed: FindFailedRetryAsync and atomic AdmitFailedRetryAsync both select a conclusively Unscheduled retry before the genuine failed predecessor, permanently blocking restored claims after payload/tracking failure. Ignore only conclusively never-scheduled attempts when selecting a predecessor; retain every cleanup obligation and reject ambiguous scheduling.
- UBH-03: medium / patch — Confirmed for supported retained-reference legacy/custom Event histories: the stored workflow is dedup-owned while its output ID differs, so the new no-inventory lookup chooses the wrong ID. The correction requires exact tenant/case/source/reference and independently Failed Dapr input proof. Default EventIngestionService inline/no-reference failures remain UnsupportedSourcePayload; producer-shaped tests model retained historical/custom references and make no default payload-retention or full Dapr Event acceptance claim.
- UBH-04: medium / patch — Confirmed: remote scheduling can succeed before its Scheduled inventory acknowledgement fails. Scheduling then remains the newest obligation, but FindFailedRetryAsync rejects it before obtaining the independently Failed workflow proof. Reconcile only that exact terminally failed accepted attempt; unknown/active attempts must remain blocked.
- UBH-05: medium / patch — Confirmed: new expected proof/source/admission refusals escape TryScheduleAsync after claim restoration and CasesEndpoints emits a generic failure. Return an existing structured conflict for precisely typed eligibility refusals; preserve operational exceptions and primary/restoration failure evidence.
- UBH-06: false — PipelinePersistenceIntegrationTests.RestartTopology_ShouldKeepFailedUnitVisibleAndAllowReingestionAfterRecovery already schedules a real Dapr replacement, awaits Completed, and checks both persisted backend IDs equal the original failed business ID. Execute this existing scenario after repair; the distinct legacy Event producer gap is covered by UBH-03.
- UBH-07: medium / patch — The first-retry tests do not assert selection among multiple failed generations. Add a second failed retry with the latest workflow lineage, stable output ID and three retained inventory obligations; grouped with UV-01.
- UBH-08: low / patch — Add the three direct wrong-route/enrolled-selector cases to the actual gateway boundary theory. This is a small test-only correction that verifies the stated per-route selector boundary.
- UBH-09: false — The owned fixture uses Aspire allocation for all three listener categories; actual Dapr startup/readiness, HTTP/gRPC calls, limiter restart and performance restart passed with live owned resources. The unit annotation check is accompanied by those runtime controls; a second listener allocator or parallel port test adds no demonstrated failure coverage.
- UBH-10: false — Normal Parties CI selects platform-package and Builds BuildModelAsync compiles the current Platform composition source in package mode. The observed hosted failure and exact-source reproduction already reach the production call; final uninstrumented exact-source CI remains an explicit acceptance gate. The verification reviewer independently traced and dropped this same tentative gap.
- UE-01: medium / patch — Independently confirms UBH-02: both predecessor selection sites shadow recoverable failures with conclusively Unscheduled admissions. Preserve their obligations while selecting the actual failed producer.
- UE-02: medium / patch — Independently confirms UBH-01: committed gitlink comparison happens before any source-mapping override, so final exact source identities and current matrix entries must be refreshed together.
- UV-01: medium / patch — Preverified regression gap: replacing LastOrDefault with FirstOrDefault does not fail any existing first-retry assertion but blocks a subsequent retry at admission. Add an actual two-failure/three-obligation sequence; grouped with UBH-07.

- RP-01: medium / patch — Parent tracing found a concrete restoration race: after a fresh retry publishes a newer failure, a late scheduling error restores the original claim over it. Three real-Redis/Dapr-payload-adapter controls failed before the correction. An internal conditional restore now keeps the newer hash/index untouched after tenant and tombstone checks; 30 affected controls pass with primary/restoration error evidence preserved.

## Verification

- Official Microsoft SDK metadata, NuGet flat-container indexes and npm registry audit.
- Owning focused builds/tests and repository guard scripts; complete hosted CI on final source.
- Pinned commitlint CLI for each exact commit candidate, with retained output.
- GitHub release workflow and complete manifest-based NuGet package download/index verification.

### Current-owner review (three independent layers, 2026-11-05)

All three layers were launched before collection; all reported before this triage. The reviewed source was frozen, with actual publication/legacy-commit bindings explicitly pending. User authorization preserves every current edit and bypasses human checkpoints; no source/history rollback is permitted.

- CB-01: high / patch (parent) — The null legacy descriptor correctly refuses authority but currently blocks automatic tuple initialization. Create a real same-code legacy commit and record its actual SHA before qualifying/publishing M; do not invent an identity or relax the guard.
- CB-02: high / patch (parent) — New current-owner admission requires SDK4.1/Tenants7.0.4, while the checked-in Platform tuple still names the older published catalog/tool. Bind the genuinely completed Builds publication and recorded producer atomically before clean qualification; publication is already running and no future version is claimed.
- CB-03: medium / patch (parent) — These are new pending source advances after UBH-01. Current assertions still match R2's committed older tuple, not the reviewed working selections. Refresh only current observations after final published identities exist; preserve historical blocks and the immutable-consumption refusals.
- CB-04: high / patch (parent) — The new reviewed working gitlinks have no final matching signoff rows yet. Record truthful source-selection rows for the actual final commits before parent adoption/range qualification; never label those rows runtime or human-review acceptance.
- CB-05: medium / patch — Ordinary package/lifecycle guards accept only the older active declaration hash. The newly added profile changes the actual declaration even when selected ordinary profiles/topology are unchanged. Admit only the exact newly measured declaration alongside the historical one; keep finite public body/image/version checks and mixed-purpose refusals intact.
- CB-06: high / patch — A substituted namespace retaining the required class/method suffix passes the mandatory verifier. Bind exact fully qualified native identities and retain nonzero/consistent report counts, with a valid-content-hash substitution negative.
- CB-07: medium / patch — The evidence reader checks regular-file type only after a blocking open and prechecks links separately. FIFO input can hang, and a raced final link can be followed. Use bounded nonblocking/no-follow held descriptors and directory traversal; test both refusals without hanging.
- CB-08: medium / patch — The new source-equivalence refusal fixtures are not invoked by mandatory CI. Run them in the blocking tuple workflow before descriptor resolution; do not weaken their actual Git-history negatives.
- CB-09: medium / patch — Earlier BH-08 established actual router/catalog boundaries; this finding identifies the additional HTTP serialization/activation hop in PartyEnvironmentTests. Extend the existing real native case with supported selector reads and a foreign-selector refusal, retaining original persisted effects, case names and frozen downstream vectors.
- CB-10: medium / patch — Unconditional keydown cancellation also removes pending focus-ring correction for modifiers, composing input and already-prevented events that do not navigate or scroll. Restrict cancellation to superseding native navigation/scroll intent and cover neutral/prevented/composing keys through held-frame real browser controls.
- CE-01: high / patch — Independently reproduced suffix-spoofed native class acceptance. Same root cause as CB-06; retain this finding separately and fix through exact full identity matching.
- CE-02: medium / patch — Removing both persisted creation timestamps passes because absent values compare equal. Require present, typed creation timestamp values and matching projection data; add missing/null negatives after recomputing valid content identities.
- CE-03: medium / patch — The new singleton profile assertion also replaced the existing ordinary multi-profile Contains behavior. Preserve ordinary compatible package batches while requiring the new current-owner purpose to be alone; cover both positive legacy batch and negative mixed-current invocation.
- CE-04: high / patch (parent) — Moving old groups while the descriptor remains unbound prevents both lanes. Same root cause as CB-01; actual L binding precedes qualification and cannot be replaced by an immutable-old-model result.
- CV-01: high / patch — Verified regression gap: removing the live owner-body refusal would still assign expected hashes, while existing tests stop before runtime or exercise only the lower-level map helper. Add controlled-process observation/preparation-boundary refusals for missing/substituted running owner bodies, asserting no observation/readiness publication.
- CV-02: medium / patch — Verified broken-verification gap: removing the source-equivalence rejection fails its existing refusal tests, but mandatory CI never runs them. Same root cause as CB-08; wire the test suite into the blocking path.
- CV-03: high / patch (parent) — The optional dispatch assertion cannot replace the committed null legacy identity. Same root cause as CB-01; fill the real descriptor before M's automatic source qualification.

KEEP: old public cohort hashes/receipts and all349 original controls; new measured private-owner authority and false qualification credit; source-equivalence enforcement; both unchanged native case names and persisted assertions; unchanged O5/ring checks, deterministic RED and60 passing repaired browser runs; all user/external edits. Add new current-only cases in separate classes so the immutable legacy349 selection remains exact. Full actual reader/native/hosted acceptance remains pending and is not inferred from local fixtures.
