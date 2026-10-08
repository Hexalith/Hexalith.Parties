# EventStore package/API prerequisite verification — 2026-10-08

The publication handoff is documented in [docs/ci.md](../../../../docs/ci.md#required-eventstore-publication).
The required API is implemented upstream but has no compatible published package
as of this observation. Parties' package-mode Release build and release remain
blocked. The earlier [2026-10-07 receipt](../eventstore-package-api-2026-10-07/README.md)
is retained as historical evidence.

## Package and source evidence

[package-probe.json](package-probe.json) records nine live NuGet v3 index probes
with UTC observation times and response hashes. DomainService, ServiceDefaults,
Client, Contracts, Server, Testing, Aspire, SignalR, and Gateway all end at
3.115.0, with no prereleases or later versions. The freshly downloaded
DomainService package and DLL match the previous receipt's hashes and the DLL
still lacks the `RequireEventStoreSidecarChannel` metadata name. These nine
identities are the inspected Parties consumer subset; the complete publisher
inventory currently contains 14 packages, as linked in the publication handoff.

The original index contents are retained in [nuget-indexes](nuget-indexes/).
They were captured again during review and match the initial probe hashes byte
for byte. To capture a fresh observation without overwriting this receipt, run:

```bash
python3 _bmad-output/implementation-artifacts/tests/eventstore-package-api-2026-10-08/probe-package.py
```

[probe-package.py](probe-package.py) uses only Python's standard library and prints
the new temporary output directory. It retains each index response, downloads the
3.115.0 package without installing it, reads the nuspec repository commit, and
checks the DLL for the metadata-name bytes. An absent name proves this assembly
cannot provide the API; a present name alone would not prove a usable public
generic method. The future published-package handoff therefore also requires
public API/dependency inspection and a successful consumer build.

The package's repository commit and the resolved upstream `v3.115.0` tag are both
`283b07a52c9c70e1c940164a7011ee8c3ad98b2d`. Source history first adds the API at
[`c4d5455a3b79ca1ba0113a286cf2432b2cace7fb`](https://github.com/Hexalith/Hexalith.EventStore/commit/c4d5455a3b79ca1ba0113a286cf2432b2cace7fb).
[upstream-history.json](upstream-history.json) records the exact argument arrays,
UTC times, outputs, and successful exit codes for the following read-only tag,
introducing-commit, and ancestry checks:

```bash
git -C references/Hexalith.EventStore rev-parse 'v3.115.0^{commit}'
git -C references/Hexalith.EventStore log --format='%H %s' -S 'RequireEventStoreSidecarChannel' -- src/Hexalith.EventStore.DomainService/EventStoreDomainServiceSecurityExtensions.cs
git -C references/Hexalith.EventStore merge-base --is-ancestor 283b07a52c9c70e1c940164a7011ee8c3ad98b2d c4d5455a3b79ca1ba0113a286cf2432b2cace7fb
```

[dependency-mode.json](dependency-mode.json) records the MSBuild evaluation
command and asset hash. The Parties domain host selects
`UseNuGetDeps=true`, `UseHexalithProjectReferences=false`, and
`HexalithEventStoreVersion=3.115.0`; Client, Contracts, DomainService, and
ServiceDefaults resolve as packages. It has no EventStore project reference.
The source orchestration projects and existing Commons fallbacks do not supply
this missing API to the package consumer.

## Validation

All commands used .NET SDK 10.0.401 and default package mode; builds and the CI
lane used Release. No warning override, audit disablement, version override,
or source-mode switch was used. [command-results.json](command-results.json)
records exact commands, UTC times, exit codes, durations, log hashes, and
sanitized compiler diagnostics.

| Command | Exit | Result |
| --- | --- | --- |
| `dotnet restore Hexalith.Parties.slnx` | 0 | Restore succeeds. |
| `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1` | 1 | 0 warnings, 2 CS1061 errors; 15.87 seconds. |
| `dotnet build src/Hexalith.Parties/Hexalith.Parties.csproj --configuration Release --no-restore -m:1` | 1 | Same 2 missing-API errors, 0 warnings; 1.83 seconds. |
| `pwsh -NoProfile -File scripts/test.ps1 -Lane ci -ResultsDirectory _bmad-output/implementation-artifacts/tests/eventstore-package-api-2026-10-08` | 0 | 101 passed, 0 failed, 0 skipped. |
| `bash scripts/check-no-warning-override.sh` | 0 | No warning-override or nested-submodule regressions. |

Both builds report the missing `RequireEventStoreSidecarChannel` extension for
`IEndpointConventionBuilder` at `src/Hexalith.Parties/Program.cs:64` and `:66`.
[ci-results.json](ci-results.json) retains sanitized per-test results and TRX
counters. After the documentation update, the built CI documentation-contract
test was rerun directly with the xUnit v3 `-method` filter: 1 passed, 0 failed,
0 skipped; its exact command and log hash are also in `command-results.json`.
Passing this local CI test lane verifies workflow contracts; it does not
establish successful full `ci.yml` for the exact current main commit.
That full-CI proof remains mandatory before Release.

Raw restore/build/test/guard logs and the TRX remain local, ignored artifacts
beside this receipt; their contents include local paths and are not publication
artifacts. The JSON receipts omit those paths and captured test output.

## Source provenance and handoff boundary

The validation started at Parties revision
`450b1ddb6dde38a4431f300033d2c7d3eba27830`, which was committed externally during
workflow startup and contains the earlier handoff. [source-before.json](source-before.json)
and [source-after.json](source-after.json) record the observed revisions and the
same root checkout identities. External work subsequently advanced Parties to
`f2886c604f642c5d730f8b615a554206a2b35300` by adding evidence JSON files only;
the runtime/build/release inputs in [input-files.json](input-files.json) stayed
unchanged. In particular, Builds is
`af20682ac8fc420068a731ecb87cff84727a3d53` and the EventStore source checkout is
`91aae06d49a23bac15be6a4bb363f37140bece4b`.

[input-files.json](input-files.json) confirms the host security calls, central
package pin, dependency-selection configuration, CI/release workflows, release
preflight wrapper, and test runner match the recorded revision. This run only
refreshes documentation and workflow evidence; it makes no dependency,
submodule, host, release-gate, commit, or publication change.

EventStore owners must publish a validated coordinated package family containing
the introducing commit and ServiceDefaults authentication support. Parties can
then select the actual published version and verify package-mode restore,
Release solution build, test lanes, and gateway/sidecar credentials in the full
topology. Publication alone does not prove runtime or release readiness.

## Upstream publication input

This applies to the EventStore owner's manual `release.yml` only: its
`workflow_dispatch` input `bypass-validation` must be `false` for this handoff.
That selects successful exact-source push `ci.yml`; the Commitlint-only selection
is insufficient. The upstream owner must return the full-CI/test and release
artifacts described in `docs/ci.md`. No release dispatch was performed by this
workflow. Parties' own release gate always requires full CI for its exact
current main commit.

The final documentation-contract check initially rejected the upstream input's
literal name in `docs/ci.md`, whose contract excludes the retired Parties bypass
name anywhere in that file. The EventStore-only spelling is retained here and
linked from the CI handoff; the existing test and release gates were preserved.

After that correction, the focused documentation test passed and the full CI
lane was rerun against the final documentation: **101 passed, 0 failed, 0
skipped**. [ci-results-final.json](ci-results-final.json) records the final TRX
hash, counters and run times, with the same test names/outcomes as the initial
run. [source-final.json](source-final.json) records the final observed revision
and confirms the stamped runtime/build/release inputs and root checkout set
remain unchanged. The broad Release build remains blocked by the two missing-API
errors already reproduced; no successful full-CI release proof is claimed.
