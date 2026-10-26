# Independent published 3.127.0 capture

`persisted-state.json` contains the exact base64-encoded physical marker and two
participant envelopes emitted by the published `Hexalith.EventStore.Client` and
`Hexalith.EventStore.Testing` 3.127.0 packages. The standalone generator uses
independent literal pre-generation model inputs, a non-retained batch, an actual
AfterCommit fault and a failed automatic reconciliation. It exports only the
three known keys and asserts that their count equals the old store's total count.
No current Parties writer or producer project participates.

`provenance.json` retains the actual restored old package graph, nuspec repository
revision, package hashes, generator hashes and snapshot hash. A second independent
run reproduces the complete output byte-identically. Preserve the generated JSON's
bytes, including its line endings.

Copy the retained source/template outside the repository before running it. This
keeps the old package references out of the current central package graph:

```bash
evidence="$PWD/_bmad-output/implementation-artifacts/tests/five-published-sdk-and-runtime-closure-2026-10-26/legacy-3.127.0"
scratch_dir=$(mktemp -d)
cp "$evidence/LegacyCapture.csproj.template" "$scratch_dir/LegacyCapture.csproj"
cp "$evidence/Program.cs" "$scratch_dir/Program.cs"
dotnet run --project "$scratch_dir/LegacyCapture.csproj" -c Release -p:NuGetAudit=false -- "$scratch_dir/persisted-state.json"
cmp "$evidence/persisted-state.json" "$scratch_dir/persisted-state.json"
```

`Published3127BytesRecoverBeforeNextTailAsync` imports those physical bytes into
an empty selected-SDK store, checks exact import equality and pending envelopes,
recovers the original receipt, verifies compacted ETags and then advances both
checkpoints with the next event. Its interrupted branch recreates storage from
serialized bytes after the content-free recovery receipt and resumes before the
next tail. It also records the actual loaded projection and Client identities
and SHA-256 hashes in the test output.
