---
title: 'Five subscription safety and package adoption deferrals'
type: 'bugfix'
created: '2026-11-03'
status: 'done'
route: 'dispatch'
baseline_commit: '6795536e3fe5fd4cf44eb5f4c67e69f3374532b4'
review_loop_iteration: 1
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EventStore leaves callbacks undrained, replacement joins vulnerable to older leaves, and canceled joins uncertain. DW-100 ignores compatible published APIs. Memories refresh qualification hard-codes a Debug child.

**Approach:** Fix three transport deferrals, adopt selected reconciliation APIs, and bind unchanged-URI refresh qualification to executed identities. Review, verify, commit in owning repositories and push main. DW-100 and refresh retain external acceptance gates.

## Boundaries & Constraints

**Always:** Preserve isolation, legacy clients/servers, cancellation, survivors, reconnects, readiness, witnessed completion and local rollback. EventStore owns transport.

**Never:** Infer absence from cancellation or quiescence from revocation. No package updates, release, deployment, production mutation or invented acceptance/completeness. Guarantees require qualified capabilities.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior | Failure handling |
| --- | --- | --- | --- |
| Callback | Removal after entry | Revoke and drain | Self-removal revokes/returns; external removal drains |
| Replacement | Old leave/new join | Latest acknowledged intent wins | Preserve survivors |
| Uncertain join | Canceled/lost acknowledgement | Reconcile retained intent | Cancellation proves no absence |
| Packages | FrontComposer 4.7.5 / EventStore 4.0.4 | Actual shared execution | Older versions retain fallback |
| Refresh | Wrapper configuration | Matching child/producer identities | Mismatch refuses |

</frozen-after-approval>

## Code Map

EventStore paths are relative to `references/Hexalith.EventStore`:

- `src/Hexalith.EventStore.SignalR/{ProjectionCallback,EventStoreSignalRClient}.cs`: invocation/group ownership and reconnects.
- `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`: group RPCs and acknowledged reconciliation.
- `tests/Hexalith.EventStore.SignalR.Tests/{EventStoreSignalRClientTests,StalledProjectionHub,StalledProjectionRpc}.cs`: real transport barriers/broadcasts.

Parties paths:

- `Directory.Build.targets`; `src/Hexalith.Parties.UI/Services/{OptimisticReconcile,EventStoreSignalRProjectionStream}.cs`; `tests/Hexalith.Parties.UI.Tests/OptimisticReconcileTests.cs`: gates/local fallback.
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartyMemoryOwnedRuntimeTests.cs`: graph and child selection. `tests/Hexalith.Parties.Tests/Search/PartyMemoryRuntimeTests.cs`: existing refresh/cleanup control.
- `scripts/prepare-aspire-runtime-hosts.sh`: selected-package native hosts; preserve compiler/completion gates.

## Tasks & Acceptance

**Execution:**
- [x] EventStore callback/client: account for active invocations and drain removals; cover cancellation/reentrancy.
- [x] EventStore client/hub: add negotiated generation-ordered acknowledged membership, serialize transitions and retain uncertainty until reconciled/connection termination.
- [x] EventStore tests: force all three races in ordinary/scoped groups; verify membership, delivery and survivors.
- [x] Parties capability/UI files: enable exact package branches; prove shared invocation and retained/shared parity.
- [x] Runtime wrapper/tests: select matching child via build metadata, reject mismatch and execute authenticated refresh/erasure with provenance.
- [x] `deferred-work.md`, `tests/README.md` and dated receipts: record five dispositions, commands, hashes, review and remaining gates.

**Acceptance Criteria:**
- Given an entered callback, when non-reentrant removal completes, then no owned invocation remains or can restart.
- Given reordered RPCs/lost acknowledgements, when reconciliation converges, then delivery matches live subscriptions without evicting survivors.
- Given selected packages, when UI controls run, then actual shared and retained behavior agree across readiness, witnesses, retries, outages and cancellation.
- Given matching runtime identities, when refresh runs, then persisted current content changes at the same URI and terminal cleanup preserves unrelated state.
- Given focused evidence, when dispositions are recorded, then publication/external acceptance remain separate.

## Implementation Notes

- I17 prerequisites: current G4/G6 APIs and selected roots. Inspect owner guidance and obtain required Aspire baseline before code edits. No dependency update.
- I17 touched repos: Parties/EventStore only; FrontComposer/Memories read-only. No nested references or reference skills.
- I17 rollback: retain legacy RPCs/overloads and local reconciliation; execute compatibility controls. Revert task commits without data migration.
- I17 validation lanes: individual owning Debug transport/server and Parties UI tests, package composition, provenance refusals and owned Memories runtime. Separate source mapping/package Release evidence and broad blockers/focused results.
- Focused validation ladder (2026-11-03): retain the original full owned runtime/default child and its malformed-PDF failed-producer assertion. Two full attempts fail with producer `INVENTORY_INCOMPLETE` HTTP 409 after source payload deletion while the failed-unit record remains. The explicitly named focused owned refresh/erasure/replay control executes the unchanged-URI/main-party terminal cleanup/survivor matrix with identical configuration and executed-identity checks. Its result cannot qualify the broader failed-ingestion gate, production completeness or writer drain. Memories remains read-only; producer correction and full runtime acceptance remain separate.
- I17 non-goals: production cutover, writer-drain/completeness attestations, crypto, publisher workarounds, custody, releases, deployment or Epic 8 promotion.
- I17 parity: additive I5 contracts, I6 scope, I9/I10 freshness/witnesses, I12 ownership, I16 identities and erasure/survivor bytes. Focused checks do not qualify canonical closure lanes.

## Spec Change Log

- Review loop 1, BH-02: the non-frozen plan omitted finite membership-attempt ownership. Re-derive the client with finite RPC/gate attempts and owned physical-connection recovery after an indeterminate timeout. A live heartbeat must not let a hung attempt monopolize membership forever. Preserve intent until acknowledged or the original connection definitively ends, then restore every still-authorized subscription; disposal cancels/awaits all recovery. Use an internal five-second attempt budget without a new public configuration surface. Do not claim remote absence from invocation cancellation alone.
- KEEP: retain all source/package capability fences, callback entry/revocation accounting, self-disposal ownership, retirement of ordinary legacy bookkeeping, protocol generations/possible-group inventory, quota-safe full authorization, raw-broadcast survivor/reconnect/backend-fault proofs, matching runtime child/provenance checks, original full runtime assertions, and both explicit broader runtime refusals. Positive source snapshots are retained below for reconstruction; preserve all these working behaviors while correcting the reviewed paths.
- Surviving patches: callback-initiated removal must recognize any callback currently executing for that client, including another group, while external removers still drain. Legacy explicit authorization rejection must prune only the rejected intent and allow authorized groups to rejoin. Every fallback/probe exception schedules owned recovery. Canceled semaphore admission cleans connection state even when acquisition fails.
- Surviving proof patches: resolve child TargetPath from the actual referenced project evaluation, including SDK artifact/output/RID settings. The focused runtime must directly prove absence of every canonical output and populate both unrelated survivor scopes with real content/derived state before verifying preservation. Set a separate SurvivorsVerified flag only after all survivor checks pass. Native-refusal diagnostics use an independent bounded token and retain the primary if capture fails. Add real mixed-tenant snapshot rejection and partial-revocation/rejoin transport controls for protocol 1 and legacy where applicable.


## Review Triage Log

- BH-01: medium / patch — Cross-group callback-specific removals can mutually await completions because reentrancy checks only target retired callbacks; source and the reviewer probe establish the cycle.
- BH-02: medium / bad_spec — Background repair supplies only the disposal token to a shared-gate RPC. Heartbeats can keep a stalled invocation alive indefinitely, blocking later membership changes. The plan omitted a finite attempt/recovery boundary.
- BH-03: medium / patch — Legacy rejected joins abort the new full iteration; only protocol 1 has pruning, whereas the prior legacy loop pruned and continued. Authorized later groups can remain unjoined.
- BH-04: medium / patch — Exceptions thrown inside the protocol-1 catch body bypass subsequent catch clauses; probe/retry failures therefore escape without scheduling owned repair.
- BH-05: medium / patch — GetOrAdd precedes WaitAsync and the try/finally. An already canceled connection token throws before cleanup, retaining the newly inserted static membership state.
- BH-06: low / reject — The demonstrated mixed-wire sequence is possible for a custom caller, but the qualified client selects one negotiated protocol and never sends legacy mutations in protocol 1. The acknowledgement names the last applied snapshot generation; arbitrary mixed-wire ownership is unqualified. Adding a new protocol-mode guard is disproportionate for this unsupported caller.
- BH-07: medium / patch — The child path reconstructs default bin layout. Supported SDK artifacts/RID/output overrides can select a missing or stale default child; use the referenced project evaluated TargetPath.
- BH-08: medium / patch — Only the second canonical output receives a direct qualified absence read. Both-output erasure needs explicit reads for every recorded authority output.
- BH-09: medium / patch — Both survivor cases are empty, so unchanged case bytes cannot detect unrelated memory-unit or derived-content deletion. Populate both survivor scopes before cleanup.
- BH-10: medium / patch — The receipt exports the terminal main-unit absence flag as SurvivorsVerified before survivor assertions run. A failed assertion can leave a false positive flag.
- BH-11: medium / patch — Native-refusal diagnostic I/O uses the primary operation token and the final write can replace the original exception. Use a bounded independent capture lifetime and preserve both failures.
- EC-01: medium / patch — Same verified legacy authorization-pruning regression as BH-03; preserve authorized later rejoin delivery.
- EC-02: medium / patch — Same verified protocol-1 fallback exception boundary as BH-04; a second backend failure can escape repair scheduling.
- EC-03: medium / patch — Same verified cross-group/callback-specific wait cycle as BH-01; identify execution across callbacks owned by this client.
- VG-01: medium / patch — Pre-verified regression gap: all snapshot transport inputs are authorized acme; legacy authorization tests do not detect bypassed snapshot authorization or partial mutation.
- VG-02: medium / patch — Pre-verified regression gap: reconnect tests retain tenant grants or dispatch locally; real partial revocation must prove valid survivor delivery and revoked absence.
- VG-03: medium / patch — Verified Other finding: duplicate legacy authorization regression, independently traced against the prior per-group catch/prune behavior.

## Verification

Build projects individually; run built xUnit v3 filters without targeted skips. Retain identities/commands/XML. Use explicit runtime opt-in/isolated Dapr; verify cleanup. Run `bash scripts/check-no-warning-override.sh` and `git diff --check`; retain pinned commitlint success. Fetch/inspect divergence; push main without force.

- Human approved and continued on 2026-11-03. Frozen intent is locked. Implementation follows step-03 sequential handoff; primary owns final review, commits and push.

- Required exact-target Aspire baseline: isolated start exited 0; resource state was inspected and captured in `tests/five-subscription-and-package-adoption-2026-11-03/aspire-baseline.json`; exact-target stop exited 0. This is startup/state evidence, not topology readiness qualification. An unrelated running Folders AppHost was preserved.

- Primary matrix audit: all five frozen matrix rows have executed passing controls, recorded in `tests/five-subscription-and-package-adoption-2026-11-03/matrix-audit.json`. The focused child passed twice; full PDF and native-proof/replay gates remain failures and are recorded separately. The fifth task is a bounded advancement, not full producer or deployment qualification.
- Final implementation lanes: client 61, hub/server 50, mapped Debug UI 53, selected Release UI 53, older Release tuple 50, preparation 57, identity/cleanup 11, and focused runtime child 1, each with zero targeted skips. Older-tuple output hashes were not captured before restoration; no stronger provenance claim is made.

## Review Re-derivation Inputs

- Read-only positive Parties source: `tests/five-subscription-and-package-adoption-2026-11-03/review1-positive-parties.diff.gz` relative to this spec directory.
- Read-only positive EventStore source: `tests/five-subscription-and-package-adoption-2026-11-03/review1-positive-eventstore.diff.gz` relative to this spec directory. Its patch paths are relative to the EventStore owner root.
- Primary intentionally reverted task code only to the captured baselines for this loop; documentation and historical receipts remain. Reconstruct the KEEP behaviors from those snapshots and re-derive the reviewed paths under the amended non-frozen plan. Do not modify the frozen intent. Do not invoke bmad-build recursively; this is the implementation handoff of the active workflow. Primary owns commits, remote operations and final publication.
- Run focused changed-file controls and the strengthened owned refresh/erasure child. Preserve original full/default controls and native-proof requirements; report their already-established broader refusals separately. Capture exact current source/binary hashes and positive plus negative results. Leave final parent audit/review/commit bookkeeping to primary.

- Review re-derivation complete: source client 63, hub/server 64, source UI 53, package UI 53, older tuple 50, preparation 57, identity/cleanup 12, native diagnostic failure controls 3, and strengthened owned child 1, all with zero skips. `review2-matrix-audit.json` maps all frozen rows to executed controls. The wrapper still fails native proof with 503; historical full PDF inventory 409 remains open. Current versus live-wrapper source snapshots and later diagnostic-helper tests are distinguished explicitly.
- At implementation handoff, Git operations remained pending after final review: validate exact messages, commit/push owner changes before the parent source pointer, reconcile current selection observations without altering historical approvals, and preserve any incoming main changes. At that handoff no commit or push was claimed by the implementation task boxes.

### Review pass 2 triage

- BH2-01: medium / defer — Legacy canceled joins can outlive a later legacy leave; this predates the task and is explicitly unqualified without the generation-aware peer. The new client retains that uncertainty until connection termination; no legacy remote-absence guarantee is claimed.
- BH2-02: medium / patch — Three offline subscriptions against a two-group peer produce an invalid full snapshot. Generic catches currently swallow/retry the unchanged invalid intent, rather than surfacing a permanent rejection.
- BH2-03: medium / patch — The new repair loop retries all fast failures every 100 ms. Backend unavailability across circuit-scoped clients can produce sustained RPC/log load; bound transient retry frequency and stop terminal-invalid retries.
- BH2-04: medium / patch — Manual recovery increments generation before replacement startup but not after membership rejoins. A consumer sampling during the acknowledgement gap can miss the final catch-up transition promised by the property contract.
- BH2-05: medium / patch — Capture timeout exhausts the shared deadline, causing receipt persistence to inherit cancellation even with a healthy filesystem. Give persistence a fresh bounded lifetime.
- BH2-06: medium / patch — WaitAsync can stop awaiting capture before its cancellation cleanup settles. Keep the qualified cooperative capture task owned until completion and prove delayed cancellation cleanup; do not replace primary refusal or claim arbitrary noncooperative work quiescence.
- BH2-07: medium / defer — The authority-file write preceding teardown exists unchanged in the baseline finally block. Receipt I/O failure can skip child-level tenant teardown; wrapper-owned topology disposal is independent, but standalone child cleanup remains a pre-existing follow-up.
- BH2-08: medium / defer — The baseline authoritative helper already selected UnitIds.Last while the control recorded multiple outputs. Earlier-unit stream/replay coverage remains a broader pre-existing gap; the current task directly proves all-output REST absence and explicitly leaves native proof/replay unqualified.
- BH2-09: low / patch — The newly introduced focused name AcrossRestart can suggest service restart although it recreates clients/adapters. Rename only the new focused fact and document client recreation; preserve the original full gate name.
- BH2-10: medium / patch — The package capability fence newly forwards real unsubscribe cancellation and recovery generation, but actual adapter verification only stalls join. Add real leave cancellation and disconnect/rejoin generation/delivery controls.
- BH2-11: maybe-false / reject — No disappearing-frame failure is demonstrated for the qualified .NET 10 runtime; the actual Release package test passes and the large shared async state machine supplies the execution frame. A different JIT/runtime would need to remove every shared frame to establish this low test-portability claim. Replacing it with a new production instrumentation surface is disproportionate.
- VG2-01: medium / patch — Pre-verified regression gap: FakeProjectionStream removal tests bypass the actual adapter; forwarding CancellationToken.None from the package unsubscribe overload would pass current checks. Add actual selected-package LeaveGroup cancellation coverage.

- Edge-case reviewer returned no findings in this pass. All remaining current-change entries route to direct patches; the existing qualified intent and design remain unchanged.


### Final review corrections and primary verification

- All current-change review-pass-2 findings were patched by the continued implementation agent and inspected by primary. Terminal intent rejection surfaces without an unchanged-intent loop; transient retries use capped jittered backoff; final recovery generation follows acknowledged rejoin. Diagnostics await cooperative cancellation cleanup before a fresh persistence budget. The focused fact names client recreation; actual packaged leave cancellation and reconnect/broadcast controls pass.
- Primary rerun: client 63, hub/server 68, mapped Debug UI 55, selected Release UI 55, older Release tuple 50, preparation 57, identity/cleanup 12, native diagnostic failure controls 4, and focused authenticated child 1, all with zero targeted skips. Exact commands, dependency types and executed identities are in final-lanes.json and final-runtime-source-identities.json in the dated receipt directory. Final-matrix-audit.json verifies every frozen matrix row.
- The final owned wrapper exercises the patched diagnostics and renamed child, then retains the native-proof HTTP 503 refusal. Owned topology disposal succeeds and its session directory is absent. Historical full failed-PDF inventory HTTP 409 remains open. Neither native replay nor full failed-ingestion cleanup is claimed.
- Three pre-existing review issues remain deferred: legacy cancellation without a generation fence, standalone child teardown after receipt-write failure, and authoritative native replay of every recorded output. Historical approvals and package versions remain unchanged.

### Git completion evidence

- EventStore task commit `f213e4115ec23cb736e8aa0113f61b9d4854480c` is reachable from published main `342664954389359a3ad4ec979a8a503f923798ef`, which retains incoming main `c58eb1ac0fe3380ec10cad685877a8072a5fcda4`. The merged owner main passed client 63 and hub/server 68 again. Parties pins the immutable qualified task cohort. No nested submodule was initialized.
- Parties consumer `26f58424400e3f72fdb9c7d572d098ae701206ce` and separate source selection `7d17c3d5df4254e98965bcf53fe176a2fb4925d7` retain incoming main `7d3bc8879c39d32f34866df5098a0d369821db03`. Current observations and a delegated signoff were updated; historical owner approval rows are unchanged. Committed-selection controls pass 21 with zero skips. Both gitlink gates and exact pinned commitlint validation pass. Final main publication follows this receipt; no remote CI success is claimed.
