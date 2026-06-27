---
baseline_commit: 8e3fe0c4937f2caa97b6213ca5d40011b5805ac1
---

# Story 1.2: Host-owned OIDC sign-in (server-side, tokens never reach the browser)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to sign in through the app,
so that I can access my role's area securely.

## Acceptance Criteria

**AC1 — OIDC challenge → server-side cookie session, tokens never reach the browser, return URL preserved**

**Given** an unauthenticated request to any protected route
**When** the app challenges via `OpenIdConnect` (10.0.8) against Keycloak (run mode) / the `tache` realm (publish)
**Then** I complete sign-in and a **server-side cookie session** is established, and the **OIDC tokens never leave the server** (the browser holds no usable bearer/access token — only the encrypted, HttpOnly auth cookie)
**And** the original URL is preserved as the return URL and I am returned to it after sign-in.

**AC2 — Sign-out + endpoint surface**

**Given** a signed-in session
**When** I sign out
**Then** the cookie session is cleared and the OIDC sign-out completes; the only host endpoints added are the framework auth endpoints (OIDC callback + signed-out callback + the FrontComposer challenge/sign-out routes) — **no public command/query API** is exposed.

**AC3 — No committed secret; env-overridable config**

**And** no secret is committed to `appsettings*.json`; OIDC config is `__`-nested env-overridable (`Authentication__OpenIdConnect__*`) and the dev client secret is injected by the AppHost (dev Keycloak realm), exactly as the sibling `Hexalith.Tenants.UI` does — and the `deploy/` credential-leak poison-sweep stays clean.

## Tasks / Subtasks

- [x] **Task 1 — Wire the OIDC auth bridge in `Program.cs`** (AC: 1, 2, 3)
  - [x] In `src/Hexalith.Parties.UI/Program.cs`, add `builder.Services.AddHttpContextAccessor();` (the access-token provider seam reads `HttpContext.GetTokenAsync` server-side; the bridge also adds it defensively — adding it explicitly mirrors `Hexalith.Tenants.UI`).
  - [x] Add the **conditional `authEnabled`** gate reading config (mirror `Hexalith.Tenants.UI/Program.cs:42-45`):
    ```csharp
    bool authEnabled =
        Uri.TryCreate(builder.Configuration["Authentication:OpenIdConnect:Authority"], UriKind.Absolute, out Uri? oidcAuthority)
        && !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:OpenIdConnect:ClientId"])
        && !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:OpenIdConnect:ClientSecret"]);
    ```
  - [x] When `authEnabled`, call the FrontComposer auth bridge with the Keycloak recipe (mirror `Tenants.UI/Program.cs:47-53`):
    ```csharp
    builder.Services.AddHexalithFrontComposerAuthentication(o => o.UseKeycloak(
        oidcAuthority!,
        builder.Configuration["Authentication:OpenIdConnect:ClientId"]!,
        builder.Configuration["Authentication:OpenIdConnect:ClientSecret"]!,
        tenantClaimType: "eventstore:tenant",
        userClaimType: "sub"));
    ```
  - [x] **Dev http-Keycloak metadata fix (load-bearing — sign-in fails without it).** The run-mode Keycloak authority is **http** (`keycloak.GetEndpoint("http")`), but the OIDC handler defaults `RequireHttpsMetadata=true` and would reject an http metadata address. The auth bridge does **not** set this. Add a **Development-only** `PostConfigure` targeting the FrontComposer OIDC challenge scheme:
    ```csharp
    if (authEnabled && builder.Environment.IsDevelopment())
    {
        builder.Services.PostConfigure<OpenIdConnectOptions>(
            "Hexalith.FrontComposer.Oidc", // = FrontComposerOpenIdConnectOptions default ChallengeScheme
            o => o.RequireHttpsMetadata = false);
    }
    ```
    (`using Microsoft.AspNetCore.Authentication.OpenIdConnect;`. The scheme name is the default `FrontComposerOpenIdConnectOptions.ChallengeScheme`; verify against the Shell options if it ever changes.)
  - [x] Add the middleware (gated on `authEnabled`), placed **after** `app.UseRequestLocalization()` and **before** `app.UseAntiforgery()` (mirror `Tenants.UI/Program.cs:100-105`):
    ```csharp
    if (authEnabled)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }
    ```
    If `app.UseAuthorization()` throws for missing services, add `builder.Services.AddAuthorizationCore();` **with no policies** — the `Admin`/`Consumer` policies are Story 1.3, **not** this story. (Authorization core is most likely already registered by the Quickstart chain since the shell renders `<AuthorizeView>`.)
  - [x] After `app.MapRazorComponents<App>().AddInteractiveServerRenderMode();`, map the bridge endpoints (gated):
    ```csharp
    if (authEnabled)
    {
        _ = app.MapHexalithFrontComposerAuthenticationEndpoints();
    }
    ```
  - [x] **Keep** the existing `AddFrontComposerDevMode(builder.Environment)` and the Quickstart/FluentUI/domain-marker calls exactly as Story 1.1 left them. Do **not** remove `UseDefaultServiceProvider(ValidateScopes=true)`.
  - [x] **Do NOT** add a token relay (`AddTenantsTokenRelay` equivalent), `AddPartiesClient`, role landing, `party_id` resolution, or `Admin`/`Consumer` policies here — see *Scope boundary* in Dev Notes.

- [x] **Task 2 — Document OIDC config shape in `appsettings.json` (no secret)** (AC: 3)
  - [x] Add an `Authentication:OpenIdConnect` scaffold to `src/Hexalith.Parties.UI/appsettings.json` with **empty-string** values (no real Authority/ClientId/**no secret**), so the `__`-nested override shape is discoverable and env-overridable:
    ```json
    "Authentication": {
      "OpenIdConnect": {
        "Authority": "",
        "ClientId": "",
        "ClientSecret": "",
        "Audience": ""
      }
    }
    ```
  - [x] Leave `appsettings.Development.json` free of OIDC values — the AppHost supplies them via env (Task 3). **Never** commit a real client secret to any `appsettings*.json`.

- [x] **Task 3 — Wire OIDC env + Keycloak reference on `parties-ui` in the AppHost** (AC: 1, 2, 3)
  - [x] In `src/Hexalith.Parties.AppHost/Program.cs`, **capture** the `parties-ui` resource in a variable (it is currently a `_ =` discard at lines 116-120):
    ```csharp
    IResourceBuilder<ProjectResource> partiesUi = builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")
        .WithReference(eventStore)
        .WaitFor(eventStore)
        .WithReference(tenants)
        .WaitFor(tenants);
    ```
  - [x] **After** the keycloak block (after line ~235, where `eventStore`/`parties`/`tenants`/`adminUI` get `.WithReference(keycloak)`), add the OIDC wiring **mirroring the `adminUI` realm/publish conditional (lines 280-303)** and the Tenants AppHost UI block:
    ```csharp
    if (realmUrl is not null)
    {
        // Run mode: interactive authorization-code sign-in against the local dev Keycloak.
        // Dev-only client secret matches the Hexalith.Tenants.UI precedent (throwaway local realm
        // secret, NOT under deploy/, NOT a production credential).
        _ = partiesUi
            .WithReference(keycloak)
            .WaitFor(keycloak)
            .WithEnvironment("Authentication__OpenIdConnect__Authority", realmUrl)
            .WithEnvironment("Authentication__OpenIdConnect__ClientId", "hexalith-parties-ui")
            .WithEnvironment("Authentication__OpenIdConnect__ClientSecret", "parties-ui-dev-secret")
            .WithEnvironment("Authentication__OpenIdConnect__Audience", "hexalith-eventstore");
    }
    else if (builder.ExecutionContext.IsPublishMode)
    {
        // Publish: tache realm. The client secret MUST come from configuration / a secret store,
        // never a committed literal. Source it from builder.Configuration (env / user-secrets).
        _ = partiesUi
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("Authentication__OpenIdConnect__Authority", PublishModeJwtAuthority)
            .WithEnvironment("Authentication__OpenIdConnect__ClientId", "hexalith-parties-ui")
            .WithEnvironment("Authentication__OpenIdConnect__ClientSecret", builder.Configuration["PartiesUi:OidcClientSecret"] ?? "")
            .WithEnvironment("Authentication__OpenIdConnect__Audience", "hexalith-eventstore");
    }
    ```
  - [x] **Do not** route `parties-ui` through the `WithJwtAuthentication(...)` helper (that wires JWT **bearer** resource-server validation for the actor hosts). `parties-ui` is an OIDC **relying party** — it uses `Authentication__OpenIdConnect__*`, not `Authentication__JwtBearer__*`.
  - [x] Preserve LF line endings and change only the intended lines (Story 1.1 review flagged a whole-file CRLF flip on this exact file — see *Build & verification gotchas*).

- [x] **Task 4 — Register the `hexalith-parties-ui` confidential Keycloak client** (AC: 1, 2)
  - [x] In `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json`, add a **new confidential client** to the `clients` array, modeled exactly on the Tenants realm's `hexalith-tenants-ui` (`references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/KeycloakRealms/hexalith-realm.json:85-166`). Use the parties-ui callback URL from `launchSettings.json` (`https://localhost:7210`):
    ```json
    {
      "clientId": "hexalith-parties-ui",
      "name": "Hexalith Parties UI",
      "description": "Confidential Blazor Server client for the Parties UI (authorization-code flow).",
      "enabled": true,
      "publicClient": false,
      "secret": "parties-ui-dev-secret",
      "standardFlowEnabled": true,
      "directAccessGrantsEnabled": false,
      "serviceAccountsEnabled": false,
      "fullScopeAllowed": true,
      "redirectUris": [ "https://localhost:7210/signin-oidc" ],
      "webOrigins": [ "https://localhost:7210" ],
      "attributes": { "post.logout.redirect.uris": "+" },
      "protocolMappers": [ /* copy the 4 mappers verbatim from hexalith-tenants-ui:
         audience-mapper (included.client.audience = hexalith-eventstore),
         tenants-mapper (eventstore:tenant), domains-mapper (eventstore:domain),
         permissions-mapper (eventstore:permission) */ ]
    }
    ```
  - [x] **`redirectUris` must match the actual browser-facing https origin.** Story 1.1 verified `parties-ui` serving `https://localhost:7210` + `http://localhost:5210` (launchSettings honored under `aspire run`). If `aspire run` proxies a different port in this environment, **align the redirectUri to the real https URL** (the OIDC `redirect_uri` is built from the request host) and re-verify; a mismatch yields a Keycloak "Invalid redirect_uri" error.
  - [x] Keep the existing public clients (`hexalith-parties`, `hexalith-eventstore`) and the test users (`admin-user`/`admin-pass`, `tenant-a-user`/`tenant-a-pass`, …) untouched — verify sign-in with `admin-user`.

- [x] **Task 5 — Auth-composition unit tests** (supports AC: 1, 2)
  - [x] In `tests/Hexalith.Parties.UI.Tests`, add `PartiesUiAuthenticationCompositionTests.cs` (DI-composition style, matching the existing `PartiesUiHostCompositionTests`). Build a `ServiceCollection`, add logging + `AddHttpContextAccessor()` + an in-memory `IConfiguration` providing the three OIDC keys, call `AddHexalithFrontComposerAuthentication(o => o.UseKeycloak(new Uri("https://idp.example/realms/hexalith"), "hexalith-parties-ui", "secret", "eventstore:tenant", "sub"))`, build the provider under `ValidateScopes=true`, then assert:
    - `IAuthenticationSchemeProvider` exposes the OIDC challenge scheme `"Hexalith.FrontComposer.Oidc"` and the cookie scheme `"Hexalith.FrontComposer.Cookie"` (server-side sign-in scheme).
    - `IUserContextAccessor` resolves to `ClaimsPrincipalUserContextAccessor` (the bridge swaps the seam — proves auth is wired, replacing the Quickstart's `NullUserContextAccessor`).
    - The OIDC options for the challenge scheme have `SaveTokens == true` and `SignInScheme == "Hexalith.FrontComposer.Cookie"` (resolve `IOptionsMonitor<OpenIdConnectOptions>.Get("Hexalith.FrontComposer.Oidc")`) — this is the **tokens-stay-server-side** guard for AC1 (tokens saved into the cookie/server ticket, browser gets only the encrypted cookie).
  - [x] Add a negative test: with **no** OIDC config keys, `authEnabled` is `false` and the host still composes (degrade-gracefully — assert the Quickstart chain + domain marker still build a provider under `ValidateScopes=true`, exactly as Story 1.1). This guards the "auth optional, host always boots" contract the conditional encodes.

- [x] **Task 6 — Extend the AppHost topology test for OIDC wiring** (supports AC: 1, 3)
  - [x] In `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiTopologyTests.cs`, add an assertion (or a sibling test) that the `parties-ui` resource carries the OIDC env vars. Use `Aspire.Hosting.Testing`'s `partiesUi.GetEnvironmentVariableValuesAsync()` and assert keys `Authentication__OpenIdConnect__ClientId == "hexalith-parties-ui"` and `Authentication__OpenIdConnect__Authority` is present/non-empty. Keep the existing graceful-skip wrapper (no Docker/DAPR needed; model-only inspection). Do **not** assert `Authentication__JwtBearer__*` on `parties-ui` — it must have none.

- [x] **Task 7 — Verify build gate + live sign-in flow** (AC: 1, 2, 3)
  - [x] `dotnet build Hexalith.Parties.slnx -c Release --no-restore` → **0 warnings** under solution-wide `TreatWarningsAsErrors`. Use **`-m:1`** for a reliable clean-build verdict (parallel clean builds flake — CS0006/MSB4018). Verify the UI host + both test projects compile clean.
  - [x] `bash scripts/check-no-warning-override.sh` passes; confirm **no `Version=`** on any new `PackageReference` (none expected — OIDC arrives transitively via the Shell; see *Library/framework*).
  - [x] `dotnet aspire run --project src/Hexalith.Parties.AppHost` (Docker running). Once `keycloak`, `eventstore`, `tenants` are healthy and `parties-ui` is Running, in a browser hit **`https://localhost:7210/authentication/challenge?returnUrl=/`** → redirected to Keycloak → sign in as `admin-user` / `admin-pass` → redirected to `/signin-oidc` → server-side cookie session established → returned to `/`. Confirm via browser devtools: the auth cookie is **HttpOnly** and there is **no access/bearer token in JS-readable storage**. Then hit **`/authentication/sign-out`** → cookie cleared + OIDC sign-out completes.
  - [x] _(Manual-flow verification; the AC mechanism is fully exercisable now even though the first **protected page** lands in Story 1.3 — the cookie middleware's `LoginPath` auto-redirect with `?returnUrl=` is the same challenge path.)_

## Dev Notes

> **Story 1.2 realizes architecture decision AR-D5 (host-owned OIDC).** The work is almost entirely **configuration + composition**, reusing the FrontComposer Shell's auth bridge (`AddHexalithFrontComposerAuthentication` / `UseKeycloak` / `MapHexalithFrontComposerAuthenticationEndpoints`). **Do not hand-roll** `AddAuthentication`/`AddOpenIdConnect`/`AddCookie`, a login UI, or a callback controller — the bridge owns all of it. [Source: architecture.md#Authentication-Security (D5); epics.md#Story-1.2]

### The canonical reference — copy `Hexalith.Tenants.UI`, don't invent

`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Program.cs` is a **sibling FrontComposer Blazor Server host that already does exactly this OIDC wiring**. Read it first and adapt name-for-name. The parts to copy: the `authEnabled` config gate, `AddHexalithFrontComposerAuthentication(o => o.UseKeycloak(...))`, the gated `UseAuthentication`/`UseAuthorization` placement, and `MapHexalithFrontComposerAuthenticationEndpoints()`. The one part **not** to copy for 1.2 is `AddTenantsTokenRelay()` + the EventStore/gateway client wiring (Tenants has a gateway to call; parties-ui's gateway client is a later story — see *Scope boundary*). Instead, parties-ui needs the small `RequireHttpsMetadata=false` PostConfigure that Tenants gets "for free" from `AddTenantsTokenRelay`. [Source: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Program.cs:42-112]

### How the FrontComposer auth bridge works (what you get for free)

`AddHexalithFrontComposerAuthentication(configure)` (in `Hexalith.FrontComposer.Shell/Extensions/FrontComposerAuthenticationServiceExtensions.cs`):
- Validates options eagerly, then `AddAuthentication` with `DefaultScheme = SignInScheme` (`"Hexalith.FrontComposer.Cookie"`) and `DefaultChallengeScheme = ChallengeScheme` (`"Hexalith.FrontComposer.Oidc"`).
- `AddCookie("Hexalith.FrontComposer.Cookie")` with secure defaults (`HttpOnly`, `SameSite=Lax`, `SecurePolicy=SameAsRequest`, no sliding expiration) and `LoginPath=/authentication/challenge`, `LogoutPath=/authentication/sign-out`.
- `AddOpenIdConnect("Hexalith.FrontComposer.Oidc")` with `SignInScheme=cookie`, `ResponseType="code"`, `CallbackPath=/signin-oidc`, `SignedOutCallbackPath=/signout-callback-oidc`, **`SaveTokens=true`**, `Scope = {openid, profile}`.
- Swaps framework seams to **`ClaimsPrincipalUserContextAccessor`** (replaces the Quickstart's fail-closed `NullUserContextAccessor`) and `FrontComposerAuthRedirector`, and registers `FrontComposerAccessTokenProvider` (reads the server-side token via `HttpContext.GetTokenAsync` — **this is why tokens never reach the browser**).

`MapHexalithFrontComposerAuthenticationEndpoints()` maps two GET routes: `/authentication/challenge` (→ `ChallengeAsync`, sanitizes `returnUrl` via `FrontComposerReturnUrl.Sanitize`, sets `RedirectUri`) and `/authentication/sign-out` (→ `SignOutAsync`). The OIDC handler middleware itself registers `/signin-oidc` and `/signout-callback-oidc`. [Source: FrontComposerAuthenticationServiceExtensions.cs:33-133, 219-301; FrontComposerAuthenticationOptions.cs:169-233]

### Endpoint inventory (reconcile with AC2)

After this story the host exposes, **all framework-provided, none hand-rolled**:
- `/authentication/challenge` (GET) — FrontComposer challenge route (preserves & sanitizes `returnUrl` → satisfies AC1's return-URL requirement).
- `/authentication/sign-out` (GET) — FrontComposer sign-out route (→ AC2 sign-out).
- `/signin-oidc` — OIDC handler callback (the "callback endpoint" AC2 names).
- `/signout-callback-oidc` — OIDC signed-out callback.

AC2's "the callback endpoint is the only extra host endpoint exposed" is satisfied in spirit: **no public command/query API** is added — only the standard OIDC/cookie auth endpoints, all from the framework. Document this inventory in the completion notes so the reviewer isn't surprised by the challenge/sign-out routes.

### Why tokens never reach the browser (AC1 mechanism)

`SaveTokens=true` stores the OIDC tokens in the **encrypted authentication ticket** (cookie / server-side ticket store), not in any JS-readable location. With Blazor **Interactive Server** (the render model from Story 1.1 / AR-D1), the browser holds only the opaque **HttpOnly** auth cookie — it cannot read the access/id tokens and cannot call the EventStore gateway directly. `FrontComposerAccessTokenProvider.GetAccessTokenAsync` later reads the token **server-side** via `HttpContext.GetTokenAsync`. Assert `SaveTokens==true` + cookie `HttpOnly` as the AC1 guards. [Source: architecture.md#Authentication-Security (D5: "OIDC tokens never leave the server"); FrontComposerAccessTokenProvider.cs]

### AppHost wiring specifics

- The `parties-ui` resource is registered at `Program.cs:116-120` as a `_ =` discard — **capture it as `IResourceBuilder<ProjectResource> partiesUi`** so the OIDC block can extend it.
- `keycloak` and `realmUrl` are computed **only in run mode** inside `if (enableKeycloak) { if (IsRunMode) { … } }` (lines 215-225), so `realmUrl is not null` ⇒ run mode ⇒ `.WithReference(keycloak)` is valid. Mirror the **`adminUI` precedent** (lines 280-303) for the run/publish/else branches.
- `realmUrl` = `http://{keycloak.http}/realms/hexalith`. It is **http**, hence the Development `RequireHttpsMetadata=false` in Task 1.
- Publish authority is `PublishModeJwtAuthority` = `http://auth.tache.ai:8080/realms/tache` (const at `Program.cs:14-15`). The publish client secret must come from `builder.Configuration`, never a literal. [Source: src/Hexalith.Parties.AppHost/Program.cs:14-15,116-120,212-303]

### Keycloak realm client (Task 4)

The parties realm (`KeycloakRealms/hexalith-realm.json`) currently has only the **public** `hexalith-parties` and `hexalith-eventstore` clients (direct-access-grant, `standardFlowEnabled:false`) — neither supports the authorization-**code** browser flow. You must add the **confidential** `hexalith-parties-ui` client (`publicClient:false`, `secret`, `standardFlowEnabled:true`, `redirectUris`, `webOrigins`, post-logout `+`, and the 4 protocol mappers). Copy the shape verbatim from the Tenants realm's `hexalith-tenants-ui`; change only `clientId`, `secret`, `redirectUris`/`webOrigins` (→ `https://localhost:7210`), and `description`. The 4 mappers inject `eventstore:tenant` / `eventstore:domain` / `eventstore:permission` claims and the `hexalith-eventstore` audience — keep them so later stories (1.3 role landing, 1.4 `party_id`, transport) have the claims they need. [Source: src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json; references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/KeycloakRealms/hexalith-realm.json:85-166]

### Scope boundary — what is explicitly NOT in this story

This story is **sign-in only**. Do not pull forward:
- **Role-based landing + `Admin`/`Consumer` policies + nav gating** → **Story 1.3**. (Register no policies here. If `UseAuthorization()` needs services, add bare `AddAuthorizationCore()` with no policies.)
- **`party_id` claim resolution / `PartyIdClaimResolver` / `NoPartyBinding`** → **Story 1.4**.
- **Self-scope accessor / `IDataSubjectAccessService`** → **Story 1.5**.
- **Gateway client + token relay** (`AddPartiesClient`, `requestCustomizer`, an `AddTenantsTokenRelay` equivalent) → lands with the **transport/Admin** work when there is actually a gateway client to relay to. The bridge's `EventStoreAccessTokenProvider` seam is wired (harmless without a consuming client). _(Story 1.1's note loosely mapped "Parties.Client → 1.2"; the 1.2 ACs do not require it, and adding an unused client + relay would violate this story's minimal-endpoint posture. Wire it where it is consumed.)_
- **`ServiceDefaults` / health endpoints** → **Story 1.10**.
[Source: epics.md#Story-1.3..1.5,#Story-1.10; architecture.md#Implementation-sequence]

### Library / framework requirements

- **No new `PackageReference`.** `Microsoft.AspNetCore.Authentication.OpenIdConnect` (10.0.8) and the auth bridge live in `Hexalith.FrontComposer.Shell`, which `Hexalith.Parties.UI` already references (Story 1.1). Confirmed: the OIDC dll is already in the host's build output and `project.assets.json`. **Central Package Management** owns versions — adding a `Version=` anywhere is a build error (AC fails). `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.8` is the only auth package in `Directory.Packages.props`; you need neither it nor a new OIDC entry. [Source: Directory.Packages.props:45; src/Hexalith.Parties.UI/obj/project.assets.json; project-context.md#Technology-Stack]
- **`OpenIdConnectOptions`** lives in `Microsoft.AspNetCore.Authentication.OpenIdConnect` namespace — add the `using` for the Task 1 PostConfigure.

### File structure requirements

| Change | File | New/Edit |
|---|---|---|
| OIDC composition + middleware | `src/Hexalith.Parties.UI/Program.cs` | EDIT |
| OIDC config scaffold (no secret) | `src/Hexalith.Parties.UI/appsettings.json` | EDIT |
| `parties-ui` OIDC env + keycloak ref | `src/Hexalith.Parties.AppHost/Program.cs` | EDIT |
| `hexalith-parties-ui` confidential client | `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json` | EDIT |
| Auth-composition tests | `tests/Hexalith.Parties.UI.Tests/PartiesUiAuthenticationCompositionTests.cs` | NEW |
| Topology OIDC-env assertion | `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiTopologyTests.cs` | EDIT |

Keep code where it belongs: auth/composition is host-level (`Hexalith.Parties.UI`), never in `Contracts`/`Server`/`Projections`. The host stays adopter-facing-at-shell-level only — do **not** add references to internal projects. [Source: project-context.md#Code-Quality-Style-Rules; architecture.md#Complete-Project-Directory-Structure]

### Testing requirements

- **xUnit v3 + Shouldly + NSubstitute + bUnit** only — no Moq/FluentAssertions/raw `Assert.*` (except the sanctioned `Assert.Skip` already used in the topology test). [Source: project-context.md#Testing-Rules]
- The existing UI tests are **DI-composition** tests over a `ServiceCollection` (not a running web host) — model the auth tests on `PartiesUiHostCompositionTests.cs`. The test project (`Microsoft.NET.Sdk.Razor`) references the UI project, so the ASP.NET Core authentication types (`IAuthenticationSchemeProvider`, `OpenIdConnectOptions`, `IUserContextAccessor`) are available transitively.
- Topology test: keep the **graceful-skip** wrapper (`Assert.Skip` when the model can't be built) — integration-lane convention; a skip is not a failure. The `IntegrationTests.csproj` already copies `DaprComponents/**` so the model builds without a DAPR runtime (Story 1.1). [Source: tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiTopologyTests.cs; memory `xunit-v3-mtp-single-dash-filters`]
- Run lanes via `scripts/test.ps1 -Lane <unit|integration|topology>`; `integration`/`topology` skip gracefully without Docker/DAPR. If running test EXEs directly, use xUnit v3 `-class`/`-method` (the `dotnet test --filter` path reports "Zero tests ran"). [Source: project-context.md#Testing-Rules; memory `xunit-v3-mtp-single-dash-filters`]

### Previous-story intelligence (Story 1.1)

- **Story 1.1 stood up the bootable host**: `Program.cs`, `PartiesUiDomainMarker`, `Components/{App,Routes,_Imports,Layout/MainLayout}`, appsettings, launchSettings, the `parties-ui` AppHost resource (no DAPR sidecar, auto-start), and the `Hexalith.Parties.UI.Tests` + `PartiesUiTopologyTests` harness. Build is green (0 warnings, `-m:1`). `Routes.razor` already wraps `<CascadingAuthenticationState>` (1.1 added it "ready for 1.2's auth") — **reuse it, do not re-add**. [Source: 1-1-*.md#File-List, #Dev-Agent-Record]
- **`<NoWarn>HFC1001</NoWarn>`** is present in the host csproj (empty-marker SourceTools analyzer). Leave it; this story still declares no `[Command]`/`[Projection]`. Do not add new NoWarns unless a specific analyzer fires — then narrow, single-rule, this-project-only, with a removal note (the sanctioned escape valve). [Source: 1-1-*.md#Debug-Log; project-context.md#Language-Specific-Rules]
- **`GET /` returns 404** on the minimal host (no routable pages yet) — expected, not a regression. The Blazor pipeline is alive. Your sign-in verification uses `/authentication/challenge`, not `/`. [Source: 1-1-*.md#Completion-Notes (AC2)]
- **Review caught a whole-file LF→CRLF flip** on `Hexalith.Parties.slnx` / `AppHost.csproj` / `AppHost/Program.cs` (the repo tree is uniformly LF in HEAD despite `.editorconfig` `crlf`). You are editing `AppHost/Program.cs` again — **preserve LF**, change only the intended lines, and keep your diff minimal so review can see exactly what changed. [Source: 1-1-*.md#Senior-Developer-Review]
- **`aspire run` topology gotcha**: with `EnableKeycloak=false`, `eventstore`/`tenants`/`parties` abort (`Authentication:JwtBearer requires Authority or SigningKey`). Run the **default** topology (Keycloak ON) — which this story now also depends on for `parties-ui` OIDC. [Source: 1-1-*.md#Debug-Log (AC2 local-run note)]

### Git intelligence

HEAD is `8e3fe0c feat(story-1.1): Stand up the Hexalith Parties UI Blazor Server host` (baseline for this story). The only recent product change is Story 1.1's host stand-up — there is no prior auth code in `parties-ui` to regress. Commit on a typed branch (`feat/...`) with a Conventional Commit (`feat(story-1.2): host-owned OIDC sign-in for parties-ui`). [Source: git log; project-context.md#Development-Workflow-Rules]

### Build & verification gotchas (read before declaring done)

- **`TreatWarningsAsErrors` is solution-wide and absolute.** File-scoped namespaces; `using` outside the namespace, `System.*` first; `_camelCase` private fields; `Async` suffix; interfaces `I*` — all enforced as errors. CRLF/4-space/final-newline/UTF-8 per `.editorconfig`. [Source: project-context.md#Language-Specific-Rules]
- **Central Package Management ON** — no `Version=` on any `PackageReference`. [Source: project-context.md#Technology-Stack]
- **Clean parallel builds flake** (CS0006 / MSB4018) — use **`-m:1`** for a reliable clean-build verdict. [Source: memory `parties-parallel-build-flake`]
- **`bash scripts/check-no-warning-override.sh` must pass** (no `-p:TreatWarningsAsErrors=false`, no global override). [Source: project-context.md; docs/build-gate.md]
- **Pre-existing, out-of-scope full-`.slnx` failures are NOT your regression**: `Hexalith.PolymorphicSerializations` pack errors (NU5118/NU5128) and the `*PackageTests` failures fail on a clean tree independent of this story. Verify your work via **per-project compile** (`dotnet build src/Hexalith.Parties.UI` + the two test projects) and the unit/topology lanes, not the full-solution pack. [Source: 1-1-*.md#Debug-Log; memory `parties-pack-tests-preexisting-fail`]
- **Submodules** must be present as **`references/` checkouts** (FrontComposer, EventStore, Tenants): `git submodule update --init references/Hexalith.EventStore references/Hexalith.Tenants` (root-repository submodules only, **never `--recursive`**). [Source: project-context.md#Technology-Stack]
- **Do not "align"** `Microsoft.Extensions.Hosting.Abstractions` 11.0.0-preview down — load-bearing for `[LoggerMessage]`. [Source: memory `hosting-abstractions-preview-pin-load-bearing`]
- **Aspire SDK skew is intentional** — do not touch `Aspire.AppHost.Sdk` / `Aspire.Hosting` versions or DCP dies (`unknown flag: --tls-cert-file`). [Source: memory `aspire-apphost-sdk-version-match`]
- **Aspire MCP can't see a sandbox `aspire run`** — fall back to logs/docker/ps when verifying the live flow. [Source: memory `aspire-mcp-blind-to-sandbox-apphost`]

### Project Structure Notes

- This story fills the **`Authentication` / Account** slice of the architecture's `UI/` tree only at the **sign-in mechanism** level — `PartyIdClaimResolver`, `PartiesUiAuthorization` (policies), and `Components/Account/{RoleLandingRedirect,NoPartyBinding}` are **later stories (1.3/1.4)**, not here. The OIDC wiring lives inline in `Program.cs` (mirroring `Tenants.UI`), not in a separate `Authentication/` folder yet — that folder materializes when 1.3/1.4 add the resolver/policies. This is an intentional, scoped variance from the full tree, not a conflict. [Source: architecture.md#Complete-Project-Directory-Structure; #Requirements→Structure-Mapping]
- Config shape uses `Authentication:OpenIdConnect:*` (`__`-nested env override), aligning with the architecture's "OIDC + `Parties:BaseUrl` + SignalR endpoints; no secrets committed." [Source: architecture.md#File-Organization-Workflow]

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.2] — story statement + AC groups.
- [Source: _bmad-output/planning-artifacts/epics.md#Additional-Requirements] — AR-D5 (host-owned OIDC, server-side cookie, tokens never reach the browser, Keycloak run / `tache` publish), AR-D2/D3 (downstream — out of 1.2 scope).
- [Source: _bmad-output/planning-artifacts/architecture.md#Authentication-Security] — D5/D2/D3 decisions; "OIDC tokens never leave the server."
- [Source: _bmad-output/planning-artifacts/architecture.md#Implementation-sequence] — sequencing (sign-in is step 1; role routing/`party_id` are step 2).
- [Source: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Program.cs:42-112] — the canonical sibling OIDC wiring to mirror.
- [Source: references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Extensions/FrontComposerAuthenticationServiceExtensions.cs] — `AddHexalithFrontComposerAuthentication` / `MapHexalithFrontComposerAuthenticationEndpoints` (the bridge).
- [Source: references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Options/FrontComposerAuthenticationOptions.cs:37-58,169-233] — `UseKeycloak` recipe + OIDC/Redirect/Cookie option defaults (schemes, callback paths, `SaveTokens`).
- [Source: src/Hexalith.Parties.AppHost/Program.cs:14-15,116-120,212-303] — `parties-ui` registration, keycloak block, `realmUrl`, `adminUI` run/publish precedent, publish authority const.
- [Source: src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json + references/Hexalith.Tenants/.../KeycloakRealms/hexalith-realm.json:85-166] — current parties clients/users + the `hexalith-tenants-ui` confidential-client template.
- [Source: tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs; tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiTopologyTests.cs] — test harness to extend.
- [Source: _bmad-output/implementation-artifacts/1-1-*.md] — previous-story intelligence (host skeleton, CRLF caution, run-topology gotcha, pre-existing pack failures).
- [Source: _bmad-output/project-context.md] — language/framework/testing/quality/workflow rules + anti-patterns (PII hygiene, no committed secrets, `__`-nested config, build gate).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Opus 4.8)

### Debug Log References

- **Build:** Per-project Release builds with `-m:1` (the reliable clean-build flag — clean parallel builds flake per memory `parties-parallel-build-flake`). `Hexalith.Parties.UI.Tests` and `Hexalith.Parties.IntegrationTests` (which transitively build the UI host + AppHost) → **0 warnings, 0 errors** under solution-wide `TreatWarningsAsErrors`. Full-`.slnx` pack failures (PolymorphicSerializations NU5118/NU5128, `*PackageTests`) are pre-existing and out of scope (memory `parties-pack-tests-preexisting-fail`); verified via per-project compile + the unit/topology lanes instead.
- **Build gate:** `bash scripts/check-no-warning-override.sh` → OK. No `Version=` added to any `PackageReference` (CPM owns versions; OIDC arrives transitively via `Hexalith.FrontComposer.Shell`).
- **Tests:** `Hexalith.Parties.UI.Tests` → **10/10 passed** (4 host-composition + 4 auth-composition + 2 OIDC-config-hygiene). Full `Hexalith.Parties.IntegrationTests` regression → **28 total, 22 passed, 0 failed, 6 skipped** (the 6 are Docker-dependent integration tests that skip gracefully by design; the 3 topology + 2 realm-validation tests all pass). _(Updated 2026-06-10 by Senior Developer Review (AI): the original record listed 6 UI / 26 integration — the `bmad-qa-generate-e2e-tests` run later added +2 auth-composition tests, the 2 OIDC-config-hygiene tests, and the 2 Keycloak-realm tests; counts re-verified live by running the test EXEs.)_
- **Topology-test API note:** the story-suggested `IResourceWithEnvironment.GetEnvironmentVariableValuesAsync(...)` is obsolete in the resolved Aspire 13.4.x (CS0618 → error). Replaced with running the resource's `EnvironmentCallbackAnnotation`s through an `EnvironmentCallbackContext(DistributedApplicationExecutionContext(Publish), partiesUi, …)` — no app build, no Docker, deterministic publish-mode rendering. Test-method awaits use `ConfigureAwait(true)` (CA2007 + xUnit1030 both enabled).
- **AppHost CS8604 avoidance:** the story snippet put `.WithReference(keycloak)` inside `if (realmUrl is not null)`, where the compiler can't prove the nullable `keycloak` is non-null (CS8604 under TreatWarningsAsErrors; project rules forbid `!`). Mirrored the `adminUI` precedent instead: `partiesUi.WithReference(keycloak).WaitFor(keycloak)` lives in the `if (keycloak is not null)` block; the realm/publish conditional only sets env vars.
- **Live flow (AC1/AC2):** brought up the full topology with `aspire run` (Docker) and verified end-to-end — see Completion Notes.

### Completion Notes List

Implemented host-owned OIDC sign-in (AR-D5) entirely as composition/config over the FrontComposer auth bridge — no hand-rolled `AddAuthentication`/`AddOpenIdConnect`/`AddCookie`, no login UI, no callback controller.

**Automated verification:**
- AC1 guard (tokens stay server-side): unit test asserts the OIDC challenge scheme has `SaveTokens == true` and `SignInScheme == "Hexalith.FrontComposer.Cookie"`, both schemes (`Hexalith.FrontComposer.Oidc` + `Hexalith.FrontComposer.Cookie`) are registered, and the bridge swaps `IUserContextAccessor` → `ClaimsPrincipalUserContextAccessor`.
- "Auth optional, host always boots": negative unit test asserts that with no OIDC config the host still composes under `ValidateScopes=true` and the seam stays the fail-closed `NullUserContextAccessor`.
- AC3 wiring: topology test asserts `parties-ui` carries `Authentication__OpenIdConnect__ClientId == "hexalith-parties-ui"` + a non-empty `Authority`, and **no** `Authentication__JwtBearer__*` (it is an OIDC relying party, not a JWT resource server).

**Live verification (full `aspire run` topology, browser-driven):**
- `GET /authentication/challenge?returnUrl=/` → **302** to `https://localhost:8180/realms/hexalith/protocol/openid-connect/auth?client_id=hexalith-parties-ui&…` (the OIDC handler discovered metadata from the **http** dev Keycloak — confirms the Development `RequireHttpsMetadata=false` fix is load-bearing) with HttpOnly correlation/nonce cookies.
- Completed the interactive sign-in as `admin-user`/`admin-pass` → `/signin-oidc` code exchange → server-side cookie session → returned to `/` (renders 404, expected for the minimal host per Story 1.1).
- Auth cookie `.AspNetCore.Hexalith.FrontComposer.Cookie` (+ chunked `C1/C2/C3`, large encrypted ticket holding the SaveTokens'd tokens) is **`httpOnly: true, secure: true`**. On the `localhost:7210` origin, `document.cookie` is empty and `localStorage`/`sessionStorage` are empty → **no access/bearer/id token is JS-readable**. This is the AC1 "tokens never reach the browser" guarantee, confirmed live.
- AC2: `GET /authentication/sign-out` → **302** to `/`; and `/api/v1/commands`, `/api/v1/queries`, `/process` all return **404** → no public command/query API; only framework auth endpoints exist.

**Endpoint inventory (AC2):** `/authentication/challenge` (GET), `/authentication/sign-out` (GET) — FrontComposer bridge; `/signin-oidc`, `/signout-callback-oidc` — OIDC handler. All framework-provided; none hand-rolled.

**Secrets posture (AC3):** `appsettings.json` carries an empty-string OIDC scaffold only; `appsettings.Development.json` has no OIDC values. The dev client secret (`parties-ui-dev-secret`) lives only in the AppHost run-mode block + the local dev realm import — a throwaway local-realm secret, NOT under `deploy/` and matching the `Hexalith.Tenants.UI` precedent. Publish-mode sources the secret from `builder.Configuration["PartiesUi:OidcClientSecret"]`, never a committed literal.

**Scope held:** sign-in only — no token relay / gateway client, no role landing, no `party_id` resolution, no `Admin`/`Consumer` policies (Stories 1.3–1.5+). `AddAuthorizationCore()` was NOT added explicitly because the FrontComposer Quickstart chain already registers it.

### File List

- `src/Hexalith.Parties.UI/Program.cs` (modified) — `AddHttpContextAccessor`, `authEnabled` gate, `AddHexalithFrontComposerAuthentication(UseKeycloak)`, Development-only `RequireHttpsMetadata=false` PostConfigure, gated `UseAuthentication`/`UseAuthorization` + `MapHexalithFrontComposerAuthenticationEndpoints`.
- `src/Hexalith.Parties.UI/appsettings.json` (modified) — empty-string `Authentication:OpenIdConnect` scaffold (no secret).
- `src/Hexalith.Parties.AppHost/Program.cs` (modified) — captured `partiesUi` resource; added `partiesUi.WithReference(keycloak).WaitFor(keycloak)` to the keycloak block; added the run/publish OIDC env conditional.
- `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json` (modified) — added the `hexalith-parties-ui` confidential client (authorization-code flow, `https://localhost:7210/signin-oidc`, 4 protocol mappers).
- `tests/Hexalith.Parties.UI.Tests/PartiesUiAuthenticationCompositionTests.cs` (new) — auth-composition tests: schemes + SaveTokens + accessor swap; authorization-code flow shape (ResponseType/callbacks/scopes); HttpOnly server-side cookie + challenge/sign-out paths; negative degrade-gracefully (4 tests + shared `BuildConfiguredAuthBridgeProvider` helper).
- `tests/Hexalith.Parties.UI.Tests/PartiesUiOidcConfigurationTests.cs` (new, QA-automation run) — AC3 secret-hygiene guards: empty OIDC scaffold in `appsettings.json` + dev-client-secret leak sweep across `appsettings*.json`.
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiTopologyTests.cs` (modified) — added `PartiesUiResource_CarriesOidcEnv_AndNoJwtBearer` (asserts ClientId + Authority + `Audience == hexalith-eventstore`, and no `Authentication__JwtBearer__*`).
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiKeycloakRealmTests.cs` (new, QA-automation run) — Task 4 realm-validation: confidential `hexalith-parties-ui` client shape (publicClient=false, standardFlow, secret, `https://localhost:7210/signin-oidc` redirectUri) + the `hexalith-eventstore` audience mapper.

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-06-10 | 1.0 | Implemented Story 1.2 — host-owned OIDC sign-in (AR-D5): FrontComposer auth bridge wiring in the UI host, OIDC config scaffold, AppHost env + Keycloak `hexalith-parties-ui` confidential client, auth-composition + topology tests. All ACs verified (automated + live browser flow). | Amelia (Dev Agent, claude-opus-4-8) |
| 2026-06-10 | 1.1 | Senior Developer Review (AI): all ACs/tasks verified against code; build (0 warnings) + build-gate + both test suites re-run green. Synced File List with the 2 QA-added test files, corrected stale test-count metrics, set Status → done. No code defects found. | Senior Developer Review (AI, claude-opus-4-8) |

## Senior Developer Review (AI)

**Reviewer:** Jérôme · **Date:** 2026-06-10 · **Outcome:** ✅ Approve → **done**

Adversarial review of the implementation against the story's AC/task claims, with every build/test claim re-verified independently.

### Verification performed
- **Build (re-run):** per-project Release `-m:1` — `Hexalith.Parties.UI.Tests` (builds the UI host) and `Hexalith.Parties.IntegrationTests` (builds the AppHost) → **0 warnings, 0 errors** under solution-wide `TreatWarningsAsErrors`. `bash scripts/check-no-warning-override.sh` → **OK**. No `Version=` added (CPM intact).
- **Tests (re-run live, xUnit v3 EXEs):** `Hexalith.Parties.UI.Tests` → **10/10 passed**; `Hexalith.Parties.IntegrationTests` → **28 total, 22 passed, 0 failed, 6 graceful Docker-skips**.
- **Source faithfulness:** confirmed the OIDC scheme constants the tests assert against the framework defaults in `Hexalith.FrontComposer.Shell/Options/FrontComposerAuthenticationOptions.cs` (`ChallengeScheme = "Hexalith.FrontComposer.Oidc"`, `SignInScheme = "Hexalith.FrontComposer.Cookie"`) and the accessor swap to `ClaimsPrincipalUserContextAccessor` in `FrontComposerAuthenticationServiceExtensions.cs:68`. UI `Program.cs` middleware order + gating matches the `Hexalith.Tenants.UI` sibling exactly.

### AC coverage
- **AC1** (code flow / server-side cookie / tokens-never-reach-browser / return-URL): ✅ `SaveTokens=true` + `SignInScheme=cookie` + HttpOnly cookie asserted in unit tests; `ResponseType=code` + callbacks + `LoginPath=/authentication/challenge`; live browser flow recorded in Dev Agent Record.
- **AC2** (sign-out + endpoint surface): ✅ `LogoutPath`/`SignedOutCallbackPath` asserted; no-`JwtBearer` env guarded in topology test; "no public command/query API" live-verified.
- **AC3** (no committed secret; env-overridable): ✅ empty-scaffold + dev-secret leak-sweep tests over `appsettings*.json`; AppHost env wiring; publish secret sourced from configuration.

### Findings
| Sev | Finding | Disposition |
|---|---|---|
| MEDIUM | File List omitted `PartiesUiKeycloakRealmTests.cs` + `PartiesUiOidcConfigurationTests.cs` (present in git, added by the QA-automation run). | **Fixed** — File List synced. |
| MEDIUM | Stale Dev Agent Record metrics (6 UI / 26 integration) and understated auth-composition test count (2 vs 4). | **Fixed** — Debug Log corrected to 10 UI / 28 integration, re-verified live. |
| LOW | `Authentication:OpenIdConnect:Audience` is scaffolded + AppHost-injected + topology-asserted but unread by the relying party (the `hexalith-eventstore` audience is stamped by the Keycloak `audience-mapper`, not requested by the client). | **Noted, intentional** — harmless forward-wiring for later transport stories; removal would break the AC3 audience assertion. |

**No CRITICAL or HIGH findings.** Implementation is correct, faithful to AR-D5, and within scope (sign-in only; no token relay / role landing / `party_id` / policies — correctly deferred to 1.3–1.5+).

_Reviewer: Jérôme on 2026-06-10_
