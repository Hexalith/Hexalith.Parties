---
title: 'Fix package version expansion in CI package validation'
type: 'bugfix'
created: '2026-07-16'
status: 'done'
review_loop_iteration: 0
baseline_commit: '4378dede55d92e489caf7aad63d6c2892e6f856d'
source_run: 'https://github.com/Hexalith/Hexalith.Parties/actions/runs/29457973512'
context:
  - '{project-root}/project-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09-g12-package-publishing-source-mode-ci.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-gh-86088119701-fix-ci-restore.md'
---

# Fix package version expansion in CI package validation

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The solution Restore and exact Release Build pass, but the package-consumer gate passes the literal `$(HexalithCommonsVersion)` to `dotnet pack`. MSBuild does not recursively expand that command-line property, so Commons.Http receives an invalid package version; the NuGet validator encodes the same unresolved token and the consumer validator uses stale Commons support versions.

**Approach:** Resolve `HexalithCommonsVersion` to its concrete evaluated MSBuild value before invoking package tools, then use that single central value consistently for Commons publication, generated dependency metadata, and package-consumer validation.

## Boundaries & Constraints

**Always:** Keep package validation in publication mode and retain the narrow Commons.Http source fallback; fail before packing if the evaluated version is empty or unresolved; publish Commons.Http and Commons.UniqueIds at the version used by Parties metadata; preserve package inventory, public contracts, warnings-as-errors, and validation gates; modify root-owned files only.

**Ask First:** Changes to shared `Hexalith.Builds` or other submodule content; changes to published package inventory, public API, or broad dependency routing.

**Never:** Duplicate or hardcode the current Commons version in root scripts; pass an MSBuild expression as a literal command-line value; omit the override and accept the default `1.0.0`; weaken package validation; initialize nested submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Current central version | `HexalithCommonsVersion` evaluates to `2.28.1` | Commons support packages and Parties dependencies use `2.28.1` | N/A |
| Invalid property result | Evaluated value is empty or contains an MSBuild expression | Script stops before package generation | Clear bounded diagnostic |
| Central version bump | Central property changes | Root scripts consume the new evaluated value without hardcoded edits | N/A |
| Consumer local feed | Parties packages require the central Commons version | Commons.Http and Commons.UniqueIds are packed at that version and consumers restore | Restore failure remains fatal |

</frozen-after-approval>

## Code Map

- `scripts/msbuild_properties.py` -- shared fail-closed resolver for evaluated MSBuild properties.
- `scripts/pack-release-packages.py` -- publishes Parties packages with concrete Commons.Http dependency metadata.
- `scripts/validate-nuget-packages.py` -- validates generated NuGet dependency metadata.
- `scripts/validate-consumer-package-references.py` -- assembles a local feed and restores sample consumers.
- `Directory.Build.props` -- currently contains an ineffective early Commons.Http version default.
- `tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs` -- static CI contract for Commons.Http routing and packaging.

## Tasks & Acceptance

**Execution:**

- [x] `scripts/msbuild_properties.py` -- query the evaluated central Commons version through `dotnet msbuild -getProperty` and reject empty or unresolved values.
- [x] `scripts/pack-release-packages.py` -- pass the resolved version as the explicit Commons.Http package override.
- [x] `scripts/validate-nuget-packages.py` -- validate Commons.Http dependencies against the resolved version.
- [x] `scripts/validate-consumer-package-references.py` -- pack Commons.Http and Commons.UniqueIds at the resolved version.
- [x] `Directory.Build.props` -- remove the ineffective early Commons.Http package-version default.
- [x] `tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs` -- distinguish the central alias from its concrete value, forbid unresolved script literals, and cover every matrix row.

**Acceptance Criteria:**

- Given the current central properties, when the complete package gate runs, then intended packages pack, NuGet metadata validates, and sample consumers restore.
- Given generated Parties package metadata, when Commons.Http dependencies are inspected, then their version is concrete and contains no `$(` token.
- Given an empty or unresolved central property, when the resolver runs, then it fails before package generation with a clear error.
- Given a new commit containing the fix, when GitHub Actions runs, then `Validate package consumer references` succeeds without weakening later test gates.

## Spec Change Log

## Design Notes

The resolver should query the project evaluation used by the package scripts rather than parse shared package XML. This preserves central-property indirection and fails closed at the external-process boundary. All affected scripts must share the resolver so version drift cannot recur along one path.

## Verification

**Commands:**

- `dotnet restore Hexalith.Parties.slnx` -- expected: restore succeeds.
- `dotnet build Hexalith.Parties.slnx --no-restore --configuration Release -warnaserror` -- expected: exact Release build succeeds without warnings.
- `dotnet build tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj --configuration Release -warnaserror` -- expected: CI tests compile.
- `dotnet tests/Hexalith.Parties.Ci.Tests/bin/Release/net10.0/Hexalith.Parties.Ci.Tests.dll -class Hexalith.Parties.Ci.Tests.CommonsHttpRestoreRoutingTests` -- expected: focused routing tests pass.
- `python3 scripts/pack-release-packages.py ./nupkgs 0.0.0-ci-test` -- expected: all intended packages are produced.
- `python3 scripts/validate-nuget-packages.py ./nupkgs` -- expected: dependency metadata passes.
- `python3 scripts/validate-consumer-package-references.py ./nupkgs` -- expected: package-mode sample consumers restore.
- `dotnet tests/Hexalith.Parties.Ci.Tests/bin/Release/net10.0/Hexalith.Parties.Ci.Tests.dll` -- expected: full CI suite passes.

## Suggested Review Order

**Central version resolution**

- Reject malformed evaluations and keep diagnostics bounded before package tooling runs.
  [`msbuild_properties.py:36`](../../scripts/msbuild_properties.py#L36)

- Query evaluated MSBuild state with controlled launch and timeout failure handling.
  [`msbuild_properties.py:74`](../../scripts/msbuild_properties.py#L74)

**Package propagation and validation**

- Feed one resolved Commons version into every release package invocation.
  [`pack-release-packages.py:33`](../../scripts/pack-release-packages.py#L33)

- Require exact Http and UniqueIds dependency versions in generated Parties metadata.
  [`validate-nuget-packages.py:166`](../../scripts/validate-nuget-packages.py#L166)

- Reject missing or mismatched Commons support artifacts before consumer restore.
  [`validate-consumer-package-references.py:93`](../../scripts/validate-consumer-package-references.py#L93)

- Assemble package-only consumers from the same evaluated central version.
  [`validate-consumer-package-references.py:291`](../../scripts/validate-consumer-package-references.py#L291)

**Routing and regression guards**

- Preserve the narrow Commons.Http source fallback without an ineffective version default.
  [`Directory.Build.props:27`](../../Directory.Build.props#L27)

- Exercise invalid resolver outputs through the production boundary.
  [`CommonsHttpRestoreRoutingTests.cs:127`](../../tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs#L127)

- Prove a synthetic central bump flows through all three script entry points.
  [`CommonsHttpRestoreRoutingTests.cs:294`](../../tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs#L294)

- Verify exact Commons support metadata and missing-package failures.
  [`CommonsHttpRestoreRoutingTests.cs:424`](../../tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs#L424)
