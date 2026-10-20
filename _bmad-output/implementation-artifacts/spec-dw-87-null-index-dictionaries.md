---
title: 'DW-87: Reject null Party index dictionaries with bounded recovery'
type: 'bugfix'
created: '2026-10-20'
status: 'in-progress'
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
