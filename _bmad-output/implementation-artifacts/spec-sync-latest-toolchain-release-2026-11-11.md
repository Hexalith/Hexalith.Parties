---
title: 'Synchronize the published toolchain for release'
type: 'bugfix'
created: '2026-11-11'
status: 'in-review'
review_loop_iteration: 0
baseline_commit: 'e16c882e4cd9ba5969488ec8d3430e5a79f29d0f'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/docs/development-guide.md'
---

<frozen-after-approval reason="user explicitly authorized updates and bypassing human validation">

## Intent

**Problem:** Parties still pins SDK 10.0.403 and older published module/AppHost tools. The root central catalog has two compatible package upgrades available; standalone fallback versions lag the selected catalog. Previous hosted CI failed and the next release needs current, verified inputs.

**Approach:** Pin latest stable .NET SDK 11.0.100 while retaining net10.0 compatibility. Upgrade the compatible published package and tool pins, align active documentation and executable expectations, build the real package graph, and fix concrete CI failures. The parent session owns commits, pushes, submodule synchronization and release/publication orchestration after this implementation.

## Boundaries & Constraints

**Always:** Preserve user changes and historical evidence. Follow owning repository instructions. Keep central package management in Hexalith.Builds. Use existing tests and gates, serialized builds and individual test projects. User has explicitly authorized all necessary updates, pushes and release, without human checkpoints. Keep production checks meaningful. Leave child implementation changes uncommitted for parent review and ordered submodule commits.

**Ask First:** No additional approval is required for this authorized maintenance. Report any unavailable external capability with exact evidence instead of claiming success.

**Never:** Initialize nested submodules, load skills beneath references, weaken warnings/tests/publication checks, invent owner approvals, print credentials, retarget to net11.0 solely to consume ASP.NET 11 packages, rewrite dated evidence or historical fixed package qualification cohorts, remove environment protections.

</frozen-after-approval>

## Code Map

- `global.json` -- current SDK 10.0.403, MTP runner, latestPatch roll-forward; Microsoft metadata verifies latest stable SDK 11.0.100.
- `.config/dotnet-tools.json` -- module CLI 4.47.0; public latest 4.48.0; aspirate9.1.0 remains latest.
- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` -- Aspire AppHost SDK13.6.4 while selected Aspire libraries already13.6.5.
- `scripts/prepare-ci-package-dependencies.sh` -- temporary AppHost and DCP/dashboard paths must match13.6.5.
- `references/Hexalith.Builds/Props/Directory.Packages.props` -- authoritative family Tenants7.0.6 to7.0.7 and Microsoft.NET.Test.Sdk18.11.1 to18.12.0; other referenced compatible stable packages current. Preserve BOM and CRLF. Its owning package/commitlint instructions apply; no skills beneath references.
- `Directory.Packages.props` -- standalone fallbacks Tenants7.0.5 and Memories3.0.25 should match7.0.7 and3.0.26 without overriding caller-selected catalogs.
- `Directory.Build.props` -- UI runtime minimum10.0.14 can advance to serviced10.0.15; target remainsnet10.0.
- `Directory.Build.targets` -- exact Memories capability selector currently excludes3.0.26; preserve fail-closed semantics unless existing selected-package protocol controls establish compatibility.
- `.github/workflows/current-owner-startup-diagnostics.yml`, `module/`, `tests/`, `README.md`, `docs/` -- active tool-version expectations/documentation need matching pins. Preserve intentionally historical cohort expectations.
- `Hexalith.Parties.Standalone.slnx`, `scripts/test.ps1`, `tests/README.md` -- real release/package build and individual test lanes.
- `/tmp/parties-sync-release-20261111/dependency-summary.json`, `nuget-audit.json`, `npm-audit.json` -- read-only registry audit. All npm dependencies current within pinned Node24.22.1; ASP.NET10.0.15 is latest compatible net10 cohort.

## Tasks & Acceptance

**Execution:**
- [ ] `global.json`, `.config/dotnet-tools.json`, AppHost project and preparation script -- align published toolchain versions and dependent active expectations.
- [ ] `references/Hexalith.Builds/Props/Directory.Packages.props`, `Directory.Packages.props`, `Directory.Build.props` -- upgrade compatible package families and runtime servicing; validate central versions.
- [ ] `README.md`, active `docs/` toolchain guidance -- describe the effective SDK and tools without rewriting historical receipts.
- [ ] Actual affected source/tests/workflows -- fix proven build or CI failures while retaining behavioral contracts.
- [ ] Existing build and tests -- validate affected package configuration and summarize exact pass/fail evidence; leave logs under the private temporary receipt directory.

**Acceptance Criteria:**
- Given an independent checkout, when SDK resolution runs, then it selects stable11.0.100 while Parties packages remain compatible withnet10.0.
- Given the updated central catalog and wrapper, when projects restore, then affected direct dependencies resolve to current compatible published versions and package references stay centrally versioned.
- Given package mode, when the owning test projects execute, then affected controls pass and unsupported runtime assumptions are reported explicitly.
- Given prior failed CI, when the failure is reproducible or logs available, then fixes address its actual cause and preserve release admission checks.

## Spec Change Log

## Design Notes

SDK updates do not imply a target-framework migration. Preserve intentional13.6.2-beta.959 Dapr integration and Node24 typings, as registry latest majors belong to different compatibility cohorts. Newly published Memories capabilities require actual protocol qualification before enabling exact selectors. Parent runs final remote CI and release checks; this implementation reports local scope honestly.

## Verification

**Commands:**
- `dotnet --version` --11.0.100.
- `bash scripts/check-no-warning-override.sh` --success.
- `dotnet build Hexalith.Parties.Standalone.slnx --configuration Release -m:1` --zero errors/warnings in package mode, or exact first-failure evidence.
- `dotnet test --project tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj --configuration Release` --existing CI contracts pass; run other individually affected projects as needed.
- `git diff --check` --no introduced whitespace defects; owning repositories checked separately.
