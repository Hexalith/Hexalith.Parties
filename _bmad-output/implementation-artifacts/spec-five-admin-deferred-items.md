---
title: 'Complete five admin portal deferred items'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-177 and DW-178 leave the admin save acknowledgement delayed by reads and lingering after a matching projection row arrives. DW-179 leaves the erasure action unavailable after a dialog-open failure. DW-183 and DW-184 record missing regression coverage for degraded search retention and accessible party-type selection.

**Approach:** Complete these five related deferred items using the existing admin components and bUnit fixtures. Show the polite accepted-command notice before slow reads, clear it when a current healthy result contains the saved party, preserve authorization and route boundaries, and restore the erasure action after a failed dialog opening. Cover empty search results carrying only a degradation reason and the rendered checked states when switching both party types. Verify, independently review, reconcile the five ledger entries, commit and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- User explicitly requested five deferred items in one batch and a push on main. Freeform work has no sprint key; initial tree is clean on main. This is a small reversible UI correction with no intent gaps, data migration, dependency changes or new public API; use the oneshot route.
- Code map: PartiesAdminPortal.razor consumes accepted commands in ApplyAuthorizationContextAsync; establish notice before its awaited reads, including the unchanged-context path, and reconcile only incoming version-current healthy rows in ApplyRowsAsync. Retain existing route/authentication reset behavior. PartyGdprOperationsPanel.razor already has ClearErasureDialogState for resetting typed confirmation; separate dialog-opening failures from best-effort input-focus failures.
- Reuse CreateEditPartyPageTests accepted form submission to stage real command receipts and RecordingAdminPortalApiClient queued async responses for deterministic delayed-read, convergence and boundary checks. Extend PartiesAdminPortalComponentTests for dialog recovery and reason-only empty search. Extend existing radio-switch assertions in CreateEditPartyPageTests.
- Acceptance: given an accepted create and a delayed list/detail, when the portal renders, then the polite acknowledgement is visible before completion. Given that notice, when a current healthy response contains the matching party, then it clears; empty, unrelated, stale, degraded or superseded responses do not clear it. Given a route/authentication change during a read, when old reads finish, then the notice stays cleared.
- Acceptance: given a dialog-open JS failure, when requesting erasure, then the request action returns, typed confirmation is reset and no command is sent; a successful retry still requires exact typed confirmation. Given last-known rows and an empty reason-only degraded search response, then rows and bounded degraded status remain. Given a party-type switch, then exactly its rendered radio has aria-checked=true.
- Baseline Aspire start and describe succeeded; resources were waiting on the running security resource when inspected. The exact AppHost stopped successfully before builds. This is lifecycle inspection, not full topology acceptance.
- Verification uses the focused Debug admin test project and complete admin assembly, plus the warning/submodule guard and whitespace checks. Preserve exact commands/results in tests/five-admin-deferred-items-2026-10-21/.

- Implementation preserves existing LF endings in the four tracked source/test files to avoid unrelated whole-file line-ending churn. New BMAD artifacts use CRLF.
- Original regression run: build passed with zero warnings/errors; selected 19 cases produced five expected failures (pending list, pending detail, unchanged-context receipt, matching-row convergence and dialog opening recovery). Reason-only degradation and radio-state assertions also executed.
- Independent Blind Hunter review found four reachable issues in the initial candidate. All were patched: a new unchanged-context receipt refreshes/invalidate earlier reads; search mapping retains payload Freshness and only Current/legacy-absent payload freshness can converge; opening failure clears old correlation; repeated opening failures reset announcement focus. New tests cover each correction, including every freshness enum through list/search and noncooperative late successful replies.
- Selected-package Debug fallback is necessary: `dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json` exits 2 with HXR011 because HEXALITH_DAPR_HOME is unconfigured. No launcher gate was weakened and no local-source/topology qualification is claimed. Both directly affected Debug test builds use the repository-selected package graph.
- Current final AdminPortal build has zero warnings/errors and its complete assembly passes 231/231 with no skips. UI build/test evidence is recorded separately after its final run. Warning/submodule guard and whitespace checks are required before commit. No dependencies or gitlinks changed.

## Review Triage Log

- Medium, patch: earlier in-flight list reads were still current when an unchanged-context receipt arrived. Refresh the list after showing that receipt, invalidating previous versions. A noncooperative pre-receipt matching response cannot clear the new notice or paint its old row.
- Medium, patch: metadata-only convergence ignored payload freshness on typed queries. Preserve Freshness through search mapping and require Current (or absent legacy metadata) in addition to healthy wrapper metadata. Twelve list/search cases cover all six statuses.
- Medium, patch: a ShowAsync failure retained the prior command correlation beside a new failure outcome. Clear it; regression first performs a successful correlated restriction operation.
- Medium, patch: repeated ShowAsync failures reused the previous focus marker. Reset it on failure and verify a fresh focus invocation on the second failure before a successful retry.
- Medium, defer: matching identity does not confirm a particular edit has projected. The frozen scope and original DW-178 describe matching-row visibility; receipt APIs do not carry a command-specific projection witness. Record the stronger convergence follow-up in the ledger and avoid claiming latest-command confirmation.
- Low, rejected: an additional delayed capability-probe test would protect ordering beyond the specified slow list/detail cases. Current code places the notice before the probe; another helper and case add optional coverage without identifying another production defect. Required pending-read paths are exercised.

- Follow-up review identified a render-yield window before list invalidation. Invalidate/cancel older list requests before setting the notice or requesting rendering. The updated regression completes a matching pre-receipt response inline during the first notice render. Removing only that invalidation makes it fail; restoring the fix makes it pass. The reviewer confirmed the correction and found no remaining concrete introduced defect.
- Final restored-source validation: complete AdminPortal 231/231 and UI 388/388 (619 total), no skips; both Debug builds have zero warnings/errors. Warning/submodule and whitespace checks pass. Locked commitlint 21.2.2 validates the exact commit candidate with zero problems/warnings. Five selected ledger entries are complete; one stronger command-specific edit-convergence follow-up remains recorded explicitly.
