---
title: 'Revalidate all tests and fix current failures'
type: 'bugfix'
created: '2026-07-12'
status: 'in-progress'
review_loop_iteration: 0
baseline_revision: '8d28a1bc7fe5faebb09bf9cc495fa671346140f5'
baseline_commit: '8d28a1bc7fe5faebb09bf9cc495fa671346140f5'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The last complete test evidence predates dependency, build-workflow, and package-routing changes. All current .NET and browser tests must be rerun, and root-owned regressions must be repaired so failures are not hidden by stale results, zero-test runs, or environment skips.

**Approach:** Establish fresh package-mode and source-reference baselines, run every configured .NET test project plus the full Playwright workspace, fix failures with focused reruns, and finish with broad validation and explicit evidence for any genuine environment blocker.

## Boundaries & Constraints

**Always:** Preserve warnings-as-errors and Microsoft.Testing.Platform semantics; run `.slnx` only for restore/build and test projects individually; inspect TRX and Playwright results for executed, failed, and skipped counts; keep package and source-reference evidence separate; treat the five advanced submodule checkouts as user-owned, read-only baseline state.

**Ask First:** Editing any `references/Hexalith.*` content or gitlink, changing a public package contract, changing dependency versions or source/package strategy, weakening a build/test/coverage gate, or accepting a previously unexpected skip as intentional.

**Never:** Do not initialize nested submodules, use legacy `.sln`, use project-level `--filter` under MTP, exclude failing tests, count a zero-test run as passing, suppress warnings globally, or modify tests solely to bypass Docker, DAPR, browser, network, or localhost limitations.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Green repository | All 15 .NET projects and browser prerequisites are available | Every configured test executes and passes; expected skips are enumerated | Record fresh per-project and browser evidence |
| Root-owned regression | A root source, test, script, or configuration defect fails a lane | Apply the smallest architecture-conformant fix and rerun focused then broad tests | Stop if the fix crosses an Ask First boundary |
| Dependency-mode drift | Package and source-reference modes differ | Diagnose the exact API/version/routing difference without conflating modes | Fix root compatibility only; report dependency-owned failures |
| Environment blocker | Docker, DAPR, Chromium, network, or port binding is unavailable | Continue all independent checks and preserve the exact blocked command | Do not mark the blocked lane green or weaken it |

</frozen-after-approval>

## Code Map

- `scripts/test.ps1` -- authoritative local inventory and lane runner for all 15 .NET test projects.
- `.github/workflows/ci.yml` and `references/Hexalith.Builds/.github/workflows/domain-ci.yml` -- active CI caller and read-only shared-workflow baseline; the retired `.github/workflows/test.yml` must not be referenced.
- `tests/Hexalith.Parties.*.Tests/**` and `tests/Hexalith.Parties.Tests/**` -- focused unit, package, component, integration, topology, and CI regression surfaces.
- `tests/e2e/package.json`, `tests/e2e/playwright.config.ts`, and `tests/e2e/specs/**` -- separate full browser test workspace excluded from `scripts/test.ps1 -Lane all`.
- `_bmad-output/implementation-artifacts/tests/test-summary.md` -- append-only consolidated verification evidence.

## Tasks & Acceptance

**Execution:**
- [~] `Hexalith.Parties.slnx`, `scripts/test.ps1`, and all 15 test projects -- fresh Release package-mode and current-catalog Debug source-reference restore/build pass with zero warnings/errors. Every project executes; latest reconciled results in each mode are **3,031 total, 3,025 executed, 3,024 passed, one gateway failure, six predeclared skips**. Live gateway acceptance remains pending; no unexpected skip is accepted.
- [x] Root-owned source/test/documentation defects -- repaired centralized claim/encryption assertions, Test-only UI context and picker composition, Fluent control semantics, search/empty recovery, mobile/focus behavior, accepted-command state, export/download cleanup, erasure modal/warning and scoped interop lifecycle. Current catalog/documentation receipts align with the existing graph; historical receipts remain dated. Focused checks and corresponding full projects pass in both modes.
- [x] `tests/e2e/**` -- locked dependencies and typecheck pass; the automatic source-mode Release host builds with `BuildInParallel=false`; **90/90 Playwright tests execute/pass, zero failed/skipped**, including configured specimen axe and authorization/privacy gates. Awaited refreshed-row focus, export cleanup, picker disposal and Escape diagnostics pass.
- [x] `_bmad-output/implementation-artifacts/tests/test-summary.md` -- October commands, per-project counts, source identities, working-tree observations, fixes, declared skips, failed baseline/recovery attempts and exact current blocker appended without changing earlier evidence.

**Acceptance Criteria:**
- Given the current workspace and all available prerequisites, when both 15-project .NET baselines and the full Playwright workspace run, then every executed test passes and every non-executed test has an exact, verified reason.
- Given a root-owned failure, when its focused test passes after repair, then the corresponding broad lane also passes without warning, inventory, runner, or architecture-gate suppression.
- Given package/source or environment divergence, when completion is reported, then runnable checks remain green and each unresolved external blocker identifies the exact command, dependency or prerequisite, and observed failure.

**Current acceptance — 2026-10-08:** Partial. Browser and all fourteen independent .NET projects pass. The gateway test remains failed after both CI-strict baseline and existing mTLS prerequisite recovery. Aspire's Tenants child `dotnet run` restores its own `Hexalith.EventStore.DomainService/3.115.0` graph and fails CS0246 for `IDomainServiceAdministratorVerifier` / `DomainServiceAdministratorClaim`, while the root's externally supplied `3.117.0` graph builds cleanly. Parties `/health` stays 503; under mTLS the Tenants readiness probe times out after two seconds. Editing reference content/versions/routing requires owner authorization; none was changed in this pass. Status remains `in-progress`.

## Spec Change Log

- 2026-07-12 — Full rerun executed (baseline `8d28a1b`). **Package-mode Release
  (CI parity): all 15 projects green — 2321 tests, 0 failed, 6 expected integration
  skips.** Root-owned environment fix: installed `ripgrep` (cleared the `rg`-dependent
  `Matrix_ValidationEvidenceCommandsAreReproducible` fitness test). **Source-mode Debug
  baseline is BLOCKED** by a governed Commons dependency-mode drift (`CS1704`
  double-import of `Hexalith.Commons.UniqueIds` on clean source rebuild;
  `FileNotFoundException Version=3.58.0.0` version skew with prebuilt submodules) —
  pinned by Story 7.1's `ProjectReference Include="$(HexalithCommonsRoot)…"` strategy,
  an **Ask First** boundary; not resolved without owner authorization. e2e: typecheck +
  16 artifact/SSR specs pass; interactive specs deferred to CI `ui-a11y`
  (`blazor.web.js` 500 under `dotnet run --no-build`). Full evidence in
  `tests/test-summary.md`. No product/test source edited in this pass.
- 2026-07-12 (owner-authorized strategy fix) — Source-mode Commons drift **resolved** by
  consuming Commons as a package in source mode (global `HexalithCommons*FromSource=false`
  overrides the submodule auto-enable → no `CS1704`, no version skew), keeping
  EventStore/Tenants/FrontComposer/Memories from source. Source-mode Debug now **14/15 green**
  (was 6 projects failing); only the 2 Release-oriented `ClientPackageTests` remain, and they
  pass in package mode. No product/test source edited; the fix is the documented build
  properties (see updated Verification command). Optional flag-free durability:
  `git submodule deinit references/Hexalith.Commons` (left as owner choice).

- 2026-10-08 — All configured current package/source projects and all 90 browser tests rerun. Root repairs and affected full projects pass; .NET builds are zero-warning/error. Current broad source and reconciled package totals are 3,031 total / 3,025 executed / 3,024 passed / one gateway failure / six existing skips. Existing mTLS recovery preserves ACLs and still fails live gateway readiness because the independently built Tenants child consumes DomainService 3.115.0 and cannot compile the required administrator contracts. Preserve failed attempts, original baseline fields and frozen intent; see the appended October evidence. Concurrent authors supplied the current dependency selections/commits; no reference, dependency strategy or Git mutation was performed by this repair pass.

## Design Notes

Run broad lanes with continue-on-failure first to capture the complete failure set. Triage CI/package routing before UI and application changes, then validate FrontComposer, EventStore, and Memories compatibility independently because the checked-out submodules are newer than the root gitlinks. Coverage remains explicitly unsupported by the local MTP runner and is reported as such rather than simulated.

## Verification

**Current commands and results (full argv/environment and identity receipts in `TestResults/bmad-revalidation-20261008/*-commands.json`):**
- `dotnet restore Hexalith.Parties.slnx -m:1` then `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1` -- **0/0, zero warnings/errors**.
- `GITHUB_ACTIONS=true pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults/bmad-revalidation-20261008/package-latest -Properties UseHexalithProjectReferences=false,UseNuGetDeps=true,NuGetAudit=false,MinVerVersionOverride=1.0.0` -- all 15 projects execute; original exit 1 includes the repaired stale receipt case and gateway failure. Complete repaired Release service rerun **907/907**; latest reconciled lane **3,024 passed, one failed, six declared skips**.
- Current-catalog source solution restore/build with `UseHexalithProjectReferences=true,UseNuGetDeps=false,HexalithCommonsFromSource=false,HexalithCommonsHttpFromSource=false,HexalithCommonsServiceDefaultsFromSource=false,NuGetAudit=false,MinVerVersionOverride=1.0.0,GeneratePackageOnBuild=false,BuildInParallel=false` -- **0/0, zero warnings/errors**.
- `GITHUB_ACTIONS=true pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Debug -ContinueOnFailure -ResultsDirectory TestResults/bmad-revalidation-20261008/source-latest -Properties UseHexalithProjectReferences=true,UseNuGetDeps=false,HexalithCommonsFromSource=false,HexalithCommonsHttpFromSource=false,HexalithCommonsServiceDefaultsFromSource=false,NuGetAudit=false,MinVerVersionOverride=1.0.0,GeneratePackageOnBuild=false,BuildInParallel=false` -- exit 1; **3,031 total, 3,025 executed, 3,024 passed, one gateway failure, six declared skips**.
- Full topology reruns in Debug/source then Release/package with the same respective properties and `GITHUB_ACTIONS=true,Dapr__Mtls__Enabled=true,Dapr__Mtls__CertificateDirectory=/home/administrator/.local/state/hexalith-parties/dapr-certs` -- each exit 1, **35 passed, one gateway failed, six existing skips**. Exact child build/CS0246 and readiness traces are retained; no ACL or gate weakened.
- `cd tests/e2e` then `npm ci`, `npm run typecheck`, `DEBUG=pw:webserver npm test` -- **pass; 90/90 executed/passed**, automatic source host, configured axe gates, no exclusions.
- `bash scripts/check-no-warning-override.sh` and `git -c core.whitespace=cr-at-eol diff --check` -- **pass**. Plain `git diff --check` reports existing CRLF line terminators; no Git/build whitespace policy was changed.

**Historical July command retained separately:** `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Debug -ContinueOnFailure -ResultsDirectory TestResults/bmad-source-20260712 -Properties UseHexalithProjectReferences=true,UseNuGetDeps=false,HexalithCommonsFromSource=false,HexalithCommonsHttpFromSource=false,HexalithCommonsServiceDefaultsFromSource=false,HexalithCommonsVersion=2.28.0,HexalithTenantsVersion=2.4.2,NuGetAudit=false,MinVerVersionOverride=1.0.0,GeneratePackageOnBuild=false,BuildInParallel=false` -- the exact obsolete version overrides now produce **NU1109** against required Commons.UniqueIds >=2.30.1. This diagnostic does not replace the current-catalog source verdict above. Original July evidence and decisions remain dated in the change log and append-only summary.
