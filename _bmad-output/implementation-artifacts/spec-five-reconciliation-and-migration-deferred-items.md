---
title: 'Address five reconciliation and migration deferred items'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
baseline_commit: '9239c531f3b0cbb4a998c419e537463b597ff14b'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Two reconciliation races leave accepted UI commands waiting indefinitely. DW-58 lacks migration rehearsal/handoff; DW-126 and the identity-helper casing report need evidence-based reconciliation.

**Approach:** Address these five entries together: recover early confirmations, resume polling after later disconnections, rehearse actor-to-SDK backfill, reconcile the obsolete toolchain-packet report, and verify the corrected helper. Review, verify, update the ledger honestly, and push task commits on main.

## Boundaries & Constraints

**Always:** Preserve user changes, including seven existing root gitlink deltas and Builds edits; keep those deltas unstaged. Commit FrontComposer fixes separately in its owning repository; root adoption remains separate. Preserve acceptance/rejection/cancellation, status mapping and shared-registration opt-in. Rebuild through existing EventStore SDK APIs from complete readable canonical history; retain actor state for rollback.

**Never:** Manually publish packages, change dependency selections, claim owner approval, execute production migration, mutate real tenant data, restore obsolete tooling solely to satisfy an old report, or substitute rehearsal evidence for deployment/publication qualification. Preserve blocked stories and retained crypto.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior |
|----------|-------|-------------------|
| Early confirmation | Confirmation during command or delayed registration | Accepted operation eventually reads Current once; rejected/cancelled commands never falsely reconcile |
| Repeated disconnect | Connected/disconnected/reconnected/disconnected | Polling resumes; completion/cancellation releases callbacks and timers |
| Backfill | Actor-only synthetic state and complete history | SDK models converge after restart; erased suppression, unrelated tenants and actor bytes survive |
| Unsafe backfill | Incomplete/unreadable history or competing writes | Explicit refusal without overwriting authority |
| Stale reports | Current tracked tooling and helper | Reproducible disposition; missing proof stays explicit |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties.UI/Services/OptimisticReconcile.cs` subscribes after issuing commands and checks connectivity once. `PartiesProjectionSubscription.cs` registers asynchronously; moving Subscribe alone is insufficient. `ProjectionFreshnessFallback.cs` supplies injected timing/options but returns on reconnect.
- Producer counterpart: `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Services/Feedback/FcOptimisticReconciler.cs`; preserve its transport-neutral callbacks.
- Retained tests: `tests/Hexalith.Parties.UI.Tests/OptimisticReconcileTests.cs`, `FakeProjectionStream.cs`, `ManualTimeProvider.cs`. Producer tests: `tests/Hexalith.FrontComposer.Shell.Tests/Services/FcOptimisticReconcilerTests.cs`.
- `src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs` and `PartyIndexSdkProjectionHandler.cs` own rebuild callbacks. Reuse SDK dispatch/store machinery in `tests/Hexalith.Parties.Tests/Projections/PartyProcessingGenerationRecoveryTests.cs`; do not introduce generic persistence plumbing.
- DW-126's named validator/schema are absent from current operative files. Inspect tracked CI/build authority before recording supersession. Historical receipts retain their identities.
- `tests/Hexalith.Parties.IntegrationTests/Identity/TestOnlyIdentityHistoryCustody.cs` already constructs UnitStatus positionally, last changed in `17290ed694801d3537a62b04b47c10ebab6e775d`.

## Tasks & Acceptance

**Execution:**
- [x] Reconciliation files above -- fix readiness/re-read recovery and continuing connectivity observation in retained and producer paths; own asynchronous lifetimes and serialize completion.
- [x] Reconciliation tests above -- cover early confirmation, delayed registration, repeated connection changes, duplicate signals, transient reads and cancellation with deterministic time and bounded waits.
- [x] `tests/Hexalith.Parties.Tests/Projections/PartyLegacyReadModelBackfillTests.cs` -- exercise actual SDK rebuild protocol from actor-only synthetic state; inspect persisted detail/processing/index after recreation, refusal cases and survivors. Keep support types in separate files.
- [x] `docs/read-model-migration.md` -- document history/key prerequisites, writer drain, rebuild ordering, verification, rollback and remaining deployment gate; link from `docs/deployment-guide.md`.
- [x] `_bmad-output/implementation-artifacts/tests/five-reconciliation-and-migration-2026-10-28/` -- retain exact commands/results, full source identities and operative-toolchain/helper evidence.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- reconcile these five entries. Keep DW-58 open until deployed cutover proof; source fixes do not close FrontComposer publication/adoption gates.

**Acceptance Criteria:**
- Given an accepted command and its only early confirmation, when registration is delayed, then Current is reached once without a second signal.
- Given repeated connection changes, when disconnect recurs, then polling resumes; completion/cancellation leaves no owned callbacks/work active.
- Given complete synthetic history, when SDK backfill completes and handlers restart, then persisted models agree, erased parties stay suppressed, unrelated tenants survive and rollback bytes remain intact.
- Given unsafe history or concurrent authority changes, when backfill runs, then publication refuses without overwriting authoritative state.
- Given current tracked tooling/helper, when verification runs, then obsolete reports receive evidenced dispositions without certifying external gates.

## Implementation Notes

- Workflow phase: step-03 implementation permits no push or remote operations. Leave task changes for the primary agent to audit, stage, review and commit; pushes occur after the review/final verification phase.

- Required runtime baseline: exact-target Aspire start succeeded; describe showed security Unhealthy (TLS health handshake EOF), with EventStore/Parties/Tenants/UI Waiting. Exact-target stop succeeded before implementation. This environment limit is separate from focused verification; see the batch baseline receipt.

- Initial Parties revision: `9239c531f3b0cbb4a998c419e537463b597ff14b`. User authorized preserving existing submodule changes and continuing the five-item batch. No implementation or deployment occurred during planning.
- Fresh IntegrationTests Debug build passes with zero warnings/errors; PartyIdentityPersistedVerifierTests passes 11/11, zero skips. Preserve receipts during implementation.

- Reconciliation waits for transport readiness before command dispatch and buffers only confirmations observed after dispatch starts. A connected old model marked Current without projection evidence stays pending. Offline connection failures retain the polling fallback. The producer readiness callback is additive and optional for synchronous transports; asynchronous handles and reads are drained on every exit.
- The actor-only rehearsal exposed an existing shared rebuild plan identity defect: rereading the just-published ETag changed CreateOnly to Match on terminal Verify. New Party index candidates now retain their initial conditional concurrency policy. Historical wrapped/raw candidates without that field retain the legacy compatibility path; an old refused session must be investigated or safely aborted/restarted, never reclassified as verified. Current erasure fences and SDK conditional publication remain enforced. Workload recreation retains the external backend and its provider ETags; restoring a backup with fresh ETags is not modeled as a process restart.

- The primary retained UI broad run recorded 428 passing cases, one existing skip and four baseline-test failures: `ActualHost_SharedUiRegistrationRequiresTheStartupOption` for null/false/true and `SelectedDependencyModeUsesOnlyAnAvailableRegisteredCapability`. Those assertions incorrectly required shared types to be absent from the selected NuGet Shell 4.7.1. The output Shell DLL and the selected package DLL both have SHA-256 `5347247d64f9a348dd0a0418711fb785370100133cd56f06dc5959e97756e80c`, confirming package bytes rather than a mapped-source mix-up. Package-mode tests now assert any available shared reconcile/download services remain unregistered for every startup-option value and assert retained local freshness markup. Source-mode registration assertions and production registration are unchanged. Preserve the failed broad-run receipt in `tests/five-reconciliation-and-migration-2026-10-28/primary-verification.json`, with its original `primary-retained-ui-tests.txt` and XML; focused corrections do not replace that failed history.

## Spec Change Log

- All execution tasks and acceptance matrix rows are verified. `audit-matrix.py` independently checks passing executed cases, log/XML hashes and implementation source hashes. Helper validation passes 11/11; five disposition checks pass. DW-126 is reconciled as inapplicable current tooling; the named identity helper is already fixed. Two source reconciliation fixes are complete while publication/adoption gates stay open. DW-58 retains its explicit deployed-cutover gate.

## Review Triage Log

| Finding | Verdict / route | Evidence |
| --- | --- | --- |
| BH-01 | medium / defer | Tenant/projection-only signals and Current-only callbacks already accept unrelated notifications after dispatch in the baseline; command/aggregate causality is a pre-existing transport/query contract gap, not established by the new buffer. |
| BH-02 | medium / defer | Both baseline and current polling inspect connectivity at interval ticks; an entire outage between ticks is not observed. Existing IProjectionStream exposes only IsConnected. Record this distinct pre-existing transition/catch-up gap. |
| BH-03 | medium / defer | Baseline connected reconciliation also waits for another notification after a stale/failed read. Existing tests explicitly require a second confirmation. A durable retry-after-notification protocol is pre-existing work. |
| BH-04 | medium / patch | Subscribe now precedes dispatch outside the command failure block; a synchronous error can leave optimism without a command or feedback. Catch acquisition failure and preserve caller-cancel semantics in both implementations. |
| BH-05 | medium / patch | The producer previously guarded callback-driver invocation with IsConnected. Its new always-running driver must still wrap actual reads with that existing guard, so a valid older direct-poll driver cannot reconcile old Current while connected. |
| BH-06 | low / patch | Both cleanup paths invoke synchronous and asynchronous disposal for the same handle. Prefer async when available, otherwise sync; this is a direct redundant-call correction. |
| BH-07 | medium / patch | An exception from cancellation/disposal can bypass later polling/read drains. Protect each later drain with nested finally blocks while preserving fault propagation. |
| BH-08 | medium / defer | The existing production adapter forwards to EventStoreSignalRClient.SubscribeAsync without a cancellation parameter; that client invokes JoinGroup without cancellation. The same applies to removal/LeaveGroup. A stalled producer call cannot be safely abandoned while guaranteeing no late registration. Retain owned draining and explicitly defer upstream cancellable transport support; no bounded transport-shutdown proof is claimed. |
| BH-09 | medium / patch | git check-ignore confirms required receipt logs are ignored and not staged. The clean-checkout audit genuinely lacks inputs; retain exact bytes under tracked names and update receipt paths. |
| BH-10 | low / patch | The recorded regeneration command names a temporary generator and retained input not yet archived. Retain their exact bytes/hashes and document reconstruction at recorded source roots, distinct from fresh resolver acceptance. |
| BH-11 | medium / patch | The matrix checker searches the union of lanes; dropping mapped UI can retain all method matches. Require each named lane, exact cases and required theory variants. |
| BH-12 | medium / patch | Disposition Pass values are trusted without comparing recorded operative/helper input hashes. Check those current inputs before accepting the stale-report row. |
| EC-01 | medium / defer | Same verified pre-existing short-outage sampling root cause as BH-02; group into one new deferred entry. |
| EC-02 | medium / patch | An unrequested startup OperationCanceledException while disconnected bypasses offline registration, unlike the existing IOException fallback. Filter on the lifetime token and connection state rather than exception type. |
| EC-03 | medium / patch | Same acquisition-failure root cause as BH-04 in producer and retained services; preserve rollback/failure feedback before any dispatch. |
| EC-04 | medium / patch | Same lane-union acceptance-audit defect as BH-11; require mapped and retained coverage independently. |
| VG-01 | medium / patch | Pre-verified gap: fake unsubscription completes synchronously and producer handles are only IDisposable. Add controlled pending async removal to prove ExecuteAsync waits for teardown. |
| VG-other-01 | medium / patch | Same independently reproduced missing-log root cause as BH-09; a checkout-shaped audit fails FileNotFoundError. |
| Primary-UI-01 | medium / patch | Four unchanged baseline assertions require package APIs to be absent. Selected Shell 4.7.1 contains them and built DLL hashes match. Preserve source assertions and check actual package registration/local-rendering fences; full UI now passes in both modes. |
| VG-followup-01 | medium / patch | Owning-index replay differed only at the preserved dirty Builds catalog. Archive its exact observed bytes as historical evidence; audit the archive for that input and current bytes for all other inputs. Five normal/optimized controls pass with owning-index catalog reconstruction. |
| Primary-audit-01 | low / patch | A permanent index-versus-history inequality would fail after a legitimate catalog commit. Remove that assertion while retaining actual index-byte reconstruction and dated difference evidence. |


- Review remediation completed for BH-04/05/06/07/09/10/11/12, EC-02/03/04 and VG-01/VG-other-01. Final primary verification passes full retained UI 432 with one named compact-package skip, full mapped UI 433 without skips and producer 27; three clean Debug builds, audit negative controls in normal/optimized Python and warning guard pass. Earlier projection 218/one legacy-SDK skip, backfill/recovery 47 and identity 11 remain source-bound. Four pre-existing transport limits are explicitly deferred. Original baseline package-absence assertion failures and corrected adoption-fence coverage are retained independently.

- Final acceptance: independent recheck confirms both original verification gaps resolved. The final historical-catalog correction passes all five controls in normal and optimized Python; audit checks source/log/XML hashes and owning-index reconstruction. Runtime source hashes are unchanged from primary passing builds/tests. User explicitly authorized pushing completed task commits to main; this overrides the skill default against automatic push. Existing root gitlink deltas and Builds edits remain unstaged; producer/root adoption and deployed cutover remain separate.

- Publication: reviewed FrontComposer task commit `5af87f9e5cc8db16896fc97d19b6a828b2ed0d0d` was rebased onto non-overlapping upstream `28d2c7f47849bcfba19501f7c18cd9f67f0fc61c` with recursion disabled, rebuilt cleanly and retested 27/27. Task source hashes are unchanged; producer main push is verified. The final owning-index replay reconstructs all 94 audit inputs with checkout filters and passes every matrix row using the explicitly archived observed catalog. Parent root gitlinks remain unstaged.

## Verification

- Build affected projects individually in Debug with warnings enforced. Invoke built xUnit v3 assemblies with single-dash class filters for reconciliation/fallback/backfill/persisted identity cases.
- Qualify retained package and mapped producer behavior separately; use repository fallback checks and record exact source-graph blockers without weakening gates.
- Run `bash scripts/check-no-warning-override.sh` and `git diff --check`; inspect task-only staging and preserved user changes.
- Validate exact commit messages with each owning repository's pinned commitlint, retain success evidence, fetch/inspect divergence, and push task commits to main.
