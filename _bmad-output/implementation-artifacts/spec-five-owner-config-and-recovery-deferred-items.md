---
title: 'Five owner configuration and recovery deferred items'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '08b9684f8464a2f9f8870be47e2a396e30e0cd99'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Deferred defects affect query vocabulary, batch recovery, JWT policy, isolated UI routing and inventory health.

**Approach:** Complete DW-56 and four unnumbered entries: unguarded batch reconciliation, Memories algorithm replacement, configurable admin/sample UI Dapr targets, and ingestion-inventory health qualification. Implement, adopt, independently review, verify, commit and push main as one requested five-item batch.

## Boundaries & Constraints

**Always:** Fix shared capabilities in EventStore/Memories first. Preserve legacy wire values, defaults when configuration is absent, authentication, ACLs, cancellation, erasure fences and SDK persistence. Separate source/package/runtime proof; record canonical owner receipts.

**Never:** Retain personal-data manifests for normal batches, reclaim newer data, remove unrelated keys, bypass source/build gates, fabricate signoff, dispatch releases or deploy. No migration; separate publication, authoritative inventory and legacy compaction debts remain open.

## I/O & Edge-Case Matrix

| State | Expected behavior |
|---|---|
| SDK query failure | Semantic source names preserve exact historical wire/HTTP behavior. |
| Batch committed; newer write/delete/tombstone; retry | Preserve newer state; foreign envelopes and guarded-owner uncertainty fail safely. |
| Explicit RS256/ES256; absent/invalid configuration | Exact configured list; historical defaults only when absent; invalid lists rejected. |
| Default or suffixed UI targets | Existing defaults or selected own-session routing; invalid targets rejected. |
| Mapping allowed, inventory denied | Health reports degraded in enabled and disabled modes. |
| Probe cancellation/read/erase failure | Bound cleanup independently; retain primary failure; touch reserved sentinels only. |

</frozen-after-approval>

## Code Map

Owner paths are relative to their root reference.

- EventStore `src/Hexalith.EventStore.Contracts/Queries/QueryAdapterFailureReason.cs`: legacy constants; `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs`: compatibility.
- EventStore `src/Hexalith.EventStore.Client/Projections/ReadModelBatchProtocol.cs`: non-retained compaction reclaims newer rows. Reuse Client.Tests `Projections/ReadModelBatchStoreTests.cs` and `DaprReadModelBatchTests.RecoveryCorrections.cs`.
- Memories `src/Hexalith.Memories.Server/Hosting/MemoriesServerServiceCollectionExtensions.cs` binds initialized algorithms; neighboring authentication options/tests cover validation.
- EventStore `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs` and `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs`: hardcoded invocation targets. Reuse SDK invocation registration and existing Admin.UI/Sample tests.
- Parties `src/Hexalith.Parties.AppHost/Program.cs`: session target wiring; `Queries/PartySdkQueryService.cs`, `HealthChecks/MemoriesSearchHealthCheck.cs` and `Search/PartyMemoryIngestionIntentStore.cs` under `src/Hexalith.Parties/`: consumer changes.

## Tasks & Acceptance

- [x] Owner query constants: add semantic aliases with unchanged values; obsolete legacy members compatibly and update compiled consumers. Adopt aliases in Parties query service/tests in a qualified dependency graph.
- [x] Owner batch protocol: preserve superseding ordinary writes/deletes and align terminal verification, retaining foreign-envelope/owner guards. Extend producer fault tests and Parties `tests/Hexalith.Parties.Tests/Projections/PartyDetailSdkProjectionRecoveryTests.cs` for newer erasure, retries and compaction races; inspect persisted bytes.
- [x] Owner authentication registration: replace explicitly configured algorithms while retaining absent-setting defaults and programmatic configuration. Extend registered-options and signed-token tests in `tests/Hexalith.Memories.Server.Tests/Authentication/`; verify actual named JwtBearerOptions and valid asymmetric acceptance/HS256 rejection.
- [x] Owner UI registration: expose validated configurable targets through SDK invocation. Wire own-session IDs in Parties AppHost; extend `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesTopologyIsolationTests.cs` for authenticated UI-client routing, cross-session denial and survivor operation.
- [x] Parties inventory store/interface and health check: add a reserved inventory sentinel using actual serialization and conditional SDK erase. Extend HealthChecks/MemoriesSearchHealthCheckTests.cs and Search/PartyMemoryPersistenceRuntimeTests.cs under `tests/Hexalith.Parties.Tests/` for SDK/health ACL rejection/repair, cancellation, cleanup failure and sentinel absence.
- [x] Update `docs/development-guide.md`, `docs/memories-backed-party-search.md`, `tests/README.md`, deferred ledger and batch receipts with exact dispositions and adoption identities. Prepare reviewed publication in owner-first order and retain unmet package/runtime gates; the parent publication phase finalizes canonical identities after review.

**Acceptance:** Given each matrix precondition, when the real affected path executes, then named tests prove its expected result. Given unavailable prerequisites, when recording completion, then preserve exact blockers and affected open gates.

## Implementation Notes

Parent audited the complete task diffs and every review finding. All 18 patch findings (15 reviewer findings and three parent verification corrections) are fixed; one preexisting MCP algorithm-binding defect is deferred. Final Release service tests pass 2,647 cases with zero failures and eight explicit prerequisite/environment skips. Complete EventStore Client/Admin.UI/Sample suites, eight registered Memories authentication controls, 999 mapped source cases, actual inventory ACL health and strengthened two-session UI runtime pass. Full details and exact limitations are in final-verification.json.

Reviewed owner commits are pushed on main: EventStore 596de60e140da779f170279a822ded010070d774 and Memories 4190ec6549dd83bcaaf268e7bb723dcff8f7286b. Root source adoption is 79768d1f2b7071db6fa49bffbfe179a43a00baa7. Commitlint, source-selection signoff, warning policy and owned runtime cleanup pass. The final authorized root main push follows this record. UI routing and inventory health entries close; aliases, recovery and Memories policy retain matching package gates. Published EventStore 3.127.0 remains selected and demonstrably lacks the first two source fixes.

An external /pushall committed most candidate work in Parties ec3d919c159aa87453910011618cb6d07f873baa and advanced seven source references. Preserve that work; reconcile observed identities without fabricating signoff or recertifying historical parity.

User steering on 2026-10-23 selects EventStore package family 3.127.0. Apply its central root pin, inspect the published API and rerun package verification. It lacks the new semantic aliases and superseding-write recovery fix, so source delivery and matching publication gates remain distinct. Frozen intent is unchanged.

Approved by the user on 2026-10-23 with "approve and continue". Frozen intent is unchanged. The parent owns workflow review and final publication; implementation has no remote operations.

Parent captured the pre-code isolated Aspire baseline, waited for Parties/security and stopped the exact AppHost. Sanitized states are in the batch aspire-baseline.json; admin and Tenants were Finished, so this is not complete-topology qualification. Security initially exceeded a fifteen-second wait, then passed. Delegate implements sequentially from this spec without rerendering the workflow.


## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| B1 | medium | patch | inventory-runtime.py records a nonzero C# exit but returns success if key absence passes; propagate the failure after finalization. |
| B2 | medium | patch | Docker and C# subprocesses have no deadline, so a hang prevents owned-runtime teardown; bound commands and terminate owned child groups. |
| B3 | medium | patch | The receipt write is outside finally, so an assertion or exception bypasses publication; persist failure and cleanup evidence during finalization. |
| B4 | medium | patch | Each failed sentinel erase leaves a permanent unique inventory row, as existing failure tests show. Apply the existing SDK expiring-store capability to reserved probe writes and require cleanup capabilities before writing. |
| B5 | medium | patch | The saved-then-cancelled case proves cancellation cleanup, but no saved-then-transport-exception case proves the documented lost-response guarantee; add persisted-absence coverage. |
| B6 | medium | patch | Current fault controls never mutate sentinel contents/ETags during cleanup. Add altered-value, missing-ETag and concurrent-update tests proving foreign bytes survive and health degrades. |
| B7 | medium | patch | SilentReloadPattern catches every gateway 404 as an empty counter and renders Last updated. Seed distinct persisted counters and assert their own-session values before and after peer disposal. |
| B8 | medium | patch | The new fixed-target validation rejects path separators while the generic routing handler rejects only whitespace/control characters; share the strict rule across both paths. |
| B9 | medium | patch | The notification test is limited to domain options while the bearer configurator uses cached IOptions. Clarify the restart requirement and verify named bearer options remain unchanged until restart; do not claim live policy reload. |
| B10 | medium | patch | Semantic query aliases do not establish the independent batch-recovery capability; use an explicit qualified source-build prerequisite instead of reflecting an unrelated field. |
| B11 | medium | patch | The present-tense batch README/ledger still select 3.126.1 and old totals. Label original validation.json historical and point current summaries to the actual 3.127.0 adoption and later matrix evidence. |
| B12 | medium | patch | Constructing an options object proves the init-only API but not the registered callbacks. Exercise programmatic configuration through the real registered options pipeline with no explicit algorithm setting. |
| E1 | medium | patch | A retained Completed marker can still reference its last operation envelope until FinishRecoveryAnchorAsync runs. The changed Completed retry verifies logical keys first and rejects that own envelope, preventing cleanup. Finish owned retained cleanup before terminal verification and cover direct Execute retry. |
| E2 | medium | patch | ProbeAsync writes before checking IReadModelConditionalEraser, leaving one row per health check for a store lacking the optional capability; fail before any write. Same accumulation root cause as B4. |
| E3 | medium | patch | The actual runner lacks the quoted raise: a nonzero test result is only recorded, then successful absence assertions allow exit zero. Same propagation root cause as B1. |
| V1 | medium | defer | Trusted missing-adoption evidence: MCP BindConfiguration retains HS256 despite explicit RS256/ES256, demonstrated by the reviewer probe. MCP registration/defaults predate and are untouched by this Server change; record this separate ingress defect for follow-up. |
| P1 | medium | patch | Real Docker inspect emitted lowercase no such object:, causing false cleanup failure despite SDK/ACL success and verified container absence. Match the concrete diagnostic case-insensitively and cover unrelated errors as negatives. |
| P2 | medium | patch | The stronger counter test demonstrated sample workload authentication was unconfigured (401 metadata/process, HTTP500 command). Apply the existing fixture signing contract to sample and allow only the own-session gateway workload; retain authentication/ACL enforcement. |
| P3 | medium | patch | The final full service suite reproduced one stale live lifecycle search after the incoming EventStore 8.5 work moved backlog to in-progress. Update that live observation and its required current-state token; retain dated G5 receipts, blockers and owner approvals. |

## Verification

Before code changes, establish the owned Aspire baseline. Build individual projects using pinned-tool Debug mappings; run focused xUnit assemblies with single-dash filters, then full affected suites. Verify Release/package separately. Exercise real Dapr persistence, options/tokens and dual-session UI calls; skips cannot establish runtime closure.

Planning backend-only evidence: `tests/five-owner-config-and-recovery-deferred-items-2026-10-23/investigation/`.

Run the solution Release build gate, `bash scripts/check-no-warning-override.sh`, CRLF-aware diff checks and owning pinned commitlint. Fetch and inspect divergence before the authorized main pushes.
