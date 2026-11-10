---
title: 'Five lifecycle and qualification deferred items'
type: 'bugfix'
created: '2026-11-10'
status: 'done'
route: 'dispatch'
baseline_commit: '7987bf1ef709ea6fb812eed56607a9778c24f033'
frontcomposer_baseline: '01fc48e3955a8cec47ab1602f5cd5c8e286801da'
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-ux-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferrals leave ambiguous lifecycle handles, inconsistent reader admission, incomplete launcher observations and formatter cases outside CI.

**Approach:** Fix the three lifecycle deferrals in FrontComposer and adopt its exact source revision in Parties. Harden launcher capture, enroll formatter tests, review, verify and push both repositories to main.

## Boundaries & Constraints

**Always:** Preserve ownership, ULIDs, hidden-unknown, policy/schema gates, retries, retention and cleanup. Require CLI and daprd observations per launcher. Distinguish source from package/deployment proof.

**Never:** Weaken gates, change versions/budgets, enable ordinary live tests in CI, initialize nested submodules, deploy or publish. Preserve unrelated work and historical evidence. Execute controls before closing claims.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Reacknowledgement | Identical retry or changed message/descriptor | Identical retry succeeds; conflict leaves entry unchanged | UnsupportedSchema |
| Alias collision | Either identifier already belongs to another entry | Refuse atomically; equal identifiers within one entry remain valid | UnsupportedSchema |
| Reader | Stable, foreign, unauthenticated or changing context | Lookup and admission share one validated reader | Hidden-unknown for inaccessible handles |
| Observation | Complete tree, failed read, missing identity/role | Complete evidence qualifies; incomplete evidence refuses after cleanup | Retain diagnostics |
| Browser gate | Maintained UI and portal commands | Both formatter cases execute and remain blocking | Nonzero on failure |

</frozen-after-approval>

## Code Map

- Owner: `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Mcp/Invocation/`. `FrontComposerMcpLifecycleStore.cs` omits retry compatibility and cross-alias checks. `FrontComposerMcpLifecycleTracker.cs` reads ownership before `FrontComposerMcpToolAdmissionService.cs` obtains another context. Reuse contracts; bind context internally.
- Owner controls: `references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Mcp.Tests/Invocation/CommandLifecycleTests.cs`.
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesOrdinaryLauncherStopTimingTests.cs`: suppresses read failures and omits role requirements.
- `tests/e2e/package.json`: omits `specs/shared-portal-display-formatters.spec.ts`; CI invokes test:ui. Update `tests/README.md`.
- Current-selection bindings: `.gitlink-signoff.tsv`, `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` and `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`.

## Tasks & Acceptance

**Execution:**
- [x] Owner store/tests -- reject changed messages/descriptors before mutation; retain identical retries and structural descriptor comparison.
- [x] Owner store/tests -- reserve aliases atomically; cover both collision directions, concurrency, foreign owners, equal aliases and eviction/reuse.
- [x] Owner tracker/admission/tests -- reproduce changing accessor/authentication loss; bind admission to the validated owner context and preserve hidden-unknown/schema behavior.
- [x] Timing test and focused observer controls -- inject read failures/missing roles; continue siblings and cleanup, retain diagnostics and refuse incomplete qualification. Rerun ordinary timing.
- [x] Browser scripts/docs -- enroll formatter cases in both commands; execute the CI UI gate without retries.
- [x] Ledger/spec/evidence -- record five outcomes and review; push owner commits before adopting the gitlink/current observations and pushing Parties.

**Acceptance Criteria:**
- Given each defect, when its regression runs before/after correction, then it fails/passes.
- Given racing retries, when conflicts arise, then snapshots, ownership and counts remain coherent.
- Given incomplete capture, when cleanup ends, then qualification refuses; complete capture retains exit-143 and 30-second checks.
- Given CI, when its owned fixture runs, then formatter cases execute and failures block success.

## Implementation Notes

Spec approved by the user on 2026-11-10. The implementation handoff performs local changes and checks only; the orchestrator completes workflow review, owner commit/push and exact source adoption afterward. Do not perform remote operations during the handoff.

Planning: repositories/submodules are clean; no pending local commits. Docker, Dapr and SDK are available. Published FrontComposer 4.7.6 stays unchanged. Pushes trigger CI.

## Spec Change Log

## Review Triage Log

Review pass 1 ran all three layers; none was skipped. Findings were assessed individually before grouping.

- BH1 — maybe-false, defer: Empty task enumeration can bypass diagnostics in the model, but no real Linux procfs empty-task result was demonstrated for an admitted live process. The same loop existed before extraction. Record medium unverified; settle using an actual procfs exit/task-enumeration reproduction.
- BH2 — medium, defer: Identity is read before comm and not rechecked. The edge harness changes identity during comm and the helper retains stale identities without refusal. This lifetime race existed in the original traversal; group with E1.
- BH3 — false, reject: TryReadSnapshot is internal. Its only production consumer is LifecycleTracker, which reads ProtocolName and returns only snapshot JSON. No production recipient can mutate retained descriptor collections through the public result; caller-owned collection mutation is independently copied and tested.
- BH4 — low, patch: The children-read row performs a separate successful capture after the failed root, so it does not prove continuation in the failing traversal. Correct the injected task layout directly.
- BH5 — medium, patch: The local sleep finally does not execute fixture disposal or the actual timing refusal. Group with VG1 and add an injected failure through the actual runner, retaining diagnostics and cleanup observations.
- BH6 — low, patch: The new empty-role refusal branch lacks its whitespace comm control. Add the direct test row without changing production behavior.
- BH7 — low, patch: The event releases immediately after scheduling; no arrival barrier establishes that contenders reached the start. Coordinate arrivals before releasing both race tests.
- BH8 — low, patch: The new clone control covers enum/map mutation but not Parameters or DerivablePropertyNames mutation. Add independent caller-collection controls.
- BH9 — low, patch: The implementation-handoff spec hash predates orchestrator notes. Preserve that historical receipt, explicitly identify its capture stage and provide final-content hashes separately.
- BH10 — low, patch: README and pending fields still describe the handoff while later commands completed consumer/live gates. Label historical state and publish one current completion summary with links to later evidence.
- E1 — medium, defer: Same verified pre-existing identity/role race as BH2: the temporary reflection harness retains stored identities 20/30 while simulated current identities advance to 21/31, with no capture refusals. Group with BH2; current live packet has no observed churn.
- VG1 — medium, patch: Pre-verified regression gap: deleting the real timing refusal leaves helper controls and complete live evidence green. Add actual-runner incomplete capture refusal, retained diagnostics and completed owned cleanup; group with BH5.

Patch groups: actual-runner refusal (BH5/VG1), same-traversal children coverage, empty-role coverage, coordinated race starts, remaining collection mutation controls, and evidence finalization (BH9/BH10). No intent or spec loopback is required.

## Verification

- Build/run owner MCP tests in Debug: focused xUnit controls, then full suite.
- Build Parties source Debug/package Release; run selection fitness and observer controls. Retain warnings as errors.
- After test builds: `bash scripts/prepare-aspire-runtime-hosts.sh --gateway-profile packaged --configuration Release`; invoke the built timing class with PARTIES_ORDINARY_TOPOLOGY_TESTS=true; retain JSON.
- In tests/e2e: `npm run typecheck`, `npm run test:config`, `CI=true npm run test:ui -- --retries=0`; check both command discovery sets.
- Retain commands/results/full revisions; validate messages with owning pinned commitlint; fetch/check divergence before pushing.

### Local implementation handoff (2026-11-10)

- Owner final controls: 119 lifecycle cases; 23 fail against the baseline production
  files and all 119 pass after correction. Full MCP suite: 529/529 pass, zero skips.
  Descriptor collections are retained independently so later caller mutation cannot
  rewrite the acknowledged structure. Bench governance prerequisite was built before
  final full-suite qualification. Owner packet:
  `references/Hexalith.FrontComposer/_bmad-output/implementation-artifacts/tests/lifecycle-alias-and-reader-2026-11-10/`.
- Observer controls: original traversal suppression extracted into an injectable seam
  fails 10/11 controls; hardened helper passes 11/11 with zero skips. The live timing
  test now retains capture diagnostics and requires CLI plus daprd under every launcher.
  The separate fresh ordinary-runtime timing rerun remains for the orchestrator.
- Browser: pinned Node 24.22.1/npm 11.19.1; typecheck and 8 configuration controls pass;
  both maintained scripts discover both formatter cases. CI UI gate passes 81/81 with
  retries disabled. Injected formatter failures produce exit 1 in both scripts; original
  formatter spec bytes were restored before final green qualification.
- Parent packet: `tests/five-lifecycle-and-qualification-2026-11-10/` relative to this
  directory. Includes commands, source and assembly hashes, XML, complete logs and the
  red observer seam. The Aspire baseline dashboard query token is redacted.
- Remaining parent gates: source Debug/package Release consumer builds and selection
  fitness, fresh runtime preparation/live timing, review, owner commit/push, exact source
  adoption/current observations and Parties commit/push. No remote operation occurred
  during the implementation handoff. Source proof does not qualify package/deployment.

### Orchestrator acceptance before review

Source Debug integration build and 27 controls pass; package Release build and 102 controls pass, with four separate mTLS opt-ins skipped. Fresh package runtime preparation passes. Ordinary live timing passes 1/1, zero skips, with complete capture, five exit-143 launchers and no survivors. All matrix rows now have passing executed controls. Repository fitness passes 158/159; its unchanged immutable-source gate refuses the deliberately dirty owner checkout. Rerun all 159 after reviewed owner commit and committed exact-source adoption, before pushing Parties. Review and authorized version-control completion remain orchestration work.

### Review corrections verified

All patch groups are resolved. The actual timing runner has an injected incomplete-capture control that checks the specific qualification refusal, retained diagnostics, fixture quiescence, exit-143 launchers and disposed application. Same-traversal children failure and whitespace comm controls pass. Owner race starts await all contenders; Parameters and DerivablePropertyNames mutations are independently controlled. Historical handoff receipts are explicitly labeled, and final source hashes/completion receipts are separate. Final owner MCP suite passes 531/531; source controls pass 28/28; package controls pass 103 with four separate mTLS opt-in skips; actual live controls pass 2/2 with zero skips. The initial negative-control assertion-helper issue is retained in review evidence; the final direct refusal capture passes. The browser gate remains 81/81 on unchanged UI/scripts. Two pre-existing observer follow-ups remain deferred; no implementation finding remains unresolved.

### Completion evidence

Owner commit `7ace06c5c50406b9021fc95d8d87cf962b5180dd` is pushed to main and adopted by exact root gitlink. The final repository fitness gate passes 159/159 after source adoption; its first post-commit attempt exposed an omitted evaluated-package-graph sentence in the new matrix section, which was restored without weakening the test. Compiler builds retain zero warnings/errors. Final verification combines 531 owner MCP tests, 28 source controls, 103 package controls (four separate mTLS opt-in skips), 81 pinned-toolchain browser cases and two unskipped live timing controls. `final-verification.json` binds current file hashes and later receipts separately from the historical implementation handoff. Review is complete; two pre-existing observer follow-ups are separately recorded. The final Parties commit records this completed state and is published by the already-authorized push.
