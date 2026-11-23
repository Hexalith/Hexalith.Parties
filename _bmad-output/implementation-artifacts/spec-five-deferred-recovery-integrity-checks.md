---
title: 'Preserve recovery evidence across five deferred items'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'c9ab76595e870a6ac42cfd2c9fa308a44465b3a9'
context: ['AGENTS.md', 'docs/ci.md', 'references/Hexalith.AI.Tools/hexalith-state-instructions.md', 'references/Hexalith.AI.Tools/hexalith-ux-instructions.md']
---

<frozen-after-approval reason="requested implementation and publication of five deferred items">

## Intent

Preserve evidence of the selected revision, workflow and profile across recovery. Address five open Parties ledger entries through focused local fixes and regression checks.

## Boundaries & Constraints

Use the pinned dependency revisions, persisted formats and existing APIs. Use synthetic records in verification. Preserve the ledger's deployment, publication and owner prerequisites.

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Queries/PartySdkQueryService.cs` reads and caches processing state. `PartySdkQueryService.ProcessingBuckets.cs` rechecks processing revision. `PartySdkLastKnownReadModelCache.cs` owns cache eviction and read generation tracking.
- `scripts/verify-backfill-and-memories-deferrals.py` declares required xUnit cases. `scripts/test_backfill_and_memories_integration.py` checks qualification evidence. `PartyLegacyBackfillProcessRuntimeTests` already rejects five report defects.
- `src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs` reconciles party-wide and single-unit cleanup. `tests/Hexalith.Parties.Tests/Search/PartyMemorySourceInventoryTests.cs` exercises durable intent and mapping state through recreated services.
- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor` applies optional acceptance details and performs bounded readback. `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/EditMyProfilePageTests.cs` has held and queued readback fixtures.

## Tasks & Acceptance

- [x] DW-50. Given two deserialized snapshots with the same processing ETag, when verification rejects that revision and the store later fails, then history and export omit the rejected records. Preserve a separately cached revision and revisionless compatibility. Carry the ETag into cache insertion and test through the query service.
- [x] DW-58. Given the maintained runner checks workload report integrity, when any of the five existing xUnit negative cases is missing, then the lane cannot qualify. Add the exact theory rows and an independent Python omission check.
- [x] DW-82. Given incomplete inventory and a live untyped reservation whose submit acknowledgement did not arrive, when cleanup sees same-case candidates, then retain them until the reservation acquires evidence. Verify persisted outcomes and unrelated-case progress.
- [x] DW-168. Given a candidate selected by direct cleanup and another unresolved workflow that can publish to it, when direct cleanup is requested, then retain the evidence and defer deletion and settlement until the sibling is reconciled. Verify durable retry and preserve existing exact-absence reuse.
- [x] DW-100. Given acceptance returns older details for the same profile and projection readback stays inconclusive, when the form is shown, then retain submitted values and show updating or degraded status. Preserve matching details and immediately honor erasure or replacement. Test with queued readbacks.

## Implementation Notes

The processing fallback records the ETag alongside the cached value. Rejection revokes active reads for that key and evicts the rejected revision. Untagged compatibility entries still support exact-instance eviction.

Direct cleanup keeps newly terminal same-unit workflows pending through deletion and scoped absence verification. A workflow discovered to have a different output keeps its own obligation. A receipt for that different output cannot qualify a newly materialized unit. Profile reconciliation applies submitted fields to the same-profile acceptance details and preserves the response's read-only evidence.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Disposition |
| --- | --- | --- |
| Blind review found that a delayed rejected revision could overwrite its replacement | High | Invalidate active reads even when preserving a different cached revision. Verify history and export. |
| Blind review found that a store outage during verification leaves the first read cached | Medium | Record the existing availability behavior for a separate decision about fallback admission. |
| Blind review found stale absence reuse after a write or readback failure | High | Keep the pending terminal obligation until fresh scoped absence is observed and test a recreated service. |
| Blind review found that a sibling's separate output forces unnecessary deletion | Medium | Limit fresh deletion to the selected canonical output and retain the other obligation. |
| Blind review found that inventory reconciliation can add obligations after the snapshot | High | Track coordination across the direct and inventory operations separately. |
| Blind review found that optimistic details discard read-only acceptance fields | Medium | Apply the submitted fields to the accepted same-profile snapshot and verify the read-only witness. |
| Blind review found that the checked tasks lacked results and ledger links | Medium | Record final executed commands, outcomes and remaining deployment and owner gates. |
| Edge review found that a delayed read can invalidate the rejection | High | Revoke pending cache stores for that key while preserving the replacement. |
| Edge review found that a revisionless reinsertion can retain the exact rejected instance | Medium | Retain exact instance matching for entries that have no ETag. |
| Edge review found that durable sibling evidence can outlive failed deletion | High | Test historical receipt, failed fresh deletion and successful recovery across service recreation. |
| Edge review found a concurrent inventory import can fall outside the snapshot | High | The direct operation still starts from a durable snapshot. Track coordination with later imports separately. |
| Edge review found unnecessary deletion for a separate canonical output | Medium | Preserve exact absence for the selected output when the sibling resolves elsewhere. |
| Verification review found missing coverage for historical absence with a newly terminal sibling | High | Add an interrupted deletion and durable retry test with a previous scoped receipt. |
| Verification review found that the direct tests always have an acknowledged sibling | Medium | Verify that direct cleanup retains both a selected workflow alias and an unacknowledged reservation without deleting. |
| Verification review found no older read held across rejection | High | Test a delayed older read after a different revision becomes the fallback. |
| Verification review independently traced stale absence reuse to the retained update | High | Keep the same-unit workflow pending while deletion or its absence read is unsuccessful. |
| Verification review independently traced stale revision insertion after rejection | High | Confirm the older read's revoked token preserves the replacement on outage. |

## Verification

- `dotnet build Hexalith.Parties.Standalone.slnx -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true` passed with zero warnings and errors on .NET 11.0.100. The build selected the extracted Aspire 17.0.0 CLI bundle through `AspireCliBundlePath`.
- The Gateway and Search namespaces passed all 2,041 tests with zero failures and skips. The finalized race, profile and cleanup regression cases ran in the corresponding project test suites.
- The ConsumerPortal test assembly passed all 125 tests from its normal project output directory, with zero failures and skips.
- The maintained backfill selection passed all 16 cases against disposable Redis 7.4.7 and Dapr 1.19.2 with EventStore 5.4.0 and Memories 4.1.0. The verifier checked all 12 child workload receipts and the disposable resources were removed.
- `python3 -m unittest discover -s scripts -p 'test_*_integration.py'` passed all 83 tests. The warning-override guard, CRLF-aware whitespace check and pinned commitlint also passed.
- The legacy `aspire run --apphost src/Hexalith.Parties.AppHost --isolated --non-interactive` command built the AppHost, then exited 2 at connection setup because `/home/agent/.aspire` is read-only. This prevented legacy topology startup verification.

The five ledger entries retain their existing production and owner prerequisites.
