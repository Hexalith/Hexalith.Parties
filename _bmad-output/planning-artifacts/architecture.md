---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments:
  # UX design set (primary driver — scope: realize the new parties-ui experience)
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/validation-report.md
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-accessibility.md
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md
  # Brownfield baseline (requirements basis, per docs/index.md brownfield note)
  - docs/index.md
  - docs/project-overview.md
  - docs/architecture.md
  - docs/data-models.md
  - docs/api-contracts.md
  - docs/development-guide.md
  # Recent change record
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-09.md
  # Project context (persistent facts — AI agent rules)
  - _bmad-output/project-context.md
  - Hexalith.EventStore/_bmad-output/project-context.md
  - Hexalith.Tenants/_bmad-output/project-context.md
  - Hexalith.FrontComposer/_bmad-output/project-context.md
  - Hexalith.Memories/_bmad-output/project-context.md
referenceDocsAvailable:
  # Loaded on-demand during decision steps (not yet read in full)
  - docs/getting-started.md
  - docs/source-tree-analysis.md
  - docs/component-inventory.md
  - docs/event-subscribing.md
  - docs/event-handler-patterns.md
  - docs/event-publishing.md
  - docs/tenant-access-projection.md
  - docs/frontend/party-picker.md
  - docs/gdpr-erased-party-status.md
  - docs/gdpr-key-rotation-and-shredding.md
  - docs/gdpr-portability-export.md
  - docs/gdpr-processing-activity-records.md
  - docs/deployment-guide.md
  - docs/kubernetes-deployment-architecture.md
  - docs/deployment-security-checklist.md
  - docs/deferred-search-and-temporal-queries.md
  - docs/memories-backed-party-search.md
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-rubric.md
  - _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/mockups/ (5 HTML mockups)
workflowType: 'architecture'
scope: 'Realize the parties-ui experience (ux-parties-2026-06-09): single Blazor app, two role-gated areas (Admin records management + Consumer GDPR self-service) on FrontComposer + FluentUI V5, extending the existing event-sourced / CQRS / EventStore-gateway-fronted Parties system.'
requirementsBasis: 'Brownfield docs/ + UX design (no formal PRD exists — per docs/index.md brownfield note).'
project_name: 'parties'
user_name: 'Administrator'
date: '2026-06-09'
lastStep: 8
status: 'complete'
completedAt: '2026-06-09'
---

# Architecture Decision Document — Parties UI

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

**Scope:** Design the architecture to realize the **`parties-ui`** experience
(`ux-parties-2026-06-09`) — a single responsive Blazor app with two role-gated
areas (**Admin** records management + **Consumer** GDPR self-service) on the
FrontComposer shell + FluentUI Blazor V5 — extending the existing event-sourced,
CQRS, EventStore-gateway-fronted Parties domain service.

**Requirements basis:** brownfield `docs/` set + the UX design (no formal PRD;
following the brownfield note in `docs/index.md`).

## Project Context Analysis

### Requirements Overview

**Functional Requirements (derived from UX `EXPERIENCE.md` — no formal PRD):**

A single responsive Blazor app, `parties-ui`, with one sign-in and two
role-gated areas. Nav auto-populates from domain manifests, gated by
`<AuthorizeView Policy=…>`.

- **FR-Shell:** Authenticate; route to landing area by role (Admin/TenantOwner →
  Admin; Consumer → Consumer); preserve return URL on `SignInRequired`.
- **FR-Admin-1:** Parties list — server-driven, debounced search + type/active
  filters; row → detail; render last-known on staleness (never block).
- **FR-Admin-2:** Party detail — full `PartyDetail`; entry to edit + GDPR.
- **FR-Admin-3:** Create / Edit party — validated → command; in-form
  `<hexalith-party-picker>` to link a related party.
- **FR-Admin-4:** GDPR operations (DPO) — erase (typed-name confirm) · restrict /
  lift · consent record/revoke · Art.20 export · Art.30 processing records ·
  erasure verification report.
- **FR-Consumer-1:** My profile — view own personal data + freshness.
- **FR-Consumer-2:** Edit my profile — correct/update own data (validated).
- **FR-Consumer-3:** My consent — grant/withdraw; opt-in default-Off; Object
  (Art.21) for legitimate-interest bases; optimistic-then-reconcile.
- **FR-Consumer-4:** My data & privacy — export own data (async, JSON) · request
  erasure (cancellable-until-start, permanent-once-complete) · see what's
  processed about me.

**Non-Functional Requirements (drive the architecture):**

- **Accessibility — WCAG 2.2 AA (consumer-facing):** live-region politeness split
  (status/freshness = polite; validation/failure = assertive `role=alert`); real
  ARIA semantics (combobox, switch, radiogroup, labeled typed-confirm); per-surface
  focus contract (trap/restore on dialogs, move-to-alert on blocking errors,
  announce-not-steal on optimistic saves); forced-colors + reduced-motion
  product-wide; color-never-alone; ≥24px (≥44px touch) targets; AA contrast gate
  (filled primary → `--colorBrandBackground`, never raw teal `#0097A7` @ 3.51:1).
- **Eventual consistency is first-class UX:** surface `ProjectionFreshnessMetadata`
  (fresh/stale/degraded) and the `StatusKind` / `PartyPickerSearchState` machines;
  optimistic echo + silent reconcile; render last-known cache, never blank/throw;
  fail-closed tenant warm-up = "still warming up," not "access denied."
- **Security / privacy:** Consumer is scoped to **their own party only**;
  no PII in logs/telemetry/copy; admin typed-name confirmation compared in-memory.
- **GDPR honesty:** consent opt-in (default Off, never pre-checked); erasure copy
  commits to the *start* (Art.12(3)), states completed-erasure is permanent;
  Art.21 Object for non-consent bases; Art.20 export machine-readable + async.
- **Responsive:** Admin desktop-first master-detail (degrades to sheet/full-screen);
  Consumer phone-first single column. One codebase, two density postures.
- **Multi-tenancy:** Admin operates within tenant scope; isolation preserved.
- **Brand discipline:** inherit FluentUI V5 (Fluent 2) + FrontComposer shell
  wholesale; specify brand-delta only (consumer 16px body, roomier density, 4
  domain components).

### Scale & Complexity

- Primary domain: **full-stack** (new Blazor frontend tier + browser-reachable
  surface/BFF over the EventStore gateway + targeted backend extensions).
- Complexity level: **High → enterprise** — regulated (GDPR) + multi-tenant +
  eventually-consistent + a new consumer authorization model + WCAG 2.2 AA + a
  net-new frontend tier and area.
- Estimated architectural components (to decide in later steps): the `parties-ui`
  app + shell composition, an authenticated browser-reachable surface / BFF,
  Consumer identity→party scoping & authorization, GDPR stub completion
  (erasure certificate / retry-verify), portability-export delivery, the
  re-skinned + ARIA-correct party picker.

### Technical Constraints & Dependencies

- **Gateway-fronted, no public API on the actor host** — only `eventstore →
  POST /process` (DAPR deny-by-default). A browser app needs an authenticated,
  reachable surface; the typed `Hexalith.Parties.Client`
  (`IPartiesCommandClient`/`IPartiesQueryClient`/`IAdminPortalGdprClient`) is the
  building block. Public command/query traffic enters EventStore
  (`Domain="party"`); acceptance is **not** read-your-write.
- **FrontComposer + FluentUI Blazor V5** (`5.0.0-rc.3`) — Fluxor single-writer
  per slice (ADR-007), scoped-lifetime discipline (ADR-030), generated components
  from `[Projection]`/`[Command]` source generator (don't hand-edit generated
  code), custom inline-SVG icons (no FluentUI icons NuGet), ULIDs (never GUID) for
  message/correlation ids.
- **Platform/domain split** — domain modules stay domain-centric; shared
  scaffolding belongs in EventStore/FrontComposer/Commons, not in Parties.
- **Existing authz is tenant-role RBAC** (`TenantReader/Contributor/Owner` →
  Read/Write/Admin) via a fail-closed, eventually-consistent, **projection-side-only**
  tenant-access projection — no "Consumer" data-subject role or own-data scoping
  exists yet.
- **GDPR backend is mostly present but**: `GetErasureCertificateAsync` /
  `RetryErasureVerificationAsync` are inert 501 stubs pending an EventStore
  contract; crypto-shredding is ON by default with only `LocalDevKeyStorageBackend`
  (in-memory, dev-only — production KMS gap).
- **Search is fail-closed allowlisted** to `Lexical`/`DisplayName` in MVP;
  temporal name-as-of queries do not exist (reserved).
- **`<hexalith-party-picker>` design debt** — currently styled against legacy FAST
  tokens; must be re-skinned to Fluent 2 tokens **and** given full WAI-ARIA
  combobox semantics.
- **Build/quality gates** — .NET 10, Central Package Management (no `Version=` in
  csproj), solution-wide `TreatWarningsAsErrors`, `.slnx` only, root-level
  submodules only (never `--recursive`), Conventional Commits.

### Cross-Cutting Concerns Identified

1. **Consumer authentication & own-data authorization** (the dominant new concern)
   — identity → partyId binding, a Consumer role/policy, and enforcement that a
   consumer can read/act on *only* their own party.
2. **Eventual-consistency propagation** — freshness metadata + status machine flow
   intact from projection → gateway → UI; optimistic/reconcile; degraded headers.
3. **Accessibility (WCAG 2.2 AA)** — semantics, politeness split, focus, contrast
   gate, forced-colors/reduced-motion — spanning every surface and component.
4. **GDPR compliance & plain-language honesty** — consent, erasure lifecycle,
   portability export, processing records, Art.21 Object — across Admin + Consumer.
5. **Privacy / PII hygiene** — logs, telemetry, copy, tombstones, in-memory-only
   typed-name confirmation.
6. **Multi-tenancy isolation** — Admin within tenant scope; fail-closed warm-up.
7. **Observability** — OpenTelemetry; `X-Service-Degraded` / `X-Stale-Data-Age`
   surfaced to the UI.
8. **Theming & brand discipline** — inherit-wholesale, brand-delta-only; theme via
   design-token API, never hard-coded.

## Starter Template Evaluation

### Primary Technology Domain

**Full-stack — brownfield .NET 10 extension.** The new tier is a Blazor web host
(`parties-ui`) on the **FrontComposer shell + FluentUI Blazor V5**, talking to the
existing EventStore-gateway-fronted Parties backend. This is **not** a greenfield
project: the stack, framework, language, build system, and component library are
fixed by the existing solution; versions are centrally pinned in
`Directory.Packages.props` (re-verified 2026-06-09). No external CLI starter
(Next.js / T3 / Vite, etc.) is applicable or permitted.

### Starter Options Considered

| Option | Verdict |
|---|---|
| Generic JS starter (Next.js/T3/Vite/SvelteKit) | **Rejected** — wrong ecosystem; violates the FluentUI/Blazor/FrontComposer brand & architecture discipline and the C#/.NET 10 stack. |
| Plain `dotnet new blazor` host | **Rejected as-is** — would miss the FrontComposer shell wiring (nav/theme/density/command-palette/skip-links), Fluxor single-writer discipline, generated-component pipeline, and EventStore client integration. |
| **FrontComposer shell host pattern (reference: `Hexalith.FrontComposer/samples/Counter/Counter.Web`)** | **Selected** — the canonical, in-repo way to stand up a standalone FrontComposer + FluentUI V5 app; gives the entire UX-required shell surface for free. |
| Promote `AdminPortal` RCL into a host | **Partial** — `AdminPortal` is an embeddable RCL and stays one; the new host **references/embeds** it for the Admin area rather than being replaced by it. |

### Selected Starter: FrontComposer shell-host pattern (`Counter.Web` reference)

**Rationale for Selection:**
The FrontComposer shell supplies exactly the surface the UX `EXPERIENCE.md` assumes —
`<FluentLayout>`, `<FluentNav>` auto-populated from domain manifests and gated by
`<AuthorizeView Policy=…>`, Light/Dark/System theme, density switch, `Ctrl+K`
command palette, and skip links. FluentUI V5 (Fluent 2) supplies the components the
`DESIGN.md` inherits wholesale. Building on this pattern means the brand-delta work
(consumer density/type, the 4 domain components) is all that remains UI-side, and
the existing `AdminPortal` RCL drops in for the Admin area.

**Initialization (should be the first implementation story):**

```bash
# New standalone Blazor Server host, modeled on FrontComposer/samples/Counter/Counter.Web
dotnet new web -n Hexalith.Parties.UI -o src/Hexalith.Parties.UI   # then convert wiring to the FrontComposer pattern
dotnet sln Hexalith.Parties.slnx add src/Hexalith.Parties.UI
# References: FrontComposer.Shell (+ .Mcp dev), Parties.AdminPortal, Parties.Client,
#   Parties.Contracts, Tenants.Client/Contracts; SourceTools analyzer (netstandard2.0)
# Aspire resource name: "parties-ui" (mirrors eventstore-admin-ui)
```

`Program.cs` wiring (per the Counter.Web reference):
```csharp
builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true); // ADR-030
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(/* Parties domain marker */));
builder.Services.AddFrontComposerDevMode(builder.Environment);
builder.Services.AddHexalithDomain<PartiesUiDomainMarker>();
// + AddPartiesClient(config) + auth (OIDC/Keycloak) + EventStore client wiring (decided in step-04)
```

**Architectural Decisions Provided by the Starter (inherited, not re-decided):**

- **Language & Runtime:** C# / .NET 10 (`net10.0`, SDK `10.0.300`); `Microsoft.NET.Sdk.Web` host; **Interactive Server** render mode (the Counter.Web / eventstore-admin-ui precedent — WASM vs Server confirmed in step-04).
- **Styling:** FluentUI V5 (Fluent 2) design tokens via the shell; theme through `IThemeService` / design-token API; **brand-delta only** (no Tailwind/CSS framework; never hard-code or redeclare Fluent custom properties).
- **Build Tooling:** .NET SDK build under solution-wide `TreatWarningsAsErrors`; Central Package Management (no `Version=` in csproj); FrontComposer **SourceTools** incremental generator wired as an analyzer (generates Feature/Actions/Reducers/Registration + command forms from `[Projection]`/`[Command]`; never hand-edit generated output).
- **State Management:** Fluxor (`Fluxor.Blazor.Web`) — single-writer-per-slice discipline (ADR-007); effects own persistence/JS interop, reducers pure; scoped-lifetime discipline (ADR-030, `ValidateScopes=true`).
- **Routing / Nav:** shell `<FluentNav>` from registered domain manifests, gated by `<AuthorizeView Policy>` — Admin vs Consumer nav never cross-render.
- **Testing:** xUnit v3 + Shouldly + NSubstitute; **bUnit** for components; Playwright a11y/visual gate available from the FrontComposer pattern if we enforce the WCAG 2.2 AA consumer gate (decided in step-04).
- **Dev Experience:** `dotnet aspire run` orchestration; new `parties-ui` Aspire resource referencing `eventstore` (gateway) + `tenants`; FrontComposer dev mode + MCP enabled in Development/Test.

**Note:** Standing up `Hexalith.Parties.UI` from this pattern (project + `.slnx` + AppHost
wiring + the FrontComposer quickstart chain) should be the **first implementation story**.

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (block implementation):**
- D1 Render/hosting model · D2 Consumer identity→party binding · D3 Own-data
  authorization · D4 Consumer-area packaging · D5 UI→gateway transport & auth flow.

**Important Decisions (shape the architecture):**
- D6 Live-freshness mechanism · D7 GDPR-stub completion (erasure verification) ·
  D8 Portability-export delivery · D9 Accessibility enforcement · D10 AppHost/deploy
  wiring · D11 Party-picker re-skin + ARIA.

**Deferred Decisions (post-MVP / out of this scope):**
- EventStore gateway RBAC "data-subject/self" principal (chose Parties-side scope
  instead; see residual risk below).
- Production KMS for crypto-shredding (pre-existing prerequisite before real PII).
- Temporal name-as-of queries, semantic/graph/hybrid search (remain reserved).

### Data Architecture

- **Inherited, unchanged:** event-sourced + CQRS aggregate (`Party`), projections
  (`PartyDetail`, `PartyIndexEntry`) in the DAPR/Redis state store; no DB/ORM.
- **UI read posture:** the UI binds projection reads carrying
  `ProjectionFreshnessMetadata`; **render last-known on stale/degraded, never blank
  or throw** (matches existing `DegradedResponseMiddleware` + freshness model).
  Acceptance is **not** read-your-write → optimistic echo + reconcile (see D6).
- **No new persistence** introduced by the UI tier. The consumer identity→party
  **binding** (D2) is the only new stored mapping; it lives in the IdP (claim) and/or
  a small binding store, **not** in the event stream.

### Authentication & Security

- **D5 — Host-owned OIDC (Interactive Server).** `Hexalith.Parties.UI` owns sign-in
  via `Microsoft.AspNetCore.Authentication.OpenIdConnect` (10.0.8, the FrontComposer
  pattern) against **Keycloak** (run mode) / the external **`tache` realm** (publish).
  Server-side cookie session; **OIDC tokens never leave the server** — the browser
  holds no bearer token, so it cannot call the EventStore gateway directly.
- **D2 — Consumer identity = `party_id` claim binding.** A verified IdP claim
  (`party_id`) maps the authenticated subject to exactly one `Party`. Resolution is
  **fail-closed**: a Consumer with no `party_id` claim is routed to an
  onboarding/error state, never to a data screen. Tenant claim (`eventstore:tenant`,
  normalized by `PartiesClaimsTransformation`) is carried as today; the consumer's
  effective scope is `{tenant, party_id}`.
- **Role routing.** Existing `Admin` policy + a new **`Consumer`** policy. Landing
  area decided by role: `Admin`/`TenantOwner` → Admin; `Consumer` → Consumer
  (`<FluentNav>` entries gated by `<AuthorizeView Policy>` — never cross-render).
- **D3 — Own-data authorization (Parties-side self-scope + BFF), defense-in-depth:**
  1. The UI BFF resolves `party_id` and **only ever issues self-scoped operations**
     for a Consumer (`GetPartyAsync(myPartyId)`, consumer GDPR commands on `myPartyId`)
     — it never calls list/search for a Consumer.
  2. The Parties host adds a **data-subject self-authorization** (new `Consumer`
     policy / `IDataSubjectAccessService`, analogous to `ITenantAccessService`,
     **fail-closed**): for a Consumer principal the request's `aggregateId` **must
     equal** the bound `party_id`, else deny — defense-in-depth if the BFF is ever
     bypassed.
  - **Residual (deferred):** the EventStore gateway still enforces tenant-RBAC
    deny-by-default in front of Parties, so a Consumer principal must clear the
    gateway with a minimal tenant role (e.g. `TenantReader`); own-data-only is then
    narrowed by the BFF + Parties self-scope. Removing the consumer's tenant-level
    reach entirely would require the (deferred) gateway self-principal.
- **PII hygiene (unchanged discipline):** admin typed-name erase confirmation is
  compared **in-memory only** (never logged/telemetered); no PII in logs, traces,
  copy, or tombstones; `[PersonalData]` respected end-to-end.
- **GDPR posture in copy:** consent **opt-in, default Off**; erasure copy commits to
  the *start* (Art.12(3)) and states completed erasure is permanent; **Art.21 Object**
  for non-consent bases; **Art.20** export is machine-readable (JSON) + async.

### API & Communication Patterns

- **D5 transport:** the BFF calls the EventStore gateway with the typed
  `Hexalith.Parties.Client` (`AddPartiesClient(config)`), injecting the user's token +
  tenant via the client's `requestCustomizer` hook. GDPR ops reuse
  `IAdminPortalGdprClient`; a consumer-facing slice reuses the same gateway commands
  (`RecordConsent`/`RevokeConsent`/`EraseParty`/`ExportPartyData`/`GetProcessingRecords`)
  scoped to `myPartyId`.
- **D6 — Live freshness:** subscribe to EventStore projection updates via **SignalR**
  (`Hexalith.EventStore.SignalR` + `Microsoft.AspNetCore.SignalR.Client` 10.0.8 — the
  FrontComposer Shell pattern) to **reconcile optimistic UI on projection confirm**;
  **polling/freshness-metadata fallback** when the stream is degraded. Surface
  `X-Service-Degraded` / `X-Stale-Data-Age` into UI state.
- **Error handling standards:** map `PartiesClientException` status → the UX
  `StatusKind` machine (400/422→Validation, 401→SignInRequired, 403→Forbidden/
  TenantUnavailable, 404/410→Gone, ≥500→LoadFailure, timeout→TransientFailure);
  **politeness split** — status/freshness `aria-live=polite`, validation/failure
  `role=alert`.
- **D7 — GDPR-stub completion:** `GetErasureCertificateAsync` /
  `RetryErasureVerificationAsync` are 501 stubs pending an EventStore contract; the
  Admin **erasure-verification report** (Flow 3 climax) depends on them. Define the
  EventStore contract + Parties implementation as a **backend story** preceding the
  Admin GDPR verification UI.
- **D8 — Portability-export delivery:** `ExportPartyData` → `PartyDataPortabilityPackage`
  (JSON). Happy path = synchronous download with progress; for slow/large exports,
  async "preparing → ready" with a SignalR/poll signal and an in-app download when
  ready. Copy makes **no time promise** (per the regulated-language review).

### Frontend Architecture

- **D1 render:** Blazor **Interactive Server** (`AddInteractiveServerComponents`),
  `ValidateScopes=true` (ADR-030).
- **State:** Fluxor single-writer-per-slice (ADR-007); generated
  Feature/Actions/Reducers/Registration + command forms from `[Projection]`/`[Command]`
  (SourceTools); effects own gateway calls + persistence, reducers pure.
- **D4 composition:** `Hexalith.Parties.UI` (host) embeds `Hexalith.Parties.AdminPortal`
  (Admin) + new **`Hexalith.Parties.ConsumerPortal`** RCL (My profile / Edit profile /
  My consent / My data & privacy). Shell-gated nav by policy.
- **Domain components (DESIGN.md):** party-state badge, data-freshness indicator,
  GDPR destructive button — built on FluentUI V5 (`FluentBadge`/`FluentButton`),
  color **plus** text label, status-token pairs (never raw hex).
- **D11 — Party picker:** re-skin `<hexalith-party-picker>` from legacy FAST tokens to
  Fluent 2 tokens **and** add the full WAI-ARIA combobox semantics (role=combobox/
  listbox/option, `aria-controls`/`aria-activedescendant`/`aria-selected`) — one
  combined design-debt story.
- **D9 — Accessibility (WCAG 2.2 AA):** real semantics (switch/radiogroup/labeled
  typed-confirm), per-surface focus contract, forced-colors + reduced-motion
  product-wide, AA contrast gate (filled primary → `--colorBrandBackground`, never raw
  teal `#0097A7`). Enforced by **bUnit** + a **Playwright a11y gate** (FrontComposer
  e2e pattern).
- **Responsive:** Admin desktop-first master-detail (→ sheet/full-screen on phone with
  a focus contract); Consumer phone-first single column. One codebase, two densities.

### Infrastructure & Deployment

- **D10 — Aspire:** add `builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")`
  referencing `eventstore` (gateway) + `tenants`, with OIDC config (Keycloak run /
  `tache` publish). **No DAPR sidecar** — the UI is a BFF over HTTP + SignalR, like
  `parties-mcp` / `eventstore-admin-ui`.
- **Containers/K8s:** .NET SDK container support (no Dockerfile),
  `EnableContainer=true` + `ContainerRepository=parties-ui`; aspirate publish grows the
  cluster **11 → 12 pods**.
- **Cross-cutting:** `ServiceDefaults` (OpenTelemetry, health) on the UI host; CI gains
  the UI build + bUnit lane + the Playwright a11y gate; Central Package Management,
  `TreatWarningsAsErrors`, `.slnx`, Conventional Commits all apply.

### Decision Impact Analysis

**Implementation sequence (suggested):**
1. Stand up `Hexalith.Parties.UI` host (step-03 init) + AppHost wiring + OIDC sign-in.
2. Role routing + `Consumer` policy + `party_id` claim resolution (fail-closed).
3. Parties-side data-subject self-authorization (`IDataSubjectAccessService`).
4. Embed `AdminPortal`; wire Admin list/detail/create-edit against the gateway client.
5. `ConsumerPortal` RCL: My profile → consent → data & privacy.
6. Live-freshness (SignalR) + optimistic/reconcile + StatusKind mapping.
7. Picker re-skin + ARIA (D11); accessibility gate (D9).
8. Backend: GDPR-stub completion (D7) for the Admin verification report.

**Cross-component dependencies:**
- D1 (Server) → D5 (server-side token/BFF) → D3 (browser holds no token, so BFF +
  Parties self-scope is sufficient defense-in-depth).
- D2 (`party_id` claim) → D3 (self-scope key) and gates all Consumer surfaces.
- D7 (EventStore GDPR contract) blocks the Admin erasure-verification UI.
- D6 (SignalR) underpins both the reconcile UX and async export-ready signaling (D8).

## Implementation Patterns & Consistency Rules

### Pattern Categories Defined

**Baseline conventions are INHERITED, not re-decided** (from `project-context.md` ×5,
`.editorconfig`, CLAUDE.md): C# style (file-scoped namespaces, Allman, `_camelCase`,
`I`-prefix, `Async` suffix), wire JSON (camelCase, string enums, ISO-8601,
null-omitted), additive-only event evolution, ULIDs-not-GUIDs, xUnit v3 + Shouldly +
NSubstitute + bUnit, Central Package Management, `TreatWarningsAsErrors`, `.slnx`.
The rules below target the **~10 UI-tier divergence points** these don't cover.

### Naming Patterns

- **Projects / namespaces:** host `Hexalith.Parties.UI`; consumer RCL
  `Hexalith.Parties.ConsumerPortal` (mirrors `AdminPortal`). Namespace = folder path.
- **Routes (area-prefixed, kebab):** Admin `/admin/parties`, `/admin/parties/{id}`,
  `/admin/parties/{id}/gdpr` (fixed by EXPERIENCE.md); Consumer `/me`, `/me/edit`,
  `/me/consent`, `/me/privacy`. One `@attribute [Authorize(Policy=…)]` per area —
  Admin pages require `Admin`, `/me/*` pages require `Consumer`.
- **Components:** PascalCase `.razor`, suffix by role to match AdminPortal
  (`*Panel` for embedded panels, `*Page` for routable pages, `*Form` for command
  forms) — e.g. `MyProfilePanel.razor`, `ConsentManagementPanel.razor`.
- **Fluxor:** slice state `{Feature}State`, actions `{Verb}{Feature}Action`,
  `{Feature}Reducers`, `{Feature}Effects`. **Generated** Feature/Actions/Reducers/
  Registration from `[Projection]`/`[Command]` keep generator-assigned names — never
  rename them.

### Structure Patterns

- **ConsumerPortal layout:** flat `Components/` (mirror AdminPortal) + `State/`
  holding one folder per Fluxor slice. Domain components live in the UI host or a
  shared place, reused by both areas.
- **Tests:** bUnit component tests in a `tests/Hexalith.Parties.*.Tests` project
  mirroring the source layout; run via the lane runner; `DiffEngine_Disabled=true`
  if Verify snapshots are used (FrontComposer rule).
- **Copy:** **localization resources** (chosen) — one resource set per area;
  regulated GDPR microcopy centralized and auditable, never inlined in components.
- **Generated code** lands under `obj/**/generated/…` — **never hand-edit, never
  commit**; change the annotated type or the generator.

### Format Patterns

- **No hand-serialization** — the UI uses the typed `Hexalith.Parties.Client`; wire
  JSON shape is the client's concern (camelCase/string-enum/ISO-8601/null-omitted,
  inherited).
- **Error surface:** RFC 9457 `problem+json` → `PartiesClientException` → the single
  `StatusKind` mapping below. Never parse problem+json ad hoc per screen.
- **Dates:** display via shell localization; never reformat wire dates by hand.
- **Freshness:** always render `ProjectionFreshnessMetadata` through the shared
  **data-freshness indicator** (dot **+** word), never as raw status text.

### Communication Patterns

- **Canonical `StatusKind` → UI-state mapping (single source — agents must not remap
  per screen):**

  | Client outcome | UI state | Politeness |
  |---|---|---|
  | 200/202 accepted | Accepted-but-processing (optimistic + reconcile) | `polite` |
  | 400/422 validation | Validation (inline, preserve input) | **`alert`** |
  | 401 | SignInRequired (route + return URL) | — |
  | 403 tenant/role | Forbidden / TenantUnavailable ("warming up") | `polite`/`alert` |
  | 404/410 | Gone (tombstone, no PII) | `polite` |
  | 408/timeout | TransientFailure (retry + backoff) | **`alert`** |
  | ≥500 | LoadFailure (retry + support, no raw 500) | **`alert`** |
  | stale/degraded read | Degraded (render last-known) | `polite` |

- **aria-live politeness split (pinned):** status / freshness / accepted-processing →
  `role="status" aria-live="polite"`; validation-rejected / transient / load-failure →
  `role="alert"` (assertive). Never blanket-polite.
- **Optimistic-then-reconcile (one shared effect pattern, not per-screen):** effect
  dispatches optimistic state → issues command via client → reconciles on **SignalR
  projection-confirm** (or `Freshness=Current`); on rejection **revert + inline reason**.
  Do **not** steal focus to a toast on routine optimistic saves — announce via aria-live.
- **Live updates:** EventStore projection subscription over **SignalR**; **polling +
  freshness-metadata fallback** when degraded. No bespoke per-screen polling.
- **Fluxor single-writer (ADR-007):** one dispatch source per action type; effects own
  gateway calls / JS interop / persistence; reducers stay pure.

### Process Patterns

- **Self-scoping (security-critical, pinned):** a Consumer principal **never** calls
  list/search. All consumer gateway calls go through **one** self-scoped accessor that
  injects the resolved `party_id` and asserts `aggregateId == party_id`. Admin uses the
  tenant-scoped client. Bypassing the accessor is a defect.
- **Auth flow:** host-owned OIDC; tokens **server-side only**; `party_id`-claim
  resolution is **fail-closed** (no claim → onboarding/error route, never a data screen).
- **DI lifetime (ADR-030, pinned):** storage / effects / auth / tenant / self accessors
  are **Scoped**, never captured by singletons; `ValidateScopes=true` fails such capture
  at boot.
- **State vocabulary:** use the EXPERIENCE.md state set — Cold-load skeleton (never
  spinner-only), Empty (clear-filters, never a dead end), Stale (render last-known,
  never blank/throw), Display-name-only, Erased/Gone tombstone. Don't invent new ones.
- **Destructive actions:** typed-name confirm in a **real labeled `<input>`**; Erase
  disabled until the name matches; comparison **in-memory only** (never logged).
- **Theming / tokens (pinned):** theme via `IThemeService` / design-token API; **never
  hard-code hex or redeclare Fluent custom properties**; filled primary →
  `--colorBrandBackground` (never raw teal `#0097A7`); party/GDPR/freshness states →
  `--colorStatus*` token **pairs**; icons via the inline-SVG `FcFluentIcons` factory
  (no FluentUI icons NuGet).
- **Generated-vs-handwritten boundary (pinned):** projection/command UI comes from
  `[Projection]`/`[Command]` generation; hand-author only shell composition, the domain
  components, and bespoke panels; never hand-edit generated output.
- **Copy register (pinned, via localization):** Admin terse/operator; Consumer
  plain/reassuring; GDPR honesty — no hard completion SLA (commit to the *start*),
  state completed erasure is permanent, consent **default-Off never pre-checked**,
  **Art.21 Object** for non-consent bases, erasure acknowledgement neutral/info (never
  success-green).
- **PII hygiene (pinned):** no PII in logs / traces / telemetry / copy / tombstones;
  `[PersonalData]` respected end-to-end.

### Enforcement Guidelines

**All AI agents MUST:** self-scope every consumer call through the single accessor;
use the `StatusKind` mapping + aria-live split verbatim; reuse the domain components +
design tokens (zero hard-coded color); route all user-facing copy through localization;
never hand-edit generated code; keep accessors `Scoped`.

**Enforcement mechanisms:** bUnit component tests + the **Playwright a11y gate (WCAG
2.2 AA)**; `TreatWarningsAsErrors`; `ValidateScopes=true` (singleton-capture caught at
boot); a storage/self-scope tripwire test (FrontComposer NFR17 pattern); mandatory code
review. Pattern changes go through an ADR/story; this section is the source of truth
for UI-tier rules.

### Pattern Examples

**Good — consumer consent toggle:** effect dispatches optimistic `Off` → calls
`RevokeConsent(myPartyId)` via the **self-scoped** client → reconciles on SignalR
confirm; control is `role="switch" aria-checked` with `aria-describedby` tying the
purpose + lawful basis; on rejection, revert + `role="alert"` inline reason. Copy from
a localization resource.

**Anti-patterns (forbidden):** `ListPartiesAsync` for a Consumer; hard-coded `#0097A7`
button fill; inline `"It'll be gone within 30 days"`; a styled `<div>` consent toggle;
a hand-edited generated reducer; `aria-live="polite"` on a validation error;
success-green toast on an erasure acknowledgement.

## Project Structure & Boundaries

### Complete Project Directory Structure

New/changed projects (★ = new, ◆ = extend existing). Solution: `Hexalith.Parties.slnx`.

```
src/
├── Hexalith.Parties.UI/                          ★ Blazor Server host (BFF) — Microsoft.NET.Sdk.Web
│   ├── Hexalith.Parties.UI.csproj                #  refs Shell(+.Mcp dev), AdminPortal, ConsumerPortal,
│   │                                             #  Client, Contracts, Tenants.Client/Contracts; SourceTools analyzer
│   ├── Program.cs                                #  Quickstart chain + OIDC + AddPartiesClient + SignalR + self-scope DI
│   ├── PartiesUiDomainMarker.cs                  #  AddHexalithDomain<PartiesUiDomainMarker>()
│   ├── appsettings.json / .Development.json      #  Parties:BaseUrl, Authentication (OIDC), Tenants, SignalR
│   ├── Components/
│   │   ├── App.razor · Routes.razor              #  <FrontComposerShell>@Body</FrontComposerShell>
│   │   ├── _Imports.razor
│   │   ├── Account/                              #  FR-Shell — OIDC challenge/callback, RoleLandingRedirect,
│   │   │                                         #  NoPartyBinding (fail-closed onboarding/error state)
│   │   └── Shared/                               #  domain components reused by BOTH areas
│   │       ├── PartyStateBadge.razor             #   color + label, --colorStatus* pairs
│   │       ├── DataFreshnessIndicator.razor      #   dot + word, ProjectionFreshnessMetadata
│   │       └── GdprDestructiveButton.razor       #   typed-confirm, danger fill
│   ├── Authentication/
│   │   ├── PartyIdClaimResolver.cs               #  D2 — resolve party_id claim (fail-closed)
│   │   └── PartiesUiAuthorization.cs             #  "Admin" + new "Consumer" policies, role→landing
│   ├── Services/
│   │   ├── ISelfScopedPartiesClient.cs / .cs     #  D3 — the SINGLE consumer self-scope accessor
│   │   └── PartiesProjectionSubscription.cs      #  D6 — SignalR projection subscription + reconcile dispatch
│   ├── Resources/                                #  shared/shell localized strings
│   └── wwwroot/                                  #  picker Fluent-2 token CSS, static assets
│
├── Hexalith.Parties.ConsumerPortal/              ★ Consumer RCL — Microsoft.NET.Sdk.Razor (mirrors AdminPortal)
│   ├── Hexalith.Parties.ConsumerPortal.csproj    #  PackageId; refs Client, Contracts, FrontComposer.Shell
│   ├── _Imports.razor
│   ├── Components/
│   │   ├── MyProfilePage.razor                   #  FR-Consumer-1  (/me)
│   │   ├── EditMyProfilePage.razor               #  FR-Consumer-2  (/me/edit)
│   │   ├── MyConsentPage.razor                   #  FR-Consumer-3  (/me/consent)
│   │   ├── MyDataPrivacyPage.razor               #  FR-Consumer-4  (/me/privacy)
│   │   ├── ConsentToggle.razor                   #  role=switch + aria-describedby(purpose+basis), default-Off
│   │   ├── ErasureRequestPanel.razor             #  two-state: cancellable-until-start → permanent
│   │   └── DataExportPanel.razor                 #  async preparing → ready → download (JSON)
│   ├── State/  Profile/ · Consent/ · Privacy/ · Export/   #  Fluxor slices (State/Actions/Reducers/Effects)
│   └── Resources/                                #  regulated GDPR microcopy (auditable, localized)
│
├── Hexalith.Parties.AdminPortal/                 ◆ existing RCL — add routable pages + State/ + Resources/
│   ├── Components/  (existing GDPR panels) +
│   │   ├── PartiesListPage.razor                 #  FR-Admin-1  (/admin/parties)
│   │   ├── PartyDetailPage.razor                 #  FR-Admin-2  (/admin/parties/{id})
│   │   ├── CreateEditPartyPage.razor             #  FR-Admin-3  (/admin/parties/new|{id}/edit) — embeds picker
│   │   └── PartyGdprPage.razor                   #  FR-Admin-4  (/admin/parties/{id}/gdpr) — wraps existing panels
│   ├── State/  Parties/ · Detail/ · Gdpr/
│   └── Resources/
│
├── Hexalith.Parties.Picker/                      ◆ D11 — re-skin to Fluent-2 tokens + full WAI-ARIA combobox
│   └── wwwroot/ (token CSS) + ARIA wiring
│
└── Hexalith.Parties/                             ◆ host — Authorization/ extension
    └── Authorization/
        ├── IDataSubjectAccessService.cs / .cs    #  D3 defense-in-depth — assert aggregateId == party_id (fail-closed)
        └── ConsumerPolicy.cs                     #  "Consumer" policy registration

tests/
├── Hexalith.Parties.UI.Tests/                    ★ bUnit — routing, role-landing, claim resolver, self-scope, StatusKind map
├── Hexalith.Parties.ConsumerPortal.Tests/        ★ bUnit — consent toggle a11y, erasure two-state, export states
├── Hexalith.Parties.AdminPortal.Tests/           ◆ bUnit — new admin pages
└── e2e/                                          ★ Playwright a11y/visual gate (WCAG 2.2 AA) — FrontComposer pattern

src/Hexalith.Parties.AppHost/Program.cs           ◆ add builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")
deploy/k8s/                                        ◆ parties-ui Deployment/Service/ingress + OIDC config (11 → 12 pods)
```

> **Cross-submodule (D7, requires explicit approval):** completing
> `GetErasureCertificate` / `RetryErasureVerification` needs an **EventStore contract**
> (in the `Hexalith.EventStore` submodule) plus the Parties-side wiring in
> `Hexalith.Parties.Client/AdminPortal/`. Treat as a separate, approved backend story.

### Architectural Boundaries

- **API boundary:** the browser talks **only** to the UI host (Blazor Server circuit +
  SignalR). The UI host (BFF) talks to the **EventStore gateway** (typed client, HTTP)
  and the **EventStore SignalR hub**. The UI exposes **no** public command/query API; it
  never calls the `parties` actor host directly. OIDC callback is the only extra endpoint.
- **Component boundary:** `AdminPortal` (Admin) and `ConsumerPortal` (Consumer) are
  independent RCLs; shared domain components live in `UI/Components/Shared`. Nav is
  policy-gated — areas never cross-render. Fluxor slices are per-RCL, single-writer.
- **Auth/authz boundary:** OIDC at the host → gateway tenant-RBAC (deny-by-default) →
  **Parties-side self-scope** (defense-in-depth). For a Consumer, the `ISelfScopedPartiesClient`
  is the **only** data path; it is the architectural choke point for own-data-only.
- **Data boundary:** no DB/ORM; reads are projections via the gateway carrying freshness;
  the only new stored mapping is the identity→`party_id` binding (IdP claim / small
  binding store), never in the event stream.

### Requirements → Structure Mapping

| Requirement | Lives in |
|---|---|
| FR-Shell (sign-in, role routing) | `UI/Components/Account/`, `UI/Authentication/` |
| FR-Admin-1..4 | `AdminPortal/Components/*Page.razor` + `State/` (routes `/admin/parties*`) |
| FR-Consumer-1..4 | `ConsumerPortal/Components/*Page.razor` + `State/` (routes `/me*`) |
| Own-data authz (D3) | `UI/Services/SelfScopedPartiesClient` + `Hexalith.Parties/Authorization/` |
| Live freshness (D6) | `UI/Services/PartiesProjectionSubscription` + per-slice Effects |
| Picker re-skin + ARIA (D11) | `Hexalith.Parties.Picker/` |
| GDPR verification (D7) | EventStore contract + `Client/AdminPortal/` (backend story) |

### Integration Points & Data Flow

- **Internal:** Fluxor effect → typed client → gateway; SignalR projection event →
  reconcile-dispatch into the owning slice.
- **External:** Keycloak (run) / `tache` realm (publish) OIDC; EventStore gateway;
  EventStore SignalR hub; optional Memories search (Admin only).
- **Command data flow:** user action → **optimistic** slice update + `aria-live=polite`
  "Saving…" → command via (self-scoped) client → gateway persists+publishes →
  projection updates → **SignalR confirm** → slice reconciles → freshness → `Current`.
  Rejection → revert + `role="alert"` inline reason.

### File Organization & Workflow

- **Config:** `appsettings*.json` (`__`-nested env override); OIDC + `Parties:BaseUrl` +
  SignalR endpoints; no secrets committed.
- **Source:** host = shell composition + cross-area services; each area = its own RCL;
  domain components shared; generated code under `obj/**/generated` (never edited/committed).
- **Tests:** bUnit per project + a solution-level Playwright a11y gate; lane runner.
- **Dev/build/deploy:** `dotnet aspire run` adds `parties-ui` (no sidecar); .NET SDK
  container (`ContainerRepository=parties-ui`); aspirate publish → 12-pod cluster.

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:** The chain D1 (Interactive Server) → D5 (server-side BFF,
tokens never reach the browser) → D3 (Parties-side self-scope + BFF) is internally
consistent and *strengthened* by the Server choice. D6 (SignalR) aligns with the
existing `Hexalith.EventStore.SignalR`. D4 (RCL split) matches the existing
AdminPortal/Picker packaging. No contradictory decisions. **Versions:** no new packages
outside the pinned ecosystem (OIDC + SignalR 10.0.8 stable; FluentUI `5.0.0-rc.3` is RC
— tracked as a version risk, not a blocker).

**Pattern Consistency:** Implementation patterns support the decisions — the single
`StatusKind→UI` mapping + aria-live split serve D6; the self-scope accessor enforces D3;
token/copy discipline serves D9 + the regulated-language review. Naming/structure
patterns align with FrontComposer + the existing repo conventions.

**Structure Alignment:** The tree realizes every decision — host=BFF, two RCLs, host-side
self-scope choke point, picker re-skin, host Authorization extension. Boundaries
(API/component/authz/data) are explicit and respected.

### Requirements Coverage Validation

**Functional coverage:**
- FR-Shell ✅ · FR-Admin-1/2/3 ✅ · FR-Consumer-1/2/3/4 ✅ (structurally placed).
- **FR-Admin-4 (GDPR) — partial:** restrict/consent/export/processing-records reuse
  existing AdminPortal panels ✅; the **erasure-verification report depends on D7**
  (EventStore contract — 501 stubs today).

**Non-functional coverage:**
- Accessibility (WCAG 2.2 AA) ✅ — D9 + bUnit + Playwright a11y gate.
- Eventual consistency ✅ — D6 + freshness + optimistic/reconcile + last-known render.
- Security/own-data ✅ — D3 choke point (with the documented gateway residual).
- GDPR honesty ✅ — copy register + localization + default-Off consent + Art.21 Object.
- Multi-tenancy ✅ · Responsive ✅ · Brand discipline ✅.

### Implementation Readiness Validation

**Decision Completeness:** ✅ critical decisions documented (D1–D11) with rationale and
inherited/pinned versions. **Structure Completeness:** ✅ concrete tree, boundaries, and
FR→location mapping. **Pattern Completeness:** ✅ naming/structure/format/communication/
process patterns with examples and enforcement.

### Gap Analysis Results

**Critical Gaps (block implementation):** None block *starting* implementation along the
decided path.

**Important Gaps (resolve before the dependent flow ships):**
1. **D2 binding provisioning (gates the Consumer area):** the *mechanism* by which a
   consumer obtains the `party_id` claim/binding (admin-link · self-registration ·
   IdP federation) is undesigned. Recommend a short onboarding/binding design before
   Consumer implementation. The Admin area and all host/self-scope plumbing are
   unaffected.
2. **D7 EventStore GDPR contract (gates FR-Admin-4 verification report):** define the
   contract + Parties wiring as an approved backend story; cross-submodule.
3. **Production KMS (pre-existing prerequisite):** crypto-shredding is ON by default with
   only `LocalDevKeyStorageBackend`; provision a real KMS before any real EU PII.

**Nice-to-Have Gaps:** SignalR reconnect/dedupe specifics; tenant-switch state reset;
async export-ready notification channel detail; Blazor Server circuit scaling
(sticky sessions / SignalR backplane) for production.

### Validation Issues Addressed

- The gateway-RBAC residual (D3) is documented as a deferred decision with a clear path
  (minimal tenant role to clear the gateway; full removal needs the deferred gateway
  self-principal) rather than left implicit.
- D7/D2 dependencies are explicitly sequenced as follow-up stories rather than assumed
  complete.

### Architecture Completeness Checklist

**Requirements Analysis**
- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped

**Architectural Decisions**
- [x] Critical decisions documented with versions
- [x] Technology stack fully specified
- [x] Integration patterns defined
- [x] Performance considerations addressed (perceived/eventual-consistency UX; Server-circuit scaling noted as future)

**Implementation Patterns**
- [x] Naming conventions established
- [x] Structure patterns defined
- [x] Communication patterns specified
- [x] Process patterns documented

**Project Structure**
- [x] Complete directory structure defined
- [x] Component boundaries established
- [x] Integration points mapped
- [x] Requirements to structure mapping complete

### Architecture Readiness Assessment

**Overall Status:** READY WITH MINOR GAPS — chosen deliberately (conservative). The
foundation + Admin path are implementable now; the Consumer path needs the D2
binding-provisioning design first, and FR-Admin-4 verification needs D7.

**Confidence Level:** medium-high — strong fit to the existing system; the open items are
scoped dependencies, not unknowns.

**Key Strengths:**
- Extends the system with **zero EventStore submodule change** for the core path.
- A single security choke point (`ISelfScopedPartiesClient`) for own-data-only.
- Reuses the already-built AdminPortal GDPR panels.
- Accessibility is *enforced* (Playwright a11y gate), not just intended.
- Eventual-consistency UX is first-class (freshness + optimistic/reconcile).

**Areas for Future Enhancement:**
- Gateway data-subject/self principal (centralize own-data enforcement).
- Production KMS for crypto-shredding.
- Blazor Server scaling (sticky sessions / SignalR backplane).
- FluentUI RC → GA tracking.

### Implementation Handoff

**AI Agent Guidelines:**
- Follow all architectural decisions (D1–D11) exactly as documented.
- Use the implementation patterns (StatusKind map, aria-live split, self-scope accessor,
  token/copy discipline) consistently; never hand-edit generated code.
- Respect the project structure and boundaries; consumer data only via the self-scope accessor.
- Refer to this document for all architectural questions.

**First Implementation Priority:** Stand up the `Hexalith.Parties.UI` host (Sdk.Web,
FrontComposer Quickstart chain + OIDC + `AddPartiesClient` + SignalR + self-scope DI),
add it to `Hexalith.Parties.slnx` and the AppHost — then role routing + `party_id`
resolution. (Resolve the **D2 binding-provisioning design** before building the Consumer
area.)
