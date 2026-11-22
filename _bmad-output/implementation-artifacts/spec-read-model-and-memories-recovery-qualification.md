---
title: Read-model and Memories recovery qualification
type: bugfix
created: 2026-11-22
status: done
route: dispatch
review_loop_iteration: 0
context: []
baseline_commit: ef6685da4d20b92502f515ea19ba0dcf0343f6dc
---

<frozen-after-approval reason="approved five follow-ups">

## Intent

Qualify processing-history paging and migration rollback, source inventory recovery, delayed cleanup after restart, and historical workflow admission under concurrency. Use the current Parties package catalog and disposable providers. Correct confirmed defects and preserve pending production prerequisites.

## Boundaries & Constraints

Exercise public consumer behavior and persisted results. Keep package versions, producer history, and product routing decisions intact. Use the released Memories package's matching source revision when running the producer. A local rehearsal qualifies only the exercised paths.

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Queries/PartySdkQueryService.ProcessingBuckets.cs` and `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs` implement complete, paged and export reads. Reuse existing coverage validation and cursor protection.
- `tests/Hexalith.Parties.Tests/Projections/PartyLegacyReadModelBackfillRuntimeTests.cs` runs two migration scenarios against real Dapr and Redis. Existing recovery tests also exercise interrupted staging and restart across buckets.
- `tests/Hexalith.Parties.Tests/Search/PartyMemoryRuntimeTests.cs` exercises a live producer with durable local stores. Existing legacy and delayed-ingestion branches supply fixtures and callbacks.
- `tests/README.md` documents the package-matched producer wrapper. The owner test `ActualOlderProducerBetweenAbsentReadAndAdmissionCannotGainModernAuthority` already interleaves creation with fresh admission, using real Redis and Dapr.

## Tasks & Acceptance

- [x] DW-50. Given a bucketed model, paged reads, complete reads and compatibility exports return the same ordered records and values. All reject corrupt inline coverage, including a missing sequence. Fix the complete/export path if needed and exercise existing rebuild recovery.
- [x] DW-58. Given a completed cutover, use fresh clients to read retained actor detail and index, compare them with canonical migrated data and re-read SDK detail, processing and index after fallback. Exercise existing-target and absent-target scenarios with persisted state.
- [x] DW-82. Given a source with remote artifacts and no local intent or mapping, inventory discovery retains obligations through incomplete historical coverage and only permits completion when scoped absence is proven. Exercise the released producer.
- [x] DW-168. Given delayed ingestion/deletion and a restarted consumer, first observed cleanup success requires terminal inventory and physical absence. Retain evidence from the released producer.
- [x] Concurrent historical creation. Run the owner's existing deterministic ordinary and annotation cases. Require both to execute and preserve the older unit bytes while refusing new cleanup authority. Fix producer admission only if a race is demonstrated.

## Implementation Notes

The November 21 ledger already records the 5.4.0 EventStore package and a November 15 deterministic producer race. Reuse those test paths. Record new outcomes and exact limitations in the spec. The coordinator will handle infrastructure-heavy validation, package-matched producer setup, and the owner race run. Implementation handoff owns Parties code/tests for the first four items and runs the focused package-mode unit checks.

## Verification

Build the individual Release projects with `UseHexalithProjectReferences=false` and invoke their built xUnit assemblies using exact class or method selectors and `-failSkips`. Run the owned backfill provider and package-matched Memories wrapper. Preserve test counts and runtime prerequisites for any blocked lane.

## Review and implementation

The complete-read path previously returned inline records before checking sequence coverage. It now uses the same validator as paging. Validation also rejects bucket metadata without its generation identifier. A legacy checkpoint can advance through events outside the retained records, including erasure, so it is preserved independently. The package tests cover swapped interior records and a missing sequence through all three read paths and compare the ordered values of 80 records across bucket boundaries.

The provider rehearsal reads the retained actor manifest, partition, detail and sequence after SDK cutover. It compares business data, erasure and restriction state across the two representations, accounts for event time versus legacy application time, and re-reads the SDK values and ETags after fallback.

Review found a timing dependency between the initial remote-only discovery and the first real deletion. The rehearsal now interrupts the request before transmission and inspects the durable intent using a recreated client. The next client performs the real deletion. It records the inventory response used by cleanup and checks the settled receipt, unit-specific absence and the producer's available physical counters. The incomplete-history case retries after the known unit settles.

## Qualification results

The standalone Release/package solution built with SDK 11.0.100 with zero errors and warnings. The catalog resolved EventStore 5.4.0 and Memories 4.1.0. All 1,091 focused core tests, 466 selected projection tests and eight real-provider rollback cases passed without skips. The projection suite uses the maintained selection for this SDK, which excludes the historical inline-only SDK control.

The original cross-process parent failed when its supervisor read `/proc/self/task/<pid>/children`, which this kernel omits. A temporary external driver used the parent-generated input and the original assembly’s seed, interrupt, rebuild, stage, commit and verify entry points. It ran them in six distinct processes per target against real Redis 7.4.7 and Dapr 1.19.2. All twelve child XML results passed, and their tenant, operation, phase and immutable input hash matched. This qualifies the workload phases on this host. The original supervisor still requires the Linux procfs facility.

The ordinary and annotation admission race tests passed against the Memories 4.1.0 producer source at `0856785c7c764a0dc305af058227d6485b65c697`. Both used real Dapr workflow history and a fresh Redis connection for the post-admission checks.

The package-matched Memories runtime passed its consumer test and its owning topology test. Two sources started with remote units and empty local mappings and intents. After interrupting the first deletion request and recreating the consumer, both known units reached the deleted state with scoped physical absence. Cleanup retained the source with incomplete historical coverage and closed the source with complete coverage. In the delayed-ingestion case, the first successful cleanup after restart observed terminal inventory and scoped absence and persisted the settled receipt. All seven focused terminal-wait tests also passed. These observations cover the disposable runtime. Historical reconciliation, production writer exclusion and deployed erasure remain owner and operator responsibilities.
