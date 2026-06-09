using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.Parties.UI;
using Hexalith.Parties.UI.Components;

using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.FluentUI.AspNetCore.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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
        tenantClaimType: "eventstore:tenant",
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

app.Run();
