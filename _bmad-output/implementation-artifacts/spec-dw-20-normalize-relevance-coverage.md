---
title: 'DW-20 normalize full-phrase search relevance coverage'
type: 'bugfix'
created: '2026-10-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-20 records that both local search providers count their synthetic full-phrase candidate as an extra matched token. Coverage exceeds one and inflates multi-token relevance and ranking before the final score clamp.

**Approach:** Count only matched real query tokens in coverage, retaining the existing ordinal-distinct numerator and original token-count denominator. Keep phrase matching and strongest field scores, weights, metadata, fuzzy rules, normalization, filters, stable tie-breakers and paging. Preserve DW-19's once-per-query candidate preparation. Prove corrected scores and ranking across both providers, including repeated tokens and semantic structured fields; update existing assertions that freeze inflated scores.

</frozen-after-approval>

## Implementation Notes

- Selected the next small, locally actionable search follow-up after completed DW-19. Repository inspection and an independent investigation agree on the correction; no human-only intent gap remains. Scope is two private provider implementations, focused tests and work records. Everything is reversible; no persistence, migration, dependencies or external publication is involved.
- `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs` and `SemanticPartySearchProvider.cs`: after matching candidates, remove the joined phrase from `matchedTokens` only when token count exceeds one. Split tokens contain no separator, so this cannot remove a real token. Field-match collection and the .7 maximum + .2 average + .1 coverage formula remain intact.
- Reuse `tests/Hexalith.Parties.Tests/Search/SearchQueryCandidateTests.cs` and `LocalFuzzyPartySearchProviderTests.cs`; change only their inflated phrase-score expectations (.87 to .82, .69 to .64, semantic email .96 to .91). Add `SearchRelevanceCoverageTests.cs` for direct score, metadata and corrected order/page regressions. Preserve duplicate-token .95 and partial-token behavior.
- Build `tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj` in Debug with `-m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`. Invoke the built xUnit assembly with single-dash class filters, then run the full affected assembly. Retain sanitized results under `tests/dw-20-normalize-relevance-coverage-2026-10-17/` alongside this spec. Run the warning-override and whitespace guards.
- Baseline revision: `19744800488aa79d5c86c76dddc3d2942db24715`. Working tree started clean on main. The authoritative Hexalith guidance governs branch policy over the stale BMAD project-context branch recommendation.
- Implemented the private matched-token correction in both providers. Field matching, metadata and cached candidate preparation are unchanged; only inflated phrase relevance expectations were adjusted.
- The initial 19-case regression run against unchanged production reproduced 17 failures and two passing exact-match controls. Post-review coverage has 29 cases; the final focused search/performance run passes 109/109 and the full service assembly passes 1127/1127 without failures, errors, skips or unrun tests. Final Debug/source build has zero warnings/errors, and warning/whitespace guards pass.
- The exact Parties AppHost was started, inspected and stopped before source changes. `aspire wait parties --timeout 20` exited 17; security TLS readiness was unhealthy and services were Waiting. This is a separately recorded baseline limitation, not full-topology qualification. See the exact commands and observations in the evidence README and validation.json.
- Independent review and follow-up confirmed all four verification improvements below. No new deferred work. The local commit message passed repository-pinned commitlint 21.2.2; successful evidence is retained. No push.

## Review Triage Log

- Low, patched: precise single-token scores did not protect the new multi-token guard. Added exact/prefix/contains cases for both providers (1.0/.82/.64), which fail if a real single token is removed.
- Low, patched: identical numeric repeats did not distinguish ordinal and case-insensitive token deduplication. Both providers now assert ABC/abc full coverage and .82 prefix relevance.
- Low, patched: isolated structured-field phrase cases did not pin deduplication across simultaneous phrase hits. A display-name/email/identifier phrase test asserts .98 relevance and exact weighted metadata ordered 1.0/.9/.8.
- Low, patched: corrected tie paging only covered display-name fallback. A semantic SortName case reverses display-name order and asserts both pages under reversed input order. Independent follow-up found no remaining issues in these additions.
