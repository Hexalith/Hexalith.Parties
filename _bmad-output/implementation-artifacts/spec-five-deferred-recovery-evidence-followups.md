---
title: 'Verify recovery evidence across five deferred entries'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '23f2dc1c4c93727d1437b2b93f5328617bbecb54'
context: ['AGENTS.md', 'references/Hexalith.AI.Tools/hexalith-llm-instructions.md', 'references/Hexalith.AI.Tools/hexalith-state-instructions.md', 'references/Hexalith.AI.Tools/hexalith-ux-instructions.md', 'docs/ci.md']
---

<frozen-after-approval reason="requested implementation and publication of five deferred items">

## Intent

Address five repository-local gaps in the latest recovery batch. Processing reads and backfill reports need to verify the revision or phase they claim. Memories cleanup needs to keep obligations through inconsistent inventory and restart. Accepted profile saves need a bounded second opportunity to confirm the projected values.

## Boundaries & Constraints

Keep the existing dependency identities and persisted formats. Exercise mutations on synthetic test resources. Retain the production and owner prerequisites in each ledger entry.

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Queries/PartySdkQueryService.ProcessingBuckets.cs` uses `VerifyProcessingRevisionAsync` for pages and complete bucketed reads. The inline complete branch serves both history and portability export. `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs` has sequential store and degraded-cache fixtures.
- `tests/Hexalith.Parties.Tests/Projections/PartyLegacyBackfillProcessRuntimeTests.cs` verifies the input in six workload processes per starting state. `scripts/verify-backfill-and-memories-deferrals.py` validates child XML and JSON reports. `scripts/test_backfill_and_memories_integration.py` builds synthetic reports.
- `src/Hexalith.Parties/Search/PartyMemoryCleanupService.Inventory.cs` parses fenced source inventory. `PartyMemoryCleanupService.cs` resolves workflows before issuing deletions. `tests/Hexalith.Parties.Tests/Search/PartyMemorySourceInventoryTests.cs` already exercises shared units, sibling order and conditional write failures on serialized state.
- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor` observes an accepted save once with the save lifetime token. `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/EditMyProfilePageTests.cs` has queued and held readback fixtures.

## Tasks & Acceptance

- [x] DW-50. Given complete processing reads or an export selected from a revisioned inline model, when the stored revision changes before completion, then fail without returning the old history. Reuse the revision verification used for pages and buckets. Keep the existing revisionless degraded inline path.
- [x] DW-58. Given the retained XML and JSON evidence of a workload phase, when the passing XML was taken from another phase or target, then reject the evidence. Emit the exact JSON receipt in the child test output with a stable prefix. Require one matching receipt in the passing test's XML output in the parent and the maintained Python verifier. Prove valid and substituted reports.
- [x] DW-82. Given a fenced source inventory row marked Admitted and terminal verified, when cleanup observes the contradictory row, then refuse it before it can clear a known workflow's terminal requirement. Prove that the unit and durable obligation survive and valid terminal evidence permits recovery.
- [x] DW-168. Given a workflow whose unit was observed but could not be persisted, when a recreated cleanup service can no longer resolve that workflow, then retain same-case candidate units and mappings until the association is known. A different case may make progress. Extend the serialized recovery regression through the unavailable retry and eventual terminal recovery. Use both deletion paths.
- [x] DW-100. Given an accepted save with stale or mismatched first readback, when one delayed read returns a current exact match, then confirm it. Keep the form usable during that second observation and preserve intervening edits for the same profile. Honor erasure and replacement immediately. Bound the observation, use the existing degraded status after exhaustion or query error, and prevent late results from overriding a newer submission or a disposed page. Test eventual confirmation, exhaustion and supersession or disposal.

## Implementation Notes

Use existing fixtures and APIs. Preserve each edited file's established line endings. The coordinator owns the ledger entries, final verification and Git operations.

The workload reports carry the exact receipt bytes in base64 because xUnit escapes raw JSON quotes in XML output. The cleanup guard applies to inventory workflows that still require terminal proof and legacy workflows with unknown outputs. Modern refreshes with independently retained revision evidence continue to make progress. The profile schedules one retry after 250 ms and caps each read at two seconds.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Disposition |
| --- | --- | --- |
| Rejected inline snapshot can survive in the fallback cache | High | Evict the selected instance atomically on observed revision change or disappearance. Exercise the following outage in history and export. |
| Live inline records can arrive without a revision | Medium | Existing inline compatibility accepts revisionless data. Retain that legacy contract. DW-50 still tracks authority during migration. |
| Rejecting a contradictory row discards its workflow identity | High | Preserve the scoped admitted obligation and guard its case while the terminal claim remains contradictory. |
| An earlier contradictory row could have cleared a persisted flag | High | Guard the case even when its known intent already has a canonical unit and a cleared flag. |
| A changed producer can leave same-case legacy mappings exposed | High | Apply case protection to unresolved foreign-authority inventory obligations. |
| The explicit single-unit helper can select an unresolved sibling | Medium | This pre-existing helper has test callers only. Its separate operation remains part of DW-168 follow-up. |
| Invalid local resubmission cancels a valid accepted observation | Medium | Cancel the earlier observation after successful validation and exercise erasure after an invalid draft. |
| The C# parent accepts different XML identities and counters from the retained verifier | Medium | Validate test identity and pass counters alongside the exact receipt in the parent. |
| Catalog proof can pass without the new cases in its required-case list | Low | The catalog's prior fixed qualification matrix remains valid. The final changed assemblies and direct rehearsal supply evidence for this batch. |
| Edge review found the same stale fallback after a revision mismatch | High | Exercise eviction on the next degraded request while preserving concurrent replacement. |
| Matching empty revision tags do not establish an authority change | Medium | Existing revisionless inline compatibility remains under DW-50. The changed path rejects contradictory revision-bearing reads. |
| Edge review found the unresolved producer-switch case | High | Apply case protection before skipping a foreign unresolved obligation. |
| Historical workflow-only intents can lack the terminal-evidence flag | High | Protect unresolved legacy workflows without retained modern refresh identity. |
| Nested XML output can hide another receipt from the Python reader | Medium | Use consistent output validation in both readers and exercise this report shape. |
| Removing the standalone parent's receipt comparison leaves existing negatives green | Medium | Exercise a matching and substituted report through the C# validation method without a provider. |
| The delayed-readback test can pass if the delay is removed | Medium | Verify the retry interval in the existing held-response test. |
| The optimistic profile already supplies the name used as the confirmation witness | Medium | Assert a read-only value that only the confirmed response supplies alongside the preserved draft. |
| Mapping progress does not prove unrelated terminal-intent progress | Medium | Add an unrelated terminal inventory entry without a mapping and verify its durable settlement. |

## Verification

Use the installed .NET 11.0.100 SDK at `/workspace/.hexalith/dotnet` and the existing NuGet cache at `/workspace/.hexalith/packages`. Build the affected test projects with `-c Release -m:1 --disable-build-servers -p:UseHexalithProjectReferences=false`. Run the gateway, Memories inventory and ConsumerPortal suites through the xUnit v3 assemblies. Run `python3 -m unittest discover -s scripts -p 'test_*_integration.py'`. Verify backfill child reports using the owned provider rehearsal when available.

The reviewed Standalone Release package build passed without warnings or errors on .NET 11.0.100 with the pinned Aspire 17.0.0 bundle. The restored packages include EventStore 5.4.0, Memories 4.1.0 and FrontComposer 6.0.1.

- The gateway and Search selections passed 2,026 tests. The full ConsumerPortal assembly passed 121 tests. Both had zero failures, errors and skips.
- The backfill rehearsal against disposable Redis 7.4.7 and Dapr 1.19.2 passed 16 tests. The maintained verifier confirmed all 12 workload receipts and refused a substituted passing report. Owned resource cleanup succeeded.
- Python integration discovery passed 82 tests. The warning-override guard, CRLF-aware diff check and pinned commitlint passed.
- `aspire run --apphost src/Hexalith.Parties.AppHost --isolated --non-interactive` reached connection setup and exited 2 because the legacy control socket needs a writable `/home/agent/.aspire`.

The production and owner prerequisites remain open in the deferred ledger.
