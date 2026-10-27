# Legacy recovery and retained evidence — 2026-10-27

This packet qualifies the five scoped DW-198 entries against synthetic stores and
checks the existing EXT-PARTIES-1 archive. Implementation receipts bind the full
baseline revisions, selected source bytes, generated source hooks, executed
assembly hashes and exact commands. Final review, commits and remote identities
are recorded by the parent after this implementation handoff.

Baseline Aspire start/describe/stop logs are retained. Primary services were waiting
on security at inspection; these observations establish no whole-topology readiness.

| Executed lane | Result |
| --- | --- |
| Focused Parties Debug preparation | 2 pass, zero skips |
| Parties Debug generation recovery | 39 pass, zero skips |
| Memories Debug migration/workflow units | 18 pass, zero skips |
| Actual Redis/FalkorDB/authenticated Dapr affected suites | 104 pass, zero skips |
| Parties Release/package preparation and generation recovery | 41 pass, zero skips |
| Entire Parties projection assembly | 464 pass, one existing old-package-only compatibility skip |
| Entire Parties service assembly before owner commit | 2,788 pass, two clean-owner gate failures, four existing optional runtime skips |
| Entire Memories Server assembly | 3,066 pass, five external/package evidence failures, one existing mutating-submodule guard skip |
| Release/package Parties solution | Pass, zero warnings/errors |
| Mapped Debug Parties solution | Four HXW006 FrontComposer retained-reference configuration conflicts |
| No-warning/nested-submodule guard | Pass |
| EXT preservation | All 71 manifest files, 17 original receipts, 14 source snapshots and 13 finding IDs agree |

The 163 focused Debug controls and 41 package controls have no targeted skips.
The two Parties service failures require clean Memories ownership for immutable
consumption proof; rerun them after the owner commit and parent gitlink adoption.
The five broad Server failures remain visible. The unchanged test/config/evidence
inputs and their baseline hashes are in
[broad-blocker-baseline-comparison.json](broad-blocker-baseline-comparison.json).
They comprise the A41 posture assertion, three assertions requiring the older
3.127.0 package graph, and the unchanged C7 waiver expiring on 2026-10-26, which
fails against the executed date 2026-10-27. No separate complete baseline checkout
run is claimed. The broad source build retains its four HXW006 errors; focused
source builds and package solution validation supply the documented fallback.

The projection guard runs during pure preparation. Checkpoint 50 with retained
failed facts through 100 rejects replay through 50 before staging or publication
in inline and bucket storage. Full replay repairs every fact; a concurrent failed
fact 101 wins the canonical ETag race. Persisted snapshots and an unrelated row
remain unchanged on refusal or stale publication.

Migration validates live syntactic id, tenant, case and the observed nonempty
source URI, legacy ownership/required fields, existing canonical ownership/fields
and capacity before shared key enrollment. These checks and the copy/retirement
share one Redis script. Missing, foreign, removed and malformed owners retain
content and return bounded reconciliation refusals. Restart controls inspect both
hash bytes and ownership sets. Only explicit synthetic fixture repair permits
activation; no production data migration or completeness attestation occurred.

The [historical protocol proof](historical-protocol-proof.json) pins full Memories
revision `cdd73d7361f67781a523f63f04853491a687f426`. The helper's RunAsync body
matches the retained original after line-ending normalization. The test schedules
the original nine-field input shape under the production workflow name, executes
the actual Redis existence activity, and blocks description or embedding while
the original worker stops. The upgraded worker resumes to
`legacy-retry-input-requires-restart` without reexecuting those activities. Its
actual modern scheduler and fenced activity persist fresh NL output. Historical
input is then physically purged and remains absent after another worker restart;
unrelated content survives. No incompatibility reproduced at those boundaries,
so the production retry workflow was unchanged. This uses selected Dapr SDK
1.18.10 and daprd 1.18.7; it does not recreate every historical SDK distribution.

Reproduce the owned runtime with explicit local binaries in an owned directory:

```bash
python3 _bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/run-owned-runtime.py \
  --runtime-root "${OWNED_RUNTIME}" --output "${NEW_PRIVATE_RUN}" --scope affected
```

`OWNED_RUNTIME` contains `dapr/{placement,scheduler}`,
`native-redis/{redis-server,redisearch.so}` and
`native-falkor/{redis-server,falkordb.so}`. The existing sidecar fixture selects
`~/.dapr/bin/daprd`. The runner owns fresh loopback listeners, backend directories,
process sessions, logs, XML and teardown. Credentials remain fixture-private.
It does not execute EXT Local/Live or deploy anything. Binaries and executed test
assemblies are hashed in the receipt.

For mapped Debug builds, use the pinned module tool 4.34.0 generated hooks
recorded in `implementation-verification.json`; do not handwrite source selectors.
Export its five source-hook paths before the recorded `dotnet build` commands.
The source solution blocker does not authorize bypassing HXW006 or initializing
nested submodules.

The [preservation check](verify-preservation.py) reads retained repository files
only:

```bash
python3 _bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/verify-preservation.py
```

[Separate preservation results](preservation-verification.json) retain all 13
review dispositions and Local exit 1, the original 446-pass/4-fail Server XML and
both original Live refusals. All historical bytes remain in the existing
`ext-parties-1-owner-delivery-2026-10-25/resume-recovery/` archive. No historical
temporary files are required. Acceptance, Available and Live qualification remain
false; installed hashes and owner dates remain null. Evidence preservation grants
no owner approval, publication/adoption, deployment or production custody.

Verify the exact current implementation snapshot and executed local DLLs with:

```bash
python3 _bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/verify-implementation.py --artifacts
```

## Review 1 patch

The table and implementation receipts above retain the original execution results.
The corrected patch has separate [receipts and hashes](review1/patch-verification.json).
Earlier versions of the changed scripts and this README are retained in
`review1/prior-scripts/`, bound to the unchanged original `SHA256SUMS` through
`review1/prior-files.json`. Use `review1/SHA256SUMS` for the current patch files.

The patch executes only eight revised migration rows and the two revised upgrade
boundaries: **10 pass, zero skips**. Source replacement now observes the new
source's closed admission after restart, while missing and empty URI fields are
isolated from missing ownership. The upgrade seeds its retry admission before the
historical worker, progresses that same admission under the upgraded scheduler,
keeps old content work blocked through actual unit/history erasure, then releases
it and checks physical absence after restart. Seven focused Python controls pass
under `python -O`; their disposable copies reject omitted/duplicate inventories,
source/body/artifact mutations, startup failures and timeouts. Historical repository
artifacts were only read.

The portable build wrapper uses the exact pinned producer resolver/materializer
and retains its configuration and original generated input under
`review1/source-build/`. It selects no source roots itself. Restore the repository's
pinned local tool when needed, then regenerate and build the affected project:

```bash
dotnet tool restore --tool-manifest .config/dotnet-tools.json
python3 _bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/build-source.py \
  --workspace "${NEW_MAPPING}" --regenerate --cwd references/Hexalith.Memories -- \
  dotnet build tests/Hexalith.Memories.IntegrationTests -c Debug -m:1 --nologo
```

The tool is `hexalith.builds.module.cli` **4.34.0**; its materializer assembly is
hash-pinned in `configuration.json`. The wrapper requires every root-declared
checkout to be initialized already and performs no submodule initialization.
Its isolated generator uses JIT execution because the pinned tool dynamically
loads MSBuild assemblies. All normal mapping validation remains enabled.

Fresh regeneration initially refused the unavailable Builds catalog object
`f9142d8a845a16e54ee060e6a0250c773d00e0ba`. A focused fallback replayed only the
retained producer-selected input through the pinned materializer, bound to the
original receipt's mapping SHA; that replay supplies no fresh graph qualification.
After the parent fetched that exact root-declared catalog object without changing
HEAD or a gitlink, authoritative fresh regeneration and the affected Debug build
passed with zero warnings/errors. The initial refusal and both later commands
remain separately recorded. No source-selector or dependency pin was changed.

Reproduce only the patched runtime cases:

```bash
python3 _bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/run-owned-runtime.py \
  --runtime-root "${OWNED_RUNTIME}" --output "${NEW_PRIVATE_RUN}" \
  --method '*NamespaceMigrationRefusesUnprovenLiveOwnershipAtomicallyAcrossRestart' \
  --method '*OriginalSameNameRawRetrySuspendedInEachContentActivityTerminatesAfterWorkerUpgradeAndPurgesHistory'
python3 -B -O _bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/review1/test_evidence_controls.py
```

The runner now hashes the executed test/server DLLs, daprd, dotnet and owned native
binaries. It persists attempted commands before launch and explicit failure
outcomes before cleanup, including startup and test timeouts. Verify current
canonical source text and exact executed artifacts with the fresh patch receipt:

```bash
python3 -O _bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/verify-implementation.py \
  --receipt _bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/review1/patch-verification.json --artifacts
```

Source verification accepts equivalent LF/CRLF text, recomputes equality with the
retained original `RunAsync` body, and keeps DLL hashes exact. Preservation pins
the original retained checksum manifest and complete reviewed-source inventory;
its explicit failures remain active under optimization. Default implementation
verification still addresses the earlier receipt, whose source snapshot is now
historical. Full combined-source verification and Git publication remain owned by
the parent.

## Parent combined-source verification

The final source uses Memories `f480fd70a7c25587264f50b5174358eee709c513`,
rebased onto upstream `2bfc735600ed153bce9069570a43ba93fe77ac0f`. The owner main
was pushed and independently read back before the parent gitlink was committed.
[Final verification](final-verification.json) supersedes the earlier snapshots
for current source/artifact verification; the original and review-patch receipts
remain historical. Final actual runtime controls pass 107/107 without skips.
The full projection suite has zero failures and one existing compatibility skip.
The broad Memories Server run retains exactly its five recorded evidence gates.
The Release/package solution build passes with zero warnings/errors.

Use `verify-implementation.py --receipt .../final-verification.json --artifacts`
for the final local source and executed-artifact snapshot. Parent post-commit
service-suite and immutable-selection results are recorded in that final receipt.

Final post-commit service verification passes **2,790 tests**, zero failures,
with four existing optional skips. Both clean-owner/committed-gitlink gates pass.
Package-mode preparation/generation controls pass **41/41**, zero skips.
All 12 individually triaged review findings are disposed: 11 corrected, one
low-impact inherited corruption-classification improvement rejected; no new
deferrals. Earlier failed/broad-blocker receipts remain unchanged.
