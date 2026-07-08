---
title: 'Run all tests and fix issues'
type: 'bugfix'
created: '2026-07-08T00:00:00+02:00'
status: 'done'
review_loop_iteration: 0
baseline_revision: 'a46d453c8f4d0568519daa9611baecef461b4780'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
warnings: []
---

<frozen-after-approval reason="human-owned intent - do not modify unless human renegotiates">

## Intent

**Problem:** The repository test lanes must be run under the current .NET 10 / Microsoft.Testing.Platform setup, and repo-owned failures must be fixed so the configured suites are trustworthy again.

**Approach:** Run the documented `scripts/test.ps1` lanes and targeted per-project tests, repair failures in root-repository code or tests, and distinguish real code defects from external feed, package-version, Docker/DAPR, or source-mode blockers.

## Boundaries & Constraints

**Always:** Use `Hexalith.Parties.slnx` for restore/build and per-project `dotnet test` or `scripts/test.ps1` for tests; keep package versions centralized; preserve `TreatWarningsAsErrors`; use Microsoft.Testing.Platform-compatible result options; keep submodule contents read-only; respect root-declared submodule policy.

**Ask First:** Editing any `references/Hexalith.*` submodule, changing public package contracts, weakening build gates, disabling warnings-as-errors, replacing source/package dependency strategy, or changing release/package versions beyond the minimum needed to explain a restore blocker.

**Never:** Do not initialize nested submodules, use legacy `.sln` files, hide failures by excluding tests, use `--filter` as a validation substitute under MTP, add inline package versions to `.csproj`, or claim package IDs are missing when the actual issue is unavailable configured versions or feeds.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Repo-owned test failure | A unit or integration test fails because root code/test setup is stale or wrong | Patch the smallest root-repo file set and rerun the focused test | If the fix crosses an Ask First boundary, stop and ask |
| Package-mode restore failure | A package ID exists but the configured version is not available from configured sources | Record the exact package/version/source blocker and avoid mislabeling it as a missing package ID | Keep source-mode validation separate |
| Environment-dependent lane | Docker, DAPR, browsers, or external services are unavailable | Run what can execute locally and document the exact skipped/blocked command | Do not change tests just to bypass the environment |
| MTP option mismatch | VSTest-era logger, coverage, or filter options produce zero tests or runner errors | Update scripts/docs/commands to use MTP-compatible options or record unsupported coverage | Do not count zero-test runs as passing |

</frozen-after-approval>

## Code Map

- `scripts/test.ps1` -- documented lane runner and result-output contract.
- `.github/workflows/test.yml` -- CI invocation of the same test semantics.
- `tests/Hexalith.Parties.Tests/**` -- main domain and infrastructure tests where current compile/runtime failures are concentrated.
- `tests/Hexalith.Parties.*.Tests/**` -- package, client, contracts, UI, security, integration, deployment, and sample validation projects.
- `Directory.Build.props`, `Directory.Packages.props`, and project files -- centralized dependency/source-mode configuration; edit only when required by a verified build failure.
- `_bmad-output/implementation-artifacts/tests/test-summary.md` -- final validation evidence and residual blockers.

## Tasks & Acceptance

**Execution:**
- [x] `scripts/test.ps1` and `.github/workflows/test.yml` -- verify or repair MTP-compatible test arguments and result output -- ensures all lanes run tests instead of failing on obsolete VSTest switches.
- [x] `tests/Hexalith.Parties.Tests/**` -- fix focused compile/runtime failures found by targeted test runs -- restores the largest root test project.
- [x] `tests/Hexalith.Parties.*.Tests/**` -- fix additional root-owned failures from package/client/contracts/security/UI/integration/deploy suites -- keeps all configured projects aligned.
- [x] `_bmad-output/implementation-artifacts/tests/test-summary.md` and this spec -- record commands, pass/fail counts, fixed issues, and external blockers -- makes the outcome reviewable.

**Acceptance Criteria:**
- Given the repository uses Microsoft.Testing.Platform, when `scripts/test.ps1` runs a lane, then each test project receives MTP-compatible result arguments and a nonzero failing exit code is preserved.
- Given a root-owned test fails, when the focused project is rerun after the fix, then the original failure no longer reproduces and no broader build gate is weakened.
- Given package-mode restore cannot obtain a configured package version, when the result is reported, then the package ID, requested version, configured source, and distinction between ID existence and version availability are stated.
- Given an environment-dependent lane cannot run locally, when validation is summarized, then the exact command and blocker are recorded without marking the lane green.

## Spec Change Log

## Design Notes

The validation ladder should separate package-mode from source-mode. Package-mode answers whether the configured NuGet sources contain the exact requested versions; source-mode answers whether the checked-out root-declared references can build and test without editing submodules.

## Verification

**Commands:**
- `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Debug -ContinueOnFailure -ResultsDirectory TestResults/bmad-source-debug-final -Properties UseHexalithProjectReferences=true,UseNuGetDeps=false,NuGetAudit=false,MinVerVersionOverride=1.0.0,GeneratePackageOnBuild=false,BuildInParallel=false` -- passed: all 15 projects passed.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults/bmad-package-final-2 -Properties UseHexalithProjectReferences=false,UseNuGetDeps=true,NuGetAudit=false,MinVerVersionOverride=1.0.0` -- passed: all 15 projects passed.
- Full working-tree search for the retired EventStore version literal -- passed: no matches.
- `bash scripts/check-no-warning-override.sh` -- passed: no warning-as-error bypass or nested-submodule regressions.
- `git diff --check && git -C references/Hexalith.Builds diff --check && git -C references/Hexalith.EventStore diff --check && git -C references/Hexalith.Tenants diff --check` -- passed: no whitespace errors.
