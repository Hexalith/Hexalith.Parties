---
title: 'Close five Memories proof and process observation deferrals'
type: 'bugfix'
created: '2026-11-10'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'fc9ea3f3b7b8544f8a8f7b8f7912ceb45489766c'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open ledger entries prevent complete runtime qualification: failed-ingestion inventory cleanup, gateway native-proof refusal, proof/replay coverage for every canonical output, empty procfs task enumeration, and process lifetime changes during capture.

**Approach:** Reproduce each boundary, repair behavior in its owning repository, and qualify authenticated disposable runtime and process observations. Retain evidence, close proven entries, then commit and push on main.

## Boundaries & Constraints

**Always:** Preserve native proof, replay, legacy refusal, unrelated scopes and owned cleanup. Distinguish source Debug from package Release evidence. Validate messages with owner-pinned commitlint; push owners before parent gitlinks. Keep the requested five-item batch together.

**Never:** Attest fixtures to certify cleanup, weaken refusals, migrate production data, publish packages, change versions speculatively, or let the observer terminate processes. Fix technical behavior in its owning module.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Fresh extraction failure | Owned candidate, malformed PDF | Payloads and failed artifacts reconcile durably | Unknown legacy remains incomplete |
| Native read | Stable matching actor proof | Authenticated gateway returns matching events | Preserve proof/range/drift refusals and diagnostics |
| Refresh outputs | Two canonical output IDs | Both streams pass byte comparison and replay | Omission or invalid authority fails |
| Empty task enumeration | Parent exits, child survives | Capture refuses qualification | Owned teardown still completes |
| Lifetime changes | PID reused or disappears during capture | Mixed observations cannot qualify | Record failure; continue independent siblings |

</frozen-after-approval>

## Code Map

- Baseline: clean main at `fc9ea3f3b7b8544f8a8f7b8f7912ceb45489766c`.
- `tests/Hexalith.Parties.IntegrationTests/Topology/MemoriesAuthoritativePersistence.cs`: selects `UnitIds.Last()`; retained 503 diagnostics omit batch/range observations. Derive replay expectations from actual event prefixes: unit deletion may be the first event.
- Same directory: `PartyMemoryOwnedRuntimeTests.cs`, `MemoryRuntimeIdentity.cs`, `OrdinaryLauncherProcessCapture.cs` and tests own runtime execution and capture.
- Memories `src/Hexalith.Memories.Server/`: `Ingestion/SourceInventoryFailureReconciler.cs` invokes `Activities/Cases/DeleteMemoryUnitProjectionActivity.cs`. Payload removal precedes discovery; extraction precedes modern provenance. Confirm the suspected missing-coverage failure.
- EventStore `src/Hexalith.EventStore/Controllers/StreamsController.cs` has native 503 guards; `src/Hexalith.EventStore.Server/Actors/AggregateActor.SourceProof.cs` owns batch/range behavior. Localize the rejecting guard.

- Additional owner code: Memories `Ingestion/SourceInventorySnapshot.cs` records fresh-candidate eligibility; `SourcePublicationVisibility.cs` retains keys without certifying unknown history. `RefreshPublicationIntegrationTests.cs` uses actual fresh admission before modern adapter writes.
- Additional qualification code: Parties `Topology/OwnedRouteCatalogBindingHandler.cs` derives current generation stamps through SDK state; `HealthChecks/PartiesAspireTopologyFixture.cs` optionally issues one exact replay tenant grant while preserving existing defaults; `DevelopmentCatalogModelFinalizationTests.cs` covers deferred app-channel custody; `OrdinaryLauncherProcessLifetimeTests.cs` owns the bounded PID-namespace reproducer.
- Runtime receipt support: `scripts/prepare-aspire-runtime-hosts.sh`, `scripts/validate-aspire-runtime-cohort.py`, and `tests/Hexalith.Parties.Ci.Tests/runtime_cohort_receipt_cases.py` bind the wrapper's transitive native UI artifact to its verified owner receipt and source/byte identity.
- Confirmed native503 boundary: installed catalog receiver peer-generation admission, before controller dispatch. Typed native batch/range and stable inspections pass; no EventStore controller/actor patch is warranted. Current caller stamps and exact replay tenant authorization repair the qualification caller while owner refusals remain intact.

## Tasks & Acceptance

**Execution:**

- [x] Memories Server `Endpoints/SourceInventoryEndpoints.cs`, `Ingestion/{SourceInventoryStore,SourcePublicationVisibility}.cs`, `Activities/Ingestion/RecordSourceInventoryCandidateActivity.cs` and `tests/Hexalith.Memories.IntegrationTests/Ingestion/SourceInventoryProducerIntegrationTests.cs`: diagnose fresh extraction failure without attestation; initialize exact fresh producer coverage without certifying unknown legacy coverage on retries.
- [x] Parties `MemoriesAuthoritativePersistence.cs` and `MemoriesAuthoritativePersistenceTests.cs`: retain bounded typed batch/range diagnostics and the primary failure. Reproduce503 and localize the refusing boundary before changing owner behavior. Correct the qualification caller's missing current-generation stamps and exact replay tenant grant; retain missing/stale-generation and other-tenant refusals plus owner source safeguard coverage. No controller/actor patch is warranted by the observed cause.
- [x] Same Parties helper/tests and `tests/Hexalith.Parties.Tests/Search/PartyMemoryRuntimeTests.cs`: validate every canonical ID, independent stream bytes, first/final replay and stable rereads. Detect omission of either output.
- [x] Parties capture helper/tests above: retain an owned Linux parent-exit reproducer and reject empty task enumeration.
- [x] Same capture files: retain isolated PID-reuse evidence; bind recursive observations to lifetimes, rereading identities around role/descendant reads. Preserve stable-tree eligibility and cleanup diagnostics.
- [x] `tests/README.md`, `deferred-work.md` and a dated verification packet beside this spec: record identities, commands, implementation results and limits; retain open ledger status until independent review and owner adoption complete.

**Acceptance Criteria:**

- Given fresh failed ingestion, when the full owned recovery control runs, then inventory settles without fixture attestation and unrelated data survives.
- Given refreshed canonical outputs, when the focused authenticated wrapper runs, then every output passes native gateway proof and authoritative replay.
- Given real empty enumeration or PID replacement, when capture runs, then qualification refuses after owned cleanup; the complete live ordinary control still passes.

## Implementation Notes

- 2026-11-11: Approved by the user. This is the implementation handoff within the existing bmad-build run; do not restart the skill. Follow repository AGENTS.md and the required Hexalith baseline in every owning repository. Execute changes sequentially. Leave staging, commits and remote operations to the parent after independent review. Preserve the frozen block.
- 2026-11-11: Runtime preparation with `--include-memory-qualification true` exposed a qualification-wrapper receipt gap: the wrapper consumes the private Admin UI host's transitive native UI reference, but the SDK validator previously accepted that artifact only in the private host itself. The wrapper now requires the separately verified private-host receipt, exact reference identity, source-artifact provenance and matching bytes; receipt readback revalidates that owner proof. Other SDK-family refusals remain unchanged. The separate source/native profile does not enroll catalog bootstrap and remains diagnostic only; use the supported package-host profile for authenticated runtime qualification.

- 2026-11-11: The owned wrapper had overridden SDK-generated app-channel custody before full package composition, causing a duplicate token resource. It now selects the existing deferred fixture-token handoff; six model controls verify generated custody reuse and final project/sidecar token agreement.
- 2026-11-11: Native503 is reproduced after a passing full recovery child. Authenticated typed actor diagnostics prove matching available initial/later proof and successful native batch/range at stable head3. The actual refusal precedes the controller: the helper lacked the catalog root/epoch/binding required by the installed receiver. The qualification caller now reads the real selected authority cell through the SDK and derives current generation stamps with the SDK codec; valid workload credentials and bound native proof remain required. Missing and stale generation controls retain503. No EventStore controller/actor behavior is weakened or speculatively changed.

- 2026-11-11: Parent staged and read the complete root/owner baseline diff. The [parent matrix audit](tests/five-memories-proof-and-process-observation-2026-11-11/parent-matrix-audit.json) verifies all five rows against 15 executed passing methods, all18 recorded source hashes, both full ownership receipts and both ordinary timing receipts. Implementation tasks are complete; independent review and authorized owner/root publication remain pending.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH1 | maybe-false | defer | New admission and one-use eligibility precede candidate writes; ordinary file/URL callers generate new GUIDs and directory callers new ULIDs, while failed retries are excluded. No real producer path reusing a historical orphan identity was demonstrated. Settle with an owned scheduler/workflow reuse reproducer and surviving-key end-state. |
| BH2 | false | reject | Candidate retention adds no coverage when provenance already exists. SourcePublicationVisibility.Stage checks the permanent unit tombstone before every content write and returns refusal; retaining an obligation does not revive data or waive that fence. |
| BH3 | false | reject | Initialization additionally requires a matching valid inventory workflow with no retained candidate or FailedRetryMemoryUnitId. IngestionIdentityGuard bounds entry IDs; blank/oversized/orphaned/consumed markers cannot satisfy that eligibility predicate. The snapshot size and marker count are bounded. |
| BH4 | medium | patch | ReadWorkloadStreamAsync deserializes; the helper compares event bytes but does not reconstruct the native proof chain. The existing SDK AuthoritativeEventStreamReader(requireNativeProof:true) supplies that validation without a new platform contract. Stable invalid proof/digest mutations need consumer controls. |
| BH5 | maybe-false | defer | The children list has no birth identity and the first identity read does not check parent membership. This ordering predates the change; the real namespace control changes identity during comm instead. Settle an owned foreign-parent replacement between children enumeration and the first birth read before adding a relationship protocol. |
| BH6 | medium | patch | The new shell is started and child output is read/opened before the cleanup try block. A canceled or malformed read can bypass explicit owned cleanup. Own the shell immediately and retain early-failure controls. |
| BH7 | medium | patch | The procfs reproducer reuses the test token and awaits child exit before attempting parent cleanup. A canceled token or first cleanup failure can skip parent cleanup. Use a fresh bounded cleanup lifetime and attempt every owned process while preserving failures. |
| BH8 | medium | patch | Both reproducers use synchronous WaitForExit without a timeout; the empty-task fact has no outer timeout. Replace these waits with bounded owned waits/forced cleanup and retain the real passing controls. |
| BH9 | maybe-false | defer | README explicitly declares Linux procfs/user-PID namespace support and the actual host passed. Whether the maintained CI/default supported developer lane schedules this test on a host lacking those prerequisites is unproved. Settle with that lane capability audit and an actual refusal before changing qualification selection. |
| BH10 | low | reject | A call already in flight can finish up to its fixed four-second RPC timeout after capture cancellation. CaptureNativeRefusalAsync deliberately awaits actual cleanup; this is a bounded diagnostic-only delay on a rare failed-read path, not accepted authority or detached work. A linked transport/deadline mechanism exceeds a direct correction for negligible harm. |
| EC1 | maybe-false | defer | Same unproved producer-identity reuse boundary as BH1; current admission proof is not inferred solely from base-key absence. A real legacy orphan collision through the scheduler/workflow and surviving deletion end-state would settle it. |
| EC2 | medium | patch | record iterates provenance rows without requiring a valid absolute-path/hash row; bind_native_ui only requires the manifest to be inventoried. Empty or metadata-only manifests can therefore satisfy the new wrapper binding. Require source rows during recording and readback, retaining metadata headers. |
| EC3 | medium | patch | Same early ownership boundary as BH6: child output acquisition occurs before the cleanup try block. Own the shell immediately and cover cancellation/malformed-output cleanup. |
| EC4 | low | patch | OwnedRouteCatalogBindingHandler sends state.Epoch+1. This proves noncurrent generation refusal, not the claimed preceding epoch. Send an older epoch in stale mode and retain actual sent versus observed epoch evidence. |
| VG1 | medium | patch | Pre-verified gap: existing complete-root tests match retained/current starts, and existing churn tests change children later. Add the complete replacement-root case so removing the expected launcher start argument fails the regression. |
| P1 | medium | patch | The namespace reproducer trusts PARTIES_PROCESS_PID_REUSE_CHILD=1 before writing ns_last_pid. A direct/inherited-marker rerun outside the owned namespace reaches that mutation. Verify owned new PID/user namespace identity before starting the mutation branch and test refusal without mutating the host counter. |

## Review Completion

All patch groups are fixed and verified. Final serial verification passes source integration71/71, adapters50/50, owner safeguards149/149, native/process models53/53, catalog models40/40, validator30/30, both owned wrappers1/1 each and ordinary live2/2, all with zero skips. Both wrappers retain complete disposal receipts; ordinary complete capture has no failures and incomplete capture refuses after cleanup. Original and first-attempt evidence remains historical; the final packet binds the patched sources and completed13-project cohort.

Three medium-unverified questions are appended to the deferred ledger: real legacy-orphan identity reuse through a producer, PID membership before the first birth read, and maintained-lane namespace capability. No confirmed review defect remains open. Memories source43d2d72f is pushed and remotely read back before parent source adoption. Parties committed-selection checks pass3/3, and both recorded-gitlink gates and pinned commitlint pass. The authorized root main push follows this final evidence snapshot; public package Release/deployment acceptance remains separate.

## Verification

- Build individual projects with pinned module-tool source mappings/Debug; run built xUnit assemblies with single-dash filters.
- Run `SourceInventoryProducerIntegrationTests`, affected owner gateway/actor controls, `MemoriesAuthoritativePersistenceTests`, `MemoryRuntimeIdentityTests`, and `OrdinaryLauncherProcessCaptureTests`.
- Prepare the matching cohort with `scripts/prepare-aspire-runtime-hosts.sh --configuration Debug --include-memory-qualification true`; use the native gateway profile when qualifying owner source fixes. Execute both `PartyMemoryOwnedRuntimeTests` methods with `PARTIES_MEMORIES_OWNED_RUNTIME=1` and zero skips; record any package limitation separately.
- Run both ordinary live timing controls after matching preparation; retain Linux reproducers and cleanup receipts.
- Run warning/whitespace gates and commitlint; fetch, inspect divergence and verify owner reachability before pushing main.
