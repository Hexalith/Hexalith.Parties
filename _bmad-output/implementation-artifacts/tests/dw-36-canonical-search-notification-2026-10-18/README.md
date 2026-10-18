# DW-36 canonical search notification verification

The shared index retains one payload-free source-event descriptor per present
aggregate. Retries use that persisted selection instead of selecting an event
from the retry batch. Historical rows use explicit synthetic attribution.
Erasure removes target provenance and rebuilds retain the canonical selection.

The baseline regression reproduces incorrect rejection-event attribution after
serialized read-back and handler recreation. A minimal baseline-only patch and
failure transcript are retained beside this document.

Final Debug/source builds have zero warnings/errors. All 316 projection tests
and 1158 service integration tests pass (1474 total), with no failures, errors,
skips or not-run cases. The named XML outcome audit, commands, source/assembly
hashes and retained log hashes are in [validation.json](validation.json).

Three independent review layers produced eleven findings: eight were patched,
one was refuted, and two low findings were rejected with recorded reasons. The
verification reviewer confirmed the new real-indexer test rejects both UTC
normalization and shortened event names. No new work was deferred. Triage is in
[the completed spec](../../spec-dw-36-canonical-search-notification.md).

Final tests include actual Dapr-configured JSON options, advancing redaction and
corrupt events, legacy-row upgrades, cleanup contention and serialized rebuild
read-back. A real indexer/mapper pipeline forwards the exact source timestamp
representation and event name after a failed ingestion and recreated-handler
retry. Memories and state-store network calls remain substituted in this proof.

The isolated no-build Aspire observation established Parties readiness only;
EventStore, Tenants, UI and Admin were Finished. Full topology qualification and
external-search concurrency fencing remain separate. Old writers can discard
the additive dictionary; the synthetic fallback preserves honest attribution.

The CRLF-aware whitespace check follows repository EditorConfig and retains
blank-at-eol/eof and space-before-tab checks. The exact local completion message
passed repository-pinned commitlint. No dependencies or submodules changed.
