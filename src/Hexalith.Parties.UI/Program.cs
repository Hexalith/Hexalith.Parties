using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Client.Extensions;
using Hexalith.Parties.ServiceDefaults;
using Hexalith.Parties.UI;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.Components;
using Hexalith.Parties.UI.Services;

using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.FluentUI.AspNetCore.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// ADR-030 — ValidateScopes=true so a Singleton capturing a Scoped service fails at boot
// (not silently leak across tenants). MUST sit on the host builder before service resolution.
builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();

// The access-token provider seam reads HttpContext.GetTokenAsync server-side; the auth bridge adds
// it defensively, but registering it explicitly mirrors Hexalith.Tenants.UI.
builder.Services.AddHttpContextAccessor();

// Quickstart chains AddLocalization + AddHexalithShellLocalization + AddHexalithFrontComposer.
builder.Services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(Program).Assembly));
builder.Services.AddFrontComposerDevMode(builder.Environment);
builder.Services.AddHexalithDomain<PartiesUiDomainMarker>();

// Story 1.3 (AR-D2) — register the role-claim Admin + Consumer policies UNCONDITIONALLY (not gated
// on authEnabled). <AuthorizeView Policy=…> on the nav entries and [Authorize(Policy = …)] on the
// /admin + /me areas must resolve these policies whether or not interactive OIDC is wired (tests,
// degraded boot). Mirrors Hexalith.Tenants.UI's unconditional AddAuthorizationCore(AddPolicy(...)).
builder.Services.AddPartiesUiAuthorization();

// Story 1.4 (AR-D2) — register the fail-closed party_id claim-resolution services UNCONDITIONALLY
// (not gated on authEnabled), mirroring AddPartiesUiAuthorization above. The Scoped PartyIdClaimResolver
// must resolve in every boot mode (tests, degraded boot, full sign-in) so RoleLandingRedirect can divert
// an unbound Consumer away from /me. The IClaimsTransformation it also registers is only INVOKED when
// UseAuthentication is wired, so unconditional registration is harmless. Both are Scoped (ADR-030):
// ValidateScopes=true (line 13) fails the boot if a Singleton captures them.
builder.Services.AddPartiesUiClaimsResolution();

// Story 1.5 (AR-D3 / ADR-030) — register the consumer self-scope choke point UNCONDITIONALLY (not
// gated on partiesClientEnabled), mirroring AddPartiesUiAuthorization / AddPartiesUiClaimsResolution
// above. The Scoped ISelfScopedPartiesClient is the SINGLE own-data-only data path for a Consumer: it
// resolves and injects the consumer's own party_id and, by the shape of its interface, can never issue
// list/search. It is Scoped (claims/circuit-derived); ValidateScopes=true (line 13) fails the boot if a
// Singleton captures it. Its gateway-client dependencies are resolved LAZILY — nothing resolves the
// accessor until a consumer data page exists (today /me is an empty stub), so unconditional registration
// composes cleanly even in a no-Parties:BaseUrl (degraded/test) boot.
builder.Services.AddSelfScopedPartiesClient();

// Story 1.7 (AR-D6 / ADR-030) — register the shared live-freshness mechanism (SignalR projection
// subscription + the optimistic-reconcile primitive + the degraded fallback) UNCONDITIONALLY (not gated
// on partiesClientEnabled), mirroring AddSelfScopedPartiesClient above. All services are Scoped
// (per-circuit; the multi-tenant BFF must never share one tenant/token Singleton connection across users),
// so ValidateScopes=true (line 13) fails the boot if a Singleton captures them. Composition is lazy/inert:
// the stream stays inert when EventStore:SignalR:HubUrl is empty/whitespace (test/degraded boot → nothing
// connects, IsConnected=false → polling fallback) and nothing resolves until a screen consumes the
// primitive (no page does yet — this story ships the mechanism + tests). No page/route/[Command] here.
builder.Services.AddPartiesProjectionFreshness(builder.Configuration);

// Register the UNDERLYING typed gateway clients (IPartiesQueryClient / IAdminPortalGdprClient) only when
// configured. AddPartiesClient throws at REGISTRATION time when Parties:BaseUrl is absent
// (GetValidatedBaseAddress), so gate it exactly like the authEnabled block below — calling it
// unconditionally would break degraded/test boot. Story 1.5 deliberately does NOT wire the OIDC→gateway
// token relay here: attaching the server-side access token to the consumer's gateway calls is the
// deferred residual (lands with the first consumer screen that fetches data, Epic 4 / Story 1.7). 1.5's
// deliverable is the structural choke point + fail-closed/Scoped guarantees, proven by tests.
bool partiesClientEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["Parties:BaseUrl"]);

if (partiesClientEnabled)
{
    builder.Services.AddPartiesClient(builder.Configuration);

    // Story 1.7 (AR-D6 / NFR8) — capture the Parties host's degradation headers
    // (X-Service-Degraded / X-Stale-Data-Age, set on GET responses by DegradedResponseMiddleware) into the
    // per-circuit IDegradedStateAccessor. Gated INSIDE the Parties:BaseUrl block (alongside AddPartiesClient)
    // so degraded/test boot is unaffected. Attached by the typed clients' default names (nameof(TClient));
    // POST responses carry no such headers (the handler only inspects GETs). The primary degraded signal
    // remains ProjectionFreshnessMetadata on PartyDetail.Freshness — this header seam is the secondary,
    // transport-level signal, captured when the gateway relays it (verify-live; see Dev Agent Record).
    builder.Services.AddTransient<DegradedResponseHeaderHandler>();
    builder.Services.AddHttpClient(nameof(IPartiesQueryClient)).AddHttpMessageHandler<DegradedResponseHeaderHandler>();
    builder.Services.AddHttpClient(nameof(IAdminPortalGdprClient)).AddHttpMessageHandler<DegradedResponseHeaderHandler>();
}

// AR-D5 — host-owned OIDC sign-in. When an OIDC provider is configured (the AppHost supplies the
// Keycloak authority/client in run mode against the dev realm, the tache realm in publish), wire
// the FrontComposer auth bridge: authorization-code login into a server-side cookie session. The
// OIDC tokens are stored in the encrypted authentication ticket (SaveTokens=true) and never reach
// the browser — the browser holds only the HttpOnly auth cookie. Mirrors Hexalith.Tenants.UI; the
// gateway client + token relay (AddTenantsTokenRelay equivalent), role landing, party_id and the
// Admin/Consumer policies are later stories (1.3+) and are intentionally NOT wired here.
bool authEnabled =
    Uri.TryCreate(builder.Configuration["Authentication:OpenIdConnect:Authority"], UriKind.Absolute, out Uri? oidcAuthority)
    && !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:OpenIdConnect:ClientId"])
    && !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:OpenIdConnect:ClientSecret"]);

if (authEnabled)
{
    builder.Services.AddHexalithFrontComposerAuthentication(o => o.UseKeycloak(
        oidcAuthority!,
        builder.Configuration["Authentication:OpenIdConnect:ClientId"]!,
        builder.Configuration["Authentication:OpenIdConnect:ClientSecret"]!,
        tenantClaimType: PartiesUiAuthorization.TenantClaimType,
        userClaimType: "sub"));
}

// Dev-only: the run-mode Keycloak authority is http (keycloak.GetEndpoint("http")), but the OIDC
// handler defaults RequireHttpsMetadata=true and would reject an http metadata address. The auth
// bridge does not set this (Tenants gets it for free via AddTenantsTokenRelay, which this story
// does not wire), so relax it ONLY in Development, targeting the FrontComposer OIDC challenge
// scheme (FrontComposerOpenIdConnectOptions default ChallengeScheme).
if (authEnabled && builder.Environment.IsDevelopment())
{
    builder.Services.PostConfigure<OpenIdConnectOptions>(
        "Hexalith.FrontComposer.Oidc",
        o => o.RequireHttpsMetadata = false);
}

// Story 1.3 (Task 6) — map the Keycloak realm-role claim to ASP.NET roles. The realm-roles-mapper on
// the hexalith-parties-ui client emits roles flat under "roles"; pointing RoleClaimType at it makes
// ClaimsPrincipal.IsInRole / RequireRole (the Admin + Consumer policies) evaluate against the signed-in
// user's realm roles. Applied whenever auth is wired (the OIDC scheme exists only then).
// VERIFY LIVE: the FrontComposer bridge may surface the claim differently (e.g. nested under
// realm_access.roles) — if the observed claim path differs, align this RoleClaimType (or the mapper's
// claim.name) to the live claim, exactly as Story 1.2 reconciled the http-metadata gotcha empirically.
if (authEnabled)
{
    builder.Services.PostConfigure<OpenIdConnectOptions>(
        "Hexalith.FrontComposer.Oidc",
        o => o.TokenValidationParameters.RoleClaimType = "roles");
}

WebApplication app = builder.Build();

app.MapStaticAssets();
app.UseStaticFiles();
app.UseRequestLocalization();

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (authEnabled)
{
    _ = app.MapHexalithFrontComposerAuthenticationEndpoints();
}

app.MapDefaultEndpoints();

app.Run();
