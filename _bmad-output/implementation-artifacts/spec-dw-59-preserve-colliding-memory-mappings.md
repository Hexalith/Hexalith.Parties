---
title: 'DW-59 preserve colliding Memories mapping rows'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Recording a Memories mapping can discard another mapped unit when the incoming unit ID matches one row and its source URI matches a different row in the same case. Cleanup then cannot discover that discarded unit.

**Approach:** Give an eligible unit-ID match precedence over source-only matches, retaining the distinct units needed by cleanup regardless of inventory order. Recompute matching against fresh state on every concurrency retry. Preserve existing source-only replacement when no eligible unit-ID match exists, legacy-case adoption, cross-case isolation, duplicate-unit handling, no-op writes, bounded retries, failure/cancellation behavior, and the storage key/schema. Verify serialized persisted inventories and cleanup through both retained unit routes, then resolve DW-59 after independent review.

</frozen-after-approval>

## Implementation Notes

- Selected one self-contained deferred bug from the user's deferred-work request. No intent gaps, irreversible operations, public APIs, migrations, dependency updates, or deployment are required. Footprint: one internal store algorithm, focused mapping and cleanup tests, the search operations documentation, this spec, the existing ledger entry, and validation evidence. Use the one-shot route.
- Baseline: Parties `679b5b2aa04ac8cea4b29edcf17a9f8ae3fe792a`, clean `main`. Independent investigation confirms the defect and the existing source-only replacement compatibility requirement. Keep the existing operational Dapr mapping seam; DW-81 tracks its separate EventStore persistence migration.
- Code map: `src/Hexalith.Parties/Search/PartyMemoryUnitMappingStore.cs`, `RecordMappingAsync` (matching inside the five-attempt ETag loop); `tests/Hexalith.Parties.Tests/Search/PartyMemoryUnitMappingStoreTests.cs` (existing compatibility assertions); `PartyMemoryMappingState.cs` (reuse serialized per-key state and deterministic mutation hooks); `PartyMemoryCleanupServiceTests.cs` (reuse real store and recording HTTP handler). No cleanup API change is needed.
- Verification: perform the required Aspire baseline and stop it before builds; add collision regressions for both row orders, legacy entries, cross-case identities, duplicate units, no-op recording, and conflict retry; demonstrate failure against the original algorithm. Build the service test project in Debug with source references and warnings-as-errors, run focused mapping/cleanup/indexing tests, then its full assembly. Run the warning-override guard and whitespace checks. No full-topology or physical-erasure qualification is claimed.
- Rollback: reverse this task's code patch; persisted entry shape and hashed keys do not change. Previously discarded mappings cannot be recovered by this forward fix. Terminal Memories workflow reconciliation, source-only replacement policy, authoritative remote inventory, cancellation recovery, and distributed erasure fencing remain separate work.

- Implemented an eligible-unit existence check inside the ETag loop, suppressing source-only replacement only when the incoming unit already has an eligible mapping. The existing loop retains duplicate-unit collapse, case adoption, ordering, and no-op detection. No API, dependency, key, or schema changed.
- Baseline runtime: Aspire start succeeded; `aspire wait parties --timeout 15` reported healthy, and describe reported the Parties, EventStore, Tenants, security, and Parties UI resources healthy. Stopped and confirmed the AppHost stopped before building. Optional MCP and admin UI resources were not started; this is baseline health evidence, not full topology qualification.
- Regression proof: the original production algorithm built with zero warnings/errors but failed all 11 initial collision cases (69 tests: 58 passed, 11 failed, no skips). The fixed initial build passed 173 focused tests and all 1244 service tests without errors/skips. Additional reviewed edge cases are undergoing final verification.
- Independent Blind Hunter review produced four findings, all patched. Follow-up inspection confirmed every correction and found no new concrete regression. No review findings were deferred.

- Final qualification: Debug/source build succeeds with zero warnings/errors; 180/180 focused tests and 1251/1251 service tests pass with no failures, errors, skips or unrun tests. Warning-override and whitespace guards pass; pinned commitlint 21.2.2 validates the exact commit message with zero problems/warnings. All four independent review findings were patched and follow-up verified, with no deferrals. Evidence: tests/dw-59-mapping-collisions-2026-10-18/validation.json. DW-59 is resolved; no remote push.

## Review Triage Log

- Low, patched: the legacy adoption comment implied every matching legacy row was adopted. It now describes selected replacements; retained source-only legacy rows keep their fallback behavior. Serialized-state and cleanup-route assertions cover both row orders.
- Low, patched: collision cleanup had only all-success evidence. Added partial failure with identical source URIs, exact failed-entry retention, and a fresh cleanup reader that retries only the failed unit.
- Low, patched: the new duplicate-source inventory lacked a later unseen-unit continuation. A fresh writer and reader now verify the explicitly preserved source-only replacement compatibility policy.
- Low, patched: conflict coverage initially kept the eligible-unit decision true. Added real concurrent indexing and cleanup interleavings that change the decision from false to true and true to false, asserting persisted inventories and bounded save attempts.
