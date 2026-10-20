---
title: 'DW-79: Parallelize detail rebuild snapshot reads'
type: 'refactor'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'b38fcdb7200f0ed4a46627b460caf84d0fb30499'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-79 records two independent state-store reads awaited sequentially in `PartyDetailSdkProjectionHandler.PrepareRebuildAsync`, adding their latencies during rebuild preparation.

**Approach:** Start the detail and processing reads before awaiting their joint completion. Preserve request validation, full-history folding, keys, store, caller token, per-slot ETag concurrency and the two-operation rebuild plan. Verify overlap without timing benchmarks, both completion orders, failure/cancellation behavior and existing persisted rebuild behavior. Limit runtime changes to rebuild preparation; live projection/recovery sequencing and platform persistence remain outside this work. Complete the deferred entry, commit and push on main as requested.

</frozen-after-approval>

## Implementation Notes

- Selected DW-79 after the completed DW-77/DW-78 sequence. Clean main baseline recorded above; this is freeform deferred work, not a sprint story.
- No intent gaps or irreversible data operations. Footprint: the existing handler, a focused test class, this spec, DW-79 ledger resolution and bounded validation evidence. No new production API or dependencies.
- Reuse existing `PartySdkProjectionHandlerTests` rebuild ETag, payload and persisted convergence coverage. Add controlled incomplete-task tests in `PartyDetailRebuildReadTests.cs`; each C# file keeps one type.
- The two reads do not form an atomic snapshot; their individual ETags continue to guard the later commit. Await both started tasks on asynchronous faults/cancellation before returning a failure.
- The retained Aspire AppHost started with existing assets in isolation. `aspire wait parties --timeout 15 --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --non-interactive` exited 17: security was Unhealthy and dependent services Waiting. The exact owned AppHost stopped successfully. No full-topology qualification is claimed.
- Both read tasks start before `Task.WhenAll`; results are retrieved asynchronously after joint completion. Runtime changes are confined to `PrepareRebuildAsync`.
- Fourteen focused cases cover overlap, both completion orders, absent/existing/mixed slot ETags, exact store/key/token forwarding, faults with a pending sibling, cancellation during read invocation, mixed fault/cancellation, dual faults and pre-cancellation. Preparation performs no writes. Existing persisted rebuild/convergence cases remain green.
- Task.WhenAll preserves faults over cancellation when both occur. For dual faults, an original read exception propagates; no particular exception order is promised. Caller cancellation still flows through the supplied token.
- Review strengthened cancellation tests by cancelling inside the second read callback before the first await, avoiding a queued-continuation race. A deliberate early-cancellation mutation fails both cancellation cases. The original handler fails 13/14 focused cases. Both probes restore implementation bytes exactly and finish with a successful Debug build.
- Final Debug build: zero warnings/errors. Complete affected projection assembly: 399 passed, zero failures/errors/skips/not-run, including all fourteen focused cases. Repository warning/submodule guard, CRLF-aware whitespace check and pinned commitlint pass. Bounded commands/results and code hashes are in `tests/dw-79-parallel-rebuild-reads-2026-10-20/validation.json`.
- Local source-mode launcher `dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json --output json` exits 2/HXR011 without HEXALITH_DAPR_HOME. With existing isolated `/tmp/story-1-6-builds-toolchain`, it exits 3/HXP020: parties/domain mapped source project unavailable; no resources start. Focused Debug/package dependency validation is recorded separately and does not certify source-mode or Release/full-topology qualification. No dependencies, gitlinks or launcher configuration changed.
- Independent reviewer verified the cancellation and mixed-failure corrections. No new deferred work.

## Review Triage Log

- Finding 1 — false for the registered runtime: a synchronous second GetAsync throw could bypass the join, but the registered `DaprReadModelStore.GetAsync` is async and captures exceptions in its task. Registration resolves that implementation; no throwing decorator is registered in Parties. A hypothetical alternative implementation does not justify a production guard here.
- Finding 2 — medium, patched: immediate completion checks after cancelling an asynchronously continued task could miss an early return. Cancellation now happens inside the processing-read callback before preparation first awaits. Both cases deterministically reject a cancellation-aware early-return mutation; reviewer follow-up confirms the correction.
- Finding 3 — low, rejected as redundant coverage: later asynchronous fault timing exercises the same join as the deterministic pre-fault/pending-sibling cases. Those cases already require both reads to start, the sibling to finish, and the exact error to propagate; adding scheduler synchronization would exceed a simple correction without exposing a different implementation branch.
- Finding 4 — low, patched: added fault/cancellation and dual-fault combinations and documented their observed behavior in implementation notes. Both slots are requested, preparation performs no writes, faults dominate cancellation and dual-fault ordering remains unspecified. Reviewer follow-up confirms the coverage.
