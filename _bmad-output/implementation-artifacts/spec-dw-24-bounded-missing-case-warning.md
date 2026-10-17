---
title: 'DW-24 bounded missing-CaseId warning tracking'
type: 'bugfix'
created: '2026-10-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When Memories search is enabled without a CaseId, the search-index adapter retains an unbounded static entry per tenant/party to suppress duplicate warnings (DW-24). Long-lived hosts keep every observed identity even though the warnings contain no identifying data.

**Approach:** Bound warning tracking to 1,024 exact tenant/party pairs for the singleton adapter's lifetime. Suppress repeat warnings while a pair is retained, evict the oldest admitted pair at capacity, and permit an evicted pair to warn again. Keep concurrent admission atomic, retain no extra history on duplicate notifications, and preserve the constant warning text, converged/no-ingest behavior for missing CaseId, disabled-search behavior, configured indexing, cancellation, and removal cleanup. Complete DW-24 only.

</frozen-after-approval>

## Implementation Notes

Planning classification: no unresolved user-visible intent gaps, irreversible actions, public API, persistence, dependency or gitlink changes. Small footprint: the internal indexer, its existing focused test file, this spec, ledger resolution and sanitized validation evidence. Use the oneshot route. The request to handle deferred work is scoped to this single independently actionable ledger item.

Code map: `src/Hexalith.Parties/Search/PartyMemoryIndexEntrySearchIndexer.cs` owns the static dictionary and source-generated diagnostics. `Extensions/PartiesServiceCollectionExtensions.cs` already registers the adapter as a singleton; leave DI unchanged. Use instance-local HashSet and FIFO queue of ordinal string tuples under one lock; both collections remain capped at 1,024 and duplicate checks do not enqueue. A private admission method keeps logger calls outside the lock. Do not introduce a generic shared helper or new package. The existing Commons publication deduplication set is unbounded and does not provide the required concurrency behavior.

`tests/Hexalith.Parties.Tests/Search/PartyMemoryIndexEntrySearchIndexerTests.cs` provides Memories/mapping test doubles and existing indexing/removal controls. Extend it with retained-repeat suppression, cross-tenant and delimiter-safe identity separation, churn beyond capacity with oldest eviction and recent-pair suppression, instance isolation, and synchronized concurrent admission. Assert both collection sizes where logging alone cannot prove the bound; use a thread-safe logger double for distinct concurrent writes. Prove no-ingest/converged behavior and constant diagnostics without PII. Reuse existing shared test doubles where safe, without changing shared helper behavior.

Verification: establish a failing-before regression against the original dictionary, then build the individual service test project in Debug/source mode with warnings as errors, run focused indexer tests, and run the full affected assembly. Run the warning-override/nested-submodule gate and CRLF-aware whitespace validation. Record exact commands, summaries and the separate Aspire baseline outcome in `tests/dw-24-bounded-missing-case-warning-2026-10-17/validation.json`. Independent blind review and any required follow-up precede completion. No runtime repair or broader logging-rate policy change is included.

Implemented the capped FIFO tracking in the indexer and extended its existing tests. Added `tests/Hexalith.Parties.Tests/Search/PartyMemoryIndexEntryWarningLogger.cs`, a test-only concrete logger with a concurrent queue: the initial NSubstitute logger failed because the generic category is internal and not accessible to Castle's generated proxy. Corrected the fixture without changing production visibility, shared test helpers, or assembly friends. The corrected regression fails against the original implementation because it retains 3,072 entries instead of the required 1,024. The original-code Debug/source build has zero warnings/errors.

Runtime baseline: AppHost start and describe succeeded. Initial state showed the identity provider starting and domain services waiting. The exact healthy wait for Parties with a 20-second timeout exited 17; AppHost stop succeeded before source builds. This records a bounded baseline observation, not a diagnosis of the runtime topology.

Final initial verification: Debug/source build passes with zero warnings/errors; focused indexer tests pass 18/18, and the complete service assembly passes 1,077/1,077 with no failures/errors/skips. The warning-override/nested-submodule gate and CRLF-aware whitespace check pass. The exact Conventional Commit candidate passes repository-pinned commitlint 21.2.2 with zero problems/warnings. Independent review remains pending. Sanitized commands and evidence are in `tests/dw-24-bounded-missing-case-warning-2026-10-17/validation.json`.


## Review Triage Log

- Low, patch: initial concurrent tests separated identical and distinct workloads. Added gated mixed duplicate/new admission with a deterministically retained pair, FIFO eviction controls, and exact bounded bookkeeping checks.
- Low, patch: reflection previously accepted any enumerable fields. Explicitly inspect both tracking structures after completion, require equal counts, unique eviction entries and matching retained identities. The final regression assertion is stronger than the original-dictionary reproduction preserved in the evidence.
- Low, patch: the test logger originally discarded structured state and exceptions. Capture both channels and assert that the constant warning contains only its OriginalFormat property and no attached exception.
- Low, patch: existing options were never changed within one adapter lifetime. Add missing/configured/missing CaseId coverage proving a previously suppressed pair indexes after repair and is suppressed again when configuration is lost.
- Low, patch: cleanup tests did not combine enabled search, missing CaseId and populated warning history. Add persisted-case HTTP deletion and mapping end-state assertions after two suppressed indexing notifications.

Independent follow-up confirms all five review corrections and finds no remaining concrete issue. The review-stage build exposed that options are immutable (CS8852); corrected the reload test to replace CurrentValue on its test monitor instead of assigning CaseId on an existing options object. No production options, visibility, dependencies or analyzer policy changed.

Final verification after all review corrections: Debug/source build has zero warnings/errors, focused indexer tests pass 21/21, and the full affected service assembly passes 1,080/1,080 with no failures/errors/skips. Independent follow-up confirms the immutable-options test correction and finds no concrete issue. All five findings are resolved, no new deferred work, and DW-24 is complete. The separate Aspire baseline timeout remains recorded without a claim of full-topology qualification.
