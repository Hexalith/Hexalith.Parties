---
title: 'DW-19 prepare search candidates once per query'
type: 'refactor'
created: '2026-10-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-19 records query-only full-phrase and candidate-collection allocations inside each party evaluation. Both local fuzzy and semantic providers repeat this work for every entry, including 10K-entry searches.

**Approach:** Prepare the joined normalized-token phrase and materialized candidates once per search and reuse them for entry evaluation. Preserve matching, phrase-first order, duplicate-token handling, original token-count scoring, metadata, filters, cancellation, deterministic ordering, and paging. Keep DW-20's independent relevance-coverage change outside this work. Verify compatibility and directly measure allocation growth with longer queries over 10K nonmatching entries, alongside the unchanged latency gates.

</frozen-after-approval>

## Implementation Notes

- Investigation: `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs` and `SemanticPartySearchProvider.cs` own the repeated `string.Join` and candidate collections in `EvaluateEntry`. Move them beside tokenization in `Search`, passing the phrase, candidate array, and original token count privately. No new public API or shared platform helper is needed.
- Scope is small, reversible and has no human-only intent gaps. No persistence, migration, dependency update, configuration trigger, or external publication is involved.
- Preserve joining parsed tokens rather than using the raw normalized query: mixed whitespace currently collapses. Preserve zero tokens after diacritic removal, single-token fuzzy matching, and the ordinal synthetic-phrase comparison.
- Reuse the provider tests and isolated performance collection under `tests/Hexalith.Parties.Tests/Search/`. Add shared behavioral and allocation regression coverage; build the individual service test project in Debug/source-reference mode and invoke its built xUnit assembly.
- Baseline: the exact Parties AppHost was started, inspected and stopped using Aspire. `aspire wait parties --timeout 20` succeeded; Parties/security were healthy, but EventStore, Tenants and UI reported Finished. This is not full-topology qualification. Other running AppHosts were left alone.

- Implemented query preparation in both providers; private entry evaluation receives the reusable array, joined phrase and original token count. No public surface or scoring change.
- Added 18 behavioral/allocation cases in `SearchQueryCandidateTests`. Baseline allocation growth is 2,400,728 bytes for longer queries over 10K entries; fixed growth is 968 bytes in both providers. A separate candidate-array mutation is detected independently by the token-count allocation regression.
- Final Debug/source build passes with zero warnings/errors; focused search/performance classes pass 80/80 and the full service assembly passes 1098/1098 with no failures/errors/skips/unrun tests. The warning-override guard passes. Durable sanitized evidence and commands are in `tests/dw-19-hoist-search-candidates-2026-10-17/` beside this spec.
- Exact local commit message passes repository-pinned commitlint 21.2.2 with zero problems/warnings; evidence is retained in that directory. No push.

## Review Triage Log

- Medium, patched: comparing two equal-token-count queries only detects per-entry phrase copies; candidate-array costs cancel out. Added an independent 2-versus-32-token allocation comparison with matching allocations eliminated. A deliberate array-only regression fails both provider cases while the phrase-length cases still pass.
- Low, patched: the private token-count parameter lacked a three-token partial-coverage assertion. Both providers now preserve the expected one-of-three score and exact-match metadata.
- Low, patched: semantic multi-token fuzzy behavior lacked the same precise positive/negative coverage as the local provider. Both providers now verify individual misspelled tokens match and a synthetic near-miss phrase remains excluded.
- Low, patched: semantic phrase-only evidence did not exercise cross-field token coverage. A three-token display-name/email/identifier test pins combined relevance, weighted metadata order and exact match modes. No new deferred findings.
