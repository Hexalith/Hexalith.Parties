---
title: 'Complete five runtime and release verification deferrals'
type: 'bugfix'
created: '2026-10-22'
status: 'done'
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

- [x] `PartiesContainerPublishWorkflowTests.cs`, UI container evidence -- protect the required UI base/globalization configuration, freshly build/publish the UI container and execute bounded Development startup/HTTP checks. Retain source SHA, archive/image identity, exact commands and results. Clean only owned containers/images; do not claim historical release repair or both architectures.
- [x] `PartyIdentityQueryService.cs`, `PartyIdentityQueryHandlerTests.cs` -- move historical parsing into bounded work before admission; qualify stalled parsing, timeout, caller cancellation, retained capacity and recovery, malformed input, and ordinary historical outcomes.
- [x] `.github/workflows/release.yml`, `ReleasePublicationVerificationTests.cs` -- restrict eligible publication tags to canonical stable release versions, inspect all eligible exact-source matches, reject ambiguity, deduplicate identical tags, and test both list orders, annotated refs, deleted tags and output safety.
- [x] `.github/workflows/release.yml`, `ReleaseSourcePreflightTests.cs` -- require completed successful rc-gate.yml main push proof for the dispatched SHA alongside ci.yml, using validated API evidence. Execute successful and rejection fixtures; preserve exact current-main validation and existing publication gates.
- [x] `PartiesContainerPublishWorkflowTests.cs` -- independently extract and verify each protected job; show a verify-publication-only environment deletion is rejected even while release remains protected.
- [x] `docs/ci.md`, `RELEASE-CHECKLIST.md`, deferred-work ledger -- document the refined publication prerequisites and tag selection, close only the five verified entries, and retain exact commands/results under the batch evidence directory.

**Acceptance Criteria:**

- Given a freshly published UI container, when run in the shared-smoke Development environment, then its process stays up and health/RCL requests succeed.
- Given a historical query, when parsing stalls, then caller completion and outstanding work remain bounded without changing successful attribution semantics.
- Given several release tags at one SHA, when publication proof runs in either list order, then one canonical stable version is selected or ambiguity fails.
- Given successful CI but absent or failed exact-source range proof, when release preflight runs, then publication is refused.
- Given deletion of only the publication job's environment, when the isolated fitness check runs, then it fails.

## Implementation Notes

Final parent verification after review: fresh Release builds have zero warnings/errors; full service 2464/2464 and CI 427/427 pass without skips. All 17 driver fault/ownership fixtures pass and optimized real container proof passes. Pinned commitlint 21.2.2 validates the exact message with zero problems/warnings. See the batch parent-verification.json.

Parent audit: read the staged implementation, tests, documentation and evidence. All six tasks and five matrix rows have passing executed checks. Service 2464/2464, focused identity 310/310 and CI 427/427 pass without skips. Evidence hash consistency and owned-container cleanup were corrected and the narrow container qualification rerun passed.

Approved by the user on 2026-10-22. Re-read the spec at approval; its frozen intent is unchanged. Main advanced externally to 9a46682a before implementation; the existing baseline identifier is preserved.

Initial tree changes were authorized for preservation; by investigation they had been committed externally. The starting main tip is recorded above. Aspire startup succeeded, but resources waited on security readiness; the owned Parties AppHost was stopped. Upstream-dependent DW-107 and DW-126, and DW-116 source-mode qualification, remain open.

## Spec Change Log

## Review Triage Log

Final disposition: all patch findings below are resolved and verified. B9 is rejected; no new follow-up was deferred. B6 now starts/removes by captured immutable identity with prior-absence and binding checks; B1/B3/B5 explicit failure controls pass even with Python optimization.

- B1 — medium, patch: Python optimization removes every acceptance assert in the one-off driver; the reviewer reproduced a successful receipt for HTTP 404 and exited state. Use explicit failing checks.
- B2 — low, patch: a load attempt can import an image before returning failure/timeout, while cleanup currently depends on a successful return. Reconcile the attempted owned import; preserve pre-existing resources.
- B3 — medium, patch: failed/timed-out inspect is treated as absence. The edge reviewer demonstrated deletion of a pre-existing tag after daemon recovery. Require explicit missing-resource evidence before loading.
- B4 — low, patch: a post-start HTTP failure with successful cleanup retains result=pending. Initialize failure and record the current phase so failed receipts are unambiguous.
- B5 — medium, patch: archive/import/preflight exceptions precede receipt finalization, leaving a prior pass untouched. Initialize this attempt's receipt before those operations.
- B6 — medium, patch: the recorded image ID is not the target passed to startup; tag mutation can select different bytes. Start the captured immutable ID and verify cleanup ownership before removing the batch tag.
- B7 — low, patch: the new capacity-recovery polling task continues after its outer WaitAsync times out. Bound the loop itself with cancellation.
- B8 — low, patch: pending RC evidence uses in_progress, duplicating the running case. Correct the pending fixture to queued while retaining strict completed-only admission.
- B9 — low, rejected: pagination already requires the sole matching release on page two and ambiguity/deduplication are separately executed against the complete parsed response. The reviewer identified no reachable acceptance bug; adding combined scenario branches for this rare ordering adds fixture complexity without closing a demonstrated gap.
- B10 — low, patch: result XML documentation describes exact request fields but the new pre-parse rejections cannot echo them. Document empty/default fields when parsing has not completed, without changing the wire contract.
- E1 — medium, patch, grouped with B3: isolated AST execution proves inspect uncertainty can admit an existing image and later remove it. Require confirmed absence.
- E2 — low, patch, grouped with B2: isolated AST execution proves an image imported before load timeout skips cleanup. Reconcile the owned import after any load attempt.
- V0 — no findings: the independent verification-gap reviewer found no coverage gaps after tracing the source paths and CI inclusion.

## Verification

- Build affected projects individually in Debug/source mode first; if blocked, record the exact failure and use the prescribed focused fallback with fresh binaries.
- Run focused historical-query and CI classes, then affected assemblies when justified. Execute local shell/API fixtures without remote publication.
- Run actionlint, warning-override guard and `git diff --check`; independently review and resolve findings.
- Validate the exact full commit message with pinned commitlint and retain successful evidence. Fetch and inspect divergence before the authorized main push; never force push.
