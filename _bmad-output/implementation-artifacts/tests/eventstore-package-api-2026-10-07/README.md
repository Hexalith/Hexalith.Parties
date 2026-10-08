# EventStore package/API prerequisite verification

The publication handoff is complete; Parties full CI remains blocked until
EventStore publishes the required API. No compatible version was available when
the NuGet indexes were checked. No package pin, host code, CI workflow, release
gate, project-reference configuration, or submodule pointer was changed by this
work.

## Package and upstream evidence

[`package-probe.json`](package-probe.json) records the UTC observation time,
NuGet v3 endpoints, version counts, and latest version for the nine installed
EventStore package identities. Each index ends at 3.115.0 and contains no
prereleases or later versions.

The downloaded DomainService 3.115.0 package records repository commit
`283b07a52c9c70e1c940164a7011ee8c3ad98b2d`. Its SHA-256 is
`c3821b9099dcaee1df2887e1f4cb26ecaf38d11f87a381c2b5b899948c2a48f9`;
the assembly lacks the `RequireEventStoreSidecarChannel` metadata name.
The source history adds that symbol in
[`c4d5455a3b79ca1ba0113a286cf2432b2cace7fb`](https://github.com/Hexalith/Hexalith.EventStore/commit/c4d5455a3b79ca1ba0113a286cf2432b2cace7fb),
after the released package commit. The required action is an EventStore-owned
publication of the coordinated package family containing that change and its
supporting ServiceDefaults authentication implementation. See
[`docs/ci.md`](../../../../docs/ci.md#required-eventstore-publication).

[`dependency-mode.json`](dependency-mode.json) records MSBuild evaluation of the
Parties domain host and the resolved EventStore assets. The evaluation command was
`dotnet msbuild src/Hexalith.Parties/Hexalith.Parties.csproj -p:Configuration=Release -getProperty:UseNuGetDeps,UseHexalithProjectReferences,HexalithEventStoreFromSource,HexalithEventStoreVersion -getItem:PackageReference,ProjectReference`.
Restored asset types were read from `src/Hexalith.Parties/obj/project.assets.json`. Package mode selects
3.115.0, with no EventStore project reference from that host. The solution also
builds source orchestration projects and existing Commons source fallbacks;
those do not supply the API to Parties' package consumer.

## Verification

All commands ran from the Parties repository with .NET SDK 10.0.401, Release
configuration, and default package-mode settings. No warning override,
source-mode switch, audit disablement, or version override was used.

| Command | Exit | Result |
| --- | --- | --- |
| `dotnet restore Hexalith.Parties.slnx` | 0 | Restore succeeds. |
| `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1` | 1 | 0 warnings, 2 CS1061 errors at `Program.cs:64` and `Program.cs:66`; 42.61 seconds. |
| `dotnet build src/Hexalith.Parties/Hexalith.Parties.csproj --configuration Release --no-restore -m:1` | 1 | Same 2 missing-API errors, 0 warnings; 7.62 seconds. |
| `pwsh -NoProfile -File scripts/test.ps1 -Lane ci -ResultsDirectory /tmp/parties-eventstore-ci-results` | 0 | 101 passed, 0 failed, 0 skipped. |
| `bash scripts/check-no-warning-override.sh` | 0 | No warning-override or nested-submodule regressions. |

The focused host build reports:

```text
Program.cs(64,27): error CS1061: 'IEndpointConventionBuilder' does not contain a definition for 'RequireEventStoreSidecarChannel'
Program.cs(66,25): error CS1061: 'IEndpointConventionBuilder' does not contain a definition for 'RequireEventStoreSidecarChannel'
```

The CI lane includes static package-routing checks and executable release-source
preflight fixtures. It verifies rejection of missing full CI, mismatched source
SHA/ref, and retired bypass values. These 101 local tests are separate from the
mandatory successful exact-current-main push run of the complete `ci.yml`.
Release remains blocked by the failing solution build.

Raw `restore.log`, `release-build.log`, `focused-host-build.log`, and `ci-lane.log`
were copied alongside this receipt for local inspection; repository ignore
rules exclude logs. The local TRX is
`/tmp/parties-eventstore-ci-results/Hexalith.Parties.Ci.Tests.trx`.

## Source identity and concurrent changes

The Parties revision is `2b8a1eff5cbfa2b7561d36a4d54c65cbf684051e`.
The Builds catalog remains at `af20682ac8fc420068a731ecb87cff84727a3d53`.
During verification, external work advanced the EventStore checkout from the
tracked `40c92e085d8a6463d469c1b340c410fec84a690f` to
`46a96f6a0769a807b3678fc47380e7c0b5b06589` and also changed the Platform
checkout. Those changes were preserved. The broad solution receipt therefore
does not certify a frozen source-submodule topology; the subsequent focused
host build independently confirms the unchanged 3.115.0 package/API blocker.
