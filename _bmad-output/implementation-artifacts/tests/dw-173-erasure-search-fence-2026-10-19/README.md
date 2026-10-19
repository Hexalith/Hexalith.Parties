# DW-173 / DW-193 search erasure reconciliation

Live and shared-rebuild notifications now check canonical state per target
before and after external work. Erasure during indexing triggers cleanup of the
late mapped result; cleanup failures, changed targets and racing re-adds retain
retryable delivery. Invalid canonical state cannot authorize external work.
Fresh live contact/identifier collections retain their existing behavior only
when their checkpoint, source metadata and stored fields match canonical state.

The initial seven regressions all fail against unchanged production code.
Independent review then found a JsonIgnore compatibility regression; its failing
transcript is retained separately. All eight review findings are patched.

Final Debug/source builds have zero warnings/errors. All 27 focused regressions,
1,311 host tests and 379 projection tests pass without failures, errors, skips or
not-run cases. The focused cases are included in the host total: the two complete
assemblies cover 1,690 cases. [validation.json](validation.json) records commands,
outcomes and source/assembly hashes; [focused-outcomes.json](focused-outcomes.json)
records the named regression outcomes. XML result destinations are capture-only
placeholders in the command receipts. Routine full-suite logs remain local.

Regressions use serialized SDK storage, independent handler/eraser instances,
deterministic bounded barriers and persisted tombstone assertions. The actual
Memories client/adapter/mapping/cleanup pipeline runs against a controlled HTTP
transport for both live and rebuild races. This proves the awaited operation
contract, not terminal completion of asynchronous external workflows. DW-168 and
DW-170 retain those separate obligations; no new deferred work was added.

The isolated Aspire baseline reached healthy Parties readiness. EventStore and
Tenants were Finished; full-topology qualification is not claimed. The owned
AppHost was stopped before builds. No dependency, schema, migration, deployment,
staging or commit operation was performed.
