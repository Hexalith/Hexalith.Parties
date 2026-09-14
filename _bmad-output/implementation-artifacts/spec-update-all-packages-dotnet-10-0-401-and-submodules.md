---
title: 'Update Parties packages, SDK, and root submodules'
type: 'chore'
created: '2026-09-13'
status: 'in-progress'
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
- Advanced the Builds catalog to EventStore `3.104.0`, CommunityToolkit Aspire Dapr hosting `13.5.1-beta.752`, and xUnit packages `4.0.1`; retained runner `4.0.0`. Catalog and audit commits are `2c931365ffb4cb8e10a32bbcefd161fb470d08f6` and `2e2220b9e450b1ec1095594005e86cc565b8f7ee`.
- Advanced verified EventStore and Memories heads to `dfc0ac557c43363159b55bffb4d40feceab1f787` and `d99bc96371afbf55f8f37cd812c9e6cedba15b1d`; advanced the Builds gitlink to its authorized local commits. Reconciled the live signoff ledger, release pin, test identities, and maintained documentation without rewriting historical receipts.
- All eight submodules are clean, detached, and have no initialized nested submodules. Seven match their re-queried remote default heads. Builds is intentionally local-ahead of `origin/main` until a separately authorized push makes the new catalog commits remotely reachable; this implementation did not push.

## Spec Change Log

- 2026-09-13: Implemented package, SDK, catalog, root-gitlink, identity-evidence, and validation updates.

## Review Triage Log

## Verification

**Commands:**
- Both Builds catalog/consumer-authority scripts -- expected: valid catalog and no consumer versions.
- `dotnet restore Hexalith.Parties.slnx && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1` -- expected: zero warnings/errors under 10.0.401.
- `scripts/test.ps1` Release lanes `unit`, `integration`, and `ci` -- expected: all applicable projects pass; report topology separately.
- Root and E2E `npm ci`, `npm audit`, `npm outdated --json`, plus E2E typecheck -- expected: clean, current graph.
- Warning-override and gitlink RC gates plus `git diff --check` -- expected: policy, root gitlinks, nesting, and formatting pass.

**Results:**

- Builds catalog, audit, Dapr-family, exception, and Parties consumer-authority validators passed. Both Builds commits and their range passed the pinned commitlint configuration.
- A serialized restore under SDK `10.0.401` completed in 8.8 seconds. The serialized Release build completed with zero warnings and zero errors.
- Direct no-restore execution passed 2,513 tests: 1,815 unit, 58 sample, 57 CI, and 583 integration tests. Restore-enabled lane wrappers stalled during implicit restore amid shared MSBuild processes and were interrupted without a product failure; the equivalent restored, built, and directly executed assemblies all passed. The topology lane was not run.
- Root and E2E npm clean installs, audits, and outdated checks passed; E2E typecheck passed. `npm outdated --json` returned an empty object for both graphs.
- Warning-override, root gitlink RC, and diff-format checks passed. Final submodule inspection confirmed clean detached checkouts with no initialized nested submodules.
