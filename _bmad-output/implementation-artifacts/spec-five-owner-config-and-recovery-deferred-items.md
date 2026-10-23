---
title: 'Five owner configuration and recovery deferred items'
type: 'bugfix'
created: '2026-10-23'
status: 'in-progress'
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

- [ ] Owner query constants: add semantic aliases with unchanged values; obsolete legacy members compatibly and update compiled consumers. Adopt aliases in Parties query service/tests in a qualified dependency graph.
- [ ] Owner batch protocol: preserve superseding ordinary writes/deletes and align terminal verification, retaining foreign-envelope/owner guards. Extend producer fault tests and Parties `tests/Hexalith.Parties.Tests/Projections/PartyDetailSdkProjectionRecoveryTests.cs` for newer erasure, retries and compaction races; inspect persisted bytes.
- [ ] Owner authentication registration: replace explicitly configured algorithms while retaining absent-setting defaults and programmatic configuration. Extend registered-options and signed-token tests in `tests/Hexalith.Memories.Server.Tests/Authentication/`; verify actual named JwtBearerOptions and valid asymmetric acceptance/HS256 rejection.
- [ ] Owner UI registration: expose validated configurable targets through SDK invocation. Wire own-session IDs in Parties AppHost; extend `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesTopologyIsolationTests.cs` for authenticated UI-client routing, cross-session denial and survivor operation.
- [ ] Parties inventory store/interface and health check: add a reserved inventory sentinel using actual serialization and conditional SDK erase. Extend HealthChecks/MemoriesSearchHealthCheckTests.cs and Search/PartyMemoryPersistenceRuntimeTests.cs under `tests/Hexalith.Parties.Tests/` for SDK/health ACL rejection/repair, cancellation, cleanup failure and sentinel absence.
- [ ] Update `docs/development-guide.md`, `docs/memories-backed-party-search.md`, `tests/README.md`, deferred ledger and batch receipts with exact dispositions and adoption identities. Publish verified owner commits before parent gitlinks; retain unmet package/runtime gates.

**Acceptance:** Given each matrix precondition, when the real affected path executes, then named tests prove its expected result. Given unavailable prerequisites, when recording completion, then preserve exact blockers and affected open gates.

## Implementation Notes

User steering on 2026-10-23 selects EventStore package family 3.127.0. Apply its central root pin, inspect the published API and rerun package verification. It lacks the new semantic aliases and superseding-write recovery fix, so source delivery and matching publication gates remain distinct. Frozen intent is unchanged.

Approved by the user on 2026-10-23 with "approve and continue". Frozen intent is unchanged. The parent owns workflow review and final publication; implementation has no remote operations.

Parent captured the pre-code isolated Aspire baseline, waited for Parties/security and stopped the exact AppHost. Sanitized states are in the batch aspire-baseline.json; admin and Tenants were Finished, so this is not complete-topology qualification. Security initially exceeded a fifteen-second wait, then passed. Delegate implements sequentially from this spec without rerendering the workflow.


## Spec Change Log

## Review Triage Log

## Verification

Before code changes, establish the owned Aspire baseline. Build individual projects using pinned-tool Debug mappings; run focused xUnit assemblies with single-dash filters, then full affected suites. Verify Release/package separately. Exercise real Dapr persistence, options/tokens and dual-session UI calls; skips cannot establish runtime closure.

Planning backend-only evidence: `tests/five-owner-config-and-recovery-deferred-items-2026-10-23/investigation/`.

Run the solution Release build gate, `bash scripts/check-no-warning-override.sh`, CRLF-aware diff checks and owning pinned commitlint. Fetch and inspect divergence before the authorized main pushes.
