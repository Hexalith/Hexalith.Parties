# Source review of the Parties UI PRD, v1.3.2

Reviewed the November 20 PRD at Hexalith.Parties commit `787aef2deed296b48677f01c9ace2d84e3f03cd5`. Source locations below refer to that commit. This review inspected tracked documents, tests, product code, workflow definitions and Git history. Test names identify the scope of an assertion. A passing hosted run or a deployed qualification requires its own evidence.

## Verdict

The requirement inventory is usable. Several verification entries could lead a reader to accept an unavailable privacy action or overlook qualification that is still open. The November update describes the production key-custody decision accurately. Four high and four medium findings affect the index or its use.

## High findings

### 1. The Article 21 objection action is disabled

FR-Consumer-3 says legitimate-interest items “provide Object under Art.21.” UX-DR14 points to `MyConsentPageTests`. The test checks the words “Object (Art. 21).” The actual control sets both `aria-disabled="true"` and `Disabled="true"`. The UX owner specifies a contact route while the objection command is unavailable. A person cannot act through this control. [PRD lines 125–131, 288–292, 361] · `src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor:112` · `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyConsentPageTests.cs:116` · `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md:148`

**Suggested correction.** Record the contact fallback under FR-Consumer-3 and UX-DR14. Mark the objection clause `unverified` until a test follows a usable contact route or submits an objection. Keep existing evidence for consent toggles.

### 2. Erasure and own-data rows omit open end-to-end qualifications

The FR-Admin-4, FR-Consumer-4 and NFR3 entries name certificate, client, component and browser checks. For example, the certificate test opens an existing retry fixture. The Consumer binding check selects a synthetic bound or unbound session. The November tracker still requires evidence that an initial request reaches committed destruction and redacted reads, that another replica refuses a destroyed key, and that a real signed-in gateway read and update preserve session isolation. A readiness reader extracting only the four tables cannot discover those limits from the affected rows. [PRD lines 320, 324, 336, 443–445, 473–480] · `tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts:23` · `tests/e2e/specs/consumer-party-binding.spec.ts:18` · `_bmad-output/implementation-artifacts/sprint-status.yaml:375`, `:404`, `:416`

**Suggested correction.** Preserve evidence for UI behavior and cite the applicable open tracker actions in the corresponding rows. Mark terminal erasure, cross-replica revocation and real-session relay separately as `unverified` pending the owners’ results. These checks are separate from GATE-KMS closure. [`_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-20.md:37`–40]

### 3. The matrix sends readers to a retired CI workflow and misses the hosted browser gate

The PRD says CI “runs three jobs” through `domain-ci.yml@main` and calls Playwright “local-only.” The current caller uses `domain-ci-v2.yml@fe68718b…`, supplies the `platform-package` profile, and defines a `ui-a11y` job which runs `npm run test:ui` and uploads `ui-a11y-results`. Five Chromium specs run there, including accessibility, the Admin list, Consumer routes and the picker. The three FR-Shell browser specs and the dedicated erasure-verification spec fall outside that command. Git commit `0f261ccd` also deleted the NFR9 verifier `PartiesContainerPublishWorkflowTests`. [PRD lines 213–221, 304–310, 316, 334, 341–342, 359] · `.github/workflows/ci.yml:22`, `:27`, `:353`, `:393`, `:398` · `tests/e2e/package.json:11` · `tests/Hexalith.Parties.Ci.Tests/BrowserProductTests.cs:11`

**Suggested correction.** Map each active check to the current pinned workflow, job and command. List the five CI browser specs and label the other specs according to their actual execution lane. Replace or remove the retired verifier.

### 4. The current freeze claim cites a deleted test

Document Control says `EpicEightAddsNoPrdFunctionalRequirement` “freezes” the PRD headings and the bytes of `epics.md`. Commit `0f261ccd` deleted `tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs`. No current occurrence of the named test was found in tests or scripts. The epics blob at `37f4ec82` and at the reviewed commit is identical (`f418a181`). Historical agreement supports the baseline comparison but supplies no evidence of ongoing enforcement by that test. [PRD lines 31–32, 483–488] · `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md:5` · Git deletion `0f261ccd`

**Suggested correction.** Retain the baseline and date of the check. Name a current enforcing mechanism or describe the freeze as a policy checked manually, with the owner and comparison procedure.

## Medium findings

### 5. NFR9 requires an obsolete runtime

“The work stays on .NET 10” conflicts with the SDK pin `11.0.100`, root target `net11.0`, and UI container base `aspnet:11.0-alpine-extra`. CI also installs a 10.0 SDK as an additional tool. An engineer following NFR9 could choose the wrong runtime when qualifying the UI. [PRD lines 211–216] · `global.json:3` · `Directory.Build.props:9` · `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj:11` · `.github/workflows/ci.yml:25`

**Suggested correction.** Record the governed move to .NET 11 and link the SDK and host target. Label the .NET 10 value in the frozen epic inventory as historical.

### 6. UX-DR6 points to superseded destructive-confirmation rules

The extractable inventory instructs readers to use the `--colorStatusDangerForeground1` fill and the matrix cites a confirmation test. The owning design explicitly supersedes that text token, specifies `--colorStatusDangerBackground3` for the dialog confirmation, and uses an outline trigger. The owning experience requires trimming, case-folding and NFKC for Admin name confirmation. The current Admin component test requires “exact candidate” and “Exact Candidate ” to be rejected. A passing check can therefore support a different interaction from the one UX approved. [PRD lines 46–49, 250–252, 353, 356] · `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md:272`–283 · `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md:149` · `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs:246` · `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor:305`

**Suggested correction.** Keep the frozen UX-DR identifier. Point its effective semantics to the design override, show the gap in the existing behavior check, and route the conflicting confirmation assertions to the UX and implementation owners.

### 7. NFR5 understates responsive-layout coverage

The row says “no automated reflow or single-column assertion.” The hosted Admin spec checks a 320-pixel form at 200% zoom for horizontal overflow and checks detail-sheet focus restoration. The Consumer spec checks overflow and keyboard interaction at 390 pixels. Those assertions cover part of the requirement. Neither demonstrates single-column behavior across all supported widths. [PRD lines 186–190, 338, 357] · `tests/e2e/specs/admin-parties-list.spec.ts:753`, `:768` · `tests/e2e/specs/consumer-portal-routes.spec.ts:94`, `:115` · `tests/e2e/package.json:11`

**Suggested correction.** Name the Admin and Consumer browser cases. Keep the `unverified` token on the single-column clause until a matching assertion exists.

### 8. The maintenance summary still describes Epic 8 as in progress

The September roster is clearly dated. The following maintenance summary also labels Epic 8 “in-progress,” while the live tracker marks Epic 8, Story 8.10 and the retrospective done. Stories 8.7–8.9 remain blocked under the approved deferrals. The aggregate status can be corrected without treating the three stories as complete. [PRD lines 373–386, 401–405] · `_bmad-output/implementation-artifacts/sprint-status.yaml:48`, `:129`, `:136`–144 · `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md:31`

**Suggested correction.** Remove the unqualified status from the maintenance summary and refer to the tracker, or date the summary and report the aggregate closure with the three deferrals.

## Verified source agreements

- GATE-KMS is correctly marked `unverified`. The PRD requires both startup refusal of ephemeral custody outside development and durable replay after host replacement and from a second replica. The November proposal and open custody action require the same evidence. Erasure and revoked-key behavior have their own actions. The default registration currently selects `LocalDevKeyStorageBackend`. [PRD lines 369, 433–448] · `_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-20.md:23`, `:37`–39 · `_bmad-output/implementation-artifacts/sprint-status.yaml:398`–409 · `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:161` · `docs/architecture.md:293`
- The stable inventory contains nine FRs, nine NFRs, sixteen UX-DRs and one deployment gate. The picker remains in FR-Admin-3. Epic 8 closure and its accepted deferrals add no MVP FRs. The September reconciliation records the owners’ decision to keep acceptance evidence in story and test records. [PRD lines 96–100, 314–369, 388–390] · `_bmad-output/planning-artifacts/epics.md:95`, `:289` · `_bmad-output/implementation-artifacts/epic-8-retro-2026-10-09.md:13`
