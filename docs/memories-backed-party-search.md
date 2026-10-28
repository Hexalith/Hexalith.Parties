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
canonical deserialization. A source URI never replaces another distinct unit in the
mapping inventory. With the selected Client.Rest/Contracts 3.0.3 packages or a
validated mapped-source build, Parties uses Memories' explicit refresh API with
the canonical application sequence as its monotonic source revision and the
observed canonical predecessor. An exact retry binds content bytes,
content type and metadata to the same token. Publication atomically changes Redis
visibility and dedup pointers; graph selection mirrors the current source revision.
A missing canonical sequence blocks before local reservation or remote submission; canonical redelivery can then proceed. A lost acceptance acknowledgement is recovered by bounded, scope-checked source inventory and the exact persisted retry-token digest. The recovered handle still requires an independent successful workflow status before mapping publication.

The Redis publication transaction also retains a graph repair journal and advances a graph-read epoch. Graph traversal and graph-scoped search repair pending work before reading and reject a snapshot that overlaps a newer publication. Graph write failure or exhausted workflow retries therefore retain durable repair authority; retired traversal neighbors cannot appear in edge metadata.

Superseded candidates remain in the inventory and are physically compensated. A newer
revision that loses a predecessor race remains retryable through a durably linked
successor attempt; an older revision cannot rebase over it. Ordinary ingestion keeps
its existing dedup behavior. Only the qualified 3.0.3 package selection enables
this refresh capability; other package versions retain the legacy ingest path
until separately qualified. A new token alone never qualifies refresh. Explicit
refresh also requires
the authenticated `X-Hexalith-Source-Refresh: source-refresh-v1` acknowledgement. A legacy
producer that ignores the new fields leaves an unresolved durable obligation and cannot
return qualified indexing success.

Mapped-source Memories sends actor/source metadata through the SDK's signed
`api/v1/commands/bound/workload` channel. The ordinary unsigned workload channel
rejects those metadata fields. The additional resource proof binds the exact command,
tenant, domain, represented actor and metadata; an independent workload assertion and
Dapr credentials remain required. Provision an RSA key of at least 2048 bits through
`EventStore:CommandGateway:BoundCommand:PrivateKeyPkcs8Base64` (the legacy
`EventStoreIntegration` prefix is also read). The receiving gateway must trust its SPKI
public key at `Authentication:BoundStreamRead:PublicKeys:<exact-workload>` and authorize
only `memories-tenants,memories-cases,memories-memory-units` at
`Authentication:BoundCommand:AllowedDomains:<exact-workload>`. The workload name must
match `Authentication:WorkloadIssuer:Workload`; the receiving Dapr ACL must allow the
exact bound-command POST. Missing proof configuration fails closed before submission.
The owned Development fixture generates ephemeral test keys; this change provisions no
production keys. The selected EventStore 3.128.4 and Memories 3.0.3 packages
supply the consumer capabilities; installed producer workload/trust configuration and deployment
qualification remain separate prerequisites.

Authoritative commands require the separate `Hexalith.Memories.DomainService` host.
The optional local AppHost flag `EnableMemoriesDomainService=true` registers its real
SDK processor for exactly `memories-tenants`, `memories-cases` and
`memories-memory-units`, version `v1`, with the native `/process` method. The native
Memories Server retains its REST/pub/sub routes and exposes no `/process` route.
The dedicated host executes existing decisions and folds, including snapshot/tail and
administrative replay, using unchanged event identities. Publish/configure that workload
and its exact EventStore caller, audience, app-channel token and deny-default Dapr ACL
before enabling this capability in a deployment. Existing installations acquire
neither a new host nor domain registrations unless explicitly configured.

Both the `EventStore` and legacy `EventStoreIntegration` gateway prefixes are supported.
An explicit legacy `/v1.0/invoke/<app>/method/` base address is normalized to one SDK
header-routing mode, retaining any preceding path and exact target. A conflicting
configured AppId is rejected; direct configured prefixes retain their path semantics.

Cleanup first persists an admission fence for the tenant/party. A concurrent
registration either enters the cleanup snapshot or fails before submission.
Permanent erasure fences survive restart and never reopen. Projection removals
without an erasure checkpoint are transient: after their obligations settle, a
readable canonical index containing the live party and no erasure checkpoint can
open a new generation. Memories independently requires the exact observed version
of a complete, settled, transient source fence before reopening. Permanent source
fences reject every new producer path, including ordinary ingest, and unit tombstones
prevent delayed modern writers from recreating erased Redis, graph or source artifacts.

Cleanup fences the exact remote tenant/source and reads bounded inventory pages even
when local mappings are empty. Every discovered workflow/canonical output is persisted
before the next page is requested. Protected SDK cursors bind tenant, source and snapshot
version; concurrent admission or terminal/deletion updates invalidate the continuation
and require a fresh delivery. Coverage defaults to incomplete, including new inventory
keys over legacy tenants. Dedup pointers, search results and the lossy maintenance
in-flight registry never establish historical coverage. Only explicit owner
reconciliation of the fenced historical scope can establish the coverage marker.
Missing, corrupt, interrupted, unauthorized or incomplete inventories retain obligations
and block completion. No production reconciliation/migration is executed by this change.

Activating the 3.0.3 protocol does not upgrade retained legacy intents. An intent
without the original refresh protocol, canonical revision, body digest or producer
authority cannot become refresh proof, even if it already names a canonical unit.
Such redelivery remains retryable and retains its obligation. Do not infer missing
proof, clear ledgers, replace retry identities or blindly resubmit an uncertain
admission. Before rollout, drain older writers and have the qualified owner
reconcile each retained intent against its exact producer, tenant, source, case,
workflow terminality and physical output. Unknown historical inventory remains
incomplete until separately attested. No automatic legacy-intent migration or
production reconciliation is supplied here.

If qualification or reconciliation is unavailable, pause new rich-search indexing
and retain the existing endpoint, credentials, ledgers, fences and cleanup
configuration. A rollback must use the retained, qualified pre-cutover binary and
configuration under owner control, preserve every durable obligation and authority
binding, and reconcile work admitted by either version before writers resume.
Disabling indexing or returning to legacy ingest proves neither cleanup nor
historical absence; do not roll back a producer fence or erase refresh obligations.

A canonical output or producer deletion marker does not prove workflow completion.
Cleanup independently validates terminal evidence while output-known producers are
still running. It then deletes every exact captured-case output and requires a scoped
HTTP 404 with JSON code `MEMORY_UNIT_NOT_FOUND`. DELETE acceptance while a unit remains
readable, arbitrary 404s, missing/failed/expired status and malformed scope retain work.
Legacy restore writes invalidate the exact source's coverage before their first memory-unit
write, retain canonical output identities, and report `restore-reconciliation-required`.
The existing restore contract does not provide per-output terminal workflow proof; owner
reconciliation must drain it before certifying coverage again. Raw ordinary/restore
Redis writes atomically consult unit tombstones/source fences and recompute managed
visibility from the current canonical pointer. Restored raw visibility flags cannot
reveal superseded units. No production restore is executed by this qualification.

A final complete, terminal source snapshot under the admission fence is required before
persisting permanent source-closure evidence. Exact canonical receipts can settle late
local mappings after credentials disappear; those receipts alone cannot certify unknown
remote work. Global closure and canonical absence receipts bind the normalized effective
producer endpoint; moving to another server or route prefix revalidates that authority,
even when case/workflow/unit IDs coincide. New admissions capture the typed client's
actual target, independently of later options changes. Missing legacy authority remains
explicit and cannot provide a cached absence proof. The endpoint prefix, escaped route
segments and captured case remain
authoritative.

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

An intent with uncertain acceptance is never blindly resubmitted. The complete source
inventory can recover the admitted workflow by its persisted token digest after a lost
HTTP acknowledgement. Source lookup alone finds one identity in one known case and
cannot clear unknown acceptance. Pending workflows are observed once per delivery;
canonical projection redelivery or erasure verification supplies retries. Each source
inventory and consumer ledger retains at most 1,024 obligations/receipts without eviction;
pages contain at most 100 entries, and malformed or oversized responses fail closed.

Verified preflight rejection permits retry after repair: existing authentication,
validation, tenant-state and rate-limit errors plus explicit source revision conflict,
stale revision, retry-body mismatch and admission-closed errors. Other response/code
pairs and connection failures preserve uncertain acceptance. Source publication stages
tracked Redis writes before pointer publication. Native Redis predicates and graph
selection run before TopK/paging, so superseded candidates cannot starve current content;
ordinary deployed index schemas gain the optional visibility field idempotently when
refresh is first enabled. Unchanged tenants retain their existing query path and paging.
A detected publication race returns retryable `SOURCE_PUBLICATION_CHANGED`, preserving
honest counts rather than returning a partially filtered page.

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
fix cannot reconstruct obligations lost before installation. Legacy tenant/source
coverage therefore remains incomplete until a qualified owner reconciliation drains
older writers and enumerates historical cases/units. The synthetic runtime protocol is
explicitly limited to newly created owned test tenants and does not initialize legacy
production coverage. Source adapter/component evidence, package publication and actual
live erasure qualification are recorded independently in the batch receipts.

## Health when indexing is disabled

`Parties:MemoriesSearch:Enabled=false` stops rich-search indexing, but existing
Memories units can still require erasure. The `memories-search` health check always
runs separate unique mapping-store and ingestion-inventory sentinels through the
actual SDK serializers, conditional writes, read-back and cleanup. With retained
endpoint, tenant, case and required credentials, it also probes the cleanup DELETE
route and the ingestion-status GET route without requiring a rich-search client. Failed probes or incomplete retained
configuration report `Degraded`; local display-name search remains available.

For a wholly unconfigured local-only installation, successful mapping and inventory
probes retain `Healthy`. Its data explicitly reports `cleanupConfigured=false`,
`cleanupProbeSkipped=true`, and `cleanupRouteReachable=false`, with a reason that
remote cleanup was not verified. This is not evidence that historical mappings or
remote units are absent. Retain cleanup configuration after disabling indexing
until outstanding cleanup obligations have been reconciled. The cleanup DELETE probe establishes route capability in the configured tenant/case;
it establishes neither a complete remote inventory nor terminal erasure. This check contributes to `/health` and does not gate `/ready`.

The inventory sentinel uses the reserved `_health-probe` tenant and a unique
`_health-probe-<guid>` party. The store must support SDK expiring writes and
conditional erasure; missing capabilities degrade health before any probe write.
Probe registration uses a one-minute TTL, so failed cleanup cannot retain a
permanent row. Normal ingestion inventories remain durable. The probe writes
the real ingestion-inventory shape and conditionally erases only that exact
sentinel with its current ETag. Cleanup has
an independent five-second budget even after request cancellation or a lost write
response. An altered sentinel, missing ETag, cleanup conflict or unverifiable
absence reports failure; normal inventories and erasure fences are retained.
A cleanup failure does not replace an earlier read/write/cancellation failure.
Both indexing modes expose `ingestionInventoryReachable` and a sanitized
`ingestionInventoryReason` alongside the separate mapping capability. A Redis ACL
that permits the mapping prefix but denies the ingestion prefix therefore reports
`Degraded`; granting that prefix repairs the health result.

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
private runtime configuration when completion cannot be observed. The selected-source harness requires bounded controlled pauses before first materialization,
after output persistence but before terminal acknowledgement, and during physical deletion.
The owned selected-source qualification executes the separate real domain-service host,
authenticated gateway and native workflows. It verifies changed current content, restart,
lost acknowledgement, all three pauses, physical absence across derived stores and
unrelated tenant/case survival. Independent actor bytes match authenticated SDK native
stream reads, and the actual domain host reconstructs first/final state through the
gateway's administrative replay route. This is disposable component qualification with
fake embeddings; it supplies no production coverage reconciliation, package publication
or deployed whole-topology claim. Exact final counts and owned cleanup are recorded in
the batch receipt.

The independent `PARTIES_MEMORIES_STATE_RUNTIME_CONFIG` opt-in test exercises the
actual EventStore SDK/Dapr serialization and conditional persistence without tenant
provisioning or a Memories producer. It can qualify mapping/intent wire compatibility
independently of the full producer workflow control; it does not by itself qualify erasure.
Its two independent SDK clients also reject a stale ETag after a competing update
and permit exactly one competing absent-key creation, inspecting persisted winner
bytes independently. Mapping and projection stores may have distinct configured
names; ingestion inventories always follow the mapping-store configuration.

## Explicit JWT algorithm policy

The selected Memories owner source replaces initialized JWT algorithms when
`Authentication:JwtBearer:ValidAlgorithms` is explicitly configured. An RS256-only
or ES256-only list remains exact; absent configuration retains the historical
HS256/RS256/ES256 defaults. Invalid empty or blank lists fail startup validation.
The public init-only options setter and existing options-monitor notifications
are preserved. Signed-token and named bearer-options checks qualify source;
matching package publication and the separate Dapr publisher secret-binding gate
remain independent requirements. See the [owner configuration batch receipts](../_bmad-output/implementation-artifacts/tests/five-owner-config-and-recovery-deferred-items-2026-10-23/README.md).

Review-loop lifecycle correction (2026-10-24): admission retains source claim-check identity before preparation, then distinguishes proven pre-scheduling failure from an ambiguous scheduling call. Missing workflow state and terminated work remain unresolved. After a source fence, independently scoped failed workflows and proven unscheduled producers physically reconcile failed-unit hashes, case indexes, source payloads and derived stores before terminal proof is retained. Durable deletion receipts repair historical output/reverse-alias interruption on later inventory reads. The earlier receipt remains historical; the review-loop receipt records the executed final controls separately.

Producer payload retention also records a bounded per-write token before each Dapr save. Only an affirmative Dapr acknowledgement settles it. Unit purge installs a permanent writer fence and refuses completion while any save may still materialize; restart and elapsed time cannot discard that authority. Ambiguous save completion therefore remains an explicit unresolved obligation until independently resolved. Failure-record persistence checks the same permanent unit tombstone atomically with its hash/index write, preventing delayed failure metadata from reappearing after cleanup.

Review-loop 2 retains each original explicit-refresh body digest and canonical sequence in the SDK consumer reservation before submission. Canonical redelivery can omit ignored search collections or change delivery context while recovering that exact admitted identity. A never-submitted supersession successor receives its own body binding before admission; submitted or ambiguous attempts keep their original binding. Legacy acknowledged intents without exact authority and refresh proof remain unresolved.

Source terminal proof also covers a distinct discarded post-index dedup candidate: its derived rows, failed-unit metadata and claim-checked payloads must be physically reconciled before the canonical winner's producer can settle. Lost output acknowledgements retain the winner obligation even when the producer fails. Annotation graph failure may record Unscheduled only from durable execution history proving no child call was attempted; ambiguous child calls and lost Preparing executions remain fenced.

SDK source inventory reads use headers-only completion, a one-MiB streamed byte limit, bounded entries/cursors, caller cancellation and the existing total HTTP timeout through body decoding. Inventory deletion receipt repair uses one exact batched read and a bounded CAS repair. Modern refreshes use retained publication-key provenance; unsealed legacy sets use a bounded exact-unit native vector-index lookup and receive a completion marker only after every discovered key is retained. Missing or incompatible native discovery requires owner reconciliation. No refresh scans the Redis keyspace.

Final executed evidence: [review-loop 2 receipt](../_bmad-output/implementation-artifacts/tests/five-memories-inventory-and-publication-2026-10-24/review2-verification.json).


Review-loop 3 (2026-10-24): every late materialization of a publication-managed unit joins its exact bounded key set, including untracked NL and chunk maintenance writes. Source, alias, refresh/publication and claim-check authority keys have a bounded tenant ownership ledger. Tenant teardown installs a permanent content-free fence and reconciles actual claim-checked bytes before backend deletion; unresolved payload or graph write acknowledgements block success across restart. Each graph invocation has independent authority. An explicit graph-command ACL denial proves non-execution; timeouts and lost acknowledgements do not. The native registry's absent-tenant path also drains this technical ownership.

Admission uses the existing authorization bound of 128 tenant characters, 4096 source characters, and 512 case/workflow/candidate/predecessor characters, rejecting control characters before preparation or durable admission. Accepted boundary identities are read through the actual public SDK. Superseded refresh compensation records its deleted output for independent terminal proof; mutation receipt reads use one bounded multi-get. Graph publication overlap returns retryable 503 with error telemetry, and raw cleanup inventory reads retain the configured total HTTP deadline through body completion. The original public constructor signatures remain available, with production DI choosing the tenant-aware adapters.

Unknown historic coverage remains incomplete. DW-196, DW-197 and DW-198 retain the pre-existing legacy NL migration, queued NL retry erasure and sparse-keyspace deletion-discovery follow-ups. Matching package publication/adoption, deployment of the workload topology and production qualification remain separate gates. Earlier receipts are historical after these corrections; final loop3 results are recorded in the bounded batch receipt.
