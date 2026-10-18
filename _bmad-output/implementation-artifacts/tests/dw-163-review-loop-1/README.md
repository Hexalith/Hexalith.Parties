# DW-163 review loop 1 verification — 2026-10-18

This packet verifies the revised implementation against the updated spec. The
first candidate's `dw-163-implementation-2026-10-18` packet remains historical;
its normal-manifest retention behavior was rejected. No historical approval or
dependency receipt is renewed by these local checks.

Only unresolved deliveries with both slots absent create initialization. Its
retained manifest contains two immutable empty read models, no event payload or
audit records, and a bounded identity independent of events and dispatch IDs.
Both participating keys recover before presence checks. Normal dispatch keeps
its scope, serialized values, concurrency and non-retained manifest behavior.
Legacy non-retained retries remain usable; normal namespace terminal anchors
also have a fixture-only regression. Failed audits still use the existing
optimistic update policy. Genuine missing historical slots require rebuild.

The exact Parties AppHost was started with `aspire start --non-interactive
--apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj
--format Json`, observed with `aspire describe` at the same target, and stopped
with `aspire stop --non-interactive --apphost` at that exact path before builds.
`aspire-baseline.json` records security Starting and dependent resources Waiting;
this is a startup observation, not full topology acceptance.

Each affected project was built individually:

```bash
dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
```

Both final builds passed with zero warnings and errors. The initial projection
build encountered a transient full filesystem (MSB3026, then exit 134); capacity
recovered before the successful final runs. No workspace artifacts were deleted
and no warning policy was weakened.

Direct xUnit v3 runs discovered nonzero tests and had zero failures, errors,
skips and not-run tests:

| Run | Passed |
| --- | ---: |
| `PartySdkProjectionHandlerTests` | 145 |
| Source `PartyDetailSdkProjectionRecoveryTests` | 37 |
| Full Projections assembly | 335 |
| Full service assembly | 1232 |
| Published-package recovery class | 37 |

```bash
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll -class Hexalith.Parties.Projections.Tests.Handlers.PartySdkProjectionHandlerTests
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Projections.PartyDetailSdkProjectionRecoveryTests
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll
```

The persisted regressions exercise repeated unresolved delivery, supported
recovery with changed dispatch identity, prepared empty markers without an
installed envelope, installation/commit/compaction interruptions, the final
recovery anchor, legacy first-create manifest identity, real SDK erasure after
normal pre-install interruption, mixed supported-prefix/unresolved redelivery,
second-operation contention, interrupted compensation, overlapping handlers,
genuine historical slot loss, UTF-8 scope limits, namespace collisions and
newer sequence-5 tombstones. Assertions include persisted values, checkpoints,
audit outcomes, ETags, pending envelopes and manifest retention.

The package consumer uses the same shared catalog without an EventStore version
override. Copy the `.csproj.txt` and `.props.txt` attachments to a temporary
directory as `PackageRecovery.csproj` and `Directory.Packages.props`, preserving
the linked source paths or adjusting them to the checkout:

```bash
dotnet build /tmp/dw163-review-loop-1-package/PackageRecovery.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:PartiesWorkspaceRoot="/path/to/parties" --artifacts-path /tmp/dw163-review-loop-1-package-artifacts
dotnet /tmp/dw163-review-loop-1-package-artifacts/bin/PackageRecovery/debug/PackageRecovery.dll -class Hexalith.Parties.Tests.Projections.PartyDetailSdkProjectionRecoveryTests
```

The final package build has zero warnings/errors. Its assets resolve EventStore
Client, Contracts, DomainService, Server, ServiceDefaults and Testing as NuGet
packages at 3.125.1. `validation.json` records their NuGet SHA-512 values and the
tested handler, both test files and shared catalog SHA-256 values separately
from the rejected candidate's records.

```bash
bash scripts/check-no-warning-override.sh
git -c core.whitespace=cr-at-eol diff --check
```

Both gates passed. No Git mutation, package publication or deployment was
performed. Independent parent review owns final acceptance and DW-163 closure.

Portable consumer templates use the required MSBuild property `PartiesWorkspaceRoot`; set it to the absolute checkout path when reproducing the package build. Saved run logs retain validation summaries; verbose local diagnostics are excluded.

Git revision map captured directly from Git after the review fixes:

- Canonical spec baseline: `6b0641829b68d22656fb173eec6578c65e8e91f0`.
- Parties HEAD: `7e73709992512d944f4950e6efb28c6132f9473c`.

These committed identities do not include the current uncommitted handler and
test changes. `validation.json` preserves the initial `source_sha256` map and
prior results as historical evidence; `review_fixes_verification` records the
current tested working-source SHA-256 values separately. Its
`tested_source_revisions` map also records each dependency's baseline gitlink,
Parties HEAD gitlink and checked-out HEAD. The HEAD gitlinks and checked-out
HEADs agree for all root-declared dependencies:

| Root-declared dependency | HEAD gitlink and checked-out HEAD |
| --- | --- |
| `references/Hexalith.EventStore` | `d55c6f7c11e9bce6f17d490a84c83f1561979d63` |
| `references/Hexalith.Memories` | `d0c5d8ca96b06e02acdda88f255b33288642a7f7` |
| `references/Hexalith.FrontComposer` | `1513fe3ed21a554eeee7a9d807f9b9e25fdb4cd2` |
| `references/Hexalith.Tenants` | `c9feedb6656d70c7271e851c844ee29bdfb28c3e` |
| `references/Hexalith.AI.Tools` | `3f194e17174994d308ec84af9ee2b5aa68674d0d` |
| `references/Hexalith.Commons` | `b7e6e7d5e28568b30c523130339cc4c52e12c674` |
| `references/Hexalith.Builds` | `332b2c144defba546fe0a09626a4a72f2340e22f` |
| `references/Hexalith.PolymorphicSerializations` | `80ce5afd99216ce5f591d66c833feafaf371f171` |
| `references/Hexalith.McpCli` | `c25e2140e14a76b696a76aed30c8302107714fd6` |
| `references/Hexalith.Platform` | `5f18593639832d1245577dd93ec07687517d3acb` |

The map was obtained with `git rev-parse <baseline>^{commit}`,
`git rev-parse HEAD^{commit}`, root `.gitmodules` enumeration through
`git config -f .gitmodules --get-regexp`, `git ls-tree <revision> -- <path>`
and `git -C <path> rev-parse HEAD^{commit}`. No dependency identity was copied
from another evidence packet or inferred from documentation.

Review-fix verification built the portable package consumer with zero
warnings/errors and ran only `PartyDetailSdkProjectionRecoveryTests`: 37/37
passed, with zero failures, errors, skips or not-run tests. See
`review-fixes-build.txt` and `review-fixes-recovery.txt`. The earlier 37-case
and full-suite results above describe the previous working-source hashes;
full final verification remains with the parent workflow.

The focused additions pin independent literal canonical bytes for both
`empty-v1` initialization operations. Changing either serialized format
requires an explicit initialization identity-version decision so an existing
prepared manifest is not stranded. The namespace collision case now reuses
the store containing the initializer receipt and verifies both distinct
marker identities and advanced models. Persisted repeated-delivery coverage
now includes unresolved-first/supported-second ordering and later audit
replacement with both checkpoints and detail recovered.

Final parent verification after review patches: both affected Debug/source builds
passed with zero warnings/errors; 145 focused handler, 37 source recovery, 335 full
projection, 1232 full service and 37 published-package recovery cases passed with
zero failures/errors/skips/not-run tests. Warning and whitespace gates passed.
Public test methods also follow the required XML documentation convention.

Two independent review passes completed. The final patches correct the same-store
collision test, freeze the empty-v1 canonical payload contract, add reverse-order
unresolved recovery, and bind this packet to canonical source revisions. Production
handler behavior did not change after the second review. One verified normal
postcommit compaction issue predates DW-163 and is recorded in the deferred ledger.
No current-story review finding remains unresolved.
