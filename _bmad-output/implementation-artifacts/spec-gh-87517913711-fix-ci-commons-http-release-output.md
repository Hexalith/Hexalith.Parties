---
title: 'Fix CI Commons.Http Release support package output'
type: 'bugfix'
created: '2026-07-16'
status: 'done'
review_loop_iteration: 1
baseline_commit: '89ae84598fd0724ffacb874aad04d24e89d49435'
source_run: 'https://github.com/Hexalith/Hexalith.Parties/actions/runs/29465579014/job/87517913711'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-gh-29457973512-fix-package-version-expansion.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09-g12-package-publishing-source-mode-ci.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI restore and Release build succeed, but package-consumer validation fails with NU5026 because `Hexalith.Commons.Http` is built as an out-of-solution project in `Debug` while support-package packing requires its missing `bin/Release/net10.0` DLL with `--no-build`. This blocks all test tiers after the package gate.

**Approach:** Make every support project consumed by the package validator, including `Hexalith.Commons.Http`, a direct project in the Release solution and add a regression guard that keeps the validator's support-project inventory aligned with solution membership.

## Boundaries & Constraints

**Always:** Keep the package gate Release-only and preserve its `--no-build` separation; retain the narrow Commons.Http source fallback, central Commons version resolution, exact support-package metadata checks, package inventory, warnings-as-errors, and later CI gates; modify root-owned files only.

**Ask First:** Changes to submodule content, the shared `Hexalith.Builds` workflow, support-package inventory, dependency routing, or the build-during-pack lifecycle.

**Never:** Pack Debug artifacts, weaken or bypass consumer validation, hardcode the current Commons version, remove later test gates, initialize nested submodules, or rely on stale local build output as proof.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Clean CI build | Fresh checkout has no Commons.Http outputs | Release solution build emits `bin/Release/net10.0/Hexalith.Commons.Http.dll`; no-build support packing succeeds | Any missing Release artifact remains fatal |
| Support inventory expands | Validator enumerates a new support project | CI contract requires that project to be a direct solution member | Focused test fails with the omitted project path |
| Stale Debug artifact exists | `bin/Debug` contains Commons.Http | Release package validation ignores it and uses the Release artifact | Debug output never satisfies the gate |

</frozen-after-approval>

## Code Map

- `Hexalith.Parties.slnx` -- authoritative project set and configuration mapping for the CI Release build; the baseline omission of Commons.Http is repaired by this task.
- `scripts/validate-consumer-package-references.py` -- enumerates eight support projects and packs them Release with `--no-build`; its behavior remains unchanged.
- `tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs` -- existing package-routing and support-package regression suite.

## Tasks & Acceptance

**Execution:**

- [x] `Hexalith.Parties.slnx` -- add `Hexalith.Commons.Http.csproj` beside the other direct Commons support projects so CI maps it to Release.
- [x] `tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs` -- derive the validator's support-project list through its Python module and assert every entry is a direct solution project, reporting omitted paths.

**Acceptance Criteria:**

- Given a clean checkout, when the exact CI restore, Release build, package, metadata, and consumer-validation sequence runs, then Commons.Http is built and packed at the resolved central version and both package-only consumers build.
- Given the current support-project inventory, when focused CI contract tests run, then all eight projects are proven to participate directly in the solution configuration mapping.
- Given the repaired worktree, when the exact package commands used by GitHub Actions run locally, then `Validate package consumer references` passes and this failure no longer blocks the later unit and integration gates; remote-run confirmation remains post-handoff evidence.

## Spec Change Log

- 2026-07-16, review iteration 1 -- The review found that a successful remote GitHub Actions run could not be completed inside the workflow's no-push boundary and that the Code Map described the baseline omission as current state. The acceptance criterion now requires the exact local CI-equivalent package gate and treats the remote run as post-handoff evidence; the Code Map now distinguishes baseline from repaired state. This avoids a false completion claim and contradictory reviewer context. KEEP: direct Commons.Http solution membership, dynamic validator-to-solution inventory comparison with omitted-path diagnostics, Release-only no-build support packing, and the already-green focused/build/package verification design.

## Design Notes

The validator intentionally packages already-built Release artifacts. Adding the missing project to the solution fixes the configuration boundary shared by all support projects without turning consumer validation into a second dependency build or changing submodule code.

## Verification

**Commands:**

- `dotnet restore Hexalith.Parties.slnx` -- expected: restore succeeds from the root solution.
- `dotnet build Hexalith.Parties.slnx --no-restore --configuration Release -warnaserror` -- expected: zero warnings/errors and Commons.Http under `bin/Release/net10.0`.
- `dotnet build tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj --configuration Release -warnaserror` -- expected: CI contract tests compile.
- `dotnet tests/Hexalith.Parties.Ci.Tests/bin/Release/net10.0/Hexalith.Parties.Ci.Tests.dll -class Hexalith.Parties.Ci.Tests.CommonsHttpRestoreRoutingTests` -- expected: focused package-routing tests pass.
- `python3 scripts/pack-release-packages.py ./nupkgs 0.0.0-ci-test && python3 scripts/validate-nuget-packages.py ./nupkgs && python3 scripts/validate-consumer-package-references.py ./nupkgs` -- expected: exact CI package-consumer gate succeeds.
- `dotnet tests/Hexalith.Parties.Ci.Tests/bin/Release/net10.0/Hexalith.Parties.Ci.Tests.dll` -- expected: full CI contract suite passes.

## Suggested Review Order

**Release configuration ownership**

- Make Commons.Http participate in the same Release solution mapping as support packages.
  [`Hexalith.Parties.slnx:15`](../../Hexalith.Parties.slnx#L15)

**Regression contract**

- Keep the validator inventory complete, unique, and directly mapped into the solution.
  [`CommonsHttpRestoreRoutingTests.cs:294`](../../tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs#L294)

- Preserve Release-only no-build package commands and reject ambiguous invocation shapes.
  [`CommonsHttpRestoreRoutingTests.cs:512`](../../tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs#L512)

**Deferred unrelated drift**

- Record the invalid Builds version without changing concurrent submodule work.
  [`deferred-work.md:29`](deferred-work.md#L29)

- Record the Memories persisted-capacity regression gap for later ownership.
  [`deferred-work.md:32`](deferred-work.md#L32)

- Record the Memories migration-state regression gap for later ownership.
  [`deferred-work.md:35`](deferred-work.md#L35)
