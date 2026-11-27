---
title: 'Harden cleanup race, bucket cutover, producer receipt, live region, and index receipts'
type: 'bugfix'
created: '2026-11-27'
status: 'done'
baseline_commit: 'c2309d2b01d7210cff691f034888fcbab877d77a'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-ux-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five verification and race-safety gaps remain across DW-168, DW-50, DW-82, DW-100, and DW-58: the post-authorization-to-DELETE cleanup race is not isolated to decide conditional-DELETE ownership; a delayed inline read can restore retired fallback after bucket cutover; reopened unit-only absence receipts are only tested via a mock handler; `EditMyProfilePage` swaps live-region DOM nodes when saving begins; and backfill phase receipts do not bind shared-index presence, byte hash, and ETag across workload processes.

**Approach:** Implement all five items in parallel via subagents, then run the full build and test suite, fix failures, update `deferred-work.md`, validate and push commits directly to `main`, and report completed/skipped items plus remaining open deferred work.

## Boundaries & Constraints

**Always:**
- Touch only `Hexalith.Parties`; keep one C# type per file, zero warnings (`TreatWarningsAsErrors=true`), XML docs, CRLF, and Hexalith UX rules (FrontComposer + Fluent UI V5 only; no custom typography/color CSS).
- Preserve erasure fences, `invalidateEarlier: true` absence ordering, zero-dictionary empty-cache churn invariants, `FreshnessStatus.razor`, `IServiceProviderIsService` `FcOptimisticReconciler` detection, `RuntimeTestRoutingTests`, `CanonicalUnits.Length == 2`, `restarts.Count == 6`, and single child method in `PartyMemoryOwnedRuntimeTests`.
- Implement all five items via subagents first; build and test only after all five are implemented.
- Validate commits with repo commitlint and push directly to `main` after checks pass.

**Never:**
- Edit `references/` submodules, add test hooks to production cleanup services, add persistent per-key dictionaries to `PartySdkLastKnownReadModelCache`, weaken tests/audits, or claim production deployment/writer-drain gates are closed.

## I/O & Edge-Case Matrix

| Item | Scenario | Expected Behavior | Error / Edge Guard |
|---|---|---|---|
| DW-168 | Pause after `ConfirmDirectCleanupAsync(..., [])`, import same-case `"Admitted"` obligation, release DELETE | 1 unconditional DELETE reaches Memories; `"shared"` removed; call returns `Cleaned == false`; fresh `GetAsync` & cleanup service retain pending obligation with 0 new DELETEs; decide to pursue conditional DELETE with Memories owner | `memory-ingestion-cleanup-snapshot-changed` -> `Cleaned = false` |
| DW-50 | Delayed inline revision check (inline-first, bucket-first, or inline-completes-before-bucket) vs complete bucket verification, then store `IOException` | `RetireEarlierProcessingFallback` (`invalidateEarlier: false`) removes entry & supersedes all other active reads for key; `TryGetProcessing` is `false`; degraded history & portability export both return `IsDegraded == true`, `[]` | `invalidateEarlier: true` & erasure eviction keep existing fences |
| DW-82 | Live Memories server serves completed unit-only row (`ReconcileAsync`), empty local mappings, seeded `permanent: false` receipt, unrelated survivor unit | Interrupted first DELETE persists pending obligation across recreated Dapr/stores/service; retry settles only after fresh `404 MEMORY_UNIT_NOT_FOUND` with incremented `CleanupRevision`; unrelated unit survives | Active unit keeps obligation pending (`Cleaned == false`) |
| DW-100 | `EditMyProfilePage` loading -> ready (current & non-current) -> saving -> accepted -> confirmed (and load-failure empty text), `sharedFeedback` false/true | Single `[role="status"][aria-live="polite"]` stays mounted with identical DOM/component node (`FcLiveRegion` `div` or fallback `p`); zero `FreshnessStatus` rendered | Errors stay `role="alert"` without unmounting live region |
| DW-58 | 6 backfill phases (`seed`, `interrupt`, `rebuild`, `stage`, `commit`, `verify`) for `existing = false` and `true` | Receipt `"index": { present, sha256, etag }` preserves absence/preexisting bytes+ETag through `rebuild`; `stage` preserves visible presence+hash with `etag: null`; `commit` writes new bytes+ETag; `verify` matches `commit` | C# parent & Python `verify_workloads` reject missing, drifted, leaked, unchanged, or mismatched index receipts |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs` (L124-L143, L625-L657) -- `ConfirmDirectCleanupAsync` + unconditional `DeleteAsync` (do not edit).
- `tests/Hexalith.Parties.Tests/Search/PartyMemorySourceInventoryTests.cs` (L1287-L1370) -- race tests; reuse `PartyMemoryTestReadModelStore`, `PartyMemoryRecoveryHandler`, `PartyMemoryRecoveryTests.Cleanup`/`Intents`/`Http`.
- `src/Hexalith.Parties/Queries/PartySdkLastKnownReadModelCache.cs` (L163-L189, L278-L297) -- `RetireEarlierProcessingFallback`; add `SupersedeOtherReads` for `invalidateEarlier == false`.
- `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs` (L207-L279, L359-L428, L2270-L2287) -- inline overlap test & regression guards.
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartyMemoryOwnedRuntimeTests.cs` (L229-L238) -- pass `ownershipServerAssemblyPath` for `currentSourceAuthority`.
- `tests/Hexalith.Parties.Tests/Search/PartyMemoryRuntimeTests.cs` (L34-L440, L567-L634, L771-L826) -- add reopened unit-only receipt check via `PartyMemoryOwnerSourceRuntime`, `ObserveCleanupClient`, `AssertDeletedOutputAsync`.
- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor` (L24-L40, L212-L227) -- single always-mounted live region (`DynamicComponent` `SharedLiveRegionType` or `<p>`).
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/EditMyProfilePageTests.cs` (L92-L126, L224-L312) & `tests/e2e/specs/consumer-portal-routes.spec.ts` (L341-L361) -- bUnit & Playwright DOM continuity checks.
- `tests/Hexalith.Parties.Tests/Projections/PartyLegacyBackfillRuntimeFixture.cs` (L147-L177) & `PartyLegacyBackfillProcessRuntimeTests.cs` (L37-L146, L272-L474) -- `TryReadPhysicalAsync`/`Serialize`, `"index"` receipts & parent assertions.
- `scripts/verify-backfill-and-memories-deferrals.py` (L250-L303) & `scripts/test_backfill_and_memories_integration.py` (L42-L214) -- independent index-receipt validation & negative controls.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- update DW-50, DW-58, DW-82, DW-100, DW-168 entries.

## Tasks & Acceptance

**Execution:**
- [x] DW-168: `tests/Hexalith.Parties.Tests/Search/PartyMemorySourceInventoryTests.cs` (plus one-type test decorator file if needed) -- pause after `ConfirmDirectCleanupAsync(..., [])`, import same-case obligation, release, and assert DELETE reached Memories, `"shared"` changed, and fresh Parties instances retain pending obligations.
- [x] DW-50: `src/Hexalith.Parties/Queries/PartySdkLastKnownReadModelCache.cs` & `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs` -- supersede all other active reads and remove entry on complete bucket retirement (`invalidateEarlier: false`); test delayed inline vs bucket cutover across degraded history and portability export.
- [x] DW-82: `tests/Hexalith.Parties.IntegrationTests/Topology/PartyMemoryOwnedRuntimeTests.cs` & `tests/Hexalith.Parties.Tests/Search/PartyMemoryRuntimeTests.cs` -- wire owner server path for `currentSourceAuthority` and verify live-producer reopened unit-only receipt across restart, fresh scoped absence, and unrelated unit survival.
- [x] DW-100: `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor`, `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/EditMyProfilePageTests.cs`, `tests/e2e/specs/consumer-portal-routes.spec.ts` -- mount one persistent live region through loading, ready, saving, accepted, confirmed, and load-failure states; verify DOM continuity with shared feedback enabled/disabled and in Chromium.
- [x] DW-58: `tests/Hexalith.Parties.Tests/Projections/PartyLegacyBackfillRuntimeFixture.cs`, `PartyLegacyBackfillProcessRuntimeTests.cs`, `scripts/verify-backfill-and-memories-deferrals.py`, `scripts/test_backfill_and_memories_integration.py` -- bind shared-index presence, byte hash, and ETag across all 6 phases; reject mismatched index receipts in C# and Python.
- [x] Ledger: `_bmad-output/implementation-artifacts/deferred-work.md` -- record 2026-11-27 results, DW-168 conditional-DELETE owner decision, and remaining external gates.

**Acceptance Criteria:**
- Given all five items are implemented via subagents, when full builds, unit/integration/Python/E2E suites, and repo checks run, then all affected lanes pass with zero warnings and zero regressions.
- Given DW-168 proves unconditional DELETE mutates Memories across the gap while fresh Parties preserves the pending obligation, then `deferred-work.md` records pursuing conditional DELETE with the Memories owner.

## Implementation Notes

- **DW-168:** Added one-type test decorator `PartyMemoryDirectCleanupInterceptingIntentStore` and `DirectCleanupPostAuthorizationGapSendsUnconditionalDeleteWhileRetainingPendingObligations`. Verified unconditional `DELETE` reaches Memories and removes `"shared"`, while `memory-ingestion-cleanup-snapshot-changed` forces `Cleaned == false` and fresh `GetAsync` + direct/bulk `PartyMemoryCleanupService` instances retain pending obligations with 0 new `DELETE`s. Recorded decision to pursue conditional `DELETE` with Memories owner.
- **DW-50:** Updated `PartySdkLastKnownReadModelCache.RetireEarlierProcessingFallback` (`invalidateEarlier == false`) to call `SupersedeOtherReads` and remove `_entries[key]` unconditionally, preserving `invalidateEarlier == true` precedence and erasure eviction fences with no new dictionaries. Added 9-case overlap theory and direct cache fence test verifying both degraded history and portability export stay empty on store outage.
- **DW-82:** Wired `ownershipServerAssemblyPath` for `currentSourceAuthority` and `PARTIES_TEST_REPOSITORY_ROOT` fallback in `PartyMemoryOwnedRuntimeTests.ResolveRoot()`; added `QualifyReopenedUnitOnlyReceiptAsync` in `PartyMemoryRuntimeTests` using live `SourceInventoryStore.ReconcileAsync`, empty local mappings, seeded `permanent: false` receipt, client recreation across restart, `200 OK` pending check, `404 MEMORY_UNIT_NOT_FOUND` settlement, and same-case survivor check; raised child/parent deadlines (`9m`/`11m`) to accommodate the added live producer ingestions.
- **DW-100:** Kept a single polite live region mounted in `EditMyProfilePage.razor` (`DynamicComponent` `FcLiveRegion` or fallback `<p>`) across all page states, removing `<FreshnessStatus>` swapping. Verified component/DOM continuity across loading, ready (current/non-current), saving, accepted, confirmed, and load/save failure in bUnit (`sharedFeedback` false/true) and Playwright (`MutationObserver` + handle identity).
- **DW-58:** Added `TryReadPhysicalAsync` and `Serialize` to `PartyLegacyBackfillRuntimeFixture`, recorded and validated `"index": { present, sha256, etag }` across all 6 phases in `PartyLegacyBackfillProcessRuntimeTests` and `scripts/verify-backfill-and-memories-deferrals.py`, and added 19 C# + 25 Python mismatched-index negative verifier controls.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Disposition & Evidence |
| --- | --- | --- |
| BH-1 / ECH-1: `SupersedeOtherReads` sets `Superseded = true` on newer overlapping reads, causing a later authoritative absence (`invalidateEarlier: true`) to skip `InvalidateReads` | `high` | **Patch.** Verified at `PartySdkLastKnownReadModelCache.cs:173-185,288-297`. Split `_activeReads` tuple to `(Key, Superseded, FallbackRetired)` so bucket retirement blocks inline admission without blocking later absence invalidation; add unit test. |
| BH-2: `RetireEarlierProcessingFallback` name/bool parameter conflates bucket cutover and absence | `low` | **Reject.** Semantic defect is resolved by splitting `FallbackRetired` from `Superseded` in BH-1; renaming an `internal` method across callers adds unnecessary churn without runtime harm. |
| BH-3: `DelayedInlineRevisionCheckCannotRestoreRetiredFallbackAfterBucketCutoverAsync` omits paged inline reads | `medium` | **Patch.** Verified at `PartySdkQueryHandlerTests.cs:905-908`: `ReadProcessingPageAsync` uses a distinct verification path. Add paged inline read overlap coverage. |
| BH-4 / ECH-2 / ECH-3: Fact `Timeout` values shorter than raised cooperative deadlines (`SelectedMemoriesRecoverAcrossProducerWorkloadRestarts` 25m vs 26m; `ActualSelectedMemoriesRunsTheRecoveryAndPhysicalErasureControl` 10m vs 11m; stale comment) | `medium` | **Patch.** Verified at `PartyMemoryRuntimeTests.cs:42,458` and `PartyMemoryOwnedRuntimeTests.cs:25,175-176`. Align timeouts (`1680000` ms) and scope 11m deadline to `currentSourceAuthority`. |
| BH-5: Non-restart JWT lifetime in `Bearer()` remains 10m while child/parent budgets are 9m/11m | `medium` | **Patch.** Verified at `PartyMemoryRuntimeTests.cs:1667`. Raise non-restart JWT lifetime from 10m to 15m so late retries/teardown cannot hit 401. |
| BH-6 / ECH-5: `ResolveRoot()` calls `Path.GetFullPath` before checking `Path.IsPathRooted`/NUL and silently falls back on invalid override; `observeResponse` unguardedly parses 404 JSON | `medium` | **Patch.** Verified at `PartyMemoryOwnedRuntimeTests.cs:502-510` and `PartyMemoryRuntimeTests.cs:1629-1635`. Validate `PARTIES_TEST_REPOSITORY_ROOT` before `GetFullPath` (fail fast when explicitly invalid) and guard 404 JSON parsing (`JsonException` + `JsonValueKind.String`). |
| BH-7: Removing `<FreshnessStatus>` drops dot/status classes, re-announces `"Up to date"` on save failure, leaves empty header slot on load failure, allocates `Dictionary` per render | `false` | **Reject.** Removing `<FreshnessStatus>` is required by DW-100 to prevent dual live regions/node swap; `"Up to date"` on save failure and per-render `Dictionary` allocation are pre-existing behaviors unchanged by this diff. |
| BH-8 / ECH-6: `AssertPoliteStatus` returns all nulls when `sharedFeedback == false` (no DOM continuity check), degraded-after-save continuity is untested, and Playwright `MutationObserver` omits `attributes` | `high` | **Patch.** Verified at `EditMyProfilePageTests.cs:578-643` and `consumer-portal-routes.spec.ts:711-782`. Verify bUnit snapshot diff (`SaveSnapshot`/`GetChangesSinceSnapshot`) and element continuity for both `sharedFeedback` modes, add degraded-after-save continuity test, and observe `role`/`aria-live` attribute mutations in Playwright. |
| BH-9 / ECH-4: `TryReadPhysicalAsync` drops non-empty bytes when ETag is empty | `medium` | **Patch.** Verified at `PartyLegacyBackfillRuntimeFixture.cs:185-187`. Treat as absent only when `string.IsNullOrWhiteSpace(etag) && value.IsEmpty`. |
| BH-10: `commit` and `verify` do not assert `fixture.Serialize(visible.Value).ShouldBe(physicalBytes)` | `false` | **Reject.** Disproved: `CommitStagedAsync` compacts with `ReadModelBatchCanonicalJson.Serialize` (ordinal key order), which differs in byte order from `DaprClient.JsonSerializerOptions` (`fixture.Serialize`). |
| BH-11: `stage` receipts omit physical staging-envelope bytes/ETag | `false` | **Reject.** Disproved: `stage` verifies physical staging envelope presence/bytes/ETag in-process and intentionally emits SDK-visible state (`etag: null`) per spec to prove absence/preexisting bytes survive staging. |
| BH-12: Untrimmed ETags accepted and missing `commit` null-ETag / duplicate-root negative controls | `low` | **Patch.** Verified in `VerifyIndexReceipt` and `validate_index_receipt`. Reject untrimmed ETags and add negative cases for null `commit` ETag, untrimmed ETag, and duplicate root `"index"`. |
| BH-13: `PartyMemoryDirectCleanupInterceptingIntentStore` lacks null check on `inner`; `BeforeRequest` does not check request content/extra headers | `low` | **Patch.** Add `ArgumentNullException.ThrowIfNull(inner)` and assert `request.Content is null` / no precondition headers on `DELETE`. |
| VG-1: `IndexReceiptRefusesMissingDriftedLeakedUnchangedOrMismatchedState` (19 cases) is omitted from `BACKFILL_METHODS` / `REQUIRED_CASES["backfill"]` in `scripts/verify-backfill-and-memories-deferrals.py` | `high` | **Patch.** Verified at `scripts/verify-backfill-and-memories-deferrals.py:52-62`. Register all `(existing, defect)` cases in `BACKFILL_METHODS` and add `test_backfill_requires_every_index_receipt_integrity_case` in `scripts/test_backfill_and_memories_integration.py`. |

## Verification

- `Hexalith.Parties.Standalone.slnx` Release package-mode build (`EventStore 5.5.2`, `Memories 4.1.1`, `FrontComposer 6.1.0`): **0 warnings, 0 errors**.
- All 16 .NET unit test assemblies passed (**6,464 passed, 0 failed**, 1 pre-existing skip in `Projections.Tests`): `Aspire.Tests` (9), `Contracts.Tests` (271), `Authentication.Tests` (12), `Client.Tests` (347), `Server.Tests` (315), `Projections.Tests` (466/467), `Security.Tests` (334), `AdminPortal.Tests` (359), `ConsumerPortal.Tests` (138), `UI.Tests` (465), `Picker.Tests` (171), `Mcp.Tests` (94), `Sample.Tests` (66), `Ci.Tests` (435), `Catalog.Tests` (20), `Tests` (2,962).
- `Hexalith.Parties.Runtime.IntegrationTests` local controls: **29/29 passed** (22 DW-58 index-receipt controls + 5 workload report controls + 2 evidence retention controls).
- DW-58 owned Redis 7.4.7 / Dapr 1.19.1 backfill rehearsal (`run-owned-backfill.py` + `verify-backfill-and-memories-deferrals.py`): `verify_workloads` passed for `empty` and `existing`, `check_report` verified **40/40** required cases in `BACKFILL_METHODS`, and owned cleanup succeeded (`cleanupSucceeded: true`).
- DW-82 live Memories 4.1.1 producer runtime qualification (`ActualCurrentSourceMemoriesRunsAuthorityAndPhysicalErasureControl`): **passed** (`1/1`, `0 skips`, `429.6s`).
- Playwright E2E (`typecheck`, `test:config` 22/22, `test:ui` 81/81 Chromium + post-patch `consumer-portal-routes.spec.ts` 33/33 with `MutationObserver` childList + `role`/`aria-live` attribute checks): **passed**.
- Python suites: `scripts/test_*.py` **136/136** (post-patch `test_backfill_and_memories_integration.py` **18/18**), `_bmad/scripts/tests` **20/20**, `tests/ci-lifecycle` **187/187**, `check-no-warning-override.sh`, `render-platform-declaration.py --check`, `verify-restored-assets.py`, and CRLF-aware `git diff --check`: **all passed**.
