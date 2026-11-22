---
title: Repeat the five backfill and Memories release checks
type: bugfix
created: 2026-11-22
status: done
route: oneshot
review_loop_iteration: 0
context: []
baseline_commit: 884169d9bdf5fae2650c7369cc46e7d56c18f41f
---

<frozen-after-approval reason="requested five deferred items and publication on main">

## Intent

Make the five local qualification checks from the latest deferred-work batch reproducible in one invocation. Run the original DW-58 backfill parent and its workload children against disposable Redis and Dapr. Run the released Memories producer cases for same-URI refresh, bounded historical discovery, purged-workflow identity reuse, and authority across source changes. Record package and source identities, the exact passing cases, and evidence from each child. Record verified local progress against the five ledger items while retaining their existing operator prerequisites.

</frozen-after-approval>

## Implementation Notes

Use the runtime, integration and nightly projects from the existing release qualification. Resolve the Memories producer revision from both restored package manifests, check the exact test counts and retain the XML and child receipts. Use the migration runbook's disposable provider contract and keep build scratch separate from retained results.

The runner builds the existing Parties runtime, Memories integration and Memories nightly projects in Release/package mode. The two restored Memories manifests bind the source checkout to `0856785c7c764a0dc305af058227d6485b65c697`. The host has a read-only user directory, so the run uses the restored Aspire 17.0.0 DCP and dashboard assets and a scratch copy of the test fixture with an explicit `daprd` path. The Parties backfill ran on a Linux host where procfs omits the task children file.

## Review Triage Log

- Medium. The ledger needed a durable reference to the new run. Added an entry to each of the five tracked items and recorded the applicable owner prerequisites.
- Medium. The reported Git commit alone did not identify the build inputs. The summary now includes Parties worktree status, both validated Builds gitlinks, the runner hash and the restored test dependency manifests.
- Medium. The report needed the actual provider identities. It records Redis and Dapr versions from the backfill provider plus the Memories `daprd` version and binary hash.
- Medium. An interrupt could leave the selected command running. The runner forwards the signal to its owned process group. A direct SIGTERM control verified that the running child exited and was reaped.
- Medium. A missing scheduler or placement address could select the host defaults. Both addresses now explicitly identify loopback services.
- Medium. A later failure discarded earlier evidence. The summary now saves source and build provenance, records each passing lane and marks the whole batch complete only after the final lane.
- Medium. Duplicate passing theory rows could replace an expected case. The reader checks unique case names, the expected method counts and all xUnit counters. A duplicate-name control was refused.
- Low. The runbook now describes the pinned Aspire assets and provides placement and scheduler commands.

## Verification

All three Release/package builds passed with zero warnings and errors. The final run passed 24 selected cases with zero skips.

| Qualification | Passed |
| --- | ---: |
| EventStore 5.4.0 backfill with Redis 7.4.7 and Dapr 1.19.2 | 11 |
| Same-URI refresh publication | 2 |
| Bounded historical discovery | 4 |
| Cross-source per-unit authority | 5 |
| Purged-workflow identity reuse | 2 |

The runner verified all twelve child workload reports for the empty and existing canonical targets. Checks rejected a producer commit different from the restored package manifests, an incomplete report and duplicate theory rows. The existing Python integration discovery passed all 72 tests. Cleanup checks confirmed the recorded thirteen containers and three Dapr processes were gone. The ledger entries retain the deployment, historical inventory and writer-exclusion prerequisites.
