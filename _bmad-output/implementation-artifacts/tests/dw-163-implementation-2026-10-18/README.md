# DW-163 implementation verification — 2026-10-18

**Historical candidate:** independent review rejected this candidate. Its recorded results apply only to its source hashes, and its linked template reads the current working tree. Final evidence is [review loop 1](../dw-163-review-loop-1/README.md).

The spec baseline remains Parties `6b0641829b68d22656fb173eec6578c65e8e91f0`.
During implementation, a separate `/pushall` recorded the initial handler/unit
changes and moved the selected graph to Parties
`7e73709992512d944f4950e6efb28c6132f9473c`, Builds
`332b2c144defba546fe0a09626a4a72f2340e22f`, and EventStore source
`d55c6f7c11e9bce6f17d490a84c83f1561979d63` (`v3.125.1-3-gd55c6f7c`).
The implementation agent made no Git mutations. Current dependency observations
and fitness expectations match that graph; historical approvals remain bound to
their original identities. All evidence summarized below was rerun against the
new graph. Earlier source runs are supplementary historical evidence.

Before production edits, the owned Parties AppHost started successfully via
`aspire start --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`.
`aspire describe --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --format Json`
reported 2 Running/Healthy, 10 Waiting, 2 NotStarted, and 1 Starting resource at
capture. This is a startup observation, not full topology acceptance. The exact
owned instance was stopped successfully with
`aspire stop --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`
before builds. The unrelated running Tenants AppHost was preserved. The local
resource snapshot is `/tmp/dw163-aspire-baseline.json`.

Each affected test project was built individually:

```bash
dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
```

Both builds passed with zero warnings/errors. Direct xUnit v3 assembly runs:

```bash
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll -class Hexalith.Parties.Projections.Tests.Handlers.PartySdkProjectionHandlerTests
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Projections.PartyDetailSdkProjectionRecoveryTests
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll
```

Results: 145/145 focused handler cases, 24/24 persisted recovery cases, 335/335
full projection cases, and 1219/1219 full service cases. Every run discovered
nonzero tests and reported zero errors, failures, skips, and not-run tests. The
service run includes the unchanged strict identity, clean-checkout, and
no-root-version-override gates.

The recovery cases use EventStore.Testing's serialized in-memory accessor and
real batch protocol. They check persisted contents, both checkpoints, terminal
manifest replay, and usable ETags after repeated unresolved deliveries,
same/changed dispatch recovery, prepared markers without installed envelopes,
installation and compaction interruptions (including empty ETags and the final
recovery anchor), second-operation contention and compensation, genuine missing
historical slots, UTF-8 scope boundaries, namespace collisions, normal supported
delivery recovery, and newer sequence-5 erasure tombstones at BeforeReceipt.

Package adoption was verified separately. The attached temporary consumer
project links the exact new recovery test class and references the real Parties
Projections project while selecting package mode. Its CPM import is the same
Parties/shared Builds catalog; it has no EventStore version override, no
EventStore project reference, and warnings remain errors. Copy the two `.txt`
project attachments to a temporary directory as `PackageRecovery.csproj` and
`Directory.Packages.props` (adjust the workspace path if needed), then run:

```bash
dotnet build /tmp/dw163-package-validation/PackageRecovery.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:PartiesWorkspaceRoot="/path/to/parties" --artifacts-path /tmp/dw163-package-artifacts
dotnet /tmp/dw163-package-artifacts/bin/PackageRecovery/debug/PackageRecovery.dll -class Hexalith.Parties.Tests.Projections.PartyDetailSdkProjectionRecoveryTests
```

The package build passed with zero warnings/errors, and all 24 recovery cases
passed. The assets graph resolves EventStore Client, Contracts, DomainService,
Server, ServiceDefaults, and Testing as NuGet packages at 3.125.1. Exact NuGet
SHA-512 values and tested source SHA-256 values are retained in `validation.json`.

```bash
bash scripts/check-no-warning-override.sh
git -c core.whitespace=cr-at-eol diff --check
```

Both passed. The Git setting recognizes the repository's required CRLF endings.
Raw build/test logs remain under `/tmp/dw163-*.log`. No warning gate was relaxed,
no package was published, and no deployment was performed. The earlier
`dw-163-resume-2026-10-18` prerequisite evidence and the 2026-10-17 withdrawn
candidate reproduce legacy behavior; they are not the retained implementation.

Portable consumer templates use the required MSBuild property `PartiesWorkspaceRoot`; set it to the absolute checkout path when reproducing the package build. Saved run logs retain validation summaries; verbose local diagnostics are excluded.
