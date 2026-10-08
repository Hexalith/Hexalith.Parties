# EventStore CI unblock validation — 2026-10-08

These checks verify the local EventStore patches against base commit
`4cc77f9554395e84539e173e94b8f0b4df14d643`, using SDK 10.0.401, default package
dependencies, Release configuration, and normal warning/audit settings. The
original failures are in [CI run 37742090359](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/37742090359).
Machine-readable commands, results, source hashes, and output hashes are in
[results.json](results.json). Full local outputs remain under
`/tmp/parties-eventstore-upstream-37742090359/`; the receipt does not retain raw
credential material.

## Verified checks

All four affected test projects build with zero warnings and errors:
Contracts, Server, Client, and ProviderVerification. Each exact command is
recorded in `results.json`; the command shape is:

```bash
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release -m:1
```

| Check | Result |
| --- | --- |
| Contracts package-authority class, including sibling project/props rejection | 36 passed |
| Added package-observation seal and tamper test | 1 passed |
| Secret-protection class after review fixes | 70 passed |
| Complete Server suite after review fixes | 3,927 passed; 25 existing skips |
| Client evolution framing class | 26 passed |
| Provider Kestrel credential and unauthorized cases | 3 passed |

The focused secret-protection cases are included in the complete Server suite.
The Contracts class passed before adding the extra seal check; the seal test and
repository-wide positive authority gate were rerun against the final source.
Single-dash `-class` and `-method` options belong to the built xUnit v3 runner,
not `dotnet test` filtering.

## Provider fixture reproduction

The ordinary in-repository Kestrel test command fails locally with
`DirectoryNotFoundException` for its nested FrontComposer Pact fixture. That
nested submodule was not initialized. The existing umbrella sibling is already
at exactly EventStore's declared gitlink commit
`c561b3210f15206a90c39c82c58f2e5b1005cd60`.

After building ProviderVerification.Tests, run from the owning EventStore root:

```bash
upstream_root=$(pwd)
test "$(git -C ../Hexalith.FrontComposer rev-parse HEAD)" = c561b3210f15206a90c39c82c58f2e5b1005cd60
fixture_root=$(mktemp -d)
mkdir -p "$fixture_root/references"
touch "$fixture_root/Hexalith.EventStore.slnx"
ln -s "$upstream_root/src" "$fixture_root/src"
ln -s "$upstream_root/../Hexalith.FrontComposer" "$fixture_root/references/Hexalith.FrontComposer"
cp -a tests/Hexalith.EventStore.ProviderVerification.Tests/bin/Release/net10.0 "$fixture_root/bin"
cd "$fixture_root"
dotnet bin/Hexalith.EventStore.ProviderVerification.Tests.dll -class Hexalith.EventStore.ProviderVerification.Tests.RealKestrelPactTests
```

Copying the built output is required because the xUnit runner chooses its
assembly directory as the working directory. This fixture validates matching
and mismatched per-run credentials and the unauthorized interaction through
production routes. It is not a full ProviderVerification suite or a release
identity attestation.

## Evidence preservation and remaining release work

The two newly excluded historical build inputs remain byte-preserved and have
pinned SHA256 checks before the authority validator runs. The serialization XML
capture also remains preserved and is exempt only at its exact sealed path/hash.
Seventeen OTEL header credential occurrences were replaced with the inert
redaction marker in the recorded JSON capture. A byte comparison verified that
these replacements are the only changes to that capture; its execution manifest
matches the resulting size and SHA256. The upstream sanitization receipt records
both hashes and the replacement count.

Independent review produced six findings; all were patched and documented in
the [implementation spec](../../spec-unblock-eventstore-api-publication.md).
At the local-fix checkpoint, publication had not been authorized or performed.
The user subsequently authorized commit/push, exact-source full CI, normal
publication, and the Parties consumer upgrade.

## Approved publication follow-through

Concurrent workspace work committed and pushed the reviewed patch as
`3600d196799b7bbc5398a6d126e918171f07a2d3`; no duplicate commit was created.
Its exact-source full CI passed. An initial normal release stopped before NuGet
publication when current main advanced. The new selected main
`b830d9829af70536d2a3fd21c5e2a23b2ca2f256` then passed
[full push CI 37749252540](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/37749252540),
including the six cases from the three required sidecar-security tests recorded
in [next-sidecar-security-ci.json](next-sidecar-security-ci.json).

[Normal Release 37750172086](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/37750172086)
succeeded with `bypass-validation=false` and published
[v3.117.0](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.117.0).
The current-main, exact-source full-CI, and publication identity gates passed.
The release source-gate receipt, immutable OCI validation, and amd64/arm64 smoke
results are preserved under [release-evidence](release-evidence/preflight/publication-identity.json).
The run summary is [release-success.json](release-success.json).

NuGet accepted all 14 uploads, then exposed the packages after asynchronous
processing. All 14 were downloaded from its public feed and verified against the
exact release source. Their hashes and the published generic sidecar API are in
[the Parties upgrade receipt](../eventstore-package-upgrade-2026-10-08/README.md).
The ordinary consumer Release build now passes with zero warnings and errors.
This publication does not replace the separate exact-current-main Parties full
CI gate or establish integrated-topology runtime acceptance.
