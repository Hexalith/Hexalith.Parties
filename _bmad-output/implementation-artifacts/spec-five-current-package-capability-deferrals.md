---
title: 'Qualify five deferred boundaries on current packages'
type: 'bugfix'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'f45d0f6a6ecaf75eec8dc16b8df83840e880a8c5'
context: ['AGENTS.md', 'docs/build-gate.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Current package selection has advanced beyond two capability allowlists: FrontComposer 6.0.0 uses retained local feedback/reconciliation, and Memories 4.0.0 disables the existing revision-aware refresh branch. Five open deferrals also retain qualification receipts for older packages.

**Approach:** Qualify the unchanged EventStore 5.1.0, Memories 4.0.0 and FrontComposer 6.0.0 cohort at five existing boundaries. Enable exact package capabilities only after their behavior passes. Review and verify the resulting changes, then commit and push to main as requested.

| Deferred item | Deliverable in this batch |
|---|---|
| DW-100: shared feedback and reconciliation | Qualify and enable the actual FrontComposer 6.0.0 implementation; preserve unregistered local rollback |
| Unchanged-canonical-URI Memories refresh | Qualify and enable exact 4.0.0 revision-aware REST requests and changed-content behavior |
| DW-82: empty-mapping inventory | Verify bounded, scoped inventory reconciliation and incomplete-coverage refusal on the selected package |
| DW-168: terminal cleanup | Verify pending ingestion/deletion, recovery and persisted erasure through the owned selected-package runtime |
| DW-50: bounded processing history | Requalify bucket/recovery/cursor/export behavior against EventStore 5.1.0 |

## Boundaries & Constraints

**Always:** Keep exact version admission, unknown-package refusal, local rollback, tenant isolation and durable cleanup obligations. Preserve historical receipts. Distinguish consumer, producer-source, package-runtime and deployed evidence. Use run-with-temp with disposable outputs beneath TMPDIR; retain useful results outside it.

**Never:** Upgrade dependencies, invent owner acceptance, attest production inventory or writer drain, mutate deployments, weaken validation, or mark an outer deferred item done while its external gates remain. Memories 4.0.0 predates the newly adopted source per-unit authority fix; package refresh evidence cannot qualify that newer contract. Existing completed DW-196/197/198 are excluded.

## I/O & Edge-Case Matrix

| Given | When | Then |
|---|---|---|
| Qualified exact 6.0.0, registered shared services | Reconcile feedback | Actual producer implementation handles readiness, witnesses, retry and connection generations |
| Unregistered or unknown package | Select feedback | Existing retained fallback remains available |
| Qualified exact 4.0.0 | Refresh changed content | REST carries stable revision/refresh identity; runtime proves current content at the same source URI |
| Empty/partial mappings or uncertain terminal evidence | Reconcile cleanup | Retain obligations; require scoped complete inventory and physical terminal deletion |
| Large processing history or interrupted recovery | Read/rebuild with 5.1.0 | Bounded storage, restart recovery, scoped cursors and complete authorized exports remain correct |

</frozen-after-approval>

## Code Map

- `Directory.Build.targets`: ConfigureFrontComposerFeedbackCandidate omits exact 6.0.0 from both HFC branches; ConfigureMemoriesSourceCompilation omits exact 4.0.0. No version properties need changing.
- `tests/Hexalith.Parties.Ci.Tests/AspireRuntimePreparationTests.cs`, `MemoriesRefreshCompilationTests.cs`: real isolated MSBuild capability/metadata evaluations.
- `tests/Hexalith.Parties.UI.Tests/{OptimisticReconcileTests,DataFreshnessIndicatorTests,StatusLiveRegionTests}.cs`: producer execution and registered/unregistered feedback controls; portal suites cover consumers.
- `tests/Hexalith.Parties.Tests/Search/`: PartyMemoryRestClientRecoveryTests has an always-compiled actual REST/capability control; PartyMemorySourceInventoryTests and recovery classes inspect serialized state.
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartyMemoryOwnedRuntimeTests.cs`: supervised three-target runtime and child; retain compiler/cohort identity validation and owned teardown.
- `tests/Hexalith.Parties.Tests/{Projections/PartyProcessingGenerationRecoveryTests,Gateway/PartySdkQueryHandlerTests}.cs` and projection test project: existing bounded history, restart, cursor and export controls.
- `_bmad-output/implementation-artifacts/deferred-work.md`: five selected entries retain independent deployment/history/approval gates.

## Tasks & Acceptance

**Execution:**

- [x] `Directory.Build.targets` and CI capability tests: prove exact current API compatibility, admit the two qualified selections, and add nearby unknown/prerelease/metadata negative cases.
- [x] UI/portal suites: execute actual registered shared reconciliation plus local rollback; repair only compatibility failures exposed by this adoption.
- [x] Search suites and owned runtime: execute refresh, inventory and delayed terminal-erasure controls with current package identity; retain persisted end-state and unrelated-survivor checks.
- [x] Processing projection/query suites: execute current-package bounded history, interruption, cursor and export controls; retain exact per-test results.
- [x] Ledger, tests README and dated receipt: record all five dispositions, commands, package provenance, assembly hashes, cleanup and remaining external gates.
- [x] Complete independent review, resolve findings, validate the exact commit message and inspect divergence; requested commit/push and remote verification are the terminal publication operation.

**Acceptance Criteria:**

- Given the selected current cohort, when the five boundary lanes execute, then each has nonzero passing coverage and no targeted skip counted as qualification.
- Given an unqualified version or absent registration, when capability selection runs, then existing refusal/fallback behavior remains.
- Given only local qualification, when the ledger is updated, then external ownership, historical completeness and deployment requirements remain explicitly open.

## Implementation Notes

User approved and continued on 2026-11-15. The implementation step prohibits remote operations; independent review and approved publication are parent follow-through after implementation. Root and all root-declared submodules were clean on main. Submodules behind their remote tracking branches are left unchanged. Two independent read-only investigations confirmed the missing current capability selections and that no five entire open ledger entries can honestly be closed through local tests alone. This proposal completes the locally executable portions of five actual open entries.

## Spec Change Log

- 2026-11-15 implementation clarification: the retained SDK 4.2 catalog view
  evaluates EventStore 4.2.0/Memories 3.0.27 by default; standalone fallbacks
  are 5.1.0/4.0.0. Qualification explicitly selects the frozen approved
  5.1.0/4.0.0/6.0.0 cohort without changing dependency properties. Initial
  old-catalog runs are separate evidence.
- The retained current Memories source host includes the newer per-unit
  authority contract. Its full mixed-host run refused source refresh; the
  exact 4.0.0 nuspec source-host snapshot is a separate disposable test input,
  with the published libraries and validation guards unchanged.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH1 | medium | patch | Counter group can be empty after leave/replacement; direct delivery plus a subsequent processing fence must exercise revocation. |
| BH2 | medium | patch | Latest connection identity does not observe original connection termination; add the server disconnection witness. |
| BH3 | low | patch | TrySetResult hides repeated survivor invocation; count it after an ordered delivery fence. |
| BH4 | medium | patch | MSBuild Boolean conditions accept case variants, while the new metadata comparison does not; use Boolean parsing. |
| BH5 | low | patch | Added confirmation/checkpoint awaits have no local deadline; removing active repair stimuli and bounding observation avoids this test hang. |
| BH6 | medium | patch | New subscriptions can invoke membership repair themselves; observe autonomous replacement and survivor membership without post-cancel mutations. |
| BH7 | low | patch | Prepared manifest references per-project receipts omitted from the public packet; retain available original sanitized receipts or state their unavailable custody explicitly. Runtime guard execution and hashes remain retained. |
| BH8 | low | patch | Older-default passing XML lacks corresponding executed assembly/package identities; retain them during its focused rerun. |
| BH9 | low | patch | Runtime host snapshot instructions omit the checkout/overlay setup; document executable setup against immutable source revisions. |
| BH10 | low | patch, parent finalization | Parent added matrix audit after implementation sealing; regenerate complete packet checksums after review additions. |
| VG1 | medium | defer | Existing browser asset qualifier admits only5.0.0 and blocks the newly selected6.0.0 download/cancellation tests before browser assertions. Component evidence substitutes JS; no browser qualification is claimed. Retain this pre-existing qualifier blocker in the open browser/G4 work. |

Edge-case layer returned no findings. Every finding was classified before grouping. BH1/3 share the delivery-observation defect; BH2/5/6 share the recovery-observation defect. Remaining corrections are independent. No production capability gate or package selection changes during review.

## Verification

Use run-with-temp for build/test jobs. Build individual test projects; execute built xUnit assemblies with single-dash class/method selectors and fresh XML. Evaluate capability controls without restoring unknown versions. Prepare runtime hosts after builds using the existing script and bind executed producer/child hashes before opt-in runtime execution. Inspect the available AppHost baseline before code changes; record exact blockers and follow the narrow validation ladder without weakening guards. A failed runtime gate remains a blocker to that deliverable. Run `bash scripts/check-no-warning-override.sh` and `git diff --check` before publication.


Implementation verification on 2026-11-15: 55 capability, 465 UI, 359 Admin,
99 Consumer, 1,264 service/search/history and 218 projection controls pass with
zero skips for the explicitly selected cohort. The older-default transport
compatibility control passes 3/3 independently. The package-matched full owned
runtime passes wrapper 1/1 and actual child 1/1, zero skips, with six native
persisted-stream/replay observations and completed owned teardown. Exact package
repository metadata, 528 host-source file hashes, ten identical tested overlays,
executed assembly hashes and per-test outcomes are retained in
`tests/five-current-package-capability-deferrals-2026-11-15/`. The initial
zero-discovery, old-default, transport-compatibility, framework-authority and
mixed-current-source runtime attempts retain their independent dispositions.
Independent review and publication remain parent follow-through; implementation
performed no remote operations, staging or commits. All five outer ledger entries
remain open for their explicit external gates.

Parent audit: all five frozen matrix rows have nonzero executed passing coverage.
`tests/five-current-package-capability-deferrals-2026-11-15/matrix-audit.json`
binds covering test names to retained outcomes; all ten tested code/configuration
overlays still match the final worktree. Publication is terminal follow-through.

Final review: all eleven findings were classified individually. The ten
transport/evidence corrections are resolved; the existing5.0.0-only browser
asset qualifier is explicitly deferred, with no browser/G4 acceptance claimed.
The independent final full UI run passes465/465, zero skips; both current and
older transport controls pass3/3. Review changed only UI test/fixture inputs,
leaving production and owned-runtime input bytes unchanged. The original13
per-project preparation receipt files were not retained; their custody limit is
disclosed without reconstructed substitutes. Original manifest/runtime proof
and exact reproducible host overlays remain available.

Commitlint21.2.4 accepted the exact publication message with zero problems.
Main has no remote divergence and all ten declared submodules are clean with
no unpublished commits. Requested commit/push follows as the terminal operation.
All five local deliverables are complete; their outer external gates stay open.
