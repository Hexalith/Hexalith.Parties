---
baseline_commit: fdc83d0349bca1bf3814bb64757388385c3af31d
---

# Story 1.3: Role-based landing and policy-gated navigation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a signed-in user,
I want to land in the area for my role with only my own navigation visible,
so that I see exactly what I am authorized to use.

## Acceptance Criteria

**AC1 — Admin/TenantOwner land in the Admin area; only Admin nav renders**

**Given** a signed-in user with role `Admin` or `TenantOwner`
**When** they reach the app entry (`/`)
**Then** they land in the **Admin** area (`/admin`) and only Admin `<FluentNav>` entries render (the Consumer entry is absent).

**AC2 — Consumers land in the Consumer area; areas never cross-render**

**Given** a signed-in user with role `Consumer`
**When** they reach the app entry (`/`)
**Then** they land in the **Consumer** area (`/me`) and only Consumer nav entries render — Admin and Consumer nav **never cross-render**.

**AC3 — `Admin` + new `Consumer` policies registered; every nav entry policy-gated; bUnit/automated proof**

**Given** the authorization configuration
**When** the host boots
**Then** an `Admin` policy (accepting `Admin`/`TenantOwner`) and a new **`Consumer`** policy are registered in the UI host, and `<AuthorizeView Policy=…>` gates every nav entry (via `FrontComposerNavEntry.RequiredPolicy`)
**And** automated tests prove it: a bUnit test asserts role→landing routing (`Admin`→`/admin`, `Consumer`→`/me`, and an authenticated user with **neither** role is **not** routed to a data area — fail-closed), a registration test asserts each nav entry carries the correct `RequiredPolicy`, and a DI-composition test asserts each policy's role mapping (Admin/TenantOwner satisfy `Admin` only; Consumer satisfies `Consumer` only).

> **Scope note on "role":** the epic frames landing **by role**, but the dev Keycloak realm today issues **no role claims** — only `eventstore:tenant`/`eventstore:domain`/`eventstore:permission` attributes (see *Dev Notes → The role-claim gap*). The ACs above are **fully and authoritatively verified by the automated tests** (bUnit + DI-composition + registration), which inject test principals carrying role claims and do not depend on Keycloak. The optional live end-to-end flow additionally requires provisioning realm roles + a role mapper (Task 6); treat the automated tests as the binding proof, exactly as Story 1.2 treated its composition/topology tests as the binding proof for the OIDC ACs.

## Tasks / Subtasks

- [x] **Task 1 — Define the `Admin` + `Consumer` policies and the shared role/policy constants** (AC: 1, 2, 3)
  - [x] Create `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs` (new folder `Authentication/`, per architecture structure). Define a `static class PartiesUiAuthorization` holding the **single source of truth** for policy names and accepted role names, and an `AddPartiesUiAuthorization(this IServiceCollection)` extension:
    ```csharp
    public const string AdminPolicy = "Admin";
    public const string ConsumerPolicy = "Consumer";

    // Mirror the actor host's Admin role set (Hexalith.Parties/Extensions/
    // PartiesServiceCollectionExtensions.cs:68-69 → RequireRole("admin","Admin",
    // "administrator","Administrator")) PLUS "TenantOwner" (epic AC: "Admin or TenantOwner").
    public static readonly string[] AdminRoleNames =
        ["Admin", "admin", "Administrator", "administrator", "TenantOwner", "tenantowner"];
    public static readonly string[] ConsumerRoleNames = ["Consumer", "consumer"];

    public static IServiceCollection AddPartiesUiAuthorization(this IServiceCollection services)
        => services.AddAuthorizationCore(options =>
        {
            options.AddPolicy(AdminPolicy, p => p.RequireRole(AdminRoleNames));
            options.AddPolicy(ConsumerPolicy, p => p.RequireRole(ConsumerRoleNames));
        });
    ```
  - [x] Call `builder.Services.AddPartiesUiAuthorization();` in `Program.cs` **unconditionally** (NOT gated on `authEnabled`), placed right after `AddHexalithDomain<PartiesUiDomainMarker>()`. Rationale: `<AuthorizeView Policy=…>` must resolve the policies whether or not interactive OIDC is wired (tests, degraded boot) — this mirrors `Hexalith.Tenants.UI/Program.cs:32-37` which registers its policy via `AddAuthorizationCore` unconditionally with a "registered unconditionally so the policy resolves whether or not sign-in is wired" rationale.
  - [x] Calling `AddAuthorizationCore` again is additive (policies accumulate); the Quickstart chain already registered authorization core (Story 1.2 confirmed `AddAuthorizationCore()` was NOT needed for `UseAuthorization()` to compose). Do not remove or duplicate the existing registration.

- [x] **Task 2 — Register the policy-gated Admin + Consumer nav entries** (AC: 1, 2, 3)
  - [x] Create `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs` modeled **name-for-name** on `Hexalith.Tenants.UI/Composition/TenantsFrontComposerRegistration.cs`. The class name **must end in `Registration`** and expose a `public static DomainManifest Manifest { get; }` + `public static void RegisterDomain(IFrontComposerRegistry registry)` — this is exactly how `AddHexalithDomain<PartiesUiDomainMarker>()` discovers it (reflection scan of the marker's assembly; see `Hexalith.FrontComposer.Shell/Extensions/ServiceCollectionExtensions.cs:64-127`).
    ```csharp
    public static DomainManifest Manifest { get; } = new("Parties", "parties", [], []);

    public static void RegisterDomain(IFrontComposerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.RegisterDomain(Manifest);

        registry.AddNavEntry(new FrontComposerNavEntry(
            "parties", "Administration", "/admin",
            Icon: "Regular.Size20.Settings", Order: 0,
            RequiredPolicy: PartiesUiAuthorization.AdminPolicy));

        registry.AddNavEntry(new FrontComposerNavEntry(
            "parties", "My space", "/me",
            Icon: "Regular.Size20.Person", Order: 1,
            RequiredPolicy: PartiesUiAuthorization.ConsumerPolicy));
    }
    ```
  - [x] Use bounded context `"parties"` (lowercase) for both the manifest and the entries so they group under one `<FluentNavCategory>` (`EntriesForContext` matches `entry.BoundedContext` ordinally to `manifest.BoundedContext`; a mismatch makes them "orphan" categories — still render, but messier). The marker is `[BoundedContext("Parties")]` but the manifest's `BoundedContext` arg is what the nav groups on — keep them consistent at `"parties"`.
  - [x] **Why this makes nav appear at all:** the shell auto-renders `<FrontComposerNavigation />` only when `HasNavigation` is true (`FrontComposerShell.razor.cs:221` → `Navigation is not null || HasRenderableManifest()`). Today the Parties UI registers a marker but **no** `*Registration` and **no** commands ⇒ no manifest ⇒ no nav. This registration is what lights up the left nav. Each entry's `RequiredPolicy` is rendered by the framework as `<AuthorizeView Policy="@entry.RequiredPolicy"><Authorized>…</Authorized></AuthorizeView>` (`FrontComposerNavigation.razor:49-61`) — so an Admin principal sees only the Admin entry and a Consumer only the Consumer entry. **This satisfies "never cross-render" using framework code you do not modify.**

- [x] **Task 3 — Role-based landing redirect at `/`** (AC: 1, 2)
  - [x] Create `src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor` (new folder `Components/Account/`, per architecture structure line 561) as the routable app entry:
    ```razor
    @page "/"
    @attribute [Authorize]
    @inject AuthenticationStateProvider AuthState
    @inject NavigationManager Nav
    ```
    In `OnInitializedAsync`: read `AuthState.GetAuthenticationStateAsync()`; if `AdminRoleNames.Any(user.IsInRole)` → `Nav.NavigateTo("/admin")`; else if `ConsumerRoleNames.Any(user.IsInRole)` → `Nav.NavigateTo("/me")`; else render a minimal **fail-closed "no area assigned"** message (an `<h1>` + short text) — **never** redirect a role-less authenticated user into `/admin` or `/me` (forward-compatible with Story 1.4's `NoPartyBinding`). Use the `PartiesUiAuthorization.AdminRoleNames`/`ConsumerRoleNames` constants — do NOT re-hardcode role strings here (single source of truth).
  - [x] `@attribute [Authorize]` (no policy = any authenticated user) ensures an **unauthenticated** hit on `/` is handled by the router's `NotAuthorized` path (Task 4) → OIDC challenge. An authenticated user always renders the component, which then role-routes or shows the no-area state.
  - [x] Give the placeholder/no-area markup an `<h1>` so the existing `<FocusOnNavigate Selector="h1">` in `Routes.razor` has a target.

- [x] **Task 4 — Upgrade `Routes.razor` to `AuthorizeRouteView` so `[Authorize(Policy)]` is enforced** (AC: 1, 2, 3)
  - [x] **LOAD-BEARING and easy to miss.** `Components/Routes.razor` currently uses plain `<RouteView>` (Story 1.1), which **does not enforce `[Authorize]`/`[Authorize(Policy)]` attributes on routed components** — without this change the area policies on `/admin` and `/me` silently do nothing. Replace `<RouteView>` with `<AuthorizeRouteView>` (keep `DefaultLayout` and the sibling `<FocusOnNavigate>`):
    ```razor
    <CascadingAuthenticationState>
        <Router AppAssembly="typeof(Program).Assembly">
            <Found Context="routeData">
                <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)">
                    <NotAuthorized Context="authState">
                        @if (authState.User.Identity?.IsAuthenticated != true)
                        {
                            <RedirectToChallenge />
                        }
                        else
                        {
                            <h1>Access denied</h1>
                            <p>You are not authorized to view this area.</p>
                        }
                    </NotAuthorized>
                    <Authorizing><p role="status" aria-live="polite">Authorizing…</p></Authorizing>
                </AuthorizeRouteView>
                <FocusOnNavigate RouteData="routeData" Selector="h1" />
            </Found>
        </Router>
    </CascadingAuthenticationState>
    ```
  - [x] `<CascadingAuthenticationState>` is already present (Story 1.1) — reuse it, do not re-add. `AuthorizeRouteView` resolves authorization services via the `AddAuthorizationCore` registration from Task 1.
  - [x] Create a tiny `RedirectToChallenge` component (e.g. `Components/Account/RedirectToChallenge.razor`) that force-navigates to the FrontComposer challenge endpoint preserving the current URL as the return URL:
    ```csharp
    Nav.NavigateTo($"authentication/challenge?returnUrl={Uri.EscapeDataString(
        new Uri(Nav.Uri).PathAndQuery)}", forceLoad: true);
    ```
    `forceLoad: true` is required — `/authentication/challenge` is the server auth endpoint mapped by `MapHexalithFrontComposerAuthenticationEndpoints()` (Story 1.2), not a Blazor route, so it must be a full browser navigation that 302s to Keycloak. This reuses Story 1.2's challenge machinery (same `?returnUrl=` contract verified live in 1.2).

- [x] **Task 5 — Minimal, transitional `/admin` and `/me` area-landing pages (policy-guarded)** (AC: 1, 2, 3)
  - [x] Create `src/Hexalith.Parties.UI/Components/Areas/AdminLanding.razor`:
    ```razor
    @page "/admin"
    @attribute [Authorize(Policy = PartiesUiAuthorization.AdminPolicy)]
    <h1>Administration</h1>
    ```
    (plus a one-line "area coming soon" note). Reference the policy-name **constant**, not a magic string.
  - [x] Create `src/Hexalith.Parties.UI/Components/Areas/ConsumerLanding.razor`:
    ```razor
    @page "/me"
    @attribute [Authorize(Policy = PartiesUiAuthorization.ConsumerPolicy)]
    <h1>My space</h1>
    ```
  - [x] These are **transitional stubs that establish the area entry points + the per-area `[Authorize(Policy)]` pattern** (architecture line 424-425). They will be **replaced/redirected** when the real areas land: `/admin` by the embedded `AdminPortal` pages (Story 2.1 — `/admin/parties*`), and `/me` by `ConsumerPortal`'s `MyProfilePage` (Story 4.3/4.4, which itself declares `@page "/me"`). **Flag in completion notes:** when ConsumerPortal/AdminPortal RCLs are embedded (their assemblies added to the `<Router>` `AdditionalAssemblies`), this stub at `/me` must be removed to avoid a duplicate-route conflict. For 1.3 the RCLs are NOT embedded, so host-local stubs are correct and unique.
  - [x] Add `@using` / `@attribute` imports as needed in `_Imports.razor` (e.g. `@using Microsoft.AspNetCore.Authorization`, `@using Hexalith.Parties.UI.Authentication`) so the `[Authorize]` attribute and `PartiesUiAuthorization` constant resolve in `.razor` files.

- [x] **Task 6 — (Live-flow enablement, recommended) Provision Keycloak realm roles + role mapper + `RoleClaimType`** (supports the optional live verification of AC1/AC2)
  - [x] **Read *Dev Notes → The role-claim gap* first.** This task makes the live browser flow route correctly; it is **not** required for the ACs (the automated tests are authoritative) but mirrors Story 1.2's "wire it + live-verify" posture and avoids leaving the feature inert end-to-end.
  - [x] In `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json`: add a top-level `"roles": { "realm": [ {"name":"Admin"}, {"name":"TenantOwner"}, {"name":"Consumer"} ] }` block; assign `admin-user` `"realmRoles": ["Admin"]` (optionally assign `readonly-user` `"realmRoles": ["Consumer"]` so the Consumer landing can be smoke-tested live, noting the full Consumer experience awaits Epic 4 / `party_id` binding).
  - [x] Add a **realm-role protocol mapper** to the `hexalith-parties-ui` client (added in Story 1.2) that emits a **flat** `roles` claim:
    ```json
    { "name": "realm-roles-mapper", "protocol": "openid-connect",
      "protocolMapper": "oidc-usermodel-realm-role-mapper", "consentRequired": false,
      "config": { "claim.name": "roles", "jsonType.label": "String", "multivalued": "true",
                  "id.token.claim": "true", "access.token.claim": "true", "userinfo.token.claim": "true" } }
    ```
  - [x] Make ASP.NET map that claim to roles: add a Development-or-always `PostConfigure<OpenIdConnectOptions>("Hexalith.FrontComposer.Oidc", o => o.TokenValidationParameters.RoleClaimType = "roles")` in `Program.cs` (same scheme + same `PostConfigure` pattern Story 1.2 used for `RequireHttpsMetadata`). **VERIFY LIVE** that the signed-in principal actually carries the role claim under the configured name — the exact claim path through the FrontComposer bridge is uncertain (it may flatten differently or nest under `realm_access.roles`). If the live claim differs, align `RoleClaimType` (or the mapper `claim.name`) to the observed claim — exactly as Story 1.2 discovered the http-metadata gotcha by inspecting the live flow. If the live role flow proves intractable in this environment, leave the realm change in place, document the observed claim shape, and rely on the automated tests for the ACs.
  - [x] Preserve LF line endings and minimal diffs on `hexalith-realm.json` / `Program.cs` (Story 1.1/1.2 flagged whole-file CRLF flips on AppHost files). Keep the existing clients/users/mappers untouched. No secret added.

- [x] **Task 7 — Tests (bUnit landing + registration + policy-definition)** (AC: 1, 2, 3)
  - [x] **Landing (bUnit):** add `tests/Hexalith.Parties.UI.Tests/RoleLandingRedirectTests.cs` (`sealed class … : BunitContext`). Use `Bunit.TestDoubles` `this.AddTestAuthorization()` → set the principal per case; render `RoleLandingRedirect`; assert the bUnit `NavigationManager`'s `Uri` (resolve via `Services.GetRequiredService<NavigationManager>()`):
    - `auth.SetAuthorized("admin"); auth.SetRoles("Admin")` → navigates to `…/admin`.
    - `SetRoles("TenantOwner")` → `…/admin`.
    - `SetAuthorized("consumer"); auth.SetRoles("Consumer")` → `…/me`.
    - `SetAuthorized("nobody")` (no roles) → does **NOT** navigate to `/admin` or `/me`; the no-area `<h1>` renders (fail-closed).
  - [x] **Nav-entry registration (xUnit):** add `PartiesUiNavigationRegistrationTests.cs`. Call `PartiesUiFrontComposerRegistration.RegisterDomain(registry)` against a captured/substitute `IFrontComposerRegistry` (NSubstitute) and assert exactly two entries: `("parties","Administration","/admin", RequiredPolicy="Admin")` and `("parties","My space","/me", RequiredPolicy="Consumer")`. This pins the **gating inputs** the framework's `<AuthorizeView>` consumes (the framework rendering itself is already covered upstream by `FrontComposerNavigationNavEntryTests`). Optionally add a focused bUnit test that renders each registered entry through `<AuthorizeView Policy="@entry.RequiredPolicy">` with `AddTestAuthorization().SetPolicies("Admin")` to demonstrate no-cross-render directly.
  - [x] **Policy definitions (DI-composition):** add `PartiesUiAuthorizationPolicyTests.cs` modeled on the existing `PartiesUiHostCompositionTests` style. Build a provider with `AddPartiesUiAuthorization()` + logging under `ValidateScopes=true`, resolve `IAuthorizationService`, and assert: a principal with role `Admin` (and one with `TenantOwner`) **succeeds** `AdminPolicy` and **fails** `ConsumerPolicy`; a principal with role `Consumer` **succeeds** `ConsumerPolicy` and **fails** `AdminPolicy`; a no-role principal fails both. This is the rigorous test of the role→policy mapping (independent of bUnit's faked authorization).
  - [x] If you provisioned the realm (Task 6), add a realm-validation test mirroring `PartiesUiKeycloakRealmTests` (Story 1.2): assert the `Admin`/`Consumer` realm roles exist, `admin-user` carries `Admin`, and the `hexalith-parties-ui` client has a `roles`-emitting realm-role mapper. Keep it in `IntegrationTests/Topology` next to the existing realm test.

- [x] **Task 8 — Build gate + (optional) live flow** (AC: 1, 2, 3)
  - [x] `dotnet build Hexalith.Parties.slnx -c Release --no-restore` with **`-m:1`** (parallel clean builds flake — CS0006/MSB4018) → **0 warnings** under solution-wide `TreatWarningsAsErrors`. Verify the UI host + `Hexalith.Parties.UI.Tests` (+ `IntegrationTests` if you touched the realm test) compile clean. No `Version=` on any `PackageReference` (none expected — see *Library/framework*).
  - [x] `bash scripts/check-no-warning-override.sh` → OK.
  - [x] Run tests via the EXE (xUnit v3; `dotnet test --filter` reports "Zero tests ran" — use `-class`/`-method`, per memory `xunit-v3-mtp-single-dash-filters`) or `scripts/test.ps1 -Lane unit`. All new tests green; existing 10 UI tests stay green.
  - [ ] (Optional, if Task 6 done) `dotnet aspire run --project src/Hexalith.Parties.AppHost` (Docker up). Sign in as `admin-user`/`admin-pass` at `https://localhost:7210/` → confirm landing redirect to `/admin` and that only the **Administration** nav entry renders (no **My space**). Aspire MCP can't see a sandbox `aspire run` — use logs/docker/browser devtools (memory `aspire-mcp-blind-to-sandbox-apphost`).

## Dev Notes

> **Story 1.3 realizes architecture decision AR-D2's "Role routing" slice** (architecture.md#Authentication-Security): *"Existing `Admin` policy + a new `Consumer` policy. Landing area decided by role: `Admin`/`TenantOwner` → Admin; `Consumer` → Consumer (`<FluentNav>` entries gated by `<AuthorizeView Policy>` — never cross-render)."* The work is **composition + a small redirect + nav registration + a Routes.razor upgrade** — no new infrastructure. [Source: architecture.md#Authentication-Security; epics.md#Story-1.3]

### The canonical references — copy these, don't invent

- **Nav registration + policy gating:** `Hexalith.Tenants/src/Hexalith.Tenants.UI/Composition/TenantsFrontComposerRegistration.cs` is the sibling that registers nav entries (incl. one gated by `RequiredPolicy`) and a manifest, discovered by `AddHexalithDomain<T>()`. Mirror its shape exactly. [Source: TenantsFrontComposerRegistration.cs:5-46]
- **Policy registration:** `Hexalith.Tenants.UI/Program.cs:32-37` registers a policy via **unconditional** `AddAuthorizationCore(options => options.AddPolicy(...))`. Mirror the "unconditional so it resolves with or without sign-in" rationale.
- **Framework nav gating (do not modify — just feed it):** `Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerNavigation.razor:49-61` wraps each entry whose `RequiredPolicy` is set in `<AuthorizeView Policy="@entry.RequiredPolicy"><Authorized>…</Authorized></AuthorizeView>`. Entries render as `<FluentNavItem>` with `data-testid="fc-nav-entry-{boundedContext}-{slug(Title)}"` (e.g. `fc-nav-entry-parties-administration`, `fc-nav-entry-parties-my-space`). [Source: FrontComposerNavigation.razor; FrontComposerNavigation.razor.cs:111-114 `NavEntryTestId`]
- **bUnit idiom in this repo:** `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs` — `sealed class … : BunitContext`, `Render<T>(…)`, `cut.Find/FindAll/Markup`, `WaitForAssertion`. [Source: PartyPickerComponentTests.cs]
- **bUnit auth + policy/role faking:** `Hexalith.FrontComposer/tests/.../FrontComposerNavigationNavEntryTests.cs` — `AddTestAuthorization()` → `BunitAuthorizationContext` with `SetAuthorized("user")`, `SetRoles(...)`, `SetPolicies(...)`, `SetNotAuthorized()`. This is the exact pattern for asserting "gated entry hidden when policy not satisfied / shown when satisfied". [Source: FrontComposerNavigationNavEntryTests.cs:115-150]

### The role-claim gap (READ THIS — it shapes the whole story)

The dev Keycloak realm (`src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json`) currently issues **no role claims** to any user — every protocol mapper is an `oidc-usermodel-attribute-mapper` emitting `eventstore:tenant` / `eventstore:domain` / `eventstore:permission` + the `hexalith-eventstore` audience. The test users (`admin-user`, `tenant-a-user`, …) carry `attributes` (tenants/domains/permissions) but **no `realmRoles`**. There is also **no `roles`/`realm_access.roles` mapper** on the `hexalith-parties-ui` client. [Source: KeycloakRealms/hexalith-realm.json:240-326]

There are also **two different "admin" notions** in the codebase — do not conflate them:
1. **Role-claim `Admin` policy** — the actor host `Hexalith.Parties` registers `AddPolicy("Admin", p => p.RequireRole("admin","Admin","administrator","Administrator"))`. [Source: Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:67-70]
2. **Tenant-membership admin** — `Hexalith.Parties.AdminPortal/Services/AdminPortalAuthorizationService.cs` derives "is admin" from the **tenant projection membership** (`TenantRole.TenantOwner` in the tenant's member map), not from a role claim. [Source: AdminPortalAuthorizationService.cs:42-49]

**Decision for this story (faithful to the epic + architecture's "existing `Admin` policy"):** area-level landing/nav gating uses **role-claim-based** `Admin`/`Consumer` policies (notion #1), via `RequireRole`. The AdminPortal's tenant-membership gate (notion #2) is a deeper, intra-area authorization concern that Epic 2 reconciles — it is **out of scope here**. The automated tests inject role-claim principals, so they fully verify the ACs with **zero realm dependency**. The realm role provisioning (Task 6) only enables the optional live browser flow, where the Keycloak→ASP.NET role-claim mapping (`RoleClaimType`) must be verified empirically.

### How `AddHexalithDomain` discovers your registration

`AddHexalithDomain<PartiesUiDomainMarker>()` (already called in `Program.cs:26`) reflection-scans the **marker's assembly** (the UI host) for every exported type whose name **ends in `Registration`** that exposes a static `Manifest` (type `DomainManifest`) **and** a static `RegisterDomain(IFrontComposerRegistry)` method, and queues `RegisterDomain` to run when the registry is built. A class with only one of the two is **skipped with a warning** (it won't register and won't error the build) — so get both members right. Putting `PartiesUiFrontComposerRegistration` in the host assembly is sufficient; no extra DI wiring. [Source: Hexalith.FrontComposer.Shell/Extensions/ServiceCollectionExtensions.cs:64-127, 625-636]

### Why `AuthorizeRouteView` is mandatory here (and Tenants.UI doesn't have it)

`Hexalith.Tenants.UI/Components/Routes.razor` uses plain `<RouteView>` and gates **only** via nav `AuthorizeView` entries — it never puts `[Authorize(Policy)]` on a page. Parties is different: the architecture mandates **"one `@attribute [Authorize(Policy=…)]` per area"** (architecture.md:424-425). `[Authorize]` attributes on routed components are enforced **only** by `<AuthorizeRouteView>`, not `<RouteView>`. So Task 4's swap is what makes `/admin`/`/me`'s policy attributes real; skipping it silently disables them and a Consumer could reach `/admin` directly by URL. This is the single most likely omission in this story. [Source: architecture.md#Naming-Patterns(routes); Components/Routes.razor]

### Landing mechanism

`RoleLandingRedirect` (`@page "/"`, `@attribute [Authorize]`) is the app entry. The host's `<Router AppAssembly="typeof(Program).Assembly">` scans **only the host assembly** (no `AdditionalAssemblies`), which is why the shell's own `@page "/"` (`FcHomeRouteView`) is NOT routed and `GET /` returned 404 in Stories 1.1/1.2 — your new `@page "/"` is unique and claims the route cleanly. On render: unauthenticated → `NotAuthorized` → `RedirectToChallenge` (Story 1.2's `/authentication/challenge?returnUrl=` machinery); authenticated → role-branch to `/admin` or `/me`, or the fail-closed no-area state. [Source: 1-2-*.md#Previous-story-intelligence (GET / → 404); Components/Routes.razor]

### Scope boundary — what is explicitly NOT in this story

Do not pull forward:
- **`party_id` claim resolution / `PartyIdClaimResolver` / `NoPartyBinding`** → **Story 1.4**. (1.3 routes by *role* only; a Consumer's data-binding fail-closed state is 1.4. The no-area state here is the generic role-less case, distinct from `NoPartyBinding`.)
- **Self-scope accessor / `IDataSubjectAccessService` / server-side `Consumer` enforcement on the actor host** → **Story 1.5**. (1.3 registers the UI-host `Consumer` policy for nav/area gating; the Parties-host defense-in-depth is 1.5.)
- **Embedding `AdminPortal` / `ConsumerPortal` RCLs + real area pages** (`/admin/parties*`, `/me/edit`, …) → **Stories 2.1 / 4.3+**. 1.3 ships transitional host-local `/admin` + `/me` stubs only.
- **Gateway client / token relay, SignalR, StatusKind map, domain components** → later Epic-1/2 stories.
[Source: epics.md#Story-1.4,1.5; architecture.md#Implementation-sequence; 1-2-*.md#Scope-boundary]

### Library / framework requirements

- **No new `PackageReference`.** `bunit` (2.8.4-preview) is **already** referenced by `tests/Hexalith.Parties.UI.Tests` (Story 1.1) — `Bunit` + `Bunit.TestDoubles` are available. `AuthorizeRouteView`/`AuthorizeView`/`[Authorize]` ship with ASP.NET Core (`Microsoft.AspNetCore.Components.Authorization`, already used in `Routes.razor`/`_Imports.razor`). `FrontComposerNavEntry`/`IFrontComposerRegistry`/`DomainManifest` come from `Hexalith.FrontComposer.Contracts`/`.Shell` (already referenced). **Central Package Management** owns versions — a `Version=` anywhere is a build error. [Source: tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj:15; Directory.Packages.props:62; project-context.md#Technology-Stack]
- `RequireRole(params string[])` lives in `Microsoft.AspNetCore.Authorization`; `IFrontComposerRegistry` exposes `AddNavEntry` via `IFrontComposerNavEntryRegistry` (the shell's registry implements both). [Source: FrontComposerRegistry.cs:75-82]

### File structure requirements

| Change | File | New/Edit |
|---|---|---|
| Policies + role/policy constants + `AddPartiesUiAuthorization` | `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs` | NEW |
| Nav-entry + manifest registration (discovered by `AddHexalithDomain`) | `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs` | NEW |
| Role landing redirect (`@page "/"`) | `src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor` | NEW |
| Challenge redirect helper | `src/Hexalith.Parties.UI/Components/Account/RedirectToChallenge.razor` | NEW |
| Admin area stub (`@page "/admin"`, `[Authorize(Policy=Admin)]`) | `src/Hexalith.Parties.UI/Components/Areas/AdminLanding.razor` | NEW |
| Consumer area stub (`@page "/me"`, `[Authorize(Policy=Consumer)]`) | `src/Hexalith.Parties.UI/Components/Areas/ConsumerLanding.razor` | NEW |
| `AddPartiesUiAuthorization()` call | `src/Hexalith.Parties.UI/Program.cs` | EDIT |
| `RouteView` → `AuthorizeRouteView` (+ NotAuthorized/Authorizing) | `src/Hexalith.Parties.UI/Components/Routes.razor` | EDIT |
| `@using` for Authorization + `PartiesUiAuthorization` | `src/Hexalith.Parties.UI/Components/_Imports.razor` | EDIT |
| Realm roles + role mapper + (opt) consumer assignment | `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json` | EDIT (Task 6) |
| `RoleClaimType` PostConfigure (Task 6) | `src/Hexalith.Parties.UI/Program.cs` | EDIT (Task 6) |
| Landing bUnit tests | `tests/Hexalith.Parties.UI.Tests/RoleLandingRedirectTests.cs` | NEW |
| Nav-registration test | `tests/Hexalith.Parties.UI.Tests/PartiesUiNavigationRegistrationTests.cs` | NEW |
| Policy-definition DI test | `tests/Hexalith.Parties.UI.Tests/PartiesUiAuthorizationPolicyTests.cs` | NEW |
| (Opt) realm-validation test | `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiRealmRolesTests.cs` | NEW (Task 6) |

Keep code where it belongs: auth/policies/composition/components are **host-level** (`Hexalith.Parties.UI`) — never in `Contracts`/`Server`/`Projections`, and do not reference internal actor-host projects from the UI host. [Source: project-context.md#Code-Quality-Style-Rules; architecture.md#Complete-Project-Directory-Structure (lines 558-574)]

### Testing requirements

- **xUnit v3 + Shouldly + NSubstitute + bUnit** only — no Moq/FluentAssertions/raw `Assert.*`. Match the house style in `PartyPickerComponentTests` (bUnit) and `PartiesUiHostCompositionTests` (DI-composition). [Source: project-context.md#Testing-Rules]
- bUnit auth: `Bunit.TestDoubles.AddTestAuthorization()` returns a `BunitAuthorizationContext`; drive it with `SetAuthorized(name)` / `SetRoles(...)` / `SetPolicies(...)` / `SetNotAuthorized()`. For landing-navigation assertions, read the bUnit `NavigationManager.Uri` (resolve `Services.GetRequiredService<NavigationManager>()`). [Source: FrontComposerNavigationNavEntryTests.cs]
- The DI-composition policy test must build the provider under `ValidateScopes=true` (ADR-030 parity with the host) and resolve `IAuthorizationService` to evaluate real `RequireRole` policies against `ClaimsPrincipal`s carrying `ClaimTypes.Role` claims.
- Run via `scripts/test.ps1 -Lane unit` or the test EXE with xUnit v3 `-class`/`-method` filters (`dotnet test --filter` ⇒ "Zero tests ran"). [Source: memory `xunit-v3-mtp-single-dash-filters`]

### Previous-story intelligence (Stories 1.2, 1.1)

- **Story 1.2 wired OIDC sign-in** into a server-side cookie session via the FrontComposer auth bridge (`AddHexalithFrontComposerAuthentication(UseKeycloak(...))`) gated on `authEnabled`, the Development `RequireHttpsMetadata=false` PostConfigure on scheme `"Hexalith.FrontComposer.Oidc"`, gated `UseAuthentication`/`UseAuthorization`, and `MapHexalithFrontComposerAuthenticationEndpoints()` (challenge=`/authentication/challenge?returnUrl=`, sign-out=`/authentication/sign-out`, callbacks `/signin-oidc`,`/signout-callback-oidc`). It deliberately registered **no policies** — that is this story. Reuse the challenge endpoint + scheme name; do not re-wire OIDC. [Source: 1-2-*.md#Tasks, #Dev-Notes]
- **Story 1.2 added the `hexalith-parties-ui` confidential client** to the realm (authorization-code, `https://localhost:7210/signin-oidc`, 4 attribute mappers + audience). Task 6's realm-role mapper attaches to **this same client**. [Source: 1-2-*.md#Task-4]
- **Story 1.1 left `Routes.razor` with `<CascadingAuthenticationState>` already wrapping the router** ("ready for 1.2's auth") and plain `<RouteView>` — reuse the cascading state, swap the RouteView. `GET /` returns 404 today (no host page at `/`) — your `RoleLandingRedirect @page "/"` fixes that. `<NoWarn>HFC1001</NoWarn>` is in the host csproj (empty SourceTools marker) — leave it; this story declares no `[Command]`/`[Projection]`, so don't add NoWarns unless a specific analyzer fires (then narrow + this-project-only). [Source: 1-1-*.md; 1-2-*.md#Previous-story-intelligence]
- **AppHost-file CRLF caution:** Stories 1.1/1.2 reviews flagged whole-file LF→CRLF flips on `AppHost/Program.cs` and friends (repo tree is uniformly LF in HEAD). If you touch `AppHost/Program.cs` or `hexalith-realm.json` (Task 6), preserve LF and keep the diff minimal. [Source: 1-1-*.md#Senior-Developer-Review; 1-2-*.md]

### Git intelligence

HEAD is `fdc83d0 feat(story-1.2): Host-owned OIDC sign-in …` (baseline for this story). Product changes so far are the host stand-up (1.1) + OIDC sign-in (1.2); there is no prior role/landing/nav code in `parties-ui` to regress. Commit on a typed branch (`feat/...`) with a Conventional Commit (e.g. `feat(story-1.3): role-based landing and policy-gated navigation`). [Source: git log; project-context.md#Development-Workflow-Rules]

### Build & verification gotchas (read before declaring done)

- **`TreatWarningsAsErrors` is solution-wide and absolute** — file-scoped namespaces; `using` outside the namespace, `System.*` first; `_camelCase` private fields; `Async` suffix; interfaces `I*`; CRLF/4-space/final-newline/UTF-8 per `.editorconfig`. Razor files: keep `@using` tidy and avoid unused usings (they error). [Source: project-context.md#Language-Specific-Rules]
- **Central Package Management ON** — no `Version=` on any `PackageReference`. [Source: project-context.md#Technology-Stack]
- **Clean parallel builds flake** (CS0006 / MSB4018) — use **`-m:1`** for a reliable verdict. [Source: memory `parties-parallel-build-flake`]
- **`bash scripts/check-no-warning-override.sh` must pass.** [Source: project-context.md; docs/build-gate.md]
- **Pre-existing, out-of-scope full-`.slnx` failures are NOT your regression** — `Hexalith.PolymorphicSerializations` pack (NU5118/NU5128) and the `*PackageTests`. Verify via per-project compile (`dotnet build src/Hexalith.Parties.UI` + the UI test project) and the unit lane, not the full-solution pack. [Source: memory `parties-pack-tests-preexisting-fail`; 1-2-*.md]
- **Submodules must be sibling checkouts** (FrontComposer, EventStore, Tenants): `git submodule update --init Hexalith.EventStore Hexalith.Tenants` (root-level only, **never `--recursive`**). [Source: project-context.md]
- **Do not touch** the Aspire SDK skew or `Microsoft.Extensions.Hosting.Abstractions` 11.0.0-preview pin (both load-bearing). [Source: memories `aspire-apphost-sdk-version-match`, `hosting-abstractions-preview-pin-load-bearing`]
- **Aspire MCP can't see a sandbox `aspire run`** — verify the optional live flow via logs/docker/browser. [Source: memory `aspire-mcp-blind-to-sandbox-apphost`]

### Project Structure Notes

- This story materializes the architecture's `UI/Authentication/` (policies + role→landing) and `UI/Components/Account/` (`RoleLandingRedirect`) slices, plus a `Composition/` registration (mirroring Tenants.UI) and transitional `Components/Areas/` stubs. The architecture's `PartyIdClaimResolver` (in `Authentication/`) and `NoPartyBinding` (in `Account/`) are **Story 1.4**, not here. [Source: architecture.md#Complete-Project-Directory-Structure (lines 561, 567-569); 1-2-*.md#Project-Structure-Notes]
- The `/me` and `/admin` host stubs are an intentional, transitional variance from the full tree (the real `/me` is `ConsumerPortal/MyProfilePage`, `/admin/*` is `AdminPortal`) — documented as a route to be reclaimed when the RCLs embed (2.1/4.3). [Source: architecture.md (lines 580, 590-595)]

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.3] — story statement + AC groups (role landing, `Consumer` policy, never-cross-render, bUnit).
- [Source: _bmad-output/planning-artifacts/epics.md#Additional-Requirements] — AR-D2 (role routing + new `Consumer` policy), AR-Client, FR-Shell.
- [Source: _bmad-output/planning-artifacts/architecture.md#Authentication-Security] — "Role routing. Existing `Admin` policy + a new `Consumer` policy … gated by `<AuthorizeView Policy>` — never cross-render."
- [Source: _bmad-output/planning-artifacts/architecture.md#Naming-Patterns + #Complete-Project-Directory-Structure] — area routes, one `[Authorize(Policy)]` per area, `UI/Authentication` + `UI/Components/Account` layout.
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.UI/Composition/TenantsFrontComposerRegistration.cs] — nav-entry + manifest registration + `RequiredPolicy` gating to mirror.
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.UI/Program.cs:32-37] — unconditional `AddAuthorizationCore(AddPolicy)` precedent.
- [Source: Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerNavigation.razor (+ .razor.cs:111-114) ] — the `RequiredPolicy → AuthorizeView` rendering + `data-testid` format the tests assert.
- [Source: Hexalith.FrontComposer.Shell/Extensions/ServiceCollectionExtensions.cs:64-127] — `AddHexalithDomain` registration-class discovery contract.
- [Source: Hexalith.FrontComposer/tests/.../FrontComposerNavigationNavEntryTests.cs] — `AddTestAuthorization` + `SetRoles`/`SetPolicies` bUnit gating idiom.
- [Source: tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs] — repo bUnit idiom (`BunitContext`, `Render<T>`, `WaitForAssertion`).
- [Source: Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:67-70] — the existing actor-host `Admin` role set to align with.
- [Source: src/Hexalith.Parties.AdminPortal/Services/AdminPortalAuthorizationService.cs:42-49] — the *other* (tenant-membership) admin notion — out of scope, do not conflate.
- [Source: src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json:240-326] — current users carry attributes, no roles (the gap Task 6 fills).
- [Source: _bmad-output/implementation-artifacts/1-2-*.md] — OIDC wiring, challenge endpoint, `hexalith-parties-ui` client, scope boundary, build gotchas.
- [Source: _bmad-output/project-context.md] — language/framework/testing/quality/workflow rules + anti-patterns.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Opus 4.8) — bmad-dev-story workflow

### Debug Log References

- Build (Release, `-m:1`, per-project per memory `parties-pack-tests-preexisting-fail` — the full `.slnx` pack pre-fails on PolymorphicSerializations/`*PackageTests`, out of scope): `Hexalith.Parties.UI.Tests` (covers the UI host) and `Hexalith.Parties.IntegrationTests` (covers AppHost + realm) both → **0 Warning(s), 0 Error(s)**.
- Two nullable-context errors surfaced and were fixed in `PartiesUiNavigationRegistrationTests.cs` before green: (1) NSubstitute `CallInfo.Arg<T>()` is nullable-annotated → switched the entry capture to `Arg.Do<FrontComposerNavEntry>(entries.Add)` (non-null arg); (2) `Arg.Is<DomainManifest>` is an expression tree (cannot contain an `is` pattern) and its predicate param is nullable → used `m => m != null && m.BoundedContext == "parties" && m.Name == "Parties"`.
- `bash scripts/check-no-warning-override.sh` → `OK`.
- Tests via the xUnit v3 EXE (`dotnet test --filter` reports "Zero tests ran" — memory `xunit-v3-mtp-single-dash-filters`): UI suite **44/44** pass (10 pre-existing + 34 new across the 5 new test files + 1 bUnit harness, including data-driven theories over every declared role-name casing). Realm topology `PartiesUiRealmRolesTests` **3/3** pass; existing `PartiesUiKeycloakRealmTests` **2/2** still green (no realm regression).

### Completion Notes List

- **AC1/AC2/AC3 satisfied by automated tests (the binding proof).** Role→landing routing, policy definitions, and nav-entry gating are proven by `RoleLandingRedirectTests` (4 cases incl. the fail-closed no-role case), `PartiesUiAuthorizationPolicyTests` (DI evaluation of the real `RequireRole` policies under `ValidateScopes=true`), and `PartiesUiNavigationRegistrationTests` (each entry carries the correct `RequiredPolicy`). "Never cross-render" rendering itself is covered upstream by the framework's `FrontComposerNavigationNavEntryTests`; this story pins the gating inputs it consumes.
- **AuthorizeRouteView swap (Task 4) — the load-bearing change.** `Routes.razor` now uses `<AuthorizeRouteView>` so the per-area `[Authorize(Policy=…)]` on `/admin` and `/me` is actually enforced (plain `<RouteView>` silently ignores it). `NotAuthorized` → `RedirectToChallenge` for anonymous (reuses Story 1.2's `/authentication/challenge?returnUrl=` endpoint with `forceLoad:true`), "Access denied" for an authenticated-but-unauthorized user.
- **Single source of truth.** Policy names + accepted role names live only in `PartiesUiAuthorization`; `RoleLandingRedirect`, the nav registration, and the area stubs all reference the constants/arrays — no re-hardcoded role/policy strings. `AddPartiesUiAuthorization()` is registered **unconditionally** (not gated on `authEnabled`) so policies resolve in every boot mode.
- **Transitional `/me` + `/admin` stubs — REMOVAL FLAG (per Task 5).** When `ConsumerPortal` (`MyProfilePage` also `@page "/me"`, Story 4.3/4.4) and `AdminPortal` (`/admin/parties*`, Story 2.1) RCLs are embedded (their assemblies added to the `<Router>` `AdditionalAssemblies`), the host-local `ConsumerLanding.razor` (`/me`) — and likely `AdminLanding.razor` (`/admin`) — must be removed to avoid a duplicate-route conflict. For 1.3 the RCLs are not embedded, so the stubs are correct and unique.
- **Task 6 (live-flow enablement) — code/config done; live browser verification DEFERRED.** Provisioned realm roles `Admin`/`TenantOwner`/`Consumer`, assigned `admin-user`→`Admin` and `readonly-user`→`Consumer`, added a flat `roles` realm-role mapper to the `hexalith-parties-ui` client, and added the `RoleClaimType = "roles"` `PostConfigure` (gated on `authEnabled`). `hexalith-realm.json` kept LF with a minimal additive diff; no secret added. The **optional** live `aspire run` + interactive sign-in step (Task 8, line 178) was **not** run — this environment has no Docker/interactive-browser path and Aspire MCP can't observe a sandbox `aspire run` (memory `aspire-mcp-blind-to-sandbox-apphost`). Per the story's own provision, the realm change stays in place and the automated tests are the authoritative proof. **VERIFY-LIVE caveat carried forward:** the exact claim path through the FrontComposer OIDC bridge is unconfirmed — if a live login shows roles nested (e.g. `realm_access.roles`) rather than flat `roles`, align `RoleClaimType` (or the mapper `claim.name`) to the observed claim.
- **Scope respected.** No `party_id`/`NoPartyBinding` (Story 1.4), no self-scope/`IDataSubjectAccessService` (1.5), no RCL embedding (2.1/4.3+). No new `PackageReference` (CPM intact). EOL convention preserved: UI host + UI tests CRLF, AppHost realm JSON + IntegrationTests LF.

### File List

- `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs` (NEW)
- `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs` (NEW)
- `src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor` (NEW)
- `src/Hexalith.Parties.UI/Components/Account/RedirectToChallenge.razor` (NEW)
- `src/Hexalith.Parties.UI/Components/Areas/AdminLanding.razor` (NEW)
- `src/Hexalith.Parties.UI/Components/Areas/ConsumerLanding.razor` (NEW)
- `src/Hexalith.Parties.UI/Program.cs` (EDIT — `AddPartiesUiAuthorization()` call + `using`; `RoleClaimType` PostConfigure)
- `src/Hexalith.Parties.UI/Components/Routes.razor` (EDIT — `RouteView` → `AuthorizeRouteView` + NotAuthorized/Authorizing)
- `src/Hexalith.Parties.UI/Components/_Imports.razor` (EDIT — Authorization + Authentication + Components.Account usings)
- `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json` (EDIT — realm roles, role mapper, user role assignments)
- `tests/Hexalith.Parties.UI.Tests/RoleLandingRedirectTests.cs` (NEW)
- `tests/Hexalith.Parties.UI.Tests/PartiesUiNavigationRegistrationTests.cs` (NEW)
- `tests/Hexalith.Parties.UI.Tests/PartiesUiAuthorizationPolicyTests.cs` (NEW)
- `tests/Hexalith.Parties.UI.Tests/PartiesUiAreaAuthorizationTests.cs` (NEW — reflects the routable area components and pins each carries the correct `[Authorize(Policy)]` / `/` carries `[Authorize]` with no policy)
- `tests/Hexalith.Parties.UI.Tests/PartiesUiNavEntryGatingTests.cs` (NEW — renders the real registered entries through the real `<AuthorizeView Policy>` gate to prove never-cross-render output, not just inputs)
- `tests/Hexalith.Parties.UI.Tests/NavEntryGatingHarness.razor` (NEW — bUnit harness reproducing the shell's `<AuthorizeView Policy="@entry.RequiredPolicy">` gate for the cross-render test)
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiRealmRolesTests.cs` (NEW)

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-06-10 | 0.1 | Created Story 1.3 — role-based landing + policy-gated navigation. Comprehensive context engine analysis: role-claim policy decision (vs tenant-membership), AuthorizeRouteView upgrade, FrontComposer nav-entry registration, RoleLandingRedirect, transitional area stubs, bUnit/DI/registration test plan, and the realm role-claim gap + live-flow enablement. | Bob (Scrum Master, create-story) |
| 2026-06-10 | 1.0 | Implemented Story 1.3 (dev-story). Added `PartiesUiAuthorization` (Admin/Consumer policies + role constants, unconditional registration), `PartiesUiFrontComposerRegistration` (policy-gated `/admin` + `/me` nav entries), `RoleLandingRedirect` (`@page "/"`, fail-closed no-area), `RedirectToChallenge`, transitional `/admin` + `/me` area stubs, and upgraded `Routes.razor` to `AuthorizeRouteView`. Provisioned Keycloak realm roles + flat `roles` mapper + `RoleClaimType`. Added 11 UI tests (bUnit landing, nav registration, DI policy) + 3 realm-topology tests; 21/21 UI + 3/3 realm green, build-gate clean (0 warnings). Optional live browser flow deferred (no Docker/interactive env). | Amelia (Dev, dev-story) |
| 2026-06-10 | 1.1 | Adversarial review (story-automator-review, auto-fix). Re-ran all gates independently: Release `-m:1` build 0 warnings/0 errors (UI host + UI.Tests + IntegrationTests), UI suite **44/44** green, realm topology **3/3** + existing `PartiesUiKeycloakRealmTests` **2/2** (no regression), `check-no-warning-override.sh` OK, no `Version=`/CPM intact, EOL preserved (UI CRLF, realm JSON LF). Verified contract signatures (`FrontComposerNavEntry`, `DomainManifest`) match. Fixed one MEDIUM documentation discrepancy: added 3 undocumented test artifacts to the File List and corrected the stale 21/21 → 44/44 count. No CRITICAL/HIGH issues; status → done. | Reviewer (story-automator-review) |

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot · **Date:** 2026-06-10 · **Outcome:** ✅ Approve (status → done)

**Scope reviewed:** all 16 changed files (story File List + 3 git-discovered test artifacts; `_bmad-output/` artifacts excluded per review policy).

### AC verification (validated against code + independently re-run tests)
- **AC1 (Admin/TenantOwner → `/admin`, only Admin nav):** IMPLEMENTED. `RoleLandingRedirect.OnInitializedAsync` branches Admin-first via the single-source-of-truth `AdminRoleNames`; `PartiesUiNavEntryGatingTests.AdminPrincipal_SeesOnlyTheAdminEntry` proves no cross-render through the real `<AuthorizeView>` gate.
- **AC2 (Consumer → `/me`, never cross-render):** IMPLEMENTED. Consumer branch + `ConsumerPrincipal_SeesOnlyTheConsumerEntry`; unauthenticated sees neither.
- **AC3 (Admin + new Consumer policies, every entry gated, automated proof):** IMPLEMENTED. `AddPartiesUiAuthorization` registers both via `AddAuthorizationCore` unconditionally; `PartiesUiAuthorizationPolicyTests` evaluates the real `RequireRole` policies under `ValidateScopes=true` (Admin/TenantOwner ⇒ Admin only; Consumer ⇒ Consumer only; role-less ⇒ neither); `PartiesUiNavigationRegistrationTests` pins each entry's `RequiredPolicy`; `PartiesUiAreaAuthorizationTests` pins the per-area `[Authorize(Policy)]`. The load-bearing `RouteView`→`AuthorizeRouteView` swap (Task 4) is present and correct — without it the area policies would silently no-op.

### Strengths
- Genuine single source of truth: role/policy names exist only in `PartiesUiAuthorization`; redirect, nav registration, and area stubs all reference the constants/arrays — verified no re-hardcoded strings.
- Fail-closed correctness: a role-less authenticated user is never routed into a data area (proven, not asserted by inspection).
- Tests are real assertions over production values (registry-captured entries, data-driven theories over every declared role casing) — not placeholders.
- `RoleClaimType` / `IsInRole` / `RequireRole` are coherent: live principals key on `roles`, test principals on `ClaimTypes.Role`, each internally consistent.

### Findings
- 🟡 **MEDIUM (fixed in this review):** 3 implemented test artifacts (`NavEntryGatingHarness.razor`, `PartiesUiAreaAuthorizationTests.cs`, `PartiesUiNavEntryGatingTests.cs`) were missing from the File List and the Debug Log test count was stale (21/21 vs actual 44/44). Documentation corrected; no code change required.
- No CRITICAL or HIGH findings. Task 6's live browser flow remains intentionally deferred (no Docker/interactive env) with the automated tests as the binding proof — consistent with the story's own provision and Story 1.2's posture.

### Gate evidence (re-run, not trusted from the story)
Release `-m:1` → 0 warnings/0 errors · UI 44/44 · realm 3/3 + existing realm 2/2 · `check-no-warning-override.sh` OK · CPM intact · EOL preserved.
