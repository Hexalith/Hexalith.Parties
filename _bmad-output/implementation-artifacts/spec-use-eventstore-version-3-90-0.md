---
title: 'Use Hexalith.EventStore 3.90.0 consistently'
type: 'bugfix'
created: '2026-08-04'
status: 'done'
baseline_commit: '02ccd31764957cee024704f809fada4f20cfcd9d'
review_loop_iteration: 1
context:
  - '{project-root}/docs/build-gate.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Source mode already pins Hexalith.EventStore `v3.90.0`, but the recorded Builds catalog selects packages 3.89.0. Package fixtures, CI guidance, and exact-tag evidence also retain older identities.

**Approach:** Adopt the checked-out Builds commit selecting 3.90.0, align fixtures with the central version, and refresh evidence made stale by the new tag. Validate package mode first; source mode remains diagnostic only.

## Boundaries & Constraints

**Always:** Preserve EventStore `v3.90.0` / `7854f8e51ce9b852bb6c3cac6012670122e93792`; source package versions from Builds; preserve the unrelated FrontComposer and Memories gitlinks; keep historical `v3.89.0` receipts; record the adopted Builds SHA in the owner-validation ledger.

**Ask First:** Stop before changing Parties APIs/behavior, selecting other dependency revisions, or resolving unrelated gitlink drift.

**Never:** Edit submodule source; add inline project versions; move the EventStore gitlink; globally replace historical evidence; weaken warnings, package mode, tests, or release gates.

</frozen-after-approval>

## Code Map

- `references/Hexalith.Builds` -- existing clean upstream advance to `a53166539bf4441d5e33d04281b14c2d59e950c3`; its sole change selects 3.90.0.
- `references/Hexalith.Builds/Props/Directory.Packages.props:8,40` -- authoritative version/property rows; read-only.
- `references/Hexalith.EventStore` -- clean `v3.90.0` source/gitlink at `7854f8e…`; read-only.
- `.gitlink-signoff.tsv` -- root-gitlink release authorization ledger for the adopted Builds SHA.
- `scripts/msbuild_properties.py:13,124` -- bounded evaluated-property resolver; extend the Commons pattern for EventStore.
- `scripts/validate-nuget-packages.py:13,126,164` and `release.config.cjs:16` -- authoritative pre-publish metadata gate currently validates only Commons dependency versions.
- `scripts/validate-consumer-package-references.py:15,39,49,291` -- package-only support feed currently hard-codes EventStore 3.47.0.
- Contracts/Client package tests and `tests/PackageTests.Shared/` -- local pack and resolved-assets evidence; any shared process helper must enforce bounded start, kill, exit, and stream capture.
- `tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs:85,370` -- central resolution and synthetic release-validator coverage.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:17,1084` -- exact live EventStore describe expectation; SHA assertion remains unchanged.
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:43` -- governed live evidence paired with the fitness assertion.
- `docs/ci.md:69` and `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs:204` -- obsolete 3.88.0 blocker narrative and its contract.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Builds`, `.gitlink-signoff.tsv` -- retain and authorize the existing upstream advance without editing submodule content.
- [x] Version-resolution scripts, consumer support packing, package tests, and CI resolver tests -- use EventStore's evaluated central version and assert the selected package assets.
- [x] `scripts/validate-nuget-packages.py` and CI tests -- require every declared `Hexalith.EventStore.*` dependency in all nine produced Parties packages to equal the evaluated version; prove wrong and missing required versions are rejected.
- [x] Fitness evidence, Story 8.3 matrix, CI guide, and documentation test -- refresh the live baseline while preserving historical receipts.
- [x] Restore, build, validate real package metadata, and run focused dependency/evidence tests before broadening through the repository validation ladder.

**Acceptance Criteria:**
- Given default package mode, when MSBuild evaluates EventStore dependencies, then all versions are `3.90.0` with no project override.
- Given the nine release packages, when the pre-publish validator reads their nuspecs, then every declared `Hexalith.EventStore.*` dependency is exactly `3.90.0`, and a stale or missing required dependency fails validation.
- Given source mode, when EventStore is inspected, then gitlink, checkout, and exact tag remain `7854f8e…` / `v3.90.0`.
- Given local package fixtures and clean consumers, when support packages are produced and restored, then both package metadata and selected assets use the evaluated EventStore version.
- Given the focused fitness and CI documentation tests, when they run, then live 3.90.0 evidence passes and historical 3.89.0 receipts remain intact.
- Given the approved dirty worktree, when implementation completes, then FrontComposer and Memories retain their pre-existing SHAs and no submodule source tree is modified.

## Spec Change Log

- 2026-08-04 review loop 1 -- Review found that the authoritative release-package validator checked Commons but could accept stale EventStore dependency metadata. Added all-nine-package EventStore nuspec validation, stale/missing-version rejection tests, selected-assets assertions, and bounded process-helper requirements; this avoids publishing packages compiled against 3.90.0 while declaring an older EventStore line. KEEP: exact Builds/EventStore identities, evaluated central-version reuse, historical 3.89.0 receipts, package-first validation, and the previously green focused package/fitness coverage.

## Design Notes

Resolve `HexalithEventStoreVersion` through the bounded MSBuild-property path used for Commons. Release validation must inspect dependency versions in produced Parties nuspecs, not merely the version of locally packed EventStore support artifacts. Any shared C# resolver must check process start, tolerate exit/kill races, bound post-kill exit and output capture, and fall back to stdout diagnostics when stderr is empty. The whole-worktree gitlink gate will still report unrelated FrontComposer and Memories drift; distinguish those blockers from the newly authorized Builds result.

## Verification

**Commands:**
- `git -C references/Hexalith.EventStore rev-parse HEAD && git -C references/Hexalith.EventStore describe --tags --exact-match HEAD` -- expected: exact SHA `7854f8e…` and `v3.90.0`.
- `pwsh -NoProfile -File references/Hexalith.Builds/Tools/test-authoritative-package-catalog.ps1` -- expected: all catalog invariants pass with EventStore 3.90.0.
- Package-mode solution restore, serialized Release build, release pack, and `scripts/validate-nuget-packages.py` -- expected: succeeds and validates EventStore dependency metadata at 3.90.0.
- Build and directly invoke `Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- expected: all focused fitness tests pass.
- Build and directly invoke `Hexalith.Parties.Ci.Tests.dll` for central-version/documentation tests -- expected: all pass.
- Run Contracts/Client package tests, warning gate, and worktree gitlink gate -- expected: tests pass; gitlink output recognizes Builds while reporting only preserved unrelated drift.

## Suggested Review Order

**Central version adoption**

- Start with the authoritative catalog value consumed across package mode.
  [Directory.Packages.props:8](../../references/Hexalith.Builds/Props/Directory.Packages.props#L8)

- Confirm the adopted Builds revision is explicitly owner-authorized.
  [.gitlink-signoff.tsv:43](../../.gitlink-signoff.tsv#L43)

**Release and restore enforcement**

- Review the per-framework, case-insensitive EventStore dependency gate first.
  [validate-nuget-packages.py:222](../../scripts/validate-nuget-packages.py#L222)

- See how the shared catalog value is evaluated without inline versions.
  [msbuild_properties.py:134](../../scripts/msbuild_properties.py#L134)

- Verify clean consumers reject stale selected EventStore assets.
  [validate-consumer-package-references.py:310](../../scripts/validate-consumer-package-references.py#L310)

- Check bounded process execution shared by package fixtures.
  [PackageTestProcess.cs:48](../../tests/PackageTests.Shared/PackageTestProcess.cs#L48)

**Live evidence and guidance**

- Confirm governed evidence records the exact tag while retaining history.
  [story-8-3-platform-api-prerequisite-matrix.md:43](story-8-3-platform-api-prerequisite-matrix.md#L43)

- Review package-mode guidance for the adopted EventStore baseline.
  [ci.md:71](../../docs/ci.md#L71)

**Regression coverage**

- Inspect synthetic all-package version propagation and rejection cases.
  [CommonsHttpRestoreRoutingTests.cs:365](../../tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs#L365)

- Verify multi-target dependency groups cannot bypass the release gate.
  [CommonsHttpRestoreRoutingTests.cs:635](../../tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs#L635)

- Check deterministic start, timeout, stream, and diagnostic coverage.
  [PackageTestProcessTests.cs:9](../../tests/Hexalith.Parties.Contracts.Tests/Package/PackageTestProcessTests.cs#L9)

- Confirm the fitness test executes the same exact-tag command.
  [PlatformApiPrerequisitesTests.cs:1069](../../tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs#L1069)
