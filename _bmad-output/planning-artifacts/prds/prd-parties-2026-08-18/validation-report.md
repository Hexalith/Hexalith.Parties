# Parties UI PRD validation

**Fair** · v1.3.2 · `787aef2dee` · 0 critical · 5 high · 4 medium · 0 low

PRD · `_bmad-output/planning-artifacts/parties-ui-prd.md`

Rubric · `.agents/skills/bmad-prd/assets/prd-validation-checklist.md`

Reviewed · 2026-11-20T12:50:11+01:00

## Overall verdict

The PRD gives maintainers a usable inventory of the Admin and Consumer requirements, and defines concrete evidence for closing the production key-custody gate. Its verification matrix still points to retired controls and omits open qualification work for erasure and signed-in access. A release reviewer needs those evidence references corrected before relying on the matrix for readiness.

The source check also found a user-visible gap. The My Consent page disables the Article 21 objection button, and its cited test only checks the label. The approved UX calls for a usable contact route. GATE-KMS correctly remains unverified pending the startup, replacement-host and independent-replica checks.

## Dimension verdicts

| Dimension | Verdict | Judgment |
| --- | --- | --- |
| Decision-readiness | Thin | GATE-KMS identifies the deployment owner, the affected data and the required startup, host-replacement and independent-replica checks (PRD lines 433–445). The November decision keeps the initial erasure worker and replica revocation separate from key custody. A reader following the FR matrix cannot see how those open decisions affect the promise to prove erasure. |
| Substance over theater | Strong | The role precedence, zero-or-multiple party-binding outcome and Consumer cancellation boundary translate into observable behaviors (lines 68–76 and 133–143). The distinction between consent and other lawful bases determines which control users see (lines 125–131). These details justify their place in a brownfield requirements index. |
| Strategic coherence | Strong | The two role-gated areas support a single purpose. The own-data boundary explains the Consumer feature set, and the Admin role covers organization-wide records within a tenant. Lines 388–415 distinguish maintenance and the external Agents contract from MVP delivery. The October scope addendum confirms that decision at `_bmad-output/planning-artifacts/implementation-readiness-scope-addendum-2026-10-09.md` lines 10–29. |
| Done-ness clarity | Thin | The functional requirements generally specify an observable outcome. Two acceptance boundaries need correction before reviewers can tell which build or deployed session satisfies them. |
| Scope honesty | Adequate | The PRD distinguishes waived UI thresholds from service benchmarks and protects regulated-data go-live with an explicit open gate. Its historical status snapshot is dated. The later maintenance summary reads as current, which matters when deciding who still owns the blocked work. |
| Downstream usability | Thin | The four tables give each requirement a stable row. Release tooling can extract all 35 identities, but several evidence references would send it to the wrong workflow, a deleted test or an inaccurate coverage status. |
| Shape fit | Strong | A short scope statement, role-specific capabilities and a matrix suit this retrospective index. The linked UX experience document already maps shell and area states to user-visible behavior and requirement IDs (`_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md` lines 55–65). Additional personas or duplicated story acceptance criteria would add little to the release decision. |

## Findings by severity

### Critical (0)

None.

### High (5)

**A consumer cannot use the Article 21 objection control** · Done-ness clarity · FR-Consumer-3 · UX-DR14

The PRD says legitimate-interest items “provide Object under Art.21.” The page renders a disabled button and the cited test checks its label. The approved UX calls for a contact route while the backend objection command is unavailable.

**Correction** · State the contact fallback in the requirement. Mark that clause unverified until a test follows the contact route or submits an objection. Keep the existing consent-toggle evidence.

[PRD](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/parties-ui-prd.md#L125) · [Rendered button](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor#L112) · [Label assertion](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyConsentPageTests.cs#L116) · [Approved fallback](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#L148)

**The erasure report can pass while end-to-end erasure remains unverified** · Decision-readiness · FR-Admin-4 · FR-Consumer-4

The browser check displays a fixture-backed verification report. The open sprint actions require one accepted request to reach key destruction, a persisted certificate and redacted reads. They also require a second replica to refuse the destroyed key. The matrix omits both qualifications.

**Correction** · Keep the request, cancellation and report-display evidence. Mark terminal erasure and cross-replica revocation unverified in the affected rows and link their tracker actions. The production key-custody gate has separate closure criteria.

[PRD](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/parties-ui-prd.md#L320) · [Report test](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts#L39) · [Erasure lifecycle](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/implementation-artifacts/sprint-status.yaml#L375) · [Replica revocation](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/implementation-artifacts/sprint-status.yaml#L404)

**Access to a user’s own data still needs a real signed-in session check** · Done-ness clarity · FR-Shell · NFR3

The cited checks exercise roles, claims, client boundaries and synthetic sessions. The tracker still calls for a read and update through the standalone host and real gateway, with session expiry and two-user isolation. A reader extracting the matrix alone misses that open qualification.

**Correction** · Keep the component coverage. Add an unverified entry for the standalone session and link the `audit-standalone-token-relay` action until the owner records the qualifying run.

[PRD](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/parties-ui-prd.md#L336) · [Browser fixture](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/tests/e2e/specs/consumer-party-binding.spec.ts#L18) · [Open session qualification](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/implementation-artifacts/sprint-status.yaml#L416)

**The CI map points to a retired workflow and misses five hosted browser specs** · Downstream usability · Traceability Matrix · NFR1 · NFR9

The PRD names `domain-ci.yml@main` and describes Playwright as local-only. The caller pins `domain-ci-v2.yml` and its `ui-a11y` job runs five Chromium specs, including accessibility, and publishes results. The three FR-Shell browser specs and the dedicated erasure report spec fall outside that command. A test class cited by NFR9 was deleted in commit `0f261ccd`.

**Correction** · Map each affected row to the caller, job, command and artifact. Identify the five hosted specs separately from the full suite. Cite a run at the reviewed commit when reporting a passing result.

[PRD](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/parties-ui-prd.md#L304) · [Pinned caller](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/.github/workflows/ci.yml#L22) · [Hosted check](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/.github/workflows/ci.yml#L393) · [Five specs](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/tests/e2e/package.json#L11)

**The named requirement-freeze test has been deleted** · Downstream usability · Source Artifacts · Document Control

The PRD says `EpicEightAddsNoPrdFunctionalRequirement` enforces the FR/NFR heading sequence and the exact contents of `epics.md`. Commit `0f261ccd` deleted the test. The current headings still match the baseline at `37f4ec82`, and the epics blob is still `f418a181`. A matching snapshot gives no evidence that the next change will be blocked.

**Correction** · Name the active enforcement mechanism if one exists. Otherwise document the owner, baseline and reproducible comparison as a manual policy check.

[PRD](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/parties-ui-prd.md#L483) · [Historical closure record](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md#L5)

### Medium (4)

**The build requirement still specifies .NET 10** · Done-ness clarity · NFR9

The PRD says the work stays on .NET 10. The current SDK pin is 11.0.100, the target framework is `net11.0`, and the UI container uses ASP.NET 11. CI separately installs a .NET 10 SDK.

**Correction** · Identify the current target and SDK from their tracked configuration. Date the .NET 10 baseline wherever its history matters.

[PRD](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/parties-ui-prd.md#L211) · [SDK](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/global.json#L3) · [Target](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/Directory.Build.props#L9) · [UI container](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj#L11)

**Destructive confirmation follows conflicting design rules** · Done-ness clarity · UX-DR6 · FR-Admin-4

The PRD carries an obsolete danger-color token. The approved design prescribes an outline trigger and a different confirmation fill. The experience specification also requires trimming, case-folding and NFKC normalization of the typed name, while the cited component test rejects case and trailing-space variants.

**Correction** · Point UX-DR6 to the effective design override. Flag the confirmation mismatch for the UX and implementation owners, and qualify the current test coverage.

[PRD](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/parties-ui-prd.md#L250) · [Color override](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#L272) · [Name confirmation rule](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#L149) · [Current assertions](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs#L246)

**Responsive and accessibility rows understate existing coverage** · Downstream usability · NFR5 · UX-DR12

The matrix says reflow has no automated assertion. Hosted browser cases check Admin layout at 320 pixels and 200% zoom, focus restoration, and Consumer overflow at 390 pixels. The accessibility specimen also asserts visible focus in forced colors and reduced-motion behavior. The Consumer single-column requirement still lacks a direct assertion.

**Correction** · Map each covered behavior to its browser assertion. Keep the unverified marker on the remaining single-column clause.

[PRD](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/parties-ui-prd.md#L338) · [Admin reflow](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/tests/e2e/specs/admin-parties-list.spec.ts#L753) · [Consumer reflow](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/tests/e2e/specs/consumer-portal-routes.spec.ts#L94) · [Media and focus](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/tests/e2e/specs/parties-accessibility.spec.ts#L418)

**The maintenance summary still calls Epic 8 in progress** · Scope honesty · Current Implementation Evidence

The following maintenance summary repeats an in-progress status after a dated September snapshot. The live tracker marks Epic 8, Story 8.10 and the retrospective done. Stories 8.7, 8.8 and 8.9 remain blocked under approved deferrals.

**Correction** · Point the summary to the live tracker or date it and describe the administrative closure with the three deferred stories.

[PRD](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/planning-artifacts/parties-ui-prd.md#L401) · [Sprint tracker](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/implementation-artifacts/sprint-status.yaml#L129) · [Closure and deferrals](https://github.com/Hexalith/Hexalith.Parties/blob/787aef2deed296b48677f01c9ace2d84e3f03cd5/_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md#L161)

### Low (0)

None.

## Verification notes

- All 35 matrix identities occur once across the four tables. They comprise nine functional requirements, nine non-functional requirements, sixteen UX requirements and one deployment gate. All seven original source-artifact entries resolve in the repository.
- GATE-KMS agrees with the November sprint decision. Before real regulated EU data goes live, the deployment owner must demonstrate that startup refuses ephemeral custody outside development and that a durable provider can decrypt and replay a synthetic protected record after host replacement and on an independent replica.
- The review checked source, test assertions, workflow definitions, Git history and sprint records at the recorded commit. Hosted test pass status and deployed behavior were outside this document review. Findings describe gaps in the evidence a readiness reader can use.
- Counts merge overlapping findings from the two reviews. Erasure and signed-in access have separate owners and acceptance checks.

## Reviewer files

- [Rubric review](review-rubric.md)
- [Source-fidelity review](review-source-fidelity.md)
