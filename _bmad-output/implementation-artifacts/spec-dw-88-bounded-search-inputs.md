---
title: 'DW-88: Bound Party search inputs'
type: 'bugfix'
created: '2026-10-21'
status: 'in-progress'
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
