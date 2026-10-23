---
title: 'Make Memories indexing and cleanup durably recoverable'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 3
implementation_status: 'complete'
verification_status: 'passed-with-live-qualification-deferred'
completion_scope: 'consumer-recovery-implementation'
baseline_commit: '6c83309d08a10f36a0da866521b9d0da6f8fc998'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Memories ingestion and deletion are asynchronous. Parties currently records accepted workflow IDs as unit IDs, and mapping failures or cancellation can lose cleanup obligations. DELETE acceptance can remove mappings before deletion finishes.

**Approach:** Complete DW-81, DW-83, DW-167, DW-168 and DW-170 together: use EventStore persistence for mappings, retain durable ingestion intents, reconcile canonical terminal IDs, and keep cleanup obligations until known ingestion and deletion finish. Commit validated changes and push `main`, as requested.

## Boundaries & Constraints

**Always:** Preserve hashed mapping keys, legacy list JSON/CaseId, tenant/case isolation, exact-snapshot removal, empty-list ETags and reserved probe deletion. Use published EventStore 3.126.1 and Memories 2.27.1 APIs without changing dependencies/gitlinks. Store metadata through SDK persistence, without personal content. Bound reconciliation attempts; retain obligations across restart.

**Never:** Treat acceptance, arbitrary 404s, missing status or source lookup misses as terminal cleanup. Preserve distinct units sharing a source URI. Leave DW-82 authoritative inventory open. No production migration or deployment.

## I/O & Edge-Case Matrix

| State | Expected behavior |
| --- | --- |
| Intent persistence fails before ingest | No remote submission; delivery remains retryable. |
| Acceptance or workflow ID cannot be durably recorded | Retain unresolved intent; cleanup stays blocked. |
| Completed duplicate workflow returns another unit ID | Validate instance, tenant and case; retain the canonical output identity for deletion. |
| Pending, failed, expired or malformed workflow evidence | Retain obligation; no indexed/cleaned success without sufficient evidence. |
| DELETE succeeds while unit remains readable | Retain mapping and retry observation. |
| Known terminal ingestion followed by structured unit absence | Remove only the exact resolved obligation, preserving concurrent arrivals. |
| Caller cancels after acceptance | Attempt bounded recovery independently, preserve durable intent, then rethrow cancellation. |

</frozen-after-approval>

## Code Map

The resumed worktree contains the reviewed implementation and unrelated CI/Aspire/publication changes. Preserve all unrelated files and the working SDK compatibility behavior. Re-derive only the affected recovery paths from the current implementation; the complete prior implementation is retained privately in the iteration-1 preservation patch.

Paths are repository-relative. `S` = `src/Hexalith.Parties/Search/`; `T` = `tests/Hexalith.Parties.Tests/`.

- `S/PartyMemoryUnitMappingStore.cs`, `IPartyMemoryUnitMappingStore.cs`, `PartyMemoryUnitMappingEntry.cs`: legacy lists, five-attempt concurrency and exact cleanup identities.
- `S/PartyMemoryIndexingService.cs`, `PartyMemoryCleanupService.cs`, `PartyMemoryCleanupResponse.cs`: acceptance currently precedes durable mapping and terminal deletion.
- `S/PartyMemoryIndexEntrySearchIndexer.cs`: reuse canonical projection redelivery for retries.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`: SDK store already registered; cleanup remains available when indexing is disabled.
- `T/Search/PartyMemoryMappingState.cs` and mapping/concurrency/cancellation/cleanup tests: serialized state, ETags and failures.
- Published APIs: `IReadModelStore`, `ReadModelWritePolicy`, `IReadModelConditionalEraser`; Memories workflow status, idempotency tokens and per-unit reads. Source lookup finds one known-case identity, not a complete inventory.

## Tasks & Acceptance

**Execution:**

- [x] `S/PartyMemoryUnitMappingStore.cs` and test helpers: replace Dapr persistence with SDK APIs, preserving legacy bytes, no-op behavior and concurrency (DW-81).
- [x] Add `S/PartyMemoryIngestionIntent.cs`, `IPartyMemoryIngestionIntentStore.cs`, `PartyMemoryIngestionIntentStore.cs`: durable attempt/case/source metadata, optional workflow/canonical IDs and unresolved state. Conditional registration/cleanup must not miss concurrent attempts; capacity exhaustion blocks submission without evicting obligations.
- [x] `S/PartyMemoryIndexingService.cs`: persist intent before submission, use stable idempotency identity, validate terminal output scope, preserve distinct units and retain obligations through mapping/compensation failure and cancellation (DW-83/167/170).
- [x] `S/PartyMemoryCleanupService.cs`, `PartyMemoryCleanupResponse.cs`: reconcile intents, require terminal ingestion, delete and observe qualified absence before subtracting mappings (DW-168). Source lookup cannot clear uncertain acceptance.
- [x] Registration and `T/Search/` tests: cover restart, conflicts, duplicate IDs, delayed ingestion/deletion, malformed scope, missing status, double failure and cancellation. Preserve health probes without calling their success erasure proof.
- [x] `docs/memories-backed-party-search.md`, `tests/README.md`, deferred ledger and batch evidence: document compatibility and recovery; close only verified entries after independent review, retaining DW-168 live qualification as open.

**Review iteration 2 completion tasks:**

- [x] `S/PartyMemoryIndexingService.cs` and adapter regression tests: derive attempt identity from durable entry/context revision fields that survive canonical serialization; changing JsonIgnore search collections on redelivery cannot create a second submission.
- [x] `S/PartyMemoryIngestionIntentStore.cs` and cleanup/indexing adapters: retain an explicit settled cleanup-receipt distinction. Never publish a known-cleaned canonical ID in a later generation. Reuse settled absence evidence for matching late mappings without issuing remote work for unused receipts. Do not claim new-token producer reindex support; the selected source-dedup behavior can leave live re-add retryable until an upstream capability exists.
- [x] `S/IPartyMemoryIngestionIntentStore.cs`, store and cleanup service: persist validated legacy workflow/canonical provenance under the cleanup fence before deletion. Store failures/conflicts/capacity exhaustion must retain the mapping and prevent deletion. Restart after subtraction failure must not depend on expired workflow status.
- [x] `S/PartyMemoryIngestionEvidence.cs`: allow exact 429/RATE_LIMIT_EXCEEDED pre-scheduling rejection to retry after repair; preserve uncertain status/code combinations.
- [x] `T/Search/PartyMemoryTestReadModelStore.cs`, coordination and persistence runtime tests: distinguish configured stores; cover receipt-bearing capacity, settled receipt reuse after remote authorization/case loss, faithful source-dedup stale IDs, legacy mapping subtraction failure/status expiry, two-client stale ETags and competing absent-key creation.
- [x] Documentation/evidence: distinguish persisted recovery/absence proof from producer content refresh and live reindex support; retain upstream prerequisites without changing producer dependencies or gitlinks.

**Review iteration 3 completion tasks:**

- [x] `src/Hexalith.Parties.Projections/Search/IPartyIndexSearchIndexer.cs`, `Handlers/PartyIndexSdkProjectionHandler.cs`, `S/PartyMemoryIndexEntrySearchIndexer.cs` and context/indexing records: forward the retained `PartyIndexEventMetadata.SequenceNumber` for each canonical application and reconciliation. Use an additive default-compatible interface overload/seam so existing implementations retain behavior. Include this sequence in durable attempt identity, without using transient search collections. Split modified mapper/context/unit types into individually named files if required by repository C# rules.
- [x] `T/Search/PartyMemoryCoordinationTests.cs`: use real projection handlers/adapters for two different contact/identifier events with identical event type/timestamp and distinct sequences; prove distinct attempts/submissions and stable same-sequence restart/redelivery. Resolve the actual `AddParties` production `IPartyIndexSearchIndexer` registration for transient removal/re-add and assert durable generation/mapping behavior. Cover cleanup of persisted definite rejection before repair and retained permanent fencing.
- [x] `S/PartyMemoryCleanupService.cs` status probe and tests: reject arbitrary successful HTML, empty or unrelated status responses; accept only a matching valid status envelope or qualified synthetic missing-status response. Do not claim tenant authorization or erasure proof.
- [x] `T/Search/PartyMemoryTestReadModelStore.cs`: serialize dictionary access and compare/write sections atomically, with async hooks outside the lock.
- [x] `T/Search/` real-REST-client regression tests: exercise actual published ingestion request/token/acknowledgement and qualified preflight rejection/repair behavior through controlled HTTP handlers and durable shared stores.
- [x] `T/Search/PartyMemoryPersistenceRuntimeTests.cs`: bounded finally cleanup of only generated synthetic mapping/intent/conflict keys, with fresh ETags and primary failure preservation. Keep two-client conflict assertions and all existing wire compatibility proof.
- [x] Documentation/evidence: record the canonical sequence identity and additional behavioral qualification; keep producer content refresh and selected-runtime delay/physical-erasure prerequisites unclaimed.

**Acceptance Criteria:**

- Given legacy inventories, when upgraded writers race cleanup, then serialized inventories stay readable and concurrent entries survive retries.
- Given acknowledged ingestion, when mapping/compensation fail or cancellation interrupts persistence, then a restarted service retains a recovery obligation and cannot certify cleanup prematurely.
- Given duplicate ingestion, when terminal output names another canonical unit, then restart and erasure retain that deletable ID.
- Given pending ingestion/deletion, when cleanup retries, then obligations remain until qualified terminal evidence exists.
- Given verified changes, when closing the batch, then reviewed evidence and exact commitlint validation precede committing and pushing `main`.

## Implementation Notes

- SDK-backed legacy mapping inventories preserve exact snapshots, empty-list ETags and reserved probe deletion. Distinct units sharing a source remain retained.
- A bounded SDK intent inventory reserves each hashed revision before submission and retains workflow/canonical IDs through restart, mapping failure and cancellation. Cleanup closes admission and requires scoped terminal ingestion plus structured unit absence.
- Matrix audit: all seven rows have passing focused coverage; see the recorded test-name mapping. The optional live fixture is separate qualification and is not counted as passing matrix evidence.
- DW-168 closure remains open: the selected runtime accepted tenant provisioning but its workflow could not initialize the registry because Memories' EventStore gateway transport lacks authenticated routing. Direct gateway requests return HTTP 401; the sidecar fallback also fails authentication/ACL checks. The approved verification rule requires withholding physical-erasure qualification. No production dependency or producer source changes are included.
- Concurrent CI/Aspire/publication edits in the shared worktree are outside this batch. Validation uses an isolated canonical checkout at the baseline with only this batch's files and the original root gitlinks.

## Spec Change Log

- Review iteration 1: the initial plan omitted admitted mapping writers, transient rebuild removal, definite rejection retries and compensation redelivery. Re-derive durable coordination so cleanup cannot retire canonical provenance before admitted writers can publish; a resumed writer cannot report indexed after cleanup. Preserve supported live re-add/rebuild recovery while permanent erasure stays fenced. Consult the existing canonical projection checkpoints when distinguishing the paths; do not silently reopen erased identities. Keep stable idempotency identities on ambiguous acknowledgement; only proven pre-acceptance rejection may re-enable submission. Avoid issuing compensating deletion for a durably retained producer if doing so would make redelivery falsely indexed. KEEP: all SDK mapping compatibility, exact subtraction, distinct-source-unit preservation, bounded persistence/cancellation, privacy, terminal scoped evidence, and the seven frozen matrix behaviors. Tests must use shared serialized intent state across real index/cleanup adapters and service recreation. Add changed-revision, definite rejection, late writer after cleanup, compensation/redelivery and real-adapter live-readd regressions. Document practical operator evidence/recovery limits and include status-read authorization in capability evidence. Keep DW-168 open: the attempted live fixture is blocked by the upstream authenticated gateway prerequisite and does not force delayed producer/deletion cases.

- Review iteration 2: R2-B2/B3/B5/B6 and E1/E2 exposed non-frozen design gaps: transient-content hashing, known-cleaned source-dedup IDs, volatile legacy canonical evidence, and settled receipts treated as active remote work. Amend the affected tasks above and re-derive those paths. KEEP: SDK legacy serialization, hashed keys, exact conditional subtraction, distinct-source-unit preservation, durable admission and cancellation, bounded inventory, privacy, terminal scoped evidence, permanent erasure fences and all seven frozen matrix behaviors. Preserve the existing dirty implementation rather than blanket-reverting user-owned work; the prior staged implementation is retained as a private positive-preservation patch. Include the independent patch/gap regressions R2-B4/B9/B10/V1 in this re-derivation. Source-content refresh and additional deployment-specific health capability evidence remain separate verified/unverified deferrals.

- Review iteration 3: R3-B1/E1 show that persisted fields plus event type/timestamp are insufficient for revision identity when distinct contact/identifier events share a timestamp. Forward the existing retained canonical sequence through an additive compatible module-owned seam and verify real replay behavior. KEEP: all R2 safety fixes, settled receipt semantics, precise legacy associations, configured-store routing, bounded rejection repair, exact SDK subtraction, privacy and the frozen seven-row matrix. The prior implementation is preserved privately rather than reverting approved user work. Incorporate the direct R3-B4/B7/B8/B9/V1/V2 test and probe corrections in the re-derivation; retain prior carried prerequisite dispositions.

- Review pass 4: the edge-case and verification-gap layers found no remaining issue. Apply the small R4-B2/B4/B5/B7/B10 corrections without changing the frozen scope: canonical-sequence identity must ignore rebuild-only markers, delayed removal must respect a live canonical target, equivalent terminal outputs reuse scoped absence, and settled receipts must persist verified progress even if exact mapping subtraction is interrupted. Preserve all earlier source compatibility, privacy, permanent-fence and retention guarantees. Baseline searchable-content reconstruction and unverified status-retention scheduling remain separate deferrals.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | high | bad_spec | Every removal calls BeginCleanupAsync; the permanent fence rejects the established live re-add rebuild retry. Re-derive lifecycle distinction against canonical erasure checkpoints. |
| B2 | high | patch | RecoverAsync deletes the canonical unit, while redelivery trusts its cached ID and returns indexed. Preserve the durably tracked producer instead of deleting it. |
| B3 | high | bad_spec | A writer paused before RecordMappingAsync can resume after the cleanup mapping reread and intent removal. Its canonical mapping loses workflow provenance. Re-derive durable writer/publication coordination. |
| B4 | medium | bad_spec | The SDK exposes definite 401 rejection but the retained null-workflow reservation prevents retry after credential repair. Distinguish proven rejection from ambiguous acceptance. |
| B5 | medium | patch | CreateIndexer constructs separate fresh stores; serialized retry does not retain reservations. Share a serialized backing store through recreation. |
| B6 | medium | defer | The blocked opt-in fixture does not force live ingestion/deletion delays. This is the unqualified selected-producer gate; DW-168 remains open and no physical-erasure claim is made. |
| B7 | low | patch | The optional fixture retains synthetic tenant/case after success or failure. Add bounded teardown with private retained handles if it cannot finish. |
| B8 | medium | patch | DELETE status is discarded, so compensation rejection is invisible. Removing unsafe compensating DELETE also removes this misleading outcome path. |
| B9 | medium | bad_spec | The new required status-read capability is absent from the existing DELETE-only health evidence; explicitly probe/report its authorization behavior. |
| B10 | medium | patch | The new blocked states are documented without an operator evidence/reconciliation path. Document bounded inspection and safe repair limits without abandoning unknown producers. |
| E1 | high | bad_spec | Same demonstrated admitted writer interleaving as B3: a fresh read cannot order a later mapping write against canonical-intent removal. |
| E2 | high | patch | Same demonstrated compensation/redelivery defect as B2; cached terminal identity is not evidence of existence after DELETE. |
| E3 | high | bad_spec | Same supported transient-removal/live-readd path as B1; parent projection deliberately returns retryable for reappearing live targets. |
| E4 | high | bad_spec | The duplicate canonical identity claim is invalidated by the admitted writer interleaving; removing its intent loses workflow-to-canonical provenance. |
| V1 | medium | patch | Pre-verified gap: same-revision tests do not prove later content revisions get distinct attempts and both retained canonical obligations. Add changed revision/restart coverage. |
| V2 | high | patch | The verifier independently traced successful compensation followed by cached-ID success; preserve producers for durable mapping retry. |
| R2-B1 | medium | defer | Selected Memories permanently deduplicates by source URI; baseline indexing used the same source and already returned the old unit for changed content. Producer revision/update support is a separate pre-existing capability, not supplied by a new consumer token. Retain the canonical duplicate identity without claiming refreshed content. |
| R2-B2 | high | bad_spec | New admission generations can accept a canonical ID already present in a validated cleanup receipt because the selected producer falls back to permanent source dedup. Reject publication of known-cleaned canonical identities and retain the new obligation as retryable; a new token alone cannot promise producer reindexing. |
| R2-B3 | high | bad_spec | Unit content includes JsonIgnore contact and identifier collections, whereas canonical redelivery reads their serialized absence. Hash durable projection identity instead of transient content; prove one admitted producer across real adapter redelivery. |
| R2-B4 | medium | patch | The selected inbound rate-limit filter emits 429/RATE_LIMIT_EXCEEDED before scheduling. Classify that exact rejection as retryable after repair, while ambiguous 429 responses remain unresolved. |
| R2-B5 | medium | bad_spec | A legacy workflow-to-canonical resolution is only local until subtraction. Deletion followed by subtraction failure and expired status loses previously discovered canonical provenance. Durably retain the scoped legacy association before DELETE, including capacity and conflict failures. |
| R2-B6 | medium | bad_spec | BeginCleanup returns settled receipts as ordinary active work, causing repeated remote deletion after a previously qualified absence. Preserve an explicit settled distinction and use receipts only to resolve matching late mappings without reopening remote obligations. |
| R2-B7 | maybe-false | defer | Mapping and ingestion inventories use the same configured SDK backend, and health reports mapping reachability only. A real key-prefix permission or serializer failure that permits mapping probes but blocks ingestion writes would establish an additional capability-probe gap; that deployment-specific condition has not been demonstrated. Unverified medium. |
| R2-B8 | medium | defer | carried: same selected-producer delayed-ingestion/deletion qualification gap as B6 in the prior review. Runtime provisioning/authentication is blocked, the fixture does not force overlap, and DW-168 remains open; do not append or claim this deferral again. |
| R2-B9 | medium | patch | The shared serialized test store keys values/ETags by key alone, hiding incorrect configured-store routing. Include storeName in its keys and cover distinct mapping/projection stores through real adapters. |
| R2-B10 | medium | patch | The actual SDK/Dapr wire test observes ETag changes but does not reject stale or competing create writes. Add two-client stale-ETag and absent-create conflicts and inspect persisted winner state. |
| R2-E1 | high | bad_spec | Same demonstrated transient-collection/durable-redelivery identity defect as R2-B3. A canonical acknowledgement must be reconciled instead of reserving a second producer. |
| R2-E2 | high | bad_spec | Same demonstrated deleted-canonical reuse through source dedup as R2-B2. A retained cleanup receipt must prevent a later generation from reporting that unit indexed. |
| R2-V1 | medium | patch | Pre-verified gap: active-intent capacity tests cannot detect omission of settled receipts from the 1,024-entry bound. Fill capacity with receipts, reopen a transient generation, and assert no submission or provenance eviction. |
| R3-B1 | high | bad_spec | Durable entries omit transient contact/identifier content, and notification context omits the canonical sequence. Two same-type events at one timestamp can share a hash. Thread the retained canonical sequence through an additive compatible notification seam and include it in the attempt identity. |
| R3-B2 | low | reject | No-op intent redelivery rewrites SDK inventories, but no incorrect state or failed convergence was demonstrated. Avoiding these writes would add a separate admission-read/write optimization around the required SDK policy; that complexity is not justified by the minor contention observation. |
| R3-B3 | low | reject | Final source audit corrected the initial false verdict: the mapping phase can add the now-settled intent and leave no active obligation while an earlier blocked reason remains. This conservatively requests one extra pass; retained settled receipts let that pass succeed without remote access. Precisely clearing only superseded scoped failures would require additional failure tracking, so the minor redundant retry is tolerated. |
| R3-B4 | medium | patch | The synthetic status probe accepts arbitrary 2xx bodies. Require a matching structurally valid status envelope or the qualified missing-status response; cover HTML/empty/unrelated success responses. |
| R3-B5 | maybe-false | defer | carried: same deployment-specific ingestion-inventory capability-probe claim as R2-B7; no key-prefix permission or serializer counterexample was demonstrated. Preserve its unverified-medium disposition without another deferral. |
| R3-B6 | low | reject | Responses deliberately expose safe retry/recovery reasons, and documented SDK inventory inspection distinguishes capacity, fences and unresolved producers. Additional coded operational identifiers require new logging/error plumbing and are a minor observability enhancement rather than a demonstrated recovery defect. |
| R3-B7 | low | patch | Opt-in state qualification uses unique synthetic keys but provides no per-test cleanup for a persistent owned sidecar. Add bounded finally cleanup of only the generated keys; use fresh ETags and preserve primary test outcomes. |
| R3-B8 | medium | patch | The shared test dictionary's version comparison and write are not atomic across threads. Protect reads and conditional writes together, leaving async interception hooks outside the lock. |
| R3-B9 | medium | patch | Important rejection tests synthesize SDK exceptions and bypass actual ingestion acknowledgement/token/error handling. Add focused handler-backed tests with the real published REST client for acknowledgement, stable token forwarding and a qualified preflight rejection followed by repaired retry. |
| R3-B10 | medium | defer | carried: same selected-producer delay/restart qualification gap as R2-B8 and earlier B6. DW-168 remains open; its blocked runtime proof is not repeated or claimed. |
| R3-E1 | high | bad_spec | Same demonstrated equal-timestamp contact/identifier revision collision as R3-B1; retained canonical sequence must distinguish those applications while remaining stable across restart and redelivery. |
| R3-V1 | medium | patch | Pre-verified regression gap: production AddParties registration can lose its optional canonical-store constructor arguments without breaking type-resolution tests or manually constructed lifecycle tests. Add a behavior test through the resolved production indexer. |
| R3-V2 | medium | patch | Pre-verified regression gap: cleanup of a persisted definite rejection is not exercised before credential repair. Assert cleaned result, empty active inventory and persistent permanent fence without a remote workflow or repaired credentials. |
| R4-B1 | maybe-false | defer | Older acknowledged obligations remain durably discoverable and are reconciled by cleanup. The selected source exposes no automatic workflow-purge/retention setting found in the search; the claimed loss requires an established status-expiry policy before erasure. Unverified medium: qualify status retention and whether bounded opportunistic reconciliation of superseded workflows is required, without assuming source misses prove completion. |
| R4-B2 | medium | patch | Rebuild notification event type/timestamp can differ while canonical sequence and persisted target remain identical. Exclude delivery-only context from sequence-qualified identity while preserving scope, persisted target and legacy-call behavior; prove incremental-to-rebuild acknowledgement recovery. |
| R4-B3 | medium | defer | Baseline 6c83309d08a10f36a0da866521b9d0da6f8fc998 already marks both transient collections JsonIgnore and BuildReconciliationFold already returns serialized canonical state. Full searchable content reconstruction after a rejected/failed first submission is a pre-existing projection-content requirement, distinct from this batch's durable identity/provenance fixes. |
| R4-B4 | medium | patch | The newly available canonical snapshot can already show a live target when a delayed removal callback starts. Skip that stale removal without closing admission or deleting the live unit, retaining erasure-checkpoint precedence and fail-closed malformed-state behavior. |
| R4-B5 | medium | patch | Multiple validated terminal intents can name one canonical unit. Reuse scoped verified absence within the pass (including settled receipts) so equivalent obligations do not issue redundant remote DELETEs or fail after later credential loss. |
| R4-B6 | low | reject | Same bounded redundant-retry behavior as corrected R3-B3. A later successful mapping phase can settle the intent, but the earlier blocked reason conservatively survives for one pass. No provenance is lost and the next pass uses settled local receipts. Avoid adding broad failure-clearing logic that could discard an unresolved scope error. |
| R4-B7 | medium | patch | Cancellation after the intent phase can leave the exact mapping unprocessed. The finally guard then prevents verified absence from moving to a durable settled receipt. Receipts now preserve provenance, so move every exact verified intent to settlement independently of mapping subtraction; preserve mappings and cancellation, then let a later pass subtract them using the receipt. |
| R4-B8 | low | reject | carried: same capacity-reason/health observability enhancement as R3-B6. Bounded operational inventory inspection is documented; adding coded logging/pressure plumbing is more than a direct correction and is not required for retained obligations. |
| R4-B9 | low | reject | carried: same unchanged-intent ETag optimization as R3-B2. No incorrect state was demonstrated, and the SDK policy's bounded retry/failure behavior remains truthful. |
| R4-B10 | low | patch | The selected-runtime fixture owns mapping/ingestion keys as well as remote resources. Include those exact handles in private teardown receipts; conditionally erase only after the fixture proves terminal unit absence and remote teardown, otherwise preserve obligations and report operator cleanup. |

## Verification

- Obtain mapped Debug source builds through pinned module tool 4.34.0 and `module/hexalith.module-manifest.v2.json`; never handwrite selectors. Inspected HEAD: `6c83309d08a10f36a0da866521b9d0da6f8fc998`.
- Build/run individual `Hexalith.Parties.Tests` with xUnit single-dash class filters for mapping/indexing/cleanup, health and projection retries; then run the full affected assembly. Inspect serialized end-state after restart/conflicts; require zero failures.
- Record Release/package qualification separately. Exercise delayed workflow/deletion against the selected Memories implementation: doubles alone cannot prove physical erasure. Blocked runtime proof leaves its ledger closure unclaimed.
- Run `bash scripts/check-no-warning-override.sh` and `git diff --check`; retain locked local commitlint success, fetch/inspect divergence and push `main` without force.

## Final Qualification Status

Consumer implementation and independent review corrections are complete. The spec is `done` for consumer recovery implementation. DW-81, DW-83, DW-167 and DW-170 are closed from focused and persisted-state evidence; DW-168 retains its explicit live qualification deferral.

- [ ] DW-168 selected-producer delayed ingestion/deletion and physical-erasure qualification remains open. Historical 2.27.1 provisioning/authentication failure is retained; independently selected 2.29.8 is not live-qualified.
- [x] Full affected assemblies pass in Debug and Release after integrating upstream qualification fix `0f336bd005dbed9e2e278bc1861dfc0fb2322a15`. The upstream fix preserves historical source observations and reconciles current SDK selection; this batch does not weaken assertions or alter dependencies.

Final mapped Debug/current-package Release/floor compatibility results, exact commands, passing matrix cases and source hashes are in the [verification receipt](tests/five-memories-recovery-deferred-items-2026-10-23/README.md). Current source/package builds have zero warnings and errors. The focused suite passes 334/334 on both current selection and the original 2.27.1 floor; Projections passes 444/444 in both configurations; real SDK/Dapr wire checks pass 2/2. Each full service run has 2,609 passed, zero failures and three opt-in skips. The two state opt-ins pass separately; selected Memories live qualification remains deferred.

The separate `/pushall` committed the then-current shared tree and advanced eight references, changing the selected Memories catalog version to 2.29.8. Those external commits and the subsequent upstream qualification fix are preserved. This batch makes no dependency, gitlink, production migration or deployment change; original 2.27.1 API-floor compatibility remains checked independently.
