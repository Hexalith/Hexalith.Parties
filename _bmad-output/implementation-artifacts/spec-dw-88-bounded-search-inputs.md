---
title: 'DW-88: Bound Party search inputs'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '6d9e3417f7abcd03e66a04de5084909c4b6c6115'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-88 records unbounded search strings flowing from payload parsing into cursor scope construction and tenant-index search work.

**Approach:** Reject oversized raw search strings through the existing InvalidEnvelope result before cursor or index evaluation. Use the established 512 UTF-16-unit string-input precedent, counting whitespace before trimming. Preserve bounded search, paging, null/empty query and unsupported legacy filter behavior. Document the query limit, add boundary and handler regressions, complete independent review, close DW-88 and push the verified change on main.

</frozen-after-approval>

## Implementation Notes

- Freeform continuation after DW-87; no sprint key. Initial main is clean and matches origin/main. No intent gaps, irreversible data action, new API, schema change or dependency update. Small parser guard, existing contract documentation and focused tests qualify for the oneshot route.
- Code map: src/Hexalith.Parties/Queries/PartyIndexProjectionQueryActor.cs, TryParseSearchPayload; src/Hexalith.Parties/Queries/PartySdkQueryService.cs, SearchPartiesAsync; tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs, existing service/envelope/store/codec helpers. The shared strict JSON parser, SDK codec and search providers remain authoritative.
- Bound decoded Query, Mode, CaseId and Type strings to 512 raw UTF-16 units before value checks. Do not trim or truncate to make oversized requests valid. Type keeps its strict allowlist; bounded unsupported modes and supplied CaseId keep UnsupportedQueryType. Payload paging remains ignored when envelope paging is supplied.
- Regression matrix: exact limit and limit+1, whitespace and supplementary Unicode, JSON escapes and mixed-case property names, legacy/envelope paging, preserved unsupported-value rejection, no store/provider/codec calls for oversized payloads, and valid maximum-sized cursor continuation. The payload parser still parses JSON; this scope limits downstream string processing rather than certifying transport-body size.
- Runtime baseline: isolated no-build Aspire start succeeded and parties became healthy. Resource inspection subsequently showed healthy parties/UI/sidecars and finished eventstore/tenants/admin hosts. The exact AppHost was stopped successfully before builds. This is startup evidence only, not full topology qualification.
- Validation: try the pinned source launcher, then use focused Debug project builds and direct xUnit v3 assembly runs under the documented fallback ladder if source orchestration is blocked. Retain commands/results, independent review triage and exact pinned commitlint evidence in tests/dw-88-bounded-search-inputs-2026-10-21/.

- Source launcher exit 2/HXR011: isolated Dapr home is unconfigured. No source runtime was launched. Focused Debug builds use selected packages as fallback without handwritten source selectors or dependency changes.
- Original-code regressions: 22 tests, 12 failures, no errors/skips/not-run. The existing Type allowlist already rejects oversized Type values; the new guard makes all raw string fields explicit. The initial test build caught CA1062 in a test parameter dereference, corrected before the successful zero-warning build.
- Concurrent work advanced EventStore, FrontComposer, Memories and Tenants checkouts after the initial clean-tree check. These external submodule changes are preserved and excluded from this task's staging.

- Concurrent /pushall committed the initial implementation, tests and spec in 805adb50f5b0d05da42ee6c11efc8f504090a682. Preserve that published history; finish as a follow-up.
- Both affected Debug builds pass with zero warnings/errors. Focused query/search tests pass 1139/1139 and complete Contracts.Tests passes 201/201. Complete service assembly passes 2157/2158 with no skips/errors/not-run. Its sole failure is unchanged FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph: expected Builds fff4a212180d8cd1c5c0759655a9bc4fb6963c45, actual concurrently committed 17d0c6ffec5fb3c8d8058e67cb85644460cb5256. Fitness source blob is identical at baseline/current (6b6e9029c7cba79678fc3782660b0de7064181a1). Do not weaken the gate or recertify dependency receipts in this input-validation change.

- Independent review found four concrete verification/documentation gaps. Patch all: use valid legacy paging in length rejection cases, assert exact-match metadata for accepted maximum queries, cover oversized query precedence over bounded unsupported mode/case values, and document bounded whitespace modes and validation precedence. The existing EnvelopePagingAloneGovernsAndLegacyPayloadPagingRemainsAvailableAsync already separately proves malformed legacy paging is ignored with envelope paging.
- Filesystem space briefly exhausted during a test-file write. Recovered that file byte-for-byte from 805adb50f5b0d05da42ee6c11efc8f504090a682, removed only this task's already-summarized temporary logs, and applied review corrections atomically after writable capacity returned. No user files or shared caches were cleaned.

## Review Triage Log

- Medium, patch: malformed ignored payload paging confounded envelope rejection cases. Use valid paging in the length matrix; retain the existing independent positive malformed-paging test.
- Medium, patch: repeated-character maximum query still prefix-matched if truncated. Assert exact-match metadata on both pages for ASCII and supplementary Unicode.
- Low, patch: mixed invalid query/unsupported-filter precedence was uncovered. Add all four Mode/CaseId and legacy/envelope paging combinations; require InvalidEnvelope and zero store/provider/codec calls.
- Low, patch: mode documentation omitted accepted bounded whitespace and implied oversized modes returned UnsupportedQueryType. State whitespace support and structural/length validation precedence in both maintained documents.

- Final reviewed Debug build passes with zero warnings/errors. Complete service rerun passes 2161/2162, including all 34 DW-88 cases; the same unchanged Builds receipt mismatch is the sole failure. Complete Contracts.Tests remains 201/201. Independent follow-up confirms all four findings resolved, no concrete scoped regression and no new deferred work. Warning/submodule and whitespace guards pass. Pinned commitlint 21.2.2 validates the exact full follow-up candidate with zero problems/warnings; successful output is retained.
