---
title: 'Preserve authoritative evidence across deferred recovery paths'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
baseline_commit: 'b9e2df3904871a8d704a76ff880b399b5239162d'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent">

## Intent

**Problem.** A bounded profile readback can end before a second observation after transport failure. Processing fallback can lose its last verified inline snapshot or regress during overlapping reads. Direct Memories cleanup has outstanding recovery and coordination cases around terminal evidence and inventory imports.

**Approach.** Advance five local recovery findings from the deferred-work ledger and the latest recovery review. Preserve the evidence required to report current, degraded or cleaned outcomes according to what each operation actually observed.

## Boundaries & Constraints

**Always:** Keep profile saves nonblocking after acceptance, treat processing revisions as opaque, and scope Memories evidence by tenant, party, case and producer. Use disposable fixtures and retain existing cancellation, erasure and conditional-write behavior.

**Never:** Change deployed data, widen deletion scope, infer erasure from a transport response alone, or declare owner and deployment gates complete from local tests.

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor` — `ReconcileAsync` retries an unwitnessed response but an exception from the first readback bypasses the retry. Reuse `ApplyReadback` and the per-attempt deadline.
- `src/Hexalith.Parties/Queries/PartySdkQueryService.cs` and `PartySdkQueryService.ProcessingBuckets.cs` in the same folder — `ReadProcessingModelAsync` can cache a bucket header before bucket validation. Complete reads and paged reads separately validate data and revisions.
- `src/Hexalith.Parties/Queries/PartySdkLastKnownReadModelCache.cs` — read registrations protect against explicit eviction. Apply ordering to competing cache admissions without ordering opaque ETags or weakening erasure invalidation.
- `src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs` and its inventory partial — direct cleanup reads a snapshot from `BeginCleanupAsync`; bulk reconciliation can import obligations after this snapshot. Reuse the durable conditional state in `PartyMemoryIngestionIntentStore.cs`.
- `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs`, `tests/Hexalith.Parties.Tests/Search/PartyMemorySourceInventoryTests.cs`, and `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/EditMyProfilePageTests.cs` contain the existing fault and cancellation fixtures.

## Tasks & Acceptance

- [x] `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor` — allow the existing bounded retry after first-attempt readback failure. Given acceptance followed by a transient first read failure, when the next observation witnesses the submitted content, then confirm the save without resubmitting or overwriting newer edits. Cancellation ends observation.
- [x] `src/Hexalith.Parties/Queries/PartySdkQueryService.cs` — keep tentative bucket headers out of degraded fallback. Given verified inline history and a bucket header whose body cannot be verified, when the store later becomes unavailable, then the tentative header cannot replace verified fallback.
- [x] `src/Hexalith.Parties/Queries/PartySdkLastKnownReadModelCache.cs` — prevent an earlier overlapping processing observation from overwriting a later admitted one. Given two verified observations completing in reverse order, when fallback is served, then it reflects the later admitted observation with the erasure fence still enforced.
- [x] `src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs` — qualify recovery after a terminal-status persistence fault. Given a known shared output, a failed evidence write and a later unavailable workflow status, when cleanup retries, then known units remain protected until exact durable evidence can be recovered.
- [x] `src/Hexalith.Parties/Search/PartyMemoryCleanupService.Inventory.cs` and its direct-cleanup caller — qualify concurrent import and cleanup with persisted state. Given inventory importing an unresolved obligation after the first direct snapshot, when direct cleanup decides whether to delete or settle, then it cannot report completion based on the stale obligation set.
- [x] Add the controlled schedules to the existing test files and update `_bmad-output/implementation-artifacts/deferred-work.md` with exact results and remaining gates.

## Implementation Notes

- Profile reconciliation uses two bounded observations. Acceptance reopens the form and a confirmed observation preserves any newer draft.
- Processing admits verified inline snapshots in read-registration order. A complete verified bucket revision or authoritative absence can retire prior inline fallback.
- Direct cleanup persists terminal workflow bindings before deletion and uses the ingestion inventory conditional write before dispatch and before settlement. An import during an already-dispatched DELETE can prevent settlement.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Disposition |
| --- | --- | --- |
| Blind 1 | Medium | A delayed initial miss can evict later processing fallback. Make absence retirement generation-aware. |
| Blind 2 | High | Missing inline verification can retain older data after authoritative absence. Retire earlier fallback for both layouts. |
| Blind 3 | Medium | Conditional revision rejection removes unrelated later registrations. Scope rejection to its revision and the relevant read generations. |
| Blind 4 | False | Baseline versioned bucket headers also needed a registered cache write before a successful read. Capacity exhaustion already suppressed the bucket result. |
| Blind 5 | Medium | Revisionless inline reads bypassed explicit eviction after registration. Guard the complete result while retaining the legacy revisionless entry point. |
| Blind 6 | High | A newly resolved legacy alias can borrow a receipt before final snapshot confirmation. Validate the case snapshot inside alias adoption. |
| Blind 7 | Medium | Unit-only inventory can match a historical receipt without creating a new obligation. This pre-existing inventory reconciliation gap is recorded separately. |
| Blind 8 | Medium | The selected terminal observation can be lost when an unresolved sibling stops the pass. Persist the selected binding earlier. |
| Blind 9 | Medium | A different unit with a prior receipt leaves its active sibling unresolved. Preserve the existing scope of direct cleanup and record independent recovery of that sibling for follow-up. |
| Blind 10 | Medium | Current Running status does not prove every output in the case is independent. Preserve established typed partial cleanup without introducing a status-only exception. |
| Blind 11 | Medium | Imports after the pre-delete conditional check can race with dispatch. The final conditional check prevents stale completion. The exact store-to-HTTP boundary remains a follow-up. |
| Blind 12 | Medium | A repeat unchanged submit can interrupt observation and duplicate the accepted update. Ignore it while that readback is in progress. |
| Edge 1 | High | Inline verification misses can retain an older fallback. Apply generation-aware absence to inline revision verification. |
| Edge 2 | Medium | An older initial miss invalidates later observations. Check read order before key-scoped cache eviction. |
| Edge 3 | False | The baseline bucket path already fell back when a versioned read could not obtain a registration. |
| Edge 4 | Medium | Explicit eviction can race an unversioned inline response. Validate its registration before returning the result. |
| Edge 5 | Medium | A scoped pending response alone does not rule out later shared output. Remove the broad exception. |
| Edge 6 | Medium | Ignoring independently resolved sibling evidence can require later rediscovery. Preserve the selected target's scope and defer the different-unit receipt path. |
| Verification 1 | Medium | Existing erasure-race tests hold inline reads only. Exercise the eviction fence with same-revision bucketed results through history and export. |
| Verification 2 | Medium | No test establishes the scope of the pending-status allowance with a same-case peer. Remove the allowance or cover malformed scope. |
| Verification 3 | Medium | Four existing unavailable-status cases fail after the broader hold. Restore independently verified typed partial cleanup and verify all four cases. |

## Verification

Build the core and Consumer Portal test projects with the pinned .NET SDK. Run the focused Gateway, Search and profile test classes from the built test assemblies, then run the Python integration discovery, warning guard and Git whitespace checks. Validate the exact commit message with the pinned commitlint and verify the remote main SHA after pushing.

Both Release projects built with .NET SDK 11.0.100 with zero warnings or errors. Gateway passed 1,000, Search passed 158, and Consumer Portal passed 127 including all 47 profile cases. No targeted cases skipped. Python integration discovery passed 84. The warning guard and CRLF-aware whitespace check passed.

The legacy AppHost built successfully. Its Aspire backchannel was blocked by a read-only profile directory. Extra unfiltered Python discovery reported one EventStore owner-publisher setup error. A direct reproduction reported ASPIRE009 because automatic CLI bundle setup could not write to the same profile directory.

The supplemental owner lane passed all 14 tests with a writable Aspire CLI bundle, the pinned owner Builds catalog and Dapr CLI 1.18.0. Full Python discovery then passed all 131 tests.

After review, both Release projects rebuilt with zero warnings or errors. The focused review run passed 32 Gateway, 21 Search, and six profile cases, including all four existing unavailable-status controls. The unchanged repeat submit, generation-aware absence, bucketed and revisionless erasure fences, typed observation marker, and alias conditional write all have controlled regressions.

Final exact-source Release Gateway and Search namespace run passed all 2,098 cases. The full Consumer Portal assembly passed all 127 cases. Both runs reported zero failures and skips.
