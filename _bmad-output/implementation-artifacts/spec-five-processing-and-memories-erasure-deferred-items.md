---
title: 'Five processing and Memories erasure deferred items'
type: 'bugfix'
created: '2026-10-25'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 1
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

- [ ] DW-50: In processing Models/Handlers above, replace growing canonical lists with fixed-capacity buckets and constant-size metadata. Coordinate Detail with affected buckets through SDK concurrency. Stage migration in resumable bounded groups; preserve side-effect-free PrepareRebuildAsync. Extend generic EventStore aggregate-generation staging/promotion where required rather than exceeding 100-operation/1-MiB batches; atomically publish coordinated authority. Add protected cursor reads, preserve complete legacy clients/exports, and document cutover requiring older writers to drain before migration. Execute no deployed cutover.
- [ ] DW-196: In Memories migrator and both deletion activities, apply atomic erasure/source fences to copy/delete ordering and remove both NL namespaces. Qualify actual migration interleavings, recreation and unrelated survivors.
- [ ] DW-197: In registry/worker/workflow files above, atomically fence retry admission, requeue, dead-letter and trimming; purge exact-unit payloads from modern and legacy live/dead storage. Prevent delayed work from retaining or restoring raw content; include scheduled workflow input in physical-erasure qualification.
- [ ] DW-198: Replace both deletion scans with bounded exact-key/native-index discovery using existing publication provenance. Validate scope and coverage; fail explicitly when legacy completeness cannot be established.
- [ ] DW-107: Rerun real MSBuild/rebind, OCI reader and immutable publisher controls. Verify published executor `11a3a79cfac4bba9548c6364a17a01ee9b561370` and validator SHA-256 `1913f75bdf39c494c5723bca5c04fcb2e6dc0474ad2d86c22e63aae96b9e850c`; reconcile closure without release dispatch.
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

## Verification

- Run affected individual Parties and EventStore SDK test assemblies; use direct xUnit v3 invocation for filters.
- Run Memories Server/Integration tests with actual Redis/FalkorDB fault/restart controls and owned cleanup.
- Run Parties CI tests and `python3 -m unittest discover -s references/Hexalith.Builds/Github/publish-containers/tests -p 'test_*.py'`; preserve exact outputs and immutable identity checks.
- Run `bash scripts/check-no-warning-override.sh`, package/Release builds and whitespace checks.
