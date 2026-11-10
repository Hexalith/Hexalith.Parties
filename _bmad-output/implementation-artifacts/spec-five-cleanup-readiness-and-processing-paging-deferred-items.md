---
title: Close five cleanup, readiness and processing-paging deferrals
type: bugfix
created: 2026-11-10
status: done
route: oneshot
review_loop_iteration: 1
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve five open deferred entries that current code still exhibits:
guarantee both standalone Memories runtime-child teardowns when the final
authority receipt write fails; release the owned development-catalog
composition and private session directory when the Aspire builder is disposed
without the application ever starting; make the consumer JSON-download-failure
browser case wait for Blazor interactivity before clicking Export; classify
processing-history cursor decode and decoded-position failures as
`invalid-cursor`; and validate processing-history page size/offset before any
detail or processing read-model access.

Preserve existing authorization, cleanup refusal while owned processes remain
unquiesced, retained-key teardown semantics, the single domain export request
and failure/retry guidance assertions, and list/search paging behavior. Add
focused failure and boundary controls. Mark these five ledger entries resolved
only after verification and independent review, then commit and push the
changes on main as requested.

Decision (owner, 2026-11-10): apply both processing-history changes —
validate size/offset before any detail/processing read and classify cursor
failures as `invalid-cursor`, aligned with list/search; document it in
`docs/api-contracts.md`.

</frozen-after-approval>

## Implementation Notes

- Selection: two read-only triage passes over twelve open candidates. Live:
  ledger lines 2217, 2384, 2388, 2255, 2259. Already delivered in code with
  stale or done ledger rows: 1523, 1546, 1962, 2015, 2023, 2246 (`0be5b476`),
  2250 (`806ed7b8`); these receive no new credit here.
- Processing-history contract is settled by repository evidence, not a new
  owner decision: `docs/api-contracts.md` states page size is checked before
  cursor decoding or data access and cursor/offset failures are
  `invalid-cursor`; EventStore `docs/reference/query-api.md` defines both
  reasons; list/search already behave this way. Both reasons stay HTTP 400
  `query_invalid_page`; the in-repo client never branches on the reason.
  Validating first also removes the existing-versus-missing party signal for
  invalid sizes.
- Small reversible corrections; no migrations, persisted-state changes,
  dependency updates or deployments. Explicit five-item maintenance request
  uses the oneshot route.
- Correction to the selection note: ledger entries are identified by summary,
  not line number — runtime-child teardown on authority receipt failure;
  pre-Build SDK composition cleanup after finalization; consumer
  JSON-download-failure Export interactivity; processing-history invalid-cursor
  classification; processing-history paging-validation precedence.
- Correction to the contract note: repository evidence (EventStore reason
  definitions, list/search behavior) informed the recommendation, but the
  ledger required a compatibility decision, so the owner approved both
  processing changes in-session (frozen Decision). Processing stays
  cursor-only: any nonzero offset keeps `invalid-page`, now checked first; only
  list/search classify offset failures as `invalid-cursor`.
- Processing: `GetProcessingRecordsAsync` validates size/offset via
  `IsSupportedProcessingPaging` before any read; `ReadProcessingPageAsync`
  returns `InvalidCursor` (logging `LogCursorRejected`) for decode, scope and
  position failures. The read-model-unavailable revision check stays first.
- Teardown: `PersistAuthorityThenTeardownAsync` runs receipt, primary and
  survivor steps independently; failures surface only after both teardowns and
  only for a completed qualification (single or aggregate), never replacing a
  propagating qualification failure.
- Catalog: `ReleaseUnstartedOwnedTestModelAsync` disposes a still-pending
  bootstrap from the fixture's builder cleanup (after the existing quiescence
  guard, before builder disposal). Instrumentation showed a built, never-started
  application's DI disposes the bootstrap asynchronously after fixture disposal
  returns, so `DisposeAsync` now shares one in-flight attempt (retry after a
  fault; composition failure still blocks deletion).
- Surprise: throwing before `BuildAsync` disposes a testing builder whose
  suspended AppHost entry point then resumes and runs in the background,
  corrupting later fixture tests. The fault is injected inside `Build` through
  a stricter `NameValidationPolicyAnnotation`; no fixture seam is added.
- e2e: `tests/e2e/helpers/interactive-shell.ts` waits for
  `.fc-shell-root[data-fc-interactive="true"]`; used before Export in the
  failing case and the sibling export case.
- Verification (package mode, Release, `-warnaserror`): full
  `Hexalith.Parties.Tests` 2958 pass/13 env-gated skips; catalog classes 58/58
  (CI minimum 51); e2e consumer portal 125/125 over five retries-disabled
  repeats; e2e typecheck clean. Red-proof: 62/62 processing controls and the
  Build-fault catalog fact fail on the reverted code. Not run: source-mode
  Debug builds, Docker topology suites, owned Memories runtime facts. Evidence:
  `tests/five-cleanup-readiness-and-processing-paging-2026-11-10/`.

## Review Triage Log

Blind Hunter (one layer; no layers skipped), 14 findings:

- B1 medium, patched: doc said processing "follows the same classes" while
  keeping `invalid-page` for offsets. Rewrote the paragraph as a cursor-only
  rule; spec note corrected.
- B2 low, deferred: processing accepts a cursor with offset zero unlike
  list/search; pre-existing contract behavior needing an owner decision.
- B3 false/low, patched wording: retained directory after a composition
  disposal failure is the pre-existing conservative design; remark clarified.
- B4 low, rejected: no forced-overlap control for the disposal gate; it needs a
  new seam. Sequential re-entry (release then DI) runs on every built-app pass.
- B5 medium, patched: teardown failures could replace the qualification
  failure and a survivor failure could replace a primary one; now collected,
  surfaced only for completed runs, with survivor/both-teardown controls.
- B6 low, patched: added overflow position and an actual stale-cursor
  round trip; relaxed the codec call-count pin.
- B7 low, rejected: `size + 1` overflow is unreachable after validation;
  constant consolidation is a separate refactor.
- B8 low, patched: doc now gives restart guidance and revision precedence.
- B9 medium-if-real, deferred: other consumer-portal interactions may share the
  race; unobserved in 125 runs and not caused by this change.
- B10 low, patched: decision authority clarified above.
- B11 low, patched: entries referenced by summary above.
- B12 low, patched: verification and sibling export-case change recorded.
- B13 false: release deletes no earlier than the existing DI path and keeps the
  fixture quiescence guard and owned-process refusal; name kept.
- B14 false/low, patched: the store-state matrix discriminates the old order
  (all cases fail on red-proof); added invalid size/offset with cursor rows.
