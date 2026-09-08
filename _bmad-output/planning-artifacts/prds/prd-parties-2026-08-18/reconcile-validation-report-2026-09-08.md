# Reconcile — Validation Report (2026-09-08) vs Parties UI PRD v1.3.0

- **Change signal:** `_bmad-output/planning-artifacts/prds/prd-parties-2026-08-18/validation-report.md` (run 2026-09-08T11:30:57+02:00, grade Fair; 7 High / 8 Medium / 3 Low)
- **Target:** `_bmad-output/planning-artifacts/parties-ui-prd.md` (`version: 1.3.0`, `last_updated: 2026-09-08`)
- **Prior version:** v1.2.0 (scratchpad copy `parties-ui-prd.v1.2.0.md`)
- **Decision log:** `_bmad-output/planning-artifacts/prds/prd-parties-2026-08-18/.memlog.md` (entries dated 2026-09-08)
- **Reviewer:** reconcile subagent, read-only against the working tree at HEAD `db478686` (PRD line numbers below are those of the working-tree file)

## Summary

| Disposition | Count |
|---|---|
| addressed | 15 |
| partially addressed (deliberate, per memlog) | 3 |
| partially addressed (gap) | 0 |
| not addressed | 0 |

All three deliberate partials are recorded as `(decision)` entries in `.memlog.md` dated 2026-09-08 (identity-only canonicity, no ACs/thresholds, freeze cannot see UX-DR/GATE IDs).

Factual spot-check: no nonexistent test class, spec file, or CI job; no UX-DR contradiction; roster matches `sprint-status.yaml`; heading inventory exact. Two attribution imprecisions are listed under Spot-check 1 and 2 (not existence errors).

## Findings disposition

### High

| # | Finding (report) | Disposition | PRD evidence (line: quote) |
|---|---|---|---|
| H1 | NFR "Verified By" cites a CI topology this repository does not run (`lint`/`test`/`ui-a11y`/`contract-test`/`report`) | addressed | L253-259: "CI jobs are those of `.github/workflows/ci.yml`, which delegates to `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main` and runs three jobs — `build-and-test` (Tier 1 … Tier 2 …), `aspire-tests` (Tier 3, `tests/Hexalith.Parties.IntegrationTests`), and `performance-tests`. The Playwright lane (`tests/e2e`, `npm run test:a11y`) is local-only and is not a CI job." L289 NFR9 cell: "`ci.yml` → `domain-ci.yml@main` jobs `build-and-test`, `aspire-tests`, `performance-tests`; … `rc-gate.yml` (`gitlink-rc-gate`); Playwright lane local-only". L281 NFR1 cell: "Playwright axe gate `parties-accessibility.spec.ts` …: local lane only, not a CI job". L213-215 NFR9 body: "Playwright accessibility checks (a local lane under `tests/e2e`; not a CI job today)". The five false job names no longer appear anywhere in the file. |
| H2 | Story 8.9 stated `backlog` but is `blocked` | addressed | L328-333: "As of `sprint-status.yaml` `last_updated: 2026-09-07`, … 8.7, 8.8, and 8.9 `blocked` — 8.9 was moved from `backlog` to `blocked` by `sprint-change-proposal-2026-09-06-sprint-status-key-freeze-and-blocked-status.md`; 8.10 in `review`". L322-326: "`sprint-status.yaml` is the only story-status source. The roster below is a dated snapshot for readers; a readiness run must read the live file and must not extract story status from this paragraph." |
| H3 | Identity-only canonicity is a dodge, not a fix (Purpose vs Source Artifacts vs Document Control) | partially addressed (deliberate, per memlog) | L15-20 Purpose rewritten: "This PRD is the canonical requirements index … It is a readiness index, not the acceptance-evidence source. … readiness tooling must read both and must not report a requirement as verified from this file alone." L31-32: "`epics.md` — frozen byte-for-byte at commit `37f4ec82`". L39-41: "Source pinning: `epics.md` is pinned as above. The other artifacts are tracked in this repository; a readiness run must record the commit it read them at". Memlog 2026-09-08 `(decision)`: "prior authority-model decision kept … Purpose rewritten to say readiness index not evidence source … epics.md pinned to 37f4ec8, other sources tracked-in-repo with record-the-commit rule (partial pin)". Neither of the report's two alternatives (make this file win, or rename it) was taken; the overclaim in the opening was removed and pinning is partial. |
| H4 | Readiness contract extracts from a matrix that omits UX-DRs and the KMS gate | addressed | L291-312 new table "UX design requirements (identity defined in `epics.md`; owning requirement here)" with UX-DR1..UX-DR16 rows. L314-318 new table "Deployment gates … \| GATE-KMS \| Deployment/platform owner \| Go-live with real regulated EU personal data \| `unverified` — documentary only". L383-388 readiness contract: "readiness tooling extracts, from the Traceability Matrix only, FR identity … (functional table), NFR identity … (NFR table), UX-DR identity with owning requirement and verification (UX-DR table), and deployment gates (gates table)." |
| H5 | No acceptance criteria, no NFR thresholds, no waived-category list | partially addressed (deliberate, per memlog) | Waived list added, L217-225: "**Waived NFR categories.** This PRD sets no UI performance, latency, availability, or browser-support threshold for the MVP. Reason: … Quantified thresholds for NFR2–NFR9 are likewise not set here; they live with the story records per the Source Artifacts precedence rule. Both waivers are revisited if this PRD becomes the chain-top requirements document again." The "stop claiming this file is the readiness source for substance" branch is taken at L17-20. Per-FR ACs and quantified NFR thresholds remain absent; memlog `(decision)`: "prior deferral kept (ACs and thresholds stay with story records per precedence rule); added explicit waived-categories paragraph". NFR2 "first-class" (L159) still has no bound. |
| H6 | Party picker is a delivered MVP surface with no FR row | addressed | L95-97 FR-Admin-3: "the form embeds the in-form `<hexalith-party-picker>` to link a related party under the UX-DR7 WAI-ARIA combobox contract (delivered by Story 2.5)". L267 matrix: "FR-Admin-3 \| Epic 2 \| 2.4, 2.5 \| `/admin/parties/new`, `/admin/parties/{id}/edit`, in-form `<hexalith-party-picker>` host \| … `PartyFormPickerBridgeTests` …, `PartyPickerComponentTests` (`Hexalith.Parties.Picker.Tests`); e2e `party-picker.spec.ts`". L302 UX-DR7 row: "Owning Requirement NFR7, FR-Admin-3". |
| H7 | KMS is still a paragraph, not a fail-closed gate | addressed | L358-368: "**GATE-KMS — Production KMS provisioning.** … Enforcement: documentary only — no production-environment rejection guard and no startup dev-only warning exist in code (`docs/architecture.md` §8), so the gate is `unverified` until a fail-closed check ships or a production key backend is registered. … Crypto/key-management extraction is Story 8.7 (`blocked`)." L316-318 matrix row. L375-376: "Production KMS provisioning is not out of scope: it is GATE-KMS above" (removed from the Out of MVP bullet list). L380-382 ID convention: "deployment gates use `GATE-<name>` (GATE-KMS)". |

### Medium

| # | Finding (report) | Disposition | PRD evidence (line: quote) |
|---|---|---|---|
| M1 | NFR1 scopes AA to Consumer-facing while UX-DRs and Admin GDPR are product-wide | addressed | L149-152: "All product surfaces — Admin and Consumer — target WCAG 2.2 AA. The Consumer area is the phone-first priority, but no Admin surface is excluded: the typed-name erasure confirmation, the party-picker combobox, the admin data grid, and the phone detail sheet are in scope." L281 NFR1 cell names `PartyPickerComponentTests` combobox contract and `PartiesAdminPortalComponentTests` typed-name confirmation. |
| M2 | NFR7 body ("UX-DR1 through UX-DR7") disagrees with matrix cell ("UX-DR1–UX-DR3") | addressed | L201: "the agreed domain deltas (UX-DR1 through UX-DR7, recorded in `epics.md`)". L287: "NFR7 \| Epics 1–5 (UX-DR1–UX-DR7)". |
| M3 | UX-DR one-liners drop criteria `epics.md` still gates (UX-DR8 assertive half, UX-DR5 degraded, UX-DR12 live gap, UX-DR6 outline split) | addressed | L229-232: "each line reproduces the `epics.md` MUST clause … including the current-gap clauses `epics.md` records". UX-DR5 L243-245: "degraded (\"showing last known\")". UX-DR6 L246-248: "restrict and withdraw use Outline, not danger fill; never auto-fire on focus or blur". UX-DR8 L254-257: "validation-rejected, transient, and load-failure announce via `role=alert` (assertive); never blanket-polite". UX-DR12 L277-279: "Current-gap clause from `epics.md`: at planning time only the picker honored them". See Spot-check 4 for the fidelity comparison. |
| M4 | Epic 8 inventory freeze cannot see UX-DRs, Deployment Gates, or the NFR table | partially addressed (deliberate, per memlog) | L392-397: "Freeze scope: `EpicEightClosureFitnessTests.EpicEightAddsNoPrdFunctionalRequirement` freezes the `### FR-*` and `### NFR<n>` heading inventory of this file against commit `37f4ec82` and freezes `epics.md` byte-for-byte. UX-DR and GATE identities, the UX Requirements list, and the matrix tables are not freeze-protected; extending the freeze to them is deferred to the Epic 8 closure owner (Story 8.10 follow-up)." The report's first option (extract UX-DR/GATE IDs in the test) was not taken; memlog `(decision)`: "no test change in this PRD update (Story 8.10 in review; fitness test is Epic 8 code scope)". The second option ("stop claiming the freeze protects readiness extractability") is taken verbatim. Confirmed against `tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:496-500` (`ExtractRequirementIds` regex `^###\s+(FR-[A-Za-z]+(?:-\d+)?\|NFR\d+)`). |
| M5 | NFR "Verified By" cells NFR2–NFR8 unexecutable | addressed | Every NFR cell now names test classes or an explicit token, e.g. L283 NFR2: "`OptimisticReconcileTests`, `ProjectionFreshnessCompositionTests`, `ProjectionFreshnessFallbackTests`, `DegradedResponseHeaderHandlerTests`, `PartiesProjectionSubscriptionTests` (SignalR subscribe/reconnect), `DataFreshnessIndicatorTests` … `ProjectionFreshnessAndDegradationTests`"; L285 NFR5: "`unverified` (no automated reflow or single-column assertion …)"; L288 NFR8: "UI-host telemetry and health endpoint: `unverified` by a UI-host-specific automated test". L259: "`unverified` marks a clause with no automated check; it is not a pass." |
| M6 | FR rows have no verification column | addressed | L263: "\| Requirement \| Primary Epic \| Stories \| Primary Surfaces \| Verified By \|" and nine populated rows L265-273 (e.g. FR-Shell: "`PartiesUiOidcConfigurationTests`, `RoleLandingRedirectTests`, `PartiesUiNavEntryGatingTests`, `PartiesUiAreaAuthorizationTests`, `PartyIdClaimResolverTests`, `NoPartyBindingRoutingTests` … e2e `admin-area-authorization.spec.ts`, `shared-role-policy-authorization.spec.ts`, `consumer-party-binding.spec.ts`"). |
| M7 | Live freshness (SignalR) absent from NFR2 | addressed | L163-168: "Live freshness is delivered by subscribing to EventStore projection updates over SignalR (`Hexalith.EventStore.SignalR`); when the stream is degraded the UI falls back to polling plus freshness metadata, surfaces `X-Service-Degraded` and `X-Stale-Data-Age` into UI state, and re-subscribes on reconnect without duplicate application (AR-D6 and Story 1.7 in `epics.md`)." Matches `epics.md:184-186` (AR-D6) and `epics.md:637-653` (Story 1.7 ACs). |
| M8 | Implementation snapshot frozen on purpose; treat as living pointer to `sprint-status.yaml` | addressed | L36-37 Source Artifacts: "`_bmad-output/implementation-artifacts/sprint-status.yaml` — the only story-status source". L322-326: "The roster below is a dated snapshot for readers; a readiness run must read the live file and must not extract story status from this paragraph." Zero-new-FR rule retained at L340-342. |

### Low

| # | Finding (report) | Disposition | PRD evidence (line: quote) |
|---|---|---|---|
| L1 | Residual adverbial copy requirements ("honestly", "plain, honest") | addressed | L127-128 FR-Consumer-3: "Bound Consumers can grant and withdraw consent under the lawful-basis rules of UX-DR14." L141-144 FR-Consumer-4: "Copy follows UX-DR13 (commit to the start, permanent once complete), UX-DR15 (no time promise), and UX-DR16 (plain verbs, single status source), and carries no hard timing promise the system cannot guarantee." Neither adverb remains in the file. |
| L2 | UX-DR one-liners drop definitional halves (UX-DR5/9/10/13/16) | addressed | UX-DR9 L258-264: "typed-erase confirmation is a real labeled `<input>` with `aria-describedby` to the irreversibility warning and Erase `aria-disabled` until the name matches (transition announced)". UX-DR10 L265-270: "trap and restore on dialogs; move focus to the alert on blocking errors; announce via aria-live without stealing focus on routine optimistic saves". UX-DR13 L280-284: "state both halves — cancellable until deletion begins, permanent once complete; the 30-day figure is not the cancel window; the acknowledgement uses neutral/info tone, never success-green". UX-DR16 L319-322: "one status source per action (never \"Saved\" and \"Saving\" together); rendered value identical to stored value across view and edit". UX-DR5 covered under M3. |
| L3 | Admin route templates `{id}` vs `@page` parameter `{RoutePartyId}` | addressed | L275-277: "Route note: the matrix writes Admin route parameters as `{id}` to match `epics.md`; the Blazor `@page` templates in `Hexalith.Parties.AdminPortal` name the parameter `{RoutePartyId}`. The paths are identical." Confirmed: `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor:2` and `PartiesAdminPortal.razor:2-3`. |

## Spot-check results (read-only, against the working tree)

### 1. Test classes cited in `Verified By` cells

Method: every backticked identifier ending in `Tests` inside `## Traceability Matrix` was grepped as `class <Name>` under `tests/` excluding `bin/` and `obj/`.

- **54 distinct class names cited; 54 found. Nonexistent: none.**
- Method reference `PartiesContainerPublishWorkflowTests.CiWorkflowDelegatesToSharedDomainCiWithPartiesTestLanes` exists at `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs:43`.
- All nine cited test project directories exist under `tests/` (`AdminPortal.Tests`, `Ci.Tests`, `Client.Tests`, `ConsumerPortal.Tests`, `Contracts.Tests`, `Picker.Tests`, `Tests`, `UI.Tests`, `IntegrationTests`).
- Content claims sampled and confirmed: typed-name confirmation in `PartiesAdminPortalComponentTests` (51 hits); `StatusKind.TenantUnavailable` in `StatusPresentationTests` (5) and `OptimisticReconcileTests` (2); reconnect/resubscribe in `PartiesProjectionSubscriptionTests` (3); `ServiceDefaults` in `RetiredLeafProjectFitnessTests` (12); tombstone/erased in `PartyStateBadgeTests` (16); combobox in `PartyPickerComponentTests` (3).

Attribution imprecisions (class exists, project label wrong or missing):

- PRD L268 (FR-Admin-4 cell) groups `AdminPortalGdprOperationContractTests` under "(`Hexalith.Parties.Contracts.Tests`)"; the class lives in `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs` (Tier 1 either way).
- PRD L273 (FR-Consumer-4 cell) cites `PartyExportFileNameTests` with no project; it is `tests/Hexalith.Parties.Contracts.Tests/PartyExportFileNameTests.cs`.

### 2. e2e spec files cited

- **9 distinct specs cited; 9 exist under `tests/e2e/specs/`. Nonexistent: none.** (`admin-area-authorization`, `admin-gdpr-erasure-verification`, `admin-parties-list`, `consumer-party-binding`, `consumer-portal-routes`, `parties-accessibility`, `party-picker`, `shared-portal-display-formatters`, `shared-role-policy-authorization`.)
- Quoted test titles exist in `tests/e2e/specs/parties-accessibility.spec.ts`: "filled primary button does not compute to raw teal" (L95), "forced-colors and reduced-motion media are observable" (L74), "skip links are the first two focusable elements in the shell …" (L47).

Attribution imprecisions (file exists, claim is loose):

- PRD L281 (NFR1 cell) lists `admin-gdpr-erasure-verification.spec.ts` alongside the axe gate as accessibility evidence; that spec's two tests (L22, L70) cover erasure-certificate contract modes and contain no axe or typed-name assertion. It is correctly cited for FR-Admin-4 (L268).
- PRD L257-258 names the Playwright lane as "`tests/e2e`, `npm run test:a11y`"; `tests/e2e/package.json:9` scopes `test:a11y` to `specs/parties-accessibility.spec.ts` only. The other eight cited specs run under `npm test` (`playwright test`). Still local-only; the "not a CI job" statement holds for both.

### 3. CI job names

| Cited | Found at |
|---|---|
| `build-and-test` | `references/Hexalith.Builds/.github/workflows/domain-ci.yml:207` |
| `aspire-tests` | `references/Hexalith.Builds/.github/workflows/domain-ci.yml:642` |
| `performance-tests` | `references/Hexalith.Builds/.github/workflows/domain-ci.yml:718` |
| `gitlink-rc-gate` | `.github/workflows/rc-gate.yml:32` |

- `.github/workflows/ci.yml:21-22` has one caller job `ci` that `uses: Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main`; the PRD's "delegates to … and runs three jobs" wording is accurate.
- Tier mapping in the PRD matches `ci.yml`: `unit-test-projects` (Tier 1) includes `Contracts.Tests`, `Client.Tests`, `AdminPortal.Tests`, `ConsumerPortal.Tests`, `UI.Tests`, `Picker.Tests`; `integration-test-projects` (Tier 2) is `Hexalith.Parties.Tests`, `Sample.Tests`, `Ci.Tests`; `aspire-test-project` is `tests/Hexalith.Parties.IntegrationTests` (where `HealthEndpointE2ETests` lives, as the NFR8 cell says).
- `commitlint.yml`, `codeql.yml`, `dependency-review.yml`, `rc-gate.yml` (all cited in NFR9) exist in `.github/workflows/`.
- None of the v1.2.0 phantom job names (`lint`, `test`, `ui-a11y`, `contract-test`, `report`) appear in v1.3.0.

### 4. UX-DR1..16 vs `epics.md` lines 263-340

- **Contradictions: none.** Every MUST clause in each `epics.md` bullet is present in the PRD bullet, including the current-gap and definitional halves the report listed (UX-DR5 degraded, UX-DR6 Outline split, UX-DR8 assertive half, UX-DR9 typed-erase input, UX-DR12 picker-only gap, UX-DR13 both halves, UX-DR16 single status source and view/edit parity).
- **Clause the PRD states that `epics.md` does not:** UX-DR12, PRD L278-279 — "the Playwright lane checks that both media are observable in the shell". `epics.md:320-321` says only "(today only the picker honors them)". The added clause is true of the repository (`parties-accessibility.spec.ts:74`) but is a verification note, not an `epics.md` requirement; it should be read as PRD commentary, not UX-DR12 identity.
- **Material MUST clauses missing: none.** Non-normative parentheticals dropped by the PRD (informational, not gating): UX-DR1 "≈`#00767f`" and "(Resolves the screen-wide 1.4.3 critical.)"; UX-DR2 "warning-on-arbitrary-tint lands ~4.44:1"; UX-DR3 "`--fontSizeBase400`"; UX-DR8 "(Wired via AR-StatusMap.)"; UX-DR10 "incl. the formerly-fake ones"; UX-DR11 "(border-weight/checkmark)" and "3.51:1"; UX-DR14 "(privacy card is a summary …)" compressed to "with withdraw/grant parity"; UX-DR16 "(name the right nearby)".
- Observation (not a UX-DR bullet contradiction): `epics.md:294` groups UX-DR8–12 under the heading "Accessibility implementation (consumer-facing WCAG 2.2 AA)" while PRD NFR1 (L149) is product-wide. UX-DR10 and UX-DR12 themselves say "product-wide", so the PRD's NFR1 scope agrees with the bullets; only the `epics.md` group heading is narrower.

### 5. Current Implementation Evidence roster vs `sprint-status.yaml` Epic 8 rows

`sprint-status.yaml` `last_updated: 2026-09-07` (L2, L48). Epic 8 rows (L147-274):

| Key | sprint-status | PRD L328-333 | Match |
|---|---|---|---|
| `epic-8` | in-progress | in-progress | yes |
| 8-1 … 8-6 | done | 8.1-8.6 done | yes |
| 8-7 | blocked | blocked | yes |
| 8-8 | blocked | blocked | yes |
| 8-9 | blocked | blocked (moved from backlog by the 2026-09-06 SCP) | yes |
| 8-10 | review | review | yes |
| 8-11 … 8-13 | done | 8.11-8.13 done | yes |

- Epics 1–7 all `done` (L56, L70, L79, L89, L98, L112, L127) — matches PRD L328-329.
- Cited SCP exists: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-06-sprint-status-key-freeze-and-blocked-status.md`. The `sprint-status.yaml` comment block at L206-220 corroborates "Stories 8.7/8.8/8.9 remain blocked; Story 8.10 remains review".
- Other cited SCPs exist: `sprint-change-proposal-2026-07-06.md`, `…-2026-07-16-epics-7-8-maintenance-scope.md`, `…-2026-08-01-projection-rollback-retention-revalidation.md`.
- Story references confirmed `done`: `1-7-live-freshness-via-signalr-…` (L63), `2-5-party-picker-re-skin-…` (L74).
- **Roster mismatches: none.**

### 6. `###` heading inventory

`grep -nE '^### '` over the PRD returns exactly 18 headings, in this order, and no other `###` heading of any kind:

```
L66  ### FR-Shell
L78  ### FR-Admin-1: Parties List
L85  ### FR-Admin-2: Party Detail
L91  ### FR-Admin-3: Create and Edit Party
L102 ### FR-Admin-4: GDPR Operations
L112 ### FR-Consumer-1: My Profile
L118 ### FR-Consumer-2: Edit My Profile
L125 ### FR-Consumer-3: My Consent
L133 ### FR-Consumer-4: My Data and Privacy
L147 ### NFR1: Accessibility
L157 ### NFR2: Eventual Consistency UX
L170 ### NFR3: Security and Own-Data Privacy
L179 ### NFR4: GDPR Honesty
L186 ### NFR5: Responsive Design
L192 ### NFR6: Multi-Tenancy
L198 ### NFR7: Brand Discipline
L205 ### NFR8: Observability
L211 ### NFR9: Build and Quality Gates
```

`git show 37f4ec82:_bmad-output/planning-artifacts/parties-ui-prd.md` yields the same 18 IDs in the same order, so the `EpicEightAddsNoPrdFunctionalRequirement` inventory freeze (regex at `EpicEightClosureFitnessTests.cs:496-500`) is satisfied by v1.3.0. **Heading inventory problems: none.**

### Other factual claims sampled

- GATE-KMS enforcement claim (PRD L361-364): `docs/architecture.md:192` (inside §8, L173-195) states "**no production-environment rejection guard and no startup 'dev-only' warning**" — matches. `docs/index.md:86` carries the GDPR/KMS notice; `docs/getting-started.md:438` carries the production KMS gate; `docs/deployment-security-checklist.md` does not exist (retired) — all as the PRD states.
- NFR2 SignalR claim: `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` references `Hexalith.EventStore.SignalR`; `X-Service-Degraded` / `X-Stale-Data-Age` are handled in `src/Hexalith.Parties.UI/Services/DegradedResponseHeaderHandler.cs:26`.
- Named seams/symbols exist in `src/`: `IAdminPortalGdprClient`, `IErasureVerificationService`, `StatusKind.TenantUnavailable`, `ProjectionFreshnessMetadata`, `PartyCommandValidationRejected`, `ProcessingActivityRecord`, `LawfulBasis`.
- Story numbers in matrix cells match `epics.md` titles: 1.6 (StatusKind→UI aria-live split, UX-DR8), 1.8 (shared domain components, UX-DR4-6), 1.9 (a11y foundation), 2.5 (picker, UX-DR7), 5.1/5.2/5.3 (consent / export / erasure), 3.5/3.6 (D7 backend / report UI).

## Residual items for the PRD owner (non-blocking)

1. L268: move `AdminPortalGdprOperationContractTests` out of the `Hexalith.Parties.Contracts.Tests` parenthetical (it is in `Hexalith.Parties.Client.Tests`).
2. L273: add the project for `PartyExportFileNameTests` (`Hexalith.Parties.Contracts.Tests`).
3. L281: drop `admin-gdpr-erasure-verification.spec.ts` from the NFR1 a11y evidence, or say what a11y clause it checks (it checks none).
4. L257-258: either cite `npm test` for the full local lane or note that `test:a11y` runs only `parties-accessibility.spec.ts`.
5. L278-279: mark the UX-DR12 "Playwright lane checks…" sentence as a PRD verification note rather than part of the `epics.md` clause, or move it to the UX-DR12 matrix row (which already says the same thing).
