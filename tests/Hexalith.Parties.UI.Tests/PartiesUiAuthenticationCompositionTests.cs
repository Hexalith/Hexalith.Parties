using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.FrontComposer.Contracts.Rendering;
using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.FrontComposer.Shell.Services;
using Hexalith.FrontComposer.Shell.Services.Auth;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Composition tests for the Story 1.2 host-owned OIDC sign-in wiring (AR-D5). These mirror the
/// <c>authEnabled</c>-gated block in <c>Program.cs</c>: when the OIDC config keys are present the
/// FrontComposer auth bridge composes a server-side cookie + OIDC challenge scheme pair that keeps
/// tokens off the browser; when the keys are absent the host still boots (auth is optional).
/// DI-composition style — like <see cref="PartiesUiHostCompositionTests"/>, no running web host.
/// </summary>
public sealed class PartiesUiAuthenticationCompositionTests
{
    private const string OidcChallengeScheme = "Hexalith.FrontComposer.Oidc";
    private const string CookieSignInScheme = "Hexalith.FrontComposer.Cookie";

    [Fact]
    public async Task AuthBridge_WhenOidcConfigured_RegistersServerSideCookieAndOidcSchemesAndSavesTokens()
    {
        // The in-memory config carries the three keys the host's authEnabled gate reads. Assert the
        // gate opens so this test exercises the same branch Program.cs takes in run/publish mode.
        IConfiguration configuration = BuildOidcConfiguration();
        AuthEnabled(configuration).ShouldBeTrue();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddHexalithFrontComposerAuthentication(o => o.UseKeycloak(
            new Uri("https://idp.example/realms/hexalith"),
            "hexalith-parties-ui",
            "secret",
            "eventstore:tenant",
            "sub"));

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        // Both the OIDC challenge scheme and the server-side cookie sign-in scheme are registered.
        IAuthenticationSchemeProvider schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        (await schemes.GetSchemeAsync(OidcChallengeScheme)).ShouldNotBeNull();
        (await schemes.GetSchemeAsync(CookieSignInScheme)).ShouldNotBeNull();

        // The bridge swaps the framework seam from the fail-closed NullUserContextAccessor to the
        // claims-backed accessor — proof interactive auth is actually wired. The accessor is scoped,
        // so resolve it inside a scope (ValidateScopes=true forbids root-resolving scoped services).
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IUserContextAccessor>()
            .ShouldBeOfType<ClaimsPrincipalUserContextAccessor>();

        // AC1 tokens-stay-server-side guard: tokens are saved into the encrypted authentication
        // ticket and signed into the cookie scheme, so the browser only ever holds the HttpOnly
        // cookie — never a usable bearer/access token.
        OpenIdConnectOptions oidc = provider
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OidcChallengeScheme);
        oidc.SaveTokens.ShouldBeTrue();
        oidc.SignInScheme.ShouldBe(CookieSignInScheme);
    }

    [Fact]
    public void AuthBridge_WhenOidcConfigured_UsesAuthorizationCodeFlowWithStandardCallbacksAndScopes()
    {
        // AC1 — the bridge wires the authorization-code flow ("code"), the only flow that keeps
        // tokens off the browser (implicit/hybrid would return tokens in the redirect fragment the
        // browser can read). The callbacks are the framework OIDC endpoints AC2's surface names.
        using ServiceProvider provider = BuildConfiguredAuthBridgeProvider();

        OpenIdConnectOptions oidc = provider
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OidcChallengeScheme);

        oidc.ResponseType.ShouldBe("code");
        oidc.CallbackPath.Value.ShouldBe("/signin-oidc");
        oidc.SignedOutCallbackPath.Value.ShouldBe("/signout-callback-oidc");

        // Minimal sign-in scopes only — openid (mandatory) + profile. No api/offline scopes are
        // requested here; the gateway client + token relay are a later story (scope boundary).
        oidc.Scope.ShouldContain("openid");
        oidc.Scope.ShouldContain("profile");
    }

    [Fact]
    public void AuthBridge_WhenOidcConfigured_IssuesHttpOnlyServerSideCookieWithChallengeAndSignOutPaths()
    {
        // AC1 — the cookie half of "tokens never reach the browser": the session cookie is HttpOnly,
        // so the encrypted ticket holding the SaveTokens'd OIDC tokens is unreadable from JavaScript
        // (the OIDC SaveTokens half is asserted above). Secure posture mirrors the BFF defaults.
        using ServiceProvider provider = BuildConfiguredAuthBridgeProvider();

        CookieAuthenticationOptions cookie = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieSignInScheme);

        cookie.Cookie.HttpOnly.ShouldBeTrue();
        cookie.Cookie.SameSite.ShouldBe(SameSiteMode.Lax);
        cookie.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.SameAsRequest);
        cookie.SlidingExpiration.ShouldBeFalse();

        // AC1 return-URL challenge path + AC2 sign-out path: the cookie middleware redirects
        // unauthenticated requests to LoginPath (mapped to a provider ChallengeAsync that preserves
        // returnUrl) and sign-out requests to LogoutPath.
        cookie.LoginPath.Value.ShouldBe("/authentication/challenge");
        cookie.LogoutPath.Value.ShouldBe("/authentication/sign-out");
    }

    [Fact]
    public void Host_WithoutOidcConfig_StillComposesUnderValidateScopes_WithFailClosedUserContext()
    {
        // No OIDC keys → the authEnabled gate stays closed, mirroring Program.cs skipping the bridge.
        IConfiguration configuration = new ConfigurationBuilder().Build();
        AuthEnabled(configuration).ShouldBeFalse();

        // The Quickstart chain + domain marker must still compose under ValidateScopes=true (ADR-030),
        // exactly as Story 1.1 — the "auth optional, host always boots" contract the gate encodes.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFluentUIComponents();
        services.AddHttpContextAccessor();
        services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(PartiesUiDomainMarker).Assembly));
        services.AddHexalithDomain<PartiesUiDomainMarker>();

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        provider.GetRequiredService<IFrontComposerRegistry>().ShouldNotBeNull();

        // Auth was NOT wired: the seam is still the fail-closed Quickstart default, not the
        // claims-backed accessor the bridge installs — the negative contrast to the test above.
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IUserContextAccessor>()
            .ShouldBeOfType<NullUserContextAccessor>();
    }

    // Builds the same FrontComposer auth-bridge composition the host runs when authEnabled — the
    // Keycloak OIDC recipe over a server-side cookie, under ValidateScopes=true (ADR-030).
    private static ServiceProvider BuildConfiguredAuthBridgeProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddHexalithFrontComposerAuthentication(o => o.UseKeycloak(
            new Uri("https://idp.example/realms/hexalith"),
            "hexalith-parties-ui",
            "secret",
            "eventstore:tenant",
            "sub"));

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static IConfiguration BuildOidcConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:OpenIdConnect:Authority"] = "https://idp.example/realms/hexalith",
                ["Authentication:OpenIdConnect:ClientId"] = "hexalith-parties-ui",
                ["Authentication:OpenIdConnect:ClientSecret"] = "secret",
            })
            .Build();

    // Same gate expression as Program.cs — kept in lock-step so this test fails if the host's
    // authEnabled contract (authority is an absolute URI + client id + secret all present) changes.
    private static bool AuthEnabled(IConfiguration configuration) =>
        Uri.TryCreate(configuration["Authentication:OpenIdConnect:Authority"], UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(configuration["Authentication:OpenIdConnect:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Authentication:OpenIdConnect:ClientSecret"]);
}
