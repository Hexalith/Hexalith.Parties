---
title: 'Synchronize, upgrade and release Parties'
type: 'bugfix'
created: '2026-11-05'
status: 'done'
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

## Completion checkpoint — 2026-11-09

This task was fulfilled by the [completed November 6 continuation](spec-sync-upgrade-and-release-2026-11-06.md). Parties [v3.1.0](https://github.com/Hexalith/Hexalith.Parties/releases/tag/v3.1.0) was published on 2026-11-06 at 21:40:43 UTC from `50904cc4ba53c9b0b2c082d5872a0d276be8f5c3`. The dated pending checkpoints below are retained as execution history and are superseded by this completion evidence.

Fresh verification confirms the exact release tag/source, successful release and all eight prerequisite workflows, and all eleven public signed NuGet archives with unchanged archive/DLL hashes and matching repository metadata. The digest-verified GitHub publication artifact contains the same packages and three verified raw OCI graphs, each for linux/amd64 and linux/arm64. [Closure evidence](tests/sync-release-2026-11-05-closure-2026-11-09/README.md) distinguishes these checks from new runtime execution or a fresh registry graph download.

The old publication receipt's `manifestSha256` differs from the exact release Git blob. It is preserved; the [fresh receipt](tests/sync-release-2026-11-05-closure-2026-11-09/verification.json) instead binds the measured Git inventory and all eleven packages directly. The qualified dependency snapshot and its tested compatibility holds remain documented in the November 6 continuation.

Current main includes later and concurrent work. Its selected checkouts were clean at capture, but four upstream tips had advanced and its newest CI was pending. This historical completion grants no acceptance to those later source changes. Five independently reviewed later/concurrent defect groups are appended to the deferred ledger; the original release identities and every existing user edit are preserved.

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
- [x] `references/*` -- direct fast-forwards complete; inspect and repair relevant failing upstream CI from owning roots.
- [x] Central catalog and owning tool manifests -- adopt verified stable updates and retain documented compatibility exceptions.
- [x] Platform publication -- await successful exact tuple CI, then establish its real versioned release when needed for Parties preparation.
- [x] Parties tool/CI/capability files -- align the selected tuple, repair observed failures and update meaningful regression coverage.
- [x] Owning changes -- run focused validation, independent review, commitlint and safe pushes; commit parent pointers after their sources exist remotely.
- [x] Release workflow -- obtain green exact-source CI and range proof, dispatch, satisfy authorized reviewer gates and verify all registry outputs.

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

- 2026-11-05 authenticated tuple data preparation: preserve all production/test/helper/workflow/docs bytes between M and L; allow exactly one additional metadata path, declaration provenance, alongside the original seven tuple paths. Require the original L provenance Git-body hash3e9b04109c5ddb34a0ac6801f5cdb36e1cbc102c65bfccb54b77c4d408091a61 and permit only the already published Tenants7.0.4 version/source pair change in M. This prevents relabeling historical dependency authority.
- 2026-11-05 honest suite selection: M uses253 current controls; remove only nine historical reader cases that otherwise return early under7.0.4. Keep their actual7.0.2 positives in the unchanged349-case L manifest (raw hash61689f1598dac43a9fa28dcf5319008059e0fea0f1018209b1a8e79e4f63d7b0), and retain the two real current-owner reader cases and reference checks. Exact committed253/349 runs and runtime effects remain pending.

## Review Triage Log

### Resumed historical closure review — 2026-11-09

All three independent layers completed before triage. The frozen baseline-to-worktree diff also contains later work from other active specs. Verified later/concurrent defects are recorded separately from the completed November 6 release; no source edits or qualified tuple substitutions were made in this closure.

- R-B01: medium / defer — Diagnostic workflow refuses synchronized Builds checkout. The snapshot initializes the root Builds gitlink 27145e52ffab5623198ba5efdab5cdb71aa42d28 but requires 3c8096180ecf2aa539ac454815efbfceb8a92822. This is a later source-adoption mismatch after the completed 50904cc4 release; reconcile measured runtime authority and checkout selection without blindly blessing a new tuple.
- R-B02: high / defer — Mandatory public verifier refuses synchronized source tuple. The CI caller supplies root gitlinks e39a9a2587ec150d550510763ab69db305d13fdb and 27145e52ffab5623198ba5efdab5cdb71aa42d28, while validate requires 0f4e27f1f78310bd731c10c19dc51bb1e0345d44 and 3c8096180ecf2aa539ac454815efbfceb8a92822 before reading evidence. The public gate was introduced in 71f61d89cf9a1aa3a60e2065f913b95b4b2a639b after this release.
- R-B03: medium / defer — Ownership marker initialization can leak a preparation directory. 18dd525c3ee80b065bb410bc639cef89f15f163a moved marker creation/write outside the cleanup try. An I/O failure can leave an occupied directory that DeleteOwnedDirectory cannot authenticate. The released 50904cc4 source retained its initialization catch; this is a later regression.
- R-B04: medium / patch-closed-concurrently — Diagnostic Dapr downloads lacked archive digest checks in the snapshot. Current committed d3d5e7d4de132c3e8b81d1ab04fb8c3541b8aaa4 reads pinned digest metadata, validates its schema and SHA shape, and verifies each archive before tar. The independently committed correction closes the snapshot finding; no additional edit is needed.
- R-B05: false / reject — Private verifier accepts its two required native cases. The frozen intent explicitly requires both actual native cases. CASES names those exact two and the verifier checks unique presence, all passed outcomes and consistent counters. Whole-suite execution is enforced by the invoking test runner; the release receipt independently records all 28 actual cases. No requirement for the private evidence reader to freeze all evolving helper-test names was established.
- R-B06: false / reject — Private verifier does not read selectorCompatibilityObserved. PartyEnvironmentTests unconditionally awaits AssertLiveSelectorCompatibilityAsync before persistence observation publication, and emits the flag as literal true. The required passing native case therefore includes those assertions. Changing only the redundant flag in a synthetic resealed document does not demonstrate a supported producer that omits the selector checks or a new false acceptance.
- R-B07: false / reject — Private verifier ignores assembly scope labels. The loaded and copied arrays already identify distinct roles and each is checked against the exact SDK identity/body map. NativeAssemblyObservations generates fixed labels from actual process/reflection and copied-output measurements; swapping labels alone changes neither measurement nor admission. No supported producer or substituted body escaping the actual checks was shown.
- R-B08: false / reject — Equivalent creation timestamp spellings compare unequal. The real native observation serializes createdEnvelope.Timestamp and the equal detail.CreatedAt as DateTimeOffset values in the same anonymous document/serializer. Native assertions require the values to be equal before issuance. The proposed mixed Z/+00:00 spelling is a synthetic re-encoding, not a demonstrated output of the supported producer; the actual 28-case release report passed.
- R-B09: low / reject — Public verifier permits a far-future measuredAtUtc. The value is syntactically validated and ordered after container startup; the producer writes DateTimeOffset.UtcNow while source/run/body authority is independently checked. Replacing only this diagnostic time does not substitute any run, image or body. No normal clock/producer failure was demonstrated; adding a new execution-interval authority/tolerance surface is disproportionate for this diagnostic edge.
- R-B10: false / reject — Observer could report empty for contradictory resourceIds/isEmpty. CompositionRunResources.IsEmpty and ResourceIds are computed read-only properties of the pinned producer. Empty five-kind inventories with unverified=false compute isEmpty=true and resourceIds=[]; the proposed contradiction cannot be serialized by that producer. Both actual release cleanup receipts and the mandatory verifier independently establish complete emptiness.
- R-E01: high / defer — Public evidence caller tuple differs from required published tuple. Independently confirms R-B02 at the CI caller and verifier boundary. It concerns the public-profile adoption introduced after released source 50904cc4, and does not invalidate the original current-owner release proof.
- R-E02: medium / defer — Diagnostic input fence differs from synchronized Builds selection. Independently confirms R-B01. Keep publication/runtime authority distinct from root source selection when fixing the later workflow; do not replace a qualified digest with an unqualified tip.
- R-E03: medium / defer — Canceled asset-staging test can leave its MSBuild process running. NativeAdminUiAssetStagingTests StageAsync awaits WaitForExitAsync with the test token but has no kill/wait finally; Process.Dispose does not terminate the child. Outer directory deletion can race the still-running child. This test was added after the completed release, in the preserved November 9 preparation work.
- R-V01: medium / defer — Split private catalog source staging lacks executed GenerateAsync regression. Preverified: existing cleanup cases invoke RunAsync via reflection, and normal CI selects configuration/cleanup classes without executing GenerateAsync. Program.cs references PublishedCatalogArtifactCohort in another source, so reverting to Program-only staging evades the selected tests but breaks runtime generation. This is a preserved concurrent development-catalog change outside the completed release snapshot.
- R-V02: high / defer — Public evidence tuple mismatch is hidden by synthetic constants. Independently confirms R-B02; synthetic fixtures replace expected identities with verifier constants and the workflow test inspects text. A real caller/reader tuple control is needed for the later adoption.


### Readiness Retry-After review (three independent layers, 2026-11-06)

All three independent layers completed before triage. The reviewed three-file diff is frozen as private `native-readiness-retry-review.diff`; actual hosted/native qualification remains pending.

- RR-B1: medium / patch — Ready response received after absolute deadline can still satisfy predicate before linkedtimerfires. PollAsync checks absolute time before GET only; awaited GET/progress can cross deadline, then success predicate returns. Existing original deadline state must also gate response acceptance. Add postresponse check and controlled heldresponse case.
- RR-B2: medium / patch — One-second deadline starts before fixture/key/client setup but requires exactlyoneGET. PreparationWorkspace performs directories,portallocation and Plan construction; Preparation() creates signingkey. A scheduling/filesystem pause can expire budget before GET. Finish setup then latch shortbudget immediately before poll using test-only fixture support; no new production timer.
- RR-B3: false / reject — Consecutive429scenarios absent. Production has no retrycount or once-only state; each429 traverses identical loop branch and continues. Existing first429 checks already fail on removingcontinue/dispatch/predicate isolation. No distinct guard or bad repeated outcome was demonstrated; do not add coverage solely mirroring unchanged loop.
- RR-B4: medium / patch — Newdeadline cases do not assert polling Task.IsCanceled. AppHostRunner distinguishes faulted cutover asHXR027 fromcancellationHXR021. Existing elapsedstartupcase asserts IsCanceled; retain tasks and assert same downstream invariant for newdeadlinecases.
- RR-E1: medium / patch — Valid oversizedRetryAfter with nullabledeadline can exceed Task.Delay limit. CompositionRunPlan.StartupDeadlineUtc isnullable and CompositionStartupClock.Remaining(null,now) explicitly defines600seconds; conditionalcap currentlyskips thatdefault. Reuse clock remaining unconditionally instead of 49-day chunks/newtimer. Add bounded component no-range-fault/cancellation control with validlongdelta.
- RR-V1: medium / patch — Positive delta equalsto fallback so ignoringDelta remainsundetected. Only successfuldelta is1s, identical fallback; longdelta casescancelbefore retry orcapsubseconddeadline. Use delta>fallback and derive notBefore fromactualhint; confirm removingDelta fails.
- RR-P1: false / reject — Parent initial negative remaining claim. Actual published447 CompositionStartupClock.Remaining clamps elapsed toTimeSpan.Zero, so existing capped Task.Delay(0) thenOCE already correct. No extra production <=0guard added.


Readiness review closure: RR-B1 checks the actual token/clock after response and before readiness acceptance; a held late200 omission fails1/1. RR-B2 finishes fixture/key/client setup before latching the immutable test deadline. RR-B4 retains deadline/cancellation tasks and verifies IsCanceled, matching the runner's HXR021 classification. RR-E1 uses the same existing clock unconditionally, including its null/default600 budget; omitting this cap fails1/1 on a valid60-day hint. RR-V1 advertises two seconds rather than the one-second fallback and derives notBefore from the actual hint; omitting delta handling fails1/1. Restored v2 passes all15 cases without skips and builds Release with zero warnings/errors. The parent verified source/protected hashes, each omission report and the restored report. No new production timer, public contract, limiter exception or readiness authority was introduced. Both rejected claims remain false. Evidence: private `platform-native-rate-limit-v2-frozen-receipt.json` and `native-readiness-retry-review-triage.json`.

### Native caller and issuer review (three independent layers, 2026-11-06)

All three context-free layers completed before triage. Blind and edge-case
reviewers returned no findings. The verification reviewer returned one finding.

- NA-01: high / patch, closed — The single-public-tenant boundary lacked a mixed owner-plus-extra-valid-tenant refusal regression. The existing refusal case now requires the exact early scope error. Removing only the count guard produces the expected three-pass/one-failure RED result; restoring it passes all15 cases. No production guard or suite count changed. Evidence: private `sdk410-auth-review-triage.json` and the frozen extra-tenant patch/RED report.


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

### Builds release recovery review (three independent layers, 2026-11-05)

All three layers launched before collection; all completed before this triage. The source diff includes preserved external release optimization and the reviewed nine-file repair.

- BB-01: false — Updating the finite catalog deliberately requires a matching Platform declaration and measured producer tuple. This is already the pending CB-02 adoption and the recorded post-publication plan; current Parties still records the old released tuple. Refusal of a mixed old Platform/new catalog is required behavior, not evidence that Builds should mislabel Tenants7.0.4 as7.0.2. Parent must refresh actual declaration provenance when qualifying the matching Platform source.
- BB-02: medium / patch — The installed semantic-release runs generateNotes and prepare after verifyRelease. Add the final qualified-source check as the last existing prepare hook, retaining actual-version qualification in verifyRelease and the publisher check; test real wrapper success/refusal.
- BB-03: medium / patch — A correctly rejected post-tag publication can leave a tag without complete public artifacts. Document additive recovery using a newly qualified current source/version, preserving old tags and partial registry history; do not add a gate-bypassing same-version publication shortcut.
- BB-04: medium / patch — The late shell guard can fall back to another successful run after the recorded attempt begins rerunning. Require the late selected run and attempt to equal the inventory proof, with a real fallback refusal fixture; retain initial dispatch selection behavior.
- BB-05: false — The publication workflow has a finite job timeout, and API failures correctly refuse authority. The claimed indefinite release has no reachable unbounded execution beyond that job limit. No observed transport stall establishes a need for a new retry/deadline subsystem; do not turn unavailable identity evidence into acceptance.
- BB-06: medium / patch — Failed API lookups discard the CLI exit status and safe HTTP category, making existing release diagnosis ambiguous. Retain bounded exit/status classification without raw credential-bearing CLI output, and test redaction/refusal.
- BB-07: false — The thrown lookup error already names the selected source CI run, and same-run workflow logs plus producer-attempt identity retain the release source/run context. The alleged untraceable lookup is therefore disproved; BB-06 improves safe cause classification without inventing a separate evidence authority.
- BB-08: medium / patch — Successful tests manually construct the CI inventory, so deleting the real qualifier's SourceCiRun forwarding does not fail them. Exercise successful real qualification and inspect its emitted mode/result/proof/eligibility, retaining honest fixture ineligibility and actual controls. Same root cause as BV-01.
- BB-09: low / patch — Add the direct CI-backed prerelease publisher combination to the existing harness, retaining exact branch/run identity, GitHub credential/destination and mismatch refusal. This is a direct test addition to existing cases, with no production branch or public option.
- BB-10: medium / patch — Text positions and direct helper tests do not prove the real pre-tag wrapper propagates late refusal after successful qualification/archive. Execute the real wrapper in an isolated controlled dependency layout and assert success/refusal order. Same root cause as BV-02.
- BV-01: medium / patch — Preverified inventory-producer gap: removing SourceCiRun forwarding leaves existing tests green and breaks real release inventory validation. Add the actual successful producer case, grouped with BB-08.
- BV-02: medium / patch — Preverified caller-propagation gap: suppressing the final guard's exception can evade textual tests. Add executable real-wrapper controls, grouped with BB-10 and covering the new final prepare check from BB-02.

The edge-case layer returned no additional findings. Independent hosted failure BCI-01 also reveals one more stale Release placement assertion in the prepare-module-build helper contract; validate its mandatory exact-source CI chain and preserved Node/contract ordering.

- FCI-01: medium / patch — Hosted FrontComposer Quality6341/6342 identifies a state-only pending registration wait attached to a fixed CascadingValue wrapper. Pinned bUnit2.13.3 waits only on that component's OnAfterRender; the failed assertion executed once with zero renders. Pre-subscribe to pending.Changed, check synchronous completion, await with the original one-second bound/cancellation, and unsubscribe in finally; preserve actual generated form submission, Pending state, subsequent PollOnce and already-rendered grid outcomes. The one-file correction passes100/100 cases across50 class runs with owning recorded catalog06a51 and zero-warning Release; fresh exact-source hosted Quality remains required.

- PFCI-01: medium / patch — Exact-source Platform CI passed the first160 controls, then one of28 qualification controls failed during test setup: the near-miss mutation still searched for7.0.2 although the canonical current fixture uses7.0.4. Derive its source version from fixture provenance and replace the complete XML value with a distinct unsupported version. Preserve names/counts, actual changed bytes and refusal-before-effects assertions. The same changed test passes28/28 with current704 and28/28 with authentic old702 component data; each Release build has zero warnings/errors. Full exact committed253/349 remains required.

### Source-inventory repair review (three independent layers, 2026-11-05)

All three context-free layers launched before collection; all completed before triage. The reviewed patch follows the demonstrated717711-byte tracked listing failure against the public65536-character capture limit. Current preparation has257 cases including four real-Git controls; the original349-case legacy manifest remains immutable.

- IB-01: medium / patch (parent) — New shared inventory/proof/test/doc bytes differ from the current recorded L32ffe802. Create and push the actual new same-code L, then record its real SHA before committing/qualifying M. This is a fresh occurrence of the already recorded binding obligation, not permission to relax the eight-path comparator.
- IB-02: medium / patch — The inventory's independent60-second budget ignores a shorter remaining StartupDeadlineUtc, so it can continue after the declared startup window while subsequent build/reader calls already honor that window. Bound enumeration by the earlier deadline and keep per-command/overall limits, with an expired-deadline control.
- IB-03: low / patch — A literal valid U+FFFD filename is incorrectly treated as a decoding error. Use strict UTF-8 for the metadata process output and reject actual decode failures while preserving valid literal names; extend the existing literal-name control. This directly corrects an existing decoder-refusal condition rather than widening accepted malformed metadata.
- IB-04: low / patch — All inventory positives currently pass HEAD, so ignoring the supplied immutable revision would evade them. Extend an existing real-Git control with a later committed file and prove the earlier revision's exact inventory remains selected.
- IB-05: low / rejected — Exhaustive4096-process/32768-entry/60-second threshold fixtures concern capacity limits well above the actual1138-directory/8202-entry owner and would require substantial fixtures or a new injectable limit subsystem. Existing truncation/depth/failed-child tests plus the independently bounded whole operation remain required; add the directly relevant startup-deadline test from IB-02. No observed current input loses completeness or evades the implemented finite limits.
- IB-06: medium / patch — The new inventory collapses truncated output, Git failures and exhausted budgets into one refusal, making a real qualification failure ambiguous. Include only a safe operation/limit/exit classification; omit raw Git stderr, environment and source-path contents. Cover representative classifications in existing negatives.
- IV-01: medium / patch — Preverified creation-boundary regression gap: removing the new revision comparisons permits a clean empty commit during preparation to bind a later parent proof to the earlier artifact receipt. Existing source-drift tests mutate after issuance or are Platform-owned, and do not exercise this standalone creation window. Add a controlled clean-HEAD advance during current-owner preparation, including final reader/issuance, asserting refusal before resource effects without synthetic qualification authority.

The edge-case layer returned no additional findings. Shared helper/test/production/workflow/docs changes still require a new real L and M before hosted verification; no passing257/349 or runtime acceptance is inferred from local fixtures.

### McpCli catalog-reader closure review (three independent layers, 2026-11-05)

All three context-free layers were launched before collection and completed before this triage. Blind reported six findings; edge-case and verification-gap layers returned none. The exact reviewed diff is 30,063 bytes, SHA256 `64041e3c68ba16c804aca4f4cb476f2f31960a698431ee1c1f225a378ee6bb20`. Later clarification establishes that the blind compiler probes used generic SDK projects rather than the actual fresh Release reader path.

- MCB-01: medium / patch — On Linux the new `*.cs` enumeration misses uppercase `.CS`, while MSBuild's default compile glob includes it. This is the same extra-source mutation domain the new guard/tests already claim to refuse. Enumerate extensions case-insensitively and exercise the actual preparation guard with an uppercase extra source.
- MCB-02: false — The reported prepopulated `obj/Debug` compiler probe does not enter `GenerateAsync`: its private reader directory must be newly created, occupied directories are refused, ordinary restore does not generate stale compiler input, and the real build uses Release. The reviewer explicitly confirmed no actual-reader reproduction or normal input-producing operation. Authenticated SDK/package generation is an existing trust boundary; do not add a generic generated-code attestation subsystem for this unestablished path.
- MCB-03: medium / patch — The copied input baseline is read after writes and is compared with itself, rather than requiring Program/cohort/global-policy bytes to equal their authenticated sources. Keep expected digests from immutable in-memory source/transformation bytes and compare actual copies to them before compilation; include generated project/bindings instead of blessing an altered copy during baseline capture.
- MCB-04: medium / patch — The claim about arbitrary changed NuGet targets is refuted by `RequireCoverage`, which verifies every cached archive entry and consumed build/target asset after restore; canonical Python preparation also seals its selected catalog and overlay. A bounded reader-local gap remains: its root props can resolve a different sibling Builds catalog, and inherited extension/ancestry imports are not locally bound. Add the actual direct recorded Builds catalog to reader provenance, force that selected path, and isolate unnecessary private-reader extension/automatic ancestry hooks. Preserve the existing authenticated package graph; no broad compiler-hermeticity redesign is required.
- MCB-05: medium / patch — An altered source introduced by the build can reach generation before the final refusal. Repeat the immutable input/extra-source check immediately after build and before launching the reader, and assert that a build-phase mutation never reaches generation.
- MCB-06: false — The controlled success fixture intentionally tests input/hash/evidence behavior and makes no actual compiler or runtime acceptance claim. The existing mandatory native 21-vector and independent Aspire lanes invoke the actual `GenerateAsync`/dotnet path with the real templates; those remain required on final adopted source. Verification-gap review independently found those controls present. A focused measured compile smoke may validate new import-isolation flags, but no duplicate unit fixture is required to pretend the already-pending live qualification has happened.

The same coding child will patch the four verified groups. The public Parties identity/adoption remains dependent on genuine publication; no artifact approval, conformance or runtime result is inferred here.

## Verification

- Official Microsoft SDK metadata, NuGet flat-container indexes and npm registry audit.
- Owning focused builds/tests and repository guard scripts; complete hosted CI on final source.
- Pinned commitlint CLI for each exact commit candidate, with retained output.
- GitHub release workflow and complete manifest-based NuGet package download/index verification.

### Current-source verification checkpoint (2026-11-05)

- Genuine Builds 4.44.4 at `5fa2b97c8b22047c2c33e2c9f7017fd5d604c9ef` completed release run 42077837054. Both public tool packages were independently indexed, downloaded and signature/source verified.
- Platform current source `8946209702a6c1c48239f206dd452ae7d729a307` passed all 258 required cases with zero failures or skips in run 42091454297. Both real current-owner package-reader cases passed, including the production final-revision-guard reach assertion. All five catalog-binding rows passed in run 42091454274.
- Same changed-code legacy source `0b6635d97c4959b09ba90b8c2df8c48f40525691` differs only in the eight permitted metadata paths; actual Git-source equivalence was verified. Its full hosted run passed 349/349 with zero failures or skips in run 42091454297. Genuine Platform v1.5.0 was then created and its exact tag/source verified at `8946209702a6c1c48239f206dd452ae7d729a307`.
- Root adoption preparation passed 70 workflow tests and 18 source-selection/refusal controls; both relevant Release builds had zero warnings or errors. Three HEAD-bound fitness checks require the actual atomic pointer commit.
- External Builds source `c58c3cb78de3cb6cc10ff2924c56e7f70f359997` remains preserved upstream. Release 42085923924 correctly refused because another session moved main back to the released source during qualification, then restored it. No 4.44.5 tag or publication was inferred, and no source guard was weakened.

### McpCli catalog-reader closure preparation (2026-11-05)

- Frozen nine-file owning McpCli patch at baseline `aa2845edbea23444a1d362060b8d0f5dc05a132e` copies the real selected `PublishedCatalogArtifactCohort.cs` alongside Program/project, binds all three original templates through the online audit inputs, verifies original/copied bytes before compilation and after generation, rejects additional C# sources, and retains the source digests in safe preparation evidence.
- The ordinary reader request still omits private-purpose/proof and public full-cohort authority. All 47 existing dependency/adoption/vector/plan/policy inputs remain byte-identical; future Parties publication/adoption and actual 21-vector plus independent Aspire smoke validation remain pending.
- Relevant Release build had zero warnings/errors, 42 .NET controls and 3 focused Python controls passed. Evidence: `mcpcli-catalog-closure-receipt.json`; reviewed immutable diff SHA256 `64041e3c68ba16c804aca4f4cb476f2f31960a698431ee1c1f225a378ee6bb20`. These local controls do not claim final public-artifact or runtime qualification.

### Native startup diagnosis and genuine Memories prerequisite (2026-11-05)

- Root adoption `5c11935f2241d46838a482939bc9a3ae06cda86f` passed its actual three HEAD-bound fitness cases and both gitlink gates before push. Hosted CI42095967132 then exposed a real verifier-fixture timing defect: parent10s/child15s/completion20s clocks conflicted during parallel PowerShell startup. The unchanged binary deterministically failed both original contention cases under an11s delayed peer.
- Root repair `b74a12c54162dbc890accb483c6f97e3bf618eab` uses one bounded startup deadline and begins completion timing after release. Its affected class18/18 and repeated delayed contention6/6 passed locally; fresh CI42099216118 verified all581 Ci.Tests cases, including the three reservation cases. All seven independent jobs, CodeQL, commitlint and range gate passed. Native integration correctly refused startup after600s; no native success or functional credit was inferred.
- Exact clean R5 local reproduction measured both pinned Memories3.0.23 hosts failing at full-catalog decode with `RouteCatalogValidationException` / `invalid-json-or-closed-schema`. Both actual dependency files select SDK3.132.2, whose closed schema lacks the three `execution.compatibilityProjectionActorTypes` fields emitted by the SDK4.1 Parties declarations. Activation v2 support alone was insufficient. The measured receipt is `runtime-r5-diagnosis-cleanup-receipt.json`; exact down verified every owned inventory empty, no unverified discovery and all five ambient containers preserved. The independent failed local attempt also cleaned up exactly.
- Genuine Memories3.0.24 at source `859680b38a77f04cc8f81ab7a5fe7f40a617cc2a` completed release42108808279 at21:17:03Z after exact-source CI/protection preflight. The actual protected production deployment was approved under explicit user authorization; all automated gates remained enabled. All ten public NuGet indexes, downloads, signatures and repository commits were independently verified. All five actual OCI indexes, both Linux architectures and source/version labels were independently verified against the immutable published evidence. SDK measurements inside the two new core images remain pending before tuple adoption.
- The new release does not change any old signed artifact or historical image map. Next adoption must add its real catalog/source/snapshot authority, qualify the exact next producer, and update only the current reference cohort while retaining the original349-case same-code legacy lane. Parties publication remains pending fresh native/effects/cleanup success.


### Published reference refresh after the native startup diagnosis

The native Parties startup failure was traced to the published Memories
3.0.23 images: their SDK 3.132.2 closed-schema reader rejected the actual
SDK 4.1 catalog field. Both owned diagnostic runs were cleaned up; no
passing native qualification was claimed from those failed runs.

- Genuine Memories v3.0.24 at
  `859680b38a77f04cc8f81ab7a5fe7f40a617cc2a` was published by run
  `42108808279`. All ten signed public NuGets and five dual-architecture
  images were independently verified. Both core images contain SDK 4.1.0;
  eight DLL bodies match the official SDK archives. Inspection containers
  were never started and were removed; five pre-existing containers remain.
- Genuine McpCli v2.0.1 at
  `aa2845edbea23444a1d362060b8d0f5dc05a132e` is independently indexed,
  downloaded, source-checked and signature-verified for both packages. The
  actual API comparison against Abstractions 2.0.0 reports no API changes.
- Genuine Builds v4.44.5 at
  `afb2d4fd556b5d30a92734d21cb62bfe946ae958` passed exact-source CI
  `42115650268` and release `42118761518`. Both signed NuGets, catalog,
  inventory and paired Tooling/AppHost bodies were independently verified.
  Family-local live audits preserve unrelated package rows and history.
- Incoming EventStore changes were integrated, then the test-only ephemeral
  credential repair was pushed at
  `eff9f6947a3762da61d10642f20d002123ccbe6a`. The unchanged secrets scanner
  passed 274 cases, native-source admission passed 11 cases, and its main
  CI `42116940835` and integration `42116939556` passed. All eight
  exact-source workflows are verified successful.
- Incoming Tenants changes were integrated. A rerun isolated an incorrect
  production dependency assertion; source qualification and all seven
  independent jobs passed on the unchanged incoming source. The subsequent
  two-test-file repair at `cf36618a2c27b5722a1ff811cfbbce4528c3c105`
  validates the exact Debug/source/private-opt-in helper edge and selects
  that same source helper in its test project only under the exact opt-in.
  Governance 485/485, default Debug/source boundaries 2/2, explicit private
  boundaries 3/3, and five actual MSBuild selection variants passed. Full
  CI `42121395272` and source qualification `42121394551` remain pending.
- Platform current source `f756111d2ed0f414268e882deb5c6da7a924576a`
  and same-code historical source
  `dd54773c34cfb55b96c4ea0680eb1f2e9ecf691a` are committed and pushed.
  Source equivalence passed with exactly eight permitted metadata paths;
  the original 349-control manifest bytes remain unchanged. The current
  private purpose selects measured Memories 3.0.24/McpCli 2.0.1 bodies
  and genuine Builds 4.44.5, preserving every older public cohort and
  producer pair. Component 14/14, tuple 19/19 and helper 9/9 checks passed.
  Current/legacy CI `42122962231` and catalog CI `42122962229` are pending;
  the two actual reader controls will be verified from the hosted current
  artifact rather than initializing additional local nested checkouts.

The original nine-file McpCli closure repair remains preserved while the
actual new Parties publication is pending. Parties has no new release or
NuGet publication yet at this checkpoint. The remaining acceptance still
requires the genuine qualified Platform release, atomic root adoption,
passing native cases with persisted effects and cleanup, actual Parties
release/publication verification, and the planned public consumer refresh.


### Owner build catalog consistency follow-up

Platform run `42122962231` failed after 244 passing cases. Both exact
current reader cases failed in the owner Release build before proof or
reader issuance; their detailed temporary build logs had been removed.
No actual HXW012 code was retained, so that code is not claimed as an
observed CI diagnostic. The published catalog matrix passed all five rows.

The private build omitted the existing early catalog flag used for model
builds. The new single functional argument makes owner runtime/package
preparation consume the same pinned catalog. Bounded finite phase/status
and diagnostic-code output now survives a preparation failure without
exposing private paths or raw process output. Component 15/15 and helper
9/9 checks pass; Release has zero warnings. One independent diagnostic
fact raises current controls to 259; historical controls remain 349 with
the same original manifest bytes.

Current source `7122fa4d056f0f9edb74f98d66aa41115cfdc702` and historical
source `9eb0521db5e9895cc126baa545f350031d6cd374` are pushed, with exact
eight-path source equivalence verified. Tuple run `42126815045` and
catalog run `42126815444` are pending; both actual reader cases must pass
in that hosted run before any Platform release.

Tenants source `cf36618a2c27b5722a1ff811cfbbce4528c3c105` now has all
five exact-source workflows verified successful, including full main CI,
Aspire and source-reference qualification. The optional performance job
was skipped by its existing condition; no performance execution is claimed.


### Qualified Platform publication for final root adoption

Run `42126815045` passed both actual source-bound reader cases and all
259 current controls, followed by the original 349 historical controls.
Both independently downloaded artifact digests, every result group,
source/run/attempt bindings, immutable legacy manifest and eight-path
source equivalence are verified. Catalog run `42126815444` passed all five
rows. The early catalog argument resolves the observed owner preparation
failures; no unretained specific compiler/error code is claimed.

Genuine Platform v1.5.1 was published at 2026-11-05T23:29:08Z and its tag
was independently resolved to `7122fa4d056f0f9edb74f98d66aa41115cfdc702`.
Final root adoption uses this qualified publication with Builds v4.44.5,
SDK 4.1.0, Tenants 7.0.4, Memories 3.0.24 and McpCli.Abstractions 2.0.1.
Root native qualification, actual Parties release and eleven-package
publication verification remain pending at this checkpoint.


### Root capability and repeated-generation startup repairs

Atomic adoption `fe0b41bda22b388cef8b93bd1e30a6d427a8ca38` passed
90 focused controls and the three post-commit source-selection controls.
Hosted CI then exposed a finite Memories refresh capability list missing
published 3.0.24. Commit `a217cda7c6504ddcb05d0f159500d2f965eecd6f`
adds that exact version, with an actual failing-before/passing-after
MSBuild evaluation and 45 passing selector/REST controls. All isolated
jobs in CI `42133947966` passed; native startup alone failed HXR027.
No native case or persisted effect is claimed for that failed run.

A normal local reproduction activated all seven catalog participants and
measured a stale receipt: fresh owner bodies and request B matched, while
current-reader receipt still described preparation A. All ten public
reference DLL hashes matched. Platform equal-canonical-directory reuse
deleted verified pending receipt B and copied retained receipt A. The
minimal fix preserves exact already-verified B bytes for current-reader
without altering the immutable original catalog receipt or proof checks.
The existing real owner-reader fact now performs a second actual private
build/pack and Generate, checks equal canonical bytes and original-receipt
preservation, validates B and refuses it under A. Current259/legacy349
counts and the immutable historical profile remain unchanged.

Platform current `ae9fb2bdcb90b3dc4db7971abcb6b41c14e0890c` and real
same-code historical `7cf37c62408df24e103aec80678bce8e10dffaae` are pushed;
exact eight-path metadata equivalence passes. Hosted tuple42139421515
and catalog42139420603 remain pending. Release compilation has zero
warnings/errors; this does not replace hosted reader execution. The local
run was stopped with all six resource inventories verified empty, owned
containers10 to0 and all five ambient containers preserved. No nested
checkout was initialized. Parties release remains pending genuine Platform
qualification, root native effects/cleanup and exact-source CI.


### New upstream work integrated during qualification

The ten-remote check found new Memories `246157a7ff8e2dcd8d356c833a77a02514490d2f`,
FrontComposer `f577f958ed1c043eec7feabc4885f59d48428818` and McpCli
`cadc4d1911df304964b3a54e79b8c138ecc51abd`. They were integrated under the
user's explicit instruction to include current edits. McpCli's three
overlaps were merged while preserving all nine closure changes and all
external pipe/redaction changes; 57 focused .NET and three Python cases
passed with a zero-warning Release build. No future Parties publication
data or live-vector acceptance was fabricated.

Genuine Memories v3.0.25/release42134622308 adds the production intent
adapter assembly to its ordinary DomainService. All ten signed NuGets,
five dual-architecture OCI graphs, both core images' SDK4.1.0 bodies and
three reader DLL/archive identities were independently verified. Its
released declaration remains byte-identical. Stopped image inspection
containers were removed and all five ambient containers preserved. The
REST24-to25 complete public API comparison reports no changes or breaks.
Builds adoption `3f3c8866e15ebf209067453700236e378f605c2e` retains old maps
and snapshots; audit `ad50acf920d78ee16033bdc1cf26c34e8275cdcb` changes only
the five Memory rows and their family. All311 unrelated package rows and
155 family decisions remain identical. Eight direct catalog tests,
zero-warning Release, canonical68 and live audit316/156/1 pass; hosted
CI42142102328 and normal producer publication remain pending.

FrontComposer's upstream CI identified four removed published members and
one stale measured analyzer seal. Repair `aa8578318971da04ce7d7bd07928d08c6b95bc1a`
preserves the signatures with explicit refusal of unscoped credentials,
retains session/tenant isolation and configured DI, and updates only the
actual identifier inventory and three justified instance-member CA1822
census entries. Seventy auth/compatibility and five governance controls,
three real APICompat-enabled package builds against unchanged4.7.2 and
two package-only consumers pass. Hosted CI42141933570 and Quality42141933127
remain required. Current Platform receipt-fix CI42139421515 has now passed
259/349 with downloaded artifacts verified and catalog42139420603 passes
all five rows. Its publication is combined with the measured latest Memory
producer adoption, avoiding an unnecessary intermediate Platform release.


### Qualified latest producer and final Platform candidate

Builds source `ad50acf920d78ee16033bdc1cf26c34e8275cdcb` passed full
CI42142102328. Normal release42144370420 completed successfully after the
existing production approval was submitted under the user's explicit
authorization. Genuine v4.44.6 resolves to that source. Both signed public
NuGets, catalog/blob, qualified inventory and paired AppHost/Tooling bodies
were independently verified; no tag assistance or gate exception was used.
FrontComposer `aa8578318971da04ce7d7bd07928d08c6b95bc1a` has all five
workflows and all eight Quality jobs successful. The actual three-case
GovernanceBuild TRX was independently verified. Existing advisory/performance
skips remain recorded as skips rather than execution credit.

Platform shared `cb4bc16b11a49ebb961c171857bf4344c234e807` selects measured
M25 artifacts and genuine446, preserves old M23/M24 maps and all three old
producer pairs, and adds the actual fourth pair with cross-pair refusals.
The combined21 source/config/test/doc files plus recorded Builds pointer
were committed after15 component,19 tuple and9 helper controls passed with
zero-warning Release. Current `e7e16f621365337e7d6f81c242c9e5c355b3556f`
and real historical `01e27acb14fde329dc427ed81128c1ad1b50da8f` are pushed;
exact eight-path source equivalence and unchanged349-profile hash pass.
Catalog42147545820 passed all five rows. Tuple42147545870 remains pending
259 current and349 historical execution; Platform publication is still held.

Root's two-file M25 capability regression passed an actual19-of20-before
failure followed by20-of20 (60 MSBuild evaluations) and27 actual REST cases
against signed Client.Rest25, all without skips and with zero-warning
Release. Three current fallback/default files passed2 fallback controls.
Known producer tool/delegates/tests and two coupled documentation pins
cover70 workflow controls (69 initially positive plus the corrected one),
with exact published446 version/source observed. All these patches remain
uncommitted for the final atomic root tuple and current source-observation
adoption. Independent release11a3/validation332b bytes are unchanged.
Primary Parties CI/native effects/cleanup, release and11-package publication
verification remain outstanding.


### Actual Platform publication and atomic root adoption

Platform v1.5.2 was genuinely published at2026-11-06T02:27:18Z, and both
API and local tag resolve to `e7e16f621365337e7d6f81c242c9e5c355b3556f`.
All259 current controls including both real readers, all349 original
historical controls and all five catalog rows passed. Both downloaded
artifact digests, result source/run/attempt, raw historical profile and
exact eight-path equivalence were independently verified before publication.

The final17-own-file root aggregate selects actual measured ten-source
identities, retains the complete prior matrix as dated history and appends
only five delegated source signoffs. Release build has zero warnings and
all18 source controls passed. Prior47 Memory,2 fallback and70 workflow
controls remain recorded; only the three committed-source cases require
the new atomic commit and clean owning layout. Genuine module446 was
restored and its full source identity observed. The original nine Mcp
closure edits are preserved for actual post-publication consumer adoption.
Root native effects/cleanup, exact-source full CI and range proof, primary
Parties release and independent eleven-package publication verification
remain pending at this atomic adoption checkpoint.

**2026-11-06 seed snapshot correction:** Root `ebb30f9252af1372d81f49f2e675fdc9f39e5895` passed all seven independent CI jobs, RC, commitlint and CodeQL; its isolated reports contain 6364 passed, zero failed and 14 configured NotExecuted cases. The required native startup failed HXR027 before either live case. Both hosted and exact-source local cleanup verified empty inventories, and local cleanup preserved all five ambient containers. Fresh reader hashes and loaded file identities matched; the actual later Host validation enriched `catalog-versions.json` after the parent proof sealed it. A real MSBuild regression reproduced proof invalidation and passed after preparing the seed consumer before proof issuance. No artifact, body, HMAC, snapshot or runtime gate was relaxed.

Genuine Platform v1.5.3 at `d1da347db92ef3272beda90005be5239878ff901` was published at 2026-11-06T03:53:36Z after exact-source tuple42157212231 passed all259 current controls (including both actual reader cases) and all349 original historical controls with zero skips. The genuine same-code historical source is `638070cbbb35bd9bd11caf5198a41bb7c511d780`; the original349 profile and exact eight-path comparison remain preserved. Catalog42157212251 passed all five compatibility rows. Independent downloaded artifact digests and raw report/source/run/attempt bindings are retained in `platform-seed-current-readers-verified.json`, `platform-seed-full-gate-verified.json` and `platform-seed-catalog-verified.json` under the private task evidence directory.

The root adoption changes only Platform selection/provenance, its delegated signoff and this task record. Builds4.44.6, SDK4.1.0, Tenants7.0.4, Memories3.0.25, McpCli2.0.1, independent release/validation pins, historical observations and all nine held McpCli edits remain preserved. Fresh root checks, clean-layout postcommit cases, full exact-source CI, both unchanged native cases, persisted effects and verified cleanup remain required before primary Parties publication. No native/public-consumer or candidate acceptance is inherited from Platform prerequisite tests.

**2026-11-06 native caller protocol correction:** Root `fb66dec0de39a15b348b7f4ec051af13e1fffdc0` again passed all seven independent CI jobs, RC, commitlint and CodeQL, plus 6364 isolated tests with zero failures and 14 configured skips. Native startup still failed before execution, with all owned cleanup inventories verified empty. Exact-source ordinary local run `9c7847b1aa57a33c55128f6acd87327b` confirmed the prior seed fix: the snapshot stayed stable and current-owner observation verified five owner and four SDK bodies. It then reached bootstrap submission and received HTTP503. Actual owned Dapr counters and mapped gateway ServiceDefaults4.1 bodies bind the failure to the SDK technical peer guard; the raw bootstrap caller omitted the required root, epoch and activation binding headers. Exact public SDK/Tenants source also requires declared management platform scopes, matching human and workload credentials, operation-specific permissions, explicit scoped status reads, and public tenant grants excluding reserved system storage.

The caller correction lives in Platform; management declarations still map to historical storage through the SDK, and its guards/deadlines remain intact. Builds owns the separate issuer correction: final native execution independently mints its token, and Tenants REST requires canonical claims without a legacy normalizer. The issuer preserves every legacy aggregate and emits the exact canonical tenant/domain/permission sets without filtering or added grants. The root native test adds its tenant only to each status GET. Component verification passed Platform15 plus26 capability evaluations and Builds18 issuer plus12 real SDK/Tenants REST boundary checks. Canonical REST scope reaches an always-refusing hosting guard; this is explicitly not HTTP200 or native acceptance. Three independent review lenses found one missing mixed-tenant guard regression; the added assertion fails when only that guard is removed and the restored15-case suite passes. Original259/349 profiles and all older cohort identities remain preserved.

Local cleanup stopped only the authenticated run: twelve owned containers became zero, all six resource inventories were verified empty, and all seven ambient containers present immediately before cleanup remained afterward, including the original five. Five additional launch-time ambient containers were already absent before any down command; no preservation of those absent containers is asserted. Evidence and exact source/proof/counter/cleanup receipts remain in the private task directory. Builds source `0b08666cb2aac1264007564f86ca9c52376499e4` is pushed with exact-source CI pending; Platform shared caller source `e43aa6b454d82221e0d627ee18747fe72d6658fc` and a preliminary historical branch are prepared. A genuine new producer publication, its measured pair in both Platform source trees, final descriptor/source comparison and fresh259/349/catalog/native gates remain required. No primary Parties release or published-consumer acceptance is claimed.


**2026-11-06 scoped caller tuple publication and adoption:** Builds v4.44.7 is genuinely published from `0b08666cb2aac1264007564f86ca9c52376499e4`; exact-source CI42166762850 and normal release42169350830 succeeded. Both signed public CLI archives, actual catalog/inventory and adjacent AppHost/Tooling bodies were independently verified. Platform v1.5.4 was genuinely published at2026-11-06T06:28:49Z from `13fc1a8978b4e6dcf8233b443c23c4e4258c49c7`. Tuple42172121935 attempt1 passed all259 current controls, both real source-bound readers and all349 original historical controls from `e25716f742dd505ebda4dfc9bbc10594d0aa3e61`, with zero skips. Catalog42172122108 attempt1 passed all five rows. Downloaded artifact digests, exact source/run/attempt, unchanged historical profile and exact eight-path source equivalence were independently verified before publication.

The atomic root update selects this actual published pair and adds tenant scope only to each native status GET. Thirteen current source/tool/workflow/documentation files are aligned; the prior matrix body/row and independent release/validation pins remain preserved. The private preparation helper refused before any writes on existing mixed architecture line endings and an overbroad dated-row uniqueness assertion; its correction preserves every architecture byte except the single SHA and checks only the actual current dated row. All70 workflow and18 non-HEAD source cases pass without skips; both relevant Release builds and the native test project compile with zero warnings/errors. All nine McpCli edits remain frozen. A fresh fetch found later Builds startup-diagnostic commit `26b9c1d192c844efe0e074cf91fc87afd1d43f4d`, reserved for the later genuine catalog producer rather than mislabeling published447. Clean-layout committed-source checks, full exact-source root CI, both native cases, persisted effects and cleanup remain required before primary Parties publication.


**2026-11-06 readiness throttling correction:** Root `dfcf84c856c9b48c6b21e57275d59dbae3c356c8` passed all70 workflow,18 non-HEAD source and3 clean-layout committed-source controls with zero-warning Release builds before push. Its exact CI42175216395 attempt1 passed all seven independent jobs plus RC42175215902, Commitlint42175216375 and CodeQL42175216621. All15 retained isolated TRX reports independently verify6378 total,6364 passed,zero failed and14 configured NotExecuted cases. Native startup alone failed HXR027; both hosted execution and cleanup retained six verified empty resource inventories. No native case, effect or publication acceptance follows.

Exact-source normal local run `a2e1b43e5bc88e9315f0539a37a5b4c9`, genuine module447 and unchanged600-second local policy reproduced a later failure at105.625seconds. The actual closed preparation progress is memories-active-observing/HTTP429. Its ordered production path already completed the bound bootstrap, tenant/member commands, native Tenants query and accepted Memories provisioning POST. The current poller requests every100ms and treats429 as terminal; published Memories25 uses an enabled inbound limiter with default120permits/60seconds and Retry-After. The correction belongs to Platform's caller: honor throttling under the existing deadline/cancellation, retain the real readiness predicate and all owner/auth guards, and never increase/disable the limiter or manufacture Active. A bounded existing-registry read did not authenticate effective Dapr DB/key-prefix coordinates, so eventual Active or provisioning failure remains unmeasured.

Exact local down completed successfully:12 labelled owned containers became zero and all six inventories were verified empty; the persistent observation session closed. All five launch-time ambient containers remain present. One additional unlabelled container present immediately before down was absent afterward; its ownership/cause was not measured, so its preservation is not claimed. Safe origin, source/run/attempt, progress/hash, MTP/workflow and cleanup receipts remain in the private task directory. Polling regression, independent review, genuine same-source259/349/catalog5 qualification, real Platform publication, root adoption and full native/persisted/cleanup acceptance remain required. The primary Parties release has not been dispatched.

**2026-11-06 reviewed polling candidate:** Shared Platform fix `c3cf901e4061fb0235bbc3555dd5a48cbe0a2468` and real historical source `e73be4fb11ac8c406b406e8d1d433444fa6996cb` contain the identical three reviewed production/test files. Final current source `d0689072da4fd80b37f9b6bde161edc07b4b2399` binds that actual historical commit. All are pushed and the guarded comparison verifies exactly the eight allowed metadata differences, with original current259 and historical349 profiles unchanged. Tuple42182274929 attempt1 and catalog42182274936 attempt1 are running. Builds remains genuine447/source0b; actual Platform publication, root adoption, MemoryActive/native effects/cleanup and primary Parties release remain pending. The historical worktree initially lacked its local commitlint executable; pinned lockfile tooling was installed and exact messages passed owning commitlint21.2.4 before its cherry-pick/push. No commit validation was bypassed.
