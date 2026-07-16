---
title: '8.1 Baseline and release-blocker stabilization'
type: 'chore'
created: '2026-07-07T07:21:23+02:00'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: false
baseline_revision: 'aca2b9edb5b0676ddc8b0e97e50966795b8bd1be'
final_revision: '677c5ba5bf0391225fa4086bfef688636252d591'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/epic-7-final-readiness-2026-06-29.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** Epic 8 cannot start deletion-heavy domain-boundary refactoring while the local and CI baseline can silently skip test projects, use solution-level `dotnet test` as a false green, or hide release blockers behind submodule/package drift.

**Approach:** Correct the explicit test lanes, align CI with the pinned SDK and complete test inventory, and record the current build/test blockers plus rerun guidance so later Epic 8 stories inherit a trustworthy baseline.

## Boundaries & Constraints

**Always:** Use `.slnx` for restore/build only; run .NET tests per project; keep root-repository submodules only; preserve existing product behavior; record unresolved release blockers with exact commands and owners.

**Block If:** Stabilization requires changing submodule contents, initializing nested submodules, changing public package contracts, weakening warnings-as-errors, or resolving a release decision that needs a platform/submodule owner.

**Never:** Do not perform structural Epic 8 migrations, delete rollback paths, suppress tests to get green, convert project references to packages, add package versions to `.csproj`, or present sandbox/network-blocked package tests as passed.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Unit lane inventory | `scripts/test.ps1 -Lane unit` | Runs every unit-style test project, including `Hexalith.Parties.Authentication.Tests` and `Hexalith.Parties.ConsumerPortal.Tests` | If a project fails, the lane fails and the test summary records the blocker |
| Aggregate lanes | `scripts/test.ps1 -Lane all` or `-Lane coverage` | Iterates explicit project lists instead of invoking solution-level `dotnet test` | Any blocked project is visible by path; no solution-level false green |
| CI shard inventory | GitHub Actions test matrix | Uses SDK `10.0.302` and includes all 15 .NET test projects across shards | Missing projects or stale SDK pins are treated as baseline drift |
| Release blocker evidence | Build/test/package/deploy checks fail because of known drift or environment limits | The blocker remains visible with the exact command, observed result, and required owner/environment | Do not edit submodules or weaken gates to hide the blocker |

</intent-contract>

## Code Map

- `scripts/test.ps1` -- local lane runner; currently omits ConsumerPortal tests and uses solution-level `dotnet test` for `all`/`coverage`.
- `.github/workflows/test.yml` -- CI build/test matrix; baseline used the previous SDK pin and omitted Authentication/ConsumerPortal test projects from shards.
- `docs/development-guide.md` -- user-facing build/test guidance; currently shows solution-level `dotnet test` for "everything".
- `docs/ci.md` -- CI/local-parity guidance; should match per-project test execution and the pinned SDK.
- `docs/index.md` -- quick reference; should not recommend solution-level test execution.
- `_bmad-output/implementation-artifacts/tests/test-summary.md` -- release-readiness evidence summary; append Story 8.1 baseline changes and unresolved blockers.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- story tracking; move Epic 8 and Story 8.1 out of backlog as implementation progresses.
- `_bmad-output/planning-artifacts/epic-7-final-readiness-2026-06-29.md` -- source of existing release blockers and rerun commands.

## Tasks & Acceptance

**Execution:**
- [x] `scripts/test.ps1` -- add `Hexalith.Parties.ConsumerPortal.Tests` to the unit project list, replace `all` and `coverage` solution-level execution with explicit per-project loops, and pass coverage arguments through the same per-project helper -- prevents skipped tests and false-green solution lanes.
- [x] `.github/workflows/test.yml` -- update all .NET setup steps to SDK `10.0.302`, add `Hexalith.Parties.Authentication.Tests` and `Hexalith.Parties.ConsumerPortal.Tests` to CI shards, and keep per-project `dotnet test` execution -- aligns CI with `global.json` and the full test inventory.
- [x] `docs/development-guide.md`, `docs/ci.md`, and `docs/index.md` -- replace solution-level test guidance with lane/per-project guidance, and document direct xUnit v3 executable filtering, `-m:1` build guidance, `MinVerVersionOverride=1.0.0`, and network-enabled package-test limitations -- gives later Epic 8 stories repeatable validation instructions.
- [x] `_bmad-output/implementation-artifacts/tests/test-summary.md` -- append a Story 8.1 section listing corrected lanes, commands run, unresolved release blockers, and owner/environment decisions for gitlink drift, package validation, deploy validation, UI accessibility, production KMS, and the missing Epic 8 architecture spine -- makes blockers auditable instead of implicit.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- update Epic 8/Story 8.1 status consistently with the completed stabilization artifact -- keeps BMAD tracking aligned with implementation.

**Acceptance Criteria:**
- Given the local lane runner, when the unit lane inventory is inspected, then `tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj` is included with the other unit test projects.
- Given the local lane runner, when `all` or `coverage` is inspected, then neither lane invokes `dotnet test` against `Hexalith.Parties.slnx`.
- Given the CI workflow, when the test matrix is inspected, then SDK setup matches `global.json` `10.0.302` and all 15 .NET test projects are assigned to a shard.
- Given current release blockers, when validation cannot pass because of submodule drift, sandbox network denial, deploy environment gaps, or owner decisions, then the exact blocker and rerun path are recorded without weakening build/test gates.

## Spec Change Log

## Review Triage Log

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 8: (high 0, medium 5, low 3)
- defer: 2: (high 0, medium 2, low 0)
- reject: 4: (high 0, medium 0, low 4)
- addressed_findings:
  - `[medium]` `[patch]` Added inventory guards so `scripts/test.ps1` and `.github/workflows/test.yml` cannot drift from the real `tests/**/*.csproj` set.
  - `[low]` `[patch]` Added a duplicate-project guard to `scripts/test.ps1`.
  - `[medium]` `[patch]` Updated visible SDK/test-count documentation in README and generated docs touched by the new baseline.
  - `[medium]` `[patch]` Added the missing restore command to CI local-parity guidance before the documented `--no-restore` build.
  - `[medium]` `[patch]` Updated fresh-clone submodule guidance to include the root build submodules while keeping nested submodules forbidden.
  - `[low]` `[patch]` Replaced the broken double-quoted PowerShell parser command in this spec with the single-quoted command that actually passed.
  - `[medium]` `[patch]` Reconciled sprint status and Story 8.1 evidence with the missing Epic 8 architecture spine by marking only Story 8.1 done and preserving the spine blocker for deletion-heavy migrations.
  - `[low]` `[patch]` Updated the Story 8.1 test summary with final review-fix verification.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 6: (high 0, medium 5, low 1)
- defer: 0
- reject: 1: (high 0, medium 0, low 1)
- addressed_findings:
  - `[medium]` `[patch]` Scoped the CI inventory guard to the `test` matrix shard project blocks so a project path elsewhere in the workflow cannot mask a missing shard assignment.
  - `[low]` `[patch]` Added duplicate-project detection to the CI inventory guard so a test project assigned to multiple shards is reported.
  - `[medium]` `[patch]` Moved fresh-clone submodule initialization before `dotnet aspire run` in the getting-started command sequence.
  - `[medium]` `[patch]` Added the missing Authentication test project row and corrected the test inventory heading in `docs/component-inventory.md`.
  - `[medium]` `[patch]` Updated the external dependency inventory to include the required Build and PolymorphicSerializations root submodules.
  - `[medium]` `[patch]` Documented default package mode and source-mode triage properties in development and CI guidance.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 7: (high 0, medium 5, low 2)
- defer: 2: (high 0, medium 2, low 0)
- reject: 1: (high 0, medium 0, low 1)
- addressed_findings:
  - `[medium]` `[patch]` Updated AppHost missing-submodule errors and onboarding guardrail tests to require the full baseline root-submodule command.
  - `[medium]` `[patch]` Added package-mode blocker caveats to README and getting-started quick-start paths so known unpublished-package restore failures are not presented as green.
  - `[medium]` `[patch]` Hardened the CI inventory lint guard to inspect only the `test` matrix include project blocks.
  - `[low]` `[patch]` Hardened the CI inventory lint guard to ignore commented PowerShell project paths when validating `scripts/test.ps1`.
  - `[low]` `[patch]` Updated `docs/source-tree-analysis.md` so its submodule explanation matches the new baseline root-submodule guidance.
  - `[medium]` `[patch]` Updated `tests/README.md` to remove solution-level test guidance and include Authentication, ConsumerPortal, and UI test projects in the lane inventory.
  - `[medium]` `[patch]` Broadened verification evidence to cover active docs/source/test guidance and directly verified the updated onboarding/topology guardrails.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 5: (high 0, medium 1, low 4)
- defer: 0
- reject: 0
- addressed_findings:
  - `[medium]` `[patch]` Hardened the CI inventory guard to read test project paths only from the PowerShell lane arrays in `scripts/test.ps1`, preventing unrelated path references from masking skipped local lane projects.
  - `[low]` `[patch]` Updated `docs/ci.md` so the Quality Gate description includes the `ui-a11y` release gate.
  - `[low]` `[patch]` Updated `docs/index.md` current source/test counts to match the repository inventory.
  - `[low]` `[patch]` Updated `docs/architecture.md` current repository counts and CI flow to include the UI accessibility gate.
  - `[low]` `[patch]` Updated `docs/source-tree-analysis.md` current solution counts, root reference wording, and CI flow summary.

## Design Notes

This story is a stabilization gate, not a refactoring story. Correcting test orchestration is in scope; resolving submodule gitlink ownership, production KMS readiness, package publishing mode, FrontComposer pointer choices, and the missing Epic 8 architecture spine is recorded as blocker/deferred-owner work unless a command proves a narrow in-repo fix is sufficient.

## Verification

**Commands:**
- `pwsh -NoProfile -Command '$tokens = $errors = $null; [System.Management.Automation.Language.Parser]::ParseFile("scripts/test.ps1", [ref] $tokens, [ref] $errors) > $null; if ($errors.Count) { $errors | ForEach-Object { $_.Message }; exit 1 }'` -- expected: script parses cleanly.
- `rg -n "dotnet test --solution|dotnet test --project|Hexalith.Parties.slnx.*dotnet test|dotnet test .*Hexalith.Parties.slnx" scripts/test.ps1 docs/development-guide.md docs/ci.md docs/index.md` -- expected: no stale solution-level/project-option test guidance in the corrected surfaces.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane unit -Configuration Release` -- expected: passes, or fails with exact project/blocker recorded in `_bmad-output/implementation-artifacts/tests/test-summary.md`.
- `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: passes, or current submodule/package blocker is recorded unchanged.

## Auto Run Result

Status: done

Summary: Story 8.1 stabilized the baseline by making local and CI test inventories explicit, guarded, and aligned with the current 15 .NET test projects. Follow-up reviews hardened the CI inventory guard, corrected active onboarding/AppHost/test guidance for the baseline root-submodule command, and made package-mode blockers visible in quick-start paths.

Files changed:
- `scripts/test.ps1` -- added ConsumerPortal tests, explicit all/coverage project loops, per-project execution, duplicate detection, and inventory validation.
- `.github/workflows/test.yml` -- aligned setup-dotnet with SDK `10.0.302`, added missing Authentication/ConsumerPortal shards, and added a matrix-scoped lint inventory guard with duplicate detection.
- `README.md`, `docs/development-guide.md`, `docs/ci.md`, `docs/index.md`, `docs/getting-started.md`, `docs/project-overview.md`, `docs/architecture.md`, `docs/source-tree-analysis.md`, `docs/component-inventory.md` -- updated SDK, test-lane, test-count, submodule, and package-validation guidance.
- `tests/README.md` -- removed solution-level test execution guidance and aligned the lane inventory with all current test projects.
- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` -- updated missing-submodule error guidance to the baseline root-submodule command.
- `tests/Hexalith.Parties.Sample.Tests/SampleOnboardingGuardrailTests.cs`, `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` -- updated guardrails for the baseline root-submodule command.
- `_bmad-output/implementation-artifacts/epic-8-context.md` -- compiled Epic 8 context.
- `_bmad-output/implementation-artifacts/tests/test-summary.md` -- recorded Story 8.1 evidence and residual blockers.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- marked Epic 8 in progress and Story 8.1 done.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- captured follow-up runner enhancements.

Review findings breakdown: initial review addressed 8 patch findings, deferred 2 runner enhancements, and rejected 4 low-consequence findings; follow-up reviews addressed 13 additional patch findings, recognized 2 existing deferred runner enhancements without duplicating ledger entries, and rejected 2 transient/low-consequence findings. This final follow-up review addressed 5 additional patch findings: 1 medium CI guard-hardening fix and 4 low documentation corrections. No new deferred-work entries were added.

Follow-up review recommendation: false. The final pass changed localized guard parsing and documentation only, and the updated guard was directly verified against the repository test inventory.

Verification performed:
- PowerShell parse check for `scripts/test.ps1` passed.
- Python inventory comparison for the `scripts/test.ps1` PowerShell lane arrays and the `.github/workflows/test.yml` test-matrix project blocks against `tests/**/*.csproj` passed.
- Stale solution-level/project-option test guidance check returned no matches.
- Stale SDK/test-count/root-submodule guidance check returned no matches in the corrected guidance surfaces.
- Active guidance stale-command scan across README, docs, src, tests, scripts, and CI returned no matches for the old two-submodule command, SDK `10.0.302`, solution-level `dotnet test`, or `dotnet test --project` guidance, excluding the historical `docs/project-scan-report.json` artifact.
- Final follow-up stale count/CI wording scan returned no active-doc matches; only the historical `docs/project-scan-report.json` scan artifact retains old counts.
- `git diff --check` passed.
- `bash scripts/check-no-warning-override.sh` passed.
- PyYAML parsed `.github/workflows/test.yml` successfully.
- `dotnet test tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` passed: 58 tests.
- `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.AppHostTenantsTopologyTests` passed: 16 tests.
- `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` failed on the pre-existing `Hexalith.Memories` source-mode Release guard.
- `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` ran 537 tests with 532 passed and 5 pre-existing tenant-event failures.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane unit -Configuration Release` failed on the recorded package-mode blocker: missing `Hexalith.Tenants.Client` from `nuget.org`.
- `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` failed on recorded unpublished/unavailable Hexalith packages: `Hexalith.Tenants.Client`, `Hexalith.Tenants.Testing`, and `Hexalith.Commons.ServiceDefaults`.

Residual risks: release remains blocked until submodule gitlink drift, package-mode/source-mode ownership, package validation network access, deploy validation completion, UI accessibility/FrontComposer validation, production KMS readiness, and the Epic 8 architecture spine are resolved by their owners.
