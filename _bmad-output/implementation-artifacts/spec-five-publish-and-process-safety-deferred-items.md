---
title: 'Five publish and process safety deferred items'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '664b99c67e90a14c8a1cc4d9663a25fa44c47c9a'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Deferred defects affect image timestamps, compact navigation, Memories production authentication and owned-process cleanup.

**Approach:** Address five entries: DW-107, DW-112, and the unnumbered Memories publish authentication, Dapr wrapper/child quiescence and package-runner external interruption entries. Implement, independently review, verify and record each disposition; commit and push completed changes on main.

## Boundaries & Constraints

**Always:** Change shared capabilities in their owning Builds/FrontComposer repositories before consumer adoption. Preserve authentication, ACLs, privacy, argument parsing and bounded cleanup. Keep secrets private; publish secret parameter references. Require executed producer/consumer evidence and already-pushed producer commits before parent gitlinks.

**Never:** Invent owner approval, bypass source/build gates, certify deployment from startup, or claim closed entries with unmet publication/runtime gates. Cleanup targets owned processes only. Direct supervisor SIGKILL remains outside cooperative guarantees.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure handling |
|---|---|---|---|
| OCI | Leap day, impossible day, missing label, colon-bearing values | Calendar validation and exact rebound created labels | Reject invalid provenance |
| Navigation | Desktop, Phone/Tablet, resize | Navigation skip link exists exactly with its mounted target; content link survives | Preserve keyboard announcements |
| Memories | Production publish targets, external OIDC, paired Dapr tokens | Valid emitted configuration starts actual producer | Missing credentials fail closed |
| Dapr | Direct exec; CLI wrapper exits first | Runtime identity remains owned; workers stop before backing resources | Retain ownership for retry |
| Runner | SIGTERM/SIGINT, caller disappears, inherited pipes | Bounded descendant termination/reaping; unrelated worker survives | Bounded failure diagnostics |

</frozen-after-approval>

## Code Map

Paths below are relative to Parties unless prefixed with an owning root reference.

- `Directory.Build.targets`: timestamp regex and rebind. `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs`: string-only proof.
- `references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py`: created-label omission; adjacent tests provide executable provenance fixtures.
- `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerShell.razor`: link uses HasNavigation; target additionally checks compact viewport. Producer shell tests already exercise visibility.
- `src/Hexalith.Parties.AppHost/Program.cs`: incomplete Memories publish branch; reuse WithJwtAuthentication and selected producer options/token validators.
- `src/Hexalith.Parties.AppHost/DaprMtlsBootstrap.cs`: generated launcher already uses exec daprd for both mTLS profiles. Ordinary non-mTLS runs retain CLI.
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyFixture.cs`: CaptureOwnedWorkers/quiescence tracks wrapper PID/start identity.
- `tests/PackageTests.Shared/PackageTestProcessSupervisor.cs`: exclusive Linux subreaper lacks signal handlers and caller-liveness checks.

## Tasks & Acceptance

- [x] `Directory.Build.targets` and Builds validator above: enforce real calendar dates and required created labels. Extend neighboring provenance/OCI Python tests and CI tests; execute actual targets and inspect SDK container archive labels without registry publication.
- [x] FrontComposer shell above: align link/target conditions. Extend producer FrontComposerShellTests, Parties `tests/Hexalith.Parties.UI.Tests/MainLayoutAccessibilityTests.cs` and `tests/e2e/specs/parties-accessibility.spec.ts` for compact/resize keyboard behavior. Qualify exact producer adoption; retain package-gated closure until matching publication/adoption.
- [x] `src/Hexalith.Parties.AppHost/Program.cs`: configure supported external OIDC authority/issuer/audience, HTTPS metadata, asymmetric algorithms, tenant claim and secret app/sidecar tokens. Extend `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesPublishEnvironmentTests.cs` with emitted-manifest and actual Production startup/invalid-input controls, retaining producer validators and required backing services. Startup does not qualify Parties bearer forwarding/search.
- [x] Launcher/fixture above: extend neighboring Topology/DaprMtlsBootstrapTests.cs and PartiesTopologyCleanupTests.cs with executable PID-continuity and wrapper-first controls. Establish ownership before wrapper loss where needed; prove exit ordering, retry and unrelated-worker survival separately for direct and CLI paths.
- [x] Shared supervisor above: register cooperative signals and explicit caller PID/start-identity monitoring before launching children. Extend `tests/Hexalith.Parties.Contracts.Tests/Package/PackageTestProcessTests.cs` with interruption during work/cleanup and isolated caller-termination controls.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md`, `tests/README.md` and adjacent `tests/five-publish-and-process-safety-2026-10-23/`: record exact evidence and remaining gates for independent review.

**Acceptance:** Given every matrix precondition, when its path executes, then assertions establish its expected behavior and emitted/OS state. Given an unavailable prerequisite, when recording results, then its exact command/blocker remains explicit and the affected entry stays open.

## Implementation Notes

The user approved this unchanged spec on 2026-10-23. Parent captured the pre-code isolated Aspire baseline: start succeeded, security and Parties reached healthy, then the exact AppHost stopped successfully. Sanitized resource states are in the evidence directory. This is an implementation handoff in an already rendered workflow; the parent owns workflow phases, review and final remote publication. Delegates must not rerun the renderer.

The user requested five items together and main publication. Investigation excluded DW-50 (missing bucket/rebuild platform contract), DW-116 (source build blocker), DW-126 (umbrella owner qualification) and retained-history snapshot capture (missing typed SDK seam).

The parent read the complete staged Parties/Builds/FrontComposer implementation diff and matrix evidence. Pre-review corrections qualify occupied default ports and independent failure cleanup; actual SDK emission exposes a retained Memories sidecar-token publisher gap. Local implementation/disposition tasks are complete under the acceptance rule for unavailable prerequisites. Owner adoption, exact source observations/signoff, independent review and the already-authorized final commit/push are post-review operations outside the implementation checklist. Four original ledger gates remain open; no missing publication proof is recertified.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and route |
|---|---|---|
| blind-1: configured algorithms append to producer defaults | medium | Producer options already initialize HS256/RS256/ES256; actual .NET binding probes retain HS256 after this caller config. This is an existing producer binding contract, not a new allowance. Defer owner replacement semantics; keep Memories open and remove any asymmetric-only qualification claim. |
| blind-2: sidecar token annotations absent from SDK output | medium | Full SDK emission already demonstrates this pre-existing Toolkit callback omission for all three target configurations. Defer upstream publish support; current app-only startup and model checks cannot close the recorded gate. |
| blind-3: real sidecar guard bypassed by wrapper control | medium | The controlled plain executable avoids the ordinary Dapr annotation guard. That guard throws before known captured descendants are terminated. Patch the actual annotated path to drain proven owned workers while retaining the unknown-custody backing fence, and test it. |
| blind-4: filesystem marker prevents supervisor cleanup | high | The newly added marker touch precedes every kill; a missing state directory raises before reaping. Diagnostic writes can also mask cleanup outcomes. Patch best-effort bookkeeping and exercise deletion/failure with live owned descendants. |
| blind-5: stale snapshot PID establishes false ownership | high | Capture re-reads a current start identity from an unbound snapshot PID, and new fallback termination can target that falsely associated lifetime. Patch resource/session lifetime proof before capture/kill and add a real unrelated-worker stale-snapshot control. |
| blind-6: child PID reuse enters owned termination set | high | A PID from the children file is re-read without a coherent current parent/lifetime check; the same false-custody root cause permits unrelated-worker termination. Group with blind-5; patch coherent lineage and session identity validation. |
| blind-7: shared target skips absent created instant | medium | The shared validation target has the same nonempty-created condition as rebinding, so a requested source/version without created bypasses both. Patch the existing guard at the publication boundary and add the missing-input control. |
| blind-8: app alive does not prove sidecar health | medium | Selected producer /alive includes the self live check; it is not an authenticated Dapr outbound health assertion. Patch the test sidecar's explicit ready/liveness precondition and a failure control. |
| blind-9: final output capture can outlive cleanup budget | low | Process exit is bounded but the immediately following Task.WhenAll(stdout,stderr) has no bound. Applying the existing independent cleanup deadline is a direct correction; preserve any primary test failure. |
| blind-10: cleanup signal control races supervisor exit | medium | A marker does not hold cleanup active; the signaler can be descheduled until its target exits. Patch deterministic test-only interruption at cleanup, without introducing a production wait state. |
| edge-1: effective JWT algorithms retain HS256 | medium | Same verified producer binding contract as blind-1. Group with blind-1 and retain the explicit upstream algorithm-policy gate; no asymmetric-only claim is accepted. |
| verification-1: exact publisher instant has no mismatch control | medium | The verification layer's in-memory mutation removes both expected-created comparisons while all ten OCI tests remain green. Patch the four-argument consumer test with a consistent valid but different publisher instant and require rejection. |
| parent-1: Linux ownership guard blocks prior non-Linux observations | medium | Follow-up inspection demonstrated an unconditional Linux marker requirement on the cross-platform capture path. Restore prior root observation/SDK shutdown on non-Linux; keep manual fallback termination Linux-only. No non-Linux containment claim is added. |
| parent-2: stale cached generation prevents valid lifetime capture | medium | A stale PID dictionary slot could suppress a newly proven owned lifetime. Replace the slot only after coherent marker/start proof and recheck it; the real marked-worker stale-generation control passes. |

Current-change fixes are direct corrections to existing ownership guards, bookkeeping, awaits and executable controls. They introduce no public surface and guard only the demonstrated stale-lifetime, missing-input and cleanup states. The pre-existing producer binding and SDK publisher limitations remain explicit upstream gates; frozen intent is unchanged.

## Verification

Establish and inspect the exact owned Aspire baseline before code edits. Build affected projects individually; run focused then full affected xUnit assemblies with single-dash filters. Use pinned-tool-generated mappings for Debug/source and record Release/package checks separately. Run Builds Python provenance/OCI suites, real MSBuild/archive proof, producer component tests, pinned-toolchain managed Playwright compact/resize checks, actual Memories Production startup and Linux live topology shutdown controls. Follow the fallback ladder for blockers. Run build guards, git diff --check, owning pinned commitlint and staged-diff inspection; retain sanitized receipts and canonical full revisions.

## Final Review Outcome

All three independent review layers completed. Twelve original findings were individually triaged; two verified upstream contracts remain deferred. Current-change fixes and both parent follow-up corrections passed their focused controls. Final parent assembly checks and exact Git adoption/publication receipts are recorded in `tests/five-publish-and-process-safety-2026-10-23/`. Four original entries retain their explicit upstream/runtime gates; cooperative runner interruption is complete within its stated Linux scope. Frozen intent is unchanged.

Final parent verification: CI 449/449, Contracts 226/226, Client 332/332, service 2624 passed with 3 optional skips, Integration 128 passed with 20 optional skips, UI 413 passed with one source-only skip, and selected producer shell 48/48. Actual Production 20/20 and the fresh combined live shutdown matrix 4/4 have no skips. The earlier port-bind and dirty-checkout precondition attempts remain in the evidence packet. Both owner main integrations are published; exact task source adoption is `ad9c72af5b029a438557f5899f1580ca6d482aef`. Pinned commitlint, build guard, source ancestry and gitlink range checks pass.
