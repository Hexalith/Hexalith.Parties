---
title: 'Align shared UI detection, post-race cleanup recovery, staging-envelope receipts, edit freshness styling, and G5 re-qualification'
type: 'bugfix'
created: '2026-11-27'
status: 'done'
baseline_commit: '869bd04362f914a05a010c039227f668c8ad903f'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-ux-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five verification and conformance gaps remain across DW-99, DW-168, DW-58, DW-100, and DW-97: four UI components still probe `IFcBrowserDownloadService` via `GetService` so download-only DI activates shared FrontComposer UI; the direct post-authorization cleanup race does not follow through to terminal resolution and the bulk `DeleteByPartyAsync` pre-`DELETE` race is untested; backfill `stage.json` receipts omit physical staging-envelope bytes/ETag; `EditMyProfilePage` lacks visual freshness-dot styling parity and Chromium `loading -> ready` live-region verification; and Story 8.7 G5 prerequisites are still dated to EventStore `5.4.0` instead of the `2d52234a` Platform 2.9 / Builds 6.9.1 cohort (which pins EventStore `5.6.0`, bumped from `5.5.2`).

**Approach:** Implement all five items in parallel via subagents without running tests mid-flight, then run the full build, unit/integration/Python/Playwright suites, fix any failures, review changes, update `deferred-work.md`, validate commits with repo commitlint, push directly to `main`, and report completed/skipped items plus remaining open deferred work.

## Boundaries & Constraints

**Always:**
- Touch only `Hexalith.Parties`; keep one C# type per file, XML docs on public/internal members, CRLF endings, `TreatWarningsAsErrors=true` (zero warnings), and Hexalith UX rules (FrontComposer + Fluent UI V5 + Fluent 2 design tokens only; no raw color literals or typography CSS).
- Preserve `#if HFC_SHARED_UI` guards, `PartyGdprOperationsPanel.razor` `ExportAsync` download resolution (`IFcBrowserDownloadService`), single always-mounted polite `[role="status"][aria-live="polite"]` in `EditMyProfilePage.razor` (zero `<FreshnessStatus>`), and `AccessibilityStyleGuardTests` / `ConsumerPortalPackagingTests`.
- Keep DW-168 strictly test-only (no test hooks in production cleanup services).
- Record exact verified identities for DW-97 (`2d52234a` pins EventStore `5.6.0` / Platform `v2.9.0` / Builds `v6.9.1`, `references/Hexalith.EventStore` `5484fae6c284cd8fe70b51bf921c5b31509b6c27`, noting the `5.5.2 -> 5.6.0` bump in `2d52234a`).
- Run tests only after all five items are implemented; run `python3 references/Hexalith.Builds/check.py` (when applicable) and repo commitlint before pushing to `main`.

**Never:**
- Edit `references/` submodules, add test hooks to production cleanup services, mount `<FreshnessStatus>` or a second polite live region in `EditMyProfilePage`, weaken tests/audits, or claim external/deployment/G5 gates are closed when prerequisites remain unfulfilled.

## I/O & Edge-Case Matrix

| Item | Scenario | Expected Behavior | Error / Edge Guard |
|---|---|---|---|
| DW-99 | Four UI surfaces with (a) throwing download-only `IFcBrowserDownloadService` and (b) `AddFrontComposerFeedback()` | (a) Local markup renders without invoking download factory; (b) shared FrontComposer component renders (`FcLiveRegion`, `FcFreshnessIndicator`, `FcDestructiveConfirmationDialog`) | `PartyGdprOperationsPanel.ExportAsync` still resolves `IFcBrowserDownloadService`; `#if HFC_SHARED_UI` builds clean both ways |
| DW-168 | Direct race follow-through (`imported` -> `"shared"` vs `"different"`); Bulk `DeleteByPartyAsync` paused before `DELETE` while importing same-case `"Admitted"` obligation | Direct: fresh Parties requires fresh scoped absence before settling both obligations and preserves `"unrelated"`; Bulk: 1 unconditional `DELETE` removes `"shared"`, `"unrelated"` survives, racing and fresh Parties calls retain `"imported"` pending and return `Cleaned == false` | Reject unqualified 404 (`{}`) / delayed deletion (`200 OK`) before settling; assert no precondition headers/content on `DELETE` |
| DW-58 | Backfill `stage` and non-stage receipts (`existing = false/true`) | `stage.json` emits `"stagingEnvelope": { present: true, sha256, etag }` distinct from `seed` (when `existing`) and `commit` bytes+ETag; non-stage phases omit `"stagingEnvelope"` | C# parent + Python `verify_workloads` reject missing, malformed, untrimmed, duplicate-key, unchanged-vs-seed/commit, or leaked-on-non-stage receipts; all C# cases gated in `BACKFILL_METHODS` |
| DW-100 | `EditMyProfilePage` across `loading -> ready (current/non-current) -> saving -> accepted -> confirmed` (bUnit + Chromium) | Single polite `[role="status"][aria-live="polite"]` stays mounted with zero node swaps or `role`/`aria-live` mutations; non-live `.hx-parties-profile__freshness-dot` (`aria-hidden="true"`) + `--current`/`--last-known` match `MyProfilePage` | Scoped CSS in `EditMyProfilePage.razor.css` uses Fluent 2 tokens only (passes `AccessibilityStyleGuardTests`); Chromium observes real `loading -> ready` via test-only E2E gate without blocking SSR |
| DW-97 | Re-qualify Story 8.7 G5 on `2d52234a` (`EventStore 5.6.0` / `Platform v2.9.0` / `Builds v6.9.1`) | Verify zero `PayloadProtection` packages in `5.5.2`/`5.6.0`, EventStore 8.11 `backlog`, 32/32 matrix commands, 24 retained security files, and `Hexalith.Parties.Security.Tests` (205 tests incl. 19 compatibility harness tests) passing on `5.6.0`; update matrix, Story 8.7, and `deferred-work.md` | Keep Story 8.7 `blocked`, matrix `needs-additive-api`, DW-97 `open` |

</frozen-after-approval>

## Code Map

- **DW-99:** `src/Hexalith.Parties.UI/Components/Shared/StatusLiveRegion.razor` (L2, L27-L39), `DataFreshnessIndicator.razor.cs` (L14-L26), `src/Hexalith.Parties.ConsumerPortal/Components/FreshnessStatus.razor` (L1, L17-L29), `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor` (L6, L182-L194; keep L912-L928 `ExportAsync`). Reference pattern: `EditMyProfilePage.razor` (L10, L15, L221-L234) & `EditMyProfilePageTests.cs` (L112-L126). Tests: `tests/Hexalith.Parties.UI.Tests/StatusLiveRegionTests.cs`, `DataFreshnessIndicatorTests.cs`, `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyProfilePageTests.cs`, `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`.
- **DW-168:** `src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs` (do not edit; direct L19-L183, bulk L186-L560), `tests/Hexalith.Parties.Tests/Search/PartyMemorySourceInventoryTests.cs` (L1374-L1495, L1629-L1797), `PartyMemoryDirectCleanupInterceptingIntentStore.cs` (L8-L148; extend or add one-type bulk interceptor), `PartyMemoryRecoveryHandler.cs` (L9-L89).
- **DW-58:** `tests/Hexalith.Parties.Tests/Projections/PartyLegacyBackfillProcessRuntimeTests.cs` (L70-L135, L211-L425, L614-L649, L720-L732), `scripts/verify-backfill-and-memories-deferrals.py` (L44-L87, L269-L380), `scripts/test_backfill_and_memories_integration.py` (L42-L115, L233-L341, L448-L493).
- **DW-100:** `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor` (L20-L34, L205-L234), `EditMyProfilePage.razor.css` (L1-L58; mirror Fluent-token rules from `MyProfilePage.razor.css` L26, L82-L103, L139-L144), `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/EditMyProfilePageTests.cs` (L97-L126, L229-L587), `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` (L1112-L1136) + `Program.cs` (test-only E2E fixture gate), `tests/e2e/specs/consumer-portal-routes.spec.ts` (L341-L398, L714-L800). Guards: `AccessibilityStyleGuardTests.cs`, `ConsumerPortalPackagingTests.cs`.
- **DW-97:** `Directory.Packages.props` (L22-L35), `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` (L1840, L1899-L1901), `8-7-data-protection-extraction.md` (L6-L24), `sprint-status.yaml` (L142-L145), `deferred-work.md` (L800-L806), `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs`.

## Tasks & Acceptance

**Execution:**
- [x] DW-99: Align `StatusLiveRegion.razor`, `DataFreshnessIndicator.razor.cs`, `FreshnessStatus.razor`, and `PartyGdprOperationsPanel.razor` (`SharedDestructiveDialogType`) to check `FcOptimisticReconciler` via `IServiceProviderIsService`, and add download-only + `AddFrontComposerFeedback()` bUnit tests across all four surfaces.
- [x] DW-168: Extend `DirectCleanupPostAuthorizationGapSendsUnconditionalDeleteWhileRetainingPendingObligations` for post-race terminal resolution (`"shared"` and `"different"`) requiring fresh scoped absence and preserving `"unrelated"`, and add companion bulk `DeleteByPartyAsync` pre-`DELETE` race test in `PartyMemorySourceInventoryTests.cs` (plus one-type interceptor helper if needed).
- [x] DW-58: Emit `"stagingEnvelope": { present, sha256, etag }` on `stage` receipts in `PartyLegacyBackfillProcessRuntimeTests.cs`, enforce presence, non-stage absence, and distinctness from `seed`/`commit` bytes and ETags in C# and `scripts/verify-backfill-and-memories-deferrals.py`, add C# and Python negative controls, and gate all C# controls in `BACKFILL_METHODS` + `scripts/test_backfill_and_memories_integration.py`.
- [x] DW-100: Restore non-live `.hx-parties-profile__freshness-dot` and `--current`/`--last-known` styling in `EditMyProfilePage.razor` + `EditMyProfilePage.razor.css` without swapping `[role="status"]`, update `EditMyProfilePageTests.cs`, and extend `consumer-portal-routes.spec.ts` (with test-only E2E loading gate) so `observeProfileLiveRegion` verifies `loading -> ready` and `ready -> saving -> saved` continuity in Chromium.
- [x] DW-97: Update `story-8-3-platform-api-prerequisite-matrix.md`, `8-7-data-protection-extraction.md`, `sprint-status.yaml` comment, and `deferred-work.md` with exact `2d52234a` (`EventStore 5.6.0` / `Platform v2.9.0` / `Builds v6.9.1`, `references/Hexalith.EventStore` `5484fae6c284cd8fe70b51bf921c5b31509b6c27`) identities and `blocked` verdict, then populate verified `Hexalith.Parties.Security.Tests` results after the full test pass.
- [x] Ledger: Update `_bmad-output/implementation-artifacts/deferred-work.md` for DW-58, DW-97, DW-99, DW-100, and DW-168 with 2026-11-27 verification results and remaining open gates.

**Acceptance Criteria:**
- Given all five items are implemented via subagents without premature test runs, when full builds, .NET unit/integration tests, owned DW-58 backfill rehearsal, Python verifiers, Playwright E2E, and repo checks run, then every affected lane passes with zero warnings and zero regressions.
- Given commit messages pass repo commitlint and all checks pass, when pushed directly to `main`, then `origin/main` contains the verified commits and the final report states completed/skipped items and exact remaining open deferred-work count.

## Implementation Notes

- **DW-99:** Aligned `StatusLiveRegion.razor`, `DataFreshnessIndicator.razor.cs`, `FreshnessStatus.razor`, and `PartyGdprOperationsPanel.razor` (`SharedDestructiveDialogType`, keeping `ExportAsync` download resolution intact) to check `FcOptimisticReconciler` via `IServiceProviderIsService`. Added download-only (throwing `IFcBrowserDownloadService` factory) and `AddFrontComposerFeedback()` bUnit coverage across `StatusLiveRegionTests.cs`, `DataFreshnessIndicatorTests.cs`, `MyProfilePageTests.cs`, and `PartiesAdminPortalComponentTests.cs`.
- **DW-168:** Extended `PartyMemoryDirectCleanupInterceptingIntentStore` with optional `afterRetainTerminalStatus` hook and extended `DirectCleanupPostAuthorizationGapSendsUnconditionalDeleteWhileRetainingPendingObligations` into a `[Theory]` covering `"shared"` and `"different"` post-race terminal outcomes requiring fresh scoped `404 MEMORY_UNIT_NOT_FOUND` absence while preserving `"unrelated"`. Added companion bulk race test `BulkCleanupPreDeleteRaceSendsUnconditionalDeleteWhileRetainingImportedObligation` pausing right after `RetainTerminalStatusAsync` before `DELETE` dispatch.
- **DW-58:** Emitted `"stagingEnvelope": { present: true, sha256, etag }` on `stage` receipts (omitted on all non-stage phases) in `PartyLegacyBackfillProcessRuntimeTests.cs`, enforced presence, non-stage absence, and distinctness from `seed`/`commit` bytes and ETags in C# and `scripts/verify-backfill-and-memories-deferrals.py`, added 25 C# negative controls (`StagingEnvelopeReceiptRefusesMissingMalformedUnchangedOrLeakedState`, gated in `BACKFILL_METHODS`) and 26 Python negative subtests + 25 gating subtests in `scripts/test_backfill_and_memories_integration.py`.
- **DW-100:** Restored non-live `.hx-parties-profile__freshness-dot` (`aria-hidden="true"`, hidden outside freshness display) and `--current`/`--last-known` classes in `EditMyProfilePage.razor` + Fluent-token scoped CSS in `EditMyProfilePage.razor.css` without mounting `<FreshnessStatus>` or swapping `[role="status"]`. Updated `EditMyProfilePageTests.cs`, added test-only interactive E2E load gate in `PartiesAdminPortalE2eFixture.cs`/`Program.cs` (with unit tests in `PartiesAdminPortalE2eFixtureTests.cs`), and extended `tests/e2e/specs/consumer-portal-routes.spec.ts` so `observeProfileLiveRegion` proves zero node swaps or `role`/`aria-live` mutations across `loading -> ready -> saving -> saved`.
- **DW-97:** Re-qualified Story 8.7 G5 on cohort adoption commit `2d52234a` (`Platform v2.9.0` / `Builds v6.9.1`, which bumped `HexalithEventStoreVersion` from `5.5.2` to `5.6.0`, `references/Hexalith.EventStore` `5484fae6c284cd8fe70b51bf921c5b31509b6c27`). Verified neither `5.5.2` nor `5.6.0` packs `Hexalith.EventStore.PayloadProtection*`, Story 8.11 remains `backlog`, 32/32 structural checks match, all 24 retained security files are intact, and Release `Hexalith.Parties.Security.Tests` passed 334/334 (19/19 compatibility harness). Updated `story-8-3-platform-api-prerequisite-matrix.md`, `8-7-data-protection-extraction.md`, `sprint-status.yaml` comment, and `deferred-work.md`.
- **Pre-existing CI test fix:** Updated `tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs` line 151 (`[InlineData("package", "3.0.4", "11.0.1")]`) to match the `HexalithCommonsVersion` `3.0.3` -> `3.0.4` bump adopted in Builds `6.9.1` (`2d52234a`).

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Disposition & Evidence |
| --- | --- | --- |
| BH-1 / ECH-1: `EditMyProfilePage.razor.css:88-93` forced-colors `.hx-parties-profile__freshness-dot` has lower specificity (`0,1,0`) than modifier rules (`0,2,0`) | `medium` | **Patch.** Verified at `EditMyProfilePage.razor.css:19-27,88-93`. Raised forced-colors selector to `.hx-parties-profile__freshness .hx-parties-profile__freshness-dot` (`0,2,0`, later in cascade). |
| ECH-2 / BH-8: `PartiesAdminPortalE2eFixture.cs:500-533` consumes armed gate on pre-canceled token and leaves `_activeConsumerProfileLoadGate` set after timeout/cancellation | `medium` | **Patch.** Verified at `PartiesAdminPortalE2eFixture.cs:500-533`. Added pre-lock `cancellationToken.IsCancellationRequested` check, cleared `_activeConsumerProfileLoadGate` in `finally`, and added `ConsumerProfileLoadGate_AlreadyCanceledTokenPreservesArmedGateAsync`. Singleton scoping claim is `false` for single-worker `Reset()`-isolated E2E fixture. |
| BH-2: `EditMyProfilePage.razor:206-215` `StatusMessage` checks `??` while `ShowFreshnessIndicator` checks `string.IsNullOrWhiteSpace` | `low` | **Patch.** Aligned `StatusMessage` to `!string.IsNullOrWhiteSpace(_saveStatusMessage)` for invariant consistency. |
| BH-3: `PartyMemorySourceInventoryTests.cs:1886` bulk race test omits `"imported"` `"Admitted"` workflow status in `bulk.Workflows` and `raceResult.BlockedReason` assertion | `medium` | **Patch.** Seeded `bulk.Workflows["imported"]` as `"Admitted"` and asserted `raceResult.BlockedReason.ShouldNotBeNullOrWhiteSpace()`. Bulk post-race terminal follow-through was outside DW-168's requested scope (direct test covers terminal follow-through; bulk test covers pre-`DELETE` race and fresh pending-obligation retention). |
| BH-4: `story-8-3-platform-api-prerequisite-matrix.md:1840` tail still says `historical 19/19 compatibility receipt is unvalidated` | `medium` | **Patch.** Verified at line 1840; updated trailing text to match the 2026-11-27 `334/334` (`19/19`) EventStore `5.6.0` result while keeping the 32 structural commands verbatim. |
| BH-5: `8-7-data-protection-extraction.md` has mixed line endings | `low` | **Reject.** Pre-existing mixed line endings in `8-7-data-protection-extraction.md` prior to this diff; reformatting 370 untouched lines adds unrelated churn. |
| BH-6: `scripts/verify-backfill-and-memories-deferrals.py` pins `5.4.0` / `4.1.0` | `false` | **Reject.** Intentional historical Memories `4.1.0` qualification script pin; DW-58 explicitly scopes to staging-envelope receipt binding and `BACKFILL_METHODS` gating. |
| BH-7: `deferred-work.md` DW-100 phrasing `ready -> saving -> saved` overstates synchronous Chromium E2E save | `low` | **Patch.** Refined DW-100 ledger text to state Chromium verifies `loading -> ready` and `ready -> saved` while bUnit verifies `loading -> ready -> saving -> accepted -> confirmed`. |
| BH-9: `PartyGdprOperationsPanel.razor` checks `FcOptimisticReconciler` without checking `IStringLocalizer` and duplicates probe across 4 files | `false` | **Reject.** Pre-existing localization assumption in `PartyGdprOperationsPanel`; DW-99 explicitly specifies matching `EditMyProfilePage.razor`'s `IServiceProviderIsService` + `FcOptimisticReconciler` check. |
| BH-10: Missing download-only `ExportAsync` and `AddFrontComposerFeedback` + throwing download tests | `low` | **Reject.** `ExportAsync` was untouched by DW-99 and remains tested by `SharedDownloadCandidatePreservesAdminBytesAndSafeCallerFilename`; all four DW-99 surfaces test both download-only and `AddFrontComposerFeedback()` modes. |
| BH-11: Missing top-level key allowlist in `verify_workloads` and extra non-object/null-sha256 negative controls | `low` | **Reject.** Both C# and Python validators already reject non-object `stagingEnvelope`, null/non-string `sha256`, and non-string `etag` at runtime; 25 C# + 26 Python negative controls already cover all DW-58 defect classes. |
| BH-12: `EditMyProfilePageTests.cs` allows freshness class/hidden diffs during text-only transitions | `false` | **Reject.** Disproved: `AssertPoliteStatus` asserts exact `ClassList.Contains` and `dot.HasAttribute("hidden")` on every transition (`EditMyProfilePageTests.cs:1081-1089`). |
| BH-13: Unsynchronized cross-task counters in `PartyMemoryDirectCleanupInterceptingIntentStore` and race tests | `false` | **Reject.** Disproved: `TaskCompletionSource.TrySetResult()` and `await` establish full happens-before memory barriers before all cross-task counter reads. |

## Verification

- `Hexalith.Parties.Standalone.slnx` Release build: **0 warnings, 0 errors**.
- All 16 .NET Release unit test assemblies passed (**6,482 passed, 0 failed**, 1 pre-existing skip in `Projections.Tests`): `Aspire.Tests` (9), `Contracts.Tests` (271), `Authentication.Tests` (12), `Client.Tests` (347), `Server.Tests` (315), `Projections.Tests` (466/467), `Security.Tests` (334/334 + 19/19 `CryptoKeyManagementCompatibilityHarnessTests` on EventStore `5.6.0`), `AdminPortal.Tests` (360), `ConsumerPortal.Tests` (140), `UI.Tests` (477), `Picker.Tests` (171), `Mcp.Tests` (94), `Sample.Tests` (66), `Ci.Tests` (436 — fixed `CommonsHttpRestoreRoutingTests` 40/40), `Catalog.Tests` (20), `Tests` (2,964; post-patch `PartyMemorySourceInventoryTests` 87/87).
- `Hexalith.Parties.Runtime.IntegrationTests` local backfill controls: **52/52 passed** (22 index-receipt + 25 staging-envelope + 5 workload-report/evidence-cleanup controls).
- DW-58 owned Redis 7.4.7 / Dapr 1.19.1 backfill rehearsal (`run-owned-backfill.py` + `verify-backfill-and-memories-deferrals.py`): `check_report` verified **65/65** required cases in `BACKFILL_METHODS`, `verify_workloads` passed for both `empty` and `existing` including `"stagingEnvelope"` receipts, and owned cleanup succeeded (`cleanupSucceeded: true`).
- Playwright E2E: `typecheck` passed, `test:config` **22/22 passed**, `test:ui` **82/82 passed** in Chromium (post-patch `consumer-portal-routes.spec.ts` **34/34 passed**, including `loading -> ready -> saved` live-region continuity and freshness-dot styling).
- Python & repo checks: `scripts/test_*.py` **140/140**, `_bmad/scripts/tests` **20/20**, `tests/ci-lifecycle` **187/187**, `verify-restored-assets.py` **5/5**, `render-platform-declaration.py --check`, and `check-no-warning-override.sh`: **all passed**.
