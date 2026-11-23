---
title: 'Preserve authority across five deferred recovery boundaries'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '5340e6e29fbdfbbe1fecabe2f89faee2b1b2ca71'
context: ['AGENTS.md', 'references/Hexalith.AI.Tools/hexalith-llm-instructions.md', 'references/Hexalith.AI.Tools/hexalith-state-instructions.md', 'references/Hexalith.AI.Tools/hexalith-ux-instructions.md']
---

<frozen-after-approval reason="requested implementation and publication of five deferred items">

## Intent

Close five repository-local gaps in DW-50, DW-58, DW-82, DW-100 and DW-168. Preserve committed dependency selections. The work exercises deployed behavior only through owned disposable test resources. Existing production cutover, rollback and owner acceptance requirements remain in the ledger.

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Queries/PartySdkQueryService.ProcessingBuckets.cs` walks bucket keys before refusing revisionless complete reads. The cache can supply bucket metadata without an ETag. `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs` has query, export, cache and read-count fixtures.
- `tests/Hexalith.Parties.Tests/Projections/PartyLegacyBackfillProcessRuntimeTests.cs` hashes the manifest in the child and emits phase receipts. `scripts/verify-backfill-and-memories-deferrals.py` verifies retained receipts. `scripts/test_backfill_and_memories_integration.py` tests the verifier.
- `src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs` and `PartyMemoryIngestionIntentStore.cs` persist authority and terminal evidence. `tests/Hexalith.Parties.Tests/Search/PartyMemorySourceInventoryTests.cs` has serialized state, alternating producers and inventory-page fixtures.
- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor` and `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/EditMyProfilePageTests.cs` own the profile save and readback.

## Tasks & Acceptance

- [x] DW-50. Given cached bucket metadata without an ETag, when the full processing query or export runs, then fail before reading any bucket and return no payload. Keep existing inline compatibility and paged behavior. Exercise both affected entry points.
- [x] DW-58. Given six independently launched backfill phases per starting state, when the parent and retained report reader verify them, then each receipt must match the SHA-256 of the exact input it consumed. Emit the hash checked by the child. Add report reader cases for both valid starting states and a changed or missing digest.
- [x] DW-82. Given a previously unbound legacy workflow recovered through validated status during party cleanup, when terminal output is learned, then bind it to the current configured producer in the same conditional local write before DELETE. Use the exact active snapshot under the cleanup fence and refuse a concurrent different binding. A restart configured for B must preserve A's pending obligation, even with reused identifiers; recovery on A must still complete. Cover the status-only path separately from the existing source-inventory import test. Preserve older unbound records and existing authoritative inventory behavior.
- [x] DW-168. Given two current-producer workflows naming the same known case and unit and only one verified terminal, when bulk cleanup runs, then retain that unit, mapping and pending obligations regardless of workflow order. An unrelated terminal unit may progress. After the sibling reaches terminal status, verify absence and settle both obligations. Reconcile workflow evidence before deletion, including when status lookup or fact persistence fails for a known unit. Keep the change within the bulk cleanup entry point and test both inventory orders with serialized state.
- [x] DW-100. Given an accepted person or organization edit, when the first projection read is stale or reports earlier values as current, then keep the submitted/accepted values and the updating status. Confirm Saved only on a current read matching the immutable submitted details for the same profile. Honor erasure and profile identity changes immediately without claiming that they confirm the save. Preserve the current query-error status. Cover person, organization and matching-current confirmation.

## Implementation Notes

Use existing tests and helpers. The coordinator owns backlog notes, final checks and Git publication. SDK 11 and the writable test package directory are available at `/workspace/.hexalith/dotnet` and `/workspace/.hexalith/packages`. Build individual projects with `-c Release -m:1 --disable-build-servers --artifacts-path "$PWD/artifacts/j/deferred-five" -p:UseHexalithProjectReferences=false`. The coordinator runs the owned Dapr/Redis rehearsal after implementation. Adjust the code map if the implementation disproves a hypothesis.

## Review Triage Log

| Finding | Verdict | Disposition and evidence |
| --- | --- | --- |
| Older terminal intent lacks producer authority | High | Require matching status or authoritative inventory before deleting its stored canonical unit. |
| Historical absence can settle a newly terminal workflow | High | Verify current absence for active workflows. Preserve the receipt path for mappings that have no remaining active obligation. |
| The first observed unit is lost after a write failure and status expiry | Medium | Defer cross-pass recovery when both durable storage and subsequent workflow status are unavailable. The current pass protects the observed unit. |
| Shared unit receives repeated DELETE requests before observed absence | Medium | Share one scoped deletion result across terminal obligations and mappings in a pass. |
| Accepted state renders only after a pending readback | Medium | Render the accepted values before awaiting readback. Exercise an independently held read. |
| One-shot readback can leave updating status indefinitely | Medium | Defer a subsequent observation mechanism. The local change preserves accepted values after the first read. |
| A canceled submit can return accepted and restart reconciliation | High | Reuse the original token and check it after command acceptance. |
| Organization confirmation has no positive control | Medium | Test matching current details in both profile types. |
| A blank operation identity can match a blank receipt | Medium | Require a nonblank operation string and test malformed inputs whose hashes still match. |
| Deferral drops a validated legacy alias | High | Retain the alias and current producer before deferring deletion. Exercise status expiry before sibling recovery. |
| A later sibling can verify absence after the first reported failure | Medium | Share the first scoped result so a single pass has a consistent outcome. |
| Reconciliation can cancel a disposed source after late acceptance | High | Retain the save token through the full accepted operation and cover cancellation. |
| Earlier values do not independently verify freshness or field matching | Medium | Cover matching stale values and changes to fields besides the name. |
| The write-failure test already knows the canonical unit | Medium | Test an unbound workflow whose status first identifies the unit before persistence fails. |

| Concurrent settlement duplicates a pending legacy alias | High | The conditional write refuses an exact receipt collision. A dedicated test retains one settled receipt. |
| Unit-only inventory can attach another producer to an old workflow | High | Keep the workflow unbound until a matching workflow observation or terminal status supplies authority. |

## Verification

Run the focused gateway, search and ConsumerPortal test classes against the pinned packages. Run the Python integration evidence tests and the separate-process backfill in an owned Dapr/Redis environment. Record actual results and remaining deployment prerequisites.

The standalone Release build passed with zero warnings and errors on .NET 11.0.100 with the Aspire 17.0 bundle. The restored test dependencies were EventStore 5.4.0, Memories 4.1.0, and FrontComposer 6.0.1.

- The xUnit v3 gateway and Search selections passed 964 and 517 cases respectively. ConsumerPortal passed 113 cases and UI passed 465. There were zero failures, errors, and skips.
- The disposable Redis 7.4.7 and Dapr 1.19.2 rehearsal passed 11 cases. The maintained verifier matched all 12 independently launched workload receipts to the retained input bytes. A changed copy of the actual input was refused. The disposable resources were removed.
- `python3 -m unittest discover -s scripts -p 'test_*_integration.py'` passed 80 checks. The warning-override guard and the pinned commitlint CLI passed.
- `aspire run --apphost src/Hexalith.Parties.AppHost --isolated` reached connection setup and stopped with exit 2 because `/home/agent/.aspire` is read-only. The separate `python3 -m unittest discover -s scripts -p 'test_verify_eventstore_publish.py'` retry passed six tests and reported a fixture error from its EventStore `--publisher manifest` subprocess, which exited -6 with `Aspire.Hosting.DistributedApplicationException`.

The ledger retains the production qualifications and two follow-ups for continued profile observation and for recovering an output when both persistence and a later status lookup fail.
