using System.Reflection;
using System.Xml.Linq;

using Hexalith.FrontComposer.Contracts.Attributes;
using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.Parties.AdminPortal.Extensions;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.UI.IdentityBinding;

using Microsoft.Extensions.DependencyInjection;
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

        source.ShouldContain(".AddAdditionalAssemblies(typeof(Hexalith.Parties.AdminPortal.Components.PartiesAdminPortal).Assembly)");
    }

    [Fact]
    public void Routes_AddsAdminPortalAssemblyAndKeepsAuthorizeRouteView()
    {
        string source = File.ReadAllText(ProjectRoot("src/Hexalith.Parties.UI/Components/Routes.razor"));

        source.ShouldContain("AdditionalAssemblies");
        source.ShouldContain("typeof(Hexalith.Parties.AdminPortal.Components.PartiesAdminPortal).Assembly");
        source.ShouldContain("<AuthorizeRouteView");
        source.ShouldNotContain("<RouteView");
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
