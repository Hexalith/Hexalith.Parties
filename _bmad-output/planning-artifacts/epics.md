---
stepsCompleted: [1, 2, 3, 4]
status: complete
completedAt: '2026-06-09'
epicCount: 5
storyCount: 30
correctCourse: '2026-06-09 — readiness course-correction: split Story 4.1 (decision spike + impl) and Story 3.5 (D7 backend + report UI); +phone-reflow AC on 2.3; +mock-fidelity rule'
inputDocuments:
  # Requirements basis (no formal PRD — brownfield; architecture + UX are the basis)
  - _bmad-output/planning-artifacts/architecture.md
  # UX design set
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/validation-report.md
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-accessibility.md
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md
  # UX mockups (visual reference for acceptance criteria)
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/mockups/signin.html
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/mockups/admin-parties.html
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/mockups/create-edit-party.html
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/mockups/consumer-profile.html
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/mockups/consumer-privacy.html
  # Brownfield docs/ baseline
  - docs/index.md
  - docs/project-overview.md
  - docs/architecture.md
  - docs/data-models.md
  - docs/api-contracts.md
  - docs/development-guide.md
  # GDPR docs/ deep-dives
  - docs/gdpr-erased-party-status.md
  - docs/gdpr-key-rotation-and-shredding.md
  - docs/gdpr-portability-export.md
  - docs/gdpr-processing-activity-records.md
  # Project context (persistent facts — AI agent rules)
  - _bmad-output/project-context.md
requirementsBasis: 'Brownfield docs/ + UX design set (no formal PRD — per docs/index.md brownfield note and architecture.md). Architecture.md consolidates FRs/NFRs derived from UX EXPERIENCE.md.'
project_name: parties
user_name: Administrator
date: '2026-06-09'
---

# parties - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for **parties** (the
`parties-ui` initiative), decomposing the requirements from the **UX design set**
(`ux-parties-2026-06-09`) and the **Architecture Decision Document** into
implementable stories. **There is no formal PRD** — this is a brownfield .NET 10
system; per the `docs/index.md` brownfield note the requirements basis is the
`docs/` baseline plus the UX design, which `architecture.md` already consolidated
into the FR/NFR set reproduced below.

**Scope:** Realize the `parties-ui` experience — a single responsive Blazor Server
app on the **FrontComposer shell + FluentUI Blazor V5**, with two role-gated areas
(**Admin** records management + **Consumer** GDPR self-service), extending the
existing event-sourced / CQRS / EventStore-gateway-fronted Parties domain service.

> **Mockup fidelity (normative — applies to every UI story below).** The UX **spine
> (`EXPERIENCE.md`) and the resolved UX-DRs are authoritative**; the HTML mockups are
> **illustrative only** and may still contain pre-fix review violations (unlabeled
> typed-confirm, non-semantic consent toggle, default-On marketing, sub-13px microcopy).
> **Where any mockup conflicts with the spine, the spine wins.** Implement against the
> spine + UX-DRs; the Story 1.9 a11y gate (bUnit + Playwright WCAG 2.2 AA) is the backstop
> that fails the build on any reintroduced defect. _(Resolves the readiness §4 mock-fidelity risk.)_

## Requirements Inventory

### Functional Requirements

> FR labels are preserved from `architecture.md` (derived from UX `EXPERIENCE.md`),
> because the architecture's FR→structure map and gap analysis already reference them.

**Shell / cross-area**

- **FR-Shell:** One sign-in (host-owned OIDC). Authenticate, then route to the
  landing area **by role** (`Admin`/`TenantOwner` → Admin; `Consumer` → Consumer);
  preserve the return URL on `SignInRequired`. Nav auto-populates from domain
  manifests, gated by `<AuthorizeView Policy=…>` — Admin and Consumer nav never
  cross-render. A Consumer with **no `party_id` claim** is routed to a fail-closed
  onboarding/error state, never to a data screen.

**Admin area (`/admin/parties*`, requires `Admin` policy)**

- **FR-Admin-1:** **Parties list** — server-driven, debounced display-name search +
  Person/Organization type filter and active filter (`FluentSelect`); row → detail;
  render last-known on staleness (never block on a degraded read).
- **FR-Admin-2:** **Party detail** — view the full `PartyDetail`; entry points to
  Edit and to GDPR operations; party-state badge reflects lifecycle.
- **FR-Admin-3:** **Create / Edit party** — validated form → command
  (`CreateParty(Composite)` / `Update*`); in-form `<hexalith-party-picker>` to link
  a related party; Person/Organization chooser as a radiogroup.
- **FR-Admin-4:** **GDPR operations (DPO)** — erase (typed-name confirm) · restrict /
  lift restriction · record / revoke consent · Art.20 data export · Art.30 processing
  records · **erasure-verification report** (the last depends on the D7 EventStore
  contract — see Additional Requirements).

**Consumer area (`/me*`, requires `Consumer` policy, self-scoped to own `party_id`)**

- **FR-Consumer-1:** **My profile** — view own personal data + freshness; no list/search.
- **FR-Consumer-2:** **Edit my profile** — correct/update own data (validated → command).
- **FR-Consumer-3:** **My consent** — grant / withdraw consent; opt-in **default Off**,
  never pre-checked; **Object (Art.21)** for legitimate-interest bases (not a withdraw
  toggle); optimistic flip → reconcile on projection confirm.
- **FR-Consumer-4:** **My data & privacy** — export own data (async, machine-readable
  JSON) · request erasure (cancellable-until-start, permanent-once-complete) · see
  what's processed about me, split into "things you control" vs "things we keep".

### NonFunctional Requirements

> Derived from `architecture.md` "Non-Functional Requirements" + EXPERIENCE.md.

- **NFR1 — Accessibility (WCAG 2.2 AA, consumer-facing):** real ARIA semantics
  (combobox / switch / radiogroup / labeled typed-confirm); **live-region politeness
  split** (status/freshness = `polite`; validation/failure = `role=alert` assertive);
  per-surface focus contract (trap/restore on dialogs, move-to-alert on blocking
  errors, announce-not-steal on optimistic saves); **forced-colors + reduced-motion
  product-wide**; color-never-alone; ≥24px (≥44px touch) targets; **AA contrast gate**
  (filled primary → `--colorBrandBackground`, never raw teal `#0097A7` @ 3.51:1).
- **NFR2 — Eventual consistency is first-class UX:** surface
  `ProjectionFreshnessMetadata` (fresh/stale/degraded) and the `StatusKind` /
  `PartyPickerSearchState` machines; optimistic echo + silent reconcile; **render
  last-known cache, never blank/throw**; fail-closed tenant warm-up reads as "still
  warming up," not "access denied." Acceptance is **not** read-your-write.
- **NFR3 — Security / own-data privacy:** a Consumer is scoped to **their own party
  only** (single self-scope choke point); no PII in logs/telemetry/copy/tombstones;
  admin typed-name erase confirmation compared **in-memory only**.
- **NFR4 — GDPR honesty (copy):** consent opt-in (default Off, never pre-checked);
  erasure copy commits to the **start** of the obligation (Art.12(3)), states
  completed erasure is permanent; **Art.21 Object** for non-consent bases; **Art.20**
  export machine-readable + async, no time promise.
- **NFR5 — Responsive:** Admin desktop-first master-detail (degrades to sheet /
  full-screen with a focus contract); Consumer phone-first single column. One
  codebase, two density postures (Admin comfortable, Consumer roomy).
- **NFR6 — Multi-tenancy:** Admin operates within tenant scope; tenant isolation
  preserved; tenant-access fails closed and is eventually consistent.
- **NFR7 — Brand discipline:** inherit FluentUI V5 (Fluent 2) + FrontComposer shell
  wholesale; specify brand-delta only (consumer 16px body, roomier density, 4 domain
  components); theme via the design-token API, never hard-coded hex.
- **NFR8 — Observability:** OpenTelemetry + health on the UI host; surface
  `X-Service-Degraded` / `X-Stale-Data-Age` into UI state.
- **NFR9 — Build / quality gates:** .NET 10, Central Package Management (no `Version=`
  in csproj), solution-wide `TreatWarningsAsErrors`, `.slnx` only, root-level
  submodules only, Conventional Commits — all apply to the new tier.

### Additional Requirements

> Technical/infra requirements from `architecture.md` (decisions D1–D11, structure,
> gaps) and the brownfield `docs/` + GDPR deep-dives that shape stories.

**Starter / foundation (impacts Epic 1, Story 1):**

- **AR-Starter:** No external CLI starter. The first implementation story stands up a
  **new standalone Blazor Server host `Hexalith.Parties.UI`** modeled on the
  **FrontComposer shell-host pattern** (`Hexalith.FrontComposer/samples/Counter/Counter.Web`):
  FrontComposer Quickstart chain + FluentUI V5 + `AddHexalithDomain<PartiesUiDomainMarker>`,
  added to `Hexalith.Parties.slnx` and the AppHost as Aspire resource `parties-ui`.

**Architecture decisions (inherited as constraints):**

- **AR-D1 Render model:** Blazor **Interactive Server** (`AddInteractiveServerComponents`),
  `ValidateScopes=true` (ADR-030).
- **AR-D2 Consumer identity binding:** verified IdP `party_id` claim maps subject →
  exactly one `Party`; resolution **fail-closed**. *(Gap: the provisioning mechanism —
  admin-link / self-registration / IdP federation — is undesigned and gates the
  Consumer area; see AR-Gap-Binding.)*
- **AR-D3 Own-data authorization:** UI BFF only ever issues self-scoped operations for
  a Consumer (never list/search) via a **single `ISelfScopedPartiesClient` accessor**;
  Parties host adds a fail-closed **`IDataSubjectAccessService`** asserting
  `aggregateId == party_id` (defense-in-depth) + a new **`Consumer`** authorization policy.
- **AR-D4 Composition:** `Hexalith.Parties.UI` host embeds the existing
  **`Hexalith.Parties.AdminPortal`** RCL (Admin) + a **new `Hexalith.Parties.ConsumerPortal`**
  RCL (Consumer); policy-gated nav.
- **AR-D5 Transport & auth:** host-owned **OIDC** (`OpenIdConnect` 10.0.8) against
  Keycloak (run) / `tache` realm (publish); server-side cookie session, **tokens never
  reach the browser**; BFF calls the EventStore gateway via the typed
  `Hexalith.Parties.Client` (`AddPartiesClient`), injecting token+tenant via
  `requestCustomizer`.
- **AR-D6 Live freshness:** subscribe to EventStore projection updates over **SignalR**
  (`Hexalith.EventStore.SignalR` + `Microsoft.AspNetCore.SignalR.Client` 10.0.8) to
  reconcile optimistic UI; polling/freshness-metadata fallback when degraded.
- **AR-D7 GDPR-stub completion:** `GetErasureCertificateAsync` /
  `RetryErasureVerificationAsync` are inert **501 stubs** pending an **EventStore
  contract** (cross-submodule, requires explicit approval); the Admin
  erasure-verification report depends on it — define as a **backend story** preceding
  that UI.
- **AR-D8 Portability export delivery:** `ExportPartyData` → `PartyDataPortabilityPackage`
  (JSON); happy path = synchronous download with progress; slow/large = async
  "preparing → ready" with SignalR/poll signal + in-app download; **no time promise** in copy.
- **AR-D9 Accessibility enforcement:** WCAG 2.2 AA enforced by **bUnit** component tests
  + a **Playwright a11y/visual gate** (FrontComposer e2e pattern) added to CI.
- **AR-D10 AppHost / deploy:** add `builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")`
  referencing `eventstore` + `tenants`, **no DAPR sidecar** (BFF over HTTP + SignalR);
  .NET SDK container (`ContainerRepository=parties-ui`); aspirate publish grows the
  cluster **11 → 12 pods**; CI gains the UI build + bUnit lane + Playwright gate.
- **AR-D11 Party-picker re-skin + ARIA:** re-skin `<hexalith-party-picker>` from legacy
  FAST tokens to **Fluent 2 tokens** **and** add the full **WAI-ARIA combobox**
  semantics — one combined design-debt story (`Hexalith.Parties.Picker`).

**Canonical implementation patterns (single source — agents must not remap per screen):**

- **AR-StatusMap:** the canonical `StatusKind → UI-state` mapping + the **aria-live
  politeness split** are defined once (architecture "Communication Patterns") and reused
  verbatim: 200/202→Accepted-processing(polite), 400/422→Validation(alert), 401→SignInRequired,
  403→Forbidden/TenantUnavailable, 404/410→Gone(polite), 408/timeout→TransientFailure(alert),
  ≥500→LoadFailure(alert), stale/degraded→Degraded(polite).
- **AR-Copy:** all user-facing copy routed through **localization resources** (one set per
  area); regulated GDPR microcopy centralized and auditable, never inlined.
- **AR-Generated:** projection/command UI comes from `[Projection]`/`[Command]` SourceTools
  generation; never hand-edit/commit generated output under `obj/**/generated`.

**GDPR backend behaviors (from `docs/gdpr-*.md`, constrain Admin/Consumer GDPR stories):**

- **AR-Gdpr-Export:** `PartyDataPortabilityPackage` includes party detail, contact
  channels, identifiers, consent records, restriction status, freshness metadata,
  processing-activity summaries, bounded audit metadata. Erased → status `Erased` (no
  payload); restricted → `RestrictedExported`; unavailable → `PersonalDataUnavailable`.
  **Export filenames derived from party id + UTC timestamp only** (no PII); logs use
  bounded metadata only.
- **AR-Gdpr-Erased:** erased parties are distinguishable from missing/inactive/restricted/
  key-failure via stable signals (`IsErased`, `ErasedAt`, status `Erased`); mutation
  attempts return `PartyErasureInProgress` rejection; list/search/picker **exclude erased
  entries or show an erased-only status**; responses never expose destroyed-key/crypto
  text, stale names, contact values, identifiers, or raw payloads.
- **AR-Gdpr-Records:** `GetProcessingRecords` returns only bounded audit metadata (no raw
  payloads, names, identifiers, or reason text); stable `summary` strings; erased parties
  retain processing records.
- **AR-Gdpr-Keys (out of UI scope / context):** tenant key rotation vs party key rotation
  vs crypto-shredding are distinct; crypto-shredding is terminal and irreversible — this
  underpins the "permanent once complete" erasure copy. **Production KMS is a pre-existing
  prerequisite** before real EU PII (only `LocalDevKeyStorageBackend` ships).

**Existing client surface (reused, not rebuilt):**

- **AR-Client:** `IPartiesCommandClient` (Create/Update*/Add/Remove/De-Reactivate),
  `IPartiesQueryClient` (`GetPartyAsync`, `ListPartiesAsync`, `SearchPartiesAsync` —
  display-name/Lexical mode only, fail-closed allowlist), and `IAdminPortalGdprClient`
  (erasure/restrict/consent/export/processing-records; two 501 stubs) already exist and
  are the building blocks. The browser talks **only** to the UI host; the UI never calls
  the `parties` actor host directly.

**Known gaps to resolve as scoped stories (from architecture gap analysis):**

- **AR-Gap-Binding:** the `party_id` claim/binding **provisioning mechanism** is undesigned
  and **gates the entire Consumer area** — needs a short onboarding/binding design story
  before Consumer implementation. Admin + host/self-scope plumbing are unaffected.
- **AR-Gap-D7:** the EventStore GDPR contract (erasure certificate / retry-verify) is a
  cross-submodule backend story gating FR-Admin-4's verification report.
- **AR-Gap-KMS:** production KMS for crypto-shredding (pre-existing prerequisite).

### UX Design Requirements

> First-class work items from `DESIGN.md` + the accessibility / regulated-language
> reviews. All critical/high review findings were **resolved in the spine**; these
> UX-DRs are about implementing the resolved contract faithfully and gating it.

**Design tokens & brand layer:**

- **UX-DR1 — AA-safe brand fill:** filled primary buttons (white text) bind to
  `--colorBrandBackground` (≈`#00767f`, AA-safe), **never** the raw teal `#0097A7`
  (3.51:1). Raw accent reserved for non-text use (active-nav stripe, tints, focus
  chrome). One primary action per view. (Resolves the screen-wide 1.4.3 critical.)
- **UX-DR2 — Status token pairs:** party/GDPR/freshness state colors map to Fluent 2
  `--colorStatus*Foreground1`-on-`--colorStatus*Background1` **token pairs** (never
  hand-mixed hex; warning-on-arbitrary-tint lands ~4.44:1). Verify light **and** dark.
- **UX-DR3 — Inheritance discipline:** no redeclaring Fluent 2 custom properties; theme
  only via `IThemeService`/token API; consumer body 16px (`--fontSizeBase400`) /
  line-height 1.5; Admin comfortable / Consumer roomy density via FrontComposer.

**Four domain components (the only specified visual deltas):**

- **UX-DR4 — Party-state badge:** `FluentBadge`(Tint) pill, color **+** text label
  (never color-alone) for `active/inactive/restricted/erased`; erased shows tombstone
  copy, not data.
- **UX-DR5 — Data-freshness indicator:** dot **+** word for fresh / stale ("as of
  HH:MM") / degraded ("showing last known"); its text node is a `role=status
  aria-live=polite` region so transitions are announced.
- **UX-DR6 — GDPR destructive button:** danger fill (`--colorStatusDangerForeground1`)
  + typed-name confirmation for irreversible actions; restrict/withdraw use
  `Outline`, not danger fill; never auto-fire on focus/blur.
- **UX-DR7 — Party picker re-skin + full ARIA combobox** (folds AR-D11): re-skin
  `--hx-picker-*` onto Fluent 2 tokens **and** implement input `role=combobox` +
  `aria-controls`/`aria-expanded`/`aria-activedescendant`, listbox `role=listbox`,
  options `role=option`+`id`+`aria-selected`, a `role=status` result count, 300ms
  debounce, `party-selected` event, and its full state machine.

**Accessibility implementation (consumer-facing WCAG 2.2 AA):**

- **UX-DR8 — Live-region politeness split:** status / freshness / accepted-processing →
  `role=status aria-live=polite`; **validation-rejected / transient / load-failure →
  `role=alert` (assertive)**. Never blanket-polite. (Wired via AR-StatusMap.)
- **UX-DR9 — Real semantics, no interactive `<div>`s:** consent control =
  `FluentSwitch` `role=switch`+`aria-checked` with purpose+lawful-basis tied via
  `aria-describedby`; Person/Org chooser = `FluentRadioGroup`; typed-erase confirm = a
  **real labeled `<input>`** with `aria-describedby` → irreversibility warning, Erase
  `aria-disabled` until the name matches (transition announced); grid rows
  `role=row/gridcell`.
- **UX-DR10 — Focus contract (per-surface):** skip links (to content, to nav) as the
  first two tab stops product-wide incl. Consumer; visible `--colorStrokeFocus2` ring on
  every control incl. the formerly-fake ones; trap/restore on dialogs; move focus to the
  alert on blocking errors; **announce via aria-live, do NOT steal focus** on routine
  optimistic saves; admin master-detail-as-sheet on phone moves focus into the sheet on
  open and restores to the originating row on back.
- **UX-DR11 — Non-color cues & targets:** active/selected affordances carry a non-color
  cue (border-weight/checkmark), not the 3.51:1 accent border alone; ≥24px AA targets
  with ≥44px touch slop (consent toggle + icon-only controls specifically, Consumer is
  phone-first); floor consumer secondary text at 13–14px; nothing conveyed only at ≤12px;
  survive 200% zoom / text-spacing.
- **UX-DR12 — Forced-colors + reduced-motion** supported **product-wide** (today only the
  picker honors them).

**Regulated-language / consumer-trust copy (GDPR honesty — all localized, AR-Copy):**

- **UX-DR13 — Erasure copy:** commit to the **start** ("We've started deleting your data…
  usually within 30 days… we'll confirm when it's done"), **never** a hard finish SLA;
  state both halves — cancellable until deletion begins, **permanent once complete**; the
  30-day figure is not the cancel window; erasure acknowledgement uses neutral/info tone,
  **never success-green**.
- **UX-DR14 — Lawful-basis honesty:** split "Things you control" (consent toggles,
  default Off) from "Things we keep to run your account" (contract / legitimate interest,
  read-only); offer **Object (Art.21)** for legitimate-interest bases, not a withdraw
  toggle; "Manage all consent →" links to the full My-consent surface (privacy card is a
  summary, with withdraw/grant parity).
- **UX-DR15 — Export copy:** no time promise ("Preparing your export — this can take a
  little while. We'll show it here the moment it's ready"); state machine-readable JSON;
  synchronous download is the happy case, not the promised baseline.
- **UX-DR16 — Plain verbs & single status source:** lead with "Delete my data" (name the
  right nearby), not "Erasure"; Admin terse / Consumer plain-and-reassuring register; one
  status source per action (never "Saved" + "Saving" together); rendered value identical
  to stored value across view↔edit.

### FR Coverage Map

- **FR-Shell** → Epic 1 — OIDC sign-in, role routing, fail-closed `party_id` binding
- **FR-Admin-1** → Epic 2 — Parties list (server-driven search + type/active filters)
- **FR-Admin-2** → Epic 2 — Party detail (full `PartyDetail`, entry to edit + GDPR)
- **FR-Admin-3** → Epic 2 — Create / Edit party (+ in-form picker, Person/Org radiogroup)
- **FR-Admin-4** → Epic 3 — GDPR/DPO ops (erase/restrict/consent/export/records/verification; + D7 backend)
- **FR-Consumer-1** → Epic 4 — My profile (view own data + freshness)
- **FR-Consumer-2** → Epic 4 — Edit my profile (validated correction)
- **FR-Consumer-3** → Epic 5 — My consent (opt-in default-Off, Art.21 Object)
- **FR-Consumer-4** → Epic 5 — My data & privacy (async JSON export, two-state erasure)

_All 9 FRs mapped. NFRs (esp. NFR1 accessibility, NFR2 eventual-consistency, NFR4 GDPR
honesty) and the UX-DRs thread through every epic's stories; the shared enablers
(domain components, a11y gate, StatusKind→UI map, SignalR freshness) are established
once in Epic 1._

## Epic List

### Epic 1: App Foundation & Secure Sign-In

Any authorized user signs in once and lands in the correct area for their role; a
consumer with no data binding lands safely (never on a data screen). Establishes the
shared security, freshness, accessibility, and shell foundation every later epic
consumes — host stand-up (AR-Starter/D1), OIDC (D5), role routing + `Consumer` policy +
fail-closed `party_id` resolution (D2), self-scope accessor + `IDataSubjectAccessService`
(D3), AppHost/deploy wiring (D10, with production-KMS gated as a deploy prerequisite),
the canonical `StatusKind→UI` map + aria-live split + SignalR live-freshness /
optimistic-reconcile pattern (D6/AR-StatusMap/UX-DR8), the 3 shared domain components
(UX-DR4/5/6), and the a11y gate scaffolding — bUnit + Playwright, forced-colors /
reduced-motion, skip-links / focus baseline, AA-safe brand-fill token (D9/UX-DR1/2/3/10/12).

**FRs covered:** FR-Shell

### Epic 2: Admin — Party Records Management

An admin / tenant-owner can search, filter, view, create, edit, and link Person &
Organization records within their tenant. Embeds the `AdminPortal` RCL; list/search/filter
(`FluentDataGrid`), detail, validated create/edit (Person/Org `radiogroup`), and the
party-picker re-skin + full ARIA combobox (D11/UX-DR7).

**FRs covered:** FR-Admin-1, FR-Admin-2, FR-Admin-3

### Epic 3: Admin — GDPR / DPO Operations

A DPO can fulfill data-subject obligations on any party — erase (typed confirm),
restrict / lift, record / revoke consent, export (Art.20), processing records (Art.30),
and prove erasure via the verification report. Wraps the existing AdminPortal GDPR panels
into `PartyGdprPage`; GDPR destructive button + typed-confirm; erased/restricted/export
backend behaviors (AR-Gdpr-*); and the D7 EventStore-contract backend story
(cross-submodule — gates the verification report).

**FRs covered:** FR-Admin-4

### Epic 4: Consumer — Identity Binding & My Profile

A consumer signs in, is bound fail-closed to exactly their own party, and can view and
correct their own personal data. Includes the `party_id` binding-provisioning **decision
(Story 4.1, ADR) + build (Story 4.2)** (AR-Gap-Binding) that gates the whole Consumer area;
`ConsumerPortal` RCL stand-up; My profile + Edit profile via the self-scoped accessor.

**FRs covered:** FR-Consumer-1, FR-Consumer-2

### Epic 5: Consumer — Consent, Data Export & Erasure

A consumer controls consent honestly (opt-in default-Off; Object for legitimate
interest), exports their data (async JSON), and requests / cancels erasure with honest
copy. Consent switch + lawful-basis split (UX-DR14), async export panel (UX-DR15),
two-state erasure panel (UX-DR13), plain-verbs / single-status (UX-DR16) — all in
`ConsumerPortal`.

**FRs covered:** FR-Consumer-3, FR-Consumer-4

### Epic Dependencies

`1 → {2, 4}` · `2 → 3` · `4 → 5`. Each epic is standalone once its predecessors ship.
Epic 3 additionally depends on the D7 EventStore contract (cross-submodule, approval
required); Epic 4 additionally depends on the AR-Gap-Binding provisioning design.
**Intra-epic sequencing:** Story 4.2 (binding build) depends on Story 4.1 (binding
decision / ADR); Story 3.6 (verification report UI) depends on Story 3.5 (D7 backend
contract, approval-gated).

### Out of MVP scope (tracked, not stories)

- **Production KMS** for crypto-shredding — a deployment prerequisite (Epic 1 deploy
  story gates it), not an MVP feature story.
- **Tenant key rotation** — backend/ops with no UI surface in this scope; noted as
  context underpinning the "permanent once complete" erasure copy.

---

## Epic 1: App Foundation & Secure Sign-In

Stand up the `parties-ui` host and deliver secure sign-in with role-based landing, plus
the shared security, freshness, accessibility, and shell foundation every later epic
consumes. **Covers FR-Shell** and establishes AR-Starter, D1, D2, D3, D5, D6, D10,
AR-StatusMap, UX-DR1/2/3/4/5/6/8/10/12, and the D9 a11y gate.

### Story 1.1: Stand up the Hexalith.Parties.UI Blazor Server host

As a developer,
I want a new FrontComposer shell-host project wired into the solution and AppHost,
So that there is a running `parties-ui` app to build the experience on.

**Acceptance Criteria:**

**Given** the FrontComposer shell-host pattern (`FrontComposer/samples/Counter/Counter.Web`)
**When** I create `src/Hexalith.Parties.UI` (`Microsoft.NET.Sdk.Web`, `net10.0`)
**Then** it wires the FrontComposer Quickstart chain + `AddFluentUIComponents` + `AddHexalithDomain<PartiesUiDomainMarker>()`, sets `ValidateScopes=true` (ADR-030), and is added to `Hexalith.Parties.slnx`
**And** references resolve via the computed sibling-root properties (no NuGet conversion of EventStore/Tenants).

**Given** the AppHost
**When** I add `builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")` referencing `eventstore` + `tenants`
**Then** the resource starts with **no DAPR sidecar** (BFF over HTTP/SignalR, like `parties-mcp`)
**And** `dotnet aspire run` shows `parties-ui` healthy once `eventstore`/`tenants` are healthy.

**Given** the solution build gate
**When** I build `Hexalith.Parties.slnx -c Release`
**Then** it succeeds with **0 warnings** under solution-wide `TreatWarningsAsErrors`, with no `Version=` in the csproj (Central Package Management) and no warning override.

### Story 1.2: Host-owned OIDC sign-in (server-side, tokens never reach the browser)

As a user,
I want to sign in through the app,
So that I can access my role's area securely.

**Acceptance Criteria:**

**Given** an unauthenticated request to any protected route
**When** the app challenges via `OpenIdConnect` (10.0.8) against Keycloak (run mode) / the `tache` realm (publish)
**Then** I complete sign-in and a **server-side cookie session** is established, and the **OIDC tokens never leave the server** (the browser holds no bearer token)
**And** the original URL is preserved as the return URL and I am returned to it after sign-in.

**Given** a signed-in session
**When** I sign out
**Then** the cookie session is cleared and the OIDC sign-out completes; the callback endpoint is the only extra host endpoint exposed.

**And** no secret is committed; OIDC config is `__`-nested env-overridable in `appsettings*.json`.

### Story 1.3: Role-based landing and policy-gated navigation

As a signed-in user,
I want to land in the area for my role with only my own navigation visible,
So that I see exactly what I am authorized to use.

**Acceptance Criteria:**

**Given** a signed-in user with role `Admin` or `TenantOwner`
**When** they reach the app entry
**Then** they land in the **Admin** area (`/admin`) and only Admin `<FluentNav>` entries render.

**Given** a signed-in user with role `Consumer`
**When** they reach the app entry
**Then** they land in the **Consumer** area (`/me`) and only Consumer nav entries render — Admin and Consumer nav **never cross-render**.

**Given** the authorization configuration
**When** the host boots
**Then** the existing `Admin` policy and a new **`Consumer`** policy are registered and `<AuthorizeView Policy=…>` gates every nav entry
**And** bUnit tests assert role→landing routing and that the opposite area's nav is absent.

### Story 1.4: Fail-closed `party_id` claim resolution

As the system,
I want to resolve a Consumer's `party_id` claim fail-closed,
So that a consumer without a binding never reaches a data screen.

**Acceptance Criteria:**

**Given** a Consumer principal carrying a verified `party_id` claim
**When** `PartyIdClaimResolver` runs (Scoped)
**Then** it resolves exactly one bound `party_id`, and the tenant claim is normalized via `PartiesClaimsTransformation`; the consumer's effective scope is `{tenant, party_id}`.

**Given** a Consumer principal with **no** (or an invalid) `party_id` claim
**When** resolution runs
**Then** the user is routed to a fail-closed `NoPartyBinding` onboarding/error state — **never** a data screen.

**And** bUnit tests cover the present-claim and absent-claim paths. _(The mechanism that issues the claim is AR-Gap-Binding — **decided** in Story 4.1 and **implemented** in Story 4.2; this story only consumes an existing claim, and its happy path is end-to-end verifiable once 4.2 lands.)_

### Story 1.5: Consumer own-data self-authorization (defense-in-depth)

As a security owner,
I want the Parties host to enforce that a Consumer can act only on their own party,
So that own-data-only holds even if the BFF is bypassed.

**Acceptance Criteria:**

**Given** a Consumer principal
**When** any consumer data operation is issued
**Then** it flows through the single `ISelfScopedPartiesClient` accessor, which injects the resolved `party_id` and only ever issues self-scoped operations (`GetPartyAsync(myPartyId)`, consumer GDPR commands on `myPartyId`) — a Consumer principal **never** calls list/search.

**Given** the Parties host
**When** a request reaches the domain for a Consumer principal
**Then** the fail-closed `IDataSubjectAccessService` asserts `aggregateId == party_id` and **denies** otherwise (defense-in-depth), and the `Consumer` policy is enforced server-side.

**And** a tripwire test fails if list/search is reachable for a Consumer, or if a singleton captures a Scoped accessor (`ValidateScopes=true` catches the latter at boot).

### Story 1.6: Canonical StatusKind→UI mapping with aria-live politeness split

As a developer,
I want one shared mapping from client outcomes to UI states with correct aria-live politeness,
So that every screen handles success, validation, failure, and staleness identically and accessibly.

**Acceptance Criteria:**

**Given** a `PartiesClientException` / client outcome
**When** it is mapped to UI state
**Then** the single canonical mapping applies (200/202→Accepted-processing; 400/422→Validation; 401→SignInRequired; 403→Forbidden/TenantUnavailable; 404/410→Gone; 408/timeout→TransientFailure; ≥500→LoadFailure; stale/degraded→Degraded) with **no per-screen remap**.

**Given** a mapped UI state
**When** it is announced
**Then** status/freshness/accepted-processing use `role="status" aria-live="polite"` and **validation-rejected / transient-failure / load-failure use `role="alert"` (assertive)** — never blanket-polite.

**And** bUnit tests assert each status code's UI state and its politeness.

### Story 1.7: Live freshness via SignalR + shared optimistic-reconcile effect

As a user,
I want my changes to appear immediately and reconcile silently,
So that eventual consistency is invisible and never alarming.

**Acceptance Criteria:**

**Given** a command issued from any screen
**When** the shared optimistic-then-reconcile effect runs
**Then** it dispatches optimistic state + `aria-live=polite` "Saving…", issues the command via the (self-scoped/tenant-scoped) client, and reconciles on **SignalR projection-confirm** (or `Freshness=Current`); on rejection it **reverts + shows an inline `role=alert` reason**.

**Given** the EventStore projection stream
**When** it is available
**Then** the UI subscribes via SignalR (`Hexalith.EventStore.SignalR` + `SignalR.Client` 10.0.8); when the stream is **degraded** it falls back to polling + freshness metadata, surfacing `X-Service-Degraded` / `X-Stale-Data-Age` into UI state.

**And** routine optimistic saves announce via aria-live **without stealing focus**; reconnect re-subscribes without duplicate application.

### Story 1.8: Shared domain components (party-state badge, freshness indicator, GDPR destructive button)

As a developer,
I want the three shared domain components built on FluentUI V5 with color-plus-label and token pairs,
So that both areas render state consistently and accessibly.

**Acceptance Criteria:**

**Given** a party lifecycle value
**When** `PartyStateBadge` renders (`FluentBadge` `Appearance.Tint`, `{rounded.full}`)
**Then** it shows `active/inactive/restricted/erased` as **color *and* a text label** (never color-alone), using matched `--colorStatus*Foreground1`-on-`Background1` **token pairs**, and an `erased` party shows tombstone copy, not data.

**Given** `ProjectionFreshnessMetadata`
**When** `DataFreshnessIndicator` renders
**Then** it shows a **dot + word** for fresh / stale ("as of HH:MM") / degraded ("showing last known"), and its text node is a `role="status" aria-live="polite"` region so transitions are announced.

**Given** an irreversible vs reversible GDPR action
**When** `GdprDestructiveButton` renders
**Then** irreversible uses the danger fill (`--colorStatusDangerForeground1`) with a typed-confirmation hook and reversible uses `Outline`; it never auto-fires on focus/blur.

**And** every component binds tokens via the design-token API (zero hard-coded hex), and bUnit asserts the color-plus-label and aria semantics.

### Story 1.9: Accessibility foundation and CI a11y gate (WCAG 2.2 AA)

As a quality owner,
I want product-wide a11y primitives and an enforced a11y gate,
So that WCAG 2.2 AA holds from the first screen, not as an afterthought.

**Acceptance Criteria:**

**Given** any page
**When** it renders
**Then** skip links (to content, to nav) are the **first two tab stops product-wide** (incl. Consumer), the `--colorStrokeFocus2` focus ring is visible and never suppressed, and `@media (forced-colors: active)` + `prefers-reduced-motion` are honored **app-wide**.

**Given** a filled primary button with white text
**When** it renders
**Then** it binds to `--colorBrandBackground` (AA-safe ≈`#00767f`) and **never** the raw teal `#0097A7` (3.51:1); the AA contrast gate is documented, replacing any "ratios hold" claim.

**Given** CI
**When** the pipeline runs
**Then** it gains the UI build + a **bUnit** component lane and a **Playwright a11y/visual gate** (WCAG 2.2 AA, FrontComposer e2e pattern) that fails the build on a violation.

### Story 1.10: Deploy parties-ui (container + K8s) with production-KMS prerequisite gate

As an operator,
I want `parties-ui` containerized and deployable to Kubernetes with OIDC config,
So that the app can ship, with the production-KMS prerequisite gated before real PII.

**Acceptance Criteria:**

**Given** the UI host
**When** it is published
**Then** .NET SDK container support builds it (`EnableContainer=true`, `ContainerRepository=parties-ui`, **no Dockerfile**), `ServiceDefaults` (OpenTelemetry + health) are wired, and `deploy/k8s` gains the `parties-ui` Deployment/Service/ingress + OIDC config; aspirate publish grows the cluster **11 → 12 pods**.

**Given** the deploy manifests
**When** `DeployValidation.Tests` runs its credential-leak poison-sweep
**Then** it passes with **no secrets/tokens** committed under `deploy/`.

**And** the runbook documents that **production KMS** must replace `LocalDevKeyStorageBackend` before any real EU PII is processed (per `deployment-security-checklist.md`) — this is a release gate, not an MVP feature.

---

## Epic 2: Admin — Party Records Management

An admin/tenant-owner can search, filter, view, create, edit, and link Person &
Organization records within their tenant. **Covers FR-Admin-1, FR-Admin-2, FR-Admin-3**;
delivers D11/UX-DR7 and threads NFR1/NFR2/NFR5. All work lives in `AdminPortal` + `Picker`.

### Story 2.1: Embed the Admin area behind the Admin policy

As an admin,
I want the Admin area mounted under `/admin`,
So that admin pages are reachable and protected.

**Acceptance Criteria:**

**Given** the UI host
**When** it references the `Hexalith.Parties.AdminPortal` RCL and mounts its pages
**Then** every `/admin/*` page carries `@attribute [Authorize(Policy="Admin")]`, the AdminPortal `State/` + `Resources/` are wired, and a "Parties" nav entry appears for Admin only.

**Given** a non-Admin (Consumer) principal
**When** they attempt `/admin/*`
**Then** access is denied with the role-needed explanation (Forbidden), never exposing record data.

### Story 2.2: Parties list with search, filters, and paging (FR-Admin-1)

As an admin,
I want to search and filter the parties list,
So that I can find a record quickly.

**Acceptance Criteria:**

**Given** the parties list at `/admin/parties`
**When** I type into search
**Then** a **debounced**, server-driven display-name search runs (`SearchPartiesAsync`, Lexical/DisplayName mode only — fail-closed allowlist), with Person/Organization **type** and **active** filters via `FluentSelect` and paging, and a row click opens `/admin/parties/{id}`.

**Given** a stale/degraded read
**When** the list renders
**Then** it shows the **last-known** rows with the freshness indicator and **never blocks or blanks**; a cold load shows a skeleton (no spinner-only screen).

**Given** no matches
**When** the list resolves empty
**Then** it shows "No parties match." + a clear-filters action (never a dead end).

**And** erased parties are excluded or shown only as an erased status; arrow-key row navigation + `Enter` opens detail; type-ahead focuses search.

### Story 2.3: Party detail (FR-Admin-2)

As an admin,
I want to view a party's full detail,
So that I can review the record and decide what to do.

**Acceptance Criteria:**

**Given** a party id
**When** I open `/admin/parties/{id}`
**Then** the full `PartyDetail` renders with the party-state badge and freshness indicator, and entry buttons to **Edit** and **GDPR**.

**Given** a partial projection (`DisplayNameOnly`)
**When** detail renders
**Then** it shows the name and marks the rest "still loading" — it does not imply the record is empty.

**Given** an erased or missing party (`Gone`/`NotFound`)
**When** detail resolves
**Then** it shows a **tombstone** ("This party was erased.") with **no personal fields and no PII** in the message.

**Given** a phone-width viewport (NFR5 master-detail reflow — the highest-reflow-risk surface)
**When** I open a party from the list
**Then** the desktop two-pane master-detail **collapses to a sheet / full-screen detail**, focus **moves into the sheet** on open and **returns to the originating row** on back/close (UX-DR10 focus contract); content reflows to a single column with no loss at 320px width / 200% zoom, and a non-color cue marks the active row.

### Story 2.4: Create and edit a party with validation (FR-Admin-3)

As an admin,
I want to create and edit parties with validation,
So that I can author correct records.

**Acceptance Criteria:**

**Given** `/admin/parties/new` or `/admin/parties/{id}/edit`
**When** I author the form
**Then** the Person/Organization chooser is a `FluentRadioGroup` (`role=radiogroup`), fields are validated, and submit issues `CreateParty(Composite)` / `Update*` commands with the **route id authoritative**.

**Given** a validation failure
**When** the gateway returns `PartyCommandValidationRejected`
**Then** the inline field error is announced via `role=alert`, tied to the field via `aria-describedby`, and the user's input is preserved with a retry offered (no exception surfaced).

**Given** a successful submit
**When** the command is accepted
**Then** the view reflects the change **optimistically** with a `role=status` "Saved — updating…" and reconciles silently on projection confirm.

### Story 2.5: Party picker re-skin + full WAI-ARIA combobox (D11 / UX-DR7)

As an admin,
I want to link a related party via an accessible picker,
So that I can capture relationships inline by keyboard or pointer.

**Acceptance Criteria:**

**Given** the existing `<hexalith-party-picker>` (legacy FAST tokens)
**When** it is re-skinned
**Then** its `--hx-picker-*` vars map onto Fluent 2 tokens (`--colorNeutralStroke1`, `--colorNeutralBackground1`, `--colorNeutralForeground1`, `{colors.accent}`, `--colorStatusDangerForeground1`) and the legacy `--*-fill-rest` tokens are gone.

**Given** the picker combobox
**When** a screen-reader user operates it
**Then** the input has `role="combobox"` + `aria-controls` + `aria-expanded` + `aria-activedescendant`, the listbox has `role="listbox"`, options have `role="option"` + `id` + `aria-selected`, and a `role="status"` announces the result count; `↓/↑` move the active option, `Enter` selects, `Esc` closes, `Backspace` on empty clears.

**Given** a selection in the Create/Edit form
**When** the user picks a party (after the 300ms debounce)
**Then** the picker emits `party-selected {partyId, partyType, status}` and the form binds the link; degraded/`LocalOnly`/`Gone` states are honored and never block, with a non-color active cue and forced-colors/reduced-motion support.

---

## Epic 3: Admin — GDPR / DPO Operations

A DPO can fulfill data-subject obligations on any party and prove erasure. **Covers
FR-Admin-4**; delivers AR-Gdpr-Export/Erased/Records, UX-DR6/UX-DR9, and the D7 EventStore
contract — split into an approval-gated backend story (**3.5**) and the report UI that consumes
it (**3.6**). Wraps existing AdminPortal GDPR panels; uses `IAdminPortalGdprClient`.

### Story 3.1: GDPR operations page

As a DPO,
I want a single GDPR page on a party,
So that I can reach all data-subject operations in one place.

**Acceptance Criteria:**

**Given** a party detail
**When** I click GDPR
**Then** `/admin/parties/{id}/gdpr` opens (Authorize `Admin`), wrapping the existing AdminPortal GDPR panels and driving them via `IAdminPortalGdprClient`.

**Given** any GDPR operation outcome
**When** it returns
**Then** the `AdminPortalGdprOutcome` (Accepted/Completed/ValidationRejected/Forbidden/…) maps through the canonical StatusKind→UI states with the correct politeness.

### Story 3.2: Erase a party with typed-name confirmation

As a DPO,
I want to erase a party behind a typed-name confirmation,
So that I never erase accidentally and the action is honest.

**Acceptance Criteria:**

**Given** the Erase action
**When** I trigger it
**Then** a `FluentDialog` (`role="dialog" aria-modal="true" aria-labelledby`) opens with a **real labeled `<input>`** typed-confirm tied via `aria-describedby` to the irreversibility warning; the danger-fill Erase button is `aria-disabled` until the **typed name matches**, and that enable transition is announced.

**Given** a matching typed name
**When** I confirm
**Then** `RequestErasureAsync` (`EraseParty`) is issued, the typed name is compared **in-memory only** (never logged/telemetered), the party-state badge flips toward `erased` with "Saved — updating…" and freshness `degraded`, and the acknowledgement uses a **neutral/info tone — never success-green**.

**And** all dialog copy is PII-free; modal depth stays ≤1; native `alert/confirm` are not used.

### Story 3.3: Restrict / lift restriction and record / revoke consent

As a DPO,
I want to restrict or lift processing and record or revoke consent on a party,
So that I can manage lawful processing.

**Acceptance Criteria:**

**Given** the GDPR page
**When** I restrict or lift restriction
**Then** `RestrictProcessingAsync` / `LiftRestrictionAsync` are issued from `Outline` (reversible) buttons with a single confirm, and the party-state badge reflects `restricted` optimistically, reconciling on confirm.

**Given** a consent record/revoke action
**When** I submit it
**Then** `AddConsentAsync` (`RecordConsent`) / `RevokeConsentAsync` are issued, the outcome is mapped, and all copy is localized (no inline strings).

### Story 3.4: Data export (Art.20) and processing records (Art.30)

As a DPO,
I want to export a party's data and view its processing records,
So that I can fulfill portability and accountability obligations.

**Acceptance Criteria:**

**Given** an authorized DPO/admin
**When** I export a party
**Then** `ExportPartyDataAsync` produces a `PartyDataPortabilityPackage` (machine-readable JSON) download whose filename derives from **party id + UTC timestamp only** (no tenant id, display name, contact value, identifier, or reason text), and logs carry **bounded metadata only**.

**Given** party state at export time
**When** the package is produced
**Then** a restricted party returns `RestrictedExported` (still exportable for an authorized DPO), an erased party returns `Erased` with **no `Party` payload**, and unavailable data returns `PersonalDataUnavailable` with no partial payload.

**Given** the processing-records view
**When** `GetProcessingRecordsAsync` returns
**Then** it shows **bounded audit metadata only** (`partyId, tenantId, sequenceNumber, eventType, operationCategory, timestamp, actorId, correlationId, outcome, summary`) with stable summaries and **no raw payloads, names, identifiers, or reason text**; erased parties retain their records.

### Story 3.5: EventStore erasure-verification contract (backend, cross-submodule — approval-gated)

As an EventStore maintainer,
I want a defined contract for erasure certification and verification retry,
So that the Parties tier can prove a party was shredded across projections instead of stubbing it.

**Acceptance Criteria:**

**Given** explicit approval for the cross-submodule change
**When** the EventStore contract for `GetErasureCertificate` / `RetryErasureVerification` is defined and the Parties-side wiring implemented
**Then** the inert **501 stubs are replaced**, `ContractUnavailable` no longer faults, and `IAdminPortalGdprClient` exposes a real erasure-certificate result the UI can consume.

**Given** an erased party
**When** the certificate is produced
**Then** it carries stable erased/verification state **without** exposing destroyed-key/cryptographic-exception text, stale display names, contact values, identifiers, or raw payloads.

**And** this story is explicitly **gated on cross-submodule approval** and sequenced as a **predecessor to Story 3.6**; the rest of Epic 3 ships without either. _(AR-D7 / AR-Gap-D7.)_

### Story 3.6: Admin erasure-verification report (UI — consumes the D7 contract)

As a DPO,
I want a verification report proving a party was shredded across projections,
So that I can prove the right to erasure was honored, not merely assert it.

**Acceptance Criteria:**

**Given** the D7 contract from Story 3.5 is in place
**When** I open the erasure-verification report for an erased party
**Then** the Admin report shows the record **confirmed shredded across projections**, mapping `GetErasureCertificate` / `RetryErasureVerification` outcomes through the canonical StatusKind→UI states with the correct politeness.

**Given** the D7 contract has **not** yet landed
**When** the report surface is reached
**Then** it degrades to a clear "verification not yet available" state (no fault, no PII), and the rest of the GDPR page remains fully usable.

**Given** an erased party
**When** the report renders
**Then** it shows stable erased/verification state **without** exposing destroyed-key/cryptographic-exception text, stale display names, contact values, identifiers, or raw payloads.

---

## Epic 4: Consumer — Identity Binding & My Profile

A consumer signs in, is bound fail-closed to their own party, and views/corrects their
own data. **Covers FR-Consumer-1, FR-Consumer-2**; resolves AR-Gap-Binding and stands up
`ConsumerPortal`. All consumer data flows through `ISelfScopedPartiesClient`.

### Story 4.1: Decide the Consumer identity → `party_id` binding mechanism (design spike → ADR)

As a product owner / architect,
I want a decided, recorded mechanism for binding a consumer's identity to their party,
So that the Consumer area can be estimated and built against a known design.

**Acceptance Criteria:**

**Given** the undesigned binding gap (AR-Gap-Binding) that blocks Epics 4–5
**When** the options — **admin-link · self-registration · IdP federation** — are evaluated against tenancy, fail-closed resolution, provisioning effort, and where the verified `party_id` is held (IdP claim and/or a small binding store — **never the event stream**)
**Then** exactly one mechanism is **selected** and the decision, its alternatives, and trade-offs are **recorded in an ADR**, including the binding-store shape (if any) and the provisioning/onboarding flow.

**Given** the recorded decision
**When** Story 4.2's acceptance criteria are written
**Then** they are derived directly from the chosen option (no open design questions remain), and the ADR is referenced as the source.

**And** this is a **decision spike**, not an implementation story; it produces a decision artifact only and is the **predecessor of Story 4.2** and all of Epics 4–5. _(Resolves readiness finding M1.)_

### Story 4.2: Implement the chosen `party_id` binding-provisioning mechanism

As a product owner,
I want the binding mechanism chosen in Story 4.1 implemented end-to-end,
So that consumers can be provisioned and the Consumer area becomes reachable.

**Acceptance Criteria:**

**Given** the mechanism selected in Story 4.1 (ADR)
**When** it is implemented
**Then** a verified `party_id` claim is issued and/or stored per the ADR (IdP claim and/or binding store — **never in the event stream**), and a consumer can be provisioned through the defined flow.

**Given** a newly provisioned consumer
**When** they sign in
**Then** their `party_id` claim resolves (consumed by Story 1.4) and they reach `/me`; an **unbound** consumer sees the `NoPartyBinding` onboarding/error UX rather than any data screen.

**And** integration tests cover a bound consumer (reaches `/me`) and an unbound one (fails closed). _This story unblocks the rest of Epic 4 and Epic 5._

### Story 4.3: Stand up the ConsumerPortal RCL and Consumer area

As a consumer,
I want the Consumer area mounted under `/me`,
So that my self-service pages are reachable and protected.

**Acceptance Criteria:**

**Given** the new `Hexalith.Parties.ConsumerPortal` RCL (`Microsoft.NET.Sdk.Razor`, mirroring AdminPortal)
**When** it is referenced and mounted by the host
**Then** every `/me/*` page carries `@attribute [Authorize(Policy="Consumer")]`, the area renders at **roomy** density with **16px** body type, and a `Resources/` set holds the regulated GDPR microcopy (localized, never inlined).

**Given** the solution build
**When** it runs
**Then** the RCL builds green (references Client, Contracts, FrontComposer.Shell) and its Consumer nav entries render for Consumers only.

### Story 4.4: My profile (FR-Consumer-1)

As a consumer,
I want to see my own personal data and how fresh it is,
So that I know what is held about me.

**Acceptance Criteria:**

**Given** a signed-in, bound consumer
**When** they open `/me`
**Then** `MyProfilePage` fetches data **only** via `ISelfScopedPartiesClient.GetPartyAsync(myPartyId)` (never list/search), renders a calm roomy layout, and shows a freshness dot reading "Up to date" when fresh.

**Given** a stale/degraded read
**When** the profile renders
**Then** it shows last-known data with the freshness cue and never blanks/throws; an erased self shows a PII-free tombstone.

**And** no PII appears in logs/telemetry for any profile read.

### Story 4.5: Edit my profile (FR-Consumer-2)

As a consumer,
I want to correct my own data,
So that what is held about me is accurate.

**Acceptance Criteria:**

**Given** `/me/edit`
**When** I edit and save
**Then** the validated `Update*` command is issued via the self-scoped accessor, and the **prefilled value is identical to the stored value** across view↔edit (no silent drift).

**Given** a validation failure
**When** `PartyCommandValidationRejected` returns
**Then** the inline error is announced via `role=alert`, the input is preserved, and exactly **one status source** shows (never "Saved" and "Saving" simultaneously).

**Given** a successful save
**When** the command is accepted
**Then** the change shows optimistically and reconciles on confirm, announced via `aria-live` **without stealing focus**.

---

## Epic 5: Consumer — Consent, Data Export & Erasure

A consumer controls consent honestly, exports their data, and exercises erasure with
honest copy. **Covers FR-Consumer-3, FR-Consumer-4**; delivers UX-DR13/14/15/16 and
NFR4. All work lives in `ConsumerPortal`, self-scoped, copy localized.

### Story 5.1: My consent — grant / withdraw with honest lawful-basis split (FR-Consumer-3)

As a consumer,
I want to control my consent honestly,
So that I decide what I am opted into without being misled.

**Acceptance Criteria:**

**Given** `/me/consent`
**When** the consent controls render
**Then** each is a `FluentSwitch` (`role=switch` + `aria-checked`) with its purpose **and lawful basis** tied via `aria-describedby`, and every consent toggle **defaults Off, never pre-checked** (Art. 7).

**Given** the data groups
**When** the page renders
**Then** "**Things you control**" (consent toggles) is visually and verbally separated from "**Things we keep to run your account**" (contract / legitimate-interest, read-only), and legitimate-interest items offer an **Object (Art. 21)** action — **not** a withdraw toggle.

**Given** a consent flip
**When** I toggle it
**Then** the switch flips optimistically with an `aria-live=polite` "Saving…" (**no focus steal**), issues `RecordConsent`/`RevokeConsent` via the self-scoped accessor, reconciles on confirm; on rejection it **reverts with an inline `role=alert` reason** and preserves intent. Withdraw is as easy as grant.

### Story 5.2: My data & privacy — export my data (FR-Consumer-4)

As a consumer,
I want to export my own data,
So that I can keep it or move it elsewhere.

**Acceptance Criteria:**

**Given** `/me/privacy`
**When** I request an export
**Then** `ExportPartyData` (self-scoped) produces a **machine-readable JSON** package, and the copy makes **no time promise** ("Preparing your export — this can take a little while. We'll show it here the moment it's ready").

**Given** an async export job
**When** it completes
**Then** a SignalR/poll signal surfaces an **in-app download** when ready; the synchronous download is the happy case, not a promised baseline.

**Given** the export service is unreachable
**When** the request fails transiently
**Then** a `TransientFailure` message ("Your data is safe — try again.") with retry/backoff shows, and the request is not lost; the filename derives from party id + UTC timestamp only.

### Story 5.3: My data & privacy — request / cancel erasure (FR-Consumer-4)

As a consumer,
I want to delete my data with honest, reversible-until-it-starts copy,
So that I understand exactly what will happen.

**Acceptance Criteria:**

**Given** the erasure control
**When** it renders
**Then** the primary verb is "**Delete my data**" (with the right named nearby) at danger-**outline** weight, not "Erasure", and the acknowledgement uses a **neutral/info tone — never success-green**.

**Given** an erasure request
**When** its state is shown
**Then** two honest states are presented: **(a) cancellable** until deletion begins ("You can cancel until deletion begins") and **(b) permanent** once complete ("Once it's done, it's permanent — we can't undo it"); the 30-day figure is **not** presented as the cancel window.

**Given** a cancellation before deletion begins
**When** I cancel
**Then** the request is cancelled via the self-scoped path; all copy stays PII-free and a single status source is shown.

### Story 5.4: My data & privacy — see what's processed about me (FR-Consumer-4)

As a consumer,
I want to see what is processed about me,
So that I have transparency over my data.

**Acceptance Criteria:**

**Given** `/me/privacy`
**When** the processing summary renders
**Then** it is sourced from `GetProcessingRecords` (self-scoped) showing **bounded audit metadata only** (no raw payloads/PII), in plain language, with freshness shown.

**Given** the privacy card
**When** I follow "Manage all consent →"
**Then** it deep-links to the full `/me/consent` surface (the privacy card is a **summary**), where withdraw/grant parity exists.

**And** an erased self retains its processing records (audit metadata needs no decrypted personal data).
</content>
</invoke>
