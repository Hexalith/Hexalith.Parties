# Reconciliation and migration implementation evidence — 2026-10-28

`implementation-verification.json` is the final implementation receipt: exact
commands, working directories, exit codes, full xUnit case outcomes, log/XML
hashes, source-file hashes and full repository identities. Every selected verification command
exited zero; the separately retained fresh-resolver blocker remains a failed qualification. All five Debug build commands reported zero warnings/errors.
Earlier logs are historical iterations, including the superseded unconditional
re-read design, compiler corrections and fixture/protocol investigation; they
must not replace the selected `final-*` evidence.

| Final lane | Result |
| --- | --- |
| Retained/package UI, fallback and subscription | 35 passed, zero skipped |
| Mapped-source UI and shared producer adapter | 35 passed, zero skipped |
| Producer reconciliation | 27 passed, zero skipped |
| Projection-handler controls | 218 passed, one existing legacy-SDK skip |
| Actor backfill, generation recovery and guarded erasure | 47 passed, zero skipped |
| Warning override guard | Passed |
| Root and producer whitespace | Passed with repository CRLF-aware whitespace policy |

The projection skip is the existing
`PublishedSdkPreservesLargeLegacyLiveAndRebuildCompatibilityAsync`: it explicitly
skips when the resolved SDK exposes generation support. All six new backfill
cases and every reconciliation matrix case executed and passed. This skip does
not supply deployment or publication evidence.

The confirmation controls prove delayed registration is ready before command
dispatch, a confirmation emitted during the command is buffered until acceptance,
and rejected/cancelled operations never reconcile. A connected old-Current model
without projection evidence remains pending. Deterministic timers cover repeated
connection changes; completion/cancellation drains registration, serialized reads,
fallback work and timers. Offline connection-start failure retains the polling
path. Shared registration remains opt-in.

The rehearsal seeds selected-tenant actor values and rollback companions without
selected-tenant SDK models. Actual SDK aggregate rebuild dispatch creates detail
and processing models; the actual shared SDK Begin/Accumulate/Finalize/Stage/
Commit/Verify lifecycle creates the tenant index. Fresh workloads and handlers
read the surviving external backend with its original provider ETags. Persisted
fields, checkpoints, processing coverage, erased suppression, actor bytes and
unrelated tenant values are asserted. Missing interior history and unreadable
encrypted-format history refuse aggregate publication. Competing aggregate and
shared authority refuse promotion without overwriting the competing bytes.
Source-head/tail and key completeness in a real capture remain deployment-owner
prerequisites, documented in `docs/read-model-migration.md`.

The rehearsal exposed a shared index plan-identity defect. New candidates retain
the original conditional write policy across terminal verification; unconditional
and delete policies are refused. Raw/older wrapped candidates retain their
existing compatibility path. A refused old session is not certified by these
new receipts. Source-guarded staged aggregate rebuilds and one-shot aggregate
rebuilds are distinct SDK lifecycles; the rehearsal verifies the latter by
persisted reads and the shared index by its staged Verify transition.

Fresh source-map generation failed, as retained in
`ui-source-debug-build.txt`: preserved gitlink/HEAD differences, the staged
Platform release identity/catalog, dirty/different Builds state and tool 4.34.0
outside Platform's `[4.35.2, 5.0.0-0)` range block current graph qualification.
The repository's pinned-tool retained-map fallback built and tested the actual
mapped source without changing dependencies or those user deltas. Its generated
map, environment and generation receipt are retained under
`retained-source-mapping/`, `retained-source-environment.json` and
`retained-generation-receipt.json`. This fallback is source-behavior evidence;
it does not qualify the current resolver graph or replace package/deployed proof.

Implementation baseline Parties: `9239c531f3b0cbb4a998c419e537463b597ff14b`.
Producer checkout baseline: `5f665c4d38afb2255a7dde4620330e09824a40a1`.
EventStore checkout: `36c78f3435d2d492a73f1b0707cf865c8735d740`.
The receipt records the remaining full source identities and all task source
hashes. No staging, commits, remote operations, package publication, deployment
or real tenant mutations occurred in this implementation phase. The primary
workflow owns audit, ledger/toolchain/helper reconciliation, review and task
publication. DW-58 and external package/adoption gates remain open.

## Primary-agent acceptance audit

`verify-dispositions.py` reproduces five checks for the obsolete DW-126 packet and
corrected tracked identity helper. `disposition-verification.json` records current operative/helper inputs and an
explicitly historical effective-graph observation bound to its catalog snapshot;
it does not qualify the current catalog or blocked source resolver. `helper-verification.json` records a fresh clean Debug build and
11 passing persisted verifier cases, with exact logs and XML.

`audit-matrix.py` checks source/log/XML hashes and maps every approved matrix row
to executed passing cases. `matrix-audit.json` records this audit. The ledger
records four completed bounded dispositions/source fixes and the implemented
DW-58 rehearsal; DW-58 stays open for deployed cutover, and matching producer
publication/adoption remains independent.


## Review corrections and retained inputs

The selected retained/mapped UI and producer receipts now include acquisition
failure, disconnected startup timeout, connected direct-poll refusal, awaited
asynchronous removal, and cleanup faults with active reads/fallback work. Only
those affected classes were rerun during this correction. The unchanged
projection/backfill/helper receipts retain their previous scope; full verification
belongs to the primary workflow. `pre-review-implementation-verification.json`
retains the superseded execution identities.

Every required command log is delivered as a `.txt` file, including
`helper-build.txt`, `helper-tests.txt` and `ui-source-debug-build.txt`. These are
byte-for-byte copies of the original ignored `.log` files: the receipt hashes
are unchanged. Ignored `.log` files are historical local scratch and are not
required to run the audit. `test_audit_matrix.py` constructs a checkout-shaped
copy containing only declared evidence and source inputs; no temporary workspace
or ignored logs are available to that audit. It proves acceptance of the complete
copy and rejection of a missing mapped lane, a missing theory variant and a
mutated disposition input even when its recorded check remains Pass. The same
four checks pass under optimized Python. `review-fixes-verification.json` and
`review-fixes-audit-tests.txt` retain the focused correction results.

## Reconstruct the retained source map

`retained-materializer/RegenerateSourceMapping.cs` and
`retained-materializer/retained-mapping-input.json` preserve the exact temporary
inputs used by `retained-generation-receipt.json`. Its `archived_inputs` records
their SHA-256 values and the retained environment bytes. Verify those hashes and
the pinned `Hexalith.Builds.Tooling.dll` hash in that receipt before replaying.
The repository tool pin is 4.34.0; this procedure changes no dependency selection.

From this evidence directory, at the recorded source roots, reconstruct the
recorded temporary workspace with the pinned producer materializer:

```bash
mkdir -p /tmp/five-reconciliation-retained-map-20261028
cp retained-generation-receipt.json /tmp/five-reconciliation-retained-map-20261028/generation-receipt.json
cp retained-materializer/RegenerateSourceMapping.cs retained-materializer/retained-mapping-input.json /tmp/five-reconciliation-retained-map-20261028/
dotnet run --file /tmp/five-reconciliation-retained-map-20261028/RegenerateSourceMapping.cs -- \
  /home/administrator/.nuget/packages/hexalith.builds.module.cli/4.34.0/tools/net10.0/any \
  /home/administrator/projects/hexalith/parties/module/hexalith.module-manifest.v2.json \
  /tmp/five-reconciliation-retained-map-20261028 \
  /tmp/five-reconciliation-retained-map-20261028/retained-mapping-input.json
```

Compare every regenerated `source-mapping/` file to the receipt's
`generated_sha256` and the environment to `retained-source-environment.json`.
The archived mapping names `/home/administrator/projects/hexalith/parties` and
its root-declared reference checkouts explicitly. Replaying the verified retained
input does not run or qualify the fresh resolver. Source-root relocation requires
a newly admitted producer mapping and new evidence; editing paths in the archived
JSON or imports cannot transfer these hashes or qualification to another tree.
The recorded temporary output path is also part of the environment identity.

## Final primary regression verification

`primary-verification.json` records clean final Debug builds, full retained UI
432 passes plus the existing DW-112 compact-package skip, full mapped UI 433
passes without skips, 27 producer reconciliation passes, four normal and four
optimized audit controls, and the warning guard. `primary-source-snapshot.json`
binds the tested source bytes, including the two capability/adoption test repairs.
Earlier unchanged baseline assertions incorrectly required published Shell 4.7.1
types to be absent. The package and built DLL bytes match; registration remains
unchanged. Exact original four-failure results are preserved under
`pre-catalog-assertion/`, with diagnosis in `catalog-assertion-diagnosis.json`.

Projection-handler and backfill code did not change during review remediation;
their retained 218 passes/one legacy-SDK skip and 47 passes remain source-hash
bound in the implementation receipt. The helper lane retains 11 passing cases.
The ledger separately records existing causality, sub-interval outage, single
stale-read retry and uncancellable transport RPC limitations. Current owned
teardown is qualified for completing/cooperative transport calls; no bounded
shutdown claim is made for a permanently stalled SDK JoinGroup/LeaveGroup call.


## Historical selected catalog

The original effective-property observation consumed the preserved dirty
`references/Hexalith.Builds/Props/Directory.Packages.props`. Its exact bytes are
archived under `historical-inputs/f9c97805fe540f17277f0d96f16a92b5f595f24c0f059c7a2c864abe8a6c1fb5.catalog`.
The archive is read-only evidence, identified by its SHA-256; `historical_inputs`
in `disposition-verification.json` binds its path, hash, owning revision and
observed owning-index blob. This snapshot does not qualify the current catalog
or source resolver and does not adopt the Builds edit. The real Builds file and
its index are unchanged.

`verify-dispositions.py` verifies the archive and reproduces the historical graph
record without a new MSBuild evaluation. The matrix audit checks this one named
historical input against the snapshot and every other operative/helper input
against current bytes. The checkout-shaped positive uses the Builds owning-index
catalog bytes with checkout filters, which differ from the observed dirty bytes;
it succeeds using the separately delivered snapshot. Existing missing-lane,
missing-variant and current-input negatives remain, with a fifth control rejecting
a mutated catalog snapshot.

From this directory, reproduce only the evidence checks with
`python3 verify-dispositions.py`, `python3 test_audit_matrix.py`,
`python3 -O test_audit_matrix.py`, and `python3 audit-matrix.py`.
`catalog-snapshot-verification.json` and `catalog-snapshot-audit-tests.txt` retain
the five passing controls and identical disposition reproduction. No runtime or
project builds are needed for these checks.
