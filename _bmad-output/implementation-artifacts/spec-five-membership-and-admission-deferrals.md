---
title: 'Resolve five membership and admission deferrals'
type: 'bugfix'
created: '2026-11-11'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '96055d5cb2b75a6d12e12e87b349b7fa473a7b66'
context: ['AGENTS.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferred concerns remain: foreign-parent PID reuse, namespace prerequisites, canceled legacy SignalR joins, receipt admission, and historical Memories identity reuse.

**Approach:** Reproduce each boundary, correct confirmed behavior in its owning repository, and retain independent verification. The requested five-item batch stays together. Commit and push on main, including the three existing published submodule advances approved by the user.

## Boundaries & Constraints

**Always:** Require real process, transport, scheduler and persisted-state evidence for the corresponding claims. Preserve independent siblings, surviving subscriptions, exact ownership and bounded cleanup. An unproved concern stays open with its concrete blocker.

**Never:** Infer historical completeness from seeded collisions, add broad database scans, weaken authorization, modify production data, publish packages, or claim source adoption qualifies deployed packages.

## I/O & Edge-Case Matrix

| Boundary | Input | Required outcome |
|---|---|---|
| Descendants | Listed PID replaced under a different parent before first identity read | Refuse attribution; preserve true siblings and owned cleanup |
| Namespaces | Unsupported host or failing namespaced child | Explicit unsupported result; strict qualification and child failures cannot pass |
| Legacy joins | Canceled RPC completes after repair | End the old physical connection before convergence; restore surviving intent |
| Receipts | Linked/swapped receipt or metadata | Read admitted bytes once; reject unsafe entries and conflicting ownership |
| Memories | Actual workflow identity reuse with historical artifacts | Never certify complete erasure while artifacts survive; fresh cleanup still settles |

</frozen-after-approval>

## Epic 8 §4 Gate

1. **Prerequisites:** Use existing APIs. Owner baselines: EventStore `c94e6fffaf2d689c94bb288f151a65e10a2fea82`, Memories `43d2d72f7170a29a58b6046f69e05c9ad9734701`, Builds `891886ddc60a9924d053bb74bf6bcc73e101b2ce`. Stamp final revisions and evaluated package versions; preserve unvalidated I16 parity.
2. **Touched repositories:** Parties; EventStore, Memories and Builds only for confirmed owner corrections and tests. Use the owning checkout for edits. Include approved McpCli/Tenants advances without changing their content.
3. **Rollback:** Preserve all runtime rollback seams. In isolated checkouts, reverse this batch's code changes, build and run the affected named controls at the recorded dependencies, and retain expected failing regressions separately from passing baseline controls. Do not reset shared worktrees.
4. **Validation lanes:** Direct xUnit v3 IntegrationTests process classes; EventStore.SignalR.Tests; Memories.IntegrationTests real adapter/workflow classes; Builds Python integration-helper tests; Parties lifecycle/current-owner reader tests. Qualify source Debug separately from package Release. No deployment or UI parity credit.
5. **Non-goals:** No crypto, AppHost retirement, UI consolidation, production migration or dependency-version changes. Preserve the immutable published Builds/Platform cohort and its reader constants.
6. **Parity checklist:** Preserve I5–I10/I8 tenant isolation, erasure refusals, replay and retained history. Memories inventory/provenance is I19 class-(b) reconciliation state: verify actual persisted contents and unrelated survivors. Process and CI ownership receipts are operational metadata. Delete no I18 baseline surface.

## Code Map

- Parties `tests/Hexalith.Parties.IntegrationTests/Topology/OrdinaryLauncherProcessCapture.cs` and `OrdinaryLauncherProcessLifetimeTests.cs`: existing lifetime checks, owned namespace authority and cleanup; add relationship qualification.
- Parties `scripts/test.ps1`, `.github/workflows/ci.yml`, `tests/README.md`: local topology/all selection differs from focused CI selectors.
- EventStore `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs`: legacy uncertainty and existing physical connection recovery; extend `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs` transport barriers.
- Builds `Github/run-module-integration/run_module_integration.py`: `receipt_owner` reopens checked paths; reuse held-directory/private-artifact helpers. Its `tests/test_run_module_integration.py` owns regressions.
- Memories Server `Ingestion/SourceInventoryStore.cs`, `Workflows/{AnnotationProjectionWorkflow,IngestionWorkflow}.cs`: fresh eligibility and candidate identity. `tests/Hexalith.Memories.IntegrationTests/Ingestion/SourceInventoryProducerIntegrationTests.cs` uses real adapters but mocks scheduling; reuse actual Dapr host setup in `RetryWorkflowErasureIntegrationTests.cs` for the missing boundary.
- Parties `scripts/qualify-disposable-ci.py` and `verify-current-owner-integration.py`: consumer admission and current-source binding; keep the public-package reader separate.

## Tasks & Acceptance

**Execution:**

- [x] `OrdinaryLauncherProcessCapture.cs`, `OrdinaryLauncherProcessCaptureTests.cs`, `OrdinaryLauncherProcessLifetimeTests.cs`: reproduce foreign-parent reuse in an owned namespace; bracket parent-membership reads with lifetime checks and cover true-parent, missing/malformed and independent-sibling cases.
- [x] Lifetime tests, `tests/README.md` and lane fitness tests: retain actual namespace-start denial, distinguish unsupported ordinary execution from strict qualification and failures after successful entry, and document maintained selection.
- [x] EventStore client/tests: reproduce delayed canceled ordinary/scoped joins; use existing physical recovery for indeterminate legacy operations and verify survivor delivery, replacement identity and callback revocation.
- [x] Builds helper/tests and Parties consumer tests: bind receipt/metadata validation to admitted bytes, cover swaps and unsafe entry types, then adopt the pushed owner fix. Reconcile current-source schema observations without altering public publication requirements.
- [x] Memories workflow/producer tests: exercise real scheduling and persisted historical artifacts. Fix only reproduced fresh-coverage errors; retain unknown-legacy/retry refusal and fresh failed-extraction cleanup. Record an unresolved result honestly if real reuse cannot be established.
- [x] This spec, `deferred-work.md`, `.gitlink-signoff.tsv` and dated evidence: activate exactly these entries, record commands/revisions/results, independently review, close only proved work, and push owners before parent pointers.

**Acceptance Criteria:**

- Given each matrix boundary, when its real control runs, then retained results demonstrate the stated behavior and completed owned cleanup.
- Given final source adoption, when focused consumer checks and independent review finish, then exact identities and unresolved publication/deployment gates remain explicit.
- Given reviewed changes, when exact messages pass pinned commitlint and divergence checks, then all owner commits precede the authorized parent main push.

## Implementation Notes

Parent staged and read the complete owner/root diff, verified every recorded source and receipt hash, and matched all five matrix rows to executed passing controls. Local corrections are complete; review, immutable source adoption and authorized publication remain the finalization task.

Approved by the user on 2026-11-11; re-read unchanged before implementation. Execute local edits and verification sequentially; the parent owns staging, commits and remote operations after independent review.

Pushes and existing advances are authorized. Namespace denial reproduces an EPERM failure. Owner corrections and focused runtime verification are implemented. Staging, exact revision signoff, independent review, commits and owner-before-parent pushes remain with the parent. Unknown purged/unavailable Memories histories remain explicitly unqualified.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| B1 | medium | patch | Inline recovery uses only disposal cancellation and recursive reconciliation starts a new attempt; the request token/deadline must bound both phases while uncertainty remains owned. |
| B2 | maybe-false (high unverified) | defer, group R1 | Existing/running histories are rejected. A second historical producer appearing after an absent lookup has not been exercised; the original admission also lacks exclusion against such a writer. Keep unknown-history qualification open and require an owned interleaving before claiming this boundary. |
| B3 | medium | patch | Both physical connections share JoinFinished, so the replacement can satisfy the asserted completion. Bind completion to the original connection. |
| B4 | medium | patch | New physical recovery affects every group, but the live survivor check covers only the canceled group; add an established different-group delivery witness. |
| B5 | medium | patch | The matching-admission test substitutes workflow history and duplicate scheduling. Add actual Dapr lost-ack recovery evidence and preserve the existing durable obligations. |
| B6 | medium | patch | Direct activity invocation does not exercise parent-workflow dispatch or dependency resolution at the new guard. Qualify the actual annotation parent refusal before graph/child effects. |
| B7 | medium | patch | The nested runner accepts exit zero without discovery, and an unmatched selector really returns Total 0. Require one executed control and its real observation. |
| B8 | low | patch | Four reversed-code cases throw on absent fixture comm/children reads instead of reaching attribution assertions. Supply valid remaining reads and refresh the receipt. |
| B9 | medium | patch | New swap controls change only the admitted leaf; the held-ancestor guarantee lacks its own directory-swap control for receipt and metadata. |
| B10 | medium | patch | The declared isolated Memories rollback lane failed to build; its final-dependency baseline controls remain unexecuted. Repair the owned mirror layout and retain its result. |
| E1 | maybe-false (high unverified) | defer, group R1 | Same absent-read/second-producer interleaving as B2; no actual concurrent producer or caller path established. Retained-history qualification does not certify writer drain or purged histories. |
| E2 | low | patch | The new test mutates DAPR_GRPC_ENDPOINT before FT.CREATE and before its restoring finally, so a setup exception leaks the endpoint. Move mutation/setup under the existing restoration lifetime. |
| V1 | medium | patch | Pre-verified missing adoption: the retained registered ScheduleAnnotationIngestionActivity still admits with default false and schedules directly. There is no current workflow caller, but its maintained compatibility exception needs the same demonstrated historical-identity refusal and runtime control. |
| V2 | medium | patch | Confirmed two existing determinism guards fail: scheduler start line moved and AdmitAsync gained its existing-admission argument. Update the exact inventory and ordering assertion without weakening either guard. |

## Verification

Implementation evidence is retained in `tests/five-membership-and-admission-2026-11-11/`.
Strict Parties process controls pass 37/37; EventStore source SignalR controls pass 63/63;
Memories source producer/workflow controls pass 64/64; consumer lifecycle controls pass
140/140 and the current-owner reader passes 31/31. The Builds final suite passes
73/73 after adding the separate metadata-swap case. The CI selector/publication class
passes 71/71. Final command/revision stamps
and review disposition belong to the batch receipt.

Isolated reversed capture code builds and retains 30 passing baseline controls with
seven expected relationship failures. Reversed EventStore client code builds and retains
57 passing baseline controls with six expected transport failures. Reversed Builds
admission retains unsafe-entry failures and bounded FIFO errors. The real Memories
historical workflow control failed before its correction and passes afterward. Existing
tracked histories require their original matching durable admission; purged/unavailable
histories cannot establish complete erasure and stay open. No package or deployed parity,
I16 approval, broad scan or production reconciliation is claimed.

Build and run the named classes directly, requiring nonzero discovery. Run owner Python and consumer suites. Retain red/green, rollback and cleanup receipts; validate exact commit messages and remote submodule availability before pushing.

The additional isolated Memories rollback build did not run: sibling EventStore project evaluation failed with MSB4019 on a missing Builds Props/Directory.Packages.props in the copied layout. The actual pre-correction compiled Dapr red and final source green remain independent evidence. Parent review must assess this remaining rollback requirement before closure.

Review correction supplement: `tests/five-membership-and-admission-review-2026-11-11/` records the requested transport deadline/completion, real Dapr acknowledgement/annotation admission, discovery and held-ancestor fixes. The owned Memories mirror layout is repaired: its Server and IntegrationTests build at the recorded dependencies, six selected baseline controls pass, and two reversed-code runtime regressions fail as expected. The four malformed-parent rollback cases now reach attribution assertions. This resolves the initial MSB4019 rollback blocker; final parent verification, source adoption and commit/push review remain pending.

### Parent integration verification

Owner commits were rebased onto concurrent main advances and pushed before parent gitlinks. The final owner suites pass SignalR 64/64, Memories 65/65 and determinism guards 25/25. Parties process 38/38 and CI 71/71 pass; the rebased Builds helper passes 75/75 and integrated lifecycle controller passes 168/168. All have zero skips. The additional Memories rollback build is repaired in the review supplement: six baseline controls pass and both required regressions fail as expected. Current-source observations are re-derived from exact Git revisions; published package pins and all Unvalidated I16 dispositions remain unchanged. The incoming attached-lifecycle project is inventoried with the other owner-run platform suites. Exact committed-selection checks and final publication records follow the parent code commit.

### Completion — 2026-11-11

All three independent review layers are triaged; twelve patch findings are resolved. Final verification passes 665 selected checks with zero failures/skips, including 128 committed-selection/documentation controls and 31 current-schema controls at parent code revision `22592d671259699f073666ed59d18480bdc9df13`. See `tests/five-membership-and-admission-final-2026-11-11/`. All ten dependency commits are remotely available and the five-pointer push-range gate passes. Four selected entries are closed; demonstrated retained-history Memories reuse is corrected while purged/unavailable history and the independently deferred B2/E1 interleaving stay open. EventStore and Memories main pushes reported PR/status-check rule bypasses. The completion commit records final receipts and proceeds to the already-authorized main push.
