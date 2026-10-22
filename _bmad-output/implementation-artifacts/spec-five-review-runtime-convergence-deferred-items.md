---
title: 'Close five review, runtime and convergence deferrals'
type: 'bugfix'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '7b7898307e8eca3774843b145d339e9986018409'
context: ['AGENTS.md', 'docs/development-guide.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five locally actionable deferrals remain: DW-1 and DW-2 independent review follow-ups, DW-17 Node/npm pinning, the accepted-edit convergence follow-up from `spec-five-admin-deferred-items.md`, and real GitHub push scheduling/concurrency proof from `spec-five-query-release-deferred-items.md`.

**Approach:** Discharge the two reviews against current code and focused tests; pin and qualify the release/browser toolchain; retain an accepted edit notice until current projected details match the submitted changes; and qualify the existing RC workflow using two overlapping authorized main pushes. Commit and push the completed work on main. The five-item request overrides single-goal scope.

## Boundaries & Constraints

**Always:** Preserve identifier compatibility, bounded validation errors, tenant/context cancellation, current authorization, published contracts and existing create convergence. Keep expected edit values only in circuit memory and clear them with existing context/navigation/disposal resets. Distinguish expected-state convergence from a command-specific version witness. Record exact revisions and test results.

**Never:** Change dependency versions other than the selected Node/npm tooling, gitlinks, owner signoffs, runtime deployment, public APIs or prerequisite dispositions. Do not close DW-116, DW-126 or other upstream-dependent work. Do not dispatch a release, alter concurrency to manufacture proof, or claim local assertions execute GitHub scheduling.

## I/O & Edge-Case Matrix

| Scenario | Input or state | Expected behavior | Error handling |
|----------|----------------|-------------------|----------------|
| Identifier review | Current ULID/readable/legacy GUID and unsafe inputs | Existing compatibility and bounded no-send guards remain covered | Resolve verified in-scope findings before closure |
| Prerequisite review | Current gitlinks and historical producer receipts | Exact-source guards pass; unresolved owner gates remain blocked | Missing/mismatched evidence fails closed |
| Toolchain | Locked root and browser installs | Exact Node 24.22.1 and bundled npm 11.19.1 are selected and enforced before install | Wrong Node/npm fails before dependencies install |
| Accepted edit | Existing row or stale/degraded/unrelated detail | Notice remains until healthy Current detail matches submitted details and added child IDs/values | Late reads cannot acknowledge another edit or context |
| Push scheduling | Two real main pushes with overlapping run lifetimes | Separate successful RC push runs retain each exact source and correct before-range | Missing, canceled or nonoverlapping evidence cannot close concurrency proof |

</frozen-after-approval>

## Code Map

- `spec-8-2-identifier-correctness-and-zero-risk-hygiene.md`, `spec-8-3-platform-api-prerequisites.md`, `spec-epic-8-followup-review.md`: historical intent and prior findings; Reuse existing validator, aggregate, client, MCP and prerequisite fitness classes.
- `.github/workflows/ci.yml`: browser setup selects floating Node 24. `.github/workflows/release.yml`: immutable Builds preparation accepts `node-version` and internally runs `npm ci`; a caller step after preparation cannot pin its first install.
- `package.json`, `package-lock.json`, `tests/e2e/package.json`, `tests/e2e/package-lock.json`: preserve locked dependencies; record engine metadata and use project `.npmrc` files to enforce it.
- `CreateEditPartyPage.razor:SubmitAsync/BuildUpdateCommand`: build the actual update once before awaiting; generated child IDs must match the receipt. The current form replaces details and adds children.
- `AdminPortalPartyQueryService.cs`: the staged receipt currently contains only party ID and context. `PartiesAdminPortal.razor`: list identity currently clears all notices; detail reads already have version, freshness and degradation checks to reuse.
- `.github/workflows/rc-gate.yml`: main push groups include the exact SHA and validate `event.before` using push-range mode. Reuse GitHub API run/job metadata and relevant step logs; do not add a second orchestration system.

## Tasks & Acceptance

**Execution:**

- [x] `spec-8-2-identifier-correctness-and-zero-risk-hygiene.md`, `spec-8-3-platform-api-prerequisites.md`: retain separate independent review records, current revision and scope limits; run identifier/MCP guards plus `PlatformApiPrerequisitesTests` and `G8OwnerDeliveryReceiptTests` before closing DW-1/DW-2.
- [x] Root/browser package metadata, project `.npmrc`, CI/release callers and CI fitness tests: pin the selected distribution and npm; qualify actual locked installs and wrong-runtime rejection without changing dependency resolutions.
- [x] `src/Hexalith.Parties.AdminPortal/Services/AdminPortalAcceptedCommand.cs` (new internal type), query service, form and portal: retain a submitted-update snapshot, preserve create identity convergence, and compare complete expected edit fields against current detail under existing authorization/version fences.
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/Components/CreateEditPartyPageTests.cs`, `PartiesAdminPortalComponentTests.cs` and `Services/RecordingAdminPortalApiClient.cs`: cover stale pre-edit values, DOB/organization changes, generated child identities, degraded/non-current reads, supersession, context reset and eventual matching detail.
- [x] Batch evidence and `docs/ci.md`: qualify two logical reviewed main pushes, recording run creation/completion overlap, exact head SHAs, event/branch, successful jobs and logged before-range. Keep the scheduling entry open until this evidence exists.
- [x] `deferred-work.md`: close only the five scoped entries after their checks pass; document results and limits in the batch spec/evidence.

**Acceptance Criteria:**

- Given current identifier and prerequisite code, when the focused checks and independent reviews finish, then DW-1/DW-2 have no unresolved in-scope findings and owner gates retain their dispositions.
- Given either npm project, when installation uses the pinned distribution, then locked install succeeds; when Node or npm differs, then installation fails before mutation.
- Given an accepted edit, when the existing row or old details arrive, then the notice remains; when the current authorized detail contains all expected edits, then it clears.
- Given two actual main pushes, when their RC runs overlap and complete successfully, then each source retains independent range proof; otherwise the scheduling item remains open.

## Implementation Notes

Parent audit: the first four deferrals now have executed checks and separate review records. The fifth requires real pushes after review; its acceptance and final ledger closure remain pending until GitHub supplies that evidence. No local declaration check is counted as scheduling proof.

Approved by the user on 2026-10-22; re-read from disk before implementation. Implementation handoff performs local edits and verification only. The parent performs final independent review, commits and the explicitly authorized main pushes, then records the live scheduling receipt and closes its ledger entry. Leave these parent-owned remote tasks pending in the handoff report.

Clean main at the recorded baseline; no intent gaps. Baseline AppHost startup succeeded, but resources failed or stayed unhealthy; exact-target stop succeeded. The existing Tenants AppHost was preserved. No topology qualification is claimed.

## Spec Change Log

## Review Triage Log

### Final independent review — 2026-10-22

- B1 — medium, patch (same root as E1): superseded submit continuations clear the current form's busy flag; completion must remain owned by its captured generation.
- B2 — high, defer: source tracing confirms that the baseline also sends using the form's loaded authorization without a fresh pre-send context comparison; a tenant switch before submit can use current API credentials with older form values. This batch adds post-acceptance receipt fences but does not introduce that send behavior.
- B3 — medium, defer: baseline form authorization is snapshot-based and does not independently subscribe to authentication transitions. A transient A/B/A context can evade a final signature comparison when no portal scope reset occurs. This is part of the pre-existing form-context invalidation gap, distinct from the new receipt's tested scope reset and disposal boundaries.
- B4 — medium, patch: the newly added post-acceptance authorization lookup can throw from its provider/store and leave the current form busy. Fail closed with bounded unavailable state and generation-owned cleanup; never offer a blind resubmission of an accepted command.
- B5 — medium, patch: terminal erasure and authorization query results currently retain the new expected personal-data receipt. Clear that receipt on current terminal responses without reporting convergence; preserve pending projection-not-found behavior.
- B6 — low, patch: matching details can be requested by reselecting the row, but the new pending-edit notice has no obvious refresh action. Reuse the existing read-only detail refresh and Fluent control/label; no polling system or public API is needed.
- B7 — false: the receipt's only production caller is BuildUpdateCommand, which emits detail replacements and adds only. No reachable caller supplies the three ignored update/removal collections; speculative future-operation guards would add unsupported branches.
- B8 — medium, patch: the asynchronous snapshot test can pass when the receipt is dropped entirely. Assert pending state against mismatching details before the original submitted state clears it.
- B9 — medium, patch (verification evidence): the changed runtime's installs and commitlint CLI pass, but browser configuration/type checks and npm signature verification have not yet been executed with that distribution. Run those actual consumers under the pin without publishing.
- B10 — low, patch (verification evidence): archive identity and no-mutation claims need reproducible commands plus observed before/after digests. Retain an executable qualification driver/receipt rather than only Boolean claims.
- E1 — medium, patch: current source confirms the old-route/new-submit busy-state race; grouped with B1.
- V1 — medium, patch: cooperative cancellation reaches a different form branch than the noncooperative boundary fixtures. Add a token-observing pending API response and verify prompt cancellation, enabled current controls and unchanged navigation/status.
- P1 — low, patch: document the new internal receipt staging/consumption contracts required by the baseline; no behavior changes are needed.

## Verification

- Use focused Debug builds first, recording the pinned module launcher's source-mode blocker separately; use fresh package-mode binaries for focused checks when necessary.
- Run affected AdminPortal and CI test projects and relevant identifier/prerequisite classes; execute Node/npm install/rejection checks with a checksum-verified isolated distribution.
- Run actionlint, warning-policy guard and diff checks; obtain independent final review and resolve findings.
- Validate every exact full commit message with pinned commitlint before use. Fetch and inspect divergence before each authorized non-force main push. Record GitHub scheduler evidence after pushes, then commit and push the closure receipt.

### Local implementation handoff — 2026-10-22

- Working-tree source based on exact revision `7b7898307e8eca3774843b145d339e9986018409`; no gitlinks, prerequisite dispositions, owner signoffs or public APIs changed.
- Pinned Node 24.22.1 and bundled npm 11.19.1 for browser, commitlint and release preparation. Both engine-strict projects passed real locked installs plus wrong-Node and wrong-npm rejection before mutation; dependency lock resolutions are unchanged.
- Accepted edits retain the submitted immutable command values and generated child identities in circuit memory. Only healthy explicitly Current details containing every expected replacement and added child value reconcile them. Existing create identity convergence remains. Detail versions and receipt identity reject superseded reads; submitting authorization, form generation, scope lifetime and disposal reject late submissions. Expected-state equality is not a command-specific version witness.
- Focused tests passed: service identifier/contact/hygiene/prerequisite/owner 172, aggregate create/composite/identifier 110, client command/query/admin-GDPR 117, MCP 94. The affected full AdminPortal suite passed 267 and the CI suite passed 429, with no skips. Exact commands and final results are in [local validation](tests/five-review-runtime-convergence-deferred-items-2026-10-22/validation.json).
- The initial launcher attempt returned HXR011 with no configured Dapr home. Supplying the existing prepared isolated home reached HXP020: the mapped Parties source project is unavailable; no resources started. Fresh package-mode Debug binaries supply focused evidence and do not qualify topology or the source launcher.
- The initial delayed-disposal test completed its response before bUnit applied renderer disposal. It now awaits the component's disposal on the renderer before completing the noncooperative response; its initial diagnostic is retained separately from the final passing suite.
- Actionlint, warning-policy guard and CRLF-aware diff check pass. Published local evidence redacts home/temp paths; local originals remain outside the repository.
- DW-17 and expected-edit convergence are locally closed. Parent retains the two separate DW-1/DW-2 review records, performs final independent review, commits and authorized main pushes, and records live scheduler proof. The scheduling ledger entry and remote task remain open until two successful overlapping real main-push RC runs prove their exact heads and before-ranges. No release, remote mutation or scheduling qualification was performed by this handoff.

### Parent verification after review

All patch findings are resolved. The fresh Debug/package AdminPortal build has zero warnings/errors and all 283 tests pass without skips. Together with the unchanged focused service/aggregate/client/MCP and CI evidence, 1,205 .NET cases pass. The reproducible Node driver passes ten commands, including eight browser configuration cases, type checking and registry signature/attestation verification. Exact pinned commitlint validates all three intended messages. B2/B3 remain one explicitly recorded pre-existing high-severity form-context follow-up; B7 is rejected for an unreachable caller shape. The real scheduling task is still pending and the spec remains in-review until the reviewed main pushes provide its receipt. See `tests/five-review-runtime-convergence-deferred-items-2026-10-22/parent-verification.json`.

### Live acceptance and completion

Remote main advanced through an unrelated eight-line CI assertion change during implementation. The two reviewed commits were rebased onto `3f377691c6bbe2d1f7b3a5c515b76b873379cc73` without conflicts; the fresh affected CI class passed 37/37 with zero warnings/errors and no skips. The original baseline identifier above is preserved.

Authorized reviewed pushes `a450220208403ba9b3dd1a0f2a14143063bf8329` and `28598290cf97e4e69961a8a9fde3f77e96f89f63` produced successful RC runs 39820004780 and 39820014459. Their lifetimes overlap twelve seconds; their actual gate jobs overlap four seconds. Both logs prove the exact preceding main tip as event.before and a successful push-range gate. See the [live scheduler receipt](tests/five-review-runtime-convergence-deferred-items-2026-10-22/scheduler-proof.json). All six tasks, all five matrix rows and all four acceptance criteria are now satisfied. The five selected ledger items are closed; the pre-existing form-context authorization finding remains a separate high-severity deferral.
