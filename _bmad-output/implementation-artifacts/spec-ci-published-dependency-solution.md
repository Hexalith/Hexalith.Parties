---
title: 'Build CI and releases against published dependencies'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="operator requested CI fixes and release without human checkpoints">

## Intent

**Problem:** Parties CI builds external diagnostic source projects from the development solution, causing concurrent EventStore output writes and mixed Debug/Release dependencies. Consumer verification also repacks external source instead of validating the published dependency graph.

**Approach:** Select the existing complete standalone solution for CI and release preparation. Restore consumer support dependencies from configured package feeds by default, retaining explicit source-support packing for diagnostics. Keep all test tiers, warnings-as-errors, exact-source release proof and publication validation active.

</frozen-after-approval>

## Implementation Notes

- Investigation: latest CI run 40586521470 failed with MSB3026 in EventStore Client and Server. StandaloneSolutionTests already proves the standalone solution includes every owned project.
- Change the CI/release solution inputs, keep the development solution intact, and make source-support packing an explicit consumer-validator option.
- Update workflow contract tests and the central-version exception test for the existing FrontComposer.Contracts.UI catalog alias. Exercise package-only Release restore/build and consumer checks, then request independent review.
- Git synchronization, owner-authorized release approval and actual publication are separate authorized operator actions performed after this code fix.

- Local standalone Release restore/build passed with zero warnings and errors; ten packages packed and validated, and two isolated consumers restored and built against public dependencies.
- Independent review identified three fixes: remove stale support-feed expectations, validate restored Commons family identities/versions, and correct active parity commands. All addressed.
- Full CI contract execution exposed a pre-existing source-only browser case inside the mandatory portal suite. Moved the unchanged cancellation scenario to an explicit source-qualification spec; absence of its capability now fails instead of skips. Required package browser suite remains unconditional.

## Review Triage Log

- medium, patched: default source test expected a removed support feed; it now requires only the Parties feed plus explicitly configured sources, and rejects source packing.
- medium, patched: published consumers did not validate Commons family versions; exact version/package checks now run before build with missing, wrong-version and project-substitution negative controls.
- low, patched: active docs parity commands still selected the diagnostic development solution; commands and policy now select the standalone package solution.

- Review follow-up: explicit source browser qualification now has its own config and is excluded from default discovery. Client optional-family negative fixtures include required Commons packages and assert the offending family so they cannot pass for the wrong reason.

- Final local verification: 464 CI contracts passed, zero failures/skips; Release standalone build had zero warnings/errors; ten package archives and both published-dependency consumers passed. Browser configuration passed 8 tests; typecheck and default/explicit source discovery passed. Remote full CI and actual release publication remain operator execution steps.

- Follow-up full service tests exposed stale current-selection receipts and EventStore package skew against the refreshed Tenants runtime graph. Select published EventStore 3.128.3, matching the catalog/runtime roots; update current source identity constants and add a dated matrix observation while preserving historical qualification receipts. Current Aspire technology tables now reflect 13.6.4.
- Public API snapshot adds only existing ProcessingActivityPage and GetProcessingRecords records from prior source commit 0455c28a8604bf68d7065d36977c1c2ccf13e2e7; the remaining 225 Contracts controls were unchanged and all 226 passed after the refresh. Independent review confirmed no API removal or hidden change.

- Clean-run follow-up: ClientPackageTests now packs only owned Contracts/Client artifacts and restores published external dependencies, removing latent dependencies on pre-existing external source output. Both package probes pass.
- Immutable source proof still checks exact committed and checked-out SHAs. Receipt descriptions now constrain git describe to the tag captured by the observation; raw short-SHA observations use rev-parse. Fetching the new Memories v3.0.1 tag reproduced the previous failure while the anchored v3.0.0 description remained unchanged.

- Full CI now passes 6,103 unit/integration tests (six existing optional skips). The final Aspire lane exposed one actual TLS UntrustedRoot error in concurrent-session UI routing. Adopt immutable Builds domain-ci 1745ea0f09df9e518aeea3eeb1a7df836f67e09d for its existing preparation hook, bounded at two minutes; trust the ephemeral SDK development certificate and propagate SSL_CERT_DIR before running unchanged mandatory topology assertions.

- Trust follow-up verification: all 464 CI contract tests pass; shell syntax and exported/persisted environment probe pass; 15 prior clean TRX reports satisfy the updated workflow validator. Review found only stale same-revision wording, now corrected to independent CI and commitlint pins.

- The TLS preparation passes in real CI. A subsequent cold runner showed only the optional Admin UI process-launch message before the per-sidecar two-minute readiness timer expired (start 01:31:05, stopped 01:33:06, no app stdout). Allow the sidecar check to consume the existing shared five-minute startup deadline; do not change overall lifetime, health responses, persisted-state checks or isolation assertions.

- All six lifecycle/cancellation controls pass with zero skips. Review confirms both callers pass the existing shared five-minute cancellation token; overall startup duration and all readiness/identity assertions remain bounded and unchanged. Cold compilation is a plausible cause of the pending process startup, not independently proven by retained output.

- The shared deadline still expired on the Admin UI sidecar after its host began listening. Exact Dapr 1.18.2 source inspection rules out HTTPS redirects as the cause: its app-channel client does not follow redirects. Logs show the sidecar discovers Aspire's proxy while the UI is still explicitly stopped. Use the actual UI HTTP listener in the complete test fixture so Dapr waits for Kestrel rather than issuing an early callback into that proxy; preserve explicit startup, HTTPS, all eight sidecar identities and every runtime assertion. This race remains an inference until cold CI confirms the change.
- Cleanup also exposed Linux ESRCH during an in-flight /proc stat read. Treat only Linux IOException HResult 3 as an exited process; preserve permission/unrelated I/O failures and the captured start-identity check. Existing cleanup controls passed (22 passed; four live controls require the runtime profile), and all six fixture lifecycle controls passed. Failure diagnostics now include the exact initialization/sidecar phase.
- Local baseline isolation passed with the installed Dapr 1.18.7 runtime (two live sessions, authenticated UI routing, persisted state, cross-call rejection and peer disposal). It did not reproduce the CI 1.18.2 startup stall. Local development-certificate trust still reports missing NSS trust, but .NET UI HTTPS requests pass; cold CI preparation already passes with its fresh certificate.
- Independent review follow-up: phase labels now advance through resource health, writer activation, endpoint health and tenant bootstrap. Deterministic OS-error controls prove ESRCH is tolerated while unrelated I/O and permission failures propagate unchanged; all 23 cleanup controls and six fixture controls pass with zero skips.
- The real two-session probe caught a fixed launch-port collision in the initial proxyless configuration. Clear both HTTP port fields for Aspire allocation. The corrected unchanged isolation test passed locally in 2m24s, with zero skips, including authenticated UI routing, persisted state, cross-call rejection and peer disposal. Release build reports zero warnings/errors. Independent review found no remaining code blockers; the exact Dapr 1.18.2 cold CI run remains required.
- Exact Dapr 1.18.2 local qualification exposed a separate Memories internal gRPC bind collision on port 46173, inside Linux's configured 32768-60999 ephemeral range. The sidecar failed before any app callback, so the HTTP direct-listener change remains scoped to Admin UI. Session allocation now excludes the actual Linux ephemeral range, uses wildcard bind probes with bounded retries, skips only address-in-use collisions, and rolls back only the current attempt's reservations on failure. Explicit competing binds still fail readiness closed.
- The new range regression failed against the previous allocator with all 16 listeners inside the ephemeral range. Both configuration controls now pass. The unchanged two-session test passed with checksum-verified Dapr 1.18.2 in 2m21s, with zero skips; build has zero warnings/errors. Independent review found no remaining correctness blocker.
- Preserved and fast-forwarded concurrent upstream 87d9264ccbe40243896937e1284966d5a32596ca, including deferred Admin.Server endpoint binding and fresh worker-exit proof. The port change is independent of those files; combined endpoint, cleanup and fixture controls are verified before pushing. Full exact-source hosted CI remains required before release.
- Final combined local probe initially stopped during bootstrap because the external Docker Desktop host rejected listener 61011 even though WSL could bind it. Restrict candidates to 20000-29999, below common Linux/IANA dynamic bands, while still excluding the actual configured Linux range. The unchanged full combined two-session test then passed against Dapr 1.18.2 in 2m05s with zero skips. Both session configuration controls pass; warning-error build and independent review pass. This preserves every hosted qualification gate and fail-closed explicit-bind behavior.
- Hosted ebdaef74 qualification passed the main build/test job and 155 Aspire tests, but one isolation case failed on the first counter POST: Dapr could not locate AggregateActor. The first gateway discovered its proxy at 03:08:41, listened at 03:11:04, and retained an empty actor registry; the second gateway registered all twelve types. Startup/cleanup races were resolved. The missing initial configuration ingestion is proven; its exact callback error was not retained.
- Give the fixture gateway a direct dynamic HTTP listener in every profile, keeping production orchestration untouched. Require AggregateActor in its own sidecar metadata after health and identity checks, before availability in complete, Memories and ordinary profiles. Zero activated instances remain valid. No command retries, skipped assertions, changed deadlines or weakened gates are introduced.
- Four metadata boundary controls reject an empty healthy registry, a wrong actor and a missing registry, while accepting registered AggregateActor at count zero. All ten fixture controls and four endpoint controls pass. The unchanged complete two-session test passed against Dapr 1.18.2 in 1m57s with zero skips; warning-error build passes. Final independent review found no remaining blocker.
- Hosted f7bd8ffa qualification advanced past actor registration and both persisted counter seeds. Its sole remaining isolation failure was Admin UI fallback HTML. The first Admin.Server never started: native concurrent dotnet run builds failed with MSB3030 missing Contracts.dll while Admin.UI reported MSB3026 shared-output contention. Sample.BlazorUI also failed compilation. Backend invocation timeouts explain the missing rendered identity; no UI assertion or retry policy is changed.
- Add a ten-minute Aspire preparation wrapper: source unchanged HTTPS trust setup, then serially build all eight declared external runtime hosts in their native Release graphs with one MSBuild node, BuildInParallel=false, disabled build servers and warnings-as-errors. Keep the package-only main/release consumer graph independent, the total Aspire job at thirty minutes, and topology startup at five minutes. No submodule operations, source-support packing or graph-mode overrides are added.
- All eight native preparation builds passed with zero warnings/errors (warm incremental local evidence). A CLI orchestration probe verifies HTTPS ordering, inherited/persisted trust, sequential native build flags and immediate failure propagation. Shell syntax, actionlint, all 64 workflow controls and the real AppHost inventory/preparation coverage guard pass. Independent review found no concrete blocker. The next cold hosted run remains the required confirmation of shared-output contention removal.
