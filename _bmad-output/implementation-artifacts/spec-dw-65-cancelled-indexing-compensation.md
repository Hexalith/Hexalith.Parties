---
title: 'DW-65 compensate cancelled Memories mapping writes'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** After Memories acknowledges ingestion, caller cancellation during mapping persistence bypasses compensating deletion. The acknowledged unit can remain outside later erasure discovery.

**Approach:** Await the existing compensating DELETE using an independent, bounded recovery token before rethrowing the original mapping cancellation. Preserve captured tenant/case/unit routing, safe response classification, sanitized failure diagnostics, and already-persisted mapping inventories. This closes DW-65's skipped-compensation boundary; durable workflow reconciliation and terminal physical deletion remain separate DW-170/DW-168 work.

</frozen-after-approval>

## Implementation Notes

- Selected the next bounded indexing-cancellation follow-up after completed DW-64. Independent investigation confirms no human-only intent gap, irreversible action, public API or dependency change. Small footprint: one internal service, one focused test class, this spec, ledger resolution and validation evidence. Use the oneshot route.
- `src/Hexalith.Parties/Search/PartyMemoryIndexingService.cs`: change only mapping cancellation recovery. Reuse `TryCompensatingDeleteAsync`, `MemoriesRoutes.CaseMemoryUnitPath` and `PartyMemoryCleanupResponse`. Use a fresh five-second cooperative token, following DW-64. Preserve ordinary compensation cancellation behavior and ingestion cancellation without an acknowledged ID. Never delete mappings after an uncertain write acknowledgement.
- `tests/Hexalith.Parties.Tests/Search/PartyMemoryIndexingCancellationTests.cs`: exercise real REST ingestion/deletion and serialized mapping state, deterministic cancellation before/during/after a mapping write, original exception/token preservation, retained unrelated mappings, safe routing/auth, rejected responses, transport failure and recovery timeout. Retain all existing mapper and cleanup tests.
- Prerequisites: existing Memories client/versioned cleanup route, unchanged approved dependency selections, root-only checkouts. No new persistence; operational-ledger migration remains DW-81. One accepted DELETE does not establish terminal workflow erasure.
- Parity: preserve public commands/queries and contracts, tenant/case isolation, erasure retry discovery and existing mapping contents, PII-free diagnostics, projection/replay and erased-party filtering. Existing baseline coverage stays intact.
- Validation: required Aspire start/wait/describe, then stop before building. Build the affected service test project in Debug/source mode with warnings as errors; invoke the built xUnit v3 assembly directly for focused classes and the complete affected assembly. Run warning-override/whitespace guards. Preserve exact commands, full revision identifiers, hashes and outcomes in `tests/dw-65-cancelled-indexing-compensation-2026-10-20/validation.json`.
- Rollback: temporarily restore only the baseline indexing service and omit the new test class, build and execute the baseline complete service assembly at unchanged dependency selections, then restore the implementation byte-for-byte. This retains all existing recovery rollback seams.

- Implemented independent five-second compensation for mapping cancellation. Existing ordinary compensation still propagates subsequent caller cancellation. Added a separate mapping-boundary helper because the internal store contract is deliberately inaccessible to dynamic proxy generation; no production visibility was widened.
- Direct-await cancellation observation preserves exception identity; the generic async assertion helper synthesized TaskCanceledException when inspecting cancelled tasks. Real serialized store/provider hooks cover cancelled admission, suspended persistence, and provider commit with lost acknowledgement.

## Review Triage Log

- Low, patch: boundary 0 originally bypassed the real store. It now calls the real mapping store with an already cancelled token and observes its initial cancellation check.
- Low, patch: boundary 2 originally cancelled after the store returned successfully. The provider now persists serialized state and throws cancellation before acknowledging the write; inventory assertions cover the uncertain commit.
- Low, patch: non-POST fixture requests originally counted as deletion. Compensation fixtures now explicitly require DELETE before mutating remote state or recording requests.
- Low, patch: the timeout assertion originally allowed twelve seconds for five-second recovery. Measure from the observed DELETE boundary and require completion within eight seconds, allowing scheduling tolerance while rejecting a ten-second budget.
- Low, patch: transport failure originally assumed local port 1 was unavailable. A controlled local endpoint now aborts the DELETE connection.
- Independent follow-up verified all five corrections and direct-await exception observation; no remaining concrete concern was found. No new deferred work.

- Final verification: Debug/source build has zero warnings/errors; focused tests pass 61/61 and the complete affected service assembly passes 1933/1933 without failures/errors/skips/not-run tests. The baseline reproduces all five response regressions. Exercised rollback passes 1920/1920 existing tests, and implementation source/tests are restored byte-for-byte. Warning and whitespace guards pass; no AppHost remains running. Exact commands, full source identities, hashes, review results and the separate topology limitation are recorded in tests/dw-65-cancelled-indexing-compensation-2026-10-20/validation.json. DW-65 is closed; DW-168/DW-170 remain open.
