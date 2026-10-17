# DW-163 SDK recovery investigation

DW-163 remains open. The domain candidate in `reproduce.patch` was withdrawn;
this patch is a diagnostic reproducer, outside the compilation graph.

Validated base: Parties `3e9651ede9ac9b8d955d756dad21d0b31ab85302`; EventStore source
`7edc260d8c0bd904f1215f801bad02b99c6694cf`. Use a disposable checkout with these existing
root source dependencies. The patch contains the candidate handler and its six
recovery tests; it does not change dependencies, runtime configuration or stores.

From that checkout's root:

```bash
git apply --check _bmad-output/implementation-artifacts/tests/dw-163-sdk-recovery-investigation-2026-10-17/reproduce.patch
git apply _bmad-output/implementation-artifacts/tests/dw-163-sdk-recovery-investigation-2026-10-17/reproduce.patch
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Projections.PartyDetailSdkProjectionRecoveryTests
```

Observed: build succeeds with zero warnings/errors; tests exit 1, five pass and
one fails. `InitialAuditDoesNotOverwriteConcurrentErasureAsync` fails for
`BeforeReceipt`. Its fault hook writes newer Detail and Processing tombstones
at sequence 5 after the initialization operations compact, then throws a
synthetic transport failure. SDK reconciliation restores the old blank Detail
candidate: `LastSequenceNumber = long.MinValue`, `ErasureSequenceNumber = null`.
Expected: both newer sequence-5 erasure tombstones survive. The `MarkerPrepared`
control and four ordinary recovery cases pass.

The SDK's `ReadModelBatchProtocol.CompactAndCompleteAsync` reconciles unguarded
committed batches without preserving a newer canonical write in this interleaving.
EventStore owns that recovery guarantee. A stable event-independent initialization
manifest would address the separate prepared/committed retry-identity issues, but
it would not establish erasure safety. No domain fix is retained.

Raw logs/XML remain local; this sanitized patch preserves a portable reproduction
without startup credentials or machine-specific log paths.
