---
title: 'Adopt five published FrontComposer UI capabilities'
type: 'refactor'
created: '2026-10-28'
status: 'in-progress'
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
- [ ] `Directory.Build.targets`, UI `Program.cs`, `Services/OptimisticReconcile.cs` — allow approved package primitives; fence shared reconciliation separately.
- [ ] UI/Admin/Consumer existing test files — prove actual shared rendering, registration and domain semantics; add package early-confirmation coverage with shared services present.
- [ ] Browser specs/configuration — run package forward/compact/download cases without source skips or asset substitution; preserve separate source qualification.
- [ ] `tests/README.md`, `docs/accessibility.md`, deferred ledger and prerequisite matrix — record five dispositions, keeping DW-100 reconciliation and blocked aggregate gates explicit.
- [ ] `tests/five-published-frontcomposer-adoption-2026-10-28/` under implementation artifacts — retain exact commands/results, package/source/asset hashes, review and limitations.
- [ ] Owning Git metadata — pinned commitlint validation, divergence inspection, task-only commit/push to main and remote identity verification.

**Acceptance Criteria:**
- Given package dependencies and the runtime opt-in, when shared capabilities execute, then producer components/services preserve every matrix behavior.
- Given registered package feedback, when confirmation precedes acceptance, then retained orchestration waits for readiness/acceptance and avoids duplicate completion.
- Given served package assets, when keyboard/confirmation/download browser controls run, then they pass without source substitution or capability skips.
- Given completed verification, when the ledger is reconciled, then only proven exits close and DW-100's fixed-reconciler package gate remains open.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

- Individual Debug test-project builds; built xUnit v3 assemblies, focused then full affected suites.
- Owned Debug browser server: accessibility, portals and shared-download controls; compare actual assets with selected package bytes.
- Attempt pinned source mapping where relevant; record blockers without editing mappings/dependency selections.
- Browser TypeScript/config checks, warning-override guard and CRLF-aware whitespace check.
- Pinned commitlint, scoped staging, fetch/divergence inspection, non-force push and remote SHA verification.
