---
title: 'Update Parties packages, SDK, and root submodules'
type: 'chore'
created: '2026-09-13'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'bfc15cc15b6556c5f97ae3d06bdefdd809d3b666'
context:
  - '{project-root}/docs/development-guide.md'
  - '{project-root}/docs/build-gate.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties must target .NET SDK 10.0.401 and refresh its root-owned dependencies and root-declared submodules. Maintained documentation, tests, release pins, and source identities must agree.

**Approach:** Re-query registries and all eight root-submodule remotes, update root-owned npm and SDK pins, move only gitlinks whose verified default-branch heads changed, and consume NuGet only through Hexalith.Builds. Reconcile live identities and narrow compatibility regressions.

**Decision:** Cross-repository Builds work is authorized. Update and validate the relevant central catalog selections, create the required commit in `Hexalith.Builds`, then advance the Parties Builds gitlink to that commit.

## Boundaries & Constraints

**Always:** Pin `global.json` to `10.0.401` with `rollForward: latestPatch`; use latest stable releases or each dependency's existing preview channel; keep the Builds catalog as sole NuGet authority; update manifests and locks together; touch only root `.gitmodules` paths; verify exact default-branch SHAs; keep submodules clean and detached; preserve package-mode CI and historical receipts.

**Never:** Initialize nested submodules; use recursive or `--remote` updates; add consumer NuGet versions; edit or commit any repository other than the explicitly authorized Builds catalog change; rewrite historical evidence; deploy, publish, push, clean, or overwrite unrelated work.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Package drift | A root/E2E npm or catalog package is older | Manifest, lock, catalog decision, and exact tests agree | Stop on install/audit failure; report catalog-owned drift; never hand-edit lock integrity |
| Submodule drift | A gitlink differs from verified default-branch head | Fetch and checkout exact head; update live signoff/identity evidence | Stop on dirty worktree, unverifiable head, changed default branch, or nested initialization |
| Compatibility regression | Refreshed graph fails validation | Make the narrowest Parties-owned fix | Do not patch unrelated submodule source or weaken gates |

</frozen-after-approval>

## Code Map

- `global.json`; six maintained `docs/*.md`; `DocumentationFitnessTests.cs` -- SDK pin and `10.0.400` assertions to update.
- `package.json`, `package-lock.json`, `tests/e2e/package.json`, `tests/e2e/package-lock.json` -- root-owned npm graphs; only E2E `@types/node` currently drifts (`26.5.0` to `26.5.1`).
- `PartiesContainerPublishWorkflowTests.cs` -- exact npm manifest/lock theory and Builds release SHA.
- `Directory.Packages.props`, `references/Hexalith.Builds/Props/Directory.Packages.props` -- version-free wrapper and sole catalog. Builds `main` trails EventStore `3.104.0`, Dapr hosting beta `.752`, and xUnit `4.0.1` by one release.
- `.config/dotnet-tools.json`, `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` -- verify-only pins; aspirate `9.1.0` and Aspire `13.5.3` are current.
- `.gitmodules`, `references/*`, `.gitlink-signoff.tsv`, `scripts/gitlink-rc-gate.sh` -- permitted gitlinks, ledger, and validation. All eight currently equal clean `origin/main`; no nested submodule is initialized.
- `.github/workflows/release.yml`, `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs`, `docs/architecture.md`, `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- live Builds/source SHA and package identities; several are stale relative to the already-selected root gitlinks.

## Tasks & Acceptance

**Execution:**
- [x] SDK pin, six maintained docs, and `DocumentationFitnessTests.cs` -- use `10.0.401` consistently.
- [x] Root/E2E npm manifests, locks, and CI theory -- install registry-latest direct dependencies.
- [x] NuGet graph -- update and commit relevant Builds catalog selections, then consume them through the advanced gitlink without overrides.
- [x] `references/*`, ledger, workflow/tests, and maintained identity evidence -- re-query remotes, advance changed heads, and reconcile live SHAs without altering historical receipts.
- [x] Parties source/tests and validation -- fix only refresh regressions; run catalog, package, test, npm, documentation, and gitlink gates.

**Acceptance Criteria:**
- Given installed SDKs, when SDK selection is evaluated, then `10.0.401` is selected and every maintained assertion agrees.
- Given current registries, when inventory is rerun, then root npm and Parties-consumed NuGet dependencies resolve latest approved-channel releases, with NuGet coming only from Builds.
- Given root `.gitmodules`, when gitlinks are audited, then seven equal verified remote default-branch heads, Builds equals its authorized validated local successor, all are clean, and none contains an initialized nested submodule.
- Given the refreshed graph, when gates run, then restore, serialized Release build, relevant test lanes, npm checks, catalog authority, and gitlink validation pass without weakening checks.

## Implementation Notes

- Selected .NET SDK `10.0.401` with `latestPatch`; refreshed maintained SDK documentation and its fitness assertions.
- Updated the E2E `@types/node` manifest and lock entry from `26.5.0` to `26.5.1`. The root npm graph was already current.
- Advanced the Builds catalog to EventStore `3.104.0`, CommunityToolkit Aspire Dapr hosting `13.5.1-beta.752`, xUnit packages `4.0.1`, and bUnit `2.11.3`; retained runner `4.0.0`. Catalog/audit commits are `2c931365ffb4cb8e10a32bbcefd161fb470d08f6`, `2e2220b9e450b1ec1095594005e86cc565b8f7ee`, `3eac4a86b68d2a92ef926aae4fe53d896c241be6`, and final `aee01323579adeda952bf41b2f3e0e61785075c6`.
- Advanced verified EventStore and Memories heads to `dfc0ac557c43363159b55bffb4d40feceab1f787` and `d99bc96371afbf55f8f37cd812c9e6cedba15b1d`; advanced the Builds gitlink to `aee01323579adeda952bf41b2f3e0e61785075c6`. Reconciled the live signoff ledger, release pin, test identities, and maintained documentation without rewriting historical receipts.
- All eight submodules are clean, detached, and have no initialized nested submodules. Seven match their re-queried remote default heads. Builds is intentionally local-ahead of `origin/main` until a separately authorized push makes the new catalog commits remotely reachable; this implementation did not push.
- Review remediation added blocking Aspire CI, a non-skipping AppHost construction proof, evaluated Memories package-mode coverage, persistent detached/nesting checks, complete SDK/package documentation fitness, and current root-gitlink receipts.

## Spec Change Log

- 2026-09-13: Implemented package, SDK, catalog, root-gitlink, identity-evidence, and validation updates.
- 2026-09-13: Review found the incremental audit had missed bUnit `2.11.3`; refreshed that family and added the focused documentation, package-graph, gitlink-state, and topology safeguards recorded below.

## Review Triage Log

| Finding | Verdict / route | Evidence |
| --- | --- | --- |
| BH-01 Builds workflow pin is not remotely reachable | high / rejected (intent-excluded publication) | `git ls-remote` does not advertise `2e2220b9`; however the frozen contract forbids push and the parent remains uncommitted, so the required dependency-order handoff is to push Builds before any later parent commit/push rather than alter this local implementation. |
| BH-02 current Commons, FrontComposer, and Tenants receipts are absent from the signoff ledger | medium / patch | `scripts/gitlink-rc-gate.sh --diff v1.1.1` rejects all three current `HEAD` gitlinks; the all-root-submodule reconciliation exposed those missing current receipts, and adding the already verified identities is direct. |
| BH-03 Code Map says all eight submodules equal `origin/main` while Results says Builds is ahead | false / rejected | Code Map records the pre-implementation inventory; Implementation Notes explicitly record the post-implementation local Builds successor and the reason it is not yet remote. |
| BH-04 `latestPatch` permits a later 10.0.4xx SDK | false / rejected | The approved contract explicitly requires `10.0.401` together with `rollForward: latestPatch`; `dotnet --version` selects `10.0.401` here, and the servicing roll-forward is intentional rather than an exact-SDK promise. |
| BH-05 `docs/development-guide.md` is omitted from the SDK documentation fitness list | low / patch | The file is one of the six maintained SDK documents changed here, but `CodeMapDocumentsThePinnedSdkVersion` currently covers only five; adding it is a direct assertion. |
| BH-06 the incremental catalog audit does not prove all consumed packages are current | high / patch | A fresh complete 141-family/286-package NuGet audit on 2026-09-13 found consumed bUnit `2.10.3` behind stable `2.11.3`; the catalog, audit, dependent docs, and focused bUnit consumers must advance. |
| BH-07 the Root submodules inventory omits `Hexalith.AI.Tools` | low / patch | `.gitmodules` declares eight root paths while `docs/component-inventory.md` names only seven; adding the tooling submodule completes the task's root inventory without changing runtime claims. |
| BH-08 Node 26 type declarations are paired with a Node 24 runtime floor | medium / defer | The mismatch is real but predates this patch (`@types/node` was already 26.5.0); choosing a Node 24 type line or raising the runtime floor requires a separate compatibility decision. |
| BH-09 Memories package identity lacks an evaluated package-mode consumer graph | medium / patch | The changed receipt is asserted only as catalog text while real Parties projects directly consume `Hexalith.Memories.*`; extend the existing evaluated-graph test to prove the default package branch resolves `2.27.1` without source references. |
| BH-10 no Playwright run proves the FrontComposer source graph | false / rejected | The baseline and candidate both select FrontComposer `b0ad2fb6`; this patch only repairs stale identity documentation, so it introduces no new FrontComposer browser compatibility surface. |
| BH-11 refreshed documented package values lack fitness assertions | medium / patch | Maintained architecture/project tables now carry Dapr, JWT/CustomElements, resilience, MinVer, bUnit, and Testcontainers values not all covered by a documentation-to-catalog check; add exact maintained-stack assertions. |
| EC-01 release workflow references the local-only Builds commit | high / rejected (intent-excluded publication) | Same verified condition as BH-01: the ref is intentionally local under the no-push contract and must be published first in a later authorized dependency-order operation. |
| EC-02 fresh-clone initialization cannot fetch the local-only Builds gitlink | high / rejected (intent-excluded publication) | Same root cause as BH-01: no parent commit containing that gitlink exists yet, and publishing the Builds dependency is explicitly outside this run. |
| EC-03 development guide can drift from the SDK pin undetected | low / patch | Same confirmed omission as BH-05; add `docs/development-guide.md` to the maintained document list. |
| EC-04 an attached submodule branch can pass the immutable checkout assertion | low / patch | `AssertGitlinkAndCheckout` checks index SHA, checkout SHA, and cleanliness but not `branch --show-current`, despite the approved detached-checkout invariant. |
| EC-05 a clean initialized nested submodule can pass the immutable checkout assertion | low / patch | Neither `AssertGitlinkAndCheckout` nor another fitness test rejects nested initialized entries, despite the approved no-nested-initialization invariant. |
| EC-06 the historical payload row still calls superseded identities retained | medium / patch | The current table selects EventStore `dfc0ac55` / package `3.104.0` / Builds `2e2220b9`, while the historical row still uses present-tense “retained identities” for `d45206f7` / `3.103.0` / `7b0b1837`; append a supersession clarification without rewriting the receipt. |
| EC-07 runtime-toolchain evidence validator expects an obsolete Dapr-hosting preview | medium / defer | The validator/schema expected `13.5.0-preview.1.260825-0345` while the pre-change catalog already selected beta `.751`; this qualification-packet drift predates `.752` and requires coordinated evidence regeneration rather than a catalog-refresh edit. |
| VG-01 Dapr sidecar compatibility lacks a blocking execution | high / patch | Pre-verified: the relevant `DaprMtlsBootstrapTests` live only in the advisory Aspire project and the recorded topology lane was omitted; make the Aspire lane blocking and execute the focused sidecar test. |
| VG-02 EventStore AppHost construction exceptions become skips | high / patch | Pre-verified: both model tests catch every `Exception` and call `Assert.Skip`, while the changed EventStore Aspire package is used by `AddHexalithEventStoreSecurity`; add a blocking construction assertion and execute it. |
| VG-03 Builds remote reachability is absent | high / rejected (intent-excluded publication) | Same verified condition as BH-01/EC-01/EC-02; it is a later publish-order prerequisite explicitly excluded by the frozen no-push boundary. |

## Verification

**Commands:**
- Both Builds catalog/consumer-authority scripts -- expected: valid catalog and no consumer versions.
- `dotnet restore Hexalith.Parties.slnx && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1` -- expected: zero warnings/errors under 10.0.401.
- `scripts/test.ps1` Release lanes `unit`, `integration`, and `ci` -- expected: all applicable projects pass; report topology separately.
- Root and E2E `npm ci`, `npm audit`, `npm outdated --json`, plus E2E typecheck -- expected: clean, current graph.
- Warning-override and gitlink RC gates plus `git diff --check` -- expected: policy, root gitlinks, nesting, and formatting pass.

**Results:**

- Builds catalog, checked-in audit, Dapr-family, exception, and Parties consumer-authority validators passed. A final complete live audit queried 286 packages across 141 families with zero preserved families and passed validation; the newer unselected candidates are explicit retained compatibility decisions rather than accepted Parties updates. All four Builds commits and their range passed the pinned commitlint configuration.
- The final serialized restore and Release build ran under SDK `10.0.401`; all 59 projects built with zero warnings and zero errors.
- Final direct no-build/no-restore execution covered every configured test project: 2,557 total, 2,551 passed, zero failed, and six explicitly deferred topology cases skipped. This comprises 1,815 unit, 585 Parties integration, 58 sample, 42 topology (36 passed / 6 skipped), and 57 CI tests. The restore-enabled lane wrapper again stalled only in redundant implicit restore and was interrupted; its already-restored direct equivalents all passed.
- Root and E2E npm clean installs, audits, and outdated checks passed; E2E typecheck passed. `npm outdated --json` returned an empty object for both graphs.
- Warning-override, working-tree gitlink RC, candidate-tree release-ledger, and diff-format checks passed. Fresh remote inspection confirmed seven clean detached root submodules at remote default heads and Builds as a clean detached local successor, with no initialized nested submodules.
