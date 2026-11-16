---
title: 'Qualify five deferred Memories boundaries across producer restart'
type: 'bugfix'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '762ea76d33af315a87b112abc6730c7ae21b2cb9'
context: ['AGENTS.md', 'tests/README.md', 'docs/memories-backed-party-search.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Current qualification recreates clients while Memories services remain running. Recovery across an actual producer workload restart remains unqualified.

**Approach:** Extend the private current-source runtime with observed workload restarts and persisted-state checks for five deferred entries. Review, verify, commit and push main as requested. All five support one producer-recovery goal, with separate evidence and remaining external gates.

## Boundaries & Constraints

**Always:** Restart only the session-owned Memories workload. Verify distinct process incarnations, unchanged backing resources, workflow state, authority generation and prepared identities. Use bounded phase acknowledgements, SDK persistence, private credentials and unconditional cleanup.

**Never:** Substitute client recreation or readiness for process replacement. Upgrade dependencies, add public control endpoints, mutate deployments, invent historical completeness/writer exclusion, delete retained implementations or certify published packages from source-host evidence. Keep unmet external gates open.

## I/O & Edge-Case Matrix

| Given / deferred entry | When | Then |
|---|---|---|
| DW-82: admitted work, empty mappings | Producer restarts before output | Inventory/authority survive; unknown-terminal obligations remain |
| DW-168: output-known work or accepted deletion | Producer restarts at both observed phases | Completion requires terminal evidence and physical absence; receipts/survivors persist |
| Same-URI refresh: settled revision 1 | Producer restarts before revision 2 | Same URI exposes changed content; stable identity and erasure of both outputs verify |
| Cross-source per-unit authority: retained original authority | Producer restarts before unqualified reuse | Foreign authority cannot grant completeness; original records/bytes survive |
| Bounded legacy discovery: unknown historical artifacts | Producer restarts before cleanup | Incomplete coverage refuses certification; explicit exact-unit fixture evidence permits bounded cleanup |

The three unnumbered entries originate respectively in `spec-five-memories-recovery-deferred-items.md`, `spec-five-snapshot-history-and-publish-deferrals.md` and `spec-five-memories-inventory-and-publication-deferred-items.md`.

</frozen-after-approval>

## Code Map

- `tests/Hexalith.Parties.IntegrationTests/Topology/PartyMemoryOwnedRuntimeTests.cs`: topology, private configuration, child and disposal; preserve the separate package-matched profile.
- `tests/Hexalith.Parties.Tests/Search/PartyMemoryRuntimeTests.cs`: revision transition and observed admission/output/deletion phases.
- Same Search directory: `PartyMemoryRuntimeConfiguration.cs`, `PartyMemoryUnitAuthorityRuntime.cs`: private configuration and exact authority/refusal helpers.
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/HealthEndpointE2ETests.cs`: existing ResourceCommands pattern.
- Ordinary Memories owner source: `UnitOwnershipAuthority.cs`, `SemanticKeyDiscovery.cs`, `SourceInventoryStore.cs` and `UnitOwnershipAuthorityIntegrationTests.cs` define existing contracts. Consume them without producer edits.

## Tasks & Acceptance

- [x] Wrapper/configuration: add private bounded phase coordination; verify original process exit, replacement identity/health and unchanged backing-resource custody.
- [x] Child runtime/authority helpers: implement all five rows using persisted observations, positive data before erasure, terminal receipts and byte-preserved survivors. Delay windows alone cannot qualify phase coverage.
- [x] Focused tests beside runtime helpers: cover foreign/replayed requests, failed restart, cancellation and evidence failure without abandoned children/resources. Keep each C# type in its own file.
- [x] Update `tests/README.md`, `docs/memories-backed-party-search.md`, the deferred ledger and a new dated receipt with exact identities, commands, outcomes, process observations, cleanup and external gates.
- [x] Prepare the verified implementation and evidence for independent review and the authorized main publication; the parent owns commitlint, submodule/divergence checks, commit/push and remote verification after review.

Given each matrix row, when its lane executes, then require nonzero passing coverage and actual process replacement. Given failed prerequisites or skipped tests, when reporting, then preserve their exact blockers separately from qualification.

## Implementation Notes

No new production defect was established. This addresses an unqualified process boundary; production closure still needs external inputs. Initial main and working tree were clean. Footprint: wrapper, configuration, runtime/authority helpers, focused tests and receipts; no public API. Push remains the terminal authorized action.

User approved this spec on 2026-11-15. Implementation handoff covers code, focused verification and documentation. The parent performs independent review and terminal Git publication after the local workflow; do not stage, commit, push or perform remote operations during implementation. Preserve the frozen intent.

Initial implementation verification completed across 2026-11-15/16 from one frozen prepared cohort: six observed producer replacements, wrapper/child 1/1 each, controlled coordination cancellation 1/1, consumer controls 51/51 and signal/cleanup controls 7/7; zero targeted skips. Both main/survivor API teardown receipts and owned topology disposal verify. The documented five-minute replay guard remained enabled at both active-ingestion replacements. Exact receipts: `tests/five-producer-restart-deferred-items-2026-11-15/`. Global runner SIGINT cleanup remains unqualified; original failed receipts and separate exact-owned reconciliation are retained. No producer defect or dependency change was introduced. Those implementation receipts precede the review fixes; use the final parent evidence below.

Parent acceptance audit normalized the terminal task into its implementation-handoff scope so the workflow can conduct independent review before publication. The parent owns independent review and the already-authorized main commit/push; no frozen intent changed.

## Spec Change Log

## Review Triage Log

All three layers completed before triage: blind hunter 11 findings; edge-case hunter none; verification-gap reviewer none. Each blind finding was classified individually before routing.

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| B1 | medium | patch | QuiesceIncompleteAsync returns when the current snapshot has no live PID, although an accepted or uncertain start can still publish one. WaitForPendingStartsAsync retains Starting/disabled-stop resources, so this cancellation window can exhaust generic disposal. Settle the already-requested start under an independent bound and prove pre-observation cancellation. |
| B2 | medium | patch | QualifyLostAcknowledgementAndDelayedCleanupAsync checks physical absence and empty intents for its delayed output but the final InspectAsync loop covers the two separate revision outputs. The resumed deletion's own terminal workflow, exact authority and retained receipt need independent final assertions. |
| B3 | low | patch | AssertPersistedPhaseAsync really asserts Running deletion, present unit and absent receipt, but discards that deletion object when serializing PersistedInterruption. Retain the already-observed facts for direct packet audit. |
| B4 | medium | patch | CrossSourceAsync creates the unknown historical artifacts after replacement. Real issued A/B bytes are captured beforehand, but that extra historical subcase cannot establish continuity through restart until seeded and captured before it. |
| B5 | medium | patch | CaptureBaselineAsync omits original provenance and source-inventory records. Post-restart self-comparison cannot detect their prior alteration; capture settled original metadata and compare it after replacement. |
| B6 | false | reject | BindPreparedLaunch subscribes BeforeResourceStartedEvent and calls VerifySelectedPreparationAsync on every start. The validator rehashes every receipt file and compares the complete recursive DLL/native/config inventory, so dependency drift refuses before replacement launch. Existing PreparedLaunchRechecksAndExecutesTheExactEntryPE and ChangedPreparationRefusesBeforeSelectingLaunchArguments cover that guard. |
| B7 | maybe-false | defer | Default ALC reuse is possible, but no additional reused dependency with divergent bytes on these owner call paths was identified. Three shared boundary assemblies are byte-checked and the prepared framework/cohort is bound. Trace every actually resolved dependency and demonstrate a reachable mismatch beyond those guards to settle the medium-unverified claim before introducing isolation. |
| B8 | medium | patch | Bounded legacy refusal retains remote hash bytes, but its callback exposes only Cleaned. Unlike the adjacent dedicated authority controls, it never rereads mapping and unsettled ingestion obligations during refusal. Assert those persisted records after both negative attempts. |
| B9 | false | reject | The sole caller creates one small synthetic survivor and one extra chunk; the executed exact inventory contains three keys. No caller supplies a 4,096-key fixture, and the internal defensive upper bound is not a generic supported-input promise. The 64-KiB protocol correctly refuses oversized private evidence; no reachable failure of this batch was shown. |
| B10 | low | patch | Existing Linux cleanup tests already require python3; the new signals additionally require Python/kernel pidfd APIs. Document and provide a harmless capability preflight. Extra runtime branches for an unshown unsupported CI host are unnecessary. |
| B11 | medium | patch | Child kill/reap precedes coordination cancellation and settlement in the same finally body. A process-operation exception can skip that work. Enclose coordination settlement and incomplete-replacement quiescence in unconditional cleanup and preserve both primary and cleanup failures. |

Patches keep the frozen intent and add no public surface. The one unverified loader claim is separately deferred; it does not establish a producer defect or invalidate the recorded exact current cohort.


The parent post-review runtime rerun reproduced a B2 publication race: after
physical deletion and successful SDK cleanup, the exact Dapr deletion workflow
still reported RUNNING at the new immediate terminal assertion. Five replacements
completed, and the failed child/wrapper both retained zero skips; owned disposal,
20 process incarnations, six backing containers and scratch absence independently
verify. The failed source hash and XML remain in a separate retained attempt.
The smallest correction waits under a bounded cancellation-aware deadline for
that exact workflow's Completed publication, preserving authority/receipt checks.
No RUNNING state is accepted as terminal success. The same implementation agent
applied this correction; the parent full rerun below verifies it.


Parent review fixes are verified on the final implementation. B1 settles pending
starts before exact-incarnation quiescence, including cancellation before a PID
appears. B11 runs every child/coordination cleanup step and retains all failures.
B2 verifies the delayed output's own terminal workflow, unchanged authority and
deletion receipt. B3 retains the independent running-deletion/presence/receipt
observations. B4/B5 capture historical bytes, provenance and both settled source
inventories before restart and compare them afterward. B8 retains SDK obligations
after both legacy refusals. B10 documents pidfd prerequisites and a harmless
preflight. The implementation agent's targeted review-fix and terminal-wait checks pass;
the parent independently rebuilds/prepares and passes 58 consumer, 10 signal/
cleanup, full wrapper/child 1/1 each and controlled cancellation 1/1, with zero
targeted skips. All six replacements, both replay timeouts, exact executed hashes,
both API teardowns and owned process/container/scratch absence are audited in
`tests/five-producer-restart-deferred-items-2026-11-15/review-final/`.
Both retained-default compile builds also pass with zero warnings/errors.
The earlier packet is preserved as historical evidence; external gates and the
global SIGINT limitation remain open. B6/B9 remain refuted; B7 is recorded as the
single medium-unverified follow-up in the deferred ledger. No intent changed.

## Verification

Use `run-with-temp`, keep disposable outputs beneath TMPDIR and retain results outside it. Inspect the Aspire baseline via its orchestration skill. Attempt pinned source preparation/individual Debug builds; preserve any NU1109/HXW010/HXW011 blocker before the documented Release fallback with explicit EventStore 5.1.0, Memories 4.0.0, FrontComposer 6.0.0 and separately bound current-source host.

Build the individual Parties.Tests and IntegrationTests projects. Prepare matching hosts with `scripts/prepare-aspire-runtime-hosts.sh --gateway-profile packaged --configuration Release --include-memory-qualification true`, supplying an artifacts path beneath TMPDIR; bind its prepared manifest. Execute the new opt-in restart wrapper and focused runtime/authority classes directly with `-parallelMode none -failSkips -result-xml`. Require five observed outcomes, zero targeted skips, persisted end-state and completed cleanup. Finish with `bash scripts/check-no-warning-override.sh`, `git diff --check` and pinned commitlint evidence.
