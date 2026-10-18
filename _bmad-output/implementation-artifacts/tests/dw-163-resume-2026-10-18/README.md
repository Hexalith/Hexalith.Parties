# DW-163 prerequisite revalidation — 2026-10-18

Parties base: `6b0641829b68d22656fb173eec6578c65e8e91f0`.
EventStore source: `ab8fa115c486875be3a22860a273d5d68a26c749`.
No production source, dependency selection, submodule selection or Git recording changed during this investigation.

The selected EventStore package 3.124.0 lacks retained-manifest recovery. The cached 3.125.1 Client assembly exposes `ReadModelBatch.RetainManifestForRecovery` and `IReadModelPendingBatchRecoveryStore.RecoverPendingAsync`; dotnet-inspect verified their metadata. The existing Parties dependency gate requires the shared catalog and rejects a root version override.

SDK source verification (working directory: EventStore root):

```bash
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1
```

Build exited 0 with zero warnings/errors. The exact focused test invocation is retained in `validation.json`: 31 passed, zero failed/skipped/not-run. It exercises terminal cleanup boundaries, preservation of newer canonical rows, recovery after conditional conflicts, and invalid evidence refusal. It does not exercise the Parties handler.

An isolated package consumer under `/tmp/dw-163-resume-validation/package-probe` used EventStore.Testing 3.125.1:

```bash
dotnet run --project /tmp/dw-163-resume-validation/package-probe/Probe.csproj --configuration Debug
```

Exit 0, three explicit assertions passed:

- Legacy initialization loses the newer Detail/Processing erasure at `BeforeReceipt` (expected negative control).
- Retained-manifest initialization preserves both sequence-5 erasure tombstones in the same interleaving.
- Recovery finds the original staged manifest under a changed delivery ID and leaves both persisted rows with ETags and no pending envelopes.

This is prerequisite evidence, not a completed domain fix or release qualification. Raw logs/XML and the probe stay local. `previous-draft.md` preserves the prior withdrawn-candidate history verbatim.

To reproduce the package checks, copy `package-probe.cs.txt` to `Program.cs` and
`package-probe.csproj.txt` to `Probe.csproj` in a temporary directory, then run
`dotnet run --project <temporary-directory>/Probe.csproj --configuration Debug`.
The text attachments are outside the repository compilation graph.
