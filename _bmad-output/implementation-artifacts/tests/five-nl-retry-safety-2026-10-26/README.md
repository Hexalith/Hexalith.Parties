# Five natural-language retry safety deferrals

This batch implements the five entries selected by
[the approved spec](../../spec-five-nl-retry-safety-deferred-items.md) in the owning
Memories repository. It covers tenant retry/history deletion, malformed addressing,
conditional maintenance, qualified alias retirement and atomic retry publication.

[Final verification](final-verification.json) records exact
commands, canonical baselines, source mappings, source/assembly hashes and broad
blockers. [Executed tests](final-executed-tests.json) retains all 215 passing
case names from the actual xUnit XML: 125 unit/indexing controls and 90 actual
Redis/authenticated Dapr/lifecycle controls, with zero skips in either lane.
[Baseline](baseline.json) records exact lifecycle results and resource limitations.
All three review layers completed; all 16 findings were resolved with no new deferrals.
Earlier implementation/execution receipts remain historical after those corrections.

Both affected Debug builds finish with zero warnings/errors. The full server
assembly reports 3,069 cases, five previously recorded package/documentation
failures and one existing destructive-submodule opt-in skip. The complete
integration assembly reports 515 cases, 170 failures at its nested EventStore
source-metadata gate and one existing skip. Those gates remain active. Historical
failed attempts, including the transient container startup failure, are retained
in the implementation receipt. No nested submodule was initialized.

Unknown historic workflow coverage requires independent owner reconciliation.
Uncertain scheduling stays durable and blocks cleanup. Ambiguous aliases remain
retained; unit identity or timestamps alone do not authorize retirement. Legacy
workflow erasure still requires the existing scheduler-drain attestation.
The scoped retirement lease coordinates registered schedulers; Dapr supplies no
execution-conditional purge against out-of-band mutations. No production data,
packages, images or deployment were changed. Source qualification does not
establish catalog/package adoption or deployed qualification.

Memories main is published at `22b792cf4f4a7aa5c9b6f129e6a491821169a8bf`. The parent records only this
root gitlink adoption; package, deployment and release-owner gates remain separate.

The mapped Debug Parties source-selection fitness class passes 115/115
with zero skips against the local adoption commit. Both exact commit messages
pass lock-pinned commitlint before and after commit. Final parent publication
is verified against the remote main ref after the evidence-only amendment.
