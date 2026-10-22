using System.Reflection;
using System.Security.Claims;
using System.Xml.Linq;

using Hexalith.FrontComposer.Contracts.Attributes;
using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.FrontComposer.Contracts.Rendering;
using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.FrontComposer.Shell.Options;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.AdminPortal.Extensions;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.UI.IdentityBinding;
using Hexalith.Parties.UI.Services;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Boot/composition smoke tests for the Hexalith.Parties.UI host (Story 1.1). Exercises the
/// unit-testable slice of host stand-up: the FrontComposer Quickstart chain plus the domain
/// marker compose into a service provider under ValidateScopes=true. Full routing/role tests
/// arrive in Story 1.3.
/// </summary>
public sealed class PartiesUiHostCompositionTests
{
    [Fact]
    public void QuickstartChainWithDomainMarker_ComposesUnderValidateScopes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(PartiesUiDomainMarker).Assembly));
        services.AddHexalithDomain<PartiesUiDomainMarker>();

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        IFrontComposerRegistry registry = provider.GetRequiredService<IFrontComposerRegistry>();

        registry.ShouldNotBeNull();
    }

    [Fact]
    public void HostServiceChain_WithFluentUi_ComposesAndResolvesRegistryWithinScope()
    {
        // Mirrors the AC1-pinned service-registration block of Program.cs: the FrontComposer
        // Quickstart chain + AddFluentUIComponents() + AddHexalithDomain<PartiesUiDomainMarker>()
        // must compose under ValidateScopes=true (ADR-030) and resolve inside a request scope —
        // the earlier smoke test left AddFluentUIComponents() out and never opened a scope, so a
        // Singleton capturing a Scoped service (the exact failure ValidateScopes guards) went
        // unexercised.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFluentUIComponents();
        services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(PartiesUiDomainMarker).Assembly));
        services.AddHexalithDomain<PartiesUiDomainMarker>();

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        // Resolving from the root provider and from a created scope both succeed; a captive
        // (Singleton-captures-Scoped) dependency would throw at this point under ValidateScopes.
        provider.GetRequiredService<IFrontComposerRegistry>().ShouldNotBeNull();

        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IFrontComposerRegistry>().ShouldNotBeNull();
    }

    [Fact]
    public void HostComposition_RegistersFluentUiComponentServices()
    {
        // AC1 mandates the host wire AddFluentUIComponents(). Assert the call actually contributes
        // FluentUI services to the container — the original composition smoke test omitted it
        // entirely, so this requirement had no regression guard.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFluentUIComponents();
        services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(PartiesUiDomainMarker).Assembly));
        services.AddHexalithDomain<PartiesUiDomainMarker>();

        services.ShouldContain(static descriptor =>
            descriptor.ServiceType.Namespace != null
            && descriptor.ServiceType.Namespace.StartsWith("Microsoft.FluentUI", StringComparison.Ordinal));
    }

    [Fact]
    public void PartiesUiDomainMarker_DeclaresPartiesBoundedContext()
    {
        BoundedContextAttribute? boundedContext =
            typeof(PartiesUiDomainMarker).GetCustomAttribute<BoundedContextAttribute>();

        boundedContext.ShouldNotBeNull();
        boundedContext.Name.ShouldBe("Parties");
    }

    [Fact]
    public void UiHostProject_ReferencesAdminPortalRcl()
    {
        XDocument project = XDocument.Load(ProjectRoot("src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj"));

        project.Descendants("ProjectReference")
            .Select(static reference => reference.Attribute("Include")?.Value)
            .ShouldContain(@"..\Hexalith.Parties.AdminPortal\Hexalith.Parties.AdminPortal.csproj");
    }

    [Fact]
    public void UiHostProject_ReferencesConsumerPortalRcl()
    {
        XDocument project = XDocument.Load(ProjectRoot("src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj"));

        project.Descendants("ProjectReference")
            .Select(static reference => reference.Attribute("Include")?.Value)
            .ShouldContain(@"..\Hexalith.Parties.ConsumerPortal\Hexalith.Parties.ConsumerPortal.csproj");
    }

    [Fact]
    public void Program_RegistersAdminPortalServices()
    {
        string source = File.ReadAllText(ProjectRoot("src/Hexalith.Parties.UI/Program.cs"));

        source.ShouldContain("using Hexalith.Parties.AdminPortal.Extensions;");
        source.ShouldContain("builder.Services.AddHexalithPartiesAdminPortal();");
    }

    [Fact]
    public void Program_AddsAdminPortalAssemblyToStaticRazorComponentDiscovery()
    {
        string source = File.ReadAllText(ProjectRoot("src/Hexalith.Parties.UI/Program.cs"));

        source.ShouldContain(".AddAdditionalAssemblies(");
        source.ShouldContain("typeof(Hexalith.Parties.AdminPortal.Components.PartiesAdminPortal).Assembly");
    }

    [Fact]
    public void Program_AddsConsumerPortalAssemblyToStaticRazorComponentDiscovery()
    {
        string source = File.ReadAllText(ProjectRoot("src/Hexalith.Parties.UI/Program.cs"));

        source.ShouldContain("typeof(Hexalith.Parties.ConsumerPortal.Components.MyProfilePage).Assembly");
    }

    [Fact]
    public void Program_RegistersConsumerPortalAdaptersAfterSelfScopedClient()
    {
        string source = File.ReadAllText(ProjectRoot("src/Hexalith.Parties.UI/Program.cs"));

        source.ShouldContain("using Hexalith.Parties.ConsumerPortal.Services;");
        source.ShouldContain("builder.Services.AddScoped<IConsumerProfileDataClient, ConsumerProfileDataClient>();");
        source.ShouldContain("builder.Services.AddScoped<IConsumerProfileEditClient, ConsumerProfileEditClient>();");
        source.ShouldContain("builder.Services.AddScoped<IConsumerConsentClient, ConsumerConsentClient>();");
        source.ShouldContain("builder.Services.AddScoped<IConsumerPrivacyExportClient, ConsumerPrivacyExportClient>();");
        source.ShouldContain("builder.Services.AddScoped<IConsumerPrivacyErasureClient, ConsumerPrivacyErasureClient>();");
        source.ShouldContain("builder.Services.AddScoped<IConsumerPrivacyProcessingClient, ConsumerPrivacyProcessingClient>();");
        source.IndexOf("builder.Services.AddSelfScopedPartiesClient();", StringComparison.Ordinal)
            .ShouldBeLessThan(source.IndexOf(
                "builder.Services.AddScoped<IConsumerProfileDataClient, ConsumerProfileDataClient>();",
                StringComparison.Ordinal));
        source.IndexOf("builder.Services.AddSelfScopedPartiesClient();", StringComparison.Ordinal)
            .ShouldBeLessThan(source.IndexOf(
                "builder.Services.AddScoped<IConsumerProfileEditClient, ConsumerProfileEditClient>();",
                StringComparison.Ordinal));
        source.IndexOf("builder.Services.AddSelfScopedPartiesClient();", StringComparison.Ordinal)
            .ShouldBeLessThan(source.IndexOf(
                "builder.Services.AddScoped<IConsumerConsentClient, ConsumerConsentClient>();",
                StringComparison.Ordinal));
        source.IndexOf("builder.Services.AddSelfScopedPartiesClient();", StringComparison.Ordinal)
            .ShouldBeLessThan(source.IndexOf(
                "builder.Services.AddScoped<IConsumerPrivacyExportClient, ConsumerPrivacyExportClient>();",
                StringComparison.Ordinal));
        source.IndexOf("builder.Services.AddSelfScopedPartiesClient();", StringComparison.Ordinal)
            .ShouldBeLessThan(source.IndexOf(
                "builder.Services.AddScoped<IConsumerPrivacyErasureClient, ConsumerPrivacyErasureClient>();",
                StringComparison.Ordinal));
        source.IndexOf("builder.Services.AddSelfScopedPartiesClient();", StringComparison.Ordinal)
            .ShouldBeLessThan(source.IndexOf(
                "builder.Services.AddScoped<IConsumerPrivacyProcessingClient, ConsumerPrivacyProcessingClient>();",
                StringComparison.Ordinal));
    }

    [Fact]
    public void App_LoadsConsumerPrivacyExportDownloadHelperAtHostBoundary()
    {
        string source = File.ReadAllText(ProjectRoot("src/Hexalith.Parties.UI/Components/App.razor"));

        source.ShouldContain("<script src=\"consumer-privacy-export.js\"></script>");
    }

    [Fact]
    public void ConsumerPrivacyExportDownloadHelper_CleansUpObjectUrlWithoutLoggingPayload()
    {
        string source = File.ReadAllText(ProjectRoot("src/Hexalith.Parties.UI/wwwroot/consumer-privacy-export.js"));

        source.ShouldContain("HexalithPartiesConsumerPortal.downloadJson");
        source.ShouldContain("streamReference.arrayBuffer()");
        source.ShouldContain("new Blob([arrayBuffer]");
        source.ShouldContain("try");
        source.ShouldContain("finally");
        source.ShouldContain("anchor.remove()");
        source.ShouldContain("URL.revokeObjectURL(url)");
        source.ShouldNotContain("console.", Case.Insensitive);
        source.ShouldNotContain("localStorage", Case.Insensitive);
        source.ShouldNotContain("sessionStorage", Case.Insensitive);
    }

    [Fact]
    public void Routes_AddsPortalAssembliesAndKeepsAuthorizeRouteView()
    {
        string source = File.ReadAllText(ProjectRoot("src/Hexalith.Parties.UI/Components/Routes.razor"));

        source.ShouldContain("AdditionalAssemblies");
        source.ShouldContain("typeof(Hexalith.Parties.AdminPortal.Components.PartiesAdminPortal).Assembly");
        source.ShouldContain("typeof(Hexalith.Parties.ConsumerPortal.Components.MyProfilePage).Assembly");
        source.ShouldContain("<AuthorizeRouteView");
        source.ShouldNotContain("<RouteView");
    }

    [Fact]
    public void HostLocalConsumerLandingStub_NoLongerDeclaresMeRoute()
    {
        string source = File.ReadAllText(ProjectRoot("src/Hexalith.Parties.UI/Components/Areas/ConsumerLanding.razor"));

        source.ShouldNotContain("@page \"/me\"");
    }

    [Fact]
    public void AdminPortalServiceChain_ComposesUnderValidateScopes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFluentUIComponents();
        services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(PartiesUiDomainMarker).Assembly));
        services.AddHexalithDomain<PartiesUiDomainMarker>();
        services.AddSingleton(Substitute.For<IPartiesQueryClient>());
        services.AddSingleton(Substitute.For<IAdminPortalGdprClient>());
        services.AddSingleton(Substitute.For<IAdminPortalAuthorizationService>());
        services.AddHexalithPartiesAdminPortal();
        services.AddIdentityBindingProvisioning();

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AdminPortalPartyQueryService>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<IPartiesAdminPortalApiClient>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<IIdentityBindingProvisioningService>().ShouldNotBeNull();
    }

    [Fact]
    public async Task TestEnvironment_ServesBlazorFrameworkScript_WhenStaticWebAssetsAreOptedInAsync()
    {
        await using WebApplicationFactory<global::Program> factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(static builder =>
            {
                builder.UseEnvironment("Test");
                builder.UseSetting("Parties:BaseUrl", "http://127.0.0.1:59999");
                builder.UseSetting("Parties:Tenant", "test-tenant");
            });

        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(
            "/_framework/blazor.web.js",
            TestContext.Current.CancellationToken);
        byte[] body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.ShouldBeTrue(
            $"Test-environment static web assets must serve blazor.web.js; got {(int)response.StatusCode} {response.ReasonPhrase}.");
        body.Length.ShouldBeGreaterThan(0, "A 0-byte blazor.web.js means the Test host fell back to SSR-only output.");
    }

    /// <summary>Resolves fixture shell scope through the actual host registration for each browser principal.</summary>
    /// <param name="accessibilityEnabled">Whether to include Playwright's accessibility scope wrapper.</param>
    /// <param name="route">The browser route represented by the circuit navigation service.</param>
    /// <param name="cookie">The browser fixture cookie, or empty for an anonymous request.</param>
    /// <param name="expectedUser">The expected shell user without changing authorization.</param>
    [Theory]
    [InlineData(false, "/admin/parties", "parties-admin-e2e=enabled", "admin-e2e")]
    [InlineData(false, "/me", "parties-consumer-e2e=bound", "consumer-e2e")]
    [InlineData(false, "/me", "", "anonymous-e2e")]
    [InlineData(true, "/admin/parties", "parties-admin-e2e=enabled", "admin-e2e")]
    [InlineData(true, "/me/privacy", "parties-consumer-e2e=bound", "consumer-e2e")]
    [InlineData(true, "/__parties/specimens/party-picker", "", "anonymous-e2e")]
    [InlineData(true, "/__parties/specimens/accessibility", "parties-admin-e2e=enabled", "parties-accessibility-user")]
    public async Task EnabledBrowserFixture_ResolvesCurrentShellScopeFromHostAsync(
        bool accessibilityEnabled, string route, string cookie, string expectedUser)
    {
        using var navigationContext = new Bunit.BunitContext();
        NavigationManager navigation = navigationContext.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(route);
        await using WebApplicationFactory<global::Program> factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.UseSetting("Parties:BaseUrl", "http://127.0.0.1:59999");
                builder.UseSetting("Parties:Tenant", "test-tenant");
                builder.UseSetting(PartiesAdminPortalE2eFixture.EnabledConfigurationKey, "true");
                builder.UseSetting("Hexalith:Parties:AccessibilitySpecimens:Enabled", accessibilityEnabled.ToString());
                // A circuit normally initializes navigation; the host's user-context
                // registration and wrapper order remain the real Program.cs composition.
                builder.ConfigureTestServices(services => services.Replace(
                    ServiceDescriptor.Scoped<NavigationManager>(_ => navigation)));
            });

        using IServiceScope scope = factory.Services.CreateScope();
        IHttpContextAccessor httpContext = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        HttpContext? previous = httpContext.HttpContext;
        try
        {
            httpContext.HttpContext = new DefaultHttpContext();
            httpContext.HttpContext.Request.Headers.Cookie = cookie;
            IUserContextAccessor shellContext = scope.ServiceProvider.GetRequiredService<IUserContextAccessor>();

            if (accessibilityEnabled)
            {
                shellContext.ShouldBeOfType<PartiesAccessibilitySpecimenUserContextAccessor>();
            }
            else
            {
                shellContext.ShouldBeOfType<PartiesAdminPortalE2eUserContextAccessor>();
            }

            shellContext.TenantId.ShouldBe(route == "/__parties/specimens/accessibility" ? "parties-accessibility-tenant" : "test-tenant");
            shellContext.UserId.ShouldBe(expectedUser);
        }
        finally
        {
            httpContext.HttpContext = previous;
        }
    }

    /// <summary>The actual package host reserves its area routes and rejects colliding module registration.</summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("me")]
    [InlineData("no-party-binding")]
    public async Task ActualHost_ReservesAreaSegmentsAndDiscoversShellModuleRoutes(string reserved)
    {
        await using WebApplicationFactory<global::Program> factory = RoutingHost();
        using HttpClient client = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        FrontComposerRouteOptions options = scope.ServiceProvider.GetRequiredService<IOptions<FrontComposerRouteOptions>>().Value;
        options.ReservedSegments.Order(StringComparer.Ordinal).ShouldBe(new[] { "admin", "me", "no-party-binding" });
        IFrontComposerRegistry registry = scope.ServiceProvider.GetRequiredService<IFrontComposerRegistry>();
        Should.Throw<InvalidOperationException>(() => registry.RegisterDomain(new("Collision", reserved.ToUpperInvariant(), [], [])));
        registry.RegisterDomain(new("Routing proof", "routing-proof", [], []));
        foreach (string route in new[] { "/routing-proof", "/routing-proof/overview" })
        {
            using HttpResponseMessage response = await client.GetAsync(route, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("Routing proof");
        }

        string[] routes = [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText).OfType<string>()];
        routes.ShouldContain("/{Module}");
        routes.ShouldContain("/{Module}/overview");
        routes.ShouldContain("/{Module}/{Tab}");
        routes.ShouldContain("/parties");
        routes.ShouldContain("/parties/overview");
    }

    /// <summary>Host-registered role policies continue to restrict destinations when package routing is enabled.</summary>
    [Theory]
    [InlineData("Admin", true, false)]
    [InlineData("TenantOwner", true, false)]
    [InlineData("Consumer", false, true)]
    [InlineData("unrelated", false, false)]
    public async Task ActualHost_RoutePoliciesRetainRoleRestrictions(string role, bool adminAllowed, bool consumerAllowed)
    {
        await using WebApplicationFactory<global::Program> factory = RoutingHost();
        using IServiceScope scope = factory.Services.CreateScope();
        IAuthorizationService authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "routing-proof"));
        (await authorization.AuthorizeAsync(principal, null, PartiesUiAuthorization.AdminPolicy)).Succeeded.ShouldBe(adminAllowed);
        (await authorization.AuthorizeAsync(principal, null, PartiesUiAuthorization.ConsumerPolicy)).Succeeded.ShouldBe(consumerAllowed);
    }

    /// <summary>Real endpoint requests cannot use generic module routes to bypass the area role boundary.</summary>
    [Theory]
    [InlineData("/admin/parties", "parties-consumer-e2e=bound", "hx-parties-admin")]
    [InlineData("/me", "parties-admin-e2e=enabled", "hx-parties-profile")]
    [InlineData("/me/privacy", "parties-admin-e2e=enabled", "hx-parties-privacy")]
    [InlineData("/no-party-binding", "parties-admin-e2e=enabled", "linked to a profile yet")]
    public async Task ActualHost_RejectsWrongRoleAreaRequests(string route, string cookie, string requestedAreaMarker)
    {
        await using WebApplicationFactory<global::Program> factory = RoutingHost();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Add("Cookie", cookie);
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // AuthorizeRouteView renders the forbidden page inside the successful SSR shell.
        body.ShouldContain("Forbidden");
        body.ShouldNotContain("parties-overview-tabs");
        body.ShouldNotContain(requestedAreaMarker);
        AdminPortalE2eSnapshot reads = factory.Services.GetRequiredService<PartiesAdminPortalE2eFixtureState>().Snapshot();
        reads.ListRequests.ShouldBeEmpty();
        reads.SearchRequests.ShouldBeEmpty();
        reads.DetailRequests.ShouldBeEmpty();
        reads.PickerSearchRequests.ShouldBeEmpty();
        reads.PickerDetailRequests.ShouldBeEmpty();
        reads.ProcessingRecordRequests.ShouldBeEmpty();
        reads.ErasureCertificateRequests.ShouldBeEmpty();
        reads.ExportRequests.ShouldBeEmpty();
    }

    /// <summary>The actual router distinguishes explicit Parties aliases, registered module landings and unregistered module routes.</summary>
    [Theory]
    [InlineData("/parties", "overview")]
    [InlineData("/parties/overview", "overview")]
    [InlineData("/parties/retired-tab", "module")]
    [InlineData("/missing-module/retired-tab", "unavailable")]
    public async Task ActualHost_PartiesAliasesAndUnknownRoutesKeepExistingSelection(string route, string expectedPage)
    {
        await using WebApplicationFactory<global::Program> factory = RoutingHost();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        IFrontComposerRegistry registry = factory.Services.GetRequiredService<IFrontComposerRegistry>();
        registry.GetManifests().ShouldContain(manifest => manifest.BoundedContext == "parties"
            && manifest.Commands.Count == 0 && manifest.Projections.Count == 0);
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Add("Cookie", "parties-admin-e2e=enabled");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        response.Headers.Location.ShouldBeNull();
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (expectedPage == "overview")
        {
            body.ShouldContain("parties-overview-tabs");
            body.ShouldContain("parties-overview-admin");
            body.ShouldNotContain("data-fc-route-unavailable");
        }
        else if (expectedPage == "module")
        {
            body.ShouldContain("data-testid=\"fc-page-tabs\"");
            body.ShouldContain("data-fc-module-route=\"/parties\"");
            body.ShouldNotContain("parties-overview-tabs");
            body.ShouldNotContain("data-fc-route-unavailable");
        }
        else
        {
            body.ShouldContain("data-fc-route-unavailable=\"true\"");
            body.ShouldNotContain("parties-overview-tabs");
        }
    }

    private static WebApplicationFactory<global::Program> RoutingHost()
        => new WebApplicationFactory<global::Program>().WithWebHostBuilder(static builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("Parties:BaseUrl", "http://127.0.0.1:59999");
            builder.UseSetting("Parties:Tenant", "test-tenant");
            builder.UseSetting(PartiesAdminPortalE2eFixture.EnabledConfigurationKey, "true");
        });

    private static string ProjectRoot(string relativePath)
    {
        string current = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(current, "Hexalith.Parties.slnx")))
        {
            DirectoryInfo? parent = Directory.GetParent(current);
            parent.ShouldNotBeNull();
            current = parent.FullName;
        }

        return Path.Combine(current, relativePath);
    }
}
