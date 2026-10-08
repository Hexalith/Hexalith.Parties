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
No commit, push, release dispatch, or package publication was performed for this
local fix. A successful full push CI run for the exact resulting current-main
SHA is still required before the normal upstream publication. The local checks
do not replace that gate. Parties can upgrade its package pin only after the
compatible package is actually published, then verify its package-mode Release
build and CI lane.
