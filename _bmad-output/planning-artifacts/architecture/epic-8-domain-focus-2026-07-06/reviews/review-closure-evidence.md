# Reviewer Gate — Closure-Evidence Integrity Review

- Lens: CLOSURE-EVIDENCE INTEGRITY (ad-hoc)
- Target: `ARCHITECTURE-SPINE.md` §7 "Story 8.10 Closure Evidence Map — 2026-08-18"
  (rows between `epic-8-invariant-map:start`/`:end`, spine lines 174–193)
- Date: 2026-08-18
- Mode: VALIDATE-only (no spine or project file modified)

## Verdict

The evidence map is substantially honest and load-bearing, not decorative. All
29 named test classes exist under `tests/`, all five deferral IDs exist as
`status: accepted` entries in `deferred-work.md` (lines 368–413) with owner,
exit-proof, rollback, and evidence fields whose scope matches what each spine
row attributes to them, and the map is itself machine-verified by the new
`EpicEightClosureFitnessTests.InvariantMapCoversI1ThroughI15WithExecutableOrDeferredEvidence`
(row completeness, named-class existence, deferral naming, and all-five-deferrals
coverage are fail-closed). The headline claim "No deferred item is represented
as delivered" holds for the five accepted deferrals: sprint-status keeps 8.7/8.8
blocked, 8.9 backlog, 8.10 review, epic-8 in-progress, and the test-summary
closure verdicts explicitly refuse `done` pending immutable receipts. Three
medium findings temper the verdict: the I2 E2E citation is environment-gated and
vacuously green without Docker/DAPR (and did not execute a live topology in the
recorded closure lane); the I13/I14 picture is one remediation out of date — the
same-day authorized FrontComposer shell adoption already delivered a slice of
what the 8.9 ledger entry's rollback text still says is retained; and several
"Executable"-only rows (I7 foremost) omit that accepted 8.6 residual-review-debt
items touch their substance (erasure-certificate identity validation, Memories
cleanup races, unbounded Art.30 read model). No fabricated evidence, no
critical or high findings.

## Per-row verification

Legend: exists = every named test class found under `tests/`;
honest = assertions plausibly cover the invariant slice the row claims;
deferral = named deferral ID(s) exist as accepted ledger entries with matching scope.

| Row | Test exists? | Coverage honest? | Deferral backed? | Notes |
| --- | --- | --- | --- | --- |
| I1 | Yes | Yes | Yes | `DocumentationFitnessTests.MaintainedDocumentationDescribesSdkRoutesUnderEventStoreOnlyDenyAcl` (tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:116–143) asserts the real ACL file `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`: `defaultAction: deny` ×2, `eventstore` as the only appId, the exact 13 routes matching the spine's I1 list, POST-only, no `/**`. `RuntimeDeploymentIsExternallyOwnedAndRetiredAssetsRemainAbsent` (:146–171) asserts `deploy/` absent and no `*DeployValidation*` tests. `ArchitecturalFitnessTests` adds `PartiesAssembly_HasNoPublicRestControllerSurface`, `PartiesAppHost_KeepsPartiesAppIdAndDedicatedDaprAccessControl` (tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs). Runtime ACL enforcement correctly deferred to `8.6-residual-review-debt` (deferred-work.md:375–381, exit_proof names "enforce the deny-default EventStore-only DAPR ACL in a runnable topology"); environment orchestration to `external-runtime-deployment` (:407–413). Honest split of static vs runtime. |
| I1a | n/a (deferred-only) | n/a | Yes | `8.8-runtime-boundary-cleanup` (deferred-work.md:391–397): rollback "Keep … Parties AppHost", exit_proof "…before deleting Parties-local paths or retiring the AppHost" — matches the row's topology/security/publish/rollback-parity condition. |
| I2 | Yes | Partially (see F1) | Yes | `RetiredLeafProjectFitnessTests.PartiesHost_UsesEventStoreDomainServiceSdkAfterStory85Cutover` guards the SDK host shape at source-text level (ledger itself notes "verified only as literal source text", deferred-work.md:169–176). `EventStoreGatewayE2ETests` has one test that starts `if (!_fixture.IsAvailable) { return; }` (tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs:35–38) — silently green without Docker/DAPR; the fixture's `RequireSeededTenants()` throws unconditionally (tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyFixture.cs:159). The deferred half (authenticated end-to-end handler-discovery) is honestly assigned to `8.6-residual-review-debt`, which matches its exit_proof verbatim. |
| I3 | n/a (deferred-only) | n/a | Yes (minor gap, F7) | 8.7/8.8/8.9 rollback fields each enumerate retained local paths (deferred-work.md:388, :396, :404). The invariant's "release recovery" rollback path has no named owner in the row; runtime rollback lives in `external-runtime-deployment` (:412), which I3 does not cite. |
| I4 | Yes | Yes | n/a | Pinned identities in the row match test constants exactly: `PayloadProtectionEventStoreSha = "454b4d100c8c095abf5077c6a8d408da6681e87e"`, `CommonsSha = "6fbac0c5dff2b8a58e90732c51b31911421a8a65"`, `BuildsSha = "17b1c7aae3e1854e464f17bd88d527f8350ea203"`, EventStore package `3.95.0` (tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:18–21, :600, :621). "Separately" is real: `FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph`, `AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity`, plus fail-closed negative tests. EventStore/Commons gitlinks match HEAD; Builds gitlink is an uncommitted working-tree advance (F5). The formerly RED `Matrix_ValidationEvidenceCommandsAreReproducible` (ledger 2026-08-04 note) is reconciled to 3.95.0 and reported 16/16 green in test-summary.md:560. |
| I5 | Yes ×5 | Plausible (name-level check) | n/a | `ContractsPublicApiSnapshotTests` (tests/Hexalith.Parties.Contracts.Tests/Package/), `ClientPackageTests`, `PartyPickerPackagingTests`, `AdminPortalPackagingTests`, `ConsumerPortalPackagingTests` all exist in the matching per-package test projects — exactly the Client+Contracts+3 RCL surface I5 names. Not deep-read; corroborated by the "Package/API validation … all 9 release packages" receipt (test-summary.md:566). |
| I6 | Yes ×3 | Plausible | Yes | Classes exist (tests/Hexalith.Parties.Tests/Gateway/, tests/Hexalith.Parties.Client.Tests/, tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs). `8.8-runtime-boundary-cleanup` exit_proof covers "G6 envelopes/freshness … typed clients" — matches "future shared-helper adoption". |
| I7 | Yes ×3 | Yes for named slice (see F3) | n/a (Executable-only) | `PartyAggregateConsentTests`/`PartyAggregateErasureTests` exist (tests/Hexalith.Parties.Server.Tests/Aggregates/). `ErasureVerificationServiceTests` (17 tests) covers status semantics, sanitized failures, cancellation, checkpoint resume — but nothing validates erasure-certificate identity/tenant/status, exactly the open accepted 8.6-review item "Validate erasure-certificate identity, status, and destroyed key versions before certifying store cleanup" (deferred-work.md:348–350). Multiple open Memories-cleanup items also touch D7 cross-submodule verification substance (:328–347). Row names no deferral. |
| I8 | Yes ×3 | Plausible | Yes | `CryptoKeyManagementCompatibilityHarnessTests` (tests/Hexalith.Parties.Security.Tests/), `AdminPortalGdprPrivacyGuardrailTests` (tests/Hexalith.Parties.Contracts.Tests/AdminPortal/) exist. `8.7-data-protection-extraction` (deferred-work.md:383–389) owns shared-engine extraction; its rollback keeps `Hexalith.Parties.Security`, the adapter, and the compatibility harness — matches. Sprint-status crypto-retention action stays open (sprint-status.yaml:298–318). |
| I9 | Yes | Yes | n/a | `PartySdkProjectionHandlerTests` — 75 tests (tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs) directly covering every claimed noun: replay (`DetailRebuildPlan_MatchesNormalReplayAfterTimestampNormalizationAsync`), checkpoint (`IndexFold_NoOpAfterErasureRetainsTombstoneAndAdvancesSafeCheckpoint`), idempotency (`IndexHandler_RetryAfterSearchFailureReconcilesIdempotentCanonicalWriteAsync`, `Eraser_TransformsAreIdempotentAcrossCleanupRetries`), duplicate (`DetailHandler_IdenticalDuplicateSequenceAppliesOnceAsync`, `…DuplicateDeliveryReturnsAlreadyCompletedAsync`), out-of-order (`DetailFold_DuplicateAndOutOfOrderBatchMatchesOrderedReplayWithoutFreshnessDrift`). Strongest row in the map. Residual 8.6 quality debts (unbounded Art.30 `Records`, null-dictionary recovery) remain open in the ledger but do not contradict the behavior-preservation claim (F3 note). |
| I10 | Yes ×2 | Yes | n/a | `PartySdkQueryHandlerTests` — 49 tests covering rebuild/freshness/stale-read/erased-index/tombstone: `DetailHandler_StateStoreFailureReturnsTenantScopedLastKnownDataAsStaleAsync`, `DetailHandlers_ErasedPartyReturnOnlyRedactedStateAsync`, `IndexHandler_LaterSameIdCreateAfterErasureCannotRestoreEntryAsync` (in the projection class), `CleanupThenDelayedOldEventsCannotRestoreAnyCanonicalReadModel`, cross-tenant no-leak on degraded reads. `ProjectionFreshnessAndDegradationTests` (6 tests) covers bounded freshness vocabulary and degraded-header stripping. Matches the row's claim precisely. |
| I11 | Yes ×2 | Plausible | Yes | `IdentifierHygieneFitnessTests` (tests/Hexalith.Parties.Tests/FitnessTests/), `PartyAggregateIdentifierTests` (tests/Hexalith.Parties.Server.Tests/Aggregates/) exist. `8.8-runtime-boundary-cleanup` exit_proof includes "G7/G9 claims and identifiers" — backs "future Commons-helper adoption". |
| I12 | Yes ×2 | Yes (see F4) | n/a (Executable-only) | `DocumentationFitnessTests.SourceAndTestProjectInventoryIsDocumentedExactly` (:72–113) pins the 15 runnable lanes out of `scripts/test.ps1` against `ExpectedRunnableProjects`. `PartiesContainerPublishWorkflowTests` (8 tests, tests/Hexalith.Parties.Ci.Tests/) pins the 9-package inventory, preflight drift rejection, and publish contract. The "mandatory closure gates" tail is enforced by `EpicEightClosureFitnessTests.ClosureStatusCannotBeDoneBeforeEveryTaskAndValidationReceiptExists` (:181–207), which requires "Release solution build" and "Playwright accessibility" receipts to start with "Pass" before `done` — currently fail-closed (the last `### Validation receipts` table records both as **Blocked**, test-summary.md:563, :569). Unmentioned: open ledger item "Restore the Playwright browser accessibility lane as a required CI gate" (deferred-work.md:296–298). |
| I13 | Yes ×2 | Yes for current tree (see F2) | Yes, but stale slice | `MainLayoutAccessibilityTests` (3 tests) now asserts *FrontComposer* skip links/landmarks (tests/Hexalith.Parties.UI.Tests/MainLayoutAccessibilityTests.cs) — i.e., the shared-shell slice of 8.9 is already adopted in today's tree (MainLayout.razor.css deleted; test-summary.md:591–610 "Authorized dependency and shell remediation"). The row's "8.9 owns future shared-primitive adoption" and the 8.9 ledger rollback "Keep the Parties … skip links … until each replacement slice proves parity" (deferred-work.md:404) are one remediation out of date for that slice. `PartiesAccessibilitySpecimenTests` exists. |
| I14 | Yes ×2 | Plausible | Yes | `MyConsentPageTests`/`MyPrivacyPageTests` exist (tests/Hexalith.Parties.ConsumerPortal.Tests/Components/). `8.9-frontcomposer-ui-consolidation` exit_proof explicitly includes "GDPR copy" parity — backs "shared-copy consolidation". Same staleness caveat as I13 for the shell slice. |
| I15 | Yes | Yes with narrow-slice caveats (F8) | n/a | `EpicEightClosureFitnessTests.EpicEightAddsNoPrdFunctionalRequirement` (:155–178): asserts spec/spine zero-PRD text, `git diff --name-only 37f4ec8…` over `parties-ui-prd.md`+`epics.md` is empty (both files exist), and no untracked canonical scope artifacts. Diff baseline is only the current HEAD commit and is skipped entirely on shallow checkouts (`TryRunGit cat-file` guard, :170 — a deliberate spec review-patch, spec-8-10:59); "cannot be reported as MVP feature delivery" is enforced as prose-string presence, which is the practical ceiling for a fitness test. |

## Cross-artifact consistency

- **Ledger**: all five deferral IDs present once each, in the exact order the
  fitness test's `ExpectedDeferrals` requires; every entry has non-empty
  owner/exit_proof/rollback/evidence (machine-checked by
  `AcceptedDeferralsAreCompleteAndKeepIncompleteStoriesHonest`, which also pins
  8.6=done-with-remaining-`[Review][Defer]`-items, 8.7=blocked, 8.8=blocked,
  8.9=backlog against `sprint-status.yaml`). Verified against the actual files:
  sprint-status.yaml:144 `epic-8: in-progress`, :167 `8-6…: done`,
  :178 `8-7…: blocked`, :185 `8-8…: blocked`, :186 `8-9…: backlog`,
  :202 `8-10…: review`.
- **Spec 8.10** (`spec-8-10-final-readiness-documentation-and-retirement-gate.md`):
  status `in-review` (line 5) — consistent with sprint `review` via the test's
  alias normalization. Task 3 (line 51) explicitly required "an
  executable-or-deferred I1-I15 map", so §7 is the deliverable the spec's own
  gate demands; the spec's Never rule ("Do not … close 8.10/Epic 8 with missing
  evidence", line 27) agrees with both test-summary closure verdicts
  (test-summary.md:586–589, :628–632), which keep 8.10/Epic 8 open pending the
  `authorized-owner-fixes-not-immutable` blocker (:622–626).
- **"No deferred item is represented as delivered"**: holds in the forward
  direction for all five accepted deferrals — no row claims executable coverage
  for anything a deferral owns; the deferred nouns in each row match the ledger
  exit_proofs. The one inversion found is the *reverse* direction (a delivered
  slice still represented as deferred): see F2.

## Findings

### F1 — MEDIUM — I2's E2E citation is environment-gated and vacuous in the recorded closure lane
`EventStoreGatewayE2ETests` contains one test whose first statement is
`if (!_fixture.IsAvailable) { return; }`
(tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs:35–38),
so without Docker/DAPR it reports green while executing nothing, and the recorded
all-lane closure run notes the topology skips (test-summary.md:564). The fixture
itself documents that no tenant is seeded and `RequireSeededTenants()` throws
unconditionally (PartiesAspireTopologyFixture.cs:22–33, :159). Citing this class
as executable evidence that it "guards the EventStore SDK host shape" overstates
what runs outside infrastructure-equipped environments; the honest load-bearing
guard in I2 is `RetiredLeafProjectFitnessTests` (source-text level, as the ledger
already concedes at deferred-work.md:169–176). Suggest the I2 row qualify the E2E
citation ("topology-gated") or drop it in favor of the deferral it already names.

### F2 — MEDIUM — I13/I14 and the 8.9 ledger entry are stale against the same-day authorized shell adoption
Test-summary's "Authorized dependency and shell remediation — 2026-08-18"
(test-summary.md:591–618) records that Parties already adopted the shared
FrontComposer shell (skip links, landmarks, status selectors), and
`MainLayoutAccessibilityTests` now asserts FrontComposer-provided skip links
(tests/Hexalith.Parties.UI.Tests/MainLayoutAccessibilityTests.cs;
MainLayout.razor.css deleted in the working tree). Yet §7 I13/I14 still say
`8.9-frontcomposer-ui-consolidation` "owns future shared-primitive adoption /
shared-copy consolidation", the ledger's 8.9 rollback still reads "Keep the
Parties picker, freshness/status regions, … skip links … until each replacement
slice proves parity" (deferred-work.md:404), and sprint-status keeps 8.9 at
`backlog`. The shell slice is delivered-in-tree but represented as wholly
deferred, and the described retained-rollback surface no longer matches the tree
for that slice. Update the 8.9 entry (and I13/I14 wording) to carve out the
adopted shell slice, or record it as a partial-delivery annotation; otherwise the
map's own honesty claim degrades at the next review.

### F3 — MEDIUM — "Executable"-only rows omit accepted residual debt touching their substance (I7 foremost)
`ErasureVerificationServiceTests` never validates certificate identity/tenant/
status — precisely the open accepted item "Validate erasure-certificate identity,
status, and destroyed key versions before certifying store cleanup"
(deferred-work.md:348–350) — and several open Memories cleanup/mapping-ledger
items (:320–347) sit squarely in I7's "two-front-door erasure + cross-submodule
verification (D7)" substance. Similar open quality debts touch I9/I10 (unbounded
Art.30 read model :352–354, null-dictionary recovery :356–358, search input
bounds :360–362). These are all owned by the `8.6-residual-review-debt` umbrella,
but the map presents that umbrella only in I1/I2 and scopes it to "runtime ACL
enforcement" and "handler-discovery proof". Rows I7/I9/I10 marked pure
"Executable" therefore read as complete guards when accepted debt narrows them.
Not evidence-washing (behavior-preservation is genuinely tested), but the rows
should either carry an "+ deferred (8.6-residual-review-debt)" tag or the
umbrella's map description should state it owns *all* unchecked 8.6 review debt.

### F4 — LOW — I12 omits the deferred CI accessibility lane
Open ledger item "Restore the Playwright browser accessibility lane as a
required CI gate" (deferred-work.md:296–298: the replacement CI workflow no
longer runs `npm run test:a11y`) is relevant to I12's "Playwright receipts remain
mandatory closure gates" claim. Closure-time enforcement exists
(`ClosureStatusCannotBeDoneBeforeEveryTaskAndValidationReceiptExists` requires a
passing "Playwright accessibility" receipt), so the row is not false, but the
continuous-CI enforcement gap is unmentioned in the map.

### F5 — LOW — Builds pin `17b1c7aa…` is an uncommitted working-tree gitlink
`git ls-tree HEAD references/Hexalith.Builds` = `6b7807533…`; the working tree
has `17b1c7aae3e1854e464f17bd88d527f8350ea203` (v4.24.0), matching the I4 row and
`PlatformApiPrerequisitesTests.cs:18`. Intentional per the checked spec review
finding (spec-8-10:56), and consistent with the standing
`authorized-owner-fixes-not-immutable` blocker's stance on non-immutable
receipts — but until committed, the I4 pin evidences the tree, not the repo
history.

### F6 — LOW — Open ledger items with no §7 or deferral pointer
`DW-1`/`DW-2` follow-up-review items are `status: open`
(deferred-work.md:15–27; DW-2 is I4-adjacent as an 8.3 follow-up), and the
pre-DW-format un-statused entries (e.g., support-safe consent/channel identifier
contract :11–13 — I7/I11-adjacent; Memories LRU/migration tests :34–39) are
implicitly open. None is represented as delivered, but §7 gives no pointer that
any accepted deferral owns them; they are outside the five-ID closure set.

### F7 — LOW — I3's "release recovery" rollback path has no named owner in the row
Invariant I3 (spine line 79) enumerates "projection, query, crypto, release
recovery" rollback paths; the I3 row names 8.7/8.8/8.9 but not
`external-runtime-deployment`, which is where runtime/release rollback ownership
actually lives (deferred-work.md:412: rollback "redeploys the prior immutable
image set").

### F8 — LOW — I15's zero-PRD guard is narrow by construction
The git diff runs only against baseline `37f4ec8` (current HEAD) and is skipped
on shallow checkouts (`TryRunGit … cat-file`, EpicEightClosureFitnessTests.cs:170
— a deliberate, spec-recorded patch), so historical Epic 8 commits are not
re-verified; the "never reported as MVP feature delivery" clause is enforced as
required prose strings. Acceptable for a fitness test, but the row's "verifies
that Epic 8 changes no PRD functional-requirement artifact" quietly means
"since the last commit, on full clones".

## Finding count

- Critical: 0
- High: 0
- Medium: 3 (F1, F2, F3)
- Low: 5 (F4, F5, F6, F7, F8)
