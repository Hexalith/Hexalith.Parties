---
title: 'Complete five runtime and release verification deferrals'
type: 'bugfix'
created: '2026-10-22'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 9daec2e4d6b8ec3855c4ebff1994a17613b8a815
context: ['AGENTS.md', 'docs/development-guide.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferred verification gaps remain locally actionable: the previously failing UI release container lacks current startup qualification; historical query parsing precedes its work boundary; release verification chooses the first matching tag; publication does not consume main-push range signoff proof; and the publication environment test can pass solely because another job is protected.

**Approach:** Complete DW-105 plus the four unnumbered entries from `spec-five-query-release-deferred-items.md` covering envelope parsing, ambiguous tags, range-proof publication binding and isolated production-environment assertions. Independently review, verify, commit and push this batch to main as requested. The explicit five-item batch is the scope override.

## Boundaries & Constraints

**Always:** Preserve user work, cancellation precedence, cumulative query deadlines, finite shared work capacity, fail-closed authorization and safe unavailable responses. Require successful exact-source push CI and exact-source main-push range proof before publication, retaining the existing release-floor signoff gate. Keep protected manual publication and complete artifact verification.

**Never:** Change dependency versions, gitlinks, owner signoffs, retention, persistence or deployment ownership. Never publish a release for testing or claim GitHub environment protection was exercised by a local wording assertion. Leave unrelated and upstream-dependent entries open.

## I/O & Edge-Case Matrix

| Scenario | Input or state | Expected behavior | Error handling |
|----------|----------------|-------------------|----------------|
| UI container | Fresh SDK-published UI image in shared-smoke Development environment | Process stays running; health routes and RCL stylesheet return HTTP 200 | Startup exit or failed HTTP proof fails |
| Historical parsing | Expensive or stalled envelope parsing | Bounded caller completion using the same deadline and work lease | Cancellation wins; no evidence escapes; retain capacity until work ends |
| Matching tags | Non-version tags and multiple tags share a source SHA | Exactly one eligible stable release version is selected independently of list order | No eligible version or multiple distinct eligible versions fails closed |
| Publication admission | Exact main source with successful CI and RC push proof | Both independent workflow identities and completed success verdicts are required | Missing, stale, pending, wrong-event or failed range proof denies publication |
| Environment guard | Only release job retains production environment | Isolated verify-publication assertion fails | Both jobs independently retain protection |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` already selects `aspnet:10.0-alpine-extra`, required for globalization. Existing local SDK-published amd64 image passes Development health and stylesheet requests; repeat with freshly built inputs and preserve bounded, owned-resource cleanup. Shared Builds owns general container smoke infrastructure; do not duplicate it.
- `src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs`: `ResolveAtAsync` deserializes before deadline creation. `PartyIdentityQueryDeadline.RunAsync` already bounds synchronous work and retains abandoned-work leases; reuse it, including configured elapsed-time enforcement.
- `tests/Hexalith.Parties.Tests/Gateway/PartyIdentityQueryHandlerTests.cs` contains gated work, cancellation, saturation and recovery fixtures.
- `.github/workflows/release.yml`: verify-source proves ci.yml success; verify-publication peels tags and breaks on the first exact-source match. `release.config.cjs` uses plain stable `vMAJOR.MINOR.PATCH` tags.
- `.github/workflows/rc-gate.yml`: main pushes execute event.before push-range checks with per-source concurrency. Require this workflow's completed successful main push run at the dispatched SHA.
- `ReleaseSourcePreflightTests.cs` and `ReleasePublicationVerificationTests.cs` execute tracked workflow shell with isolated fake GitHub responses. `PartiesContainerPublishWorkflowTests.cs` currently protects release environment globally; isolate each job.

## Tasks & Acceptance

**Execution:**

- [ ] `PartiesContainerPublishWorkflowTests.cs`, UI container evidence -- protect the required UI base/globalization configuration, freshly build/publish the UI container and execute bounded Development startup/HTTP checks. Retain source SHA, archive/image identity, exact commands and results. Clean only owned containers/images; do not claim historical release repair or both architectures.
- [ ] `PartyIdentityQueryService.cs`, `PartyIdentityQueryHandlerTests.cs` -- move historical parsing into bounded work before admission; qualify stalled parsing, timeout, caller cancellation, retained capacity and recovery, malformed input, and ordinary historical outcomes.
- [ ] `.github/workflows/release.yml`, `ReleasePublicationVerificationTests.cs` -- restrict eligible publication tags to canonical stable release versions, inspect all eligible exact-source matches, reject ambiguity, deduplicate identical tags, and test both list orders, annotated refs, deleted tags and output safety.
- [ ] `.github/workflows/release.yml`, `ReleaseSourcePreflightTests.cs` -- require completed successful rc-gate.yml main push proof for the dispatched SHA alongside ci.yml, using validated API evidence. Execute successful and rejection fixtures; preserve exact current-main validation and existing publication gates.
- [ ] `PartiesContainerPublishWorkflowTests.cs` -- independently extract and verify each protected job; show a verify-publication-only environment deletion is rejected even while release remains protected.
- [ ] `docs/ci.md`, `RELEASE-CHECKLIST.md`, deferred-work ledger -- document the refined publication prerequisites and tag selection, close only the five verified entries, and retain exact commands/results under the batch evidence directory.

**Acceptance Criteria:**

- Given a freshly published UI container, when run in the shared-smoke Development environment, then its process stays up and health/RCL requests succeed.
- Given a historical query, when parsing stalls, then caller completion and outstanding work remain bounded without changing successful attribution semantics.
- Given several release tags at one SHA, when publication proof runs in either list order, then one canonical stable version is selected or ambiguity fails.
- Given successful CI but absent or failed exact-source range proof, when release preflight runs, then publication is refused.
- Given deletion of only the publication job's environment, when the isolated fitness check runs, then it fails.

## Implementation Notes

Initial tree changes were authorized for preservation; by investigation they had been committed externally. The starting main tip is recorded above. Aspire startup succeeded, but resources waited on security readiness; the owned Parties AppHost was stopped. Upstream-dependent DW-107 and DW-126, and DW-116 source-mode qualification, remain open.

## Spec Change Log

## Review Triage Log

## Verification

- Build affected projects individually in Debug/source mode first; if blocked, record the exact failure and use the prescribed focused fallback with fresh binaries.
- Run focused historical-query and CI classes, then affected assemblies when justified. Execute local shell/API fixtures without remote publication.
- Run actionlint, warning-override guard and `git diff --check`; independently review and resolve findings.
- Validate the exact full commit message with pinned commitlint and retain successful evidence. Fetch and inspect divergence before the authorized main push; never force push.
