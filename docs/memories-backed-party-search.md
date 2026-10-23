# Memories-Backed Party Search

Hexalith.Parties can route party discovery through Hexalith.Memories when `Parties:MemoriesSearch:Enabled` is `true`. Parties remains authoritative: Memories results are candidates, and Parties hydrates every returned party from its own projections before REST or MCP responses are produced.

## Configuration

```json
{
  "Parties": {
    "MemoriesSearch": {
      "Enabled": true,
      "Endpoint": "https://memories.example/",
      "RequireApiToken": true,
      "ApiToken": "<token>",
      "TenantId": "tenant-a",
      "CaseId": "parties",
      "EnabledAxes": [ "hybrid", "syntactic", "semantic", "graph" ]
    }
  }
}
```

When enabled, Parties service registers `MemoriesClient` from `Hexalith.Memories.Client.Rest`. `Hexalith.Parties.Contracts` does not reference Memories packages.

## Modes

- `hybrid`: default rich search through Memories hybrid search.
- `lexical` or `syntactic`: single-axis syntactic search.
- `semantic`: single-axis semantic search.
- `graph`: traversal from a known Memories context.

REST returns the search response envelope. The envelope includes the paged results plus execution status, degraded reason, Memories score metadata, and source metadata. REST also adds status headers for clients that inspect headers before parsing the body:

- `X-Parties-Search-Status`: `Rich`, `Degraded`, or `LocalOnly`.
- `X-Parties-Search-Degraded-Reason`: present when rich search is degraded.

MCP `find_parties` uses the same search service and preserves list behavior when `query` is empty.

## Hydration Rules

Parties omits Memories candidates that cannot hydrate to a readable Parties projection. This includes stale memory units, erased parties, wrong-tenant source URIs, unauthorized parties, wrong-case candidates, and duplicate hits for the same party.

## Erasure Cleanup

Party memory units use stable source URIs:

```text
urn:hexalith:parties:{tenantId}:party:{partyId}
```

Erasure workflows must delete or tombstone mapped Memories memory units. If Memories cleanup fails, Parties records blocked cleanup evidence and must not report erasure as complete until cleanup succeeds or the block is explicitly resolved.

Indexing reserves a content-free ingestion intent through the EventStore SDK before
submitting to Memories. It records the captured case and source, an opaque stable
revision/idempotency identity, and acknowledged workflow/canonical IDs. Personal
content and search metadata are never copied into this recovery inventory. The
mapping list keeps its existing hashed key and JSON list shape, including legacy
rows without `CaseId`; both stores use the configured mapping state store.

A workflow acknowledgement is not an indexed unit. Indexing returns success only
after the published ingestion-status API reports `Completed`, an `Indexed` unit,
and matching workflow, tenant and captured case. Duplicate ingestion can name a
canonical ID different from its workflow ID; the canonical ID is durably retained
and used for deletion. Projection redelivery reconciles acknowledged workflows
without submitting them again. Attempt identity hashes the durable serialized entry
and event context, including the retained `LastIndexedEvents.SequenceNumber` for the
last canonical application. Two contact or identifier events with the same type and
timestamp therefore keep distinct attempts, while restart and redelivery of the
same sequence reuse its producer. Checkpoint-only events retain the prior application
sequence. The projection notification seam forwards this sequence through an additive
default interface overload, preserving existing indexer implementations. Transient
`JsonIgnore` contact/identifier collections cannot create a second producer after
canonical deserialization. A source URI never replaces another
distinct unit in the mapping inventory. These guarantees concern producer recovery
and identity: the selected Memories source-URI deduplication can return an existing
canonical unit for changed content, so a new revision token does not prove content
refresh. Producer revision/update support remains an upstream prerequisite.

Cleanup first persists an admission fence for the tenant/party. A concurrent
registration either enters the cleanup snapshot or fails before submission.
Permanent erasure fences survive restart and never reopen. Projection removals
without an erasure checkpoint are transient: after their obligations settle, a
readable canonical index containing the live party and no erasure checkpoint can
open a new generation. That generation uses new idempotency identities even for
unchanged content. A terminal canonical ID already covered by a settled cleanup
receipt cannot be published as indexed again. The selected producer can permanently
deduplicate by source URI and return the deleted ID despite a new token; this path
retains its new obligation and stays retryable. Live producer reindexing therefore
requires upstream recreation/update support and is not supplied by this consumer
change. Missing or malformed canonical state cannot authorize reopening.
Each known ingestion must reach validated terminal success before its output is
deleted. Cleanup then reads the exact scoped unit and requires HTTP 404 with JSON
error code `MEMORY_UNIT_NOT_FOUND`. DELETE acceptance while the unit remains readable,
arbitrary 404s, missing/failed/expired status, and malformed scope retain obligations.
The endpoint prefix, escaped route segments and captured case remain authoritative.

Intent registration and updates use the SDK write policy with five concurrency
attempts. The legacy mapping adapter retains its five-attempt no-op-aware merge
loop over SDK conditional reads/writes. Cleanup subtracts only exact verified
snapshot records, preserving concurrent arrivals and changed identities. Domain
inventories retain empty values and their ETag generation; physical deletion is
limited to unique reserved health probes through the SDK conditional eraser.

Mapping failure leaves the acknowledged remote producer and its durable intent
available for projection redelivery after restart. Index recovery never issues a
compensating DELETE: deleting a producer while retaining its cached canonical ID
would make later redelivery falsely report indexed. Caller cancellation after
acceptance triggers an independent five-second persistence/reconciliation attempt,
then rethrows cancellation. Confirmed cleanup progress uses its own five-second
persistence budget.

Verified cleanup moves exact obligations into durable content-free canonical
receipts. A writer suspended before its mapping write can resume after cleanup or
process restart; its final generation/admission check returns retryable, while the
receipt still resolves its late mapping to the right canonical unit. Cleanup does
not wait for a process-local lock or assume an old writer died after a timeout.
Receipts remain explicitly settled in persistence and are never silently evicted.
They are absence evidence for matching late mappings, not active remote work: unused
receipts issue no HTTP requests, and matching mappings can settle after remote
credentials or case access disappear. A distinct legacy workflow resolving to the
same cleaned canonical ID retains its own workflow association before subtraction.

An intent with uncertain acceptance and no durably captured workflow ID remains
blocked. It is not resubmitted automatically: the old request may still be a live
producer. Source lookup finds one identity in one known case and cannot prove a
complete inventory or settle an unknown acceptance. Pending workflows are polled
once per indexing/cleanup delivery, with later canonical projection redelivery or
erasure verification supplying retries; no unbounded polling worker is introduced.
Each party inventory retains at most 1,024 attempts and cleanup receipts together.
Exhaustion blocks new submissions without evicting obligations or provenance.
Verified submission preflight rejection permits another submission after repair:
HTTP 401/403; HTTP 400 `INVALID_INPUT`; HTTP 404 `TENANT_NOT_FOUND`; HTTP 429
`RATE_LIMIT_EXCEEDED`; or HTTP 409 `TENANT_PROVISIONING`, `TENANT_DELETING`,
`TENANT_FAILED`, `TENANT_UNAVAILABLE`.
The selected Memories implementation emits these before dedup reservation/scheduling.
Other status/code combinations remain uncertain. A status-read authorization failure, connection
failure, timeout or ambiguous acknowledgement preserves the same unresolved
reservation and never authorizes resubmission.

Legacy mapping IDs are only candidate workflow handles. Cleanup must validate their
terminal status and can resolve a different canonical output ID. Under the cleanup
fence, that validated association must persist before deletion; persistence failure,
five exhausted conflicts, or capacity exhaustion retains the mapping and prevents
DELETE. A later subtraction failure cannot erase this canonical provenance, so a
restarted cleanup no longer depends on the original workflow status. Where the old
workflow status expired before its provenance was durably captured, the legacy mapping stays blocked;
a source lookup miss is insufficient to clear it. No production migration runs.
Drain older Parties writers before relying on the new guarantees, and do not expire
or delete empty ledgers or fences while operations may be in flight. This forward
fix cannot reconstruct obligations lost before its installation. DW-82 remains open:
a locally empty inventory does not establish authoritative absence of historical
remote units after state loss or configuration drift.

## Health when indexing is disabled

`Parties:MemoriesSearch:Enabled=false` stops rich-search indexing, but existing
Memories units can still require erasure. The `memories-search` health check always
runs a unique mapping-store sentinel through record, read and cleanup. With retained
endpoint, tenant, case and required credentials, it also probes the cleanup DELETE
route and the ingestion-status GET route without requiring a rich-search client. Failed probes or incomplete retained
configuration report `Degraded`; local display-name search remains available.

For a wholly unconfigured local-only installation, a successful mapping probe
retains `Healthy`. Its data explicitly reports `cleanupConfigured=false`,
`cleanupProbeSkipped=true`, and `cleanupRouteReachable=false`, with a reason that
remote cleanup was not verified. This is not evidence that historical mappings or
remote units are absent. Retain cleanup configuration after disabling indexing
until outstanding cleanup obligations have been reconciled. The cleanup DELETE probe establishes route capability in the configured tenant/case;
it establishes neither a complete remote inventory nor terminal erasure. This check contributes to `/health` and does not gate `/ready`.

The status-read probe accepts only a matching structurally valid workflow status
envelope or the structured synthetic `INGESTION_STATUS_NOT_FOUND` response. Empty,
HTML and unrelated successful responses do not qualify. This establishes routing
behavior and reports explicit authorization rejection; it does not establish access
to an existing tenant workflow or terminal erasure.

The cleanup HTTP client captures its endpoint and API token when Parties starts.
Restart Parties after changing either value, including when repairing retained
cleanup configuration. The health check reads current options, but an existing
client continues to use its startup endpoint and credentials until restart.

## Recovery evidence and operator limits

Inspect the SDK-backed mapping inventory and ingestion inventory using authorized
state inspection; use `PartyMemoryUnitMappingStore.BuildKey` semantics to locate
the hashed tenant/party key and replace `memory-mapping` with `memory-ingestion`
for its companion. Retain ETags, captured case/source, attempt/generation,
workflow/canonical IDs, rejection state, admission flags and cleanup receipts.
Do not copy content, bearer tokens or provider responses into public receipts.
A missing key, source lookup miss or empty local list cannot settle unknown
producers or establish authoritative remote inventory (DW-82).

For a captured workflow, read the published ingestion-status route with the same
application principal and tenant authorization used by Parties. Record HTTP status,
instance/tenant/case agreement, runtime terminal state and canonical indexed unit ID.
Then issue scoped per-unit DELETE and record a subsequent scoped GET with structured
`MEMORY_UNIT_NOT_FOUND`. Retry canonical projection delivery or erasure verification
once configuration is repaired; each delivery performs bounded work. Pending,
failed, expired or unavailable workflow evidence remains blocked. Retain an uncertain
reservation without a workflow handle until the producer owner supplies authoritative
scheduling/terminal evidence; neither deleting the intent nor manually replaying
submission is a safe repair. No automatic ledger reset or production migration is
provided. Retained receipts may eventually exhaust capacity; operator retirement
requires independent proof that all older writers are drained and that no producer
or mapping still needs that provenance. There is no automatic expiration.

Health data includes `ingestionStatusRouteReachable`, status/reason and
`ingestionStatusAuthorizationVerified=false`. The synthetic status probe reports
401/403 rejection and qualified route misses, but the selected Memories endpoint
checks tenant ownership only for an existing workflow. Therefore a synthetic miss
cannot prove status-read authorization. Deployment capability evidence must also
include a successful read of an existing owned workflow and tenant/case validation;
DELETE probe success alone does not qualify cleanup or physical erasure.

The optional owned-runtime fixture attempts bounded case/tenant teardown on both
success and failure, retaining private handles and teardown outcomes beside its
private runtime configuration when completion cannot be observed. It does not
force delayed ingestion/deletion. The selected runtime qualification remains blocked
by Memories' authenticated EventStore gateway prerequisite, so DW-168 stays open
and this batch makes no physical-erasure qualification claim.

The independent `PARTIES_MEMORIES_STATE_RUNTIME_CONFIG` opt-in test exercises the
actual EventStore SDK/Dapr serialization and conditional persistence without tenant
provisioning or a Memories producer. It can qualify mapping/intent wire compatibility
while the remote workflow prerequisite remains blocked; it does not qualify erasure.
Its two independent SDK clients also reject a stale ETag after a competing update
and permit exactly one competing absent-key creation, inspecting persisted winner
bytes independently. Mapping and projection stores may have distinct configured
names; ingestion inventories always follow the mapping-store configuration.
