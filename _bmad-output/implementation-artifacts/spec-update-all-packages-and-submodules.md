---
title: 'Update all Parties packages and root submodules'
type: 'chore'
created: '2026-09-06'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '4710620616cab7174d2763dd6e22170a7f4ef773'
context:
  - '{project-root}/docs/development-guide.md'
  - '{project-root}/docs/build-gate.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties' root JavaScript dependencies, centrally selected NuGet graph, and five root-declared submodule pointers lag their current upstream releases or default-branch heads. Stale source identities are also embedded in release and fitness checks.

**Approach:** Advance direct npm dependencies to their registry `latest` releases, adopt the newest authoritative `Hexalith.Builds` catalog, and move every root-declared submodule to its verified default-branch head. Reconcile only live identity assertions and repair compatibility regressions exposed by restore, build, and focused tests.

## Boundaries & Constraints

**Always:** Re-query npm, NuGet, and every root submodule remote immediately before editing. Treat the latest `Hexalith.Builds` catalog as the sole NuGet authority, including its deliberate prerelease selections; package versions inside referenced repositories remain owned by those repositories. Update only the eight paths declared by the root `.gitmodules`, add owner-backed `validated-advance` ledger rows for changed gitlinks, keep package-mode CI/release behavior, preserve unrelated dirty work, and update current identity pins without rewriting historical receipts. Use `jpiquot`, the authenticated GitHub owner and existing ledger identity, for this requested advance.

**Never:** Initialize or update nested submodules; use recursive or `--remote` submodule commands; add inline/override NuGet versions; edit or commit source inside a submodule; downgrade a required v5/compatible prerelease merely because an older stable package exists; update unrelated GitHub Action pins, deploy, publish, commit, push, clean, or overwrite pre-existing changes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Registry package update | Direct npm dependency has a newer `latest` release, including a new major | Manifest range and lockfile resolve that release | Stop on install/audit failure; do not hand-edit lock integrity data |
| Central NuGet update | Latest Builds catalog differs from the current gitlink | Parties evaluates exactly the new catalog; no consumer overrides are added | Report any registry version newer than the authoritative catalog as catalog-owned drift |
| Submodule advance | Root remote HEAD differs from the recorded gitlink | Explicit fetch and detached checkout at the verified SHA, with no nested initialization | Stop if a submodule is dirty, its default branch changes unexpectedly, or remote HEAD cannot be verified |
| Compatibility regression | Updated graph fails compile or focused tests | Make the narrowest Parties-owned compatibility fix | Do not patch submodule source or weaken warnings/tests |

</frozen-after-approval>

## Code Map

- `Directory.Packages.props` and `references/Hexalith.Builds/Props/Directory.Packages.props` -- version-free consumer wrapper and sole NuGet catalog; Builds `8db7459d... -> 6daad3d5...` updates Memories `2.25.0 -> 2.26.1`, Tenants `5.6.0 -> 5.7.0`, and Parties `1.0.0 -> 1.1.1` in the planning snapshot.
- `package.json`, `package-lock.json` -- release-tool dependencies; registry scan found Commitlint `21.2.2`, semantic-release `25.0.9`, and GitHub plugin `12.0.9` updates.
- `tests/e2e/package.json`, `tests/e2e/package-lock.json` -- Playwright toolchain; scan found axe `4.13.0`, Playwright `1.63.0`, Node types `26.4.1`, and TypeScript `7.0.2`.
- `.config/dotnet-tools.json`, `global.json`, `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` -- verify-only tool/SDK pins; aspirate `9.1.0`, .NET SDK `10.0.400`, and Aspire `13.5.3` were already current.
- `.gitmodules`, `references/*` -- eight permitted root gitlinks. Planning remote heads require EventStore `910fda6a...`, Memories `115e2839...`, FrontComposer `f0c3b6fd...`, Tenants `f75cdacc...`, and Builds `6daad3d5...`; AI.Tools, Commons, and PolymorphicSerializations were current.
- `.gitlink-signoff.tsv`, `scripts/gitlink-rc-gate.sh` -- owner authorization for every changed gitlink and non-recursive RC validation.
- `.github/workflows/release.yml`, `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs` -- two Builds execution SHA pins and their contract assertion must track the adopted catalog.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs`, `docs/architecture.md`, `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- live source/package identity assertions; merge around existing user changes and leave historic evidence intact.

## Tasks & Acceptance

**Execution:**
- [x] Root npm manifests/locks -- install every direct dependency at registry `latest`, then run clean-install and audit checks.
- [x] `references/*`, `.gitlink-signoff.tsv` -- advance only verified root gitlinks and record each changed SHA without touching submodule worktrees beyond checkout.
- [x] Builds SHA pins and live identity records -- reconcile release, tests, docs, and active evidence to the selected catalog/source graph without disturbing unrelated edits.
- [x] Parties-owned source/tests -- apply only compatibility fixes required by the new graph.
- [x] Validation -- run catalog/consumer-authority gates, package-mode restore and serialized Release build, unit/integration/CI lanes, npm typecheck, and gitlink/nested-submodule guards.

**Acceptance Criteria:**
- Given current upstream state, when dependency inventory is rerun, then every root-owned direct npm dependency resolves its `latest` release and every NuGet reference evaluates from the latest Builds catalog with no consumer override.
- Given root `.gitmodules`, when gitlinks are inspected, then all eight equal their verified default-branch heads, are clean, and have no initialized nested submodules.
- Given the updated graph, when repository gates run, then restore, Release build, applicable focused lanes, npm clean installs/typecheck, catalog authority, and gitlink validation succeed without weakened checks.
- Given the pre-existing dirty tree, when the final diff is reviewed, then unrelated user changes remain present and no commit, push, deployment, or publication occurred.

## Implementation Notes

- Updated all 11 direct npm development dependencies to their 2026-09-07 registry `latest` versions and regenerated both lockfiles through npm. The first root audit exposed six high-severity transitive advisories; `npm audit fix` refreshed supported transitive resolutions without manifest overrides, after which clean install and audit passed with zero vulnerabilities.
- Advanced EventStore to `3c6a5e33f9fbaf8469047ba3de72f70ab4425e66`, Memories to `7e9c2c387ee84b4d5c96c27f4cf613e13ff2c9e2`, FrontComposer to `f0c3b6fd7dbf0a750170b5ec72d09d17febb8f6a`, Tenants to `f75cdacc8eca458778c7109fd3f713f8907bed02`, and Builds to `6daad3d501e97204eba66d971bba6a7103b85ccd`. AI.Tools, Commons, and PolymorphicSerializations already matched their verified `main` heads. All eight checkouts are detached, clean, index-aligned, and contain no initialized nested submodule.
- Adopted Builds as the unchanged sole NuGet authority. Its current catalog selects Memories `2.26.1`, Tenants `5.7.0`, and Parties `1.1.1`; nuget.org's Memories `2.26.2` is recorded as catalog-owned drift and no consumer override was added.
- Reconciled the exact Builds release-workflow SHA, checkout fitness constants for all eight root submodules, current architecture/evidence identities, and five `jpiquot`-owned `validated-advance` ledger rows. Historical validation receipts and prior signoff rows remain intact.
- Made pre-commit identity fitness read the exact selected parent index gitlink instead of committed `HEAD`, while retaining independent exact-checkout and clean-worktree assertions. Added an 11-case CI theory covering every direct npm manifest range and lockfile resolution.

## Spec Change Log

- 2026-09-07: Implemented the current registry/catalog/default-branch refresh and recorded green package, build, test, and gitlink evidence.
- 2026-09-07: Triaged all 22 independent review findings, applied the current-graph documentation, receipt, closure-fitness, ledger, and test-name corrections, and recorded five pre-existing follow-up groups without widening the approved implementation.

## Review Triage Log

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| B1 | false | The accepted-wait evidence remains explicit in the `ExpectedDeferrals` set and the ledger section provenance; removing the invalid packed `status: accepted` field does not erase acceptance. |
| B2 | medium / patch | DW-108 is still open and prescribes old package versions and exact-tag resets that conflict with this approved default-branch-head update. Supersede it with the new selected graph. |
| B3 | false | DW-99 and sprint status date `5cbc5583...` as the historical 2026-08-18/19 shell-slice adoption; neither presents it as the current root identity. |
| B4 | high / patch | The authoritative Playwright receipt still claims the old FrontComposer SHA matches the current reconciliation table. Rerun the lane at `f0c3b6fd...`, update the receipt, and bind closure fitness to that SHA. |
| B5 | medium / patch | `docs/architecture.md` retains an active Aspire `13.4.6` statement while the selected SDK is `13.5.3`; reconcile the maintained version prose. |
| B6 | medium / patch | `docs/index.md` retains the same stale active Aspire version; reconcile it with the selected SDK. |
| B7 | medium / patch | `docs/project-overview.md` has stale active Aspire, Fluent UI, and Memories version prose; reconcile it with the selected graph. |
| B8 | medium / patch | `docs/component-inventory.md` has the same stale active versions; reconcile it with the selected graph. |
| B9 | medium / defer | README's SDK `10.0.302` statement predates this dependency task and is outside the implementation's touched documentation set; record it as follow-up instead of widening this change. |
| B10 | false | The cited lists describe build/runtime baselines rather than every root-declared maintenance submodule; AI.Tools supplies repository instructions and Memories is separately identified as optional. |
| B11 | medium / patch | The CI theory validates selected manifest and lock versions, not live registry state. Rename it to describe the reproducible assertion; registry and audit evidence remains in this spec. |
| B12 | medium / patch | With the changed gitlinks staged, `gitlink-rc-gate.sh --worktree` does not exercise ledger matching. Temporarily unstage only those five paths, run the worktree gate, then restore the exact staged selection. |
| B13 | medium / defer | The source-mode static-asset test only proves the Blazor framework asset and predates this update; record explicit FrontComposer RCL asset coverage as follow-up. |
| B14 | medium / defer | The UI test's NuGet cache lookup ignores MSBuild/global-package-path configuration and mishandles an empty `NUGET_PACKAGES`; record portable resolution as follow-up. |
| B15 | false | `type: chore` is spec metadata, not a proposed Git commit subject. The no-`chore` policy applies to commit messages; this task creates no commit. |
| E1 | medium / defer | The closure parser does not consult first-class ledger status, so a resolved entry whose inline reason remains could stay in the accepted-wait set. Record status-aware retirement as follow-up. |
| E2 | medium / defer | An empty `NUGET_PACKAGES` value is a concrete case of B14's package-root portability defect and shares that follow-up. |
| E3 | medium / defer | `reuseExistingServer` can attach to a wrong existing host in local `Test` mode; the configuration predates this dependency update, so record deterministic server ownership as follow-up. |
| E4 | false | The selected parent index is the exact proposed gitlink tree before commit, while separate assertions require the checkout to equal it and remain clean; CI's index and HEAD are identical after commit. |
| E5 | medium / patch | This duplicates B5's active Aspire version mismatch and shares the documentation reconciliation patch. |
| V1 | high / patch | The closure receipt is green at the superseded FrontComposer SHA. Execute the current accessibility lane and require the authoritative receipt to contain the selected SHA. |
| V2 | medium / defer | Current checks only inspect the `reuseExistingServer` expression and would not catch a semantically wrong environment matrix. Record an executable configuration-matrix test as follow-up. |

## Verification

**Results:**
- `npm ci && npm audit` -- passed; 500 packages audited, 0 vulnerabilities.
- `npm --prefix tests/e2e ci && npm --prefix tests/e2e run typecheck && npm --prefix tests/e2e audit` -- passed under TypeScript `7.0.2`; 0 vulnerabilities.
- `npm outdated --json` in both workspaces -- passed with `{}`; all direct dependencies still match their registry-selected versions.
- `dotnet package list --project Hexalith.Parties.Standalone.slnx --outdated --no-restore --format json` -- only Hexalith drift is Memories `2.26.1 -> 2.26.2`; catalog authority intentionally retained. Keycloak/Kubernetes prereleases report no stable latest result.
- Central catalog and consumer-authority validators -- passed for 286 catalog entries and 30 consumer projects.
- `dotnet restore Hexalith.Parties.slnx` and serialized Release build -- passed; 0 warnings, 0 errors.
- Unit lane -- passed 1,752 tests across 11 projects. Integration lane -- passed 560 Parties tests plus 58 sample tests. CI lane -- passed 57 tests.
- Playwright accessibility -- passed 6/6 under Playwright `1.63.0` at FrontComposer `f0c3b6fd7dbf0a750170b5ec72d09d17febb8f6a`; closure fitness now requires that exact SHA in the authoritative receipt.
- Warning/nested-submodule guard and gitlink RC gate -- passed. The gitlink gate was re-run with exactly the five changed gitlinks temporarily unstaged and reported five `DRIFT ok — validated-advance` results before their original staging state was restored.
- Final live remote audit verified all eight selected/index/checkout SHAs equal their `origin` default-branch heads; every root checkout is clean and every nested submodule remains uninitialized.
- Scoped `git diff --check` passed for all paths changed by this implementation. The repository-wide command still reports pre-existing CRLF lines in unrelated dirty UI-host/test edits; those user changes were preserved.
- Independent review -- 22 findings triaged: 10 patch findings resolved, 7 findings consolidated into five pre-existing deferred-work entries, and 5 false findings rejected with evidence. No high-severity finding remains open in this update.

**Commands:**
- `npm ci && npm audit` and `npm --prefix tests/e2e ci && npm --prefix tests/e2e run typecheck && npm --prefix tests/e2e audit` -- expected: clean installs, no vulnerabilities, successful TypeScript 7 check.
- `npm outdated --json` in the root and `tests/e2e` workspaces -- expected: `{}`.
- `pwsh -NoProfile -File references/Hexalith.Builds/Tools/validate-central-package-versions.ps1 references/Hexalith.Builds/Props/Directory.Packages.props` and consumer-authority validation -- expected: pass.
- `dotnet restore Hexalith.Parties.slnx && dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1` -- expected: zero errors/warnings.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane unit -Configuration Release`, then `integration` and `ci` -- expected: all applicable projects pass.
- `npm --prefix tests/e2e run test:a11y` -- expected: all six accessibility scenarios pass at the selected FrontComposer source identity.
- `bash scripts/check-no-warning-override.sh` and `bash scripts/gitlink-rc-gate.sh --worktree` with the changed gitlinks unstaged -- expected: both guards pass and every changed gitlink reports owner-backed `DRIFT ok`.
