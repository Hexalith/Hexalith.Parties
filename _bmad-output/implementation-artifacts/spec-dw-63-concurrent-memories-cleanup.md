---
title: 'DW-63/DW-169 preserve concurrent Memories cleanup mappings'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Cleanup overwrites its previously read Memories mapping inventory after HTTP deletions. Concurrent indexing can lose a newly committed mapping, and matching only a unit ID can remove a different case's uncleaned mapping.

**Approach:** Remove only successfully cleaned, exact snapshot entries from fresh state using bounded optimistic-concurrency retries. Preserve concurrent additions and changed unit/source/case identities, including during empty-key deletion and health-sentinel clearing. Return incomplete cleanup when the committed inventory still contains mappings. Preserve scoped HTTP response qualification, existing key/schema, partial-failure behavior, cancellation propagation and sanitized failures. Add deterministic persisted-state race regressions and close DW-63/DW-169 only after verification and independent review.

</frozen-after-approval>

## Implementation Notes

- User selected the recommended DW-63/DW-169 scope. No human-only intent gap or irreversible operation remains. This is a bounded internal bug fix: two behavioral call paths, an internal interface, mechanical test-double adaptations, focused regressions and documentation. No public API, real-data operation, migration, dependency update, deployment or remote publication is required; use the one-shot route.
- Baseline Parties revision: bb4d39f017b4075b50741932c834b2465c57bd92. Clean tree on main. Independent investigation confirms snapshot replacement and unit-ID-only subtraction. Authoritative Hexalith Git policy overrides stale project-context branch guidance.
- Prerequisites: existing Dapr 1.18.10 conditional state APIs and installed EventStore domain host remain sufficient; no new platform API or owner approval is required. Preserve selected root identities and dependency modes. EventStore source: ab8fa115c486875be3a22860a273d5d68a26c749; Memories source: ef842c9abca9b9a0f77699ba1232e97c23405693; Builds: 544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9. Record the complete dependency graph with validation evidence.
- Touched repositories: Parties only. Keep this existing Dapr-backed operational side-effect ledger's class (b), storage key, entry schema and rebuild-surviving behavior. DW-81 retains the separate persistence-seam migration; this repair does not claim to resolve that baseline deviation or reclassify the store.
- Implementation: replace internal ReplaceMappingsAsync with RemoveMappingsAsync accepting exact successfully cleaned entries and returning the retained mapping count. Re-read live state and ETag, subtract exact record matches, then conditionally save or delete; recompute on each of at most five attempts. Missing/unchanged entries are idempotent; a nonempty state lacking an ETag fails closed. Clear captures its target snapshot once and uses the same removal algorithm, preserving arrivals across conflicts. Move the existing interface and entry record to their own named files.
- Cleanup records successful full entries instead of removing all rows sharing a unit ID. A retained committed inventory blocks completion with a fixed diagnostic. Keep snapshot deletion counts separate from live inventory counts; exceptions and caller cancellation still propagate. DW-64/DW-65/DW-167/DW-168/DW-170 and cross-instance erasure fencing remain separate.
- Rollback path: reverse only this change's patch; no key/schema or persisted-data migration needs reversal. Preserve all existing behavior tests and exercise the baseline build and service assembly before changing production code. Retain baseline evidence and verify restored-source equivalence if an explicit rollback exercise is needed; do not revert user edits or dependency pointers.
- Validation lanes: required Aspire start/wait/describe baseline, then stop before builds. Debug/source build of tests/Hexalith.Parties.Tests with warnings-as-errors; direct xUnit v3 mapping, cleanup, indexer, mapper, health and projection-adapter classes; then the full service assembly. Run warning-override guard and whitespace checks. UI, deployment and topology parity are unaffected and are not claimed by focused verification.
- Parity checklist: immutable public contracts (I5); preserved tenant/case isolation (I6), fail-closed erasure progress (I7), sanitized diagnostics (I8); unchanged projection rebuild/freshness paths and class-(b) retention (I9/I10). Deterministic tests assert final persisted inventory for concurrent appends, changed identities, same-ID other-case partial failure, conditional-delete conflicts, concurrent cleanup, retry exhaustion, provider failures and cancellation. New consistency tests support this repair without asserting new I18 owner approval.
- Non-goals: terminal Memories workflow reconciliation, identical-identity recreation fencing, authoritative empty-inventory proof, mapping deduplication policy, cancellation recovery redesign, operational-ledger migration, retirement of baseline surfaces, or Epic 8 umbrella closure.

- Implementation completed with an internal exact-removal seam and ETag-guarded FirstWrite saves/deletes. Clear fixes its removal snapshot before retrying. Production types now each occupy one file; five consumer test-double files were adapted. Debug/source build is clean (zero warnings/errors), and the initial focused mapping/cleanup/indexer/mapper/health/adapter run passes 123/123. Baseline assembly XML records 1158/1158 passing tests. Independent review and final qualification remain in progress.

- Independent review found Redis reuses a per-key ETag after physical deletion/recreation, allowing a stale delete to remove a distinct new mapping. This supersedes the initial generic empty-key deletion design: domain removal always CAS-saves its list, including `[]`, retaining the generation at the existing hashed key. Physical Clear now accepts only reserved single-use health-probe identities; the sole production caller already supplies them. No entry schema or public API changes. Operators must drain older cleanup writers and avoid expiring live ledger keys. Source verification: https://raw.githubusercontent.com/dapr/components-contrib/v1.18.0/state/redis/redis.go.
- Review follow-up also strengthens the fake to store serialized values/versions by actual key and reset versions on physical recreation, adds overlapping real cleanup/indexing operations, tenant/party isolation, combined partial failure and append, post-HTTP reconciliation read failure, legacy-case adoption, and restricted probe clearing. Moved the remaining options type into its own file.

## Review Triage Log

- High, patched (same root cause as fake fidelity): Redis deletes its version with the key, so two cleanups followed by distinct-identity indexing can recycle an ETag and lose the new mapping. Domain cleanup now retains an empty CAS-saved inventory; physical clearing is constrained to reserved one-use health probes. The overlapping real cleanup/indexing regression covers the interleaving.
- Medium, patched (same root cause): the state fake kept monotonic versions after physical deletion and rejected stale-ETag creation on absent keys. It now resets the key-local version and accepts absent-key creation, matching the verified Redis Lua behavior.
- Low, patched: the fake ignored storage keys, weakening tenant/party boundary evidence. Serialized inventories and versions now use the actual key, and a shared-client test verifies both tenant and party isolation.
- Low, patched: the concurrent-cleanup test directly deleted fake state rather than invoking the real removal path. Overlapping real removal calls now verify idempotency, with and without intervening indexing.
- Low, patched: partial HTTP failure and concurrent addition lacked combined coverage. The mixed regression preserves the failed mapping and appended mapping while retaining the original snapshot's 1-of-2 progress.
- Low, patched: a post-HTTP reconciliation read failure lacked service-level coverage. The regression asserts sanitized propagated failure and unchanged persisted inventory, with no successful cleanup result.
- Low, patched: concurrent legacy-case adoption was only represented by direct state replacement. A real RecordMappingAsync adoption now runs during cleanup reconciliation, preserving the new case and requiring another pass while verifying the original fallback DELETE route.
- Low, patched: the options class remained a second production type in the store file. It now occupies PartyMemoryUnitMappingStoreOptions.cs with its existing configuration shape.

- Independent follow-up verified all eight patches by inspection and found no concrete remaining regression. Post-review Debug/source build has zero warnings/errors; all 133 focused tests pass without failures, errors or skips. Explicit baseline-source rollback and an unsafe-delete mutation exercise are running before final qualification.

- Rollback exercised against baseline bb4d39f017b4075b50741932c834b2465c57bd92 by temporarily restoring only owned C# changes to baseline and excluding newly added C# files: Debug/source build succeeded with zero warnings/errors and all 1158 baseline service tests passed. Exact implementation source bytes were restored afterward.
- Unsafe-deletion mutation exercise succeeded: temporarily reinstating domain empty-key physical deletion built cleanly but the overlapping real cleanup/indexing regression failed (retained count expected 1, actual 0). The corrected implementation was restored byte-for-byte. This proves the regression detects the reviewed distinct-identity ABA race; it is an expected negative-test result, not an outstanding failure.

- Final qualification: Debug/source build succeeds with zero warnings/errors; focused tests pass 133/133 and the full service assembly passes 1187/1187 with no failures, errors, skips or unrun tests. Warning-override and CRLF-aware whitespace guards pass. The AppHost is stopped; its interrupted baseline wait does not provide healthy-topology evidence. All eight independent findings are patched and follow-up verified; no new deferred work. Exact commands, identities, rollback/mutation proof and source hashes are recorded in tests/dw-63-concurrent-memories-cleanup-2026-10-18/validation.json. Both ledger entries are closed.
