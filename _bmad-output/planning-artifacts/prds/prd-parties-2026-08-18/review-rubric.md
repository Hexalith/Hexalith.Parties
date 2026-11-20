# PRD Quality Review — Parties UI PRD v1.3.2

## Overall verdict

The PRD gives maintainers a usable inventory of the Admin and Consumer requirements, and defines concrete evidence for closing the production key-custody gate. Its verification matrix still points to retired controls and omits open qualification work for erasure and signed-in access. A release reviewer needs those evidence references corrected before relying on the matrix for readiness.

## Decision-readiness — thin

GATE-KMS identifies the deployment owner, the affected data and the required startup, host-replacement and independent-replica checks (PRD lines 433–445). The November decision keeps the initial erasure worker and replica revocation separate from key custody. A reader following the FR matrix cannot see how those open decisions affect the promise to prove erasure.

### Findings

- **[high] Erasure completion lacks a qualification reference** (FR-Admin-4 and FR-Consumer-4, lines 104–109, 135–143, 320 and 324). The PRD says an Admin can “prove erasure with a verification report.” The browser test checks a report returned after a retry and captured request counts (`tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts` lines 39–60). The tracker still calls for one accepted gateway request to reach destruction, persisted certification and redacted reads, and for both replicas to refuse retired keys (`_bmad-output/implementation-artifacts/sprint-status.yaml` lines 375–381 and 404–409). **Fix** — Keep the report and request/cancel UI evidence, then mark the live lifecycle and replica clauses `unverified` in the applicable FR cells and cite the two tracker actions. Keep GATE-KMS limited to key custody.

## Substance over theater — strong

The role precedence, zero-or-multiple party-binding outcome and Consumer cancellation boundary translate into observable behaviors (lines 68–76 and 133–143). The distinction between consent and other lawful bases determines which control users see (lines 125–131). These details justify their place in a brownfield requirements index.

## Strategic coherence — strong

The two role-gated areas support a single purpose. The own-data boundary explains the Consumer feature set, and the Admin role covers organization-wide records within a tenant. Lines 388–415 distinguish maintenance and the external Agents contract from MVP delivery. The October scope addendum confirms that decision at `_bmad-output/planning-artifacts/implementation-readiness-scope-addendum-2026-10-09.md` lines 10–29.

## Done-ness clarity — thin

The functional requirements generally specify an observable outcome. Two acceptance boundaries need correction before reviewers can tell which build or deployed session satisfies them.

### Findings

- **[high] Signed-in own-data access has an open deployment check** (FR-Shell and NFR3, lines 60–76, 170–177, 316 and 336). The PRD calls the Parties-side assertion “the implemented enforcement today.” Its matrix cites role, claim and client tests. The November tracker still requires a signed-in read and write through the standalone host and real gateway, including session expiry and isolation between independent users (`_bmad-output/implementation-artifacts/sprint-status.yaml` lines 416–421 and `_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-20.md` line 40). **Fix** — Mark this integration clause `unverified` under FR-Shell/NFR3, preserve the component evidence, and reference `audit-standalone-token-relay` until its owner records the qualifying run.
- **[medium] NFR9 specifies the wrong target framework** (Build and Quality Gates, lines 211–216). “The work stays on .NET 10” conflicts with SDK 11.0.100 in `global.json` line 3, the `net11.0` target in `Directory.Build.props` line 9 and the ASP.NET 11 UI container in `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` line 11. A maintainer following this criterion could reject the current build. **Fix** — Identify .NET 10 as the original MVP baseline if that history matters, and cite the tracked SDK, target framework and container as the current qualification baseline.

## Scope honesty — adequate

The PRD distinguishes waived UI thresholds from service benchmarks and protects regulated-data go-live with an explicit open gate. Its historical status snapshot is dated. The later maintenance summary reads as current, which matters when deciding who still owns the blocked work.

### Findings

- **[medium] Epic 8 closure is missing from the maintenance summary** (Current Implementation Evidence, lines 378–386 and 401–405). The prose after the dated snapshot labels Epic 8 “in-progress.” The live tracker records Epic 8 and Story 8.10 as done while 8.7, 8.8 and 8.9 remain blocked (`_bmad-output/implementation-artifacts/sprint-status.yaml` lines 128–144). The retrospective documents closure with explicit deferrals (`_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md` lines 161–171). **Fix** — Use a single dated historical snapshot or a live tracker pointer throughout. Preserve the three blocked stories when recording administrative closure.

## Downstream usability — thin

The four tables give each requirement a stable row. Release tooling can extract all 35 identities, but several evidence references would send it to the wrong workflow, a deleted test or an inaccurate coverage status.

### Findings

- **[high] The CI and browser evidence map names retired or incorrect gates** (verification conventions and NFR1/NFR9, lines 213–222, 304–310, 334 and 342). The PRD names `domain-ci.yml@main` and calls Playwright “local-only.” The actual caller pins `domain-ci-v2.yml` and its `ui-a11y` job runs `npm run test:ui` and uploads `ui-a11y-results` (`.github/workflows/ci.yml` lines 20–32, 353–363 and 388–401). The script lists five browser specs, including the accessibility specimen (`tests/e2e/package.json` line 11). Commit `0f261ccd` also removed `PartiesContainerPublishWorkflowTests.cs`, still cited by NFR9. **Fix** — Trace CI rows to the pinned caller and actual job or artifact, remove the deleted test, and distinguish the five hosted specs from specs that require the full local suite. Associate a passing result only with an identified run.
- **[high] The named requirement-freeze test was deleted** (Source Artifacts and Document Control, lines 30–32 and 483–488). The PRD says `EpicEightAddsNoPrdFunctionalRequirement` “freezes” the FR/NFR heading inventory and the epic file. Commit `0f261ccd` deleted `tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs`. At the reviewed revision the heading sequences still match and both epic blobs are `f418a181`. That comparison establishes current agreement only. **Fix** — Cite the current owner and enforcement mechanism if one exists. Otherwise record the historical test and the explicit baseline comparison a readiness run must perform.
- **[medium] Responsive and accessibility evidence is understated** (NFR5 and UX-DR12, lines 280–282, 338 and 359). The NFR5 row says “no automated reflow” assertion. Admin tests check 320-pixel layout, zoom, no horizontal overflow and full-screen sheet behavior (`tests/e2e/specs/admin-parties-list.spec.ts` lines 753–788). A Consumer test checks overflow on narrow routes (`tests/e2e/specs/consumer-portal-routes.spec.ts` lines 93–115). The accessibility specimen checks visible focus in forced colors and motion suppression, beyond observing media selection (`tests/e2e/specs/parties-accessibility.spec.ts` lines 298–311 and 418–442). **Fix** — Link each covered behavior to its assertion and keep an `unverified` marker for any remaining requirement, such as an explicit Consumer single-column assertion.

## Shape fit — strong

A short scope statement, role-specific capabilities and a matrix suit this retrospective index. The linked UX experience document already maps shell and area states to user-visible behavior and requirement IDs (`_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md` lines 55–65). Additional personas or duplicated story acceptance criteria would add little to the release decision.

## Mechanical notes

- Reviewed the root repository at `787aef2deed296b48677f01c9ace2d84e3f03cd5`. The source is `_bmad-output/planning-artifacts/parties-ui-prd.md`, version 1.3.2. All PRD line numbers refer to that revision.
- All four tables together contain 35 distinct identities with one row each. They comprise nine FRs, nine NFRs, sixteen UX-DRs and GATE-KMS. The FR/NFR heading sequence matches commit `37f4ec82`, and all seven files in Source Artifacts are tracked.
- This review checked source, configuration, test assertions and recorded decisions. Hosted pass status and production behavior require their own execution evidence.
