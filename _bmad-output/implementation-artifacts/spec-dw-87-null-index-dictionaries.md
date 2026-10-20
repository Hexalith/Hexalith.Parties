---
title: 'DW-87: Reject null Party index dictionaries with bounded recovery'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'fa708d1cdb71a8926c9ed82e2e18a3ce58fd52ef'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-87 records that explicit null dictionaries in a persisted Party index can cause dictionary exceptions. Some paths silently replace them with empty dictionaries, potentially discarding entries, sequence evidence and permanent erasure checkpoints.

**Approach:** Reject malformed nonnull index rows before query caching, projection folding or erasure writes, using bounded existing failure surfaces and a fixed rebuild-required reason. Reject malformed wrapped and legacy staged candidates. Preserve omitted-property defaults, missing-index behavior, cancellation and valid empty rows. Permit a valid full SDK rebuild to replace a corrupt canonical row using its existing concurrency token, document recovery and the limits of missing remote-search inventory, verify the affected paths, close DW-87, and commit and push on main.

</frozen-after-approval>

## Implementation Notes

- Freeform deferred work; no sprint key. Continue the bounded queue after DW-86; earlier owner-dependent entries retain their separate prerequisites. Baseline main is clean and matches origin/main.
- No intent gaps, irreversible data operation, new API, dependency or schema change. Small local guards at existing index boundaries, focused regression tests and operator documentation. Do not migrate or delete stored data automatically.
- Code map: PartyIndexSdkProjectionHandler.FoldCore and FromCandidate; PartySdkQueryService.ReadIndexAsync; PartySdkReadModelEraser.RemoveParty. The existing ReadSearchIndexAsync already checks all four dictionaries. Reuse SDK optimistic writes, shared staged rebuild and in-memory serialized batch-store test support.
- Null dictionaries are corruption; dictionaries omitted from older JSON retain initializer defaults. Validate all four: Entries, LastIndexedEvents, LastSequenceNumbers and ErasureSequenceNumbers. Never reconstruct missing checkpoints from a partial live delivery or certify erasure after silently dropping them.
- Projection failure is Retryable/projection-rebuild-required, including optimistic retry snapshots. Query failure uses ActorException with no payload or degraded metadata and evicts unsafe cached state. Erasure and staged candidate failures use fixed InvalidOperationException messages before writes.
- Recovery uses an independently valid candidate folded from complete retained readable history, then the existing SDK ETag-protected commit. A missing prior Entries dictionary cannot prove the optional external search inventory; its authoritative inventory and cleanup remain separately tracked owner work.
- Required Aspire baseline: isolated start with existing assets succeeded; parties became healthy. Resource inspection showed healthy parties and eventstore, finished tenants/admin, and optional resources not started. Stop this exact AppHost before builds. This is startup evidence only.
- Validation will use the pinned module source launcher first and the documented focused fallback if environment-blocked; direct xUnit v3 runs for affected projects, repository warning/submodule and whitespace guards, independent blind review and exact pinned commitlint validation. Retain bounded commands/results under tests/dw-87-null-index-dictionaries-2026-10-20/validation.json.

- Concurrent workspace /pushall sessions committed the initial spec and test additions in 1796416d6f8bfcc1e18e2cffd4e1ed966d36bc44 and ed66683252e08c48b757f776ab06378153b20ca9. Preserve those commits and all externally advanced root gitlinks. Current implementation is a continuation, not a rewrite of that history.
- Source launcher exits 2/HXW004: EventStore checkout a07265f00994a3ad093184cec0f329d11184c3e9 differs from staged gitlink 54261aeccb1d8d8c5c99beb2437e2e8a5ad69eaa. Additional diagnostics report Platform identity/tag and module-tool version drift. Debug selected-package builds provide focused fallback without changing dependency selections.
- Test fixture refinement: production JSON options omit null properties on serialization. Corruption fixtures explicitly preserve null properties so real round-trip store reads and raw/wrapped candidates exercise explicit null, rather than silently testing legacy omitted-field defaults. The guard remains independent of serialization settings.

- Review identified an eventless-fence recovery gap: SDK cleanup runs before PartyErased is persisted. Finalization must merge every surviving canonical fence, preserve its high-water evidence and remove candidate entries/provenance for fenced owners. An explicit-null canonical fence dictionary cannot be safely inferred from event history; bounded finalization refusal requires owner-qualified restoration before recovery. This refines what constitutes a valid recovery candidate without changing APIs or inventing evidence.
- Review test improvements: use the SDK protocol fake to reject an actually stale recovery plan, preserve all three erasure slots across corruption on a conflict reload, invalidate in-flight cache reads after canonical corruption, and exercise search reconciliation after recovery with explicit unknown-inventory limits. Operator guidance now links the authoritative internal entry point and specifies scope, fresh session and verification sequence.

- Final validation: both affected Debug project builds have zero warnings/errors. Full Projections.Tests passes 428/428; focused query/cache/adapter cases pass 785/785; full service assembly passes 2127/2128 with no skips/errors/not-run. Its sole failure remains the unchanged dependency-receipt fitness code, now reporting expected EventStore gitlink 54261aeccb1d8d8c5c99beb2437e2e8a5ad69eaa versus concurrently committed a07265f00994a3ad093184cec0f329d11184c3e9. Source-launcher and broad-gate limitations are retained separately in validation.json; neither gate was weakened.
- Independent follow-up confirms all six findings addressed and no remaining concrete introduced issue (read-only, no tests run by reviewer). No new deferred item. Owner-qualified restoration of lost fence evidence remains an explicit corruption-recovery prerequisite; existing DW-82 still owns authoritative remote inventory.
- Warning/submodule and whitespace guards pass. Repository-pinned commitlint 21.2.2 validates the exact full candidate with zero problems/warnings; successful evidence is retained.

## Review Triage Log

- High, patch: a newly recoverable null-Entries row could retain cleanup-created fences absent from even complete event history. Preserve those fences and highest available checkpoint/timestamp, remove replayed entries/provenance for fenced owners, and refuse finalization if canonical fence evidence itself is explicitly null. Regression covers cleanup without PartyErased, long.MinValue fences and missing evidence.
- Medium, patch: the local serialized batch fixture declares but does not enforce ETags. Add a service-test regression using the SDK protocol fake: a real competing canonical write makes the stale recovery batch conflict and preserves the competing persisted contents and ETag. No test dependency was added.
- Medium, patch: initial-read erasure coverage did not exercise corruption after a conflict, and processing was absent. Seed all three slots, inject a first-batch conflict plus corruption, assert there is no second batch and compare every persisted slot.
- Medium, patch: sequential cache eviction did not prove in-flight invalidation. Pause a healthy empty read, detect corrupt canonical data concurrently, then release it and verify the old token cannot repopulate the cache or authorize degraded outage fallback.
- Medium, patch: recovery tests stopped before optional search completion. Exercise CompleteRebuildAsync, current rebuilt-entry indexing and surviving obsolete-entry removal; explicitly assert that an unknown null-Entries inventory does not produce an invented removal target. Canonical fence targets remain independently available for removal.
- Low, patch: recovery guidance lacked a concrete authoritative entry point and scope. Link the existing internal SDK surface, identify the authorized EventStore orchestrator, tenant/domain/shared-index scope, fresh candidate, staged lifecycle and verification steps; document lost-fence and external-inventory limits without inventing a module CLI or unsafe manual overwrite.
