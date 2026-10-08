# EventStore package upgrade — 2026-10-08

Parties now selects the publicly published EventStore package family `3.117.0`.
The ordinary package-mode solution restore and Release build pass with zero
warnings and errors, using SDK `10.0.401` and normal warning/audit settings.
The unchanged host calls to `RequireEventStoreSidecarChannel` compile against the
published DomainService assembly. The prior `3.115.0` observation remains in
[the historical receipt](../eventstore-package-api-2026-10-08/README.md).

## Published identity and API

[EventStore v3.117.0](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.117.0)
selects source `b830d9829af70536d2a3fd21c5e2a23b2ca2f256`, following successful
[full push CI](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/37749252540)
and [normal Release](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/37750172086).
The upstream validation-bypass input was false; source gates and six passing
security cases are retained in [the publication receipt](../eventstore-ci-unblock-2026-10-08/README.md).

[published-package-proof.json](published-package-proof.json) records public-feed
URLs, nuspec repository commits, dependencies, and SHA256 hashes for all 14
packages in the manifest read from the exact release commit, with its SHA256.
Each package has the exact release version and
repository commit. Every EventStore dependency selects the same release version.
The source descends from the API-introducing commit
`c4d5455a3b79ca1ba0113a286cf2432b2cace7fb`.

The DomainService assembly metadata exposes public static
`EventStoreDomainServiceSecurityExtensions.RequireEventStoreSidecarChannel<TBuilder>`
with a public declaring type, the extension attribute, a `TBuilder` receiver and
return type, and an `IEndpointConventionBuilder` generic constraint. Its
ServiceDefaults dependency selects `3.117.0`. SHA256 identities:

- DomainService nupkg: `92946a2cbb05f186937d11426e80f77ca14694d0acab8c0e02b75f75c140ce98`
- DomainService DLL: `a7d0238939bd7cb877fa2cf649f73a8141ca935f8e473cdf786c45997a9b1db6`

Signature presence is metadata evidence; this probe does not independently
validate signatures. Normal NuGet restore performs its standard package checks.
The probe downloads bytes without installing packages or creating a local feed.

## Consumer verification

[dependency-mode.json](dependency-mode.json) records the domain host's evaluated
package/project references and properties. It selects `UseNuGetDeps=true`,
`UseHexalithProjectReferences=false`, and `HexalithEventStoreVersion=3.117.0`.
[resolved-family.json](resolved-family.json) lists all 29 restored consumer asset
graphs containing EventStore packages; every listed family asset is `3.117.0`.
The solution's orchestration source dependencies and existing Commons source
fallbacks retain their documented scope.

[input-files.json](input-files.json) records the pre-commit Parties revision and
build inputs. The dependency identity fitness check requires a committed root
EventStore pointer, so that check runs after the upgrade commit and before push.
All 1,918 unit cases and 101 CI contract cases passed without skips. The focused
security class passed 35 cases and the documentation class passed 10. The wider
consumer integration assembly passed 906 cases, excluding only the current-root
identity case reserved for the post-commit check. These two focused classes are
included in those 906 integration cases. [results.json](results.json) records
commands, counters, result hashes, and the final aligned-AppHost build.

Local logs and TRX outputs are under
`/tmp/parties-eventstore-package-upgrade-2026-10-08/`.

Reproduce the ordinary consumer checks from the Parties repository root:

```powershell
dotnet restore Hexalith.Parties.slnx
dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1
pwsh -NoProfile -File scripts/test.ps1 -Lane unit -Configuration Release
pwsh -NoProfile -File scripts/test.ps1 -Lane ci -Configuration Release
```

Run the sidecar-security and documentation fitness classes against the built
package-mode test assembly:

```bash
dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartiesDomainServiceSecurityTests
dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests
bash scripts/check-no-warning-override.sh
```

## Reproduce the public package probe

Copy the standalone BCL-only verifier into a temporary directory before building
so repository build imports do not alter its environment. From the Parties root:

```bash
receipt=_bmad-output/implementation-artifacts/tests/eventstore-package-upgrade-2026-10-08
verifier_dir=$(mktemp -d)
cp "$receipt/api-verifier/Program.cs" "$receipt/api-verifier/Verifier.csproj" "$verifier_dir/"
dotnet build "$verifier_dir/Verifier.csproj" --configuration Release
output_dir=$(mktemp -d)
python3 "$receipt/probe-published.py" 3.117.0 b830d9829af70536d2a3fd21c5e2a23b2ca2f256 "$output_dir" references/Hexalith.EventStore "$verifier_dir/bin/Release/net10.0/Verifier.dll"
```

This compiler/package receipt does not assert integrated-topology acceptance,
refresh historical migration parity, or authorize a Parties publication. Runtime
credential wiring and required topology proof remain documented in
[ci.md](../../../../docs/ci.md#required-eventstore-publication). A Parties release
still requires successful full CI for its exact current-main source.


## Replay graph and source observations

After restoring/building the selected graph, run this command from its repository
root. It writes fresh receipts in a new directory instead of altering historical
receipts, using the exact MSBuild queries and asset extraction in the script:

```bash
output_dir=$(mktemp -d)
python3 _bmad-output/implementation-artifacts/tests/eventstore-package-upgrade-2026-10-08/capture-consumer.py "$output_dir"
```

`source-inputs.json` records every root source/test/sample input known to Git,
including untracked nonignored source files, the current commit/status, and each
root dependency's committed and checked-out identity/status. Capture it before
and after checks to detect concurrent edits. The initial `input-files.json`
records selected inputs only; it does not attest the whole dirty workspace used
by the initial local checks. The committed upgrade is separately verified in an
isolated checkout before push, with matching before/after full source manifests.

Sanitized per-case outcomes and counters are retained in
[test-results.json](test-results.json), omitting runtime console output and
attachments. Exact restore/build outputs are retained under
[build-logs](build-logs/build-aligned-apphost.log.txt). Original result hashes refer
to the temporary full outputs; the retained summaries are separately inspectable.
