---
title: 'Five deferred items for reliable optimistic reconciliation'
type: 'bugfix'
created: '2026-10-30'
status: 'in-review'
route: 'dispatch'
baseline_commit: 'f9757d94a9e3f144d250b21f295b903c00d5f412'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Optimistic reconciliation can finish on unrelated Current data, miss a brief outage, remain pending after its only confirmation, or stall during transport teardown. DW-100 also retains a shared-reconciler adoption gate.

**Approach:** Address the four final reconciliation entries in `deferred-work.md` together with DW-100 as one reliable-operation batch. Extend the owning transport/shared primitives, integrate supported capabilities in Parties, and qualify retained and shared paths independently. Commit reviewed task changes in their owning repositories and push to main after verification, as requested.

## Boundaries & Constraints

**Always:** Preserve tenant authorization, local rollback, existing public overloads, subscription readiness before dispatch, early-signal buffering until acceptance, serialized reads, exactly-once completion, silent caller cancellation and existing accessibility announcements. Generic transport behavior belongs to EventStore; generic orchestration belongs to FrontComposer. Record full Git identities and validation outcomes.

**Never:** Infer causal confirmation from a tenant/projection signal or Current alone. Do not change dependency selections, attest owner acceptance, release packages, deploy, migrate production data or close a publication gate from source-only tests. Older package behavior remains explicitly bounded. DW-100 stays open if no already-selected published asset supplies the corrected contract.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Causal confirmation | Unrelated signal and old Current model | Remain pending until caller's operation-specific witness succeeds | Missing witness cannot certify causality |
| Brief outage | Disconnect/reconnect entirely between interval ticks | Durable transition triggers catch-up read after acceptance | Keep retrying safely if catch-up is stale |
| Sole confirmation | Stale or failed first read; transport remains connected | Serialized interval retries reach witnessed completion without another signal | Cancel owned retry work on exit |
| Stalled RPC | Join/leave acknowledgement never arrives | Cancellation removes local callback authority and bounds transport work | No late registration or callback effects |
| Shared capability | Registered producer supports required contract | Same behavior as retained implementation | Unsupported package retains local implementation |

</frozen-after-approval>

## Code Map

Paths below are rooted at Parties unless explicitly prefixed with a root submodule.

- `src/Hexalith.Parties.UI/Services/`: `OptimisticReconcile.cs` accepts Current alone; `OptimisticReconcileRequest.cs` lacks a witness; `CommandAcceptance.cs` carries Boolean acceptance. `IProjectionStream.cs`, `EventStoreSignalRProjectionStream.cs`, `PartiesProjectionRegistration.cs` and `ProjectionFreshnessFallback.cs` own registration and interval sampling.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs`: owns group RPCs, callback rollback and reconnect/rejoin. Detailed notifications already exist; Parties currently discards their metadata.
- `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts.UI/Freshness/FcOptimisticReconcileRequest.cs` and `src/Hexalith.FrontComposer.Shell/Services/Feedback/FcOptimisticReconciler.cs` under that root: generic callback contract and orchestration.
- `Directory.Build.targets` and `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs`: capability fences and opt-in composition.
- `_bmad-output/planning-artifacts/architecture.md`: acceptance is not read-your-write; reuse one effect and existing announcements.

## Tasks & Acceptance

**Execution:**
- [x] EventStore client above -- add compatible cancellation and observable transition/rejoin generations. Fence registration locally; cancellation must settle RPCs rather than abandon their awaiters.
- [x] FrontComposer request/service above -- add caller-owned operation witness, serialized retry and catch-up support. Default to the existing interval; remain pending until witnessed completion or caller cancellation. Preserve legacy API behavior without certifying causality.
- [x] Parties service files above -- forward producer capabilities and witness qualification. Retry stale/failed reads after confirmation even while connected; recover sub-interval outages; drain all owned work.
- [x] Capability/composition files above -- integrate supported source APIs; retain selected-package fallback. No dependency/version changes or automatic package publication.
- [x] `tests/Hexalith.Parties.UI.Tests/{OptimisticReconcileTests,PartiesProjectionSubscriptionTests,ProjectionFreshnessFallbackTests,FakeProjectionStream}.cs` -- test the matrix with deterministic time and concurrent/late callbacks.
- [x] `references/Hexalith.EventStore/tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs` -- exercise actual stalled hub joins/leaves and cancellation races.
- [x] `references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Services/FcOptimisticReconcilerTests.cs` -- independently qualify shared orchestration and legacy compatibility.
- [x] `_bmad-output/implementation-artifacts/tests/five-causal-reconciliation-2026-10-30/` and `deferred-work.md` -- retain five evidenced dispositions, commands and full identities, distinguishing implementation from publication/deployment.

**Acceptance Criteria:**
- Given unrelated signals and old Current data, when strengthened reconciliation runs, then only the operation-specific read witness permits completion.
- Given a sole stale/failed confirmation or brief outage, when the model catches up, then recovery completes once without another notification.
- Given stalled real transport RPCs, when cancellation runs, then teardown settles within its tested bound with no late registration or callback authority.
- Given retained and supported shared paths, when the matrix runs, then behavior agrees and owned timers, reads and subscriptions end.
- Given source-only success, when dispositions are recorded, then DW-100 publication/owner/deployment gates remain open.

## Implementation Notes

- Human approved and continued on 2026-10-30. Implementation is sequential; no commits, push or remote operations during the implementation handoff. Primary agent owns final staging, review and publication.
- Required exact-target Aspire baseline start exited 2 before code edits: PartiesCatalogDeclarations.cs cannot resolve RouteCatalogDeclarationSourceAttribute/RouteOperationContract in the selected package graph. Exact commands and output are retained in tests/five-causal-reconciliation-2026-10-30/aspire-baseline-*.txt. This does not authorize dependency changes; use the documented validation fallback ladder.

- Final diagnostic parity passes 129/129 independently in retained/shared lanes; the matrix audit checks exact cases and source/receipt hashes. Owning EventStore passes 50/50 and FrontComposer reconciler 30/30 with clean Debug builds. The broad Shell suite records 3,885 passes, 12 governance failures and one Live skip; Parties UI and fresh pinned source mapping remain independently blocked. Evidence: `tests/five-causal-reconciliation-2026-10-30/README.md`.
- The source shared adapter requires the new producer contract; selected packages preserve local orchestration and legacy transport APIs. Composition registration was inspected and retained; independent source capability fences were added in Directory.Build.targets. Four source findings are reconciled; DW-100 remains open for exact package adoption, named owner acceptance and deployment.

- Review remediation is complete: separate five-second removal lifetime, propagated initial-join cancellation, captured-snapshot tests, real hub survivor delivery, legacy signature execution, all four capability combinations and owned-process-group timeout proof. Final diagnostic suites pass 135/135 in each mode; owning EventStore passes 55/55. The isolated missing-revocation mutant fails both intended cases. Original broad blockers and publication gates remain unchanged.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence |
| --- | --- | --- |
| BH-01 | false / reject | Missing-witness pending behavior is the admitted fail-closed contract, explicitly tested and documented; an unrelated Current must never certify an unsupported caller. No production caller of this primitive supplies a missing witness. Public shape remains compatible; callers must supply authority or cancel. |
| BH-02 | medium / patch | RemoveAsync cancels its registration lifetime before passing that same token to the new leave RPC; even a healthy server cannot receive ordinary cleanup. Use a separate bounded leave lifetime. |
| BH-03 | medium / defer | A raw delegate already inside Invoke can finish after removal; the prior implementation invoked captured raw Actions with no revocation at all. This pre-existing arbitrary in-flight delegate-quiescence gap is distinct from suppressing captured-but-not-started registrations. Parties drains its cancellation-aware reads and suppresses their effects. |
| BH-04 | medium / defer | The original client also removed the dictionary entry before invoking LeaveGroup, so a competing replacement join could precede a stale leave. Per-group remote sequencing is pre-existing work, separate from callback authority. |
| BH-05 | medium / defer | The original JoinGroup catch also removed local registration after an uncertain failure despite possible server completion. New cancellation exposes the same uncertain remote-membership problem. Source qualification explicitly permits late server completion and does not attest remote group absence. |
| BH-06 | medium / patch | The new caller token reaches startup JoinAllGroupsAsync but its blanket cancellation catch reports success. Rethrow caller cancellation while retaining disposal handling. |
| BH-07 | medium / patch | Reflection-only survivor assertions miss real group delivery; use a real hub broadcast and a bounded received-signal barrier for the surviving subscriber. Physical membership absence remains unqualified under BH-04/05. |
| BH-08 | low / patch | Existing calls now target cancellable overloads because of analyzer requirements. Add explicit execution of the preserved legacy method signatures, including signal and scoped/tenant-wide detail APIs. |
| BH-09 | medium / patch | Two coupled diagnostic modes leave independently gated mixed source capabilities unexecuted. Generate and run each mixed configuration separately. |
| BH-10 | medium / patch | subprocess.run timeout kills only its direct child. Give each diagnostic command an owned process group and kill/drain that group before hashing timeout output; qualify the bounded utility path without claiming escaped-session containment. |
| EC-01 | medium / patch | Same verified already-cancelled normal leave lifetime as BH-02. |
| EC-02 | medium / patch | Same verified startup cancellation swallow as BH-06. |
| EC-03 | medium / defer | Same pre-existing raw in-flight delegate-quiescence boundary as BH-03; the new check suppresses snapshots not yet entering Invoke, while Parties owns and drains asynchronous effects. |
| VG-01 | medium / patch | Pre-verified: current tests dispatch after dictionary removal and would pass without the new revocation guard. Pause logging after dispatch snapshot capture, unsubscribe, resume and assert suppression for signal/detail registrations. |


## Verification

- Read owning-repository guidance and inspect Git state before edits; obtain the required Aspire baseline through its orchestration skill.
- Build affected projects individually in Debug with warnings enforced; run built xUnit v3 class filters and affected full suites. Use pinned source mapping; record blockers and focused fallback evidence separately.
- Qualify real stalled-RPC controls and producer/consumer parity independently.
- Run `bash scripts/check-no-warning-override.sh` and `git diff --check`. Validate each full commit message with its owning repository's pinned commitlint and retain evidence. Fetch and inspect divergence; push dependency task commits before parent gitlink commits to main.

- All patch findings were corrected and verified. Three pre-existing owner-protocol gaps were appended to the ledger; no stronger raw in-flight callback or physical server-membership claim is made.
