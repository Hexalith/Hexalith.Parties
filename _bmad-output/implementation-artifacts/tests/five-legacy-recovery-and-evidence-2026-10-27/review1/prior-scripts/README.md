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
