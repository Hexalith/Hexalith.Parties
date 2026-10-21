---
title: 'Complete five deferred browser verification items'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferred browser checks lack continuing proof: DW-143 needs host-resolved fixture scope and a complete portal/picker browser run; DW-180 needs an actual privacy-export download trace; DW-189 leaves contact selection outside required CI; the unnumbered admin-accessibility CI item leaves dialog warning and dropdown assertions outside CI; DW-134 lacks an asserted image baseline and working update command. DW-153 duplicates DW-134 and counts as the same work item.

**Approach:** Finish these five items together as requested. Exercise existing fixture composition, download and compare the real exported bytes, run the existing admin/consumer/picker browser specs in blocking CI, and assert a deterministic screenshot that the existing update command regenerates. Verify the checks, independently review, reconcile the ledger, commit and push to main while preserving the seven pre-existing submodule updates outside the commit.

</frozen-after-approval>

## Implementation Notes

- The user authorized five deferred items and a push on main, then explicitly approved proceeding with the existing submodule updates untouched. These small reversible test and CI corrections introduce no public API, data migration, dependency upgrade or unresolved product choice; use oneshot. DW-116 remains open because a package-mode asset test would not establish its required source qualification.
- Code map: `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs` uses `WebApplicationFactory<Program>`. Resolve `IUserContextAccessor` through the actual enabled Test host, proving the existing `Program.cs` registration. Preserve production authentication and fixture enablement restrictions.
- Code map: `tests/e2e/specs/admin-parties-list.spec.ts` already tests dialog shadow-root warning/description, dropdown popup semantics and contact Email selection. Extend its erased-party export case with a real Playwright download, exact fixture filename and payload bytes from `PartiesAdminPortalE2eFixture.cs`; preserve the actual JavaScript helper and immediate revocation unless supported Chromium reproduces a failure.
- Code map: `tests/e2e/package.json`, `.github/workflows/ci.yml` and `tests/Hexalith.Parties.Ci.Tests/AccessibilityWorkflowTests.cs` own scripts and blocking gate enforcement. Add one explicit portal command for all three non-a11y browser specs within the existing job and preserve the specimen-only `test:a11y`. Keep both suites' reports in the uploaded evidence instead of overwriting one run with the next.
- Code map: `tests/e2e/specs/parties-accessibility.spec.ts` currently attaches a screenshot and compares manually loaded semantic JSON. Retain semantic checks and add Playwright's image matcher with a committed Chromium/Linux baseline, stable viewport/locale/timezone, loaded fonts and disabled animation/caret. Reuse `test:visual:update`; document its actual asserted PNG and the separate semantic JSON. No new raster generation or production styling is required.
- Acceptance: given the enabled fixture host, resolving shell context returns its tenant and fixture user. Given an erased-party export, clicking the real control produces the exact fixture JSON bytes and expected filename despite immediate URL revocation. Given normal CI, the admin contact selector and warning/dropdown checks run without optional conditions. Given a deterministic specimen, a visual CSS mutation fails the image comparison and the update command regenerates the asserted PNG.
- Validation: individual UI and CI Debug builds/assemblies, Node configuration tests, TypeScript check, full portal and accessibility suites, screenshot update and intentional style-mutation rejection, repository warning guard and exact pinned commitlint. Preserve bounded commands/results and useful evidence under `tests/five-browser-deferred-items-2026-10-21/`. Record package-mode fallback separately from source/topology qualification. Never update source receipts to uncommitted dependency pointers.
- Required Aspire baseline: exact AppHost start and describe succeed; security is Running/Unhealthy and dependent resources Waiting. Exact AppHost stop succeeds before builds. This is not complete topology readiness proof.

- Final qualification: UI Debug build has zero warnings/errors and 394/394 tests pass. CI Debug build has zero warnings/errors and 252/252 tests pass. Node 24 typecheck, eight configuration cases and actionlint pass. The exact CI-mode Release/package UI build has zero warnings/errors; combined browser execution passes 57 with only the two pre-existing DW-91 skips and no retries. The isolated Ubuntu 24.04 Playwright image also passes both visual checks against that Release host.
- Source launcher refuses the preserved root/checkouts mismatch (HXW004) and reports existing Platform/tool/catalog mismatches. No source selectors or dependency receipts are rewritten. This batch establishes browser/package checks, not source or full topology qualification. The initial full CI assembly's six audit-metadata failures disappear after normal restores with audit enabled; the gate is unchanged.
- The first Release browser run exposes the pre-existing edit test's transient-status timing assumption. Its body is byte-identical to HEAD before correction. A Current matching row can clear pending status immediately; the test now asserts the captured route-authoritative update, accepted redirect and visible detail. The create test retains pending-status coverage. The separate command-specific edited-state witness deferral stays open, and no production behavior changes.
- Final image proof uses the installed Playwright 1.63.0 Noble image. A 24px button shift fails the exact matcher; the update command changes the asserted PNG; a 2099 footer control still matches after the actual year normalization. The committed image and temporary source bytes are restored after every experiment. All owned standalone hosts and the AppHost are stopped.
- Closed DW-143, DW-180, DW-189, DW-134 and the unnumbered admin accessibility CI entry. DW-153 is a duplicate of DW-134 and is reconciled without counting it as a sixth item. The seven existing submodule updates remain outside the commit. The commit message passes the lockfile-pinned commitlint 21.2.2 with zero problems/warnings.

## Review Triage Log

- Medium, patch: server-generated footer year makes the PNG time-dependent. Normalize only that value to 2000; the future-year control passes.
- Medium, patch: Linux-only PNG would break Windows/macOS generic checks. Keep semantic checks everywhere and explicitly skip only the pixel case off Linux; document Ubuntu 24.04 qualification.
- Low, patch; source-reference claim false: document exact Release/package regeneration. Evaluated graph proves the legacy local selector does not activate source references; the reviewer withdraws that claim.
- Medium, patch: initial Ubuntu 26.04 rendering lacked CI-image qualification. Regenerate and verify in the installed Ubuntu 24.04 Playwright image against the Release/package host; retain its immutable digest.
- Medium, patch: single-fixture DI proof omitted the actual accessibility wrapper. Seven host cases cover both configurations, ordinary routes and the synthetic specimen with only circuit navigation supplied by bUnit.
- Low, patch/reject remainder: extend the simple source guard to describe.skip/fixme. No conditional early return or harmless-comment failure exists in current specs. Arbitrary executable-code analysis would add unsupported scope; actual combined execution records every required case.
- Low, patch: filename regex alone accepts stale or impossible instants. Require a canonical UTC instant between operation start/finish.
- Low, patch: byte equality omits Blob media type. Observe the real createObjectURL argument and require application/json with the exact byte length.
- Low, patch: successful download left no continuing receipt. Attach bounded filename, size and digest metadata in the report without the payload.
- Low, patch: successful transfer alone does not prove URL cleanup. Wrap the native creation/revocation calls without replacing the download helper and require one revoked Blob URL.
- Independent follow-up finds no remaining concrete defects; it independently verifies TypeScript, 59-test collection and all seven host theory cases. A separate final follow-up confirms the Release edit-test correction preserves its intended request/routing proof and leaves edited-state convergence outside its scope.
