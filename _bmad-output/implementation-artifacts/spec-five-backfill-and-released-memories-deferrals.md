---
title: Complete the backfill supervisor and qualify four Memories recovery checks
type: bugfix
created: 2026-11-22
status: done
route: oneshot
review_loop_iteration: 0
context: []
baseline_commit: 3468759a3dee5c8f4a9a07e33b4a4809d6d17fd6
---

<frozen-after-approval reason="requested five deferred work items">

## Intent

Make the existing DW-58 Redis/Dapr cross-process backfill rehearsal run when Linux omits the procfs task children file. Retain the exclusive reaper, bounded teardown and direct command exit status.

Qualify four open Memories ledger entries against the source revision of released Memories 4.1.0. Check same-URI content refresh, bounded historical discovery, purged workflow identity reuse, and per-unit authority across source changes. Use disposable providers and existing producer tests. Record which checks run, the package and source identities, and any remaining deployment or operator prerequisites.

</frozen-after-approval>

## Implementation Notes

Use the fast procfs children list when available. On a missing file, read the parent PID from the process stat record and consider only this supervisor's direct children. Exercise the retained process tests and original backfill parent on this host. Use the producer integration and nightly test assemblies at the v4.1.0 tag for the four Memories checks.

The base commit failed both selected adopted-child tests with `FileNotFoundError` on `/proc/self/task/<pid>/children`. The supervisor now checks the direct parent field in numeric procfs stat records when that file is absent. Cleanup checks the original deadline during the scan and drains exited children before scanning again. The regression covers exit statuses 0 and 7, adopted-exit reaping during the direct command, and teardown of a child that started a separate session. It exercises the missing-file path even when the host supplies the task children file.

## Verification

Built each affected test project separately in Release/package mode with SDK 11.0.100. All builds reported zero warnings and errors. Invoked the assemblies directly with `-failSkips` and checked the named results and counts in their XML.

| Ledger item | Executed test and result |
| --- | --- |
| DW-58 backfill rehearsal | All 36 `PackageTestProcessTests` passed, including two forced missing-file cases. The real provider suite passed all 11 selected cases across `PartyLegacyReadModelBackfillRuntimeTests` and `PartyLegacyBackfillProcessRuntimeTests` using EventStore 5.4.0, Redis 7.4.7 and Dapr 1.19.2. The original parent verified all six phases for each of the empty and existing canonical targets. All 12 child XML reports passed. |
| Same-URI refresh | Both `BothCompletionOrdersPreserveCurrentContentAndRetryableNewerRevision` cases passed against Redis Stack and FalkorDB. |
| Bounded historical discovery | All four selected `NaturalLanguageErasureIntegrationTests` passed. They covered exact ownership with missing legacy index, exact chunks and both namespaces with unrelated survivors, partial timeout refusal, and missing legacy tag refusal. |
| Purged workflow identity reuse | Both `PurgedActualHistoricalWorkflowCannotGainCompletenessFromReusedIdentity` nightly cases passed with real Dapr Workflow. Same-source and cross-source paths refused historical cleanup authority and preserved exact historical and survivor hashes. |
| Authority across source changes | Five `UnitOwnershipAuthorityIntegrationTests` cases passed. They covered issued annotation authority through deletion, purge and source change, three historical source/marker cases, and refusal of foreign keys, changed evidence and reused generations. |

Both the REST client and contracts 4.1.0 NuGet manifests identify Memories source `0856785c7c764a0dc305af058227d6485b65c697`, which matches the tested `v4.1.0` tag. The owner test assembly used the disposable copy's one-line `daprd` path override because the home directory is read-only. Producer sources, test assertions and package pins were unchanged. The build used the 17.0.0 DCP and dashboard package assets. Redis and FalkorDB containers and their owned Dapr processes were removed or reaped after the tests.

The full Parties `aspire run` baseline stopped before startup because the CLI could not write its first-use files to the read-only home. The selected provider and producer tests ran through their dedicated fixtures. The five ledger entries retain the outstanding operator and production requirements.

## Review Triage Log

- Medium. The procfs fallback could scan past the cleanup deadline and rescan for every reaped process. Checks now use the same absolute deadline and reap all ready children before another scan.
- Medium. The prior tests could miss the fallback on hosts with the task file. The regression now injects a missing-file response and asserts that both the adopted daemon and escaped child are reaped.
- Low. Recorded the backfill parent and child results in DW-58 with the deployed prerequisites.
- Low. Recorded the four exact-release Memories results and their evidence limits in the ledger.
