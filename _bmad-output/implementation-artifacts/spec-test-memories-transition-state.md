---
title: 'Test Memories transition-state restoration at capacity'
type: 'chore'
created: '2026-09-07'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: false
baseline_revision: '7e9c2c387ee84b4d5c96c27f4cf613e13ff2c9e2'
baseline_commit: '1cdcbe37d39693b3535e1c038d714510adfed00f'
context:
  - '{project-root}/.bmad-loop/runs/20260907-080828-c3f0/bundles/test-memories-transition-state/intent.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** The Memories case-ingestion counter tests cover JSON restoration and bounded LRU eviction independently, but do not prove that eviction still respects a refreshed workflow after a 256-entry state is serialized and restored. They also leave the immediately preceding persisted shape—sequence watermarks without an explicit workflow-order field—unverified at the capacity boundary.

**Approach:** Add focused server-unit regression scenarios that cross the real JSON serialize/deserialize boundary before applying the 257th workflow, covering both the current persisted shape and the predecessor shape whose order must be reconstructed.

## Boundaries & Constraints

**Always:** Work in the root-declared `references/Hexalith.Memories` submodule; exercise `CaseIngestionCounterLogic` through `CaseIngestionCounterState` serialized and deserialized with the existing web JSON conventions; use exactly 256 tracked workflows before the triggering transition; assert both the retained refreshed workflow and the correct evicted workflow; preserve the current tests and deterministic, Docker-free unit-test lane.

**Never:** Change production transition logic or persisted-state contracts unless a new regression test first demonstrates that the existing implementation is wrong; rely only on in-memory state without a JSON round trip; initialize nested submodules; edit `_bmad-output/implementation-artifacts/deferred-work.md` or any other deferred-work ledger.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Current persisted LRU | 256 workflows, with `workflow-1` refreshed to sequence 2, serialized and restored with explicit workflow order | Applying `workflow-257:1` evicts `workflow-2`, retains `workflow-1`, records the expected least-to-most-recent order, and rejects delayed `workflow-1:1` without changing counts | A wrong eviction or replay application fails the focused assertions |
| Predecessor state migration | Serialized state with 256 `AppliedTransitionSequences`, `LastTransitionId` identifying refreshed `workflow-1:2`, and no `AppliedTransitionWorkflowOrder` property | Restoration plus `workflow-257:1` reconstructs the queue, evicts `workflow-2`, retains `workflow-1`, emits explicit order, and rejects delayed `workflow-1:1` | Missing or incorrect migration reconstruction fails the focused assertions |

</intent-contract>

## Code Map

- `references/Hexalith.Memories/tests/Hexalith.Memories.Server.Tests/Actors/CaseIngestionCounterLogicTests.cs` -- existing JSON round-trip, 256-entry bound, and refreshed-workflow eviction tests; extend this file with the two missing persistence-boundary scenarios and reuse `JsonSerializerOptions.Web` plus Shouldly conventions.
- `references/Hexalith.Memories/src/Hexalith.Memories.Server/Actors/CaseIngestionCounterState.cs` -- read-only persisted shape: nullable sequence-watermark dictionary and nullable least-to-most-recent workflow-order array.
- `references/Hexalith.Memories/src/Hexalith.Memories.Server/Actors/CaseIngestionCounterLogic.cs` -- read-only behavior under test: `CreateWorkflowOrderSnapshot` accepts predecessor state, `CreateAppliedSequenceSnapshot` refreshes the workflow named by `LastTransitionId`, and `TrimAppliedSequences` evicts the oldest workflow above 256 entries.
- `references/Hexalith.Memories/tests/Hexalith.Memories.Server.Tests/Hexalith.Memories.Server.Tests.csproj` -- Docker-free xUnit v3/Shouldly test project and focused validation target.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- read-only orchestration ledger; the caller explicitly owns resolution recording.

## Tasks & Acceptance

**Execution:**

- [x] `references/Hexalith.Memories/tests/Hexalith.Memories.Server.Tests/Actors/CaseIngestionCounterLogicTests.cs` -- add current-shape restore-at-capacity coverage and predecessor-shape migration-at-capacity coverage, using real JSON serialization/deserialization and asserting eviction, persisted order, counters, and delayed-transition idempotency.

**Acceptance Criteria:**

- Given a full 256-workflow state in which `workflow-1` has been refreshed and its explicit LRU order has crossed a JSON serialize/deserialize boundary, when `workflow-257:1` is applied, then `workflow-2` is evicted, `workflow-1` remains at its refreshed watermark, the order is persisted correctly, and delayed `workflow-1:1` is rejected without changing counts.
- Given a serialized predecessor state with 256 sequence watermarks, `LastTransitionId` set to `workflow-1:2`, and no explicit workflow-order property, when it is restored and `workflow-257:1` is applied, then the migration path reconstructs recency, evicts `workflow-2` rather than refreshed `workflow-1`, emits a 256-entry explicit order, and rejects delayed `workflow-1:1` without changing counts.

## Spec Change Log

## Review Triage Log

### 2026-09-07 — Review pass

- verdicts: 7 findings — high 0, medium 0, low 1, false 6, maybe-false 0
- findings:
  - `[false]` `[reject]` The current-shape test does not replay evicted `workflow-2:1` — the bounded 256-workflow ledger intentionally provides a finite retention horizon; the bundle requires proof that the refreshed workflow is not evicted, and the test replays that retained workflow to prove the stated delayed-transition protection.
  - `[false]` `[reject]` The predecessor test should use an immutable historical JSON fixture — the anonymous payload serializes exactly the predecessor fields with camel-case web conventions while intentionally omitting `AppliedTransitionWorkflowOrder`; the compatibility behavior under test is restoration of that absent field, not byte-for-byte fixture provenance.
  - `[false]` `[reject]` The predecessor test assumes an order that the historical schema did not persist — the predecessor implementation explicitly removed and reinserted dictionary keys and documented dictionary insertion order as its LRU queue, so JSON member order plus the persisted `LastTransitionId` makes `workflow-2` the expected eviction for this fixture.
  - `[false]` `[reject]` The tests miss the required actor persistence boundary — the bundle explicitly locates the work in counter-state persisted-LRU tests and asks for behavior after serialize/restore; both tests perform that real JSON boundary, while the repository does not override Dapr actor JSON defaults and Dapr's web defaults use the same relevant camel-case, case-insensitive conventions.
  - `[low]` `[reject]` Malformed, duplicate, missing, or stale explicit-order entries remain untested — this is a real pre-existing normalization-test opportunity, but it concerns corrupted or partial newer state rather than either requested persisted shape; the negligible everyday risk does not justify broadening this focused bundle with multiple additional cases.
  - `[false]` `[reject]` A defensible intent reading requires state-manager persistence and actor reactivation — the referenced bundle disambiguates the invocation by asking specifically for counter-state tests after serialize/restore; “a restored actor” describes the avoided consequence, not a required live-Dapr test surface.
  - `[false]` `[reject]` The predecessor intent leaves full reconstructed order ambiguous — the predecessor implementation history establishes dictionary insertion order as the compact LRU contract, and `LastTransitionId` is explicitly the final recency fact; the test's exact order follows those two persisted facts.

## Design Notes

The predecessor payload should be produced without defining another C# type: serialize an anonymous object containing the counter fields and ordered sequence-watermark dictionary while intentionally omitting `AppliedTransitionWorkflowOrder`, then deserialize it as `CaseIngestionCounterState`. Insert `workflow-1` first but identify `workflow-1:2` as `LastTransitionId`; this proves restoration moves that persisted recency fact to the most-recent position before the new workflow forces eviction.

## Verification

**Commands:**

- `dotnet build tests/Hexalith.Memories.Server.Tests/Hexalith.Memories.Server.Tests.csproj --configuration Release -warnaserror` (from `references/Hexalith.Memories`) -- expected: the server test project and dependencies compile with zero warnings or errors.
- `dotnet tests/Hexalith.Memories.Server.Tests/bin/Release/net10.0/Hexalith.Memories.Server.Tests.dll -class Hexalith.Memories.Server.Tests.Actors.CaseIngestionCounterLogicTests` (from `references/Hexalith.Memories`) -- expected: every focused counter-logic test passes, including both new restoration scenarios.

## Auto Run Result

Status: done

Summary: Added JSON restoration-at-capacity regression coverage for the current explicit LRU-order state and the immediate predecessor watermark-only state. Both scenarios prove the refreshed workflow survives the 257th insertion, the actual oldest workflow is evicted, explicit order is correct, counters remain correct, and a delayed transition for the refreshed workflow stays idempotent.

Files changed:

- `references/Hexalith.Memories/tests/Hexalith.Memories.Server.Tests/Actors/CaseIngestionCounterLogicTests.cs` — adds the two focused 256-workflow serialization/restoration tests.
- `_bmad-output/implementation-artifacts/spec-test-memories-transition-state.md` — records the implementation contract, review triage, verification, and completion evidence.

Review findings breakdown: 0 patches applied; 0 items deferred; 7 findings rejected. Evicted-workflow replay was rejected because the ledger has an intentional finite horizon; an immutable predecessor fixture was unnecessary because the generated payload exactly represents the historical shape; the unordered-dictionary concern was disproved by the predecessor implementation's explicit insertion-order LRU contract; two actor-boundary concerns were rejected because the bundle requests counter-state serialization/restoration rather than live Dapr persistence; malformed explicit-order coverage was rejected as a low-value pre-existing expansion; and predecessor-order ambiguity was disproved by the historical dictionary-order contract plus `LastTransitionId` recency.

Follow-up review recommendation: false. Patched findings: high 0, medium 0, low 0.

Verification performed:

- Release build of `Hexalith.Memories.Server.Tests`: succeeded with 0 warnings and 0 errors.
- Focused `CaseIngestionCounterLogicTests` class: 21 passed, 0 failed, 0 skipped, 0 not run.
- Exact current-shape restoration method: 1 passed, 0 failed, 0 skipped, 0 not run.
- Exact predecessor-shape migration method: 1 passed, 0 failed, 0 skipped, 0 not run.
- `git diff --check`: passed with no whitespace errors.

Residual risks: The bounded ledger intentionally cannot deduplicate workflows after they are evicted from the 256-entry horizon; this bundle verifies the requested invariant that a recently refreshed workflow is not selected for eviction after restoration.
