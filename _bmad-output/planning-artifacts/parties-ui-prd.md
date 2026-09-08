---
project_name: parties
document_type: prd
status: canonical-requirements-source
date: 2026-06-27
last_updated: 2026-09-08
version: 1.3.0
requirements_basis: "Brownfield docs + final UX design set + architecture requirements inventory + epics FR map"
---

# Parties UI PRD

## Purpose

This PRD is the canonical requirements index for the `parties-ui` initiative:
the source of truth for which requirements exist, their stable IDs, and what is
in or out of scope. It is a readiness index, not the acceptance-evidence source.
Acceptance evidence for each requirement lives in the implementation story
records and the tests named in the Traceability Matrix; readiness tooling must
read both and must not report a requirement as verified from this file alone.

The project is brownfield. The original requirements were captured in the
architecture document, the final UX design set, the existing docs baseline, and
the epics/story breakdown. This file consolidates that requirements basis so
readiness tooling can extract FR/NFR/UX-DR identity and coverage mapping without
treating the absence of a traditional PRD as a blocker.

## Source Artifacts

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epics.md` — frozen byte-for-byte at commit
  `37f4ec82` by `EpicEightClosureFitnessTests.EpicEightAddsNoPrdFunctionalRequirement`
- `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/validation-report.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — the only
  story-status source (see Current Implementation Evidence)
- `docs/index.md` and linked brownfield project documentation

Source pinning: `epics.md` is pinned as above. The other artifacts are tracked
in this repository; a readiness run must record the commit it read them at and
must not treat an untracked copy as a source.

This file is canonical for requirement identity and scope: which requirements
exist, their IDs, and what is in or out of scope. When this PRD and source
artifacts conflict on a topic's detailed semantics, the source artifact owning
the topic wins: architecture for system decisions, UX spines for product
experience, and implementation story records for completed work evidence.
UX-DR identifiers are defined in `epics.md`; the UX design set records the
resolved experience those IDs implement.

## Product Scope

Realize `parties-ui`: a single responsive Blazor Server application on
FrontComposer and FluentUI Blazor V5, with two role-gated areas:

- Admin records management and GDPR/DPO operations under `/admin/parties*`.
- Consumer own-data GDPR self-service under `/me*`.

The app extends the existing Hexalith.Parties event-sourced/CQRS service through
the EventStore gateway. The browser talks only to the UI host/BFF. The UI host
owns OIDC sign-in and keeps tokens server-side.

## Functional Requirements

### FR-Shell

Authenticate users through host-owned OIDC, preserve return URLs, and route users
to the correct area by role. Admin or TenantOwner users land in Admin; Consumer
users land in Consumer. A principal holding both Admin and Consumer roles lands
in Admin (the shell checks Admin-policy roles first); an authenticated principal
with neither role sees a fail-closed no-area state. There is no separate DPO
role: DPO duties are performed under the Admin policy. Navigation is
policy-gated so Admin and Consumer entries do not cross-render. Consumers
without exactly one verified `party_id` claim land in the fail-closed
`NoPartyBinding` state, never on a data screen.

### FR-Admin-1: Parties List

Admins can search and filter parties server-side by display name, party type, and
active state. The list supports paging, row-to-detail navigation, stale/degraded
read handling per the NFR2 freshness contract, last-known rendering, and
accessible keyboard navigation.

### FR-Admin-2: Party Detail

Admins can view the full `PartyDetail`, including lifecycle state and freshness.
The detail view provides entry points to edit and GDPR operations. Missing or
erased parties render PII-free tombstone states.

### FR-Admin-3: Create and Edit Party

Admins can create and edit Person and Organization parties through validated
forms; the validation rules are owned by the architecture's domain validation
contract (rejections surface as `PartyCommandValidationRejected`).
Person/Organization selection uses a real radiogroup, and the form embeds the
in-form `<hexalith-party-picker>` to link a related party under the UX-DR7
WAI-ARIA combobox contract (delivered by Story 2.5). Route ids are
authoritative on edit, validation errors are announced accessibly, and successful
commands use optimistic UI plus projection reconciliation.

### FR-Admin-4: GDPR Operations

DPO/Admin users can erase a party with typed-name confirmation, restrict and lift
processing restriction, record and revoke consent, export data under Art.20, view
processing records under Art.30, and prove erasure with a verification report
bounded per the D7 erasure-certificate decision in `epics.md` (delivered by
Stories 3.5 and 3.6). GDPR operations must avoid PII leakage and route through
the existing typed client/gateway seams (`IAdminPortalGdprClient`,
`IErasureVerificationService`).

### FR-Consumer-1: My Profile

Bound Consumers can view their own personal data and projection freshness. They
never see list/search surfaces. Stale/degraded reads show last-known data, and an
erased self renders a PII-free tombstone.

### FR-Consumer-2: Edit My Profile

Bound Consumers can correct their own data through validated, self-scoped update
commands (same validation ownership as FR-Admin-3). Prefilled values match
stored values, validation preserves input, and accepted commands reconcile
through the shared optimistic/freshness pattern.

### FR-Consumer-3: My Consent

Bound Consumers can grant and withdraw consent under the lawful-basis rules of
UX-DR14. Consent toggles default Off, are real switch controls, and distinguish
consent-based items from contract, legal, and legitimate-interest bases.
Legitimate-interest items provide Object under Art.21 rather than a withdraw
toggle.

### FR-Consumer-4: My Data and Privacy

Bound Consumers can export their own data as machine-readable JSON, request
erasure, and cancel a requested erasure while the erasure obligation is still
pending — cancellation is accepted until erasure processing begins and is
rejected afterwards ("deletion has already begun"). They can view what is
processed about them through the bounded Art.30 `ProcessingActivityRecord`
reads defined by the architecture's projection/query contract. Copy follows
UX-DR13 (commit to the start, permanent once complete), UX-DR15 (no time
promise), and UX-DR16 (plain verbs, single status source), and carries no hard
timing promise the system cannot guarantee.

## Non-Functional Requirements

### NFR1: Accessibility

All product surfaces — Admin and Consumer — target WCAG 2.2 AA. The Consumer
area is the phone-first priority, but no Admin surface is excluded: the
typed-name erasure confirmation, the party-picker combobox, the admin data grid,
and the phone detail sheet are in scope. Required patterns include real ARIA
semantics, correct live-region politeness split, visible focus, forced-colors and
reduced-motion support, non-color cues, keyboard operation, and target sizes of
at least 24×24 CSS px (WCAG 2.2 SC 2.5.8).

### NFR2: Eventual Consistency UX

Projection freshness is first-class. The UI renders last-known data on stale or
degraded reads, uses optimistic echo for accepted commands, reconciles on
projection confirmation, and never treats accepted commands as read-your-write.
A read counts as stale or degraded per the architecture's projection-freshness
contract (`ProjectionFreshnessMetadata` and the degraded-response middleware).
Live freshness is delivered by subscribing to EventStore projection updates over
SignalR (`Hexalith.EventStore.SignalR`); when the stream is degraded the UI
falls back to polling plus freshness metadata, surfaces `X-Service-Degraded` and
`X-Stale-Data-Age` into UI state, and re-subscribes on reconnect without
duplicate application (AR-D6 and Story 1.7 in `epics.md`).

### NFR3: Security and Own-Data Privacy

Consumer operations are own-data only. Consumer pages use the self-scoped accessor
and must not accept caller-supplied party ids. Parties-side defense-in-depth
asserts `aggregateId == party_id`; this Parties-side assertion is the implemented
enforcement today, and the deferred gateway-level data-subject/self principal
support (see Out of MVP Scope) would add an earlier enforcement seam without
replacing it. Logs, telemetry, tombstones, and error copy do not expose PII.

### NFR4: GDPR Honesty

Consent is opt-in and default Off. Erasure copy commits to starting the obligation
and states completed erasure is permanent. Export copy promises machine-readable
delivery but no fixed completion time. Legal bases are surfaced as recorded
(`LawfulBasis`), never coerced into consent toggles.

### NFR5: Responsive Design

Admin is desktop-first but reflows to sheet/full-screen detail on small screens.
Consumer is phone-first and single-column. Both areas share one responsive codebase
with different density postures.

### NFR6: Multi-Tenancy

Admin operates within tenant scope. Tenant access fails closed and may be
eventually consistent after restart. Tenant warm-up is surfaced as a distinct
temporary warm-up state, not as an access-denied error.

### NFR7: Brand Discipline

The UI inherits FrontComposer and FluentUI V5/Fluent 2. New styling is limited to
the agreed domain deltas (UX-DR1 through UX-DR7, recorded in `epics.md`). Do not
hard-code raw accent colors for text-bearing controls or redeclare Fluent tokens
in product CSS.

### NFR8: Observability

The UI host uses `Hexalith.Commons.ServiceDefaults` (the local ServiceDefaults
wrapper was retired by Story 8.4), OpenTelemetry, health checks, degraded
headers, and freshness metadata without logging personal data or event payloads.

### NFR9: Build and Quality Gates

The work stays on .NET 10, central package management, `.slnx`, warnings as
errors, xUnit v3/Shouldly/NSubstitute/bUnit, Playwright accessibility checks
(a local lane under `tests/e2e`; not a CI job today), and root-level
submodules under `references/` only.

**Waived NFR categories.** This PRD sets no UI performance, latency,
availability, or browser-support threshold for the MVP. Reason: this is a
brownfield readiness index for a Blazor Server shell over an existing service;
service-side performance benchmarks run in the `performance-tests` CI job and
are not UI requirements, and browser support follows the FluentUI Blazor V5 and
Blazor Server support matrix. Quantified thresholds for NFR2–NFR9 are likewise
not set here; they live with the story records per the Source Artifacts
precedence rule. Both waivers are revisited if this PRD becomes the chain-top
requirements document again.

## UX Requirements

The final UX design set is authoritative for the product experience; the UX-DR
identifiers below are defined in `epics.md` (Design Requirements), and each
line reproduces the `epics.md` MUST clause so that this list is a faithful
extractable inventory, including the current-gap clauses `epics.md` records:

- UX-DR1 — AA-safe brand fill: filled primary buttons (white text) bind to
  `--colorBrandBackground` (AA-safe), never the raw teal `#0097A7`; the raw
  accent is reserved for non-text use; one primary action per view.
- UX-DR2 — Status token pairs: party/GDPR/freshness state colors map to Fluent 2
  `--colorStatus*Foreground1`-on-`--colorStatus*Background1` token pairs, never
  hand-mixed hex, verified in light and dark themes.
- UX-DR3 — Inheritance discipline: no redeclaring Fluent 2 custom properties;
  theme only via `IThemeService`/token API; Consumer body 16px with line-height
  1.5; Admin comfortable and Consumer roomy density via FrontComposer.
- UX-DR4 — Party-state badge: `FluentBadge` tint pill with color plus text label
  (never color alone) for active/inactive/restricted/erased; erased shows
  tombstone copy, not data.
- UX-DR5 — Data-freshness indicator: dot plus word for fresh, stale ("as of
  HH:MM"), and degraded ("showing last known"); its text node is a `role=status
  aria-live=polite` region so transitions are announced.
- UX-DR6 — GDPR destructive button: danger fill (`--colorStatusDangerForeground1`)
  plus typed-name confirmation for irreversible actions; restrict and withdraw
  use Outline, not danger fill; never auto-fire on focus or blur.
- UX-DR7 — Party picker re-skin with the full WAI-ARIA combobox contract (folds
  AR-D11): `--hx-picker-*` on Fluent 2 tokens; input `role=combobox` with
  `aria-controls`, `aria-expanded`, and `aria-activedescendant`; `role=listbox`
  with `role=option` plus `id` and `aria-selected`; a `role=status` result
  count; 300 ms debounce; `party-selected` event; full state machine.
- UX-DR8 — Live-region politeness split: status, freshness, and
  accepted-processing announce via `role=status aria-live=polite`;
  validation-rejected, transient, and load-failure announce via `role=alert`
  (assertive); never blanket-polite.
- UX-DR9 — Real semantics, no interactive `<div>`s: consent control is a
  `FluentSwitch` (`role=switch`, `aria-checked`) with purpose and lawful basis
  tied via `aria-describedby`; Person/Organization chooser is a
  `FluentRadioGroup`; typed-erase confirmation is a real labeled `<input>` with
  `aria-describedby` to the irreversibility warning and Erase `aria-disabled`
  until the name matches (transition announced); grid rows use
  `role=row`/`gridcell`.
- UX-DR10 — Per-surface focus contract: skip links (to content, to nav) are the
  first two tab stops product-wide including Consumer; visible
  `--colorStrokeFocus2` ring on every control; trap and restore on dialogs; move
  focus to the alert on blocking errors; announce via aria-live without stealing
  focus on routine optimistic saves; the phone detail sheet moves focus in on
  open and restores it to the originating row on back.
- UX-DR11 — Non-color cues and target sizing: active/selected affordances carry
  a non-color cue, not the accent border alone; targets at least 24px with at
  least 44px touch slop for the consent toggle and icon-only controls; Consumer
  secondary text floored at 13–14px; nothing conveyed only at 12px or smaller;
  survives 200% zoom and text spacing.
- UX-DR12 — Forced-colors and reduced-motion supported product-wide. Current-gap
  clause from `epics.md`: at planning time only the picker honored them; the
  Playwright lane checks that both media are observable in the shell.
- UX-DR13 — Honest erasure copy: commit to the start ("We've started deleting
  your data… usually within 30 days… we'll confirm when it's done"), never a
  hard finish SLA; state both halves — cancellable until deletion begins,
  permanent once complete; the 30-day figure is not the cancel window; the
  acknowledgement uses neutral/info tone, never success-green.
- UX-DR14 — Lawful-basis honesty: split "Things you control" (consent toggles,
  default Off) from "Things we keep to run your account" (contract and
  legitimate interest, read-only); offer Object (Art.21) for legitimate-interest
  bases, not a withdraw toggle; "Manage all consent" links to the full
  My-consent surface with withdraw/grant parity.
- UX-DR15 — Export copy with no time promise ("Preparing your export — this can
  take a little while. We'll show it here the moment it's ready"); states
  machine-readable JSON; the synchronous download is the happy case, not the
  promised baseline.
- UX-DR16 — Plain verbs and a single status source: lead with "Delete my data",
  not "Erasure"; Admin terse and Consumer plain-and-reassuring register; one
  status source per action (never "Saved" and "Saving" together); rendered
  value identical to stored value across view and edit.

## Traceability Matrix

Verification conventions used below: CI jobs are those of `.github/workflows/ci.yml`,
which delegates to `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main`
and runs three jobs — `build-and-test` (Tier 1 unit test projects and Tier 2
integration test projects listed in `ci.yml`), `aspire-tests` (Tier 3,
`tests/Hexalith.Parties.IntegrationTests`), and `performance-tests`. The
Playwright lane (`tests/e2e`; `npm run test:a11y` runs the axe specimen, `npm test`
runs every spec) is local-only and is not a CI job. `unverified` marks a clause with no automated check; it is not a pass.

Functional requirements:

| Requirement | Primary Epic | Stories | Primary Surfaces | Verified By |
|---|---|---|---|---|
| FR-Shell | Epic 1 | 1.1–1.5 | Sign-in, role landing, navigation, NoPartyBinding | `PartiesUiOidcConfigurationTests`, `RoleLandingRedirectTests`, `PartiesUiNavEntryGatingTests`, `PartiesUiAreaAuthorizationTests`, `PartyIdClaimResolverTests`, `NoPartyBindingRoutingTests` (Tier 1, `Hexalith.Parties.UI.Tests`); e2e `admin-area-authorization.spec.ts`, `shared-role-policy-authorization.spec.ts`, `consumer-party-binding.spec.ts` (local lane) |
| FR-Admin-1 | Epic 2 | 2.1, 2.2 | `/admin/parties` | `PartiesAdminPortalComponentTests` (Tier 1, `Hexalith.Parties.AdminPortal.Tests`), `AdminPortalQueryContractTests` (`Hexalith.Parties.Client.Tests`), `MvpDisplayNameSearchContractTests` (Tier 2, `Hexalith.Parties.Tests`); e2e `admin-parties-list.spec.ts` |
| FR-Admin-2 | Epic 2 | 2.3 | `/admin/parties/{id}` | `PartiesAdminPortalComponentTests`, `AdminPortalRouteIdsTests` (`Hexalith.Parties.AdminPortal.Tests`), `PartyStateBadgeTests` (tombstone, `Hexalith.Parties.UI.Tests`) |
| FR-Admin-3 | Epic 2 | 2.4, 2.5 | `/admin/parties/new`, `/admin/parties/{id}/edit`, in-form `<hexalith-party-picker>` host | `CreateEditPartyPageTests`, `PartyFormPickerBridgeTests` (`Hexalith.Parties.AdminPortal.Tests`), `PartyPickerComponentTests` (`Hexalith.Parties.Picker.Tests`); e2e `party-picker.spec.ts` |
| FR-Admin-4 | Epic 3 | 3.1–3.6 | `/admin/parties/{id}/gdpr` | `PartiesAdminPortalComponentTests` (typed-name erasure), `AdminPortalGdprSurfaceTests`, `AdminPortalGdprPrivacyGuardrailTests` (`Hexalith.Parties.Contracts.Tests`), `AdminPortalGdprOperationContractTests` (`Hexalith.Parties.Client.Tests`), `GdprDestructiveButtonTests` (`Hexalith.Parties.UI.Tests`); e2e `admin-gdpr-erasure-verification.spec.ts` |
| FR-Consumer-1 | Epic 4 | 4.3, 4.4 | `/me` | `MyProfilePageTests` (`Hexalith.Parties.ConsumerPortal.Tests`), `ConsumerProfileDataClientTests`, `SelfScopedPartiesClientTests` (`Hexalith.Parties.UI.Tests`); e2e `consumer-portal-routes.spec.ts` |
| FR-Consumer-2 | Epic 4 | 4.5 | `/me/edit` | `EditMyProfilePageTests` (`Hexalith.Parties.ConsumerPortal.Tests`), `ConsumerProfileEditClientTests` (`Hexalith.Parties.UI.Tests`) |
| FR-Consumer-3 | Epic 5 | 5.1 | `/me/consent` | `MyConsentPageTests` (`Hexalith.Parties.ConsumerPortal.Tests`), `ConsumerConsentClientTests` (`Hexalith.Parties.UI.Tests`) |
| FR-Consumer-4 | Epic 5 | 5.2–5.4 | `/me/privacy` | `MyPrivacyPageTests` (`Hexalith.Parties.ConsumerPortal.Tests`), `ConsumerPrivacyExportClientTests`, `ConsumerPrivacyErasureClientTests`, `ConsumerPrivacyProcessingClientTests` (`Hexalith.Parties.UI.Tests`), `PartyExportFileNameTests` (`Hexalith.Parties.Contracts.Tests`) |

Route note: the matrix writes Admin route parameters as `{id}` to match
`epics.md`; the Blazor `@page` templates in `Hexalith.Parties.AdminPortal` name
the parameter `{RoutePartyId}`. The paths are identical.

NFR coverage (cross-cutting; primary owners and verification):

| Requirement | Primary Epics | Verified By |
|---|---|---|
| NFR1 | Epics 1–5 (a11y foundation in Story 1.9) | Tier 1 bUnit: `MainLayoutAccessibilityTests`, `PartiesAccessibilitySpecimenTests`, `AccessibilityStyleGuardTests`, `StatusLiveRegionTests` (`Hexalith.Parties.UI.Tests`); `PartyPickerComponentTests` combobox contract (`Hexalith.Parties.Picker.Tests`); `PartiesAdminPortalComponentTests` typed-name confirmation (`Hexalith.Parties.AdminPortal.Tests`). Playwright axe gate `parties-accessibility.spec.ts` (`npm run test:a11y`) plus `party-picker.spec.ts` combobox checks (`npm test`): local lane only, not a CI job |
| NFR2 | Epics 1–2, 4 (shared freshness/optimistic pattern) | `OptimisticReconcileTests`, `ProjectionFreshnessCompositionTests`, `ProjectionFreshnessFallbackTests`, `DegradedResponseHeaderHandlerTests`, `PartiesProjectionSubscriptionTests` (SignalR subscribe/reconnect), `DataFreshnessIndicatorTests` (`Hexalith.Parties.UI.Tests`); `ProjectionFreshnessAndDegradationTests` (Tier 2, `Hexalith.Parties.Tests`) |
| NFR3 | Epic 1 (Story 1.4 binding), Epics 4–5 (self-scoped clients) | `PartyIdClaimResolverTests`, `IdentityBindingBoundaryTests`, `SelfScopedPartiesClientSurfaceTests`, `PartiesUiAreaAuthorizationTests` (`Hexalith.Parties.UI.Tests`); `AdminPortalGdprPrivacyGuardrailTests` (PII guardrail); `PartyStateBadgeTests` (tombstone); e2e `consumer-party-binding.spec.ts` |
| NFR4 | Epics 3, 5 | `MyConsentPageTests`, `MyPrivacyPageTests` (`Hexalith.Parties.ConsumerPortal.Tests`); `GdprDestructiveButtonTests`; `RecordConsentValidatorTests`, `RevokeConsentValidatorTests` |
| NFR5 | Epics 2, 4 | `unverified` (no automated reflow or single-column assertion; the phone sheet behavior is e2e-pinned per the UX validation report, and its focus contract is covered under UX-DR10) |
| NFR6 | Epics 1–2 | `StatusPresentationTests` and `OptimisticReconcileTests` tenant warm-up split to `StatusKind.TenantUnavailable` (`Hexalith.Parties.UI.Tests`); `HelperDrivenTenantAccessTests` fail-closed tenant access (Tier 2, `Hexalith.Parties.Tests`) |
| NFR7 | Epics 1–5 (UX-DR1–UX-DR7) | `AccessibilityStyleGuardTests`, `SharedDomainComponentStyleTests` (`Hexalith.Parties.UI.Tests`); e2e `parties-accessibility.spec.ts` "filled primary button does not compute to raw teal" |
| NFR8 | Epic 1 (host wiring), Story 8.4 (wrapper retirement) | `RetiredLeafProjectFitnessTests` (wrapper retirement); `ServiceDefaultsCompatibilityTests`, `HealthEndpointIntegrationTests` (Tier 2, service side); `HealthEndpointE2ETests` (`aspire-tests`). UI-host telemetry and health endpoint: `unverified` by a UI-host-specific automated test |
| NFR9 | Epic 6 and CI | `ci.yml` → `domain-ci.yml@main` jobs `build-and-test`, `aspire-tests`, `performance-tests`; `PartiesContainerPublishWorkflowTests.CiWorkflowDelegatesToSharedDomainCiWithPartiesTestLanes`, `StandaloneSolutionTests` (`Hexalith.Parties.Ci.Tests`); `commitlint.yml`, `codeql.yml`, `dependency-review.yml`, `rc-gate.yml` (`gitlink-rc-gate`); Playwright lane local-only |

UX design requirements (identity defined in `epics.md`; owning requirement here):

| Requirement | Owning Requirement | Primary Epics / Stories | Verified By |
|---|---|---|---|
| UX-DR1 | NFR7 | Epic 1 (Story 1.9) | `AccessibilityStyleGuardTests`; e2e raw-teal check |
| UX-DR2 | NFR7 | Epic 1 (Story 1.8) | `SharedDomainComponentStyleTests` |
| UX-DR3 | NFR7 | Epic 1 (Story 1.1, 1.9) | `AccessibilityStyleGuardTests` |
| UX-DR4 | NFR7, FR-Admin-2 | Epic 1 (Story 1.8) | `PartyStateBadgeTests` |
| UX-DR5 | NFR7, NFR2 | Epic 1 (Story 1.8) | `DataFreshnessIndicatorTests` |
| UX-DR6 | NFR7, FR-Admin-4 | Epic 1 (Story 1.8), Epic 3 | `GdprDestructiveButtonTests` |
| UX-DR7 | NFR7, FR-Admin-3 | Epic 2 (Story 2.5) | `PartyPickerComponentTests`; e2e `party-picker.spec.ts` |
| UX-DR8 | NFR1, NFR2 | Epic 1 (Story 1.6) | `StatusLiveRegionTests`, `StatusPresentationTests` |
| UX-DR9 | NFR1 | Epics 2, 3, 5 | `MyConsentPageTests`, `CreateEditPartyPageTests`, `PartiesAdminPortalComponentTests` (typed-erase) |
| UX-DR10 | NFR1 | Epic 1 (Story 1.9), Epic 2 | `MainLayoutAccessibilityTests`; e2e skip-link and keyboard-flow tests |
| UX-DR11 | NFR1 | Epics 1–5 | `PartiesAccessibilitySpecimenTests`; e2e axe gate |
| UX-DR12 | NFR1 | Epic 1 (Story 1.9) | e2e "forced-colors and reduced-motion media are observable" (local lane) |
| UX-DR13 | NFR4, FR-Consumer-4 | Epic 5 (Story 5.3) | `MyPrivacyPageTests` |
| UX-DR14 | NFR4, FR-Consumer-3 | Epic 5 (Story 5.1) | `MyConsentPageTests` |
| UX-DR15 | NFR4, FR-Consumer-4 | Epic 5 (Story 5.2) | `MyPrivacyPageTests`, `ConsumerPrivacyExportClientTests` |
| UX-DR16 | NFR4 | Epics 4–5 | `MyPrivacyPageTests`; e2e `shared-portal-display-formatters.spec.ts` |

Deployment gates (not UI feature scope; readiness must surface them):

| Gate | Owner | Blocks | Verified By |
|---|---|---|---|
| GATE-KMS | Deployment/platform owner | Go-live with real regulated EU personal data | `unverified` — documentary only; see Deployment Gates |

## Current Implementation Evidence

`_bmad-output/implementation-artifacts/sprint-status.yaml` is the only
story-status source. The roster below is a dated snapshot for readers; a
readiness run must read the live file and must not extract story status from
this paragraph.

As of `sprint-status.yaml` `last_updated: 2026-09-07`, the MVP scope — Epics 1-5
and their stories — is `done`, Epic 6 is `done`, Epic 7 is `done`, and Epic 8 is
`in-progress` (stories 8.1-8.6 and 8.11-8.13 `done`; 8.7, 8.8, and 8.9
`blocked` — 8.9 was moved from `backlog` to `blocked` by
`sprint-change-proposal-2026-09-06-sprint-status-key-freeze-and-blocked-status.md`;
8.10 in `review`). Readiness validation after this date must reconcile this PRD
and planning documents with implementation story records.

Post-MVP maintenance status:

**Scope invariant:** Epics 6, 7, and 8 are maintenance scope only. None of them
introduces or covers a new PRD functional requirement, and none may be counted
as MVP or product-feature functional coverage.

- Epic 6 (`done`) is in-repository consolidation scope. It supports NFR9 and
  carries no new PRD functional requirement coverage.
- Epic 7 (`done`) is completed platform-alignment maintenance scope. Its final
  readiness record preserved rollback paths and deferred deletion-safe cleanup;
  the projection rollback-only paths were subsequently removed on 2026-08-01
  under the governed retention closure (Story 8.6,
  `sprint-change-proposal-2026-08-01-projection-rollback-retention-revalidation.md`),
  while crypto/key-management rollback paths remain preserved pending the
  Story 8.7 gate.
- Epic 8 (`in-progress`), approved by `sprint-change-proposal-2026-07-06.md`,
  is domain-focus refactoring and platform extraction; stories 8.11-8.13 were
  added by the 2026-07-07 and 2026-07-08 correct-course proposals. It is
  post-MVP maintenance only, carries no new PRD functional requirement
  coverage, and must not be reported as product-feature delivery.

Known completed dependency evidence:

- Story 1.4 completed fail-closed `party_id` claim resolution with synthetic-claim
  and DI coverage.
- Story 3.5 completed the D7 erasure certificate (the D7 decision in `epics.md`)
  and retry backend behavior through existing projection-query and command seams.
- Story 3.6 completed the bounded Admin erasure-verification report UI.
- Story 4.1 completed the accepted Consumer identity binding ADR.
- Story 4.2 completed admin-link identity binding provisioning.

## Deployment Gates

Not UI feature scope, but go-live requirements that readiness reporting must
surface rather than ignore. Gates carry stable `GATE-<name>` IDs and appear in
the Traceability Matrix.

- **GATE-KMS — Production KMS provisioning.** A production KMS or
  secret-store-backed key provider must replace the default key store
  (`LocalDevKeyStorageBackend`, in-memory and dev-only) before processing real
  regulated EU personal data. Owner: deployment/platform owner. Enforcement:
  documentary only — no production-environment rejection guard and no startup
  dev-only warning exist in code (`docs/architecture.md` §8), so the gate is
  `unverified` until a fail-closed check ships or a production key backend is
  registered. Documentation: the GDPR notice in `docs/index.md` and the
  production KMS gate in `docs/getting-started.md` (the former
  `docs/deployment-security-checklist.md` was retired by Story 8.13).
  Crypto/key-management extraction is Story 8.7 (`blocked`).

## Out of MVP Scope

- Gateway-level data-subject/self principal support remains a future enhancement.
- Consumer self-registration and IdP federation are future provisioning options.
- Temporal name-as-of queries and semantic/graph/hybrid search remain deferred.

Production KMS provisioning is not out of scope: it is GATE-KMS above, a
go-live prerequisite rather than a UI feature story.

## Document Control

Frontmatter semantics: `date` is the original issue date; `last_updated` and
`version` advance with every edit; `status: canonical-requirements-source` is a
stable machine anchor for readiness tooling and does not change.

Requirement ID conventions: functional requirements use `FR-<Area>` or
`FR-<Area>-<n>` (FR-Shell, FR-Admin-1..4, FR-Consumer-1..4); non-functional
requirements use `NFR<n>` (NFR1..NFR9); UX design requirements use `UX-DR<n>`
(UX-DR1..UX-DR16, defined in `epics.md`); deployment gates use `GATE-<name>`
(GATE-KMS). Only `FR-*` entries count as PRD functional requirements for scope
invariants such as the Epics 6-8 zero-new-FR rule; NFRs, UX-DRs, and gates are
non-functional, design, and deployment coverage respectively.

Readiness contract: readiness tooling extracts, from the Traceability Matrix
only, FR identity with epic/story/surface/verification mapping (functional
table), NFR identity with verification (NFR table), UX-DR identity with owning
requirement and verification (UX-DR table), and deployment gates (gates table).
A `Verified By` cell names the CI job, test class, or e2e spec that checks the
requirement; `unverified` is an explicit no-evidence token and must be reported
as such. Acceptance evidence for each requirement lives in the implementation
story records, which win on completed-work evidence per the Source Artifacts
precedence rule.

Freeze scope: `EpicEightClosureFitnessTests.EpicEightAddsNoPrdFunctionalRequirement`
freezes the `### FR-*` and `### NFR<n>` heading inventory of this file against
commit `37f4ec82` and freezes `epics.md` byte-for-byte. UX-DR and GATE
identities, the UX Requirements list, and the matrix tables are not
freeze-protected; extending the freeze to them is deferred to the Epic 8
closure owner (Story 8.10 follow-up).

Change log:

| Version | Date | Change |
|---|---|---|
| 1.0.0 | 2026-06-27 | Initial brownfield consolidation. |
| 1.1.0 | 2026-07-06 | Post-MVP maintenance status: Epic 7 completion, Epic 8 approval. |
| 1.1.1 | 2026-07-16 | Epics 7-8 maintenance-scope invariant (`sprint-change-proposal-2026-07-16-epics-7-8-maintenance-scope.md`). |
| 1.2.0 | 2026-08-18 | Governed correction from `prds/prd-parties-2026-08-18/validation-report.md`: currency refresh, NFR traceability, UX-DR enumeration, deployment-gate section, clarified wording. No functional requirement added or removed. |
| 1.3.0 | 2026-09-08 | Governed correction from the 2026-09-08 validation run (`prds/prd-parties-2026-08-18/validation-report.md`): real CI topology and named tests in every `Verified By` cell with explicit `unverified` tokens; FR verification column; UX-DR and GATE-KMS matrix tables; UX-DR MUST clauses restored from `epics.md`; party picker restored to FR-Admin-3; NFR1 product-wide; NFR2 SignalR clause; NFR7 range aligned; waived NFR categories stated; 8.9 `blocked` and `sprint-status.yaml` as living status pointer; Purpose aligned with identity/scope canonicity; readiness contract and freeze scope stated. No functional requirement added, removed, or reordered. |
