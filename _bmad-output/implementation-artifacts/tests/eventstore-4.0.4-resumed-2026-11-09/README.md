# EventStore 4.0.4 resumed offline qualification

The existing qualification was resumed on 2026-11-09 at Parties HEAD `806ed7b83b35fcc24996e58affb78ea51a0c8aa7`.
The implementation was already committed. This run changes qualification evidence
and workflow tracking only; concurrent source and configuration edits are preserved.

All five affected project builds completed with zero warnings/errors, using the
explicit public EventStore 4.0.4, Tenants 5.11.2 and Memories 3.0.11 package cohort.
The final focused result is 53 passing tests with no skips: three catalog owner
tests, 33 capability cases, 16 preparation/provenance controls and one workflow
policy check. The policy case initially observed concurrent tool/test edits and
passed after rebuilding the updated test source; its original failure is retained.

An isolated helper validated the actual 41 routes, 41 operation schemas, 28
descriptors and 28 adapters with the packaged SDK validator/codec. Eight freshly
downloaded public cohort archives match the restored packages; four actual native
SDK PE bodies match those archives. The adapter remains outside the native host
dependency closure. No source-mode symbol or producer substitution was used.

`receipt.json` retains exact commands, exit codes, hashes, per-test counts, all
review verdicts and limitations. Gzip files preserve executed logs, XML and helper
sources. The requested temporary handoff was recreated from these fresh results at
`/tmp/tenants-release-20261103-07JciR/parties404-qualification-handoff.json`.
The missing historical temporary receipts and original 14-archive producer proof
were not reconstructed.

The three review layers completed. Their remaining findings concern later
integration/lifecycle code and are recorded in the deferred-work ledger; current
public source-identity mismatch still blocks the broader public evidence gate.
This receipt claims no hosted CI, source-mode qualification, native topology
acceptance, publication or deployment. Downstream McpCli acceptance remains separate.
