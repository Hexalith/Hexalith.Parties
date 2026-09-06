---
title: '8.10 Final readiness, documentation, and retirement gate'
type: 'refactor'
created: '2026-08-17'
status: 'in-review'
baseline_commit: '37f4ec826c6f4aea4651cfbad94fb6ab7fc4f0a0'
review_loop_iteration: 2
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

- [ ] [Review][Patch] Amend frozen §4 clause 1's FrontComposer identity to `5cbc5583142a6774ff7813698ad98ec267b336f0` / `v4.3.0-7-g5cbc5583` / packaged `4.3.0`, and fix the matrix's stale row still citing `7a337a21`. [spec-8-10-final-readiness-documentation-and-retirement-gate.md:58-59, story-8-3-platform-api-prerequisite-matrix.md:101]
- [ ] [Review][Patch] Reconcile PolymorphicSerializations identity to `8aeed1d27c9a050bc4bec6d89051aa00de306a69` across `test-summary.md`, the 8-3 matrix G5 row, and `.gitlink-signoff.tsv`; close the `polymorphicserializations-stylecop-fix-incomplete-at-selected-gitlink` and `authorized-owner-fixes-not-immutable` blockers now that the Release build is confirmed green at this gitlink. [_bmad-output/implementation-artifacts/tests/test-summary.md:615, story-8-3-platform-api-prerequisite-matrix.md:68, .gitlink-signoff.tsv]
- [ ] [Review][Patch] Add a `.gitlink-signoff.tsv` `validated-advance` row for Commons `6da79aed2daa4e199689331ee3196f7872c0988a`, dated 2026-09-05, owner `jpiquot`. [.gitlink-signoff.tsv]
- [ ] [Review][Patch] Fix sprint-status.yaml's Story 8.10 comment — it must not claim a "moved review -> in-progress" transition when the key's value is `review`; update it to reflect the current status and the now-resolved PolySer/build blockers. [_bmad-output/implementation-artifacts/sprint-status.yaml:216-224]
- [ ] [Review][Patch] PlatformApiPrerequisitesTests.RunProcess reads stdout synchronously before its timeout applies — `ReadToEnd()` on stdout runs before `WaitForExit(ProcessTimeoutMilliseconds)`; only stderr got the async read a recent commit's message claims added timeout protection for. A hung `dotnet msbuild -getProperty/-getItem` child (used by `EvaluateProjectGraph`) that keeps stdout open blocks forever, and the 300s bound never fires. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1549-1573]
- [ ] [Review][Patch] AssertGitlinkAndCheckout accepts a staged-but-uncommitted gitlink via its `indexLink` disjunct — undermines the "must be a real commit" invariant the surrounding comment states; local/pre-commit runs can be fooled by `git add` without `git commit` (CI itself is unaffected since index equals HEAD there). [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1511-1531]
- [ ] [Review][Patch] README.md's new DAPR-ACL paragraph omits the one allow-by-default exception — states blanket "deny-by-default" while `docs/event-publishing.md` explicitly documents that `accesscontrol.eventstore-admin.yaml` is `defaultAction: allow`, in this same diff. Repeats the round-2 finding; fixed in one doc but not propagated to README. [README.md:45-48]
- [ ] [Review][Patch] sprint-status.yaml / deferred-work.md cite superseded dependency identities — Story 8.10's sprint-status narrative and deferred-work.md's "code review of spec-8-10" note still say EventStore 3.95.0/Commons `6fbac0c5`/Builds `17b1c7a`, which docs/ci.md, docs/architecture.md, the 8-3 matrix, and PlatformApiPrerequisitesTests.cs superseded with the 2026-09-05 catalog adopt. Not load-bearing, but misleads a reader of the closure log. [_bmad-output/implementation-artifacts/sprint-status.yaml:198-201, deferred-work.md:389]
- [ ] [Review][Patch] Tracked review-prompt artifact commits AI-reviewer-directed instructions to permanent history — `review-verification-gap-8-10-round-3-prompt.md` is tracked, addresses a future AI reviewer directly, references an absolute local path now excluded by this diff's own new `.gitignore` entry (`_bmad/render/`), and cites a PolySer end-SHA matching neither the matrix's SHA nor the actual current gitlink. Delete it (or keep such scratch prompts under the already-gitignored path). [_bmad-output/implementation-artifacts/review-verification-gap-8-10-round-3-prompt.md]
- [ ] [Review][Patch] Matrix reconciliation section dated 2026-08-18 but content is 2026-09-05 — the "Story 8.10 final retained-identity reconciliation" section heading predates the EventStore/Builds/FrontComposer rows it contains, which match `.gitlink-signoff.tsv` entries dated 2026-09-05; the section's own text even says the gitlink "tracks the live checkout after the 2026-09-05 catalog adopt." [story-8-3-platform-api-prerequisite-matrix.md:51]
- [ ] [Review][Patch] docs/component-inventory.md banner date is stale — says "reconciled for Epic 8 closure on 2026-08-18" but its "External dependencies" line was edited for the 2026-09-05 catalog adopt without bumping the date. [docs/component-inventory.md:3]
- [ ] [Review][Patch] Malformed reconciliation-table row silently dropped — `ParseFinalConsumptionRows` does `continue` on any row without exactly 5 pipe-delimited cells instead of failing loudly; a malformed required row still fails downstream via a generic "missing key" message, but an unlisted row vanishes with zero signal. [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:855-858]
- [ ] [Review][Patch] DocumentationFitnessTests' `defaultAction: allow` check is indentation-sensitive — requires exactly 4 spaces (`^\s{4}defaultAction:\s*allow\s*$`) unlike the deny-check's flexible `^\s*defaultAction:\s*deny\s*$`; works today only because every real file happens to use 4-space indent. [tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:178]
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
