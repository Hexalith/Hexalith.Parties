---
title: 'Five processing and Memories erasure deferred items'
type: 'bugfix'
created: '2026-10-25'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
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

## Implementation Notes

Approved by the user on 2026-10-25: approve and continue. The parent owns this already-rendered BMAD workflow, review, commits and pushes. The implementation child implements this spec directly, sequentially; do not invoke another build workflow or spawn additional agents. Read applicable owning-repository guidance before changes. Parent owns final independent verification and Git publication.

Pre-code baseline: isolated Aspire start succeeded for the exact Parties AppHost; `aspire wait parties --timeout 30 --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` exited 17 while security remained Starting and Parties waited. Exact-target stop exited 0. Raw receipts are under `/tmp/parties-five-processing-20261025/`; the unrelated access-telemetry AppHost was preserved. Treat this as blocked healthy-topology evidence, not a passing runtime baseline.

Legacy exports allocate in proportion to history.

The build renderer ran exactly once. Establish an isolated Aspire baseline before code. Develop with source/Debug; verify package/Release parity.

## Spec Change Log

## Review Triage Log

## Verification

- Run affected individual Parties and EventStore SDK test assemblies; use direct xUnit v3 invocation for filters.
- Run Memories Server/Integration tests with actual Redis/FalkorDB fault/restart controls and owned cleanup.
- Run Parties CI tests and `python3 -m unittest discover -s references/Hexalith.Builds/Github/publish-containers/tests -p 'test_*.py'`; preserve exact outputs and immutable identity checks.
- Run `bash scripts/check-no-warning-override.sh`, package/Release builds and whitespace checks.
