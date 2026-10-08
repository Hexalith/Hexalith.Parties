# EventStore 3.117.1 consumer verification — 2026-10-08

Parties now selects the public EventStore package family `3.117.1`. All 14
published packages match release source
`0dad344d37343f589d859d6d8d6701283122b338`; the DomainService assembly exposes
`RequireEventStoreSidecarChannel<TBuilder>`. Serialized package-mode restore and
the Release solution build pass with zero warnings and errors. The publication,
API and compiler handoff for Story 8.8 prerequisite P1 is resolved.

## Published package and consumer identity

[EventStore v3.117.1](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.117.1)
was published on 2026-10-08. [published-package-proof.json](published-package-proof.json)
records public NuGet URLs, package SHA256 hashes, nuspec source commits, exact
family dependency versions, the release manifest hash and API metadata. The release
source descends from API-introducing commit
`c4d5455a3b79ca1ba0113a286cf2432b2cace7fb`. The generic extension is public/static,
has a public declaring type, extension attribute, `TBuilder` receiver and return,
and an `IEndpointConventionBuilder` constraint. The probe checks metadata and
signature presence; it does not independently validate signatures.

[consumer-graph.json](consumer-graph.json) records the evaluated domain-host
properties/references and verifies its output DomainService DLL against the
actual public package DLL. [resolved-family.json](resolved-family.json) contains
all 29 restored consumer graphs; every EventStore family package is `3.117.1`.
The host remains in package mode with `UseNuGetDeps=true` and
`UseHexalithProjectReferences=false`. Existing orchestration and Commons source
fallbacks keep their separate scope. Package version expectations are updated;
source identity expectations remain separate from package selection.

## Validation and limits

[results.json](results.json) records exact commands, exit codes, per-project
counters, XML/log hashes, build input manifests and root dependency identities.
The following tests ran directly from freshly built Release assemblies:

- 1,923 unit cases across all 11 unit projects passed, without skips.
- 101 CI contract cases passed, without skips.
- 35 sidecar-security cases and 10 documentation fitness cases passed, without skips.
- The warning/nested-submodule guard passed.
- The one current-source identity case failed at the existing Builds mismatch:
  the test/matrix expects `ad52c5bdd4361c59eedf12a16620150006403584`, but the root
  committed gitlink is `893db14b25843db140942d839e4d659584221315`. The workspace
  checkout is `6f07763bd955d22ace0123798add528dc933bf51`. The mismatch predates
  this package patch; its guard remains intact and no source pointer was changed.

This is working-tree evidence at Parties
`890119f1b4ced37ccec34787c812f064199fb3ef`. All 858 root source/build/test inputs
and all ten root dependency identities/statuses were unchanged between the
pre-build and post-test captures. Existing user changes to dependency checkouts
and `eng/verify-ext-parties-1.ps1` were preserved. Full input manifests and XML
results remain under `/tmp/parties-eventstore-3.117.1-2026-10-08/`; their hashes
are retained in the receipt. Restore/build output is retained in
[restore.log.txt](restore.log.txt) and [build.log.txt](build.log.txt), with only
line-ending/trailing-whitespace normalization; original and retained hashes are recorded.

The initial default restore remained silent for 162 seconds and was interrupted.
Only its subsequent successful serialized retry is accepted as restore proof.
The normal unit-lane script also stalled during restore and was terminated
(exit 143); the full unit and CI contract assemblies then passed through the
direct runner. These process stalls are recorded separately from package results.
No warning/audit gate was weakened.

Story 8.8 remains blocked on Story 8.7/G5 and its row identity, owner approval,
continuity, parity and runtime topology requirements. The frozen consumer intent
is unchanged. Runtime credential wiring, no-skips topology acceptance and
successful exact-source Parties full CI remain separate gates. This receipt
neither activates a production migration nor authorizes publication.
The prior [3.117.0 receipt](../eventstore-package-upgrade-2026-10-08/README.md)
retains its original normal-release/consumer proof.

## Reproduce

From the Parties root, use serialized restore/build without server reuse:

```bash
DOTNET_CLI_USE_MSBUILD_SERVER=0 dotnet restore Hexalith.Parties.slnx --disable-parallel -m:1 -nr:false
DOTNET_CLI_USE_MSBUILD_SERVER=0 dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1 -nr:false
```

Use the repository unit/CI lanes normally. If their restore phase stalls, invoke
each built Release assembly individually with the exact commands in `results.json`.
For example:

```bash
dotnet tests/Hexalith.Parties.Client.Tests/bin/Release/net10.0/Hexalith.Parties.Client.Tests.dll -result-xml /tmp/parties-client.xml
dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartiesDomainServiceSecurityTests -result-xml /tmp/parties-sidecar.xml
bash scripts/check-no-warning-override.sh
```

Reuse the existing BCL-only package verifier and graph-capture script from the
[3.117.0 receipt](../eventstore-package-upgrade-2026-10-08/README.md#reproduce-the-public-package-probe),
with probe arguments `3.117.1` and `0dad344d37343f589d859d6d8d6701283122b338`.
Write fresh results to a temporary directory; retain historical receipts.
