---
title: 'Qualify five deferred process boundaries for Memories erasure'
type: 'bugfix'
created: '2026-11-16'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'd4a71068bc1366ba67699aa9ec68cf24c0645c71'
context: ['AGENTS.md', 'tests/README.md', 'docs/memories-backed-party-search.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Four deferred Memories erasure scenarios have real Redis/Dapr evidence but recreate adapters or hosts within one process. The direct-owner helper also verifies only three shared dependency assemblies, leaving actual dependency reuse beyond them unqualified.

**Approach:** Address five existing ledger entries as one process-continuity qualification goal: direct-owner dependency reuse (B7), legacy NL migration/both namespaces, NL retry payload/history erasure, purged historical identity reuse, and concurrent historical creation. Add observed process replacement and independently retained persisted-state evidence; fix only defects demonstrated by these controls. Review, verify, commit and push owning changes to main before recording parent gitlinks, as requested.

## Boundaries & Constraints

**Always:** Preserve fresh ingestion, tracked lost acknowledgements, permanent fences, exact tenant/unit scope and unrelated survivors. Bind evidence to executed assembly bytes, distinct process start identities and unchanged backing-resource custody. Use bounded authenticated private coordination, cancellation and unconditional owned cleanup. Keep each C# type in its own file.

**Never:** Substitute readiness, client recreation or same-process IHost replacement for process continuity. Replace real workflow purge/concurrency with manually seeded artifacts, add public control endpoints, upgrade dependencies, initialize nested submodules, mutate deployments or certify production completeness/writer exclusion. Preserve completed algorithm evidence. Package publication/adoption and external owner gates remain separate; this batch cannot mark those full entries done.

## I/O & Edge-Case Matrix

| Given | When | Then |
|---|---|---|
| B7: exact prepared owner and current three guards | Trace real owner calls and exercise a valid divergent-byte dependency in a disposable process | Record loaded paths/hashes; reproduce or refute reachable reuse; any reproduced mismatch refuses before owner mutation |
| Valid legacy NL and unrelated unit | Replace the worker across migration/erasure | Both victim namespaces remain absent; fences and survivor bytes persist; unknown ownership still refuses |
| Content-bearing legacy retry history and modern retry work | Replace the actual worker at observed activity boundaries, then erase | Scoped history, payloads and derived victim output disappear; late work cannot restore them; unrelated work progresses |
| Actual historical chunks and purged workflow | Replace producer before identity reuse | Missing exact-unit authority cannot become completeness; all retained historical/survivor hashes match |
| Actual absent-history observation | Older producer completes before ordinary/annotation admission across replacement | Record the ordered two-producer interleaving; no false modern authority or successful incomplete-erasure claim |

</frozen-after-approval>

## Code Map

- Parties `tests/Hexalith.Parties.Tests/Search/PartyMemoryOwnerSourceRuntime.cs`: WithServerAsync checks Contracts, QueryCursorCodec and Redis bytes; Store also passes already-loaded OptionsWrapper to the owner constructor. Trace Microsoft.Extensions.Options first; do not assume divergence or changed behavior.
- Parties `tests/Hexalith.Parties.IntegrationTests/Topology/PartyMemoryProducerRestartRuntime.cs` and `PartyMemoryOwnedRuntimeTests.cs`, plus `tests/PackageTests.Shared/PartyMemoryProducerRestartProtocol.cs`: reuse identity, prepared-launch, bounded acknowledgement and incomplete-start cleanup patterns; retain the existing six-phase lane.
- Memories owner `tests/Hexalith.Memories.IntegrationTests/Ingestion/RetryWorkflowErasureIntegrationTests.cs`: reuse PurgedActualHistoricalWorkflowCannotGainCompletenessFromReusedIdentity, ActualOlderProducerBetweenAbsentReadAndAdmissionCannotGainModernAuthority and OriginalSameNameRawRetrySuspendedInEachContentActivityTerminatesAfterWorkerUpgradeAndPurgesHistory. Existing workers are IHosts in one test process.
- Same owner project `Ingestion/NaturalLanguageErasureIntegrationTests.cs`: migration/erasure and retry controls currently recreate Redis connections. Reuse actual migrator, registry, activities and exact-byte survivor assertions.
- Same owner project `Fixtures/DaprStateSidecarFixture.cs` and existing historical workflow types: retain authenticated owned backing resources while private worker test processes are replaced. No new production transport is needed.

## Tasks & Acceptance

**Execution:**

- [x] Parties owner helper and new adjacent `PartyMemoryOwnerDependencyTests.cs`: capture actually loaded dependency identities; demonstrate a reachable divergent-byte reuse beyond existing guards in isolated disposable children. Add the smallest justified pre-mutation guard if reproduced, with matching-byte success and mismatch refusal controls. If refuted, retain the trace and exact attempted counterexample without claiming a defect.
- [x] Memories owner `Fixtures/MemoryErasureProcessWorker.cs`, `Fixtures/MemoryErasureProcessProtocol.cs` and `Ingestion/MemoryErasureProcessRestartTests.cs`: build private worker orchestration inside the existing test executable; parent owns Redis/Dapr and observes original exit and replacement birth identity. Extract reusable existing fixtures only where necessary. Cover foreign/replayed requests, failed start and cancellation without abandoned workers.
- [x] Those owner restart tests and existing two ingestion test files: execute each of the four matrix scenarios with observed persisted checkpoints, actual owner operations, complete victim/survivor hashes and terminal outcomes. Preserve ordinary and annotation variants and both content-bearing retry activity boundaries. Keep process variants separate from existing algorithm controls.
- [x] Parties `tests/README.md`, `docs/memories-backed-party-search.md`, `deferred-work.md` and new `tests/five-process-erasure-boundaries-2026-11-16/` receipt beneath implementation artifacts: map all five entries to new evidence and retain external gates. Preserve failed attempts and exact cleanup results.
- [x] Prepare owning changes and exact evidence for independent review and the authorized main publication. After review, the parent validates full commit messages, inspects root submodules, reconciles divergence, pushes owning main and verifies remote availability before recording the parent gitlink.

**Acceptance Criteria:**

- Given each runtime scenario, when qualification completes, then require nonzero passing coverage, zero targeted skips, distinct observed process incarnations, unchanged owned backing resources and independently verified persisted outcomes.
- Given interruption or prerequisite failure, when execution stops, then bounded cleanup finishes and exact failures remain separate from passing qualification.
- Given the implementation checks pass, when the handoff reaches independent review, then the complete diff and exact verification evidence are available for parent review and subsequent authorized publication. The parent must still verify remote main and submodule availability before completing the frozen publication intent.

## Implementation Notes

Implementation handoff and parent matrix audit completed. The parent read the complete changed code, verified all 143 captured artifact hashes, rebuilt the mapped source cohort and passed the full existing owner gate 100/100 plus new process/protocol gate 14/14, zero skips/errors. The cached four-method fixture corrections restore exact-unit authority setup. All 20 new worker births, eight sidecar exits and both owned backing containers are independently closed. The staged CRLF check exposed the new Parties file lacking its whitespace attribute; the parent added the matching narrow attribute and both staged checks now pass. Independent review and all requested patches are complete; terminal publication follows the verified owner-first order.

User approved this spec on 2026-11-16. The implementation handoff covers code, verification and documentation; the parent conducts independent review and the authorized terminal Git publication after implementation. During the implementation handoff, do not stage, commit, push or perform remote operations. The final Git task remains pending for the parent. Preserve the frozen intent.

## Spec Change Log

The parent normalized the unfrozen terminal task and acceptance wording into their implementation-handoff scope. This resolves the workflow ordering: independent review precedes terminal Git publication. The approved frozen intent and publication requirement are unchanged.

## Review Triage Log

All three review layers completed before triage: blind hunter ten findings, verification-gap reviewer two findings, edge-case hunter none. Every finding is classified separately below before shared-root grouping.

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| B1 | medium | patch | Historical production is a real child process, but ordinary/annotation admission still executes in the parent. Move those existing calls into the replacement's private protocol so the admission observation is bound to the process whose incarnation is retained. |
| B2 | medium | patch | RecordCandidate requires a prior durable entry, disproving the strongest missing-admission claim; nevertheless unrelated post-admission scheduling failures are swallowed. Require the actual scheduling attempt and only the expected duplicate-address disposition. |
| B3 | medium | patch, grouped V1 | HashesAsync serializes missing hashes as empty rows. Removing chunk production leaves the NL collision and preservation comparisons passing; require positive expected chunk/NL fields before baseline capture. |
| B4 | medium | patch | The final enqueue meets an existing permanent tombstone and supplies no victim work to TickAsync. Execute a genuine retained pre-erasure retry input after replacement and assert its refusal plus independent absence. |
| B5 | low | patch | String value 1 alone does not establish the promised nonexpiring fence. Add the direct TTL absence assertion to the existing persisted check. |
| B6 | medium | patch | RunChildAsync treats its result receipt as optional; a successful no-op child can supply no call evidence. Require a fresh receipt with exact mode and completed call list. |
| B7 | medium | patch, grouped V2 | ResolverSelectsCandidate is recorded but unchecked, so a manifest dropping Hashing can pass the selected-private-path experiment. Assert exact private selection in that variant. |
| B8 | medium | patch | B7 child-reported birth is not compared with the Process started by its parent, and observed exit is omitted from retained evidence. Bind and retain that exact incarnation and completed cleanup. |
| B9 | low | reject | PID reuse can cause a false failure in these short cleanup assertions, but cannot produce false success or kill an unrelated process. This is unlikely in ordinary execution; adding branching lifetime-absence helpers for that rare test-only outcome exceeds a direct correction. |
| B10 | low | patch | The parent added current 100/100 evidence but several handoff sentences still call that gate pending. Correct those current dispositions while retaining original failures as historical. |
| V1 | medium | patch, grouped B3 | Preverified gap: the new historical workflow's chunk delegation can be removed while NL collision and empty chunk-baseline comparisons still pass. Add positive persisted chunk assertions for both victim and survivor before capture. |
| V2 | medium | patch, grouped B7 | Preverified gap: the modified private Hashing file can remain present while its resolver selection disappears; no current assertion detects that loss. Require selection of privatePath in private-refutation. |

Patches add no production/public surface and guard the demonstrated fixture state. The approved frozen intent is unchanged. No new deferral is required for these findings.

## Verification

Final parent acceptance passed 100 existing owner, 14 strengthened process/protocol, 58 consumer, 10 signal/cleanup and one B7 parent test (three bound children), zero failures/skips/errors. All 20 runtime births, three B7 births, eight sidecar exits and exact owned backing removal are independently audited in `tests/five-process-erasure-boundaries-2026-11-16/parent-final/`. Source is reconciled on Memories `65bfb98dca7f65c005213507b15fbe4ab45f2e19`; owner remote main was pushed and read back before parent publication. The full exact message passed owning pinned commitlint before/after commit and after rebase. Every root-declared module was inspected; nested modules remain uninitialized.

B1/B2 now bind the real absence record and admission/scheduling to the replacement. B3/V1 require positive historical chunk/NL data. B4 executes a retained pre-erasure retry input; B5 checks nonexpiring fences. B6/B8 bind fresh mandatory child receipts and observed birth/exit; B7/V2 assert exact private resolution. B10 distinguishes current acceptance from retained failed attempts. Parent source preparation's HXW004 mismatch was preserved, then resolved by staging the selected owner identity for verification; no unpublished owner commit is recorded in the parent commit. No frozen intent changed.

Use run-with-temp for disposable builds, feeds and worker copies; save receipts outside TMPDIR and wait for children. Read owning guidance and Aspire lifecycle skills before runtime work. Attempt pinned Debug/source preparation and individual test builds; retain exact blockers before the documented serialized Release/package fallback, with explicit qualified cohort pins and separately identified current owner source. Run built test assemblies directly with focused -class/-method selectors, -parallelMode none, -failSkips and XML results. Reuse authenticated RedisSearch/FalkorDB/Dapr fixtures. Run existing affected owner algorithm and Parties restart/cleanup controls, warning-policy checks, git diff --check and pinned commitlint. A broad environment blocker never supplies runtime acceptance.
