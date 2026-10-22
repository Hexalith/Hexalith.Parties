---
title: 'Complete five SDK and runtime qualification deferrals'
type: 'bugfix'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '3ace477aa61f6cd936bd9c06d99fd8fe59a205a8'
context: ['AGENTS.md', 'docs/development-guide.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Invalid page sizes report cursor errors; enabled Memories indexing acknowledges missing configuration without retry. Authentication publication, Tenants compatibility and shutdown need runtime evidence.

**Approach:** Complete DW-27, DW-84, DW-13, DW-158 and the unnumbered “Verify bounded Aspire shutdown before adding explicit teardown deadlines” entry from `spec-fix-ci-and-publish-release.md`. Implement, independently review, verify, commit and push to main. The requested five-item batch overrides single-goal scope.

## Boundaries & Constraints

**Always:** Reuse the selected SDK, projection reconciliation and owned infrastructure. Distinguish package/source and model/live evidence. Preserve cursor compatibility, cleanup ownership and privacy.

**Never:** Change dependencies, gitlinks, signoffs, deployment or persistence formats; weaken gates; qualify skips or mocks as runtime proof. Leave DW-116/DW-126 open.

## I/O & Edge-Case Matrix

| Scenario | Expected behavior | Failure handling |
|---|---|---|
| Invalid envelope PageSize with or without cursor | InvalidPage; gateway HTTP 400/query_invalid_page | No read or search; no input disclosure |
| Valid size with invalid cursor | Existing InvalidCursor behavior | Valid continuation and legacy paging preserved |
| Enabled Memories without CaseId | Projection remains retryable; no ingestion | Warning suppression stays bounded |
| Repair CaseId and redeliver | Existing canonical target indexes and converges | Erasure/removal checks remain authoritative |
| Owner publish with synthetic local credentials | Actual manifest retains external auth/audience and secret references | Reject literal credentials and local development endpoints |
| Tenants package build and runtime | Published verifier APIs resolve and service responds | Retain exact dependency/build/runtime evidence |
| Normal, canceled or failed topology startup | Owned workers stop before infrastructure removal | Bounded completion, retained ownership and reported cleanup failure |

</frozen-after-approval>

## Code Map

- EventStore 3.126.1 publishes InvalidPage and gateway mapping; adapt the retained DW-27 consumer patch to current code.
- `src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs` already returns `search-reconciliation-required` for false indexing results.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs` adopts JWT helpers; existing owner tests inspect models only. Read without editing upstream.
- Existing runtime-isolation receipts include Tenants readiness. Fixture StopAsync has a thirty-second token; disposal remains awaited. Cleanup retains control-plane ownership until quiescence.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.Parties/Queries/PartySdkQueryService.cs` and `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs`: adopt InvalidPage for size bounds. Adapt retained real-router HTTP regressions; preserve cursor/continuation/legacy controls.
- [x] `src/Hexalith.Parties/Search/PartyMemoryIndexEntrySearchIndexer.cs` and `tests/Hexalith.Parties.Tests/Search/PartyMemoryIndexEntrySearchIndexerTests.cs`: return false for enabled/missing CaseId. Prove handler retry after repair against persisted canonical state and stored mapping; preserve disabled/removal/cancellation and bounded warnings.
- [x] New `scripts/verify-eventstore-publish.py` and `scripts/test_verify_eventstore_publish.py`: emit actual owner manifests privately with poison credentials; validate workload auth, audiences and secret references. Mutated emitted copies must fail credential/development-endpoint checks. Clean output; never print credentials.
- [x] Build `references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj` in package-mode Release; record restored verifier identities. Rerun `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesMtlsTopologyTests.cs` plus authenticated gateway controls.
- [x] `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesTopologyCleanupTests.cs` and `HealthChecks/PartiesAspireTopologyFixture.cs`: trace installed Aspire/DCP shutdown bounds; qualify controlled failure and live cleanup under observation deadlines. Repair reproduced gaps without abandoning disposal or losing ownership.
- [x] `tests/README.md`, `docs/development-guide.md`, batch evidence and ledger: record exact commands/revisions; close only proven items.

**Acceptance Criteria:**

- Given invalid paging, when queried, then the real gateway distinguishes safe size/cursor failures.
- Given missing CaseId, when repaired and redelivered, then persisted-target indexing converges.
- Given poison inputs, when published locally, then actual manifests retain external auth without credentials.
- Given pinned packages, when Tenants builds and runs, then verifier compatibility and routing pass.
- Given successful/interrupted startup, when disposed, then bounded cleanup preserves ownership and ordering.

## Implementation Notes

Approved by the user on 2026-10-22 with “approve and continue”. The frozen intent is locked. The UI source-mapping MSB4044 blocker stays separate.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH-1 | medium | patch | Reproduced: an emitted secret parameter with a literal top-level value passes. Validate its parameter-input reference as well as secret/default metadata. |
| BH-2 | medium | patch | Reproduced accepted credentials in resource args, connectionString and URL user information. The new generic qualification claim requires those manifest locations to fail closed. |
| BH-3 | medium | patch | Reproduced accepted decimal-loopback URL in emitted content. The development-endpoint predicate does not normalize numeric IPv4 forms. |
| BH-4 | medium | patch | subprocess.run timeout terminates only its direct child; the newly introduced build/publisher runner has no owned descendant lifetime. Add bounded owned-group cleanup and a child regression. |
| BH-5 | medium | patch | All five current Python tests acquire manifests through a successful setUpClass context. Publication/parse/validation/second-variant failure cleanup and safe diagnostics have no execution control. |
| BH-6 | medium | patch | Tracked workflow reads only BMAD test discovery and test_platform_declaration.py. The new owner publisher suite is not invoked by a normal CI job; same root cause as VG-1. |
| BH-7 | medium | patch | Confirmed with current code and SDK StopResourceAsync: resources without enabled Stop commands are skipped, and an empty captured PID set exits despite pending Starting/Waiting resources. Add a pending-start barrier and delayed-start control; grouped with EH-1. |
| BH-8 | maybe-false | defer | Only DCP executable PIDs are observed. Normal live tests establish observed worker exits/listener cleanup, but do not settle whether a stopped Dapr CLI wrapper can leave an escaped daprd descendant. A producer-qualified orphan-child scenario or exact CLI/DCP descendant-stop proof would settle this unverified medium claim. |
| BH-9 | medium | patch | Post-disposal DockerOutputAsync uses only the test token. There is no local inspection deadline after the 60-second disposal observation, so a stalled Docker daemon can hang qualification. |
| BH-10 | medium | patch | The Bash/sleep process starts before try/finally; cancellation during PID/handle/identity setup skips cleanup, and the finally wait has no bound. Establish ownership cleanup immediately and bound/fallback termination. |
| VG-1 | medium | patch | Pre-verified broken-verification gap: no CI invocation discovers OwnerPublishTests, allowing poison-rejection regression to ship green. Grouped with BH-6. |
| EH-1 | medium | patch | Confirmed skipped Starting/Waiting resource can start after the final PID capture; caller token has not fenced background startup. Same pending-start root cause as BH-7. |
| PV-1 | medium | patch | Final live rerun reproduced loss of completed stop proof after an observer failure. Worker commands finished in 23.77 seconds, but the retry waited on an already stopped Unknown/disabled snapshot until cancellation. Retain completed per-application stops across automatic and explicit retries; preserve the existing deadline and exact-exit guard. |

## Verification

Build affected projects individually; run fresh xUnit assemblies with `-class`/`-method`. Require affected suites, real gateway/publisher controls and live mTLS/cleanup with zero qualifying skips. Record mapped Debug blockers separately without bypass. Run warning/diff guards, independent review and pinned commitlint; fetch/inspect divergence before authorized non-force main push.

## Parent Acceptance Audit — 2026-10-22

Read the complete staged baseline diff, including new files and normalized evidence. All six execution tasks and seven frozen matrix rows map to passing named controls in matrix-audit.json. Full service 2534/2534, live 22/22, publisher 5/5 and observer 1/1 results have zero qualifying skips. Exact package/verifier identities and owned-resource cleanup are recorded; dependencies/gitlinks and DW-116/DW-126 are unchanged. Mapped Debug qualification, independent review and final Git operations remain parent gates.

## Final Parent Verification — 2026-10-22

All five approved items are complete. Three independent reviewers produced twelve findings; eleven were patched and one unverified medium descendant-process claim was appended to the ledger. Parent verification reproduced and repaired a retry-state defect, including failed-stop siblings, without changing deadlines. Final checks: Release service 2534/2534, mapped Debug service 2534/2534, integration 28/28 (seven live host cases), publisher 10/10; zero qualifying skips. All four cleanup modes pass and all six final session roots/control planes and eighteen backing names are absent. Integration Debug still fails before fixture compilation at the pinned FrontComposer mapper MSB4044/AssetsFile gate. Exact commands, results, source hashes, review routes and limits are in final-verification.json. Dependencies/gitlinks and DW-116/DW-126 remain unchanged.
