---
title: 'Five recovery and UI conformance deferred items'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '08f43497b3f916efa0cdf9b110258f763d894bd9'
memories_source: 'b55db82c060caf917a142fdb6585fa7340b01647'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Recovery can report premature erasure success or omit searchable content; worker cleanup, workflow retention and UI conformance need qualification.

**Approach:** Complete the explicitly requested five-item batch: DW-192, DW-103, and the unnumbered entries titled “Contain test-process descendants…”, “Reconstruct complete searchable contact and identifier content…”, and “Qualify workflow-status retention…”. Verify each disposition, then commit and push on main.

## Boundaries & Constraints

**Always:** Derive terminal erasure status from committed events. Use SDK persistence and existing projection slots; keep privacy, authorization, cancellation, sequence and erasure fences. Use Fluent V5 and Fluent 2 tokens. Require canonical-matching replay for ingestion; distinguish producer retention contracts from runtime proof.

**Never:** Promote ambiguous certificates; retain personal-data recovery manifests; change public wire values, dependencies, gitlinks, owner signoffs or deployment. Do not infer physical erasure or authoritative remote inventory from consumer doubles. Preserve commands, form-context fences, typed-name confirmation and picker contracts. Process cleanup must target only owned workers.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Failed append | Successful domain result, rejected/canceled persistence | No new Verified/Erased status | Retain committed lifecycle; retry normally |
| Successful retry | Committed erasure events, duplicated delivery | Monotonic projected lifecycle and safe query result | Stale legacy status cannot promote/regress lifecycle |
| First ingestion retry | Recreated services; complete matching replay | All contact and identifier content restored | Older, partial or changed canonical source stays retryable |
| Superseded workflow | Older acknowledged attempt; newer current revision | Older obligation survives restart and cleanup resolves it | Missing/expired/unavailable status retains uncertain obligation |
| Exited parent | Descendant inherits streams | Bounded capture and descendant termination/reaping | Unrelated workers survive; failures remain bounded |
| UI sections/theme | Multiple titled sections; responsive/forced-colors modes | Fluent accordion, primary expanded; readable focus/status | Single primary forms/grids remain directly available |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs`: writes lifecycle records before EventStore append, including verification retry.
- `src/Hexalith.Parties/Queries/PartySdkQueryService.cs`: currently trusts those records first. `src/Hexalith.Parties.Projections/Models/PartyDetailSdkReadModel.cs` and `Handlers/PartyDetailSdkProjectionHandler.cs`: existing committed aggregate slot and event fold.
- `src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs`: reconciliation loses JsonIgnore search collections. Reuse canonical-target matching and complete independent replay, never a stale transient snapshot.
- `src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs`: reconciles durable older attempts. Selected Memories `docs/operations/pipeline-persistence.md` states completed history persists until explicit purge; no automatic purge feature.
- `tests/PackageTests.Shared/PackageTestProcess.cs`: direct-parent exit defeats tree cleanup. `scripts/verify-eventstore-publish.py` demonstrates Linux session/subreaper supervision.
- UI component roots: `src/Hexalith.Parties.{UI,AdminPortal,ConsumerPortal,Picker}/Components`. Style guard currently scans only UI.

## Tasks & Acceptance

**Execution:**

- [x] `PartyDomainProcessor.cs`, `PartySdkQueryService.cs`, detail model/handler above — remove optimistic lifecycle authority; project additive lifecycle metadata from committed events, retaining tombstones and old-model compatibility. Extend `tests/Hexalith.Parties.Tests/Domain/PartyDomainProcessorValidationTests.cs`, `PartyDomainEventPublicationContractTests.cs` and gateway/projection tests with failed append, cancellation, commit/retry and legacy-status cases. Assert persisted state and query results.
- [x] `PartyIndexSdkProjectionHandler.cs` — reconstruct ignored collections only from a complete contiguous replay matching canonical content and sequence. Extend `tests/Hexalith.Parties.Tests/Search/PartyMemoryCoordinationTests.cs` and `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs` for failed first submission, restart/redelivery, stale/partial replay, concurrent updates and erasure.
- [x] `tests/Hexalith.Parties.Tests/Search/PartyMemoryCleanupServiceTests.cs` and `PartyMemoryRecoveryTests.cs` — qualify superseded attempts across serialized restart, terminal status, explicit missing/expired status and outages. Document the selected retention contract and its limits in `tests/README.md`; change production reconciliation only if these tests demonstrate a gap.
- [x] `tests/PackageTests.Shared/PackageTestProcess.cs` and focused new supervisor files — establish owned descendant containment through parent exit, finite cleanup and stream draining. Extend `tests/Hexalith.Parties.Contracts.Tests/Package/PackageTestProcessTests.cs` with inherited-pipe, closed-pipe, timeout, early-exit and unrelated-worker controls; qualify supported operating systems explicitly.
- [x] UI roots above — audit every Razor/CSS file. Group sibling sections in Admin `PartiesAdminPortal`/`PartyGdprOperationsPanel`, Consumer `MyProfilePage`/`EditMyProfilePage`/`MyConsentPage`/`MyPrivacyPage` and applicable UI specimens; keep titles/toolbars and single primary regions outside. Replace legacy tokens, raw theme styling and redundant typography with component parameters/tokens. Expand `tests/Hexalith.Parties.UI.Tests/AccessibilityStyleGuardTests.cs` to all four roots; update component/browser assertions for structure, defaults and interactions.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` — record five evidence-backed dispositions; retain unrelated gates. Store bounded receipts under `tests/five-recovery-and-ui-conformance-2026-10-23/` beside this spec.

**Acceptance Criteria:**

- Given each matrix precondition, when its production path executes, then the stated outcome and durable state hold across restart/redelivery where applicable.
- Given all affected UI RCLs, when style, component and browser checks execute, then conformance, keyboard focus, authorization and existing behavior pass without exemptions or qualifying skips.

## Implementation Notes

This is an active, already rendered bmad-build implementation handoff. Delegates must not rerun the renderer or drive workflow phases; the parent owns review and final Git operations. Complete the unchecked implementation tasks sequentially.

The user approved this unchanged spec and continued implementation on 2026-10-23. The existing baseline revision is preserved.

## Verification

- Build affected test projects individually with `dotnet build -c Release -m:1`; invoke built xUnit assemblies using single-dash `-class` filters, then full affected assemblies.
- Qualify Debug/source builds using pinned module-tool-generated mappings; record package and source evidence independently. Follow the repository fallback ladder for blockers.
- Use the pinned Node/npm toolchain for `npm --prefix tests/e2e run typecheck` and the managed Playwright accessibility, portal, erasure and picker cases, including forced-colors/responsive controls.
- Run `git diff --check`, pinned commitlint and staged-diff inspection; retain exact results and validation limits.

### First implementation tranche

Committed lifecycle, complete live search replay and workflow-retention qualification are implemented. Release service/projection builds pass with zero warnings/errors; 1,155 focused service tests and all 447 projection tests pass with zero skips. Receipts and matrix mappings are in `tests/five-recovery-and-ui-conformance-2026-10-23/README.md`. The lifecycle slot omits absent metadata to preserve empty-v1 bytes; privacy tombstones cannot certify committed erasure. New search rows carry an internal digest erased with their content; legacy rows retain the documented weaker witness. Shared rebuild ignored-content reconstruction is outside this live/retry fix.

The required pre-code Aspire baseline was missed by the first delegate. A subsequent current run reached unhealthy security and waiting dependents, then its exact AppHost stopped successfully; sanitized evidence records that limitation. Source/Debug and full affected-suite verification remain pending. Pinned Node/npm are available at `/tmp/parties-resume-20261023-c_009akj/tooling/node-v24.22.1-linux-x64/bin`; prepend it for browser commands without changing dependencies.

### Parent acceptance verification

All five items have evidence-backed ledger dispositions. Package suites pass 4,278 tests; three opt-in Memories runtime cases remain unqualified. Managed browser verification passes 63 cases with zero retries; two existing DW-91 source-only cases remain skipped and are not used for this batch. The updated screenshot was inspected at original resolution and passes unchanged zero-pixel comparison; a subsequent four-route guard verifies stable Consumer region names and actual scoped heading targets. All six matrix rows map to executed passing tests in `tests/five-recovery-and-ui-conformance-2026-10-23/matrix-audit.json`.

The pinned source tool prepared an isolated canonical checkout with ten exact direct references, then exited 3 at HXR020 while building selected Platform composition (seven errors, including CA1062); HXE160 reports the follow-on evidence-write failure. The failed workspace has no surviving mapping, so focused mapped Debug builds cannot be qualified without changing the frozen dependency/gate contract. Exact `down` completed and removed the run record/workspace. Passing focused/default-package Release evidence is recorded separately; no source selectors or warning suppressions were hand-authored.

Final Git operations remain the user-authorized post-review step: validate the exact commit message with pinned commitlint, reconcile current main and push without force. They are outside the implementation checklist so review precedes publication.

### Source evidence-path correction

The first source attempt used an unsupported absolute evidence path; its HXE160 is an operator path error. A second attempt used the required repository-relative path and successfully wrote its failed-run evidence. The final result reports only HXR020 at the same selected Platform compile gate, with no resources started; exact cleanup again completed and removed the workspace/run record. This supersedes the earlier interpretation of HXE160 as a follow-on failure. All source-qualification limits remain explicit.


## Review Triage Log

| Finding | Verdict | Evidence and route |
|---|---|---|
| blind-1: tail delivery drops content witness | medium | A serialized contact update lacks prior ignored collections, removes the digest, and permits weaker subsequent replay matching. Same root cause as edge-1; patch by rejecting an incomplete content mutation before canonical persistence. |
| blind-2: idempotent replay drops upgraded digest | medium | IsIdempotentNoOp compares checkpoints, fences and serialized entries but omits SearchContentHashes; a computed historical-row witness is lost on its early return. Patch the persistence comparison and exercise restart. |
| blind-3: conflicting lifecycle backfill | false | The lifecycle input is committed EventStore history, not an arbitrary replacement snapshot. ProjectionUpdateOrchestrator reads GetEventsAsync(0), builds readable committed events and dispatches them. A fabricated conflicting immutable prefix is not a reachable committed input; matching a cleanup-redacted canonical detail would incorrectly prevent valid lifecycle backfill. |
| blind-4: no full-history recovery route | false | ProjectionUpdateOrchestrator.cs reads the aggregate from sequence zero for immediate delivery and retries; complete current replay is also exercised by the recovery controls. Partial input intentionally remains retryable. The separate poisoned content-tail case is blind-1/edge-1. |
| blind-5: external supervisor interruption | medium | An external SIGTERM can bypass Python finally; killing the calling test runner also left its command tree alive in the baseline runner. This pre-existing external-interruption gap is deferred separately; normal exit, runner-requested timeout and early error use the owned cleanup path. |
| blind-6: cleanup checks deadline only while waiting | low | Reapable descendants can keep the Python loop busy without checking its internal deadline. Patch the existing deadline check to cover every iteration and add a descendant-generation control. |
| blind-7: focus helper does not inspect actual indicator | medium | The new helper checks reserved outer-ring geometry and actual keyboard focus but only the specimen checks computed indicator paint. Patch the helper to observe the actual focused indicator and prove suppression fails. |
| blind-8: focus helper ignores intermediate clipping | low | The helper skips non-accordion clipping ancestors. Patch the traversal to inspect every clipping ancestor and include a clipping negative control. |
| blind-9: danger styles override disabled appearance | medium | The effective danger selectors also target disabled irreversible buttons; the Admin equivalent correctly restricts danger styling to enabled controls. Patch those selectors and verify disabled appearance. |
| blind-10: changed portals lack their own axe scans | medium | The blocking axe scan covers the specimen; the actual portal tests cover keyboard/geometry without axe. Patch expanded Admin/GDPR and Consumer browser cases to run the existing blocking gate without exclusions. |
| edge-1: content tail poisons canonical checkpoint | medium | Reviewer reflection harness seeded Created+ContactAdded, serialized it, then folded tail ContactChannelUpdated: checkpoint advanced with stale descriptor and absent digest, and complete replay failed canonical matching. Group with blind-1 and patch before persistence. |
| verification-1: lifecycle timestamps unverified | medium | Accepted regression-gap evidence: replacing committed UpdatedAt with UnixEpoch leaves current status and final ErasedAt assertions green. Patch persisted transition/duplicate timestamps and complete query-record assertions. |
| verification-other-1: collapsed GDPR route stays hidden | medium | Reproduced in Release browser: collapse outer GDPR section then activate toolbar GDPR navigation; URL changes but destination remains hidden and focus stays on toolbar. Patch reopening the existing item before route focus and extend navigation control. |
| parent-2: touched wrapper formatting | low | Newly inserted wrapper blocks have misaligned closing tags and redundant blank lines. Patch indentation only to keep the changed markup readable. |
| parent-3: actual-page axe exposes missing title | medium | All six actual expanded portal scans report only serious document-title failures. The requested conformance audit needs a non-empty host title; apply the existing title convention directly and retain every axe rule without exclusions. |
| parent-1: stale status-store failure injection | low | ErasureStatusHandler_StoreFailure still injects the unused legacy store and passes on an unconfigured read-model result. Patch it to throw from the actual detail read and assert bounded failure. |

All accepted current-change fixes are direct corrections to existing state comparisons, guards, component references, selectors or assertions; they add no public surface. No intent or plan loopback is required. Three review layers completed against the same staged implementation snapshot.


### Review completion

All three required independent layers reported and every finding received a triage row. Direct patches are verified by seven Release builds with zero warnings/errors, 4,290 passing .NET tests and 66 passing browser cases with zero retries/failures. The five existing optional/source-only skips remain unqualified. Six whole-page portal axe scans pass with no exclusions and no findings; zero-pixel visual comparison remains green. The external runner/supervisor interruption gap is the only new deferred entry. Complete matrix cases reran successfully, the frozen intent is unchanged, and pinned commitlint accepts the exact final message. Source Debug remains blocked by the unchanged selected Platform compiler gate. Final Git publication follows the user's explicit push-on-main instruction.
