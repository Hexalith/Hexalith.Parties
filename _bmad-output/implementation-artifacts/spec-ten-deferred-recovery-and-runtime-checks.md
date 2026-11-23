---
title: Correct retry and runtime behavior for ten deferred items
type: bugfix
created: 2026-11-23
status: done
route: dispatch
review_loop_iteration: 0
context: []
baseline_commit: cb4acf25f0e51091ac3c6ac5f7804f2089dd947c
---

<frozen-after-approval reason="requested batch of ten deferred items for publication on main">

## Intent

Correct consumer recovery, UI composition and degraded-response behavior for DW-50, DW-58, DW-82, DW-98, DW-100 and DW-168. Qualify four unnumbered Memories deferrals covering same-URI refresh, purged historical identity reuse, exact-unit authority across source changes, and concurrent older-producer admission. Exercise the consumer's published dependency versions and use the package-matched producer for runtime evidence. Preserve the distinction between repository behavior and owner-operated migration, writer exclusion, production configuration and rollback.

## Boundaries and constraints

Preserve the committed product dependency selections. Pulling all direct repositories supplies the current upstream source for inspection. Updating release pins or running migrations in a deployment requires its own compatibility or operator evidence. Preserve ambiguous remote admissions and existing fenced producer bindings across retries.

</frozen-after-approval>

## Code map

- `PartyDetailSdkProjectionHandler.RecordUnresolvedProcessingAsync` chooses an inline write before the store performs its optimistic update. Concurrent generation publication and canonical removal can invalidate that choice.
- `PartyMemoryIngestionIntentStore` imports observed inventory. `PartyMemoryCleanupService` uses the durable producer binding on restart. `PartyMemoryIndexingService` reserves an intent before the read-only predecessor lookup and then submits it.
- `ProjectionFreshnessServiceCollectionExtensions`, `PartiesUiServiceCollectionExtensions`, and the UI `Program.cs` wire the exported and standalone hosts. `DegradedResponseMiddleware` owns the two response headers.
- `scripts/verify-backfill-and-memories-deferrals.py` selects owner tests. `scripts/verify-released-catalog-results.py` and the existing report-reader tests demonstrate xUnit case-level verification.

## Tasks and acceptance

- [x] In the processing projection, validate each newly read state before writing an unresolved inline audit. Given a concurrent rebuild/cutover or disappeared coordinated slot, when an audit retries, then the competing state remains intact and replay can recover. Cover persisted contents and ordinary compatible inline behavior.
- [x] In the ingestion intent store, bind a legacy producerless intent to the first authoritative matched inventory when importing its facts. Given consumer restart with producer B after observing A, when cleanup resumes, then B cannot confirm A's unit absence. Cover subsequent recovery on A and preserve historical evidence rules.
- [x] In indexing, preserve retryability when a read-only refresh prerequisite fails before any submission. Given a failed preflight then redelivery, when the prerequisite succeeds, then one refresh is submitted and the canonical mapping survives. Once submission starts, preserve lost-acknowledgement recovery.
- [x] Have embedded and standalone UI registrations honor the same FrontComposer opt-in. Given true, false or absent configuration, when resolving the registered module, then both entry points select equivalent services and retain fallback defaults.
- [x] Correct degraded-response header handling when downstream has started a 5xx response. Given a real started response, when control returns or downstream throws, then middleware preserves the original outcome without mutating read-only headers. Preserve successful degraded response headers. Use the existing middleware test project.
- [x] In the backfill/Memories runner include both historical competing-admission scenarios and the available cross-source authority negative control. Require the actual theory variants for refresh, historical identity reuse and cross-source authority. Verify assembly and collection results consistently. Include regression tests for a substituted theory value, missing scenario and contradictory report. Update the maintained invocation guide.
- [x] Verify each affected path. Record the ten outcomes and remaining prerequisites in the ledger and this specification. The coordinator owns environment setup, package/runtime verification, ledger updates and publication. The implementation agent owns product and runner code, regression tests, and the invocation guide. It may adjust the code map if examination disproves a hypothesis.

## Verification

Run the relevant individual .NET test projects with the committed package catalog, the report-reader controls and the disposable provider/producer runner. Obtain case counts from the reports. The publisher verifies the exact commit candidate with the pinned commitlint tooling and inspects the remote main revision after push.

## Implementation notes


The package-mode standalone solution builds with SDK 11.0.100. Verification uses the committed Builds and EventStore references. The disposable rehearsal uses Redis 7.4.7, Dapr 1.19.2, and Memories 4.1.0 producer revision `0856785c7c764a0dc305af058227d6485b65c697`.

## Review triage

| Finding | Decision and regression coverage |
| --- | --- |
| A worker can stop during either read before the refresh POST. | High. Keep the initial reservation durably unsubmitted. Claim submission conditionally after both reads. A concurrent redelivery and a reopened generation exercise the boundary. Existing ambiguous admissions retain their original meaning. |
| Unit-only inventory can clear a pending workflow's terminal-evidence requirement. | High. Merge the observations in both orders, preserve the workflow obligation, and clear the prior non-submission marker when authoritative producer facts arrive. |
| A processing retry can lose its companion detail slot. | High. Re-read both slots on each retry and report a missing slot as rebuild-required. The released SDK only offers write and delete participants in a batch. The existing unresolved-audit contract requires a processing-only write, so independent detail removal after the last read still depends on coordinated writers. |
| Generation routing and checkpoint advancement could mask each other in the race test. | Medium. Exercise a competing generation whose checkpoint still allows the unresolved event. Check that the winning metadata and bucket bytes survive. |
| Existing bucket selection performed event validation twice. | Medium. Route existing bucket generations before validating inline delivery. |
| The outer result could mask a missing slot. | Medium. Propagate the rebuild reason from the racing delivery. |
| Malformed XML can hide a failed test outside its collection. | Medium. Reject misplaced rows as well as inconsistent counters. |
| Removing producer cases or an entire lane could still leave a passing summary. | Medium. Check the final lane set, check its case identities, and independently test omitted authority and admission controls. |
| CI discovery did not select the reader regressions. | Medium. The test module now matches the existing integration discovery pattern. The invocation guide includes a focused command. |


## Verification results

- The standalone Release solution built with its committed dependency selections, zero warnings and zero errors. The legacy EventStore 4.0.4 and Memories 3.0.11 compatibility build also passed.
- The maintained provider runner passed 29 cases and validated all twelve child-workload reports. The six groups produced 11 backfill, two refresh-order, four bounded-discovery, six authority, two purged-identity and four concurrent-admission results. The disposable provider and its containers were stopped.
- The focused health and coordination suites passed all 76 cases. The Python suite passed all 124 tests, including negative report controls selected by CI.
- The legacy AppHost started in a disposable SDK container with a private writable user profile. The startup check observed HTTP 200 from Parties `/alive` and HTTP 204 from its Dapr sidecar. Platform owns route-catalog activation and application readiness for the seven-server environment.
- The released catalog passed 1,100 core cases, 466 projection cases and 465 UI cases. There were zero errors, failures, skips or unexecuted cases in these selections. The ten ledger entries record repository qualification and retain their external prerequisites.
