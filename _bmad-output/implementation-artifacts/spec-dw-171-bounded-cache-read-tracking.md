---
title: 'DW-171 bounded last-known cache read tracking'
type: 'bugfix'
created: '2026-10-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every canonical read-model miss calls cache eviction, which retains a key in the singleton's unbounded generation dictionary. A stream of distinct missing party IDs grows memory indefinitely even though cached values have capacity and retention limits (DW-171).

**Approach:** Replace historical generations with bounded tracking of active reads. Keep exact per-key invalidation so evicted or ended reads cannot restore pre-erasure data, unrelated reads remain valid, and fresh reads can populate the cache. Release tracking on every query outcome and convenience store. Preserve tenant/slot isolation, existing fallback behavior, value retention/capacity, and the existing long-valued API signatures. At tracking capacity, reject additional stores safely; do not reuse read tokens or permit an untracked canonical read to bypass invalidation. Complete only DW-171.

</frozen-after-approval>

## Implementation Notes

Planning classification: no unresolved intent gaps, irreversible actions, domain persistence changes, dependency updates or consumer-facing contract changes. Small footprint: two host implementation files and focused service/cache tests, plus this spec, the ledger and sanitized evidence. Use the oneshot route. The host is not a public library package. Independent read-only investigation confirmed all production call sites and the concurrency design.

Code map: `src/Hexalith.Parties/Queries/PartySdkLastKnownReadModelCache.cs` owns cache synchronization, convenience stores and the unbounded dictionary. Replace the dictionary with a token-to-exact-key map capped at the configured maximum entries. Allocate unique positive long tokens under the same lock; zero is an untracked, always-rejected token. Fail closed at token exhaustion. Eviction removes cached values and all matching active tokens, retaining no record of inactive keys. Validate both token and key on conditional stores. Add idempotent `EndRead(long)` and release convenience-store tokens in finally. Keep existing `generation` parameter names for source compatibility.

`src/Hexalith.Parties/Queries/PartySdkQueryService.cs` has three canonical read scopes: index, detail and processing. Release each captured token in finally, covering success, missing data, errors, degraded fallback and cancellation. Keep erasure composition in `Extensions/PartiesServiceCollectionExtensions.cs` unchanged. `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs` supplies existing concurrency, key-isolation, cache-capacity and retention controls; extend its meaningful service tests or a separate focused test class without changing wire responses or source/dependency observations.

Verification must reproduce boundedness failures before the implementation, then cover all slots, saturation/recovery, ended and evicted tokens, old-token completion after a new read, cross-key/tenant isolation, churn, and controlled asynchronous erasure races. Query tests must establish release after repeated success, failure, cancellation, missing values and fallback with a capacity-one cache. Inspect retained metadata size directly where behavioral assertions cannot prove the memory bound. Build the individual service test project in Debug/source mode with warnings as errors, run focused tests and the full service assembly, and run the warning-override gate and whitespace checks. Keep exact commands and outcomes in sanitized evidence. Record the separate Aspire baseline outcome; do not broaden this fix into runtime repair or cross-instance cache invalidation (DW-172).

Implemented the bounded token map and idempotent cleanup in the two planned production files. Invalid tokens never authorize a store, and token exhaustion remains fail closed. A capacity-one cache exercises every service outcome except processing fallback, which needs two cached values for its required detail read and processing record. That case uses capacity two. The existing separate bounds for values and read registrations are documented on the constructor.

Failing-before proof: the new missing-key churn test retained 512 dictionary entries against capacity four with the original production implementation (one test, one expected failure). The final initial Debug/source build has zero warnings/errors; the focused service/cache class passes 88/88 and the complete service assembly passes 1061/1061, with zero failures/errors/skips. A new pending-task await initially triggered CA2007; explicit ConfigureAwait(true) corrected it without analyzer changes. Independent review is pending.

Runtime baseline: `aspire start --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --non-interactive` succeeded. Initial describe showed the identity provider starting; `aspire wait parties --status healthy --timeout 15 --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --non-interactive` succeeded after 9.5 seconds. `aspire stop` against the same AppHost succeeded before the first source build. The warning-override/nested-submodule gate passes. No orchestration configuration, dependency selection or gitlink changed.

Final verification after review corrections and CRLF formatting: the Debug/source build has zero warnings/errors, the focused class passes 93/93, and the full service assembly passes 1066/1066 with zero failures/errors/skips. Independent follow-up verified every correction and found no remaining concrete issue. No new deferred work. Exact commands, result summaries and source hashes are retained in `tests/dw-171-bounded-cache-read-tracking-2026-10-17/validation.json`. A CRLF-aware whitespace check passes; default Git whitespace checking flags the CRLF endings required by .editorconfig. The local Conventional Commit candidate passed the repository-pinned commitlint 21.2.2 with zero problems/warnings.

## Review Triage Log

- Low, rejected: eviction scans active registrations under the cache lock. The scan is bounded by the configured capacity (1024 by default); a sequential missing-key workload retains no active registrations after each read, and the maximum scan requires that many overlapping reads. A reverse index adds state and synchronization invariants for an unmeasured latency concern. Preserve the simple bounded design; this review did not demonstrate a user-facing regression.
- Low, patch: processing saturation lacked service-level coverage. Added deterministic saturation with a cached prerequisite detail, proving untracked processing values are discarded, empty degraded behavior is retained, and canonical reads recover after releasing capacity.
- Low, patch: saturation with existing cached values lacked assertions. Added all three slots with seeded and empty caches, checking cached projection identity and degraded metadata, no canonical replacement, and fresh canonical recovery.
- Low, patch: the historical-identifier test allowed up to four retained inactive identifiers. Tightened it to require zero dictionary entries after missing-key churn.
- Low, patch: capacity tests lacked coordinated concurrent admission. Added 32 gated readers against capacity eight, asserting exact positive-token count, uniqueness, rejected untracked stores, successful admitted stores and complete cleanup/recovery.
- Low, patch: convenience-store summaries implied guaranteed storage despite bounded admission. Documented skipped storage when tracking capacity or token space is exhausted on all three methods.
