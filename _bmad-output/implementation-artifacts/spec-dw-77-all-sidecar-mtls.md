---
title: 'DW-77 prove mTLS across every configured sidecar'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'a5102784b358432795808b1456b39554b8275b5b'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-77 has YAML and synthetic-sidecar coverage only. EventStore now adds an admin UI sidecar that Parties never configures for mTLS. Existing tests cannot certify every participant or a real credentialed invocation.

**Approach:** Complete the AppHost wiring and qualify the actual topology with both optional profiles enabled. Prove all eight sidecars, authenticated test tenant bootstrap and the existing Parties-to-Tenants readiness invocation. Review, verify, commit and push completed work on main.

## Boundaries & Constraints

**Always:** Reuse existing trust bootstrap, SDK app-channel/event-subscription helpers and private test Redis. Use disposable dependencies, bounded cancellation, environment restoration and owned-resource cleanup. Keep credentials out of evidence. Runtime skips cannot close DW-77; required CI infrastructure failures fail tests.

**Never:** Expand ACLs, bypass authentication, add public endpoints, mutate developer data, initialize nested submodules, upgrade dependencies or change command/projection semantics. This qualifies local mTLS; runtime deployment stays external.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Behavior | Failure |
|----------|--------------|-------------------|---------|
| Complete profile | mTLS and both optional flags | Eight credentialed, responsive sidecars | Missing participant fails |
| Invocation | Parties sidecar calls Tenants GET /ready | Existing identity-specific ACL admits request | Timeout/non-success fails |
| Bootstrap | Tenant/member envelopes with test token | Registered handler establishes access | Missing/wrong token rejected |
| Interrupted startup | Failure/cancellation | Owned resources cleaned; environment restored | Preserve cleanup failure for retry |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties.AppHost/Program.cs`: composition; missing admin UI credential/config wiring.
- `src/Hexalith.Parties.AppHost/DaprMtlsBootstrap.cs`: reuse credential loading, generated mTLS configuration and sidecar options.
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyFixture.cs`: lifecycle, CI bootstrap, private Redis and writer cutover; retain ordinary defaults.
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesTestRedis.cs`: reuse private routing, including stateless sidecars.
- `tests/Hexalith.Parties.Tests/Tenants/TenantEventInfrastructureTests.cs`: supported SDK envelope examples for TenantCreated/UserAddedToTenant; invoke the real subscription handler.
- Read-only EventStore Aspire helpers provide the admin UI sidecar and WithEventStoreAppChannelToken. Consume existing owner seams without editing submodules.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.Parties.AppHost/Program.cs` — apply generated mTLS-only config and trust bundle to the existing admin UI sidecar; preserve its other options.
- [x] `tests/Hexalith.Parties.IntegrationTests/Topology/DaprMtlsBootstrapTests.cs` — inspect actual default/complete models, exact sidecar identities, rendered config, credentials and application credential isolation.
- [x] `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyFixture.cs` — add opt-in complete mTLS profile with optional-service disposable dependencies, explicit-start admin UI, all-sidecar readiness and authenticated tenant bootstrap. Propagate cancellation and retain cleanup ownership.
- [x] `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesMtlsTopologyTests.cs` — add serial runtime metadata, cross-service invocation and bootstrap authentication/authorization assertions. Verify resulting tenant access through existing seams, beyond callback status.
- [x] `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesAspireTopologyFixtureTests.cs` — cover changed failure/cancellation, environment restoration and cleanup behavior.
- [x] `tests/README.md`, `docs/development-guide.md` — document runtime command, prerequisites and qualification limits.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` — close DW-77 only after runtime proof; retain commands/results in the sibling `tests/dw-77-all-sidecar-mtls-2026-10-20/` evidence directory.

**Acceptance Criteria:**

- Given the complete AppHost profile, when qualification runs, then eventstore, eventstore-admin, eventstore-admin-ui, parties, tenants, memories, sample and sample-blazor-ui all run with configured mTLS and answer their own Dapr metadata endpoints.
- Given those sidecars, when Parties invokes Tenants readiness, then the real target returns 200 through its existing caller-specific ACL.
- Given a fresh fixture, when authenticated tenant/member envelopes arrive, then the registered handler establishes test access; missing/wrong tokens establish no access.
- Given interrupted startup, when disposal completes, then only owned resources are removed and original environment values return.

## Implementation Notes

- Selected DW-77 after completed DW-76; no sprint-story mutation. Clean main baseline: a5102784b358432795808b1456b39554b8275b5b. Commit/push explicitly authorized when done.
- No unresolved human choices or irreversible implementation changes. Production wiring plus fixture lifecycle and runtime coverage exceed the small-change route: dispatch.
- Baseline exact-target Aspire start succeeded; security, eventstore and parties each passed `aspire wait` with a 30-second timeout. This default run does not certify mTLS. Exact-target stop succeeded; `aspire ps --format Json` then returned [].
- Docker 29.8.2, SDK 10.0.402 and Dapr are available. Fixed control-plane names/ports require serial execution. Independent investigation confirmed five default/eight complete-profile sidecars.

- Implementation audit: read the complete baseline diff including all new files. AppHost now credentials the existing admin UI sidecar without changing ACLs. The complete test profile uses isolated optional backing services, explicitly starts the admin UI, probes all eight real sidecars, and performs authenticated bootstrap.
- Matrix audit: complete profile/invocation/bootstrap rows are covered by both passing PartiesMtlsTopologyTests cases; interrupted startup is covered by passing fixture failure, cleanup-retry and caller-cancellation cases. Full affected assembly: 93 passed, zero failed, six pre-existing health skips; no DW-77 row depends on a skipped test.
- Tenant access effects use the production subscription/authentication/access registrations in a controlled HTTP TestServer; the external callback is exercised separately. No public read endpoint or background-service dependency is added. Exact limits and commands are retained in the evidence directory.
- Preserved original line endings for untouched lines and CRLF for new/changed lines. Explicit CRLF-aware whitespace validation passes; workspace-default git diff --check reports CR bytes as whitespace.

## Verification

- `dotnet build tests/Hexalith.Parties.IntegrationTests -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` — zero warnings/errors.
- Invoke built integration assembly with xUnit v3 single-dash `-class` for bootstrap, fixture and runtime classes; then run the complete affected assembly. Preserve skips/failures separately from passing evidence.
- Require actual runtime responses; YAML alone cannot satisfy acceptance. Complete independent review and patch introduced defects.
- `bash scripts/check-no-warning-override.sh` and `git diff --check` pass. Validate the complete commit message with pinned commitlint; retain evidence. Fetch/inspect main divergence before pushing.

## Review Triage Log

- Blind 1 — medium, patch: bootstrap-host disposal precedes the cleanup coordinator, so a thrown disposal skips remaining teardown. Put the coordinator in a finally path; same root cause as Edge 1.
- Blind 2 — low, rejected: external callback 200 does not distinguish SDK skip outcomes from processing. Evidence already limits external proof to authentication and verifies projection/access effects separately through real registered services. No public runtime state seam exists; introducing an inspection mechanism exceeds a simple correction for this local qualification.
- Blind 3 — false: an explicit security-log assertion is absent, but model tests assert each real sidecar's mTLS config/trust settings and runtime checks require all eight to initialize and answer. Retained startup messages corroborate that combined proof; no runtime security evidence is claimed from metadata alone.
- Blind 4 — medium, patch: add the real admin UI-to-admin /health invocation to cover the corrected pair directly through unchanged policy.
- Blind 5 — low, rejected: direct authenticated callback bootstrap does not qualify full broker subscription/discovery delivery. This is documented and the intent calls for test bootstrap; adding pub/sub lifecycle/state inspection would add substantial machinery without changing the local mTLS guarantee.
- Blind 6 — medium, patch: existing cancellation coverage ends during construction; strengthen pending-readiness cancellation and owned-cleanup assertions. Same root cause as Verification 1.
- Blind 7 — medium, patch: optional databases have no health probes while Memories synchronously connects with ConnectionMultiplexer.Connect. Add actual Redis PING readiness before Memories starts.
- Blind 8 — medium, patch through verification: Debug/source results do not qualify the Release/package graph used by CI. Run the package build and focused runtime proof, recording exact blockers separately if necessary.
- Blind 9 — low, patch through verification: retained local runtime is 1.18.7 while CI selects 1.18.2. Validate using an isolated task-local copy of 1.18.2 and record its version/provenance; do not alter the developer installation or dependency pins.
- Edge 1 — medium, patch: bootstrap disposal can skip remaining teardown, matching Blind 1; the finalizer must still attempt independent cleanup.
- Verification 1 — medium, patch: the construction cancellation case cannot detect reverting the token on an outstanding readiness request. Add a pending HTTP-response case that cancels before its HTTP timeout, matching Blind 6.

## Final Verification

- Review remediation is complete: bootstrap teardown preserves failures and attempts topology cleanup; the runtime test invokes the corrected admin UI pair; pending readiness cancellation has a regression; both optional databases gate Memories on actual PING readiness. Follow-up edge-case and verification reviews independently confirm their fixes.
- Final verification: Release/package build has zero warnings/errors; complete affected assembly passes 98/104 with six pre-existing explicit health skips, zero failures/errors/not-run. The exact CI Dapr 1.18.2 runtime is separately qualified in Release/package mode: both tests pass without skips and all eight security startup messages report 1.18.2. Both real cross-service invocations return 200.
- Earlier Debug/source runtime build race is retained separately in validation.json; its one bounded retry passes. No source/package failure is hidden or recertified. Final evidence is generated from actual xUnit XML and bounded runtime-log fields; no credentials or host paths are retained.
- Pinned commitlint 21.2.2 validates the exact complete message with zero problems/warnings. All review findings are triaged above; no new deferred work.
