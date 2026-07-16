---
title: 'Fix invalid Builds package version blocking CI restore'
type: 'bugfix'
created: '2026-07-16'
status: 'in-progress'
review_loop_iteration: 0
baseline_commit: 'c715eae7114d8befe1ba120793d269661dd7bc4f'
source_run: 'https://github.com/Hexalith/Hexalith.Parties/actions/runs/29467970597'
context:
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-ci-invalid-package-version.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md'
  - '{project-root}/_bmad-output/implementation-artifacts/deferred-work.md'
---

# Fix invalid Builds package version blocking CI restore

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties Actions run `29467970597` and its successor fail during
solution restore because the consumed `Hexalith.Builds` catalog supplies
`v1.16.3` as a NuGet version. NuGet rejects the Git-tag prefix, so build,
package-consumer validation, and every test tier are skipped.

**Approach:** Consume the source correction released in `Hexalith.Builds`
`v4.18.11`, add a fail-closed Builds release guard for every evaluated central
package version, then advance the Parties Builds gitlink with owner signoff and
prove the complete local and remote CI ladders.

## Boundaries & Constraints

**Always:** Fix shared package metadata in its owning Builds repository; validate
the evaluated package catalog before semantic release; retain exact restore,
Release warnings-as-errors, package-consumer, and test gates; update the parent
gitlink only to an immutable pushed Builds commit; preserve unrelated worktree
changes; initialize root-declared submodules only.

**Ask First:** Changes to Parties product behavior, public APIs, package
inventory, dependency routing beyond the invalid version, another root
submodule, or PRD/epic/UX scope.

**Never:** Keep a root-only hardcoded version override; weaken or skip restore,
package, or test gates; accept blank or unresolved versions; treat a tag name as
a NuGet package version; initialize nested submodules; overwrite the concurrent
Memories pointer change.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Original regression | Evaluated version is `v1.16.3` or `V1.16.3` | Builds release stops before `Create Release` | Diagnostic identifies the package and removes ambiguity about the `v` prefix |
| Valid stable version | Evaluated version is `1.16.3` | Validator accepts it and Parties restore resolves the published package | Any later restore failure remains fatal |
| Valid prerelease/legacy version | Evaluated version is SemVer prerelease/build metadata or a valid four-part NuGet version | Validator accepts existing catalog syntax | No normalization changes are made by the validator |
| Invalid evaluation | Version is blank, unresolved, malformed, or evaluator JSON is invalid | Validator fails closed | Bounded actionable diagnostic; no release step |
| Parent adoption | Builds guard commit is pushed and shared workflow passes | Parties gitlink and signoff reference the exact immutable SHA | Mismatched or unsigned pointer fails the RC gate |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Builds/Props/Directory.Packages.props` -- owning catalog;
  `v4.18.11` contains the source normalization to `1.16.3`.
- `references/Hexalith.Builds/Tools/validate-central-package-versions.ps1` --
  evaluates all central `PackageVersion` items and rejects unsafe values.
- `references/Hexalith.Builds/Tools/test-central-package-version-validator.ps1`
  -- fixture coverage for valid and invalid evaluation paths and workflow order.
- `references/Hexalith.Builds/.github/workflows/build-release.yml` -- runs both
  central-version gates before Dapr validation and semantic release.
- `.gitlink-signoff.tsv` -- owner-authorized immutable Builds pointer.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- closes the exact
  warning that predicted this restore failure while retaining Memories entries.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` --
  keeps the Story 8.3 matrix contract portable and aligned with the approved
  G4/G8/G11 routing decisions.
- `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` --
  isolates gateway contract tests from newly registered Dapr-backed projection
  collaborators while preserving message identity and status lifecycle proof.
- `tests/Hexalith.Parties.Tests/Search/LocalFuzzySearchPerformanceBenchmarkTests.cs`
  -- runs strict wall-clock search benchmarks without parallel test contention.
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`
  -- records the accepted G4/G8/G11 ownership, proof, and rollback invariants
  exercised by the fitness tests.

## Tasks & Acceptance

**Execution:**

- [x] Consume Builds `v4.18.11`, where commit `7cd855c` normalizes the shared
  PolymorphicSerializations version from `v1.16.3` to `1.16.3`.
- [x] Add a central-version validator that evaluates all `PackageVersion` items
  and rejects tag prefixes, blanks, unresolved expressions, and malformed
  NuGet versions.
- [x] Add fixture tests for valid stable, prerelease, and four-part versions;
  exact lowercase/uppercase tag-prefix regressions; blank, unresolved, and
  malformed versions; malformed/failed evaluator output; and workflow order.
- [x] Wire both scripts before semantic release and preserve the existing Dapr
  validation gates.
- [x] Require a successful Builds workflow for guard commit `6516faf` (release
  run `29480773799`, including both new and both existing validation steps).
- [x] Advance the Parties Builds gitlink and signoff to the pushed guard commit.
- [x] Correct the post-restore MTP runner contract exposed by Parties run
  `29482004796`: retain VSTest as the shared default, add explicit
  `microsoft-testing-platform` routing, emit MTP-native TRX, and use xUnit v3
  trait filters for Aspire/performance lanes. Builds run `29482625063` passes
  the new shared-workflow contract gate and publishes `v4.19.0`.
- [x] Pass the affected local CI path: restore, serial Release build with
  warnings-as-errors, package generation/metadata/consumer validation, all
  Tier 1 projects, Sample tests, and CI contract tests.
- [x] Correct the Tier 2 failures exposed by clean run `29482841788`: reconcile
  fitness contracts with the approved G4/G8/G11 planning changes, make fixed-
  string evidence validation independent of runner-installed `rg`, and replace
  Dapr-backed gateway test collaborators with isolated no-op/in-memory seams.
- [x] Preserve the 500 ms local-search hard gate while removing intra-process
  benchmark noise exposed by Release run `29484894219`; the performance
  collection runs non-parallel instead of relaxing the threshold or skipping
  the test.
- [ ] Require the resulting Parties remote Actions run to pass the complete
  clean-checkout workflow.
- [x] Close the invalid-version deferred entry without changing the two Memories
  review entries.

**Acceptance Criteria:**

- Given the original tag-shaped version, when the Builds release gate evaluates
  it, then validation fails before semantic release with a package-specific
  diagnostic.
- Given the fixed Builds catalog, when Parties restores without property
  overrides, then every solution project restores successfully.
- Given the parent change, when the RC gitlink gate runs against the prior root
  commit, then the exact Builds SHA has real-owner `validated-advance` evidence.
- Given the repaired root commit, when GitHub Actions runs, then restore, Release
  build, package-consumer validation, CI contracts, unit tests, and integration
  tests pass without gate weakening.

## Spec Change Log

- 2026-07-16 -- During proposal approval, Builds commit `7cd855c` and release
  `v4.18.11` landed the source normalization concurrently. Implementation keeps
  that immutable fix, adds the approved preventive guard in `6516faf`, and does
  not duplicate or rewrite the released correction.
- 2026-07-16 -- Clean Parties run `29482004796` passed the repaired restore,
  Release build, and package-consumer gates, then failed before executing Tier 1
  because the shared workflow passed unsupported VSTest `--logger`/`--collect`
  options to an MTP-selected repository. Builds `v4.19.0` adds an explicit,
  backward-compatible test-platform contract and a 20-assertion release guard;
  Parties opts into MTP without removing tests or evidence.
- 2026-07-16 -- Clean Parties run `29482841788` proved the MTP route by passing
  all 1,649 Tier 1 tests, then exposed seven prerequisite-matrix fitness
  mismatches and Dapr-backed projection activation in isolated gateway tests.
  The follow-up aligns the fitness contract with approved G4/G8/G11 routing,
  replaces the external `rg` dependency with an equivalent bounded in-process
  fixed-string search, and restores gateway isolation without skipping tests.
- 2026-07-16 -- CI run `29484894277` passed the repaired Tier 1 and Tier 2 jobs.
  Concurrent Release run `29484894219` then exposed the strict 10K local-search
  wall-clock benchmark competing with the other 575 tests: it measured 632 ms
  remotely versus 89–239 ms in local full-suite runs. The benchmark retains its
  500 ms limit and now runs in a non-parallel xUnit collection.

## Design Notes

The validator uses MSBuild evaluation rather than XML text matching so property
indirection is resolved exactly as consuming repositories see it. The accepted
grammar covers NuGet's one-to-four numeric components plus prerelease/build
metadata; tag prefixes remain explicitly rejected. The hidden evaluator seam is
test-only and lets malformed process output be exercised through the production
boundary.

## Verification

**Commands:**

- `pwsh -NoProfile -File ./Tools/validate-central-package-versions.ps1` in
  Builds -- expected: all evaluated catalog entries pass.
- `pwsh -NoProfile -File ./Tools/test-central-package-version-validator.ps1`
  in Builds -- expected: all focused fixtures pass.
- Existing Builds Dapr validator and fixture commands -- expected: no regression.
- `actionlint .github/workflows/build-release.yml` -- expected: workflow valid.
- `scripts/gitlink-rc-gate.sh --diff origin/main` after the parent commit --
  expected: exact Builds bump is signed and accepted.
- `dotnet restore Hexalith.Parties.slnx` -- expected: success without overrides.
- `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore
  -warnaserror` -- expected: zero warnings and errors.
- `python3 scripts/pack-release-packages.py ./nupkgs 0.0.0-ci-test && python3
  scripts/validate-nuget-packages.py ./nupkgs && python3
  scripts/validate-consumer-package-references.py ./nupkgs` -- expected: package
  generation, metadata, and both package-only consumers pass.
- CI contract, unit, and integration test lanes -- expected: all pass.
- Builds run `29480773799` and the resulting Parties Actions run -- expected:
  successful conclusions.

**Observed local results:**

- Restore succeeds without the diagnostic property override that was required
  to isolate the original failure.
- The documented serial Release build (`-m:1`) succeeds with zero warnings and
  errors. An initial parallel attempt encountered two EventStore output-file
  locks; the prescribed serial parity command removed that local contention.
- All 9 Parties packages, NuGet metadata, and both package-only consumers pass.
- All 11 Tier 1 projects pass (1,649 tests); Sample passes 58 tests; CI contracts
  pass 16 tests.
- `Hexalith.Parties.Tests` passes all 576 tests in about six seconds after the
  accepted planning rows and fitness expectations were reconciled and the
  gateway factory replaced Dapr-backed projection activation, activity, index,
  and hosted-service collaborators with isolated test seams. The exact shared
  Tier 2 sequence also passes Sample (58) and CI contracts (16): 650 tests total.
- Parties run `29482004796` confirms the original repair remotely: Restore,
  Release Build, and package-consumer validation all pass. Its Tier 1 command
  exits before test discovery because VSTest-only arguments are rejected by
  MTP; that follow-up is corrected in Builds `v4.19.0` and the caller workflows.
- Parties run `29482841788` confirms Builds `v4.19.0` remotely: restore, Release
  build, package-consumer validation, and all 1,649 Tier 1 tests pass. Its Tier 2
  timeout is the direct regression evidence for the isolated gateway and matrix
  follow-up now passing locally; a new clean run remains the closure gate.

## Suggested Review Order

1. Confirm the released source normalization at Builds `7cd855c`.
2. Review validator failure semantics and fixture matrix at `6516faf`.
3. Confirm release-workflow ordering before `Create Release`.
4. Confirm the Parties gitlink and signoff use the same immutable SHA.
5. Review exact local and remote CI evidence before closing this spec.
