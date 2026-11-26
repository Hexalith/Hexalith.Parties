---
title: 'Preserve recovery evidence across processing, backfill, Memories and profile saves'
type: 'bugfix'
created: '2026-11-26'
status: 'done'
baseline_commit: '3eb2aee3884604d1c36b06e82b427c1789740ca8'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent">

## Intent

Advance the five requested deferred items through their remaining reproducible repository gaps. Preserve the evidence needed to retire stale processing fallback, verify migration snapshots, settle Memories obligations and announce accepted profile saves.

## Boundaries & Constraints

1. **Prerequisites.** Use the retained SDK projection, cursor, Memories inventory and FrontComposer feedback contracts. Qualify released EventStore 5.4.0, Memories 4.1.0 and FrontComposer 6.0.1 in package mode. Deployment and historical completeness still require owner evidence.
2. **Touched repositories.** Parties only. Processing history is an I19 class (a) read model. Memories intents and mappings are class (b) ledgers that survive replay.
3. **Rollback.** Reverse the Parties patch at fixed dependency identities. Use baseline runs of the affected test classes and retain the existing cursor, compatibility export, actor data, receipt and UI fallback paths.
4. **Validation.** Run the Gateway, Memories, backfill and Consumer Portal xUnit assemblies/classes and the Python verifier regressions. The migration lane uses owned Redis/Dapr with both canonical starting states. The profile coverage exercises rendered accessibility semantics.
5. **Non-goals.** Deployed migration, deletion, writer drain, route switch-back, dependency changes, and approval of production acceptance are out of scope.
6. **Parity.** Check complete ordered processing, erasure cache fences, history and snapshot hashes across processes, producer-scoped receipts and surviving remote units, and one polite status in both UI compositions. No baseline path is retired.

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Queries/PartySdkQueryService.ProcessingBuckets.cs` verifies paged results and already uses the cache read registration.
- `tests/Hexalith.Parties.Tests/Projections/PartyLegacyBackfillProcessRuntimeTests.cs` owns the phase receipts. `scripts/verify-backfill-and-memories-deferrals.py` checks retained bytes.
- `src/Hexalith.Parties/Search/PartyMemoryCleanupService.Inventory.cs` knows the producer row state. The intent store conditionally retains discoveries. The direct cleanup service already persists independently resolved sibling evidence.
- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor` renders loading and save announcements. Its sibling `FreshnessStatus.razor` demonstrates the qualified feedback component selection.

## Tasks & Acceptance

- [x] DW-50. Update `PartySdkQueryService.ProcessingBuckets.cs` and `PartySdkQueryHandlerTests.cs`. Given verified inline fallback, a revision-verified first page covering the whole bucket generation retires it. Partial and continuation pages retain it.
- [x] DW-58. Update the workload test, Python verifier and `scripts/test_backfill_and_memories_integration.py`. Given receipt layers that still agree, a missing, changed, or inconsistently observed survivor, canonical or generation snapshot prevents qualification.
- [x] DW-82. Update inventory discovery, its intent-store contract and `PartyMemorySourceInventoryTests.cs`. Given empty mappings and a historical receipt, a current completed unit-only row retains or reopens an active obligation. A subsequent scoped absence can settle it.
- [x] DW-168. Update direct cleanup and the existing different-unit sibling test. Given an independently terminal sibling with a prior receipt, the selected cleanup persists the sibling binding. A new client can retry it after status becomes unavailable and requires new absence evidence.
- [x] DW-100. Update `EditMyProfilePage.razor` and `EditMyProfilePageTests.cs`. Given shared feedback registration, loading and save use the actual FrontComposer live region. Without registration, retain equivalent polite markup and a single announcement source.
- [x] Record results and remaining owner gates in `deferred-work.md`.

## Implementation Notes

- The processing page retires fallback after complete first-page coverage and revision readback. Continuations and incomplete pages preserve the last verified fallback.
- Phase reports bind the snapshots each workload created or checked. The parent compares every observation and the retained report reader repeats the checks on disk.
- Rediscovered unit receipts retain stable attempt identities and advance an optional cleanup revision. Conditional updates holding an older revision cannot settle the new obligation.
- Profile loading and saving share status selection. The edit route uses the FrontComposer component when feedback is registered and the existing polite markup otherwise.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Disposition |
| --- | --- | --- |
| Blind 1. Reopening loses an inventory-adopted workflow alias. | Medium | Preserve the existing workflow binding when reopening the same receipt. |
| Blind 2. An active rediscovery leaves an older settlement snapshot valid. | High | Advance the cleanup revision on a completed unit-only observation of active work. |
| Blind 3. A full inventory can strand a unit already represented by a scoped historical receipt. | Medium | Reuse the scoped receipt when creating a slot would exceed capacity. |
| Blind 4. The first save mounts an already populated live region. | Medium | Existing edit-page behavior across both implementations. A persistent site for announcements across the freshness transition remains a follow-up. |
| Blind 5. A download-only service can select the shared announce path. | Medium | Inspect feedback registration without activating download services. |
| Blind 6. Complete-page revision failure lacks warmed-fallback coverage. | Medium | Verify a failed recheck preserves inline history for a public degraded read. |
| Blind 7. The generation snapshot can name another interruption process. | Medium | Match the snapshot process to the interrupt receipt in both verification layers. |
| Blind 8. The large aggregate's canonical snapshot excludes the shared index. | Medium | Current stage and verify distinguish absent and existing targets by typed state. An exact retained index byte and revision comparison across processes is separate migration evidence. |
| Blind 9. The log directory is local to this checkout. | Low | Preserve local links and add reproducible commands to the verification record. |
| Edge 1. A concurrent remover can settle an unchanged active intent after rediscovery. | High | Same active revision race as Blind 2. Check under the intent-store conditional write. |
| Edge 2. Reopening can drop a workflow alias whose status is gone. | Medium | Same receipt identity issue as Blind 1. Exercise unit-to-workflow adoption across pages. |
| Verification 1. No complete rejected bucket page exercises subsequent degraded history. | Medium | Same verification gap as Blind 6. |
| Verification 2. The existing test never settles an adopted unit slot containing a workflow ID. | Medium | Same identity issue as Blind 1. Exercise the persisted alias after rediscovery. |

## Verification

- Package-mode Release builds selected EventStore 5.4.0, Memories 4.1.0, FrontComposer 6.0.1 and Commons 3.0.1. The isolated rollback comparison passed 1,093 core, 47 profile and 12 Python cases. The first forward run passed 1,241 core, 129 Consumer Portal and 14 Python cases with zero skips.
- Baseline and forward Redis 7.4.7 and Dapr 1.19.1 rehearsals each passed 18 cases and confirmed owned resource cleanup. Forward retained reports verified all 12 exact snapshot receipt sets. One package-mode Chromium profile save passed with FrontComposer feedback enabled, Playwright 1.65.1, Node 26.13.1 and npm 11.20.0.
- The default retained AppHost launch (`aspire start --non-interactive --isolated --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`) exited 2 on NU1109. Commons.ServiceDefaults 3.0.4 requires Microsoft.Extensions.Http.Resilience and Microsoft.Extensions.ServiceDiscovery 11.0.1. The default central catalog selects 11.0.0 for both.
- The warning-override guard and CRLF-aware diff check passed. [Local test logs and receipts](../../TestResults/dw-50-58-82-100-168-2026-11-26/)

- Applying the new product regression tests to the baseline reproduced four core failures and two shared profile failures. Neither lane reported skipped tests.
- Build the affected projects with `dotnet build -c Release -m:1 --disable-build-servers -p:UseHexalithProjectReferences=false -p:HexalithSourceMappingMode=package -p:MinVerVersionOverride=1.0.0` inside a `run-with-temp` workspace. Run the core and Consumer Portal assemblies directly with `-class` or `-method` and `-failSkips`. Run the independent checks with `python3 -m unittest discover -s scripts -p test_backfill_and_memories_integration.py`.
- The provider command uses [the owned runtime harness](tests/five-snapshot-history-and-publish-2026-11-11/run-owned-backfill.py) with the built Runtime.IntegrationTests assembly and a new output directory. Set `PARTIES_BACKFILL_WORKLOAD_EVIDENCE` to an absolute subdirectory of that output so the independent reader can verify the saved phase reports. The browser command uses the Chromium case matching `bound Consumer saves editable profile details` in `tests/e2e/specs/consumer-portal-routes.spec.ts`.

- Review checks cover warming and retaining fallback through an unavailable revision readback, preserving a workflow alias across split inventory pages, rejecting a racing exact settlement, and reopening an exact scoped receipt at capacity. Cleanup revisions advance only for an active obligation already bound to the observing producer. Both original unbound-intent controls passed with their persisted record assertions unchanged.
- Final Release builds passed without warnings. The exact reviewed patch passed 1,245 core cases (1,211 in the main lane and 34 cleanup-service cases), 130 Consumer Portal cases, and 15 independent verifier controls. The final Chromium save route also passed. [Reviewed package checks](../../TestResults/dw-50-58-82-100-168-2026-11-26/final-recheck/) · [Chromium result](../../TestResults/dw-50-58-82-100-168-2026-11-26/browser-final/junit.xml)

- The reviewed owned-provider lane passed 18/18. The independent reader verified six snapshot-bound workloads for each canonical starting state, including the matching interruption receipt. Both final rehearsals confirmed termination of the owned test process and Dapr sidecar and removal of the owned Redis container. [Snapshot verification](../../TestResults/dw-50-58-82-100-168-2026-11-26/final-recheck/snapshot-verification.json)
