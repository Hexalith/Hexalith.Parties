# Actor read-model migration rehearsal and cutover handoff

DW-58 remains open until the deployment owner supplies cutover evidence. The
synthetic rehearsal in `PartyLegacyReadModelBackfillTests` exercises existing
EventStore SDK dispatchers and an in-memory implementation of their persisted
batch and staging contracts. It does not execute a production migration, qualify
a deployed state provider, or authorize retiring actor rollback data.

## Prerequisites and source authority

Record the exact Parties, EventStore and deployment image identities, effective
`Parties:Projections:ReadModelStateStoreName`, tenant/aggregate inventory, route
catalog fingerprint, and stable rebuild operation IDs. Use the authenticated
EventStore-owned rebuild workflow; the Parties SDK endpoints are internal,
sidecar/workload authenticated receivers, not a new public migration API.

Capture complete canonical event history for every selected aggregate, from
sequence 1 through its captured source head, with no missing tail or interior
sequence and no conflicting duplicates. Record source head message IDs and raw
history/inventory fingerprints. A contiguous prefix alone does not prove that its
tail is complete. Obtain this capture from the EventStore owner, independently of
legacy projection checkpoints. Checkpoints and actor snapshots cannot replace
canonical history or establish the tenant's complete aggregate inventory.

Prove that the selected key custody can read and decrypt all retained live
history and apply the registered payload evolution chain. An unresolved type,
unsupported format, unavailable required key, missing history, or a decode failure
must stop the migration for investigation. Do not feed encrypted bytes directly
into Parties projection folds or treat a drop-and-advance diagnostic as migration
success. Legitimate post-erasure redaction requires the authoritative erasure
inventory and suppression proof; never restore destroyed keys or personal data.
Keep the existing crypto, erasure and historical recovery reconciliation gates.

Snapshot actor values, sequence companions, index partition manifests, erasure
fences, SDK canonical values and recovery material using the deployment owner's
backup process. Retain byte hashes, provider versions/ETags and restore evidence.
Actor state names historically use `tenant:party-detail:party` and
`tenant:party-index:partition` with sequence/manifest companions. Physical Dapr
keys include app ID, actor type and actor ID: discover their actual deployed
layout rather than applying a string replacement. The test uses representative
synthetic actor backing keys and leaves every rollback byte untouched.

The SDK address authority is `PartySdkReadModelAddresses`:

| Model | Logical SDK key |
| --- | --- |
| Detail | `readmodel:{tenant}:party:party-detail:{party}:detail` |
| Processing | `readmodel:{tenant}:party:party-detail:{party}:processing-records` |
| Tenant index | `readmodel:{tenant}:party:party-index:parties:index` |

Large processing histories also use generation-scoped buckets. SDK private
markers, staging values and recovery directories belong to EventStore; do not
write, rename, enumerate for deletion, or replay them using custom persistence.

## Drain and rebuild order

1. The deployment owner stops admission and drains commands, projection
   subscriptions, actor reminders/buffers, live SDK writers, erasure/cleanup
   workers, and retry jobs for the selected scope. Prove quiescence and capture a
   stable source head/inventory after the drain. Prevent both legacy and SDK
   writers from resuming during cutover. Erasure operations must not be lost;
   reconcile any new authoritative erasure before promotion.
2. Rebuild each aggregate's detail and processing models together with the SDK
   aggregate rebuild protocol (`DomainProjectionDispatcher.RebuildAsync`, or the
   owner-managed source-guarded stage/commit/verify lifecycle). These are distinct
   SDK lifecycles: the one-shot batch is verified by persisted reads, not by
   issuing a staged-operation Verify against a batch which was never staged. The
   `PartyDetailSdkProjectionHandler.PrepareRebuildAsync` callback supplies the
   coordinated conditional writes and any immutable processing generation.
   Preserve operation identity on retry; recreate handlers/processes and resume
   from SDK-owned durable state. Do not copy a legacy actor value into an SDK key.
3. After all aggregate models verify, rebuild the shared tenant index with
   `DomainSharedProjectionRebuildDispatcher`: Begin the captured inventory,
   Accumulate each complete history in increasing ordinal string order of aggregate ID, Finalize the
   expected count and fingerprint, then Stage, Commit and Verify. The index is a
   whole-tenant replacement: omitting another party from the capture can delete
   its index entry. Carry authoritative erasure information and retain fences;
   an erased identifier must remain suppressed even if old create traffic exists.
4. Use SDK concurrency/owner checks throughout. A new canonical write, erasure,
   changed source capture, catalog mismatch or refused stage/commit is a stop
   condition. Preserve the competing authority and investigate; do not override
   ETags, force publication, regenerate operation IDs to evade a conflict, or
   overwrite selected state by hand. A set of individually successful SDK calls
   does not provide a transaction across all aggregates and the tenant index.

## Verify before routing reads

Restart handlers and the workload, then read persisted detail, processing
metadata/buckets and index through the SDK store and authenticated queries. Match
business fields, source checkpoints, index membership, processing coverage and
query freshness against the captured canonical source. Verify erased details
are tombstones, processing remains PII-free, and erased parties are absent from
index/search/export. Test a delayed old create for an erased identity. If optional
Memories search is enabled, require its separate rebuild and reconciliation proof;
the no-op search indexer in this rehearsal cannot certify it.

Verify that every actor rollback byte and unrelated tenant value still matches
its snapshot, that a handler/process restart preserves the result, and that SDK
Verify reports the expected durable outcome. Exercise interrupted operations and
provider concurrency refusal in a deployment rehearsal. The focused tests cover
persisted restart convergence, erased suppression, unreadable/missing-sequence
refusal, and competing aggregate/shared authority; the broader processing
recovery suite covers interrupted generations. Neither proves a real provider's
transaction semantics, a production inventory, or live workload authorization.

## Rollback and remaining deployment gate

Keep the drained legacy writer version, actor state, deployment configuration and
routing rollback plan until named owner acceptance. Before admitting new commands,
a failed rehearsal can restore the previous routing to the retained actor view
under the owner's validated procedure. After writes resume, actor bytes may be
stale: drain again and reconcile canonical events and erasure decisions before
routing reads back. Never restore an old snapshot over a newer erasure or revive
an erased party's keys. Preserve SDK recovery data for owner-supported cleanup;
do not delete staged or committed material manually.

DW-58 closes only with an owner-approved deployed rehearsal/cutover receipt that
names the exact images and state provider, proves writer drain and history/key
completeness, verifies persisted and query parity after restart, records survivor
and erasure checks, and demonstrates safe rollback. Successful source tests do
not close topology, package publication/adoption, blocked story, cryptographic
custody or deployment qualification gates.
