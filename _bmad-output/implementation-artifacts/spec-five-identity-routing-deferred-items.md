---
title: 'Complete five identity and routing deferrals'
type: 'bugfix'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: d55058ddaa4bfc94c47b799e5f5d9e9eff8dce77
context: ['AGENTS.md', 'references/Hexalith.AI.Tools/hexalith-llm-instructions.md', 'references/Hexalith.AI.Tools/hexalith-git-instructions.md', 'references/Hexalith.AI.Tools/hexalith-ux-instructions.md', 'docs/development-guide.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Identity admission/folding can exceed query deadlines, abandoned providers accumulate work, the privacy inventory omits identity contracts, and published routing retains obsolete source gates. A completed dependency reconciliation also remains open.

**Approach:** Complete five items together: DW-139, DW-145, DW-174, DW-175, and the unnumbered dependency-reconciliation entry from `spec-dw-72-production-publish-environments.md`. Verify that existing reconciliation with fresh binaries. Review, validate, commit and push to main as requested.

## Boundaries & Constraints

**Always:** Preserve caller-cancellation precedence, fail-closed outcomes, source positions, independent finite history custody and profile encryption. Inventory opaque linkage identifiers as sensitive in context; distinguish transient authorization/proofs from profile fields. Preserve historical receipts and owner signoffs.

**Never:** Change wire contracts, retention policy, cryptographic annotations, dependencies, gitlinks or deployments. Never claim abandoned work was terminated, create unbounded queues, fabricate approvals, or close unrelated G4/topology prerequisites.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior | Error handling |
|----------|-------------|-------------------|----------------|
| Identity success | Valid authority/source | Existing evidence and exact positions | Preserve denials |
| Deadline | Stalled admission/fold/provider | Bounded unavailable, no late evidence | Caller cancellation retains original token |
| Saturation | Repeated noncooperative work | Finite shared outstanding work | Capacity acquisition consumes query budget |
| Recovery | Abandoned work/callbacks finish | Capacity available again | Observe late faults; release once |
| Package routing | Selected Shell 4.7.0 | Reserved segments and default-tab routing enabled | Preserve authorization |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Queries/`: `PartyIdentityQueryService.cs` invokes admission and both folds between deadline checks. `PartyIdentityQueryDeadline.cs` bounds provider waits but creates unrestricted tasks; preserve cancellation-callback isolation and fault observation.
- `PartyIdentitySourceFold.cs` and `RetainedHumanActorHistoryFold.cs` in that directory own replay loops. Preserve public signatures; add internal cooperative checking.
- `tests/Hexalith.Parties.Tests/Gateway/PartyIdentityQueryHandlerTests.cs` provides controlled clocks, authority/source/custody doubles and cancellation controls.
- `tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs` distinguishes purpose-custodied history from profile encryption but omits identity types.
- `src/Hexalith.Parties.UI/`: `Program.cs`, `Hexalith.Parties.UI.csproj`, `Components/Pages/PartiesOverview.razor` and its codebehind retain `HFC_ROUTE_OPTIONS`. Published Shell 4.7.0 exposes the required route and tab APIs, confirmed by package inspection.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs`, `PartyIdentityQueryDeadline.cs` and both mapped folds -- cover initial/final admission and replay with the whole-query budget (DW-174).
- [x] `src/Hexalith.Parties/Queries/PartyIdentityQueryWorkLimiter.cs` and mapped deadline -- add bounded shared admission; retain leases until abandoned work and associated cancellation callbacks finish (DW-175).
- [x] Mapped `PartyIdentityQueryHandlerTests.cs` -- test synchronous stalls, folding cancellation, saturation/recovery and late faults for current/historical queries without timing sleeps.
- [x] Mapped `PersonalDataInventoryTests.cs` -- inspect identity commands, queries, events and nested evidence/state/provisioning records; classify every property with context-specific rationale and enforce completeness (DW-145). Keep production annotations unchanged.
- [x] Mapped UI files -- remove obsolete gates/dictionary, pass tab parameters directly, and verify Shell route discovery (DW-139).
- [x] `tests/Hexalith.Parties.UI.Tests/PartiesOverviewTests.cs` and `PartiesUiHostCompositionTests.cs` -- test actual host reserved segments, module/default-tab routes and role restrictions against packages.
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- append current routing proof, preserving historical identities/approvals.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- close only the five verified items; retain exact commands/results under `tests/five-identity-routing-deferred-items-2026-10-22/`.

**Acceptance Criteria:**
- Given either identity query, when admission/folding/providers overrun, then unavailable releases no identity evidence and cancellation precedence remains intact.
- Given exhausted work capacity, when queries repeat, then outstanding work stays bounded and completion restores capacity.
- Given identity contracts, when inventory runs, then every property has classification/rationale without persistence changes.
- Given published dependencies, when the UI starts, then reserved routes and module tabs work without source defines and authorization remains enforced.
- Given fresh binaries, when dependency fitness runs, then committed selections and maintained documentation agree before ledger closure.

## Implementation Notes

Approved by the user on 2026-10-22. Before implementation, fast-forwarded the clean tracked tree to the existing origin/main tip d55058ddaa4bfc94c47b799e5f5d9e9eff8dce77 (one AdminPortal build setting); preserve that change. The implementation handoff implements and verifies only; the parent owns independent review, staging, commit and authorized push after verification. Do not invoke bmad-build recursively or perform remote operations in the handoff.

Initial clean main: `9cfeb6c645317a6aff3aa3812822bb6b92c1ec86`. The user explicitly requests five items; preserve the batch. Runtime concurrency plus routing merits dispatch. No unresolved product choice or destructive operation is proposed. DW-17 belongs to Memories and is excluded. Existing Debug dependency-test binaries are stale; rebuild them.

## Spec Change Log

## Review Triage Log

- B1 — medium, patch: final historical admission can move UTC behind source/destruction observations after the bounded validator passed. The old validator used the post-admission instant. Restore the terminal lower-time bound; reject backward movement from the final validation instant and cover source/certificate cases.
- B2 — medium, patch: PartyIdentityCommandResult lives in Contracts.Results and its serialized ResultPayload is omitted by namespace-based roots. Include its inherited public result properties using concrete inventory context and explicit rationales.
- B3 — low, patch: nested discovery ignores generic collection arguments and value types, so a newly classified collection can omit its nested identity record. Extend the existing completeness traversal through element/generic types with a visited set; no production surface changes.
- B4 — medium, defer: CurrentValue is accessed synchronously before deadline creation and by PolicyIsCurrent in both the baseline and candidate. A noncooperative options monitor can still block setup/revalidation. This predates admission/fold changes; retain as distinct remaining budget work.
- B5 — medium, defer: the current and historical LINQ interval scans remain outside the work boundary exactly as in the baseline. They can consume the remaining budget on a large history before the next check; this is separate pre-existing selection work.
- B6 — low, reject additional complexity: SDK validation is not internally cancellable, but its source/payload bounds are checked (10,000 positions, 16 MiB), it runs inside a bounded worker, and the fold checks cancellation before validation and before transitions. Remaining SDK work is explicitly bounded rather than forcibly terminated. A new producer API is disproportionate without an ordinary-use delay reproduction.
- B7 — low, reject additional complexity: the existing isolated one-slot tests exercise both query kinds, denial before authority, both cleanup completion orders, and recovery; default service wiring is explicitly asserted against Shared. TryAcquire delegates atomic admission to SemaphoreSlim.Wait(0). No concurrent lease defect or divergent service wiring was identified; extra scheduling scaffolding would mainly retest that primitive.
- B8 — low, patch: wrong-role HTTP checks should also exclude target area content and observable fixture reads, not only require Forbidden and absence of overview tabs. Strengthen the existing assertions without changing production authorization.
- B9 — low, patch: the unknown-tab bUnit case mounts PartiesOverview directly; its name must not imply the real generic route selected it. Add actual host/router coverage of Parties aliases and preserved unknown-route behavior, keeping the existing generic RouteUnavailable behavior where no generated Parties manifest exists. Do not introduce a new redirect or registration policy.
- B10 — low, reject additional complexity: saturation has no dedicated metric, but the existing Unavailable result is correct and the fixed 16-query policy is documented in the evidence. New telemetry plumbing is an optional diagnostic improvement, with no demonstrated incorrect result or leaked work; it is not a direct correction to this implementation.
- E1 — medium, patch, grouped with B1: SDK validator checks both stream and destruction-certificate observation lower bounds; repeating only ValidUntil after final admission loses them under backward UTC movement.
- V1 — medium, patch: pre-verified caller-path gap. The existing historical replay stall occurs at initial validation, while direct fold cancellation and five-second terminal expiry tests do not exercise deadline expiry during fold/terminal validation. Gate those later enumerations separately and require unavailable before releasing the gate.


- E2 — medium, patch: independent follow-up cleared the production clock guard but found a destruction fixture that failed fold-time validation before final admission. The fixture now starts valid, refreshes its destruction observation during controlled custody, and asserts admission call 3 before the unavailable verdict. Both clock cases pass in the 267-case focused class.
- B9 evidence correction — the host does have a manual lowercase `parties` manifest in Composition/PartiesUiFrontComposerRegistration.cs; the earlier case-sensitive/absence hypothesis was false. Actual HTTP tests now preserve explicit aliases, the registered generic landing for an unknown Parties tab, and RouteUnavailable only for an unregistered module. Component fallback coverage is labeled separately; no routing policy changed.
- P1 — low, patch: array types inherit their element namespace, so graph traversal must inspect the element without inventorying the array as a record. The helper now continues after element traversal and the existing container test includes a plain class-array exclusion control; inventory 9/9 passes.

## Verification

- Establish the exact owned Aspire baseline before code edits. Use the pinned module launcher for Debug/source checks; document exact blockers before focused package fallback.
- Build affected test projects individually with warnings as errors; invoke mapped focused xUnit v3 classes directly, then affected assemblies where justified.
- Build `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release` against published dependencies for DW-139.
- Rebuild `Hexalith.Parties.Tests`; run `*MaintainedTechnologyTablesMatchCentralPackageCatalog` and `*FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph`.
- Run `bash scripts/check-no-warning-override.sh`, `git diff --check`, independent review and pinned commitlint against the exact message file. Fetch and inspect divergence before pushing main.

## Implementation handoff — 2026-10-22

- Implemented DW-174/DW-175 with one finite shared work lease per query, bounded synchronous admission/replay, cooperative fold cancellation, isolated provider callback cancellation and late-fault observation. Terminal historical validation precedes final authority admission; certificate, custody and policy checks use the release instant.
- DW-145 inventories identity roots and nested binding/provisioning/response/admission/custody records with explicit per-property classification and rationale. Production annotations, wire contracts and retention policy are unchanged.
- DW-139 removes obsolete source gates/dictionary and passes route-backed tab parameters directly against the selected published Shell 4.7.0. Actual host endpoint/module/reserved-segment/role and overview tab checks pass. The routing matrix has an appended current observation; historical approvals and unrelated G4/topology gates are preserved.
- Fresh dependency-reconciliation fitness checks pass 2/2; no dependency or gitlink changes were needed. Closed only the four numbered items and the exact DW-72 unnumbered reconciliation entry.
- Final builds of the three affected test projects and standalone UI Release build pass with zero warnings/errors. Focused identity 262/262, inventory 8/8, routing 42/42; full service 2416/2416, Contracts 210/210 and UI 404/404 pass. The four final wrong-role HTTP cases were added after the full UI run and pass in the rebuilt focused suite; UI runtime code is unchanged. No failures/errors/skips/not-run are reported.
- The pinned module launcher was attempted before code edits. The existing prepared isolated Dapr home cleared HXR011, but startup then failed HXP020: the mapped Parties source project is unavailable; no resources started. Debug/source and integrated topology checks remain blocked. Focused Release/package fallback is recorded separately.
- Exact commands/results and implementation hashes are retained under `tests/five-identity-routing-deferred-items-2026-10-22/`. Warning guard and CRLF-aware whitespace check pass. Independent review, exact-message pinned commitlint, staging, commit and authorized push remain parent-owned. No remote operations were performed in this handoff.

## Review patch handoff — 2026-10-22

- All three review layers completed. The terminal-clock patch has independent follow-up confirmation; historical deadline cases gate initial validation, fold validation, fold replay and terminal validation independently.
- The identity result wrapper includes inherited properties with concrete-context classifications. Nested inventory traversal handles arrays, collections, nullable value records and visited types; production annotations remain unchanged.
- Final focused implementation-side results: identity 267/267, inventory 9/9 and routing 46/46. Parent full verification follows; no broader implementation-side reruns were requested.
- Two distinct pre-existing budget gaps (options access and post-fold selection) are appended to the deferred ledger. Low-complexity verification corrections are patched; optional telemetry and primitive-concurrency scaffolding are rejected with recorded rationale.

## Final verification — 2026-10-22

- Parent rebuilt all three affected test projects and ran all three final assemblies after review corrections: service 2421/2421, Contracts 211/211, UI 412/412. Zero errors, failures, skips or tests not run; builds have zero warnings/errors.
- All matrix rows have passing runtime witnesses in the full suites and focused correction receipts (identity 267/267, inventory 9/9, routing 46/46). Terminal UTC lower-bound preservation has independent follow-up confirmation.
- The approved frozen block is unchanged. Original and final implementation hashes/receipts are distinguished. Source/topology remains blocked by the recorded HXP020; no resources started.
- The exact Conventional Commit message passes pinned commitlint 21.2.2. A final fetch shows no divergence from origin/main. Final staged whitespace checks, commit and the previously authorized push follow.
