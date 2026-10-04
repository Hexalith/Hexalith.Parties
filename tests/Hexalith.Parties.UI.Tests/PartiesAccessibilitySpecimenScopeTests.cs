using Bunit;

using Hexalith.FrontComposer.Contracts.Rendering;
using Hexalith.Parties.UI.Components.Specimens;
using Hexalith.Parties.UI.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Verifies that the accessibility fixture cannot supply scope to ordinary UI requests.
/// </summary>
public sealed class PartiesAccessibilitySpecimenScopeTests : BunitContext
{
    /// <summary>
    /// Verifies that synthetic identity requires the exact enabled route and a permitted environment.
    /// </summary>
    [Theory]
    [InlineData("true", "Test", PartiesAccessibilitySpecimenRoutes.ShellSpecimen, true)]
    [InlineData("true", "Development", PartiesAccessibilitySpecimenRoutes.ShellSpecimen + "?probe=1#fc-main-content", true)]
    [InlineData("true", "Production", PartiesAccessibilitySpecimenRoutes.ShellSpecimen, false)]
    [InlineData("false", "Test", PartiesAccessibilitySpecimenRoutes.ShellSpecimen, false)]
    [InlineData("true", "Test", "/admin", false)]
    [InlineData("true", "Test", PartiesAccessibilitySpecimenRoutes.ShellSpecimen + "/child", false)]
    [InlineData("true", "Test", PartiesAccessibilitySpecimenRoutes.PartyPickerSpecimen, false)]
    public void SyntheticScopeRequiresEnabledSpecimenAtTheExactRoute(
        string enabled, string environmentName, string route, bool expectedSynthetic)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PartiesAccessibilitySpecimenRoutes.EnabledConfigurationKey] = enabled,
            })
            .Build();
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        IUserContextAccessor authenticated = Substitute.For<IUserContextAccessor>();
        authenticated.TenantId.Returns("authenticated-tenant");
        authenticated.UserId.Returns("authenticated-user");
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(route);
        var context = new PartiesAccessibilitySpecimenUserContextAccessor(authenticated, navigation, configuration, environment);

        context.TenantId.ShouldBe(expectedSynthetic ? "parties-accessibility-tenant" : "authenticated-tenant");
        context.UserId.ShouldBe(expectedSynthetic ? "parties-accessibility-user" : "authenticated-user");
    }

    /// <summary>
    /// Verifies that navigating away restores the selected context, including missing identity.
    /// </summary>
    [Fact]
    public void RegisteredWrapperRestoresTheSelectedContextWhenNavigationLeavesTheSpecimen()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PartiesAccessibilitySpecimenRoutes.EnabledConfigurationKey] = "true",
            })
            .Build();
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Test");
        IUserContextAccessor authenticated = Substitute.For<IUserContextAccessor>();
        authenticated.TenantId.Returns("authenticated-tenant");
        authenticated.UserId.Returns("authenticated-user");
        Services.AddSingleton(configuration);
        Services.AddSingleton(environment);
        Services.AddScoped<IUserContextAccessor>(_ => authenticated);
        PartiesAccessibilitySpecimenUserContextAccessor.Register(Services);
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(PartiesAccessibilitySpecimenRoutes.ShellSpecimen);
        IUserContextAccessor context = Services.GetRequiredService<IUserContextAccessor>();

        context.TenantId.ShouldBe("parties-accessibility-tenant");
        navigation.NavigateTo("/me");
        context.TenantId.ShouldBe("authenticated-tenant");
        context.UserId.ShouldBe("authenticated-user");
        authenticated.TenantId.Returns((string?)null);
        authenticated.UserId.Returns((string?)null);
        context.TenantId.ShouldBeNull();
        context.UserId.ShouldBeNull();
    }
}
