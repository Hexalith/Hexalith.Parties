---
project_name: parties
document_type: prd
status: canonical-requirements-source
date: 2026-06-27
last_updated: 2026-11-20
version: 1.3.3
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
- `_bmad-output/planning-artifacts/epics.md` — commit `37f4ec82` is the
  reference copy for the Epic 8 inventory check in Document Control.
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

DPO/Admin users can request erasure with typed-name confirmation, restrict and
lift processing restrictions, record and revoke consent, export data under
Art.20, and view processing records under Art.30. The verification report must
show completion only after the service persists the certificate and commits
the terminal erasure events. Stories 3.5 and 3.6 supply the bounded D7
certificate and report UI. The matrix tracks the deployed erasure and replica
checks separately. GDPR operations must avoid PII leakage and use the existing
typed clients (`IAdminPortalGdprClient`, `IErasureVerificationService`).

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
For each legitimate-interest item, show the Article 21 right to object on its
own line. Until an objection command exists, give the Consumer a usable contact
route and explain that the team will review the objection and reply within a
month. The current page shows a disabled Object button.

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
and must not accept caller-supplied party ids. Parties checks that
`aggregateId == party_id`. Through the standalone OIDC host and real gateway,
each signed-in user can read and update only their own record. Once a session
expires, it must no longer authorize either operation. The NFR3 matrix row
tracks the deployed qualification. Gateway-level data-subject/self principal
support is deferred.
Logs, telemetry, tombstones, and error copy do not expose PII.

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

The UI targets `net11.0` and runs in an ASP.NET 11 container. The repository
pins SDK `11.0.100`. The .NET 10 target in the frozen MVP inventory is
historical. CI also installs SDK `10.0.404` as an additional tool.
[SDK pin](../../global.json) · [Build target](../../Directory.Build.props) · [UI container](../../src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj)

Builds use central package management, `.slnx`, warnings as errors, and direct
submodules under `references/`. Tests use xUnit v3, Shouldly, NSubstitute,
bUnit, and Playwright. The matrix identifies the hosted browser checks.

**Waived NFR categories.** This PRD sets no UI performance, latency,
availability, or browser-support threshold for the MVP. Reason: this is a
brownfield readiness index for a Blazor Server shell over an existing service;
the .NET test project runs `LocalFuzzySearchPerformanceBenchmarkTests` for
service-side search. Browser support follows the FluentUI Blazor V5 and
Blazor Server support matrix. Quantified thresholds for NFR2–NFR9 are likewise
not set here; they live with the story records per the Source Artifacts
precedence rule. Both waivers are revisited if this PRD becomes the chain-top
requirements document again.

## UX Requirements

The design requirements in `epics.md` define the identifiers below.
`DESIGN.md` and `EXPERIENCE.md` define their current visual and interaction
rules, including the destructive confirmation and Article 21 contact route.

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
- UX-DR6 — Use an outline erase trigger. Only the irreversible Confirm inside
  the dialog uses the `--colorStatusDangerBackground3` fill with
  `--colorNeutralForegroundOnBrand` text. Restrict, lift, withdraw and cancel
  use Outline. For Admin erasure, show the expected name, allow paste and enable
  Confirm when the names match after trimming, Unicode NFKC normalization and
  case-insensitive comparison. The Consumer erasure request uses a single
  confirmation. Focus and blur never submit either action.
  [Button design](ux-designs/ux-parties-2026-06-09/DESIGN.md) · [Confirmation behavior](ux-designs/ux-parties-2026-06-09/EXPERIENCE.md)
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
- UX-DR12 — Support forced colors and reduced motion throughout both areas.
  Hosted Chromium tests check visible keyboard focus with forced colors and
  motion suppression on specimen controls. The matrix also names the checks on
  Consumer routes.
- UX-DR13 — Honest erasure copy: commit to the start ("We've started deleting
  your data… usually within 30 days… we'll confirm when it's done"), never a
  hard finish SLA; state both halves — cancellable until deletion begins,
  permanent once complete; the 30-day figure is not the cancel window; the
  acknowledgement uses neutral/info tone, never success-green.
- UX-DR14 — Lawful-basis honesty: split "Things you control" (consent toggles,
  default Off) from "Things we keep to run your account" (contract and
  legitimate interest, read-only). Show the Article 21 objection right
  separately for legitimate-interest items and provide the contact route in
  FR-Consumer-3 while a dedicated command is unavailable. "Manage all consent"
  links to My Consent, where users can both grant and withdraw consent.
- UX-DR15 — Export copy with no time promise ("Preparing your export — this can
  take a little while. We'll show it here the moment it's ready"); states
  machine-readable JSON; the synchronous download is the happy case, not the
  promised baseline.
- UX-DR16 — Plain verbs and a single status source: lead with "Delete my data",
  not "Erasure"; Admin terse and Consumer plain-and-reassuring register; one
  status source per action (never "Saved" and "Saving" together); rendered
  value identical to stored value across view and edit.

## Traceability Matrix

The `ci` job calls `Hexalith.Builds/.github/workflows/domain-ci-v2.yml` at
`fe68718b6c074a6b500a7191e3554a1d39e17688`. It lists the .NET test projects
and selects the `platform-package` integration profile. The
`verify-public-package-integration` job checks the integration evidence for
the tested revision. [CI workflow](../../.github/workflows/ci.yml)

The separate `ui-a11y` job runs `npm run test:ui` in Chromium and uploads
`ui-a11y-results`. The five specs are `parties-accessibility.spec.ts`,
`admin-parties-list.spec.ts`, `consumer-portal-routes.spec.ts`,
`party-picker.spec.ts` and `shared-portal-display-formatters.spec.ts`.
The full local suite uses `npm test`. A test name identifies what it exercises.
Readiness requires a passing result for the revision under review.
`unverified` marks a clause that still lacks qualifying evidence.
[Browser test commands](../../tests/e2e/package.json)

Action IDs in the matrix refer to the sprint tracker. Each action names the
owner and the evidence needed to close it.
[Sprint tracker](../implementation-artifacts/sprint-status.yaml)

Functional requirements:

| Requirement | Primary Epic | Stories | Primary Surfaces | Verified By |
|---|---|---|---|---|
| FR-Shell | Epic 1 | 1.1–1.5 | Sign-in, role landing, navigation, NoPartyBinding | `PartiesUiOidcConfigurationTests`, `RoleLandingRedirectTests`, `PartiesUiNavEntryGatingTests`, `PartiesUiAreaAuthorizationTests`, `PartyIdClaimResolverTests`, `NoPartyBindingRoutingTests` in `Hexalith.Parties.UI.Tests`. The full local suite includes `admin-area-authorization.spec.ts`, `shared-role-policy-authorization.spec.ts` and `consumer-party-binding.spec.ts`. Real signed-in reads and updates, expiry and two-user isolation are `unverified` (`audit-standalone-token-relay`). |
| FR-Admin-1 | Epic 2 | 2.1, 2.2 | `/admin/parties` | `PartiesAdminPortalComponentTests` (Admin Portal), `AdminPortalQueryContractTests` (client), `MvpDisplayNameSearchContractTests` (domain). The hosted `ui-a11y` job runs `admin-parties-list.spec.ts`. |
| FR-Admin-2 | Epic 2 | 2.3 | `/admin/parties/{id}` | `PartiesAdminPortalComponentTests`, `AdminPortalRouteIdsTests` (`Hexalith.Parties.AdminPortal.Tests`), `PartyStateBadgeTests` (tombstone, `Hexalith.Parties.UI.Tests`) |
| FR-Admin-3 | Epic 2 | 2.4, 2.5 | `/admin/parties/new`, `/admin/parties/{id}/edit`, in-form `<hexalith-party-picker>` host | `CreateEditPartyPageTests`, `PartyFormPickerBridgeTests` (Admin Portal) and `PartyPickerComponentTests` (picker). The hosted `ui-a11y` job runs `party-picker.spec.ts`. |
| FR-Admin-4 | Epic 3 | 3.1–3.6 | `/admin/parties/{id}/gdpr` | `PartiesAdminPortalComponentTests` exercises typed confirmation. `AdminPortalGdprSurfaceTests`, `AdminPortalGdprPrivacyGuardrailTests`, `AdminPortalGdprOperationContractTests` and `GdprDestructiveButtonTests` cover the UI and contracts. The local `admin-gdpr-erasure-verification.spec.ts` displays a fixture-backed certificate. Normalized name matching is `unverified` (UX-DR6). Terminal erasure is `unverified` (`epic-8-retro-erasure-status-after-commit`). Cross-replica revocation is `unverified` (`audit-replica-key-revocation`). |
| FR-Consumer-1 | Epic 4 | 4.3, 4.4 | `/me` | `MyProfilePageTests` (Consumer Portal), `ConsumerProfileDataClientTests` and `SelfScopedPartiesClientTests` (UI). Hosted `consumer-portal-routes.spec.ts` uses a bound fixture. Reading a Consumer’s own record through the deployed gateway is `unverified` (`audit-standalone-token-relay`). |
| FR-Consumer-2 | Epic 4 | 4.5 | `/me/edit` | `EditMyProfilePageTests` (Consumer Portal) and `ConsumerProfileEditClientTests` (UI) cover editing and client behavior. Updating a Consumer’s own record through the deployed gateway is `unverified` (`audit-standalone-token-relay`). |
| FR-Consumer-3 | Epic 5 | 5.1 | `/me/consent` | `MyConsentPageTests` and `ConsumerConsentClientTests` cover consent controls and the client. Hosted `consumer-portal-routes.spec.ts` checks grant and withdrawal and expects a disabled Object button. The Article 21 contact route is `unverified` until a Consumer can follow it. |
| FR-Consumer-4 | Epic 5 | 5.2–5.4 | `/me/privacy` | `MyPrivacyPageTests`, `ConsumerPrivacyExportClientTests`, `ConsumerPrivacyErasureClientTests`, `ConsumerPrivacyProcessingClientTests` and `PartyExportFileNameTests` cover the page, request/cancel flow, processing reads and export. Committed erasure through redacted reads is `unverified` (`epic-8-retro-erasure-status-after-commit`). Refusal of retired key versions on both replicas is `unverified` (`audit-replica-key-revocation`). |

Route note: the matrix writes Admin route parameters as `{id}` to match
`epics.md`; the Blazor `@page` templates in `Hexalith.Parties.AdminPortal` name
the parameter `{RoutePartyId}`. The paths are identical.

NFR coverage (cross-cutting; primary owners and verification):

| Requirement | Primary Epics | Verified By |
|---|---|---|
| NFR1 | Epics 1–5 (a11y foundation in Story 1.9) | bUnit coverage includes `MainLayoutAccessibilityTests`, `PartiesAccessibilitySpecimenTests`, `AccessibilityStyleGuardTests`, `StatusLiveRegionTests`, `PartyPickerComponentTests` and `PartiesAdminPortalComponentTests`. Hosted `ui-a11y` runs the axe specimen in `parties-accessibility.spec.ts` and the combobox checks in `party-picker.spec.ts`. |
| NFR2 | Epics 1–2, 4 (shared freshness/optimistic pattern) | `OptimisticReconcileTests`, `ProjectionFreshnessCompositionTests`, `ProjectionFreshnessFallbackTests`, `DegradedResponseHeaderHandlerTests`, `PartiesProjectionSubscriptionTests` (SignalR subscribe/reconnect), `DataFreshnessIndicatorTests` (`Hexalith.Parties.UI.Tests`); `ProjectionFreshnessAndDegradationTests` (`Hexalith.Parties.Tests`) |
| NFR3 | Epic 1 (Story 1.4 binding), Epics 4–5 (self-scoped clients) | `PartyIdClaimResolverTests`, `IdentityBindingBoundaryTests`, `SelfScopedPartiesClientSurfaceTests` and `PartiesUiAreaAuthorizationTests` exercise binding and access rules. `AdminPortalGdprPrivacyGuardrailTests` checks PII handling. `PartyStateBadgeTests` covers tombstones. Local `consumer-party-binding.spec.ts` uses a fixture. Standalone OIDC reads and updates through the real gateway, expiry and isolation between two signed-in users are `unverified` (`audit-standalone-token-relay`). |
| NFR4 | Epics 3, 5 | `MyConsentPageTests`, `MyPrivacyPageTests` (`Hexalith.Parties.ConsumerPortal.Tests`); `GdprDestructiveButtonTests`; `RecordConsentValidatorTests`, `RevokeConsentValidatorTests` |
| NFR5 | Epics 2, 4 | Hosted `admin-parties-list.spec.ts` checks horizontal overflow on a 320 px form at 200% zoom and focus restoration on closing the phone detail sheet. Hosted `consumer-portal-routes.spec.ts` checks overflow and keyboard access at 390 px. The Consumer single-column layout across supported widths is `unverified`. |
| NFR6 | Epics 1–2 | `StatusPresentationTests` and `OptimisticReconcileTests` tenant warm-up split to `StatusKind.TenantUnavailable` (`Hexalith.Parties.UI.Tests`); `HelperDrivenTenantAccessTests` fail-closed tenant access (`Hexalith.Parties.Tests`) |
| NFR7 | Epics 1–5 (UX-DR1–UX-DR7) | `AccessibilityStyleGuardTests`, `SharedDomainComponentStyleTests` (`Hexalith.Parties.UI.Tests`); e2e `parties-accessibility.spec.ts` "filled primary button does not compute to raw teal" |
| NFR8 | Epic 1 (host wiring), Story 8.4 (wrapper retirement) | `RetiredLeafProjectFitnessTests` checks UI-host registration in source. `ServiceDefaultsCompatibilityTests` and `HealthEndpointIntegrationTests` cover service-side behavior. The six `HealthEndpointE2ETests` cases are skipped. UI-host runtime health and telemetry export are `unverified`. |
| NFR9 | Epic 6 and CI | `ci` calls the pinned `domain-ci-v2.yml` workflow. Its `build-and-test` job runs the listed .NET projects. `verify-public-package-integration` validates the package integration evidence. `ui-a11y` runs `npm run test:ui` and uploads `ui-a11y-results`. `StandaloneSolutionTests` and `BrowserProductTests` check the solution and browser commands. Related workflows include `commitlint.yml`, `codeql.yml`, `dependency-review.yml` and `rc-gate.yml` (`gitlink-rc-gate`). |

UX design requirements (identity defined in `epics.md`; owning requirement here):

| Requirement | Owning Requirement | Primary Epics / Stories | Verified By |
|---|---|---|---|
| UX-DR1 | NFR7 | Epic 1 (Story 1.9) | `AccessibilityStyleGuardTests`; e2e raw-teal check |
| UX-DR2 | NFR7 | Epic 1 (Story 1.8) | `SharedDomainComponentStyleTests` |
| UX-DR3 | NFR7 | Epic 1 (Story 1.1, 1.9) | `AccessibilityStyleGuardTests` |
| UX-DR4 | NFR7, FR-Admin-2 | Epic 1 (Story 1.8) | `PartyStateBadgeTests` |
| UX-DR5 | NFR7, NFR2 | Epic 1 (Story 1.8) | `DataFreshnessIndicatorTests` |
| UX-DR6 | NFR7, FR-Admin-4 | Epic 1 (Story 1.8), Epic 3 | `GdprDestructiveButtonTests` covers explicit confirmation. The Admin panel styles use the approved token pair. The shared button still uses `--colorStatusDangerForegroundInverted` for text. `PartiesAdminPortalComponentTests` rejects case and trailing-space variants. Consistent Confirm styling and normalized Admin name matching are `unverified`. |
| UX-DR7 | NFR7, FR-Admin-3 | Epic 2 (Story 2.5) | `PartyPickerComponentTests`; e2e `party-picker.spec.ts` |
| UX-DR8 | NFR1, NFR2 | Epic 1 (Story 1.6) | `StatusLiveRegionTests`, `StatusPresentationTests` |
| UX-DR9 | NFR1 | Epics 2, 3, 5 | `MyConsentPageTests` and `CreateEditPartyPageTests` cover control semantics. `PartiesAdminPortalComponentTests` checks the Admin input and exact-match enablement. Trimmed, case-insensitive NFKC matching is `unverified` (UX-DR6). |
| UX-DR10 | NFR1 | Epic 1 (Story 1.9), Epic 2 | `MainLayoutAccessibilityTests`; e2e skip-link and keyboard-flow tests |
| UX-DR11 | NFR1 | Epics 1–5 | `PartiesAccessibilitySpecimenTests`; e2e axe gate |
| UX-DR12 | NFR1 | Epic 1 (Story 1.9) | Hosted `parties-accessibility.spec.ts` checks visible keyboard focus in forced colors and suppression of animation and transitions on specimen controls. Hosted `consumer-portal-routes.spec.ts` checks accordion keyboard focus at narrow width under forced colors. Coverage of all product surfaces is `unverified`. |
| UX-DR13 | NFR4, FR-Consumer-4 | Epic 5 (Story 5.3) | `MyPrivacyPageTests` |
| UX-DR14 | NFR4, FR-Consumer-3 | Epic 5 (Story 5.1) | `MyConsentPageTests` checks the objection label. Hosted `consumer-portal-routes.spec.ts` verifies consent controls and the disabled Object button. The Article 21 contact fallback is `unverified` until a test follows the contact route. |
| UX-DR15 | NFR4, FR-Consumer-4 | Epic 5 (Story 5.2) | `MyPrivacyPageTests`, `ConsumerPrivacyExportClientTests` |
| UX-DR16 | NFR4 | Epics 4–5 | `MyPrivacyPageTests`; e2e `shared-portal-display-formatters.spec.ts` |

Deployment gates (not UI feature scope; readiness must surface them):

| Gate | Owner | Blocks | Verified By |
|---|---|---|---|
| GATE-KMS | Deployment/platform owner | Go-live with real regulated EU personal data | `unverified` — documentary only; see Deployment Gates |

## Current Implementation Evidence

On 2026-11-20 the tracker records the MVP epics (1–5) as `done`. Read the
live tracker for current epic and story status and the story records for
acceptance evidence. [Sprint tracker](../implementation-artifacts/sprint-status.yaml)

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
- Epic 8 closed as post-MVP refactoring and platform extraction. The
  2026-11-20 tracker marks the epic, Story 8.10 and the retrospective `done`.
  Stories 8.7, 8.8 and 8.9 remain `blocked` under explicit deferrals. The
  retrospective records the outstanding erasure qualifications.
  [Epic 8 retrospective](../implementation-artifacts/epic-8-retro-2026-10-09.md)

**External consumer extension:** `EXT-PARTIES-1` Branch B is separate
Agents-consumer contract work, governed by
`spec-ext-parties-1-branch-b-authoritative-identity.md` and the
`sprint-change-proposal-2026-10-09.md` decision. Its Parties Contracts API,
identity history, and host policy are outside this UI PRD's requirement and
traceability inventories and outside Parties sprint epic/story tracking. Its
in-progress implementation is not Epic 7 or 8 maintenance, MVP coverage, or
verified product-feature delivery. Owner acceptance and production availability
require the spec's live qualification and dependency evidence.

Known completed dependency evidence:

- Story 1.4 completed fail-closed `party_id` claim resolution with synthetic-claim
  and DI coverage.
- Story 3.5 completed the D7 erasure certificate (the D7 decision in `epics.md`)
  and retry backend behavior through existing projection-query and command seams.
- Story 3.6 completed the bounded Admin erasure-verification report UI.
- Story 4.1 completed the accepted Consumer identity binding ADR.
- Story 4.2 completed admin-link identity binding provisioning.

**Open erasure qualifications.** One accepted gateway request must reach key
destruction, persisted certification, cleanup, committed terminal events and
redacted reads through restart and redelivery. The separate revocation check
requires both replicas to refuse retired keys, including a lookup already in
flight. [November sprint change](sprint-change-proposal-2026-11-20.md)

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
  `unverified`. To close it, the deployment/platform owner must show that the
  service refuses the ephemeral key backend outside development and that the
  selected durable provider can decrypt and replay a synthetic protected record
  after host replacement and from an independent replica. Record the deployed
  provider identity and redacted test results. See the
  [November sprint change](sprint-change-proposal-2026-11-20.md) for the
  separate erasure and replica-revocation checks. Operator guidance lives in
  the GDPR notice in `docs/index.md` and the production KMS gate in `docs/getting-started.md` (the former
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

The Epic 8 freeze applies to the ordered FR/NFR headings and the full text of
`epics.md`. Commit `0f261ccd` retired the fitness test that enforced it.
The readiness reviewer compares the ordered `### FR-` and `### NFR` headings
in this PRD with the copy at `37f4ec82`, then compares the bytes of
`epics.md` with that commit. The reviewer records the revision and both results.
At `787aef2d`, the heading inventory matched and both epics blobs were
`f418a181`. UX-DRs, deployment gates and matrix mappings fall outside that
historical freeze.

Change log:

| Version | Date | Change |
|---|---|---|
| 1.0.0 | 2026-06-27 | Initial brownfield consolidation. |
| 1.1.0 | 2026-07-06 | Post-MVP maintenance status: Epic 7 completion, Epic 8 approval. |
| 1.1.1 | 2026-07-16 | Epics 7-8 maintenance-scope invariant (`sprint-change-proposal-2026-07-16-epics-7-8-maintenance-scope.md`). |
| 1.2.0 | 2026-08-18 | Governed correction from `prds/prd-parties-2026-08-18/validation-report.md`: currency refresh, NFR traceability, UX-DR enumeration, deployment-gate section, clarified wording. No functional requirement added or removed. |
| 1.3.0 | 2026-09-08 | Governed correction from the 2026-09-08 validation run (`prds/prd-parties-2026-08-18/validation-report.md`): real CI topology and named tests in every `Verified By` cell with explicit `unverified` tokens; FR verification column; UX-DR and GATE-KMS matrix tables; UX-DR MUST clauses restored from `epics.md`; party picker restored to FR-Admin-3; NFR1 product-wide; NFR2 SignalR clause; NFR7 range aligned; waived NFR categories stated; 8.9 `blocked` and `sprint-status.yaml` as living status pointer; Purpose aligned with identity/scope canonicity; readiness contract and freeze scope stated. No functional requirement added, removed, or reordered. |
| 1.3.1 | 2026-10-09 | Classify EXT-PARTIES-1 Branch B as a separate spec-only external-consumer extension; preserve the PRD requirement inventory and Epics 7-8 maintenance scope. |
| 1.3.2 | 2026-11-20 | Define the startup, restart and replica evidence required to close GATE-KMS. |
| 1.3.3 | 2026-11-20 | Reconcile the nine November validation findings. Update privacy and UX qualifications, CI and runtime references, Epic 8 closure and the inventory comparison. |
