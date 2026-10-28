---
title: 'Adopt five published FrontComposer UI capabilities'
type: 'refactor'
created: '2026-10-28'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '4e4bfb9b73bb1f0cc8b8fd44aa216d949a748cfa'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five UI slices remain source-only even though selected FrontComposer 4.7.1 packages publish their capabilities. Package builds retain duplicate implementations and skip newly executable browser checks.

**Approach:** Address DW-91/100/101/102/112 using Shell/Contracts.UI 4.7.1, producer revision `5688c3eba98567133285b0f51843d9daa8f24f0b`. Approval authorizes these named adoption slices, conditional on parity passing. Preserve the runtime opt-in and corrected local reconciliation: this package lacks the newer registration-readiness contract. Review, verify and push task changes to main.

## Boundaries & Constraints

**Always:** Preserve unrelated changes, especially the Builds catalog. Preserve labels, ARIA politeness, freshness mapping, export bytes, safe filenames, cancellation, ordinal confirmation and keyboard focus. Distinguish shared components from readiness-capable reconciliation. Retain local rollback without shared registration. Bind evidence to installed packages and served assets.

**Never:** Change dependencies, publish packages, edit producers or production data, infer source qualification from package success, or close whole G4/Story 8.9. DW-100 stays open for fixed-reconciler package adoption; its freshness/live-region portion can qualify independently.

**Approved exception (2026-10-28):** The user explicitly authorizes only the root EventStore family pin alignment from 3.128.3 to 3.128.4, matching the refreshed Builds catalog and Tenants.Client 5.11.1 minimum. All other dependency selections remain frozen.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior |
|---|---|---|
| Route focus | Cold/hydrated/client navigation | Forward Tab reaches skip links/targets; route announcement survives |
| Compact layout | Phone/tablet/desktop/resize | Navigation bypass exists exactly with its unique mounted target; content bypass survives |
| Freshness | Every state; polite/assertive/none | Shared components preserve domain text/ARIA; missing registration uses local components |
| Export | Admin/Consumer file/JSON; repeated/cancelled | Identical bytes/safe names; browser/stream cleanup; existing failure feedback |
| Typed confirmation | Empty/case/space mismatch/exact/Escape | Exactly one guarded submit for ordinal match; safe cancel/focus |
| Reconciliation | Shared package services; delayed registration/early confirmation | Corrected local orchestration waits for readiness/acceptance and drains cancellation |

</frozen-after-approval>

## Code Map

- `Directory.Build.targets`: source-only `HFC_SHARED_UI`; separate package capability from readiness support.
- UI `Program.cs` and `Services/OptimisticReconcile.cs`: registration opt-in and producer dispatch fence.
- Existing UI freshness/live-region, Consumer freshness/privacy and Admin GDPR components already map producer APIs behind compilation/service guards; reuse them.
- UI `MainLayoutAccessibilityTests.cs`, `RestoredFrontComposerAssets.cs`, component and reconciliation tests supply package/asset/lifetime controls; existing Admin/Consumer suites supply confirmation/download cases.
- `tests/e2e/specs/parties-accessibility.spec.ts` and `shared-feedback-source.spec.ts`: source-only gates and asset provenance; preserve source identity checks while adding package qualification.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Build.targets`, UI `Program.cs`, `Services/OptimisticReconcile.cs` — allow approved package primitives; fence shared reconciliation separately.
- [x] UI/Admin/Consumer existing test files — prove actual shared rendering, registration and domain semantics; add package early-confirmation coverage with shared services present.
- [x] Browser specs/configuration — run package forward/compact/download cases without source skips or asset substitution; preserve separate source qualification.
- [x] `tests/README.md`, `docs/accessibility.md`, deferred ledger and prerequisite matrix — record five dispositions, keeping DW-100 reconciliation and blocked aggregate gates explicit.
- [x] `tests/five-published-frontcomposer-adoption-2026-10-28/` under implementation artifacts — retain exact commands/results, package/source/asset hashes, review and limitations.
- [x] Owning Git metadata — pinned commitlint validation, divergence inspection, task-only commit/push to main and remote identity verification.

**Acceptance Criteria:**
- Given package dependencies and the runtime opt-in, when shared capabilities execute, then producer components/services preserve every matrix behavior.
- Given registered package feedback, when confirmation precedes acceptance, then retained orchestration waits for readiness/acceptance and avoids duplicate completion.
- Given served package assets, when keyboard/confirmation/download browser controls run, then they pass without source substitution or capability skips.
- Given completed verification, when the ledger is reconciled, then only proven exits close and DW-100's fixed-reconciler package gate remains open.

## Implementation Notes

- Primary acceptance verifies 1,200 passing .NET cases, 68 package browser cases without skips/retries, exact installed/served asset hashes, and all six matrix rows through `tests/five-published-frontcomposer-adoption-2026-10-28/audit-matrix.py`. Four named package exits close; DW-100 fixed shared-reconciler adoption and source HXR015 remain explicit.
- Human-approved EventStore family alignment is the sole dependency edit. User commits made during this run remain preserved. Git publication follows independent review.

- Approved 4.7.1 package primitives now compile behind `HFC_SHARED_UI`; only selected source compiles `HFC_SHARED_RECONCILE_READINESS`. Registration stays opt-in and package feedback retains corrected local orchestration.
- Package browser checks run without forward/compact capability skips or source-asset substitution. Actual shared registration/rendering, ordinal confirmation, export bytes/names and teardown are covered. Cancel/Escape restore the initiating erasure control once after its conditional recreation.
- Strict individual Debug builds have zero warnings/errors. Full UI 445, Admin 325 and Consumer 85 cases pass; primary Client verification adds 345 passes. All 68 package browser cases pass without skips/retries, including the existing PNG comparison. Node 24 typecheck/configuration, warning and CRLF-aware whitespace checks pass.
- DW-91/101/102/112 package exits close; DW-100 remains open for fixed-reconciler package adoption. Fresh source launch refuses HXR015 (Aspire CLI 13.6.4 versus catalog SDK 13.6.1). No source qualification or aggregate readiness is inferred.
- Evidence: `tests/five-published-frontcomposer-adoption-2026-10-28/README.md`; initial restore/focus failures remain historical. Primary acceptance, independent review and Git publication follow the implementation handoff.


- Final review remediation supersedes the initial counts above: UI 445, Admin 327, Consumer 85 and retained Client 345 all pass (1,202 total), with all 68 package browser cases passing without retries/skips. All six matrix rows and 11 evidence controls pass normally and under optimized Python.
- BH-01 through BH-10, EC-01 and VG-01 are resolved. The independent verification reviewer rechecked actual package navigation cancellation, matching operation IDs and strengthened evidence checks with no unresolved finding. Primary audit uses unique xUnit record IDs because long theory display names can be truncated identically.
- The sole remaining bounded adoption gate is DW-100 fixed shared-reconciler package publication/adoption; source HXR015 and whole-story gates remain separate. The user has explicitly authorized the final commit and push to main.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence |
|---|---|---|
| BH-01 | medium / patch | Fallback Escape calls ClearErasureDialogState directly and bypasses the new restoration flag; route it through CancelErasure. |
| BH-02 | low / patch | Pending restoration survives ClearSensitiveOperationState during party/tenant changes; clear it at that existing reset. |
| BH-03 | medium / patch | Package browser cancellation bypasses the actual service/page navigation; the only navigation test is source-only. |
| BH-04 | medium / patch | The bUnit test counts cancelDownload without binding its ID to beginDownload/downloadStream; an incorrect ID passes. |
| BH-05 | medium / patch | run-check.py attaches pre-existing XML when a failed runner emits none. Require fresh results. |
| BH-06 | medium / patch | The verifier omits final .NET suite and Client build exit-code checks; altered nonzero exits still report success. |
| BH-07 | medium / patch | Assembly counters are not tied to retained case count/errors; an empty case list satisfies all(...). |
| BH-08 | medium / patch | Native browser XML checks only failure/skipped elements; empty testsuites passes. Bind counts/errors/identities. |
| BH-09 | medium / patch | Unstaged-only git diff omits staged implementation from refreshed input inventory. Include staged changes/new files. |
| BH-10 | low / patch | Receipt omits actual Parties implementation DLL hashes; add host/test output hashes for the exercised implementation. |
| EC-01 | medium / patch | Same fallback Escape root cause as BH-01; actual handler directly clears state despite the reviewer snippet naming CancelErasure. |
| VG-01 | medium / patch | Pre-verified: mocked counts/direct native calls do not bind package service cancellation to page disposal. Same root cause as BH-03. |


## Verification

- Individual Debug test-project builds; built xUnit v3 assemblies, focused then full affected suites.
- Owned Debug browser server: accessibility, portals and shared-download controls; compare actual assets with selected package bytes.
- Attempt pinned source mapping where relevant; record blockers without editing mappings/dependency selections.
- Browser TypeScript/config checks, warning-override guard and CRLF-aware whitespace check.
- Pinned commitlint, scoped staging, fetch/divergence inspection, non-force push and remote SHA verification.
