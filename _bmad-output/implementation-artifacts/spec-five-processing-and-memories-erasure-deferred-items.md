---
title: 'Five processing and Memories erasure deferred items'
type: 'bugfix'
created: '2026-10-25'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 3
baseline_commit: '1ef274ea0471643d83f924147e85ac8326cf6a47'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Article 30 history grows as one repeatedly serialized value. Memories legacy migration and retry queues can retain or recreate erased content, and deletion discovery scans unrelated Redis keys. OCI timestamp closure is stale.

**Approach:** Deliver the requested five-item batch: DW-50 bounded processing history, DW-107 verified OCI timestamp closure, DW-196 fenced legacy NL migration, DW-197 retry-payload erasure, and DW-198 bounded semantic discovery. Review, verify, publish owner commits first, then adopt and push Parties main.

## Boundaries & Constraints

**Always:** Preserve existing array-returning clients and complete exports; introduce additive paged reads. Persist Parties models through EventStore SDK stores, batches and protected scoped cursors. Preserve checkpoints, unresolved-event replacement, freshness, concurrency, recovery and Article 30 retention after erasure. Keep infrastructure in its owner; assert persisted absence and unrelated survivors.

**Never:** Silently truncate history; invent inventory completeness or owner approval; change dependency versions, execute production migration/deletion, deploy or dispatch a release. Do not close package-publication debts using source proof. Keep immutable publisher pins and required build gates intact.

## I/O & Edge-Case Matrix

| Given / when | Then |
|---|---|
| Long history receives new, duplicate or formerly unresolved events | Bounded buckets update without copying all history; preserve ordered, deduplicated authoritative facts. |
| Legacy state migrates or a large rebuild restarts | Resume bounded staged writes; switch canonical authority only after complete validation; preserve newer writes and erasure fences. |
| A cursor is reused across tenant, party, caller, page size or projection revision | Reject it; valid paging and compatibility reads preserve complete authorized results. |
| Migration, queue admission, trimming or delayed retry races deletion | Tombstones prevent resurrection; erase canonical/legacy NL and live/dead queued payloads, including durable retry inputs. |
| Semantic deletion encounters sparse unrelated keys, cancellation or missing legacy index | Use bounded exact-unit discovery; retain uncertainty and require explicit reconciliation when completeness is unprovable. |
| OCI created labels are valid, missing, impossible or inconsistent | Accept only canonical matching image/artifact timestamps across both platform children. |

</frozen-after-approval>

## Code Map

Paths below are Parties-relative; `M` means `references/Hexalith.Memories/`.

- `src/Hexalith.Parties.Projections/Models/PartyProcessingSdkReadModel.cs`, `PartySdkReadModelAddresses.cs`; `Handlers/PartyProcessingActivityFold.cs`, `PartyDetailSdkProjectionHandler.cs`: current list, addresses, fold and coordinated live/rebuild/unresolved writes.
- `src/Hexalith.Parties/Queries/PartySdkQueryService.cs`; `src/Hexalith.Parties.Contracts/Queries/`; `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`: existing paging codec and compatibility reads. `docs/gdpr-processing-activity-records.md` defines retained audit semantics.
- `M/src/Hexalith.Memories.Server/Migration/RedisNaturalLanguageNamespaceMigrator.cs`; `Activities/Cases/DeleteMemoryUnitProjectionActivity.cs`, `DeleteCaseProjectionActivity.cs`: migration/deletion.
- `M/src/Hexalith.Memories.Server/NaturalLanguage/FailedNaturalLanguageEmbeddingRegistry.cs`, `NaturalLanguageEmbeddingRetryHostedService.cs`; `Workflows/NaturalLanguageEmbeddingRetryWorkflow.cs`: live/dead payloads and durable retry input.
- `M/src/Hexalith.Memories.Server/Ingestion/RefreshPublicationStore.cs`: reuse exact-key provenance, tombstone fencing and bounded native legacy discovery.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/DomainProjectionRebuildPlan.cs`, `IAsyncDomainProjectionRebuildHandler.cs`; Client `Projections/`: generic bounded rebuild/promotion belongs here, preserving pure preparation.
- `.github/workflows/release.yml`, `scripts/verify-release-publication.py`, `tests/Hexalith.Parties.Ci.Tests/`: already adopted immutable publisher and executable created-label controls.

## Tasks & Acceptance

**Execution:**

- [x] DW-50: In processing Models/Handlers above, replace growing canonical lists with fixed-capacity buckets and constant-size metadata. Coordinate Detail with affected buckets through SDK concurrency. Stage migration in resumable bounded groups; preserve side-effect-free PrepareRebuildAsync. Extend generic EventStore aggregate-generation staging/promotion where required rather than exceeding 100-operation/1-MiB batches; atomically publish coordinated authority. Add protected cursor reads, preserve complete legacy clients/exports, and document cutover requiring older writers to drain before migration. Execute no deployed cutover.
- [x] DW-196: In Memories migrator and both deletion activities, apply atomic erasure/source fences to copy/delete ordering and remove both NL namespaces. Qualify actual migration interleavings, recreation and unrelated survivors.
- [x] DW-197: In registry/worker/workflow files above, atomically fence retry admission, requeue, dead-letter and trimming; purge exact-unit payloads from modern and legacy live/dead storage. Prevent delayed work from retaining or restoring raw content; include scheduled workflow input in physical-erasure qualification.
- [x] DW-198: Replace both deletion scans with bounded exact-key/native-index discovery using existing publication provenance. Validate scope and coverage; fail explicitly when legacy completeness cannot be established.
- [x] DW-107: Rerun real MSBuild/rebind, OCI reader and immutable publisher controls. Verify published executor `11a3a79cfac4bba9548c6364a17a01ee9b561370` and validator SHA-256 `1913f75bdf39c494c5723bca5c04fcb2e6dc0474ad2d86c22e63aae96b9e850c`; reconcile closure without release dispatch.
- [ ] Extend projection/query/client and Memories migration/retry/deletion integration tests for every matrix row; update relevant docs, deferred ledger and compact receipts. Independently review, run affected suites, validate exact messages with pinned commitlint, publish owner commits before separate Parties adoption, then push main.

**Acceptance Criteria:**

- Given large or interrupted histories, when SDK stores reload, then values/work groups remain bounded and compatibility outputs agree with replay.
- Given erasure races, when real adapters restart old work, then scoped payloads remain absent and unrelated data survives.


## Review-driven re-derivation requirements

Preserve the frozen intent. These findings expose missing implementation requirements; no product intent question remains. Complete all accepted corrections coherently:

- Make bounded unresolved audit writes idempotent and recoverable through the actual SDK protocol for the same dispatch ID, pending envelopes, repeated unresolved delivery and eventual resolution. Preserve Detail/Processing checkpoint coordination, validate duplicate deliveries before acknowledgement, and derive bounded deterministic chunk identities.
- Give paged history a bounded integrity contract that rejects malformed header boundaries and missing/truncated retained coverage without scanning all history on each page. Preserve complete legacy/export behavior and final revision revalidation; execute mid-read revision mutations for both APIs.
- Never infer complete legacy semantic-key coverage from an optional TAG query. Missing-TAG legacy keys must remain obligations or force explicit qualified owner reconciliation; unknown historical coverage must default incomplete. Keep bounded discovery, modern exact provenance, cancellation, scope, index schema/readiness and FAIL-timeout checks.
- Preserve live JSON-only legacy retry processing by an atomic, fenced compatibility path. Restore canonical-unit existence admission before expensive work and before publication, without propagating raw content through workflow history. Enforce dead caps after valid moves. Execute a successful admitted retry with real persisted NL output and unchanged unrelated data.
- Validate complete canonical promotion/ownership before staging any generation. Preserve structured retryable/indeterminate outcomes for staging storage failures in every dispatcher path. Execute malformed-plan rejection with zero staging writes and storage-fault outcome tests.
- Repair incomplete canonical NL targets safely or report explicit owner reconciliation; never silently complete a migration that stranded valid legacy content.
- Qualify actual content-bearing legacy activity history and late completion after termination/purge/restart, beyond the existing synthetic timer control. Keep cross-tenant scope validation, collision-safe modern IDs, foreign survival and default quiescence refusal.
- Wire durable-history controls into a maintained automated integration lane with required trait and owned Redis/Dapr resources; that lane must execute the assertions without optional skips. Keep bootstrap and cleanup scoped to owned resources and use existing technical test support where applicable.

KEEP the complete reviewed implementation snapshot at `/tmp/parties-five-processing-20261025/rederive-1/`. Its manifest records every owned file and exact baseline. Restore its positive implementation as the foundation, then re-derive the deficient paths above rather than replacing working behavior. Preserve fixed-capacity buckets, the seven executed real-protocol migration/rebuild/erasure restart and competing-writer tests, bounded eraser staging identity, source/package compatibility and empty-v1 canonical bytes; preserve all 49 real backend controls, new scoped workflow IDs and explicit legacy quiescence opt-in, modern publication fences/provenance, strict native checks, immutable publisher pins and actual dual-RID OCI proof. Keep existing public signatures compatible, package/catalog versions unchanged, all original positive tests, and truthful prior receipts. Parent owns review and Git publication; no nested submodules, new build workflow, production migration, deployment or release dispatch.

## Implementation Notes

Phase-3 audit: parent read all three repository diffs, including untracked code/tests. The matrix follow-up executes migration/rebuild/erasure restart and competing authority cases through the serialized SDK batch protocol and public dispatcher. Parent independently reran all seven cases: 7 passed, zero skips. Source/package and operator-quiescence boundaries remain explicit. Review/publication items in the final task are pending the remaining workflow steps.

Approved by the user on 2026-10-25: approve and continue. The parent owns this already-rendered BMAD workflow, review, commits and pushes. The implementation child implements this spec directly, sequentially; do not invoke another build workflow or spawn additional agents. Read applicable owning-repository guidance before changes. Parent owns final independent verification and Git publication.

Pre-code baseline: isolated Aspire start succeeded for the exact Parties AppHost; `aspire wait parties --timeout 30 --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` exited 17 while security remained Starting and Parties waited. Exact-target stop exited 0. Raw receipts are under `/tmp/parties-five-processing-20261025/`; the unrelated access-telemetry AppHost was preserved. Treat this as blocked healthy-topology evidence, not a passing runtime baseline.

Legacy exports allocate in proportion to history.

The build renderer ran exactly once. Establish an isolated Aspire baseline before code. Develop with source/Debug; verify package/Release parity.

## Spec Change Log

2026-10-25 review loop 1: B1/B2/B4/B6/E6/V2 require coherent retry, integrity, historical-coverage and automated-verification changes. Added the requirements and KEEP snapshot above to prevent non-idempotent audits, silent partial history, unproved legacy absence and unexecuted durable-history coverage. All 23 findings retain individual triage rows; accepted direct corrections are included in re-derivation. Frozen intent is unchanged.

2026-10-25 review loop 2: R2-B1/B2/B8 expose snapshot identity, configured staging preflight and stored-bucket validation omissions. Added the completion requirements and exact KEEP snapshot. Direct guard and test corrections are included in the coherent implementation. Current user commits are preserved; no task code remains uncommitted to revert. Frozen intent remains unchanged.

2026-10-25 review loop 3: R3-B1/B3/E3 require coordinated prepared-state erasure, proven-unscheduled cleanup and command-failure-safe retry moves. Added explicit observable requirements and KEEP preservation of all loop-2 corrections. Other accepted guards/test corrections join this re-derivation; frozen intent remains unchanged.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| B1 | high | bad_spec | Repeated unresolved audit delivery reuses a committed batch ID with fresh ETags; the changed manifest conflicts and fails instead of remaining retryable. Qualify identical dispatch retries through the real SDK protocol. |
| B2 | high | bad_spec | Large audit-only delivery bypasses canonical pending-batch recovery. Pending envelopes can lose reproducible conditional ETags before the next audit attempt. |
| B3 | medium | patch | Valid transactional overflow moves leave removableMembers empty, so dead-queue trimming is skipped. Repeated valid enqueue can exceed the configured dead cap. |
| B4 | medium | bad_spec | Dequeue accepts JSON-only legacy members, while ReadCurrentAsync reads only modern hash payloads. Valid legacy retries therefore lose their content and can exhaust attempts without indexing. |
| B5 | high | patch | The new activity removed the existing syntactic-unit existence checks; registry presence and tombstone absence do not prove a canonical unit exists, and the indexing activity itself does not require that hash. |
| B6 | high | bad_spec | Paged reads can report successful completion for positive-count metadata with absent bounds or truncated/empty buckets. Complete reads check counts, but paging lacks an equivalent bounded integrity proof. |
| B7 | medium | patch | A valid legacy hash with an incomplete existing canonical target returns zero without repair or explicit failure; MigrateAsync discards that result and can report migration completion. |
| B8 | medium | patch | Both dispatcher paths stage durable generation values before constructing/validating the combined canonical batch and binding guarded ownership. Invalid combined plans can write before rejection. |
| B9 | medium | patch | Generation storage failures throw InvalidOperationException from preparation outside the rebuild execution handler, escaping the per-route outcome protocol. |
| B10 | medium | patch | The retained-history test proves synthetic input/timer purge, not content-bearing legacy activities and late completions. Add an actual paused legacy activity and persisted-state restart control. |
| E1 | medium | patch | Unresolved auditing can leave Processing ahead of Detail. The bucket early acknowledgement checks Processing alone, so a redelivered valid prefix can leave Detail stale. |
| E2 | medium | patch | The same early return acknowledges below-checkpoint conflicting duplicates before the established duplicate validation runs. |
| E3 | medium | patch | Appending a part suffix can exceed the 512-byte SDK batch identity limit for an otherwise valid dispatch ID. Derive bounded deterministic part IDs. |
| E4 | high | bad_spec | Positive retained count with missing/reversed bucket bounds produces a successful empty final page; this is the metadata form of B6. |
| E5 | medium | bad_spec | Legacy JSON-only queue records cannot be resolved by the modern-only content lookup; same root cause as B4. |
| E6 | high | bad_spec | A scoped legacy chunk without the optional memoryUnitId TAG is omitted by native search although its key is valid. Index readiness/failure counters do not prove field coverage; sealing completeness can certify deletion while content survives. |
| E7 | high | patch | Canonical-unit absence with surviving queued content can create orphan NL output; same missing authoritative guards as B5. |
| E8 | medium | patch | An incomplete existing canonical target is silently skipped by the new migration script; same root cause as B7. |
| E9 | medium | patch | Valid-only overflow bypasses dead-cap enforcement and can eventually reach the 100,000-member erasure refusal; same root cause as B3. |
| V1 | medium | patch | Pre-verified gap: no executed admitted-record test traverses the new activity through actual NL persistence; substituting its Indexed result would miss a removed index call. |
| V2 | medium | bad_spec | Pre-verified gap: durable-history controls lack the integration trait and depend on an endpoint supplied by neither normal CI nor nightly. A maintained automated owned runtime lane must require actual execution without skips. |
| V3 | medium | patch | Pre-verified gap: tests change revision before the next request, not during bucket loading. Add paging and complete-export failures when the final metadata ETag changes mid-read. |
| V4 | medium | patch | Verified other finding: valid overflow never reaches the existing dead trim call; same root cause as B3 and E9. |

| R2-B1 | high | bad_spec | Verified: migration generation keys depend on the observed Processing ETag, but staging scope uses the unchanged dispatch ID. A 30-record inline snapshot, competing inline append to 31, and retry of staged 31–33 changes the immutable staging fingerprint. |
| R2-B2 | medium | bad_spec | Verified: ReadModelGenerationStaging validates against new default options while the SDK accepts lower configured limits. A later group with a longer key or larger manifest can fail only after earlier groups persisted. |
| R2-B3 | medium | patch | Verified: the raw public concurrency record permits null ExpectedETag; StageAsync dereferences Length before ReadModelBatchValidation, and the dispatcher maps the resulting exception to Indeterminate. |
| R2-B4 | medium | patch | Verified: ReadModelBatchFingerprint writes canonical values as base64. The staging size estimate counts raw bytes, so a 400-KiB value can exceed the documented conservative 512-KiB manifest budget. |
| R2-B5 | maybe-false | defer | Unverified medium: old nonempty-input executions return before replaying the old activity sequence, but no actual old-binary replay demonstrates nondeterminism. Qualify a suspended original production workflow under upgraded workers and inspect its terminal/retry outcome; current code schedules no further raw activity. |
| R2-B6 | medium | patch | Verified: the activity checks admission after embedding, so erasure during description generation still permits a new embedding call carrying the description. Recheck the exact registry record before that external call. |
| R2-B7 | high | defer | Verified pre-existing: DeleteTenantDataKeysActivity at approved Memories baseline already lacks retry queue/payload/workflow purge. This change does not create the tenant cleanup omission; tenant physical erasure needs a separately tracked owner fix. |
| R2-B8 | high | bad_spec | Verified: live bucket writes read only the value-presence guard, then fold and publish without the query reader’s scope/order/coverage/capacity checks. Corrupted touched buckets can survive a new successful checkpoint. |
| R2-B9 | medium | patch | Verified: the new client accepts oversized arrays, null HasMore, terminal cursors and empty continuing pages. These malformed success envelopes can hide history or create unusable continuation; enforce the returned envelope invariants. |
| R2-B10 | low | reject | Unselected generations have no reclamation path, increasing storage after failed migration or explicit rebuild. Routine live delivery updates selected buckets in place; safe reclamation requires new coordinated lifecycle behavior. Reject low-frequency operator storage overhead because its fix adds substantial state and concurrency complexity. |
| R2-E1 | high | patch | Verified: PrepareRebuildAsync compares only Detail checkpoint, while existing lagging-Detail tests demonstrate Processing can be newer. A shorter rebuild can overwrite that history; compare both authoritative checkpoints. |
| R2-E2 | medium | patch | Verified: same null ExpectedETag dereference as R2-B3. |
| R2-E3 | false | reject | The spec, operator documentation and original approved constraints explicitly preserve selected package versions and leave matching-package adoption open. Source generation support is qualified separately; no claim that the published package performs bucket conversion is accepted. |
| R2-V1 | medium | patch | Pre-verified regression gap: worker scheduling accepts Arg.Any input, while persisted-history and successful-output controls bypass construction by the worker. A sentinel raw payload restored in worker input escapes checked assertions. |
| R2-V2 | medium | patch | Pre-verified broken-verification gap: the paged client’s only success response is empty and never checks Items, so returning an empty array unconditionally still passes. Add ordered nonempty records. |

| R3-B1 | high | bad_spec | Verified: retained canonical batches include full Detail bytes. SDK PrepareMarkerAsync persists OriginalManifest before any participant; a crash there leaves no canonical envelope through which the eraser can discover and retire that scoped private state. Require crash-before-first-install physical erasure proof through SDK-owned recovery authority. |
| R3-B2 | medium | patch | Verified: list paging uses CompactCursorScope/TryDecodeCursor, while processing paging uses raw scopes directly. Long valid caller identity can produce a cursor beyond QueryPolicyLimits.MaxCursorLength that the gateway rejects on continuation. |
| R3-B3 | high | bad_spec | Verified: a new admission that fails before scheduling has no output/provenance. FailureReconciler invokes general deletion, which now unconditionally requires legacy owner reconciliation. Producer tests pre-seed attestations and hide the loss of proven-unscheduled settlement. |
| R3-B4 | medium | defer | Verified pre-existing: baseline cdd73d7 DequeueBatchAsync also accepts modern payloads whose MemoryUnitId differs from their hash field/member. Current writers always address by record.MemoryUnitId, so this needs separate corruption/legacy reconciliation qualification; do not infer complete absence of misaddressed historical payloads. |
| R3-B5 | medium | defer | Verified pre-existing: baseline namespace migration copies orphan legacy hashes without requiring syntactic state. New tombstone/source guards prevent known erased-owner resurrection; qualification/repair of pre-tombstone orphan histories remains separate owner reconciliation. |
| R3-B6 | medium | patch | Verified: the new full-inventory check returns zero, indistinguishable from benign no-op, and MigrateOneAsync ignores it. Give this newly introduced capacity exhaustion an explicit blocking reconciliation result. |
| R3-B7 | low | reject | The implementation materializes preflight batches and keys proportional to the supplied generation; the frozen contract bounds stored values and work groups, not all replay/preparation memory. Large replay already materializes its complete plan. Reject a new spool/admission lifecycle for this developer-only optimization. |
| R3-B8 | medium | patch | Verified: a valid 512-byte ProjectionType becomes invalid after the private .generation suffix. Derive a bounded discriminator for only oversized internal components while preserving ordinary identities. |
| R3-B9 | medium | patch | Verified: late activity proof waits a fixed delay and invokes a second purge before observing absence after restart. That can hide recreated history; observe actual completion and check persisted absence before any additional cleanup. |
| R3-B10 | low | patch | Verified: the saved rerun script hardcodes this checkout and a temporary source-mapping file. Resolve repository paths relative to the tracked receipt and document/use the authoritative workspace bootstrap instead of private import paths. |
| R3-E1 | medium | patch | Verified: JSON-only legacy promotion fails HashNotExists whenever a modern entry exists for the same unit; the legacy sorted-set member is left oldest, so batch-size-one dequeues repeatedly return no work. Retire only a proven redundant legacy alias atomically without replacing modern content. |
| R3-E2 | high | patch | Verified: legacy NL key for victim equals the raw semantic key of valid unit nl:victim. The new unconditional legacy KeyDeleteAsync ignores stored memoryUnitId and can delete that unrelated unit; atomically validate stored owner scope before deletion. |
| R3-E3 | high | bad_spec | Verified: new transactional overflow queues destination writes and source deletes, discards command tasks, and trusts EXEC success. Redis WRONGTYPE failures do not roll back other EXEC commands; malformed destination types can lose the admitted live payload. Preflight type/state atomically before mutation and observe individual outcomes. |
| R3-E4 | medium | defer | Verified in concurrent untracked TestOnlyIdentityHistoryCustody.cs: lowercase named arguments do not match UnitStatus positional parameter casing. This file belongs to the user’s separate owner-delivery work and is excluded from task publication; preserve it and track the external compile blocker. |
| R3-E5 | high | patch | Verified claim: same physical namespace collision and unrelated deletion as R3-E2; accepted survivor intent requires a stored-scope guard. |
| R3-V1 | medium | patch | Pre-verified regression gap: deletion activity caller tests do not seed retry payloads and actual durable history together; removing either helper call escapes those controls. Exercise unit and case deletion entry points with persisted victim queues/history and unrelated survivors. |
| R3-V2 | medium | patch | Pre-verified regression gap: paging tests cover only early pages; changing bucket <= last to bucket < last leaves those assertions green. Follow protected continuation to exhaustion and assert exact complete sequence and terminal metadata. |

## Verification

- Run affected individual Parties and EventStore SDK test assemblies; use direct xUnit v3 invocation for filters.
- Run Memories Server/Integration tests with actual Redis/FalkorDB fault/restart controls and owned cleanup.
- Run Parties CI tests and `python3 -m unittest discover -s references/Hexalith.Builds/Github/publish-containers/tests -p 'test_*.py'`; preserve exact outputs and immutable identity checks.
- Run `bash scripts/check-no-warning-override.sh`, package/Release builds and whitespace checks.

## Review-loop-2 completion requirements

Preserve the approved source/package split and all previously passing controls. Bind migration staging and final publication identities to the same immutable observed snapshot so overlapping inline deliveries at the 32-record cutover can retry without fingerprint reuse. Reject rebuilding below either canonical checkpoint. Validate touched stored bucket scope, capacity, order, uniqueness and retained sequence coverage before folding or publishing; malformed state must not advance authority. Reuse validation where practical.

Generation staging must preflight every group against the effective owning-store limits before any write, reject null raw concurrency ETags as malformed input, and measure actual canonical manifest bytes including base64 encoding and scope overhead within the conservative 512-KiB and 32-operation budgets. Keep existing public calls compatible. Exercise ordinary and streamed dispatcher outcome behavior, lower configured limits, late invalid groups and byte boundaries.

After description returns, recheck exact retry admission before sending a new embedding request. Add a worker-driven real Dapr/Redis control with a sentinel raw registry payload: scheduled durable input/history must be content-free while admitted work successfully persists NL output. Strengthen the paged HTTP client with ordered nonempty content and malformed-envelope controls (oversized, unknown HasMore, inconsistent cursor, empty continuation).

KEEP the exact current task-owned snapshot at `/tmp/parties-five-processing-20261025/rederive-2/` and all prior positive implementation/tests/receipts. Concurrent user `/pushall` commits now own the recorded code on main; never revert those commits or unrelated current work. There are no uncommitted task code deltas to revert at this handoff, so re-derive the deficient paths on that preserved positive foundation. Parent owns final current-graph documentation, independent review and Git publication. Do not invoke a skill, spawn agents, stage, commit, push, initialize submodules or change package versions.

### Loop-2 checkpoint adjudication

Parent independently verified the pre-existing `DetailFold_PartyCreatedAfterErasurePreservesTombstone`, `IndexFold_NoOpAfterErasureRetainsTombstoneAndAdvancesSafeCheckpoint` and `WholePayloadRedaction_AfterErasurePreservesPersistedTombstonesAsync` controls: a fresh admitted event after erasure advances safe checkpoint/freshness while preserving erasure and retained facts. Do not globally freeze erased writers or add a durable dispatch-admission ledger. The new cutover retry legitimately replans against current authority. Keep exact winning raw bytes after the rejected stale CAS, and after all rebuild retries. For the live retry after a competing erasure only, replace the overstrict final raw-byte comparison with full model equality to the winning models after changing only the established safe checkpoint/version/projected-at fields to the independently expected delivery values. Assert unchanged erasure/lifecycle, selected generation, complete retained records/bucket bytes and no PII resurrection. This corrects the new test's conflict with established behavior; it does not change the frozen matrix, weaken stale-promotion exclusion or relax erasure guarantees.


Phase-3 loop-2 audit: parent read all changed runtime and regression-test deltas, verified 29 log hashes and all 17 changed-code hashes, and audited every frozen matrix row against executed passing controls. The final lanes are recorded in `tests/five-processing-and-memories-erasure-2026-10-25/review2-verification.json`. Source/package and broader-workspace blockers remain explicit. Independent review/publication remain pending.

## Review-loop-3 completion requirements

Preserve every successful loop-2 correction and positive control in the exact KEEP snapshot `/tmp/parties-five-processing-20261025/rederive-3/`.

- Establish SDK-owned discoverability/retirement of PII-bearing prepared recovery state before its first durable private write, and integrate that authority with Parties erasure. A real serialized-SDK crash after manifest preparation but before the first participant, followed by a fresh eraser, must not report successful physical erasure while any original private manifest/marker/participant bytes remain. Prevent retired late installers from selecting older authority. Keep persistence in EventStore; do not add Party-owned Redis/Dapr batch internals, assume unknown inventory complete, or weaken recoverability/atomic Detail+Processing publication. Use a coherent generic SDK mechanism; source/package capability boundaries and unchanged package versions remain.
- Preserve provably never-materialized producer cleanup without fabricating legacy inventory provenance. Derive authority only from persisted, independently verified unscheduled admission/terminal producer evidence; ambiguous or historical candidates still require reconciliation. Remove fixture owner attestations from controls claiming to prove pre-scheduling settlement, and execute those real adapter paths.
- Make retry queue moves fail before any source deletion if destination/source types or required state are malformed. Await individual queued command outcomes; Redis EXEC success alone is insufficient. Apply the same demonstrated safety contract to admission/requeue/dead-letter/overflow and retain fences/caps. Prove a WRONGTYPE destination leaves original payloads intact and cannot report success.
- Fix the demonstrated mixed legacy/modern retry alias starvation without replacing a valid modern payload. Use atomic exact-scope/identity checks and prove batch-size-one progress plus erasure races.
- Guard legacy namespace deletion atomically by stored ownership: valid colon-containing IDs make legacy NL names overlap other units’ raw-semantic names. Preserve known foreign hashes; unknown owner scope requires explicit reconciliation. Execute unrelated `nl:victim` raw-semantic survival through unit and case deletion.
- Make migration inventory-capacity refusal explicit and blocking. Keep valid legacy content and all prior incomplete-target/fence behavior intact.
- Reuse compact cursor scope handling for processing pagination, qualify long caller identity, and traverse every page through exact final records/terminal metadata.
- Bound the private generation ProjectionType discriminator at documented UTF-8 component limits, preserving ordinary identities and resumability.
- Prove actual late content-bearing activity completion and persisted absence before another purge; remove the second-purge masking in that control. Add native unit/case deletion entry-point tests with real live/dead modern/legacy queues and real durable workflow history, asserting physical absence and unrelated survival.
- Make the tracked rerun artifact resolve its repository root and describe the authoritative source workspace bootstrap, without committing machine-local source mappings or introducing another build workflow. Parent may complete documentation-only bootstrap instructions.

Parent captured the exact current task-owned code before reverting the 17 uncommitted loop-2 implementation paths. Restore that KEEP foundation, then re-derive these deficiencies. Concurrent user changes, including owner-delivery integration helpers, scripts and registrations, must remain untouched and uncommitted by this build. No skill invocation, delegation, staging, commit/push, package changes, nested initialization, production migration/deletion, deployment or release dispatch. Parent owns review, receipts and Git publication.

### Loop-3 historical recovery authority

The frozen prohibition on inventing inventory completeness applies to SDK private histories too. Documentation alone must not let the source-capable eraser certify unknown older unregistered preparations. Add the smallest explicit qualification gate using existing `PartySdkReadModelOptions`: a clearly named historical-private-recovery reconciliation option defaults false, and source erasure refuses before mutations while it is false. This is an operator attestation for the selected store's historical private recovery coverage after draining older writers; production code must never set it automatically from an empty new directory. Keep the unchanged package path and existing public constructor signatures compatible. Tests may explicitly enable it only for their known synthetic inventories, with that fixture authority documented; add an unqualified-default refusal control that proves all state remains unchanged, and retain qualified crash-before-participant erasure and safe-update controls. No production option is enabled by this build. This bounded gate avoids adding an operator tool or a second historical inventory protocol to this batch.

All current Parties source paths that can create PII-bearing recovery material must enroll, including ordinary and streamed dispatcher rebuild promotion. Guarded rebuilds can retain OriginalManifest even without RetainManifestForRecovery; the historical attestation cannot excuse newly created unregistered state after cutover. Propagate an additive opt-in from the Parties rebuild plan through the generic dispatcher while preserving guarded OwnerSelection/OwnerSelections semantics and package compatibility. Keep the fixed empty-v1 initializer unmodified because its bytes contain no private model. Qualify a prepared rebuild crash through the public dispatcher and a fresh qualified eraser, proving private preparation reclamation and refusal of stale replay. Do not silently apply new policy to unrelated generic owners.
