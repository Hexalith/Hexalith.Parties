---
title: '8.10 Final readiness, documentation, and retirement gate'
type: 'refactor'
created: '2026-08-17'
status: 'done'
baseline_commit: '37f4ec826c6f4aea4651cfbad94fb6ab7fc4f0a0'
review_loop_iteration: 3
context:
  - '{project-root}/_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Epic 8 cannot close while 8.7/8.8/8.9 are blocked, deferrals are incomplete, four dependency receipts are stale, and docs describe retired surfaces.

**Approach:** Use a preflight-first, deferral-based closure: reconcile immutable identities and explicit deferrals, refresh docs and executable fitness coverage, then record green validation or leave Epic 8 open. Add no PRD functional requirement.

## Boundaries & Constraints

**Always:** Preserve I1-I15, public compatibility, `.slnx`, CPM, warnings-as-errors, and root-only submodules. Keep current 8.7-8.9 statuses unless their own evidence supports another existing status. Each deferral names an owner, exit proof, rollback, and evidence; record package and source identities separately.

**Ask First:** Dependency, submodule, owner-repository, rollback-deletion, owner-commitment, or PRD changes.

**Never:** Treat a matrix label, checkout, compile, skip, or historical pin as consumption proof. Do not restore retired deploy assets, invent a deploy lane, weaken gates, mark 8.7-8.9 done, or close 8.10/Epic 8 with missing evidence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Deferral closure | 8.7-8.9 incomplete | Accepted deferrals preserve rollback and permit closure | Missing field leaves Epic 8 open |
| Identity check | Package/source graph consumed | Matrix matches exact releases/gitlinks and consumers | Mismatch blocks closure |

</frozen-after-approval>

Administrator renegotiated the frozen Problem sentence on 2026-09-06
(`sprint-change-proposal-2026-09-06-sprint-status-key-freeze-and-blocked-status.md`):
Story 8.9 is `blocked`, not `backlog`.

## Code Map

- `_bmad-output/implementation-artifacts/{sprint-status.yaml,story-8-3-platform-api-prerequisite-matrix.md,deferred-work.md}` -- statuses, stale receipts, open G4-G11 gates, and deferral ledger.
- `Directory.Build.props:19`, `Directory.Packages.props:5`, `src/Hexalith.Parties/Program.cs:18`, `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:159`, `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs:401` -- selected dependency modes and consumers.
- `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md:50`, `docs/{architecture,source-tree-analysis,index,data-models,getting-started,api-contracts,component-inventory}.md`, `README.md` -- stale ACL, actor, inventory, and deployment claims.
- `tests/Hexalith.Parties.Tests/FitnessTests/` -- reuse current boundaries and add documentation, identity, deferral, zero-PRD, and I1-I15 gates; retired DeployValidation tests remain deleted.
- `scripts/test.ps1`, `_bmad-output/implementation-artifacts/tests/test-summary.md` -- lane inventory and final evidence.
- `src/Hexalith.Parties.UI/Services/PartiesAccessibilitySpecimenUserContextAccessor.cs`, `tests/Hexalith.Parties.UI.Tests/{MainLayoutAccessibilityTests,PartiesAccessibilitySpecimenScopeTests}.cs` -- 2026-10-04 fixture repair: provide valid shell scope only at the enabled accessibility specimen route and preserve authenticated context on ordinary routes.

## §4 Readiness Gate — activation of `8.9-frontcomposer-ui-consolidation` (added 2026-08-19)

Spine I17 requires the six §4 clauses from any spec that *works* an accepted
closure deferral. Story 8.10 authored four deferrals and activated exactly one:
the FrontComposer shell slice (G4 work package F). Authority:
`_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-19-story-8-10-frontcomposer-shell-slice-backfill.md`.
The other three entries (`8.7-data-protection-extraction`,
`8.8-runtime-boundary-cleanup`, `external-runtime-deployment`) are recorded with
`authored_by_spec` and carry no §4 obligation here.

1. **Prerequisites.** G4 work package F only — owner-certified parity for the
   existing FrontComposer shell skip links. Consumed at root gitlink
   `5cbc5583142a6774ff7813698ad98ec267b336f0` (`v4.3.0-7-g5cbc5583`), recorded
   in the Story 8.3 reconciliation table (amended 2026-09-06; originally recorded
   at `7a337a21d4ba261bf27aeb3feedde47789f0160a` / `v4.1.1-104-g7a337a21`, superseded
   by the 2026-09-05 catalog adopt). Packaged `4.3.0` remains the CI, bUnit,
   and released-container identity; the two are recorded separately because a
   package version never proves a gitlink. Prior stories: 8.1-8.6 done.
2. **Touched repos/submodules.** Parties (`src/Hexalith.Parties.UI`,
   `tests/Hexalith.Parties.UI.Tests`, `tests/e2e`) and
   `references/Hexalith.FrontComposer` (consumed, not edited in this repo).
   EventStore, Commons, Builds, and `deploy` are untouched by this slice.
3. **Rollback path.** Restore the Parties-owned `.parties-skip-link` anchors,
   `#parties-main-content`, and `#parties-app-navigation` from the parent of
   `2b63ab9`, restore the deleted `MainLayout.razor.css` rules, and pin
   FrontComposer back to `97f44c499e83a0ffbf054febd0aab384054ea39e`. The revert
   reinstates the duplicate skip-link strict-locator ambiguity, so it must be
   paired with a Playwright rerun. No other Parties UI primitive may be deleted.
4. **Validation lanes.** `tests/Hexalith.Parties.UI.Tests` run directly as an
   xUnit v3 assembly (`-class Hexalith.Parties.UI.Tests.MainLayoutAccessibilityTests`,
   `-class Hexalith.Parties.UI.Tests.AccessibilityStyleGuardTests`), plus
   `npm --prefix tests/e2e run test:a11y`. Parity evidence required before the
   slice counts as delivered: skip-link ordering, both landmark ids and labels,
   programmatic focus targets, and an app-content focus indicator under both
   normal and forced-colors media.
5. **Non-goals.** Do not delete the Parties picker, per-record freshness/status
   regions, browser download helpers, typed-name erasure confirmation,
   optimistic reconciliation, or portal components. Do not promote the G4 matrix
   row to `available`. Do not mark Story 8.9 `done`; workflow status is `blocked`.
6. **Parity-evidence checklist.** I13 only. I5 is unaffected (no public package
   shape change); I14 GDPR copy is unchanged by this slice. **I13 is not yet
   discharged:** the 6/6 Playwright receipt focuses `.fc-skip-link`, which has
   its own `fc-shell.css` rules, and never focuses a content control, so it does
   not cover the dead `MainLayout.razor.css` focus-visible and forced-colors
   rules. I13 parity requires that regression repaired and a content-control
   focus assertion added.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/{story-8-3-platform-api-prerequisite-matrix.md,deferred-work.md,sprint-status.yaml}` -- reconcile 8.6 and append its accepted residual-review-debt umbrella; append accepted 8.7-8.9 and external-runtime deferrals; record EventStore package `3.95.0` and source `454b4d100c8c095abf5077c6a8d408da6681e87e`, Commons HTTP source `6fbac0c5dff2b8a58e90732c51b31911421a8a65`, and Builds catalog `17b1c7aae3e1854e464f17bd88d527f8350ea203`. Halt if incomplete.
- [x] `docs/*.md`, `README.md`, and the Epic 8 spine -- document 13 source projects, 15 runnable tests plus one support host, SDK projection/query routes under the deny-default EventStore-only ACL, and external runtime deployment ownership.
- [x] `tests/Hexalith.Parties.Tests/FitnessTests/{DocumentationFitnessTests,EpicEightClosureFitnessTests,PlatformApiPrerequisitesTests}.cs` -- pin maintained paths/inventory, actual dependency selection, deferral completeness, I15, and an executable-or-deferred I1-I15 map.
- [x] `_bmad-output/implementation-artifacts/tests/test-summary.md` and `sprint-status.yaml` -- append exact pins/results/rollback/blockers; close 8.10/Epic 8 only after every assertion and required lane passes.

### Review Findings

- [x] [Review][Patch] Advance and pin references/Hexalith.Builds SHA 17b1c7aae3e1854e464f17bd88d527f8350ea203 and reconcile submodule gitlinks in matrix, tests, and documentation [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1468]
- [x] [Review][Patch] Deletion of MainLayout.razor.css removes deep focus-visible and forced-colors rules on body controls [src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor.css:1-44]
- [x] [Review][Patch] AccessibilityStyleGuardTests brittle static file read on external FrontComposer stylesheet [tests/Hexalith.Parties.UI.Tests/AccessibilityStyleGuardTests.cs:58-67]
- [x] [Review][Patch] EpicEightAddsNoPrdFunctionalRequirement fragile git diff on shallow CI checkout [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:170]
- [x] [Review][Patch] DocumentationFitnessTests omits docs/project-overview.md from project inventory verification [tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:11-24]
- [x] [Review][Patch] Deferrals table in story-8-3... not verified against deferred-work.md in fitness tests [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:150]

#### Round 2 — code review of the diff vs. baseline `37f4ec8` (2026-08-19)

- [x] [Review][Decision] Shell adoption executed a deferred 8.9/G4 slice without reconciling the gate artifacts — `MainLayout.razor` dropped the Parties-owned skip links and `role="main"`/`role="navigation"` landmarks for `FrontComposerShell`, yet Story 8.9 is `backlog`, the 8.3 "FrontComposer UI primitives" row is still `needs-additive-api` with an explicit "keep rollback path … skip links" clause, and the 8.9 deferral rollback gained a "Delivered-slice carve-out" describing work already done. `tests/test-summary.md:591-599` records that the change was authorized, so the open question is propagation: reconcile the matrix row + spine §7 I13/I14 + sprint-status, raise a formal SCP, or revert the slice.
- [x] [Review][Decision] Spec activates four accepted deferrals without the six §4 clauses, and the §7 map omits I16-I18 — `deferred-work.md` records `source_spec: spec-8-10-…` for 8.7/8.8/8.9/external-runtime, which spine I17 says may be worked only through a spec declaring all six §4 clauses; this spec has no Prerequisites, Touched repos, Rollback path, Non-goals, or Parity-evidence checklist. Separately `InvariantMapCoversI1ThroughI15…` and the §7 map cover I1-I15 only, leaving the three gate-integrity invariants with neither executable evidence nor a named deferral. Both touch `<frozen-after-approval>` text ("Preserve I1-I15"), so amending needs your call.
- [x] [Review][Decision] Accessibility lane integrity after the shell adoption — the Playwright webServer now forces source mode (`-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`) and drops `--no-build`, so the a11y receipt is produced against a FrontComposer checkout 104 commits past the packaged `4.1.1` that CI bUnit and the released container actually use; the skip-link test now pre-focuses `.fc-shell-root`, so it no longer proves "first two keyboard tab stops" as its name, the AC, and `docs/accessibility.md` all still claim; and no workflow runs the lane at all. Restore package-mode + strict assertions (may fail and route defects to FrontComposer owners), or record these as accepted 8.9 debt. [tests/e2e/playwright.config.ts:41]
- [x] [Review][Patch] MainLayout.razor.css focus-visible and forced-colors rules are dead — MainLayout renders no HTML element, so Blazor emits no `b-*` scope attribute and the `::deep` selectors match nothing; fc-shell.css has no generic content-control focus rule to replace them [src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor.css:1]
- [x] [Review][Patch] AssertGitlinkAndCheckout accepts a bare working-tree checkout as gitlink proof — the third disjunct is already guaranteed by the following DescribeIdentityGap assertion, so a divergent gitlink passes [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1449]
- [x] [Review][Patch] test-summary closure evidence contradicts the tree at HEAD — claims the superproject "still records 97f44c49…" and that no gitlink receipt exists, while HEAD records FrontComposer 7a337a21 and PolySer 0dca9e9d [_bmad-output/implementation-artifacts/tests/test-summary.md:614]
- [x] [Review][Patch] Three submodule gitlinks advanced with no recorded immutable identity — FrontComposer 7a337a21, Memories 003fd214, PolySer 0dca9e9d appear nowhere in _bmad-output, and FrontComposer is now actively consumed for landmarks and skip links [_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:60]
- [x] [Review][Patch] ParseValidationReceipts has no section terminator, so superseded Blocked rows decide the closure gate — LastIndexOf slices to end of file, swallowing the later remediation tables; also ContainsKey("Playwright accessibility") matches the stale row, not "npm and Playwright accessibility" [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:313]
- [x] [Review][Patch] AccessibilityStyleGuardTests forced-colors assertions are conditional and the reduced-motion guard was deleted outright — passes vacuously on any package-mode or local clone without the FrontComposer submodule, and reads submodule source rather than the shipped package asset [tests/Hexalith.Parties.UI.Tests/AccessibilityStyleGuardTests.cs:66]
- [x] [Review][Patch] EpicEightAddsNoPrdFunctionalRequirement silently no-ops when the baseline commit is unreachable — reports pass, not skip, violating AC3's "failures and skips remain owner-visible" [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:168]
- [x] [Review][Patch] Invariant-map "Executable" evidence is satisfied by a class name appearing anywhere under tests/ — no check that it lives in a runnable project, declares a Fact, or asserts anything about the invariant [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:141]
- [x] [Review][Patch] Cross-story identity-gate assertions for specs 8.6 and 8.8 were deleted while both stories remain blocked, and the fail-closed consumer test now greps this spec's own frozen prose instead of exercising behavior [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1771]
- [x] [Review][Patch] Removing the ls-tree/rev-parse/describe receipt assertions leaves the G5 matrix row claiming v3.91.0 / 1d6e9321 while HEAD is v3.95.0 / 454b4d10, with nothing failing on the contradiction [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1732]
- [x] [Review][Patch] docs/event-publishing.md claims all accesscontrol.*.yaml are deny-default, but accesscontrol.eventstore-admin.yaml is allow-by-default; the fitness test asserts the doc omits the string, cementing the inaccuracy [docs/event-publishing.md:149]
- [x] [Review][Patch] Residual stale actor terminology in maintained docs after the SDK migration [docs/getting-started.md:59]
- [x] [Review][Patch] Test-environment UseStaticWebAssets opt-in has no test — a silent revert regresses the a11y lane to SSR-only with a green build [src/Hexalith.Parties.UI/Program.cs:28]
- [x] [Review][Patch] Visual baseline replaced the role=status/aria-live=polite locator with a text match, dropping the live-region contract; use .first() on the strict locator instead [tests/e2e/specs/parties-accessibility.spec.ts:200]
- [x] [Review][Patch] docs/accessibility.md states bUnit and Playwright gates catch regressions, but no workflow runs the Playwright lane [docs/accessibility.md:3]
- [x] [Review][Patch] EvaluateProjectGraph parses MSBuild stdout as JSON with no preamble tolerance and no timeout, and fails hard on a package-only clone rather than skipping [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1924]
- [x] [Review][Patch] The 8.6 deferral gate is inverted — it requires unchecked Review/Defer items to persist, so resolving Story 8.6 residual debt breaks Epic 8 closure fitness [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:45]
- [x] [Review][Patch] architecture.md §5 dropped crypto-shredding decryption, the post-key-destruction redaction fallback, and the registration-ordering caveat without a replacement anchor [docs/architecture.md:98]
- [x] [Review][Patch] Hexalith.Parties.Authentication is classified three different ways across README, architecture.md, source-tree-analysis.md, and component-inventory.md [README.md:69]
- [x] [Review][Patch] Test-project inventory uses a magic Length.ShouldBe(16) instead of an exact set, so an add+remove pair passes unnoticed [tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:88]
- [x] [Review][Patch] tabUntilTestId focus diagnostics render blank — element.id is an empty string, not nullish, so the ?? chain stops there; the evaluate callback also returns two shapes and testId is never read [tests/e2e/specs/parties-accessibility.spec.ts:149]
- [x] [Review][Patch] MainLayoutAccessibilityTests derives the target id with href[1..], which breaks on the shell's composed path#fragment href at any non-root route [tests/Hexalith.Parties.UI.Tests/MainLayoutAccessibilityTests.cs:62]
- [x] [Review][Patch] Evidence-anchor regex orders cs before csproj, so a .csproj evidence path is truncated to .cs and reported as a missing anchor [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:263]
- [x] [Review][Defer] Only one of six accesscontrol components is verified by the new documentation fitness test [tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:119] — deferred, broadening ACL coverage is outside the 8.10 Code Map
- [x] [Review][Defer] The Playwright accessibility lane is wired into no workflow [.github/workflows/ci.yml:21] — deferred, already a named open ledger item in spine §7 I12
- [x] [Review][Defer] Duplicated timeout-free git/process helpers across three fitness classes; RunGit drains stdout then stderr sequentially [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:414] — deferred, pre-existing pattern and no realistic deadlock at these output sizes
- [x] [Review][Defer] EventStore 3.95.0 is hardcoded in five-plus places rather than read from the catalog property [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:958] — deferred, pre-existing convention across the fitness suite

##### Round 2 resolutions (2026-08-19)

Decisions were resolved as follows. **D1 — shell slice:** backfilled rather than reverted, via `sprint-change-proposal-2026-08-19-story-8-10-frontcomposer-shell-slice-backfill.md`; the matrix gained FrontComposer, Memories, and PolymorphicSerializations identity rows, the G4 row records work package F as delivered while staying `needs-additive-api`, Story 8.9 stays `backlog`, and the 8.9 deferral's rollback clause was restored to a real rollback with the delivered slice moved to a `delivered_slices` field. **D2 — §4 clauses and I16-I18:** the conflated `source_spec` field was split into `authored_by_spec` / `activated_by_spec`, which showed 8.10 activated exactly one deferral; the six §4 clauses were written for that one activation only, and I16-I18 gained an explicit §7a disposition in the spine instead of manufactured executable evidence. **D3 — accessibility lane:** source mode retained (the packaged FrontComposer 4.1.1 predates the shell fixes, and the existing `authorized-owner-fixes-not-immutable` blocker already owns the exit); the strict first-tab-stop restore was attempted and **failed**, which surfaced a genuine shell finding now routed to FrontComposer owners as `frontcomposer-skip-link-reachability-after-route-focus` — the test is renamed to what it actually proves and `docs/accessibility.md` records both the caveat and the fact that no workflow runs the lane.

New finding raised during verification, not by the review layers:

- [x] [Review][Patch] Release solution build is red at HEAD — 16 SA1316 errors, all inside `references/Hexalith.PolymorphicSerializations` at the selected gitlink `0dca9e9d`, none outside it. The 2026-08-18 `Pass` receipt was produced from a modified working tree; the commit that landed carries only part of that fix. The authorized owner working-tree patch now restores the required tuple-element casing and makes the 59-project Release build green, but Epic 8 closure remains blocked until the owner fix is committed and the superproject selects that immutable gitlink. Tracked as `polymorphicserializations-stylecop-fix-incomplete-at-selected-gitlink` [_bmad-output/implementation-artifacts/tests/test-summary.md:563]

#### Round 3 — code review of the diff vs. baseline `37f4ec8` (2026-09-06)

##### Round 3 decisions (2026-09-06)

- **FrontComposer identity:** amend the frozen §4 clause to `5cbc5583142a6774ff7813698ad98ec267b336f0` (`v4.3.0-7-g5cbc5583`), matching reality and `PlatformApiPrerequisitesTests.cs`'s `FrontComposerSha`; also fix the matrix's other stale row still citing `7a337a21`.
- **PolymorphicSerializations identity:** accept `8aeed1d27c9a050bc4bec6d89051aa00de306a69`. Re-verified 2026-09-06: `dotnet build Hexalith.Parties.slnx -c Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` — **0 warnings, 0 errors** (the 16 SA1316 errors from the `0dca9e9d` measurement are gone; `PolymorphicHelper.cs`'s tuple casing is fixed at this gitlink). Reconcile `test-summary.md`, the 8-3 matrix G5 row, and `.gitlink-signoff.tsv` to `8aeed1d2`, and close the `polymorphicserializations-stylecop-fix-incomplete-at-selected-gitlink` and `authorized-owner-fixes-not-immutable` blockers — the owner fix is now a real committed gitlink, not a working-tree patch.
- **Commons sign-off:** add a `validated-advance` row to `.gitlink-signoff.tsv` for `6da79aed2daa4e199689331ee3196f7872c0988a`, dated 2026-09-05 (same catalog-adopt batch as Builds/EventStore/FrontComposer/Memories/Tenants), owner `jpiquot`.
- **8.10 status:** `review` is correct (matches spec frontmatter `in-review`); fix the comment to stop claiming an `in-progress` transition it didn't make. Given the PolySer build is now confirmed green and its owner fix is a real commit, both blockers the comment cites are resolved as of this review round — the comment must reflect that instead.

- [x] [Review][Patch] Amend frozen §4 clause 1's FrontComposer identity to `5cbc5583142a6774ff7813698ad98ec267b336f0` / `v4.3.0-7-g5cbc5583` / packaged `4.3.0`, and fix the matrix's stale row still citing `7a337a21`. [spec-8-10-final-readiness-documentation-and-retirement-gate.md:58-59, story-8-3-platform-api-prerequisite-matrix.md:101] — resolved (checked 2026-10-04): §4 clause 1 cites `5cbc5583142a6774ff7813698ad98ec267b336f0` / `v4.3.0-7-g5cbc5583` / packaged `4.3.0` with its amendment history, and the matrix G4 row cites `7a337a21` only as the original 2026-08-18 adoption in a dated amendment chain.
- [x] [Review][Patch] Reconcile PolymorphicSerializations identity to `8aeed1d27c9a050bc4bec6d89051aa00de306a69` across `test-summary.md`, the 8-3 matrix G5 row, and `.gitlink-signoff.tsv`; close the `polymorphicserializations-stylecop-fix-incomplete-at-selected-gitlink` and `authorized-owner-fixes-not-immutable` blockers now that the Release build is confirmed green at this gitlink. [_bmad-output/implementation-artifacts/tests/test-summary.md:615, story-8-3-platform-api-prerequisite-matrix.md:68, .gitlink-signoff.tsv] — resolved (checked 2026-10-04): `.gitlink-signoff.tsv` has the 2026-09-06 `8aeed1d2` row, test-summary closes both blockers at `8aeed1d2`, and the matrix PolymorphicSerializations row keeps that proof as history; Round 6 D1 later superseded the pin with `98de6e01` (`v1.19.4`).
- [x] [Review][Patch] Add a `.gitlink-signoff.tsv` `validated-advance` row for Commons `6da79aed2daa4e199689331ee3196f7872c0988a`, dated 2026-09-05, owner `jpiquot`. [.gitlink-signoff.tsv] — resolved (checked 2026-10-04): `.gitlink-signoff.tsv` contains `references/Hexalith.Commons|6da79aed2daa4e199689331ee3196f7872c0988a|validated-advance|v2.30.0-19-g6da79ae|jpiquot|2026-09-05`.
- [x] [Review][Patch] Fix sprint-status.yaml's Story 8.10 comment — it must not claim a "moved review -> in-progress" transition when the key's value is `review`; update it to reflect the current status and the now-resolved PolySer/build blockers. [_bmad-output/implementation-artifacts/sprint-status.yaml:216-224] — resolved (checked 2026-10-04): the Round 3 comment no longer claims the transition and records the closed PolySer/build blockers; it now dates `review` to 2026-09-06 and points to the 2026-10-04 lines that explain the current `in-progress` value.
- [x] [Review][Patch] PlatformApiPrerequisitesTests.RunProcess reads stdout synchronously before its timeout applies — `ReadToEnd()` on stdout runs before `WaitForExit(ProcessTimeoutMilliseconds)`; only stderr got the async read a recent commit's message claims added timeout protection for. A hung `dotnet msbuild -getProperty/-getItem` child (used by `EvaluateProjectGraph`) that keeps stdout open blocks forever, and the 300s bound never fires. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1549-1573] — resolved (checked 2026-10-04): `RunProcess` drains stdout and stderr with `ReadToEndAsync` under one shared deadline and kills the process tree on timeout or stream fault (Round 4 patches).
- [x] [Review][Patch] AssertGitlinkAndCheckout accepts a staged-but-uncommitted gitlink via its `indexLink` disjunct — undermines the "must be a real commit" invariant the surrounding comment states; local/pre-commit runs can be fooled by `git add` without `git commit` (CI itself is unaffected since index equals HEAD there). [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1511-1531] — resolved (checked 2026-10-04): `AssertGitlinkAndCheckout` and the G5 check read only `git ls-tree HEAD` (Round 6 D2); no index disjunct remains.
- [x] [Review][Patch] README.md's new DAPR-ACL paragraph omits the one allow-by-default exception — states blanket "deny-by-default" while `docs/event-publishing.md` explicitly documents that `accesscontrol.eventstore-admin.yaml` is `defaultAction: allow`, in this same diff. Repeats the round-2 finding; fixed in one doc but not propagated to README. [README.md:45-48] — resolved (checked 2026-10-04): README.md names the `accesscontrol.eventstore-admin.yaml` `defaultAction: allow` exception in its ACL paragraph.
- [x] [Review][Patch] sprint-status.yaml / deferred-work.md cite superseded dependency identities — Story 8.10's sprint-status narrative and deferred-work.md's "code review of spec-8-10" note still say EventStore 3.95.0/Commons `6fbac0c5`/Builds `17b1c7a`, which docs/ci.md, docs/architecture.md, the 8-3 matrix, and PlatformApiPrerequisitesTests.cs superseded with the 2026-09-05 catalog adopt. Not load-bearing, but misleads a reader of the closure log. [_bmad-output/implementation-artifacts/sprint-status.yaml:198-201, deferred-work.md:389] — resolved (checked 2026-10-04): the sprint-status 2026-08-18 identities are labeled superseded history, DW-92 dates its `3.95.0` citation, and no current claim cites `6fbac0c5` or `17b1c7a`.
- [x] [Review][Patch] Tracked review-prompt artifact commits AI-reviewer-directed instructions to permanent history — `review-verification-gap-8-10-round-3-prompt.md` is tracked, addresses a future AI reviewer directly, references an absolute local path now excluded by this diff's own new `.gitignore` entry (`_bmad/render/`), and cites a PolySer end-SHA matching neither the matrix's SHA nor the actual current gitlink. Delete it (or keep such scratch prompts under the already-gitignored path). [_bmad-output/implementation-artifacts/review-verification-gap-8-10-round-3-prompt.md] — resolved (checked 2026-10-04): `review-verification-gap-8-10-round-3-prompt.md` is neither tracked nor present.
- [x] [Review][Patch] Matrix reconciliation section dated 2026-08-18 but content is 2026-09-05 — the "Story 8.10 final retained-identity reconciliation" section heading predates the EventStore/Builds/FrontComposer rows it contains, which match `.gitlink-signoff.tsv` entries dated 2026-09-05; the section's own text even says the gitlink "tracks the live checkout after the 2026-09-05 catalog adopt." [story-8-3-platform-api-prerequisite-matrix.md:51] — resolved (checked 2026-10-04): the section heading is dated 2026-10-04 and its first paragraph states the 2026-08-18 to 2026-09-13 provenance.
- [x] [Review][Patch] docs/component-inventory.md banner date is stale — says "reconciled for Epic 8 closure on 2026-08-18" but its "External dependencies" line was edited for the 2026-09-05 catalog adopt without bumping the date. [docs/component-inventory.md:3] — resolved (checked 2026-10-04): the banner reads reconciled 2026-10-04 and notes the 2026-09-05 dependency refresh.
- [x] [Review][Patch] Malformed reconciliation-table row silently dropped — `ParseFinalConsumptionRows` does `continue` on any row without exactly 5 pipe-delimited cells instead of failing loudly; a malformed required row still fails downstream via a generic "missing key" message, but an unlisted row vanishes with zero signal. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:855-858] — resolved (checked 2026-10-04): `ParseFinalConsumptionRows` fails loudly with `cells.Length.ShouldBe(5, …)`.
- [x] [Review][Patch] DocumentationFitnessTests' `defaultAction: allow` check is indentation-sensitive — requires exactly 4 spaces (`^\s{4}defaultAction:\s*allow\s*$`) unlike the deny-check's flexible `^\s*defaultAction:\s*deny\s*$`; works today only because every real file happens to use 4-space indent. [tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:178] — resolved (checked 2026-10-04): the allow check uses `^\s*defaultAction:\s*allow\s*$` scoped to the top-level `accessControl` block before `policies:` (Round 4).
- [x] [Review][Defer] Skip links are no longer the real first-Tab keyboard stop after a client-side route change [tests/e2e/specs/parties-accessibility.spec.ts:37-49] — deferred: already routed to FrontComposer shell owners as `frontcomposer-skip-link-reachability-after-route-focus` in deferred-work.md
- [x] [Review][Defer] Release workflow bypass-validation mapping is verified only by substring-ordering, not real bash execution [.github/workflows/release.yml:44-56] — deferred: already self-disclosed in deferred-work.md:478-479

**Rejected (Round 3):**
- `false`: Deleted `git ls-tree`/`rev-parse`/`describe` literal-evidence assertions in PlatformApiPrerequisitesTests.cs — replaced by a regex validating present-tense evidence text against the live HEAD gitlink plus a direct live-git check; strictly stronger, not weaker.
- `false`: `RebindContainerProvenanceLabels` only rebinds on `_IsSingleRIDBuild` — the repo has no multi-arch container RID configuration anywhere, so the uncovered path is never reached.
- `low`: `Directory.Build.targets`'s `ContainerProvenanceCreated` regex accepts calendar-invalid dates (e.g. day 31 in April), but its sole producer (`publish-containers.sh`) always generates it from `date -u`, so an invalid date can never occur in practice.
- `low`: `EpicEightClosureFitnessTests`'s raw-text scan for `class FooTests` isn't scoped to a real declaration — exploitable only via deliberate gaming of this meta-test; a proper fix needs more than a direct correction.
- `low, unverified`: a Playwright `.evaluate()` on `[data-testid='fc-navigation-rail']:visible` could hit a strict-mode violation if that testid renders more than once in the FrontComposer shell at test viewport — unconfirmed without reading the shell's markup; would only be a loud, low-impact flakiness issue if true.
- out of scope: `.bmad-loop/bmad_loop_hook.py` (orphaned `.tmp` on rename failure; `os.lstat` `PermissionError` treated as "not a link") and `_bmad/scripts/resolve_config.py` (`stdout.reconfigure()` unguarded against `io.UnsupportedOperation`) — BMAD framework tooling swept in by unrelated "update BMAD 6.12.0" commits, not part of Story 8.10's Code Map.

#### Round 4 — code review of the diff vs. baseline `e95d2f82` (2026-09-06)

- [x] [Review][Decision] Root submodule gitlinks for EventStore/Tenants/Memories advanced in this diff (commit `8a8032c2`) with no recorded authorization, breaking the identity-fitness gate and contradicting this diff's own DW-108 decision — resolved: owner chose "authorize new identities"; `PayloadProtectionEventStoreSha`/`PayloadProtectionEventStoreDescribe`, the 8.3 matrix, `docs/architecture.md`, `ARCHITECTURE-SPINE.md` §7 I4, `sprint-status.yaml`'s comment, and `.gitlink-signoff.tsv` were all reconciled to `acf5c4e4`/`d914b13b`/`fa8f5316`; `FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` re-verified green — `references/Hexalith.EventStore` is now `acf5c4e403699d4f9290fd6636e4d6b1872a3bd6` (`v3.102.0-31-gacf5c4e4`), `references/Hexalith.Tenants` is `d914b13bd9b2354dbb6b556c9887664f68672069` (`v5.7.0-7-gd914b13b`), and `references/Hexalith.Memories` is `fa8f53165183695888dd0df001d4b1e58071f39e` (`v2.26.0`) — past what every other identity record in this same diff still cites (`.gitlink-signoff.tsv` has no `validated-advance` row for any of the three; `story-8-3-platform-api-prerequisite-matrix.md`, `docs/architecture.md`, `ARCHITECTURE-SPINE.md`'s §7 I4 row, and `sprint-status.yaml`'s own Round-3 comment all still cite EventStore `7a81a410`/Tenants `0ca32a5c`/Memories `e9653980`). Verified live: running `PlatformApiPrerequisitesTests.FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` FAILS — `AssertGitlinkAndCheckout` expects `7a81a41000df11632ea84d331d764c6a240a8a46` and finds `acf5c4e403699d4f9290fd6636e4d6b1872a3bd6`. This also directly contradicts DW-108's own recorded decision ("Reset the three diagnostic gitlinks to exact catalog-selected release tags") — the diff moved them further away from the catalog tags instead of resetting to them. Violates the frozen spec's "Ask First: Dependency, submodule... changes" boundary and spine invariants I4/I16. Your call: (a) authorize the three new identities and reconcile every downstream record plus the hardcoded test constants, (b) revert the three gitlinks to the previously-recorded identities, or (c) execute DW-108 literally and reset all three to the exact clean catalog-selected release tags (EventStore `3.102.0`, Tenants `5.6.0`, Memories `2.25.0`). [references/Hexalith.EventStore, references/Hexalith.Tenants, references/Hexalith.Memories, tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:30]
- [x] [Review][Patch] `deferred-work.md`'s reformat from the old `## Story 8.10 accepted Epic 8 closure deferrals` heading + `- deferral_id:` bullets to numbered `### DW-N` entries deleted the exact heading text `EpicEightClosureFitnessTests.ParseDeferrals` anchors on, and embedded the five closure-deferral entries' `deferral_id`/`owner`/`exit_proof`/`rollback`/`evidence` fields as prose inside `reason:` instead of giving each its own field line the way every other `DW-N` entry does. Verified live: `EpicEightClosureFitnessTests.AcceptedDeferralsAreCompleteAndKeepIncompleteStoriesHonest` FAILS — `headingIndex` is `-1`, "Missing ## Story 8.10 accepted Epic 8 closure deferrals". Fix: hoist the five closure-deferral entries' embedded fields onto their own lines like every other entry, and change `ParseDeferrals` to select those five by their `authored_by_spec:`/`activated_by_spec:` marker (confirmed unique to exactly the 5 closure deferrals: 8.6/8.7/8.8/8.9/external-runtime-deployment) instead of the removed heading. [_bmad-output/implementation-artifacts/deferred-work.md, tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:362-366]
- [x] [Review][Patch] `PlatformApiPrerequisitesTests.RunProcess` can take up to 2× `ProcessTimeoutMilliseconds` (10 minutes, not the intended 5) to detect and kill a hung child, because `Task.WaitAll([outputTask, errorTask], timeout)` and the subsequent `process.WaitForExit(timeout)` each get an independent full timeout budget instead of sharing one deadline. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1566-1567]
- [x] [Review][Patch] `RunProcess` never kills the child if `outputTask`/`errorTask` faults (a stream I/O error) before the timeout elapses instead of hanging — `Task.WaitAll` propagates the fault as an `AggregateException`, which skips past the `process.Kill(entireProcessTree: true)` call below it and can leak the child process tree. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1563-1573]
- [x] [Review][Patch] `DocumentationFitnessTests`'s ACL allow-by-default regex (`^\s*defaultAction:\s*allow\s*$`) matches at any indentation depth, so a future `accesscontrol*.yaml` with `defaultAction: allow` nested under a per-app policy (not the file's actual top-level default) would be misclassified as allow-by-default; no such file exists today. [tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:175-179]
- [x] [Review][Patch] 14 `deferred-work.md` entries have their `decision:` line duplicated back-to-back verbatim (DW-5, 9, 12, 13, 17, 18, 26, 28, 32, 35, 47, 48, 50, 91, 108) — a copy-paste artifact from the pre-answer commits. [_bmad-output/implementation-artifacts/deferred-work.md]
- [x] [Review][Patch] `test-summary.md`'s PolymorphicSerializations closure note says `.gitlink-signoff.tsv` "needs a corresponding `validated-advance` row before this identity may ship" — but this same diff already added that row. [_bmad-output/implementation-artifacts/tests/test-summary.md:763-764]
- [x] [Review][Patch] Two test files were renamed (`AssemblyInfo.cs`→`NonParallelCollection.cs` in `IntegrationTests` and `Server.Tests`) with a line-ending flip (LF→CRLF) bundled into the rename, making every line show as removed+re-added with byte-identical content and leaving these two files inconsistent with the rest of the repo. [tests/Hexalith.Parties.IntegrationTests/NonParallelCollection.cs, tests/Hexalith.Parties.Server.Tests/NonParallelCollection.cs]
- [x] [Review][Patch] `Directory.Build.targets`'s `ValidateContainerProvenanceInputs` repeats the identical absolute-HTTPS-URL regex verbatim for `ContainerProvenanceRepositoryUrl`, `ContainerProvenanceReleaseUrl`, and `ContainerProvenanceDocumentationUrl`, so the three copies can drift independently on a future edit. [Directory.Build.targets:82-88]
- [x] [Review][Patch] `DW-100`–`DW-104` headers are full, period-terminated sentences copied verbatim from the old ledger's `summary:` field, breaking the short-title convention every other `### DW-N` heading uses. [_bmad-output/implementation-artifacts/deferred-work.md:804-836]
- [x] [Review][Defer] The RC gitlink sign-off gate (`rc-gate.yml`) enforces only on a release-candidate-labeled PR or a push to `rc/**`/`release/**`/a `v*` tag — never an ordinary push to `main`, this repo's normal workflow — so an unauthorized root-submodule bump can land on `main` undetected unless a hardcoded SHA constant happens to also pin it (EventStore has one; Tenants and Memories do not). [.github/workflows/rc-gate.yml:7-22, tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs] — deferred: pre-existing gate design, not introduced by this diff; recorded as DW-109.
- [x] [Review][Defer] `validate-publication-preflight.sh` accepts `HEXALITH_RELEASE_SOURCE_CI_WORKFLOW=commitlint.yml` (the weaker proof path) with no independent check that an operator actually authorized the bypass; today it's reachable only through `release.yml`'s gated `bypass-validation` input and the script isn't wired into any workflow file yet, so it is not currently exploitable — but a future direct caller could accept the weaker proof unauthorized. Related to the already-open DW-106 (bypass-mapping has no executed-bash test). [scripts/validate-publication-preflight.sh:36-40] — deferred: unverified severity (medium/high if a future caller bypasses the gate); settle by checking every caller when this script is wired into CI; recorded as DW-110.

**Rejected (Round 4):**
- `false`: `Directory.Build.targets`'s `ContainerProvenanceCreated` regex accepts calendar-invalid dates — already decided in Round 3 (sole producer always generates it via `date -u`); re-confirmed against `references/Hexalith.Builds/Github/publish-containers/publish-containers.sh:30`.
- `false`: `RebindContainerProvenanceLabels`'s `_IsSingleRIDBuild` condition allegedly leaves multi-RID publishes unrebound — verified against the installed SDK's `Microsoft.NET.Build.Containers.targets`: a multi-RID publish's per-RID inner MSBuild invocation explicitly sets `_IsSingleRIDBuild=true` and is what actually runs `_ParseItemsForPublishingSingleContainer`, so the existing condition already covers every path that needs rebinding.
- `false`: README.md's ACL paragraph allegedly overclaims blanket deny-by-default — `DW-89` (status: done) already broadened `DocumentationFitnessTests` to enumerate and verify every `accesscontrol*.yaml` file's posture (commit `f8fd7404`), so the README's one-exception claim is now test-backed.
- `false`: DW-105–108's origin note quotes a legacy heading dated "2026-08-18" while their `source_spec` was created 2026-09-05 — all four are `status: open`, not `accepted`; the quoted text is preserved migration provenance, not an asserted acceptance date.
- rejected (fix would edit the spec under review): spec-8-10's own Round 3 checklist still shows 8 `[Review][Patch]` items unchecked that this diff's code already resolves (FrontComposer identity doc, PolySer reconciliation, Commons signoff row, sprint-status comment, `RunProcess` race, `indexLink` disjunct, README ACL exception, `DocumentationFitnessTests` indentation) — the only fix is flipping checkboxes in the spec itself.
- `maybe-false` (if true, only `low`): `.bmad-loop/decisions.json` stores empty `resolution`/`bundle_name` for entries whose full narrative lives in `deferred-work.md` — would need the bmad-loop tool's schema to know whether these fields are meant to stay empty (a lightweight index) or duplicate the text; even if a real gap, it's at most a low-severity drift-risk between two ledgers.

#### Round 5 — code review of the Code Map chunk vs. baseline `37f4ec8` (2026-09-06)

Chunked to the Story 8.10 Code Map (45 files). Four layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor.

- [x] [Review][Decision] Closure-deferral ledger has two contradictory status fields — resolved: option 5 — `ExpectedDeferrals` is the accepted-wait set; stripped packed `status: accepted` from DW-11/96–99 `reason:` lines; first-class `status: open` remains the work-queue field; dropped `deferral.Status.ShouldBe("accepted")`. [_bmad-output/implementation-artifacts/deferred-work.md, tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:35-40]

- [x] [Review][Patch] Canonical `### Validation receipts` table still records a tree that is not HEAD — `ParseValidationReceipts` selects this heading; Release solution build is still `**Blocked**` at PolymorphicSerializations `0dca9e9d` and Playwright accessibility is still Pass at FrontComposer `7a337a21`. Later narrative closed those blockers at `8aeed1d2` / `5cbc5583`. `DescribeReceiptGaps` would still refuse `done`. I16 re-validation of stamped I9/I10/I13 receipts at the live identities is also missing. [_bmad-output/implementation-artifacts/tests/test-summary.md:606-623]

- [x] [Review][Patch] G5 "Payload protection engine package" cell still names EventStore `454b4d10` / Builds `17b1c7aa` as "the retained identities … recorded in the Story 8.10 reconciliation table above." That table now records EventStore `acf5c4e4` / package `3.102.0` and Builds `8db7459d`. Present-tense `git ls-tree HEAD` regex does not catch this prose. [_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:104]

- [x] [Review][Patch] Tenants is absent from the 8.10 reconciliation table and PolymorphicSerializations is absent from `AssertGitlinkAndCheckout` — this diff advanced Tenants to `d914b13b` (consumed by AppHost topology; architecture.md already cites it) and PolySer to `8aeed1d2`. Memories already has a "not consumed" row; Tenants does not. `FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` pins EventStore, Commons, Builds, and FrontComposer only. Tenants/Memories SHA constants remain DW-109. [_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:67-75, tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:618-621]

- [x] [Review][Patch] Story 8.9/8.10 status narrative is stale after the patches it describes — DW-99 packed fields still say "Story 8.9 stays backlog" and stamp the slice at FrontComposer `7a337a21`; the 8.9 sprint-status comment still says app-owned focus-visible/forced-colors rules are "unscoped and dead at runtime" after `MainLayout` gained the `display: contents` wrapper; the 8.10 Round 4 comment still says the deferred-work heading-anchor fitness failure "remains open" after `ParseDeferrals` was rewritten and that finding checked off. YAML values are already `blocked` / `review`. [_bmad-output/implementation-artifacts/deferred-work.md:793, _bmad-output/implementation-artifacts/sprint-status.yaml:196-250]

- [x] [Review][Patch] Code Map docs still advertise SDK `10.0.302` after `global.json` pinned `10.0.400` — `docs/architecture.md`, `docs/index.md`, `docs/project-overview.md`, `docs/source-tree-analysis.md`, `docs/getting-started.md`. `DocumentationFitnessTests` does not pin the SDK version. [global.json:3, docs/getting-started.md:15]

- [x] [Review][Patch] `docs/getting-started.md` still requires a sandbox cluster, `kubectl`, Helm, and aspirate `9.1.0` "for the optional Kubernetes walkthrough (Step 1b)" after Step 1b retired that walkthrough. [docs/getting-started.md:20-30,86-94]

- [x] [Review][Patch] ACL posture is still over-general outside `event-publishing.md` — `docs/source-tree-analysis.md` labels every `accesscontrol.*.yaml` deny-by-default; `docs/getting-started.md` lists deny-default SDK routes without the `accesscontrol.eventstore-admin.yaml` `defaultAction: allow` exception. `DocumentationFitnessTests` requires that exception only in `event-publishing.md`, and a second top-level allow file would share that substring. [docs/source-tree-analysis.md:21, docs/getting-started.md:69, tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:195-202]

- [x] [Review][Patch] Test-host `UseStaticWebAssets` is pinned only as source text — `TestEnvironmentStaticWebAssetOptIn_StaysCoupledToThePlaywrightWebServer` greps `Program.cs` and `playwright.config.ts`. Commenting out the call while leaving the identifier strings keeps every .NET project green; Playwright is not in CI. Add a `WebApplicationFactory` boot with `UseEnvironment("Test")` that asserts `blazor.web.js` (or an RCL stylesheet) is served. [src/Hexalith.Parties.UI/Program.cs:28-31, tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs:251-275]

- [x] [Review][Patch] Shell forced-colors/`--colorStrokeFocus2` assertions read FrontComposer checkout `fc-shell.css` and `SkipUnless` when that file is absent — packaged `4.3.0` is the CI/bUnit/container identity; a missing submodule skips rather than proving the shipped asset. Keep the App.razor link asserts always-on; pin the tokens against the packaged RCL stylesheet. [tests/Hexalith.Parties.UI.Tests/AccessibilityStyleGuardTests.cs:61-73]

- [x] [Review][Patch] `FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` enumerates `*.csproj` under `src`/`samples`/`tests` with no `obj`/`bin` exclusion — `DocumentationFitnessTests.IsNotBuildOutput` already filters those copies; a stale generated csproj can drive MSBuild graph evaluation. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:672-677]

- [x] [Review][Patch] Playwright `reuseExistingServer: !IS_CI` can attach to a non-Test host on port 5072 — the lane now depends on `ASPNETCORE_ENVIRONMENT=Test` for `UseStaticWebAssets`. A leftover Development server yields SSR-only pages locally. Refuse reuse unless that environment is Test. [tests/e2e/playwright.config.ts:43, src/Hexalith.Parties.UI/Program.cs:28-31]

- [x] [Review][Defer] App-owned content `:focus-visible` / reduced-motion CSS is only grepped, and the Playwright forced-colors check still focuses `.fc-skip-link` — spec §4.6 already records I13 as not discharged; `test:a11y` is not in CI (DW-76). A stronger computed-style assertion would not protect merge until that lane is wired. [src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor.css:8-31, tests/e2e/specs/parties-accessibility.spec.ts:88-90] — deferred: pre-existing I13 gap recorded in frozen §4.6 and DW-76; recorded as DW-111.
- [x] [Review][Defer] `EpicEightClosureFitnessTests.RunGit` / `TryRunGit` still drain stdout then `WaitForExit` with no timeout — Round 2 already deferred this pattern. [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:537-585] — deferred: pre-existing; Round 2 Review Defer.
- [x] [Review][Defer] Skip-to-navigation stays rendered when `#fc-nav` is unmounted at Tablet/Phone (`IsSubCompactDesktopViewport`) — FrontComposer shell, consumed not edited here. [references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerShell.razor:37-41,118-131] — deferred: owner-repo defect; recorded as DW-112.
- [x] [Review][Defer] `EvaluateProjectGraph` parses MSBuild stdout from the first `{` to end of string — trailing log after the JSON object would throw `JsonException`. Unverified whether current MSBuild `-getProperty/-getItem` emits trailing content. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:893-898] — deferred: maybe-false (would be medium); settle by capturing a live `-getItem` payload; recorded as DW-113.
- [x] [Review][Defer] `ValidateContainerProvenanceInputs` / `RebindContainerProvenanceLabels` are never invoked by Parties tests — already DW-107. [Directory.Build.targets:68-103] — deferred: already recorded as DW-107.
- [x] [Review][Defer] Tenants/Memories gitlinks have no `AssertGitlinkAndCheckout` SHA constant — already DW-109. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:618-621] — deferred: already recorded as DW-109.

**Rejected (Round 5):**
- `false`: Skip-link Enter leaves `#fc-main-content` / `#fc-nav` without a Parties `:focus-visible` ring — those landmarks are rendered by the shell, outside `.parties-main-content`; MainLayout CSS is not the owner.
- `false`: Keyboard-flow test never sees primary FluentButton focus through shadow `activeElement` — `tabUntilTestId` already walks `shadowRoot.activeElement` and uses `element.contains(active)`.
- `false`: `ContainerProvenanceCreated` accepts Feb 31 — rejected R3/R4; sole producer is `date -u` in `publish-containers.sh`.
- `false`: Add Parties 8.7/8.9 spec paths to `downstreamConsumers` — only `spec-8-8` declares `Block If — available-row identities`; the `8-7`/`8-9` strings in `PlatformApiPrerequisitesTests` are EventStore G5 story keys.
- `false`: `ParseDeferrals` treats any later `deferral_id:` mention as a spurious closure deferral — extras fail `ShouldBe(ExpectedDeferrals)` (fail-closed).
- rejected (fix would edit the spec under review): Verification command still uses MTP-invalid `-class`; Execution task still lists EventStore `3.95.0` / `454b4d10`; Round 3 still has eight unchecked Review items that Round 4 already refused to tick.
- `low`: reduced-motion rule omits `transition-delay` / `[role=button]` / custom elements — duration is already 0.01ms; expanding the selector is extra CSS complexity for an edge users rarely meet.
- `low`: `Directory.Build.targets` comment still cites SDK 10.0.302/303 for the `Split(':')` label bug after pinning 10.0.400 — comment-only; the rebind target is unchanged.

#### Round 6 — code review of the Code Map chunk `882c0245..HEAD` (2026-10-04)

Chunked to the Story 8.10 Code Map paths changed since the Round 5 record commit `882c0245` (27 files, CR-at-EOL churn ignored; the PartiesOverview and actor-provisioning feature files were excluded). Four layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor. Live evidence at HEAD `6298e84b`: the three closure fitness classes report 36 total / 5 failed (`FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph`, `MaintainedTechnologyTablesMatchCentralPackageCatalog`, `Matrix_NamedAvailableRowsRecordImmutableConsumptionIdentities`, `Matrix_ValidationEvidenceCommandsAreReproducible`, `EpicEightAddsNoPrdFunctionalRequirement`), run from the last built assembly; a fresh package-mode `dotnet build tests/Hexalith.Parties.Tests` fails with 10 × CS0246 in `Hexalith.Parties.Contracts` (EventStore identity-history types present only in source `b0464255`, from commit `37d87f5a`, outside this chunk), so every "Release build: Pass" receipt is false at HEAD.

- [x] [Review][Decision] All eight root gitlinks and the Builds catalog moved past every recorded Story 8.10 identity without sign-off — HEAD records AI.Tools `3f194e17`, Builds `688eec9a`, Commons `116d2681`, EventStore `b0464255` (`v3.111.0-2`), FrontComposer `bf40099f` (`v4.5.0-113`), Memories `3d72927f`, PolymorphicSerializations `98de6e01` (`v1.19.4`), Tenants `b63bdbba` (moved in `06714c16`/`6298e84b`; Tenants was already `c150d5b1` at `14d249fd`). None appears in `.gitlink-signoff.tsv`, the reconciliation table, or the `*Sha` constants (`5f93d2ec`/`aee01323`/`19d7d4d6`/`dfc0ac55`/`b0ad2fb6`/`d99bc963`/`8aeed1d2`/`ff43dc94`). The catalog now selects EventStore `3.110.0`, Commons `2.30.1`, FrontComposer `4.5.0`, Dapr `1.18.10` against asserted `3.104.0`/`2.30.0`/`4.4.0`/`1.18.7`. `FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` fails on its first assertion, `scripts/gitlink-rc-gate.sh` reports eight unvalidated bumps, and the spine I16 stop is in effect. The new AI.Tools pin and the clean-checkout requirement for optional Memories/AI.Tools have no sign-off or matrix row (AC2). Spine I20 forbids claiming any approval, I16 re-validation included, until the 8.3 approval table exists, and it does not. Frozen Ask First covers submodule changes. Options: (a) authorize the HEAD identities — create the I20 approval table, add sign-off rows, re-stamp constants, reconciliation table, spine, and test-summary; (b) revert the eight gitlinks to the 2026-09-13 signed set; (c) keep 8.10 open, mark the table and receipts `unvalidated`, and route to owners. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:22-34, .gitlink-signoff.tsv, _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:51-76] — resolved: option (a), authorize the HEAD identities (see the D1 patch below).
- [x] [Review][Decision] `AssertGitlinkAndCheckout` redefines pin proof — it now reads the parent index (`git ls-files --stage`) instead of the committed tree, re-opening the staged-but-uncommitted gap Round 3 recorded as a defect (open item at line 169) and contradicting the 8.3 rule that evidence "must include `git ls-tree <Parties-commit>`" and the frozen Never; the G5 `expectedGitlink` check moved the same way. The same edit adds a detached-HEAD requirement that fails in this workspace (all eight submodules are on `main` even when the SHA matches) plus a nested-submodule check. The new comment says this is deliberate, so a correct RC pre-commit check can pass. Options: (a) restore `ls-tree HEAD` and drop the branch-name check (SHA equality plus a clean tree already prove identity); (b) require both index and HEAD to match (pre-commit RC runs fail until committed) and accept attached HEAD when the SHA matches; (c) keep index semantics and amend the 8.3 rule and spine with your approval. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1386,1572-1593] — resolved: option (a), restore `ls-tree HEAD` and drop the branch-name check (see the D2 patch below).
- [x] [Review][Decision] Zero-PRD gate is red — commit `60b9836e` added a 3-line McpCli course-correction banner to `epics.md`; `EpicEightAddsNoPrdFunctionalRequirement` requires an empty `git diff 37f4ec82 -- epics.md`, so I15 fails although the FR inventory is unchanged. The companion spine §2/McpCli row moves MCP ownership with no I20 approval row, while the 8.3 G11 row still routes MCP plumbing to FrontComposer.Mcp. Options: (a) move the banner out of `epics.md` (keep it in the spine/SCP); (b) narrow the gate to compare the epic/story/FR inventory, as the PRD branch already does, and record an I20 approval for the McpCli divergence; (c) treat it as an Epic 8 scope change through correct-course. [_bmad-output/planning-artifacts/epics.md:47, tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:161] — resolved: option (a), move the banner out of `epics.md` (see the D3 patch below).
- [x] [Review][Decision] `Hexalith.Parties.UI` consumes FrontComposer source-only types — `FrontComposerRouteOptions` (`Program.cs:49`) and `FcModuleLandingPage` (`Program.cs:216`, `Routes.razor:7`) exist only in FrontComposer source (added 2026-09-27) and are absent from the packaged Shell `4.4.0` and `4.5.0` DLLs. The package-mode graph — the CI, bUnit, and released-container identity — therefore cannot compile the UI host. It also adopts a G4 surface beyond work package F that no deferral or matrix row records, and nothing tests the reserved-segment set. The origin commit `37f1d04b` is not 8.10 work; `37d87f5a` repeats the pattern in Contracts. Options: (a) wait for a FrontComposer release carrying these types, then bump the catalog and identity rows; (b) gate the route wiring behind source mode; (c) revert the routing change until the package ships. [src/Hexalith.Parties.UI/Program.cs:49,216] — resolved: option (a), ship a FrontComposer release; the release and catalog bump are deferred as DW-124, and recording the surface is the D4 patch below.
- [x] [Review][Patch] D1 — Authorize the HEAD root identities: create the I20 approval table in the 8.3 ledger, add `.gitlink-signoff.tsv` `validated-advance` rows for AI.Tools `3f194e17`, Builds `688eec9a`, Commons `116d2681`, EventStore `b0464255`, FrontComposer `bf40099f`, Memories `3d72927f`, PolymorphicSerializations `98de6e01`, and Tenants `b63bdbba`, and re-stamp the `*Sha`/describe constants, the catalog assertions (EventStore `3.110.0`, Commons `2.30.1`, FrontComposer `4.5.0`), the reconciliation table, the spine, and the test-summary; mark every receipt not re-run at these identities `unvalidated`. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:22-34, .gitlink-signoff.tsv, _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:51-76] — complete 2026-10-04: the original Round 6 set is recorded as historical approval. Administrator / jpiquot explicitly answered "yes" to approving the three later existing current pins at Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461`: EventStore `cbbe41501ba722731bf36b2c343efdef4ac714fb`, FrontComposer `374bb83392d8ab4e8a8397cfd312d09948fb0b9d`, and Tenants `cc348c9d7839ec7aad01649fc1c0e6f4fe672da4`. The I20 rows, new signoff rows, constants/describes, reconciliation, docs/spine/sprint, and test-summary now select those exact IDs. Source-mode Debug fitness passes 39/39 with no skips; the RC diff gate passes all eight current root gitlinks. Every affected parity receipt remains unvalidated pending its own complete evidence and separate I16 approval; this selection decision does not authorize release, publishing, or deletion.
- [x] [Review][Patch] D2 — Restore committed-tree pin proof: `AssertGitlinkAndCheckout` and the G5 `expectedGitlink` check return to `git ls-tree HEAD` (`160000 commit <sha>`), the branch-name assertion is removed, and the nested-submodule check stays. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1386,1572-1593]
- [x] [Review][Patch] D3 — Move the McpCli course-correction banner out of `epics.md`; the spine and the SCP already carry it. [_bmad-output/planning-artifacts/epics.md:47]
- [x] [Review][Patch] D4 — Record the module-landing route surface (`FrontComposerRouteOptions`, `FcModuleLandingPage`) in the 8.3 G4 row and the DW-99 `delivered_slices` as a source-only adoption awaiting DW-124's release, without promoting G4. [_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:136, _bmad-output/implementation-artifacts/deferred-work.md:805]
- [x] [Review][Patch] The 2026-10-03 Story 8.7 G5 revalidation section sits inside the 8.10 reconciliation-table parse range — its 3-cell tables trip `ParseFinalConsumptionRows`' fail-loud `cells.Length.ShouldBe(5)`, so `Matrix_NamedAvailableRowsRecordImmutableConsumptionIdentities` fails, and the section's own "Static gate inspection passed" claim is false. Move it below `<!-- platform-api-prerequisite-matrix:end -->`. [_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:98-123]
- [x] [Review][Patch] G5 row validation and `RequiredAbsentPayloadProtectionPaths` still require surfaces to be absent that the same diff records as present — `Hexalith.EventStore.PayloadProtection.csproj` exists, `IPersonalDataPolicy`/`IErasureStateProvider` exist, and owner 8.2 is `done`, yet the row keeps `test ! -f …PayloadProtection.csproj`, `rg … (expected no matches)` and `8-2/8-3 … backlog`, and the test asserts `File.Exists(...).ShouldBeFalse`. Re-state the absent-set as the 8.11 closure packet, the AzureKeyVault project, and catalog/release enrollment, and keep G5 `needs-additive-api`. [_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:132, tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:41-46]
- [x] [Review][Patch] FrontComposer slice identity is recorded five ways — `EpicEightClosureFitnessTests.FrontComposerSha` pins the receipt's `f0c3b6fd` while `PlatformApiPrerequisitesTests.FrontComposerSha` pins `b0ad2fb6`, so the a11y receipt never proves the selected gitlink. The Playwright receipt falsely says "matching the current reconciliation table"; the G4 matrix row says `f0c3b6fd`, and DW-99 plus the sprint-status 8.9 comment overwrite the 2026-08-18 adoption identity `7a337a21` with `5cbc5583`. Compare the receipt to the single gitlink constant, mark the receipt `unvalidated` until re-run (spine §7a), and restore the original identity with its amendment chain. [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:19, _bmad-output/implementation-artifacts/tests/test-summary.md:636]
- [x] [Review][Patch] test-summary closure evidence describes neither the reconciliation table nor HEAD — "Retained immutable identities" lists the first 2026-09-07 set (`3c6a5e33`/`3.102.0`, `6daad3d5`, `f0c3b6fd`/`4.3.0`, `7e9c2c38`/`2.26.1`, `f75cdacc`) while the table records the 2026-09-13 set. The "All .NET test projects 2,437", package/API, and npm receipts carry no date or identity, and the Release-build receipt predates SDK `10.0.401` and the 09-13 refresh. Align the list with the table and stamp or mark unvalidated every receipt not re-run at the selected identities. [_bmad-output/implementation-artifacts/tests/test-summary.md:536-560,629-640]
- [x] [Review][Patch] G5 "Payload protection engine package" cell still claims its retained identities (`d45206f7`/`3.103.0`, Builds `7b0b1837`) are "recorded in the Story 8.10 reconciliation table above" — the Round 5 fix was a supersession paragraph plus `matrix.ShouldContain("Supersession clarification (2026-09-13)")`, a document-contains-its-own-wording assertion. Correct the cell's present-tense claim and drop that assertion. [_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:132,142, tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:707]
- [x] [Review][Patch] Architecture spine contradicts the tests and records it cites — frontmatter `updated: 2026-09-08` predates the 2026-09-27 McpCli paragraph; `open-condition` still names Builds `a32cb422`/`35c3d1e5`; §7 I4 says the test pins `3.103.0`/`c6efdbba`/`6da79aed`/`35c3d1e5` (it pins `3.104.0`/`dfc0ac55`/`19d7d4d6`/`aee01323`); §7a I16 cites FrontComposer `a0acb78f`; the §9 row lists live pins as `10.0.400`/`1.18.5`/`13.5.3`; the §9 G5-cell row was never closed. Reconcile after the identity decision above. [_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md:5,10,433,464,534]
- [x] [Review][Patch] Documentation fitness tests hard-code the versions they guard, and the docs have drifted — `CodeMapDocumentsThePinnedSdkVersion` hard-codes `10.0.401` (and omits README, which still says `10.0.302`), so the next `global.json` bump with stale docs passes. `MaintainedTechnologyTablesMatchCentralPackageCatalog` compares both sides to literals and skips the Aspire rows: docs still say "Aspire 13.4" (architecture.md:18, index.md:10, project-overview.md:19) and "aligned at `13.5.3`" against AppHost SDK and `Aspire.Hosting` `13.6.0`, and ci.md says the catalog selects `3.104.0`. `AccessibilityStyleGuardTests` hard-codes `4.4.0` while the catalog selects `4.5.0`, so a clean-cache CI run fails and a warm cache checks an asset that does not ship. Derive expectations from `global.json` and the Builds catalog, then update the docs. [tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:124,145, tests/Hexalith.Parties.UI.Tests/AccessibilityStyleGuardTests.cs:9]
- [x] [Review][Patch] Source-mode `PackageReference Hexalith.FrontComposer.Shell` (`IncludeAssets=none`) next to the same-id `ProjectReference` is a no-op — `project.assets.json` resolves only `Hexalith.FrontComposer.Shell/1.0.0` (type `project`), so the package is never restored and the csproj comment ("still restores packaged 4.4.0") is false. Use a `PackageDownload` at the catalog version (verify CPM accepts it), or skip loudly in source mode. [tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj:16-20]
- [x] [Review][Patch] `deferred-work.md` duplicates `decision:` lines again — DW-56, DW-82, DW-112, DW-119, and DW-120 each repeat their decision line verbatim, the same defect Round 4 patched. [_bmad-output/implementation-artifacts/deferred-work.md]
- [x] [Review][Patch] Two trailing `- source_spec:/summary:/evidence:` bullets (the @types/node floor and the runtime-toolchain packet) have no DW id, origin, or status, so DW-format sweeps cannot see them; DW-119–DW-123 also lack the blank line after their headings. Convert the bullets to DW-125/DW-126 (DW-124 is taken by this round's deferral). [_bmad-output/implementation-artifacts/deferred-work.md:1013-1019]
- [x] [Review][Patch] Stale open ledger entries — DW-109 still says "Tenants and Memories have no such pin" after this diff added `TenantsSha`/`MemoriesSha`, and DW-115 says `global.json` selects `10.0.400` (it selects `10.0.401`). [_bmad-output/implementation-artifacts/deferred-work.md:887,938]
- [x] [Review][Patch] sprint-status lags its own content — `last_updated: 2026-09-07` (header comment and YAML) although 2026-10-03 entries were added, and the Story 8.10 narrative ends at the Round 4 identities (`acf5c4e4`/`fa8f5316`) without the 2026-09-07/09-13 refreshes. [_bmad-output/implementation-artifacts/sprint-status.yaml:2,48,255-263]
- [x] [Review][Patch] The new `WebApplicationFactory` test never disposes its `HttpResponseMessage`. [tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs:276]
- [x] [Review][Patch] `IsNotBuildOutput` matches `bin`/`obj` anywhere in the absolute path — a clone under e.g. `~/bin/` filters every csproj and fails with a misleading empty-consumer assertion; test `Path.GetRelativePath(root, path)` instead. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1566]
- [x] [Review][Patch] Story 8.7 receipts in test-summary use present-tense `git ls-tree HEAD` for identities that are not HEAD (`d45206f7`, `2c58ffda`) — the dated-placeholder rule enforced on the matrix; name the recorded Parties commit instead. [_bmad-output/implementation-artifacts/tests/test-summary.md:807,839-840]
- [x] [Review][Patch] Spine §7b says to create the I20 approval table "empty, with the eight decision rows", but I20 enumerates more decision kinds, and "empty" contradicts "with rows". [_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md:497-499]

- [x] [Review][Defer] FrontComposer release carrying `FrontComposerRouteOptions`/`FcModuleLandingPage` plus the catalog bump [src/Hexalith.Parties.UI/Program.cs:49,216] — deferred: owner-repo release (FrontComposer), chosen under D4; recorded as DW-124.

**Rejected (Round 6):**
- `low`: the 2026-09-07 Story 8.7 receipt's `rg -F '…\|…'` makes the alternation literal, so "no matches" was vacuous — superseded by the 2026-10-03 receipt, which records both contracts present; fixing it would mean re-running historical evidence.
- `false`: the supersession prose inside the `matrix:start/end` markers breaks the matrix parser — `ReadRows` parses only `|`-prefixed lines of the marked section; prose is skipped.
- `false`: DW-114–DW-118 are mislabeled "migrated from legacy ledger" — they were written in legacy format in `904b0ca9` and migrated by `70691364`.
- `false`: DW-108 is `done` without a new decision line — its resolution names the superseding user-approved spec.
- tracked (DW-118): Playwright `reuseExistingServer` checks the runner's `ASPNETCORE_ENVIRONMENT`, not the reused server's.
- tracked (DW-117): `ResolveNuGetPackagesRoot` accepts an empty `NUGET_PACKAGES` value and ignores `RestorePackagesPath`/`globalPackagesFolder`.
- tracked (DW-116): the static-asset test requests only `_framework/blazor.web.js`, not a FrontComposer RCL asset.
- decided (Round 5 option 5, DW-114): removing `deferral.Status.ShouldBe("accepted")` lets a resolved deferral satisfy `ExpectedDeferrals`.
- `false`: the new sibling `..\Hexalith.FrontComposer` probe violates root-only submodules — it mirrors the existing Commons/EventStore sibling probes, and the `references/` probe still wins when present.
- rejected (fix would edit the spec under review): Round 5 items flipped to `[x]` without resolution notes, items checked while their substance moved to DW-116–DW-118, and the frozen §4 clause 1 FrontComposer identity.
- `low`: DW-9/DW-10/DW-12 close G8 sub-deliverables citing unpinned submodule paths — the G8 matrix row stays fail-closed at `needs-additive-api`; adding SHAs means re-deriving owner evidence.
- `low`: the Memories source graph is never evaluated with `useSource: true` — Memories is recorded as not consumed; the fix adds assertion machinery for a path developers rarely exercise.
- `low`: `ReadValidationReceiptEvidence` would truncate an evidence cell containing an escaped pipe — no current receipt does, and the fix needs an escape-aware cell splitter.
- `maybe-false` (if true, `low`): `authentication` is not a reserved FrontComposer segment — it only matters if a module registers that alias.
- pre-existing, ledger-tracked: `@types/node` 26 against the `>=24` engine floor, and the runtime-toolchain packet still expecting an older CommunityToolkit Dapr version (both in the trailing bullets the DW-125/126 patch makes visible).
- `false`: npm typecheck was not re-run after the TypeScript 6→7 bump — the Verification Gap layer ran `npm --prefix tests/e2e run typecheck` on TypeScript 7.0.2 and it passed.
- `false`: the reserved segments miss the `/parties` routes — `PartiesOverview` owns literal `/parties` and `/parties/overview` routes, which take precedence over `/{Module}`.

### Working-tree identity reconciliation — 2026-10-04

**Decision (Ask First, answered).** Parties HEAD `7c720c7f9879ecb469993b1d2dff2bdb72b96e1b` committed EventStore `e968467c`, FrontComposer `37c8c6d2`, Memories `47027d35`, Tenants `c05fe317` past the approved pins; the working tree advanced them (and Builds) further by clean fast-forwards. On 2026-10-04 the Administrator / jpiquot chose **"approve working tree"**: approve exactly these five identities, and nothing else:

| Root dependency | Prior approved | Approved working-tree identity | `git describe --tags --always` |
| --- | --- | --- | --- |
| Builds | `688eec9a4333245cc0ff7772115c769094471863` | `145ae921d9032f110b7371614939559e0aee202a` | `v4.29.1-16-g145ae92` |
| EventStore | `cbbe41501ba722731bf36b2c343efdef4ac714fb` | `2242ad55a1b678828df8aa093fd92399c29af5bf` | `v3.112.0-2-g2242ad55` |
| FrontComposer | `374bb83392d8ab4e8a8397cfd312d09948fb0b9d` | `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` | `v4.5.0-121-g2cc8dd3a` |
| Memories | `3d72927f4dac66af4968cc6726c4e96df292f2e4` | `5b43fe2f8a0f04dc021921a077dff1a573c2ce5e` | `v2.28.0` |
| Tenants | `cc348c9d7839ec7aad01649fc1c0e6f4fe672da4` | `04e655cf070b17eced9daefb9eaa87a09ec60e81` | `v5.7.0-141-g04e655cf` |

The approval authorizes recording, signing, and guarding these source pins only. It grants no parity (I16), owner release, publishing, commit, or rollback deletion. The gitlinks stay uncommitted (the human commits them), so the committed-tree guard stays red until then. Never weaken that guard. Never reset, stage, or commit submodules. The Builds catalog at `145ae921` itself selects EventStore `3.112.0`, consistent with the Parties pre-import pin; Commons `2.30.1`, FrontComposer `4.5.0`, Memories `2.27.1`, Tenants `5.7.0` are unchanged. Scope is reconcile-then-stop: closure stays blocked by DW-124, the I16 approval, and I13. Story 8.10 stays `in-progress`.

- [x] `.gitlink-signoff.tsv` -- append a commented 2026-10-04 approval block and five `validated-advance` rows (owner `jpiquot`), using the table's SHA and describe values verbatim. — done 2026-10-04.
- [x] `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- re-stamp `BuildsSha`, `PayloadProtectionEventStoreSha`/`PayloadProtectionEventStoreDescribe`, `FrontComposerSha`, `MemoriesSha`, `TenantsSha`, and the Memories/Tenants describe literals in `RequiredFinalConsumptionRows`. `AssertGitlinkAndCheckout` is unchanged. — done 2026-10-04.
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- reconciliation-table rows (EventStore source, Builds, FrontComposer, Memories, Tenants): cite the new identity, mark the prior one superseded, and state that the clean checkout matches while the committed gitlink is pending (HEAD `7c720c7f…` records the old pointer). Note in the Builds row that the catalog also selects `3.112.0`. Add one I20 row for this five-pin approval. Annotate the "Working source observations" section as approved. — done 2026-10-04; the G4 row's FrontComposer amendment chain was also completed.
- [x] `docs/architecture.md`, `ARCHITECTURE-SPINE.md` (§7 I4, §7a I16), `deferred-work.md` (DW-99), `sprint-status.yaml` (8.10 comment; value stays `in-progress`) -- replace current-identity citations with the new IDs; keep superseded IDs only as dated history. — done 2026-10-04; spine frontmatter, §7b I20, and §12 plus the sprint-status 8.9 chain were aligned too.
- [x] `_bmad-output/implementation-artifacts/tests/test-summary.md` -- append a dated section with the approval, exact commands, and results. In the canonical `### Validation receipts` table, mark every receipt that was not rerun at the new identities `Unvalidated`, including Playwright accessibility at `374bb833`. — done 2026-10-04.
- [x] This spec, Round 3 -- check each unchecked `[Review][Patch]` against the tree. Tick it with a one-line resolution note only if it is resolved; otherwise leave it unchecked with the reason. — done 2026-10-04: all 13 were resolved in the tree and are ticked with notes.
- [x] Verify (focused only) -- package-mode `dotnet build tests/Hexalith.Parties.Tests -c Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`, then run the assembly EXE with `-class` for `DocumentationFitnessTests`, `EpicEightClosureFitnessTests`, and `PlatformApiPrerequisitesTests`, plus `bash scripts/gitlink-rc-gate.sh`. Expected: the only failures are committed-gitlink assertions caused by the uncommitted pointers. Any other failure is a defect to fix. Full solution, all-tests, pack, and a11y lanes are out of scope for this pass (blocked by DW-124). — done 2026-10-04: the build passed with 0 warnings and 0 errors. Documentation passed 6/6, closure 26/26, and prerequisites 14/16. The only failures are the two committed EventStore gitlink assertions (HEAD `e968467c`, approved `2242ad55`). `gitlink-rc-gate.sh` passed. A scratch clone with the approved gitlinks committed passed 48/48 and the RC diff gate. Receipts are in tests/test-summary.md.

### Final identity, approval, and EventStore 3.113.0 reconciliation — 2026-10-05

**What happened after the 2026-10-04 pass.** The Administrator's `/pushall` commit `2f6157d330139e38d031c9a3d1925bca5c317ff7` committed that reconciliation. It recorded FrontComposer `2cc8dd3a` and Memories `5b43fe2f` (approved) and Builds `145ae921` (approved). EventStore was recorded at `547c938d52e4fd31b47783b9dd032528a5100549` and Tenants at `b55f96d89b839fd443ac589835880102add46d64`; neither was ever approved. `2bdb303b31739b7631036fc15ac21a012724ddb8` then moved Builds, EventStore (`0dc44e46ccb7f56c3182b855c21f337173a1307f`), and Tenants again. `47e2da3244fd7d6d14e39bc30513b23504e9bc27` ("preserve Parties UI compatibility with published shell") puts `FrontComposerRouteOptions` behind `HFC_ROUTE_OPTIONS`, which is defined only when `HexalithFrontComposerFromSource=true`, and routes to the `FrontComposerShell` assembly. Through that commit the Administrator chose Round 6 D4 option (b), gating the route wiring behind source mode.

**Decisions, Administrator / jpiquot, 2026-10-05 (Ask First, answered):**
1. Approve exactly Builds `360a2b9c4e96809365a7de785be9a68152d5ac28` (`v4.29.1-17-g360a2b9`) and Tenants `72b8e4f508176b69826549e87b7b2a286f607fd1` (`v5.7.0-143-g72b8e4f5`). The committed EventStore `547c938d` / `0dc44e46` and Tenants `b55f96d8` are never approved. The 2026-10-04 approvals of EventStore `2242ad55` and Tenants `04e655cf` were never committed and are superseded.
2. "Update eventstore to version 3.113.0" covers both the package and the source. The Parties pre-import CPM pin becomes `3.113.0` (it restores). The root checkout moves to tag `v3.113.0` = `865cd9e49273dffbb1cdae85efeaf1aac322e09e`, one fast-forward commit past `0dc44e46`. Package and source are now the same release.
3. **I16 identity re-validation: approved** for the final set below. Under I16 this approval does not turn a receipt into Pass. A receipt is `Pass` only when it was rerun at the stamped identity. Any receipt not rerun stays `Unvalidated`.
4. **I13 / DW-111: accepted deferral.** The Administrator accepts the missing runtime content-control focus, forced-colors, and reduced-motion proof as a named deferral owned by `8.9-frontcomposer-ui-consolidation` (DW-111) for Story 8.10 closure. I13 itself is **not** discharged; spine §7 keeps it `Deferred`.

**Final identity set:** AI.Tools `3f194e17`, Builds `360a2b9c`, Commons `116d2681`, EventStore `865cd9e4` (`v3.113.0`), FrontComposer `2cc8dd3a`, Memories `5b43fe2f`, PolymorphicSerializations `98de6e01`, Tenants `72b8e4f5`. Packages: EventStore `3.113.0`, Commons `2.30.1`, FrontComposer `4.5.0`, Memories `2.27.1`, Tenants `5.7.0`. Builds `360a2b9c` itself still selects EventStore `3.112.0`, so the Parties pin governs. Already applied in the working tree: the fast-forward to `47e2da32`, the EventStore checkout at `v3.113.0`, and the `Directory.Packages.props` pin. Do not stage, commit, reset, or touch nested submodules; the human-directed commit follows this pass.

- [x] `.gitlink-signoff.tsv` -- append a commented 2026-10-05 block with `validated-advance` rows for Builds `360a2b9c`, EventStore `865cd9e4` (`v3.113.0`), and Tenants `72b8e4f5` (owner `jpiquot`). — done 2026-10-05: one commented block plus Builds `360a2b9c…|v4.29.1-17-g360a2b9`, EventStore `865cd9e4…|v3.113.0`, and Tenants `72b8e4f5…|v5.7.0-143-g72b8e4f5` rows, owner `jpiquot`.
- [x] `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- set `EventStorePackageVersion` to `3.113.0` and re-stamp `BuildsSha`, `PayloadProtectionEventStoreSha`/`Describe` (`v3.113.0`), `TenantsSha`, and the Tenants describe literal. Guards are unchanged. — done 2026-10-05; guards unchanged. The approved Builds `360a2b9c` catalog routes `CommunityToolkit.Aspire.Hosting.Dapr` through `$(HexalithAspireHostingDaprVersion)`, so this class now pins that property value and the alias exactly, and `DocumentationFitnessTests` resolves catalog property references as MSBuild does for a Parties project. `Hexalith.Parties.Ci.Tests` expectations that hard-coded EventStore `3.104.0` or read only the catalog default now use the effective Parties pin.
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- update the reconciliation rows to the final set. Remove "commit pending" wording: FrontComposer and Memories were committed in `2f6157d3`, and the rest are pending the commit that follows this pass. Record the unapproved committed and superseded identities as dated history. Add I20 rows for decisions 1 and 2. Fill the existing `I16 identity parity re-validation` row (decision 3) and add an I13/DW-111 accepted-deferral row (decision 4). Add `47e2da32`'s source-mode gating to the G4 row; G4 stays `needs-additive-api`. — done 2026-10-05. Tree check: Builds `360a2b9c` and Tenants `72b8e4f5` were already committed in `2bdb303b`, so only the EventStore gitlink is pending. The rows say so, and the never-approved and superseded IDs are dated history. Four I20 rows were added or filled (decisions 1-4), and the G4 row records `47e2da32` while staying `needs-additive-api`.
- [x] `deferred-work.md` -- DW-111: add a decision line for the acceptance. DW-124: if the Release solution build passes, resolve it citing `47e2da32`, and keep a follow-up noting that package-mode reserved-segment wiring still waits for a FrontComposer release. Otherwise leave it open with the exact errors. — done 2026-10-05: DW-111 has the acceptance decision and stays open as the exit-proof carrier. DW-124 is `done 2026-10-05`, resolved by `47e2da32` (recorded 0-warning/0-error Release solution build), and open follow-up DW-139 covers package-mode reserved-segment/module-landing wiring. The full lane exposed three failures outside this spec's authority, recorded as DW-140 (Art.18 restriction inventory), DW-141 (`PartiesOverview` passes `FcPageTabs.ModuleRoute`, which Shell `4.5.0` lacks; package-mode runtime failure), and DW-142 (personal-data inventory).
- [x] `docs/architecture.md`, `docs/ci.md`, `ARCHITECTURE-SPINE.md` (frontmatter `open-condition`, §7 I4/I13, §7a I16, §7b I20), `sprint-status.yaml` (8.10 comment; value stays `in-progress`) -- cite the final set and the four decisions. — done 2026-10-05: architecture.md stack rows plus a final-set paragraph, and ci.md cites `3.113.0` with the source tag. Spine frontmatter `updated`/`amendment`/`open-condition`, §7 I4/I13, §7a I16, §7b I20, and a new §13 are updated. In sprint-status, `last_updated` is 2026-10-05, the 8.9 chain is corrected, the 8.10 comment is appended, and the value stays `in-progress`.
- [x] Verify and record in `tests/test-summary.md`: a dated section plus updated canonical receipt rows stamped with the final set. Run: — done 2026-10-05; receipts are in the canonical table and the dated 2026-10-05 section. Fitness classes: 46/48, failing only on the two EventStore committed-gitlink assertions; a scratch clone with that gitlink committed passes 48/48 and the RC diff gate. The `gitlink-rc-gate.sh` working-tree gate passes. Full Release lane: 2,608/2,624 passed, 10 failed, 6 skipped. After the Ci.Tests fix (57/57), 8 failures remain: 2 committed-gitlink assertions plus DW-140/141/142. Pack/API/consumers pass, npm ci and typecheck pass, and Playwright passes 6/6 at FrontComposer `2cc8dd3a`. Story 8.10 stays `in-progress`.
  - warning policy and Release solution build;
  - package-mode Parties.Tests build, plus `-class` runs of the three fitness classes;
  - `bash scripts/gitlink-rc-gate.sh`;
  - if the solution build passes: `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults`, then `npm ci --prefix tests/e2e && npm --prefix tests/e2e run typecheck && npm --prefix tests/e2e run test:a11y`, then the package/consumer command.

  A fitness failure is expected only where an assertion compares against the uncommitted EventStore/Builds/Tenants gitlinks; any other failure must be fixed or recorded. Record each lane's real result. Never write `Pass` for a lane that did not run green at these identities. Never stop a process with `pkill -f Hexalith.Parties.UI`, which kills its own shell; stop by PID.

  Already run 2026-10-05 at the final working-tree identities (Parties `47e2da32` plus the EventStore `v3.113.0` checkout and the `3.113.0` pin), so do not rerun it: `bash scripts/check-no-warning-override.sh && dotnet restore Hexalith.Parties.slnx -p:NuGetAudit=false && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` exited 0 with **Build succeeded, 0 Warning(s), 0 Error(s)** in 00:02:02. The former 3 CS0234 DW-124 errors are gone.

### Closure-blocker resolution: DW-140, DW-141, DW-142 — 2026-10-05

**State.** Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6` committed EventStore `865cd9e4`, so `git ls-tree HEAD references/` now records all eight final-set identities and the two committed-gitlink assertions should pass. The remaining closure failures are DW-140 (Server.Tests 1), DW-141 (UI.Tests 4), and DW-142 (Contracts.Tests 1).

**Decisions, Administrator / jpiquot, 2026-10-05 (Ask First, answered).** Story 8.10 resolves the three entries, then proceeds toward closure only if every gate is green:
1. **DW-140 (I7): exempt, then prove refusal.** Add `ProvisionAgentParty`, `EstablishHumanActorBinding`, `RebindHumanActorBinding`, and `RevokeHumanActorBinding` to the `exempt` inventory, each with a one-line justification. Production handlers stay unchanged. Provisioning is creation-only: a restricted party already exists, so the handler rejects it as an occupied identity, and an exact retry emits no events. The binding handlers refuse a restricted party through `CanBind` (`IsRestricted: false`) with `HumanActorBindingRejected("binding-unavailable")`, and an exact retry of a recorded transition emits no events.
2. **DW-142 (I8): classify.** `PartyCreated.CreatedAt`, `PartyState.HasBeenCreated`, `PartyState.HumanBindingVersion`, and `PartyState.AgentProvisioning` (a non-natural agent organization) are `NonPersonalMetadata`. `PartyState.HumanActorBindings` and `PartyState.HumanActorTransitions` (they carry `ActorId` / `OperatorActorId`) are `DeferredPrivacyDesign`, bound to open DW-129. No `[PersonalData]` attribute or contract changes. **Amended 2026-10-05 after review (Edge 17), Administrator / jpiquot:** `PartyState.AgentProvisioning` is already marked `[PersonalData]` (`PartyState.cs:19-20`, identity feature `37d87f5a`), so it is classified `PersonalData`, not `NonPersonalMetadata`. A new inventory guard requires every `NonPersonalMetadata` row to be unmarked. The other five rows stand.
3. **DW-141: gate behind source mode.** Packaged Shell `4.5.0` lacks both `FcPageTabs.ModuleRoute` and `FcPageTabs.DefaultTabId`. Pass `ModuleRoute="/parties"` and `DefaultTabId="overview"` only when `HFC_ROUTE_OPTIONS` is defined, through an `@attributes` dictionary in a new `PartiesOverview.razor.cs` partial (empty in package mode), matching `47e2da32`.

No dependency, submodule, owner-repository, rollback-deletion, or PRD change. Do not stage or commit.

- [x] `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateRestrictionTests.cs` -- decision 1 exemptions with justifications. — Reverted 2026-10-05 for review loop 3, then re-derived the same day per KEEP: the four commands are in `exempt`, each comment worded per the loop-3 patch.
- [x] `tests/Hexalith.Parties.Server.Tests/Aggregates/HumanActorBindingTests.cs` -- reuse the existing fixtures to add discriminating coverage. An Establish that succeeds on the unrestricted person party is refused with `binding-unavailable` and no events after `ProcessingRestricted` is applied. Rebind and Revoke behave the same on a bound party that is then restricted. — Reverted 2026-10-05 for review loop 3, then re-derived the same day per KEEP: the helpers and the three `Restricted*_Refuses*ThatSucceedsUnrestricted` facts pass (Server.Tests 259/259).
- [x] `tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs` -- decision 2 rows. — Reverted 2026-10-05 for review loop 3, then re-derived the same day per KEEP with amended decision 2 (`AgentProvisioning` = `PersonalData`).
- [x] `src/Hexalith.Parties.UI/Components/Pages/PartiesOverview.razor` and new `PartiesOverview.razor.cs` -- decision 3. — Reverted 2026-10-05 for review loop 3, then re-derived the same day per KEEP: `@attributes="ModuleRouteTabAttributes"` plus the new `PartiesOverview.razor.cs` (empty in package mode); all five `PartiesOverviewTests` pass and the source-mode branch compiles.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- DW-140/141/142: `status: done 2026-10-05` with `decision:` and `resolution:` lines naming the tests. DW-142's resolution names DW-129 as the open carrier for the binding classification. DW-139: add `PartiesOverview.razor` `ModuleRoute`/`DefaultTabId` to its location and exit proof. — done 2026-10-05.
- [x] Current claims that the EventStore gitlink commit is pending -- spine frontmatter `open-condition`, `docs/architecture.md` (EventStore row), and the current 8.3 matrix EventStore rows: cite `75b4fa1f` as the commit. Keep dated sections as history. Append a dated spine §13 paragraph for decisions 1-3. — done 2026-10-05: spine `open-condition` and the §7 I4 row, the `docs/architecture.md` EventStore row, and the 8.3 reconciliation paragraph plus EventStore source row cite `75b4fa1f`; the dated I20 rows, signoff comments, and dated sections stay as history; spine §13 has the dated paragraph.
- [x] Verify, then record in `tests/test-summary.md`: add a dated section and update the canonical `### Validation receipts` rows, stamped with the final set and the Parties tree under test (`75b4fa1f` plus this diff). Run: — Reverted 2026-10-05 for review loop 3, then rerun the same day with the loop-3 amendment's ordered verification; the receipts are in the test-summary review loop 3 subsection and the re-stamped canonical rows.
  - the warning-policy, restore, and Release solution build command above (code changed, so rerun it);
  - `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:HexalithFrontComposerFromSource=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` (diagnostic source-mode compile of the gated branch; record separately; not a canonical row);
  - `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults` (expected 0 failures plus the six Story 12 skips) and `bash scripts/gitlink-rc-gate.sh`;
  - the npm/typecheck/Playwright command and the package/consumer command in `## Verification`.

  Fix only failures caused by this section. Record any other failure as a new DW entry and leave closure blocked. A canonical row reads `Pass` only when its lane ran green at this tree. Append a dated 8.10 comment in `sprint-status.yaml`; the value stays `in-progress` because the workflow's review step owns status transitions. Stop processes by PID, never with `pkill -f Hexalith.Parties.UI`.

Acceptance for this section:
- Given a restricted person party, when an otherwise-valid Establish, Rebind, or Revoke runs, then it is rejected with `binding-unavailable` and no events, while the same command succeeds unrestricted.
- Given package mode (Shell `4.5.0`), when `PartiesOverview` renders, then all `PartiesOverviewTests` pass; given source mode, the gated `ModuleRoute`/`DefaultTabId` branch compiles.
- Given the inventory, when Contracts.Tests runs, then every inspected property is classified and no `[PersonalData]` marking changed.

### Review loop 3 amendment — 2026-10-05

**Why.** The review of the closure-blocker resolution (Review Triage Log, 2026-10-05) found one bad_spec entry. Decision 2 classified `PartyState.AgentProvisioning` as `NonPersonalMetadata`, but the property is already marked `[PersonalData]`. The Administrator corrected decision 2 (see above) and deferred the E2E fixture-scope gap as DW-143, which already exists. The code of the section above was reverted. Re-derive it per the KEEP instructions in `## Spec Change Log`, apply the corrected decision 2, and apply the surviving review patches. No dependency, submodule, owner-repository, rollback-deletion, `[PersonalData]`-attribute, production-handler, or PRD change. Do not stage or commit.

- [x] Re-derive the reverted tasks in the section above (restriction exemptions, binding refusal tests, inventory rows, `PartiesOverview` gate) per KEEP, then tick them. — done 2026-10-05: the restriction exemptions, binding refusal tests, inventory rows, and `PartiesOverview` gate were re-created per KEEP; the tasks above are ticked.
- [x] `tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs` -- add `PartyState.AgentProvisioning` = `Classification.PersonalData` (amended decision 2), plus a `[Fact]` asserting that no `NonPersonalMetadata` row's property carries `[PersonalData]` (mirror `OrganizationEntityFields_RemainUnmarkedByDefault`). — done 2026-10-05: the row reads `Classification.PersonalData`, and `NonPersonalMetadataProperties_RemainUnmarked` passes; a mutation run that reclassified `AgentProvisioning` as `NonPersonalMetadata` made it fail.
- [x] `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateRestrictionTests.cs` (Blind 6 / Edge 19 / Edge 20) -- each of the four exempt comments says the command never mutates a restricted party and lists the actual outcomes. Provisioning: an unmarked existing party gets `occupied-unmarked-identity`, a marked agent party with identical intent replays its original result with no events, otherwise `intent-conflict`, and invalid authorization gets `authority-unavailable`. Binding: a restricted party gets `binding-unavailable`, an exact retry replays with no events, and a reused LogicalId with another digest gets `intent-conflict`. — done 2026-10-05: each of the four comments says the command never mutates a restricted party and lists those outcomes.
- [x] `tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs`: — done 2026-10-05: `IsCleanPass` accepts only an exact `Pass` and both checks use it; the three qualified values and a new `AccessibilityStampIsRequiredOnlyForACleanPass` theory (red receipts without the SHA produce no gap) pass.
  - Edge 11: `DescribeReceiptGaps` accepts only an exact `Pass` after trimming `*` and spaces. Add `Pass with errors`, `Pass (not run)`, and `Pass (partial)` to `QualifiedPassWithMissingValidationFailsClosed`.
  - Edge 12: `ValidationReceiptSectionResolvesToTheCurrentTableAndIsWellFormed` checks the FrontComposer SHA only when the Playwright Result is a clean `Pass`.
- [x] `tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs` (Blind 13 / Edge 16) -- `CodeMapDocumentsThePinnedSdkVersion` also asserts that every `10.0.\d{3}` token in each document equals the pinned SDK. — done 2026-10-05: `FindStaleSdkTokens` runs for each document; all seven list only `10.0.401`, and a new `StaleSdkTokensAreReportedBesideThePinnedSdk` theory reports a stale token beside the pinned one.
- [x] `Directory.Packages.props` (Blind 12) -- extend the EventStore pin comment with its removal condition: drop the pin once the Builds catalog selects `3.113.0` or later. Comment only; the value is unchanged. — done 2026-10-05: the comment names the removal condition; the value stays `3.113.0`.
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` (Blind 1) -- in the current reconciliation rows for Commons, Builds, FrontComposer, Memories, Tenants, PolymorphicSerializations, and AI.Tools, replace "at Parties HEAD `47e2da3244fd7d6d14e39bc30513b23504e9bc27`" with "at Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`". Dated I20 rows stay as written. — done 2026-10-05: the seven rows cite `75b4fa1f`; the dated I20 row keeps `47e2da32`.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md`: — done 2026-10-05: DW-142's decision and resolution are amended and name the new guard, DW-129 and DW-92 have dated `note:` lines, and DW-143 is unchanged.
  - DW-142: amend the decision and resolution for `AgentProvisioning` = `PersonalData` (Edge 17 renegotiation) and name the new guard.
  - DW-129 (Blind 8): add a `note:` that its exit proof also classifies `PartyState.HumanActorBindings` / `HumanActorTransitions` in `PersonalDataInventoryTests` (currently `DeferredPrivacyDesign`).
  - DW-92 (Blind 11 group): add a dated `note:` that the effective EventStore version is the Parties pre-import pin `3.113.0` (catalog default `3.112.0`). The test-side lookups are DW-92 consolidation scope: `CommonsHttpRestoreRoutingTests`, `PartiesContainerPublishWorkflowTests.ReadEffectiveEventStoreVersion`, the `DocumentationFitnessTests` catalog evaluator, the `AccessibilityStyleGuardTests` FrontComposer lookup, and `PlatformApiPrerequisitesTests.EventStorePackageVersion`.
  - Leave DW-143 unchanged.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` (Blind 14) -- in the G5 retention action comment, change "Historical receipt:" to "Historical receipt (2026-08-01; superseded by the 2026-10-03 line below):". Also append a dated 8.10 loop-3 comment; the value stays `in-progress`. — done 2026-10-05: the label is changed and the dated loop-3 comment is appended; the value stays `in-progress`.
- [x] `docs/component-inventory.md` (Blind 15) -- the banner says the external-dependency versions were refreshed 2026-10-04. — done 2026-10-05: the banner says refreshed 2026-10-04.
- [x] Spine §13 and the test-summary closure-blocker section: correct their `AgentProvisioning` wording and add a dated loop-3 line. — done 2026-10-05: spine §13 decision 2 is corrected and has a dated *Review loop 3* paragraph; the test-summary closure-blocker authority text and change bullet are corrected, and the loop-3 subsection closes that section.
- [x] Verify, then record in `tests/test-summary.md`: add a dated loop-3 subsection, and re-stamp the six canonical Verification rows plus "Parties UI tests" and "Static diff" to "`75b4fa1f` plus the loop-3 diff". Producer rows keep their 2026-10-05 reruns, because the identities are unchanged. Run, in order: — done 2026-10-05: all seven steps ran green in order and are recorded in the test-summary review loop 3 subsection; the eight rows are re-stamped and the producer rows are unchanged.
  1. the warning-policy, restore, and Release solution build command;
  2. the diagnostic source-mode `Hexalith.Parties.UI` build;
  3. the full Release lane plus both `gitlink-rc-gate.sh` modes;
  4. the package/consumer command;
  5. npm/typecheck/Playwright;
  6. the three fitness classes after the record edits;
  7. `git -c core.whitespace=cr-at-eol diff --check`.

  Record real results, fix only failures caused by this amendment, and stop processes by PID.

Acceptance for this section:
- Given `PartyState.AgentProvisioning` marked `[PersonalData]`, when Contracts.Tests runs, then it is classified `PersonalData`, and any `NonPersonalMetadata` row carrying `[PersonalData]` fails the new guard.
- Given a receipt Result other than an exact `Pass`, when closure fitness evaluates it, then it is a gap; given a red Playwright receipt without the SHA, the structural test still passes.
- Given a maintained document that lists a stale `10.0.xxx` SDK beside the pinned one, when documentation fitness runs, then it fails.

**Acceptance Criteria:**
- Given an incomplete 8.6-8.9 disposition, when closure fitness runs, then any missing owner, proof, rollback, or evidence names the gap and prevents closure.
- Given package/source modes, when identity fitness runs, then receipts equal selected dependencies and unconsumed surfaces have explicit deferrals.
- Given maintained docs and I1-I15, when fitness runs, then current topology/inventory and zero-PRD scope map to executable evidence or a named external deferral; failures and skips remain owner-visible.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 && dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests` -- expected: build and closure fitness pass; repeat the assembly command for `DocumentationFitnessTests` and `PlatformApiPrerequisitesTests`.
- `bash scripts/check-no-warning-override.sh && dotnet restore Hexalith.Parties.slnx && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1` -- expected: pass.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults` -- expected: 15 projects pass; record topology skips.
- `pkg_dir=$(mktemp -d /tmp/parties-810-packages.XXXXXX); consumer_dir=$(mktemp -d /tmp/parties-810-consumer.XXXXXX); python3 scripts/pack-release-packages.py "$pkg_dir" 0.0.0-story810 && python3 scripts/validate-nuget-packages.py "$pkg_dir" && python3 scripts/validate-consumer-package-references.py "$pkg_dir" --work-directory "$consumer_dir"` -- expected: package/API compatibility passes.
- `npm ci --prefix tests/e2e && npm --prefix tests/e2e run typecheck && npm --prefix tests/e2e run test:a11y` -- expected: accessibility passes.


## Implementation note — 2026-10-04

Completed the independent Round 6 patches: committed-tree pin proof, zero-PRD
scope restoration, G4/G5 receipt reconciliation, current SDK/catalog documentation
and guards, CPM package asset restoration, closure receipt rejection cases, ledger
cleanup, and dated identity/evidence records. The valid-scope MainLayout fixture
and exact-route Development/Test specimen wrapper preserve the existing shell
assertions; ordinary routes retain the selected authenticated context. The wrapper
and its focused tests are listed in the Code Map above. The existing amended G5
ADR field is checked exactly, and independent G5 checks execute before the approved
pin guard. No dependency, submodule, owner repository, or rollback path changed.

Current evidence is in
`_bmad-output/implementation-artifacts/tests/test-summary.md`, including the
canonical closure table and exact commands. Source-mode Debug builds report zero
warnings/errors; after the review repairs, fitness passes 48/48 (documentation 6,
closure 26, prerequisites 16), with no skips. Focused UI checks pass 39/39
(MainLayout 3, style 4, host composition 17, scope guards 8, existing specimen 7),
and Playwright passes 6/6 at the same now-approved FrontComposer pin. Those source
executions do not constitute approved immutable parity. Package-mode Release
restore passes but build fails with 10 CS0246 errors because selected EventStore
package 3.110.0 lacks the identity-history types consumed by Parties.Contracts;
full Release tests and package/consumer validation are blocked by that build.

D1 identity reconciliation is complete after the Administrator's explicit
2026-10-04 approval of the three later pins. Constants, I20 records, and signoff
match the current committed gitlinks, and the RC diff gate passes all eight.
The separate I16 parity re-validation approval remains pending.
DW-124 retains FrontComposer release/catalog/consumer proof; the remaining I13
content-control focus evidence and applicable I20 approvals remain pending.
Story 8.10 and Epic 8 stay in progress; Stories 8.7–8.9 stay blocked. The frozen
intent, original baseline, and frontmatter status are preserved.

## Spec Change Log

### 2026-10-05 — review loop 3 (bad_spec: Edge 17)

- **Trigger:** Review of the closure-blocker resolution, Edge 17. `PartyState.AgentProvisioning` is marked `[PersonalData]`, yet decision 2 classified it `NonPersonalMetadata`, and no inventory guard caught the contradiction.
- **Amended:** decision 2 in *Closure-blocker resolution* (Administrator / jpiquot renegotiation: `AgentProvisioning` → `PersonalData`), plus the new *Review loop 3 amendment* section with the surviving review patches. The Administrator deferred Verification 1 as DW-143. The frozen block is untouched.
- **Known-bad state avoided:** an inventory that classifies a `[PersonalData]`-marked property as non-personal with no test to notice, which would let the inventory and the protection pipeline disagree silently.
- **KEEP** (the first pass was verified green; re-create these changes as described here):
  - `PartiesOverview.razor`: `<FcPageTabs @attributes="ModuleRouteTabAttributes" AriaLabel=… TestId="parties-overview-tabs">`, with `ModuleRoute` and `DefaultTabId` removed from the markup.
  - New `PartiesOverview.razor.cs`:
    - namespace `Hexalith.Parties.UI.Components.Pages`, `public partial class PartiesOverview`;
    - `private static IReadOnlyDictionary<string, object> ModuleRouteTabAttributes { get; }`, an Ordinal dictionary holding `["ModuleRoute"] = "/parties"` and `["DefaultTabId"] = "overview"` only under `#if HFC_ROUTE_OPTIONS`, and empty otherwise;
    - XML docs naming packaged Shell 4.5.0 and DW-139.
  - `HumanActorBindingTests.cs`: helpers `Rebind(DateTimeOffset)` (SecondActor, ExpectedPartyRevision 2, binding version 1), `Revoke(DateTimeOffset)` (FirstActor, revision 2, version 1), `Restriction()` (`ProcessingRestricted` for party-1/tenant-a), `BoundHuman()` (applies the Establish event), and `ShouldBeRefusedAsUnavailable`. The three facts are `RestrictedPersonParty_RefusesEstablishThatSucceedsUnrestricted`, `RestrictedBoundParty_RefusesRebindThatSucceedsUnrestricted`, and `RestrictedBoundParty_RefusesRevokeThatSucceedsUnrestricted`. Each handles the command on the unrestricted state without applying the result, applies `ProcessingRestricted` to the same state, then asserts one `HumanActorBindingRejected("binding-unavailable")`.
  - `PartyAggregateRestrictionTests.cs`: the four commands are in `exempt` with one-line comments, worded per the loop-3 patch.
  - `PersonalDataInventoryTests.cs`:
    - `PartyCreated.CreatedAt`, `PartyState.HasBeenCreated`, and `PartyState.HumanBindingVersion` are `NonPersonalMetadata`;
    - `HumanActorBindings` and `HumanActorTransitions` are `DeferredPrivacyDesign`, with a DW-129 comment;
    - `AgentProvisioning` follows the amended decision 2.

## Review Triage Log

### Baseline review following the three-pin approval — 2026-10-04

Review content is the full baseline diff from `37f4ec826c6f4aea4651cfbad94fb6ab7fc4f0a0`
at Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461`, including untracked repairs:
20,285,633 bytes and 2,093 file entries. All three instructed layers returned.
This is a bounded review of reachable product/configuration/test paths; the
reviewers explicitly did not exhaustively inspect every historical/installed
BMAD asset. Findings below are judged individually before grouping. Identity
feature paths are unchanged in this repair and were already committed in
`37d87f5a2869b076c651a58714d60f64647848bc`; other historical paths name their
commits below. Those findings are follow-ups, not claims of delivered fixes.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| Blind 1 — ineligible human/unknown client replies | medium | Query service returns Ineligible with a live HumanBinding (or Unknown classification), while HttpPartiesIdentityClient rejects both shapes at lines 58–65. Existing identity-feature code from `37d87f5a2869b076c651a58714d60f64647848bc`, unchanged here. Defer; shared root cause with Verification 3. |
| Blind 2 — empty binding acknowledgment | medium | SubmitAsync accepts any JSON object unless success=false or rejected=true; SubmitCommandResponse is a record without constructor validation and the binding callers discard it. `{}` can complete binding submission with missing correlation/message identities. Existing identity-feature code, unchanged here. Defer. |
| Blind 3 — no-op snapshot custody | high | ProtectSnapshotStateAsync wraps any provider result; the test provider returns its input. HumanActorBindings/Transitions and their actor evidence have no profile PersonalData annotation; the adapter marks IdentityHistorySnapshot Protected. A no-op provider can leave history in that wrapper while metadata claims protection. Existing identity-feature code, unchanged here. Defer; no live custody qualification is inferred. |
| Blind 4 — expired-history replay | maybe-false | All command rehydration unwraps history, and fallback catches only PartyEncryptionKeyDestroyedException. Whether a qualified provider's expiry outcome breaks ordinary commands requires exercising expiry with that provider and replay/switch-back. No live expiry provider is proved in this change; possible high harm remains unverified. Defer with that required experiment. |
| Blind 5 — fold wire versions | medium | PartyIdentitySourceFold reads payload as JSON without consulting StreamReadEvent.SerializationFormat or MetadataVersion. A contiguous JSON-shaped unsupported representation is accepted by this fold; the reader's authoritative flag does not itself document Party wire-version support. Existing identity-feature code, unchanged here. Defer. |
| Blind 6 — release bypass without tests | medium | release.yml selects commitlint.yml when bypass-validation=true and passes empty test-projects to publication. The exact-source proof then proves commitlint, not CI tests. Existing workflow from `14d249fde316b0002aec84351d7a7cdf953d1d30`, unchanged here; protected human approval does not supply missing test evidence. Defer to release-policy owners. |
| Blind 7 — source Playwright bypasses package CI | false | No repository workflow calls Playwright/test:a11y; package CI is a separate lane. Source-mode Playwright is the existing Round 2 D3 accepted decision, with package parity explicitly unvalidated and no package certification claimed. This configuration therefore does not bypass an executing package CI lane. Reject. |
| Blind 8 — real Parties admission proof not exercised | medium | PartyIdentityAdmissionTests substitutes IPartyIdentityAuthority; PartyIdentityAuthority constructs digest, logical/message/operation/scope values before calling the owner proof verifier, but repository searches find no direct real-authority test. Owner verifier tests cannot prove this Parties scope construction. Existing identity-feature code, unchanged here. Defer. |
| Blind 9 — visual gate/update command | medium | The specimen attaches a PNG, checks only its byte count, and compares a manually read JSON baseline. No image assertion uses --update-snapshots, so layout/color regression and the advertised update command are unverified. Existing test from `f8fd74045d95d441ee5adb0809a254307d0a24a6`, unchanged here. Defer. |
| Blind 10 — media test does not observe content styling | medium | The test proves media emulation and only a truthy outlineColor on a shell skip link; invisible outlines still have a color and no app-content animation/transition duration is asserted. This leaves I13 content focus/reduced-motion proof missing. Existing browser test, unchanged here. Defer. |
| Blind 11 — shell excluded from axe scope | medium | The axe include selects #fc-main-content; requiredSelectors only prove navigation/skip-link existence. Navigation and skip-link defects are outside the scan. Existing browser test, unchanged here. Defer. |
| Edge 1 — required receipt omission | high | Closure requires only Release solution build and Playwright rows, then evaluates whatever rows survive. Omitting required all-.NET/package/npm/warning evidence can admit closure. Patch: require the canonical rows for every Verification lane and add omission rejection cases. |
| Edge 2 — receipt heading boundary | medium | ReadLatestValidationReceiptSection stops only at a literal level-three heading. A subsequent level-one/two section can add unrelated receipt rows or trigger duplicates. The reviewer's guard_snippet showed the proposed regex rather than the actual implementation; the inspected IndexOf confirms the reported outcome. Patch: stop at the next level-one/two/three heading and cover each boundary. |
| Verification 1 — processor provisioning success | medium | Pre-verified regression gap: aggregate provisioning tests inject Authorization directly; processor tests cover only unsupported-policy rejection. Removing processor Authorization injection leaves those cases green while real provisioning rejects. Existing identity-feature paths, unchanged here. Defer despite the reviewer's patch suggestion. |
| Verification 2 — wrong-purpose case masked by expiry | medium | Pre-verified broken-verification gap: the single client case combines an expired interval with other-purpose. Removing the purpose check still rejects on expiry. Existing identity-feature test, unchanged here. Defer and require independent valid-interval wrong-purpose coverage. |
| Verification 3 — ineligible server/client mismatch | medium | Verified at PartyIdentityQueryService ResolveAsync and HttpPartiesIdentityClient ResolvePartyIdentityAsync: a valid inactive human reply becomes client Unavailable/503. Existing identity-feature code, unchanged here. Defer; grouped with Blind 1 without dropping either finding. |

There are no intent_gap or bad_spec entries. The two closure guard patches are
in this repair. The twelve remaining root causes are deferred; each keeps its
own evidence rather than expanding this documentation/retirement change into
identity feature delivery or release-policy changes. No owner commitment,
release, dependency change, or deletion is authorized by this triage.

### Review patch verification and workflow stop — 2026-10-04

Both Edge patches are complete. Closure and structural checks now require all six
canonical Verification-lane receipts. Six independent omission cases exercise
the same closure guard. The parser stops at the next level-one/two/three heading;
three boundary cases prove that later rows and evidence stay outside the table.
The source-mode Debug build passed with zero warnings/errors; parent execution
of DocumentationFitnessTests, EpicEightClosureFitnessTests, and
PlatformApiPrerequisitesTests passed 48/48 (6/26/16), with no errors, failures,
skips, or unrun cases. Focused UI remains 39/39 and Playwright 6/6 at the unchanged
approved source pins. Review did not qualify package or I13 parity.

After a successful matching package-mode Release restore, the first required
Verification command was rerun exactly. Its Release build failed with zero
warnings and ten CS0246 errors because EventStore package 3.110.0 lacks the
identity-history contract types. The chained Release assembly test did not run.
Step-04 requires a halt on unfixable verification failure; solving this package
blocker requires owner release/catalog work beyond the exact three-pin approval.
The workflow therefore stops before step-05, with Story 8.10 restored to
in-progress and sprint status synchronized. Full Release tests, package/API and
package-only consumers remain blocked. No commit, push, owner edit, catalog
change, or rollback deletion occurred. Exact commands and receipts are in
`_bmad-output/implementation-artifacts/tests/test-summary.md`.

### Review of the closure-blocker resolution — 2026-10-05

Review content is the delta from the last fully reviewed tree. The 2026-10-04
baseline review read the whole 20 MB `37f4ec82…` diff at Parties `c782b68c…`. This
round reviews `git diff c782b68c5cf56a19e6a2a237f5f44e3043d5e461` against the
working tree, plus the untracked `PartiesOverview.razor.cs`: 447,490 bytes,
covering commits `7c720c7f`, `2f6157d3`, `2bdb303b`, `47e2da32`, `75b4fa1f` and
this pass's diff. All three layers returned: Blind 17 findings, Edge 22,
Verification 2 gaps plus 1 other. No row matches a 2026-10-04 row, so nothing is
carried.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| Edge 17 — `AgentProvisioning` is marked `[PersonalData]` | high | `PartyState.cs:19-20` marks `AgentProvisioning` `[PersonalData]`. Decision 2 and the new inventory row classify it `NonPersonalMetadata`, and no inventory test requires `NonPersonalMetadata` rows to be unmarked. Decision 2 rested on a wrong premise in the triage question ("non-natural agent organization"). bad_spec: decision 2 needs Administrator renegotiation. |
| Verification 1 — E2E fixture routes get no shell scope | medium | Pre-verified gap. The AdminPortal E2E fixture keeps `NullUserContextAccessor`, and the `2cc8dd3a` shell renders `FcScopeBlocked` without a current scope. The admin, consumer and picker Playwright specs therefore cannot observe their flows. Only `test:a11y` is recorded, and CI runs no e2e job. The Code Map scoped the 2026-10-04 repair to the specimen route, and the spec does not settle a fixture user-context. intent_gap: Administrator. |
| Verification other — fixture routes untracked | medium | Same root cause and route as Verification 1. |
| Edge 11 — qualified pass accepted | medium | `DescribeReceiptGaps` accepts `Pass with errors`, `Pass (not run)` and `Pass (partial)`, because none contains block, unvalidated, fail or skip. Patch: accept only an exact `Pass` after trimming bold markup, and add those values to the qualified-pass theory. |
| Edge 12 — SHA check fails honest red receipts | low | The structural test checks the FrontComposer SHA unless the Playwright Result says "unvalidated". An honest `**Blocked**` receipt without the SHA would break the build, contrary to the test's own comment. Patch: check the SHA only when that Result is a clean `Pass`. |
| Blind 1 — matrix rows cite the old HEAD | low | Seven current 8.3 rows (lines 84-90) say "at Parties HEAD `47e2da32`", but HEAD is now `75b4fa1f`. Rows 154-155 are dated I20 history. Patch: cite `75b4fa1f` explicitly in the seven current rows. |
| Blind 6 — DW-140 justifications imprecise | low | The comments say a restricted party "is rejected as an occupied identity" or gets `binding-unavailable`. The code also replays a marked agent party's original result, answers `intent-conflict` to a reused LogicalId with another digest, and `authority-unavailable` to bad authorization. The retry-before-`CanBind` ordering is already exercised by `RefreshedOperatorProof_RetriesImmutableOriginalAfterStateBecomesInactive`. Patch: reword the four comments to say "never mutates a restricted party" and list those exact outcomes. |
| Edge 19 — provisioning outcome misstated | low | Same root cause as Blind 6; same patch. |
| Edge 20 — intent-conflict on a restricted party | low | Same root cause as Blind 6; same patch. |
| Blind 8 — DW-129 does not carry the classification | low | DW-129's summary and exit proof cover snapshot-protection effectiveness only, yet DW-142 names it as the carrier. Patch: add a DW-129 note that its exit proof also classifies `PartyState.HumanActorBindings` and `HumanActorTransitions` in `PersonalDataInventoryTests`. |
| Blind 11 — divergent version lookups; DW-92 stale | low | The test-side lookups agree today, because only EventStore has a root pin, but would diverge for a root pin of another package. DW-92 still says to read EventStore from the Builds catalog, though the Parties pin governs. Patch: append a dated DW-92 note naming the effective pin and listing the test-side lookups as DW-92 scope. |
| Edge 8 — style guard reads the FrontComposer catalog only | low | Same root cause as Blind 11; covered by that patch. |
| Edge 9 — mini-evaluator ignores conditions | low | Same root cause as Blind 11; covered by that patch. |
| Edge 15 — root-pin condition unchecked | low | Same root cause as Blind 11; covered by that patch. |
| Edge 21 — "as MSBuild does" claim | low | Same root cause as Blind 11; covered by that patch. |
| Blind 12 — pin has no removal condition | low | `Directory.Packages.props:4-5` explains the pin but not when to drop it. Patch: extend the comment with the removal condition (drop it once the Builds catalog selects `3.113.0` or later). |
| Blind 13 — SDK guard lost negative checks | low | `c782b68c` had `ShouldNotContain("10.0.400")` and `("10.0.302")`; a stale SDK listed beside the pinned one now passes. All seven documents list only `10.0.401`. Patch: assert that every `10.0.\d{3}` token in each document equals the pinned SDK. |
| Edge 16 — SDK guard deletion | low | Same root cause as Blind 13; same patch. |
| Blind 14 — G5 comment only partly relabelled | low | The present-tense sentences after "Historical receipt:" read as current until the 2026-10-03 line. Patch: label the paragraph as the 2026-08-01 receipt, superseded by the 2026-10-03 line. |
| Blind 15 — component-inventory banner stale | low | The banner says versions were refreshed 2026-09-05, but this delta changed Aspire, DAPR and FluentUI. Patch: correct the banner. Extending the table guard is rejected (new guard, low). |
| Blind 2 — spec body contradicts the status | low | The dated sections still say in-progress. The fix would edit this build's spec. Reject. |
| Edge 18 — "no events" wording | low | A rejection appends one `HumanActorBindingRejected` event, which the tests assert, and DW-140 says "no state-changing events". Only the spec's acceptance wording is loose, and fixing it would edit this build's spec. Reject. |
| Blind 3 — specimen scope survives in-circuit navigation | low | Real, but the wrapper exists only in Development/Test with the specimen flag, and the fix needs forceLoad or circuit-snapshot logic. Reject. |
| Edge 4 — in-circuit specimen exit | low | Same root cause as Blind 3. Reject. |
| Edge 2 — PathBase, case or trailing slash | low | The comparison is an Ordinal `AbsolutePath` match. The specimen is a Development/Test Playwright fixture visited at its exact path. Reject. |
| Edge 3 — torn TenantId/UserId read | low | Needs a navigation between two reads by one consumer. Blazor serializes circuit work, and the wrapper is Development/Test only. Reject. |
| Edge 1 — uninitialized NavigationManager | maybe-false | No out-of-circuit `IUserContextAccessor` consumer exists in Parties or the FrontComposer Shell. If one appeared, it would throw only with the Development/Test specimen enabled, so the impact is low. Reject. |
| Edge 5 — keyed descriptor | low | No keyed `IUserContextAccessor` is registered, and one would fail loudly at startup. Reject. |
| Edge 6 — lifetime or disposal | false | `NullUserContextAccessor`, `ClaimsPrincipalUserContextAccessor` and `ServerCircuitUserContextAccessor` are scoped registrations that implement only `IUserContextAccessor`. Reject. |
| Blind 4 — Register type path and Program gate untested | low | The type path (Test-env `NullUserContextAccessor`) runs in the green Playwright specimen lane. The Production theory row returns the authenticated context, and a missing registration fails loudly. Reject. |
| Edge 7 — older sibling FrontComposer root | low | The root gitlink `2cc8dd3a` carries the route options, and an older sibling root fails loudly at compile time. Reject. |
| Blind 9 — source branch only compiled | low | Both keys match the source `FcPageTabs` parameters at `2cc8dd3a`. Only the developer/Playwright source graph takes this branch, and no spec visits `/parties`. Tying the gate to source mode is decision 3. Reject; DW-139's exit proof removes the gate with green tests. |
| Verification 2 — source branch never rendered | low | Pre-verified gap. Same root cause as Blind 9, and it affects the source graph only. The filed disposition is defer, but this change caused it, so defer does not apply. Reject (low; a new source-mode test lane). |
| Blind 16 — superseded approvals still pass the RC gate | low | True: `ledger_has` accepts any `validated-advance` row. The 8.3 rollback paths deliberately name prior approved identities (for example Builds `145ae921`), and a withdrawal state needs a new ledger disposition and gate branch. Reject. |
| Edge 14 — superseded signoff rows | low | Same root cause as Blind 16. Reject. |
| Edge 13 — heading parser | low | test-summary has no fenced or setext headings, and the fix adds parsing logic. Reject. |
| Blind 5 — fail-closed rule drops honest skip wording | false | The six Story 12 skips stay verbatim in the evidence column, and the gate deliberately treats any qualified Result as a gap. Reject. |
| Blind 7 — revoke refused while restricted | false | Decision 1 chose "exempt + prove refusal" over the offered "allow revoke" option, and spine §13 records it. Reject. |
| Blind 10 — package-mode reserved segments untested | false | Packaged Shell `4.5.0` has no `FcModuleLandingPage` or `/{Module}` route (DW-139), so nothing collides in package mode. Reject. |
| Blind 17 — DW-127–138 format | false | They follow the step-04 defer template (`source_spec`/`summary`/`evidence`) plus origin, location, severity and status. Reject. |
| Edge 10 — catalog duplicates throw | false | An exception on an input the catalog does not contain is a loud failure, not a silent pass. Reject. |
| Edge 22 — no focused-fitness receipt row | false | The three fitness classes live in `Parties.Tests`, which the "All .NET test projects" receipt covers (612/612). Reject. |

Routing: there is one bad_spec entry (Edge 17) and one intent_gap entry
(Verification 1 and other). Both trigger a loopback and need the
Administrator. The patch entries (Edge 11, Edge 12, Blind 1, Blind 6/Edge
19/20, Blind 8, Blind 11 group, Blind 12, Blind 13/Edge 16, Blind 14, Blind 15)
survive the loopback. No owner commitment, release, dependency change, or
deletion is authorized by this triage.

Resolution: the Administrator corrected decision 2 (`AgentProvisioning` →
`PersonalData`, plus a guard) and deferred Verification 1 as DW-143. Review loop
3 re-derived the code with the patches.

### Review loop 3 — 2026-10-05

This round reviews the same delta scope, `c782b68c…` to the working tree plus the
untracked `PartiesOverview.razor.cs`: 488,177 bytes. All three layers returned:
Blind 14 findings, Edge 18 (plus 1 claim), and Verification 2 gaps plus 1 other.
Rows marked `carried` match a 2026-10-05 row whose code still reads as that row
describes. They keep that row's verdict and route and are not patched again.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| L3 Blind 2 — gate tied to source mode, not capability | low | carried — Blind 9. Reject. |
| L3 Blind 10 — superseded spec sections read as current | low | carried — Blind 2. Reject. |
| L3 Verification 1 — specimen host wiring checked only by Playwright | low | carried — Blind 4 (same `Register`/`Program.cs:203-206` claim). Reject. |
| L3 Verification 2 — source route-options branch unasserted | low | carried — Verification 2. Reject. |
| L3 Verification other — style guard ignores a FrontComposer root pin | low | carried — Edge 8 (patched through the DW-92 note). |
| L3 Edge 1 — superseded signoff rows still authorize | low | carried — Blind 16 / Edge 14. Reject. |
| L3 Edge 3 — CommonsHttp root-pin condition | low | carried — Edge 15. |
| L3 Edge 4 — style guard catalog-only lookup | low | carried — Edge 8. |
| L3 Edge 5 — catalog conditions or duplicates | low | carried — Edge 9 / Edge 10. |
| L3 Edge 9 — uninitialized NavigationManager | maybe-false | carried — Edge 1. Reject. |
| L3 Edge 10 — specimen path case, slash, or PathBase | low | carried — Edge 2. Reject. |
| L3 Edge 11 — torn TenantId/UserId read | low | carried — Edge 3. Reject. |
| L3 Edge 12 — keyed, singleton, or missing original accessor | low | carried — Edge 5 / Edge 6 / Blind 4. Reject. |
| L3 Edge 18 — "no events" claim | low | carried — Edge 18. Reject. |
| L3 Blind 4 — closure gate checks identity only on the Playwright row | medium | True: the other required rows accept a stale `Pass`. The behavior is pre-existing: `c782b68c` had the same Playwright-only SHA check and identity-blind `DescribeReceiptGaps`. Defer (DW-144). |
| L3 Edge 16 — stale `Pass` from an earlier identity passes the gate | medium | Same root cause as L3 Blind 4. Defer (DW-144). |
| L3 Blind 5 — identity-feature contracts missing from the personal-data inventory | medium | True: `s_inspectedTypes` omits the binding commands, evidence, and nested identity records. These were added in `37d87f5a`, before this delta, and DW-142 classified only the six properties that failed. Pre-existing coverage gap. Defer (DW-145). |
| L3 Blind 1 — pin has no automated tripwire | low | Any catalog move needs a Builds gitlink advance, which fails `PlatformApiPrerequisitesTests` (`BuildsSha`) until reconciled, and the pin comment now states the removal condition. Reject. |
| L3 Edge 2 — catalog passes the root pin silently | low | Same root cause as L3 Blind 1. Reject. |
| L3 Blind 3 — tree under test not content-addressed | low | The next commit is made from this working tree, and the record edits made after the lanes change no build input. A digest procedure is more than a direct fix. Reject. |
| L3 Blind 6 — exemption-comment outcomes untested | low | `ProvisionAgentParty` never reads `IsRestricted`, so the existing `AgentPartyProvisioningTests` cover its outcomes. `RetryBinding` runs before `CanBind`, which the inactive-state retry test already exercises. Reject. |
| L3 Edge 19 — decision 1 wording for agent parties | low | The test comments are already accurate, and fixing the wording would edit this build's spec. Reject. |
| L3 Blind 7 — I7/I8 decisions recorded only in prose | low | I20's enumerated approval kinds (spine line 337) include neither restriction-inventory exemptions nor inventory classification. The decisions carry the named human and date in DW-140/DW-142 and spine §13. Reject. |
| L3 Blind 8 — dated I20 rows lack an outcome | low | The decision 1 and decision 2 rows, and the 2026-10-05 `.gitlink-signoff.tsv` comment, still read "commit pending", whereas the five-pin row has an outcome note. Patch: append the outcome (committed in `75b4fa1f`). |
| L3 Blind 9 — G4 rows omit DW-141 and DW-111; rollback commit unnamed | low | Patch: add the DW-141 gate and the 2026-10-05 DW-111 acceptance to the G4 cells, and name `2b63ab9` in the FrontComposer rollback. |
| L3 Blind 11 — historical test-summary pointers stale | low | Patch: point both historical sections at the canonical table and the 2026-10-05 sections. |
| L3 Blind 12 — Code Map omits the newly changed files | low | Fixing it would edit this build's spec. Reject. |
| L3 Blind 13 — DW-143 under-recorded | low | Patch: add a `decision:` line to DW-143, and note in the canonical Playwright row that only `parties-accessibility.spec.ts` runs (the other specs fall under DW-143). |
| L3 Blind 14 — RC diff base unexplained | low | Patch: add one test-summary sentence stating what `882c0245` is and why it bounds the RC diff. |
| L3 Edge 6 — SDK regex hard-codes the 10.0 band | low | Patch: build the pattern from the pinned SDK's major.minor and add a theory case. |
| L3 Edge 14 — untyped `FcPageTabs` parameter keys | low | Patch: use `nameof(FcPageTabs.ModuleRoute)` / `nameof(FcPageTabs.DefaultTabId)` inside the `#if`, so a source-mode rename fails at compile time. |
| L3 Edge 7 — private-IP false positive | low | The failure would be loud, no maintained document contains such an address, and the suggested lookahead would miss sentence-final versions. Reject. |
| L3 Edge 8 — stale Aspire/EventStore tokens | low | It needs a new guard. Reject. |
| L3 Edge 15 — staged gitlink differs from HEAD | low | The gate proves the committed tree, and the next run sees the staged pointer at HEAD. Reject. |
| L3 Edge 13 — package mode newly exposes the Shell `/home` route | false | Before `47e2da32` the router already included the Shell assembly through `typeof(FcModuleLandingPage).Assembly`; the commit only changed the anchor type to `FrontComposerShell`. Reject. |
| L3 Edge 17 — detached-HEAD check deleted | false | A Round 6 review decision (spec line 262, option (a)) restored `ls-tree HEAD` and dropped the branch-name check, because SHA equality plus a clean tree already prove identity. Reject. |

Routing: there is no intent_gap or bad_spec entry. The eight patch entries go to
the loop-3 implementation agent; DW-144 and DW-145 are appended. No owner
commitment, release, dependency change, or deletion is authorized by this triage.

## EventStore Package Update — 2026-10-04

Administrator explicitly requested EventStore version 3.112.0. Parties now pins
that release in Directory.Packages.props before importing the shared catalog,
and selected-package guards/docs/I20 records are reconciled. All ten package
consumers evaluate to 3.112.0, including evaluation against the committed older
Builds catalog. Package-mode Parties.Tests Release build passes with zero
warnings/errors; the former ten missing identity-history type errors are resolved.
Focused checks report 61 passed, two committed source-pin failures, zero skips.
Full Release solution restore passes; build remains blocked by three CS0234
errors for missing FrontComposer Shell 4.5.0 module-landing types (DW-124).
The original baseline/frozen intent are retained. Existing user source advances
remain untouched and unvalidated; no source-pin guard or approval was weakened.
Story 8.10 stays in-progress. Exact commands are in tests/test-summary.md.
