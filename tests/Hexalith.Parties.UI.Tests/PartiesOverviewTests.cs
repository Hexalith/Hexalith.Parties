using System.Reflection;

using Bunit;
using Bunit.TestDoubles;

using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>Proves the default Parties Module tab only exposes authorized destinations.</summary>
public sealed class PartiesOverviewTests : BunitContext
{
    private readonly BunitAuthorizationContext _authorization;

    public PartiesOverviewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddFluentUIComponents();
        Services.AddHexalithFrontComposerQuickstart();
        _authorization = AddAuthorization();
        _authorization.SetAuthorized("operator");
    }

    [Fact]
    public void RoutesExposeTheSameOverviewTabAtAliasAndCanonicalUrl()
    {
        string[] routes = [.. typeof(PartiesOverview).GetCustomAttributes<RouteAttribute>()
            .Select(attribute => attribute.Template)];
        routes.ShouldContain("/parties");
        routes.ShouldContain("/parties/overview");
    }

    [Fact]
    public void AdminPolicyShowsOnlyAdministration()
    {
        _authorization.SetPolicies(PartiesUiAuthorization.AdminPolicy);
        IRenderedComponent<PartiesOverview> cut = Render<PartiesOverview>();

        cut.FindAll("[data-testid='parties-overview-admin']").Count.ShouldBe(1);
        cut.Find("[data-testid='parties-overview-admin']").GetAttribute("href").ShouldBe("/admin/parties");
        cut.FindAll("[data-testid='parties-overview-consumer']").ShouldBeEmpty();
        cut.FindAll("[data-testid='parties-overview-no-access']").ShouldBeEmpty();
    }

    [Fact]
    public void ConsumerPolicyShowsOnlyMySpace()
    {
        _authorization.SetPolicies(PartiesUiAuthorization.ConsumerPolicy);
        IRenderedComponent<PartiesOverview> cut = Render<PartiesOverview>();

        cut.FindAll("[data-testid='parties-overview-admin']").ShouldBeEmpty();
        cut.FindAll("[data-testid='parties-overview-consumer']").Count.ShouldBe(1);
        cut.Find("[data-testid='parties-overview-consumer']").GetAttribute("href").ShouldBe("/me");
        cut.FindAll("[data-testid='parties-overview-no-access']").ShouldBeEmpty();
    }

    [Fact]
    public void BothPoliciesShowEachAuthorizedDestinationOnce()
    {
        _authorization.SetPolicies(PartiesUiAuthorization.AdminPolicy, PartiesUiAuthorization.ConsumerPolicy);
        IRenderedComponent<PartiesOverview> cut = Render<PartiesOverview>();

        cut.FindAll("[data-testid='parties-overview-admin']").Count.ShouldBe(1);
        cut.FindAll("[data-testid='parties-overview-consumer']").Count.ShouldBe(1);
        cut.Find("[data-testid='parties-overview-admin']").GetAttribute("href").ShouldBe("/admin/parties");
        cut.Find("[data-testid='parties-overview-consumer']").GetAttribute("href").ShouldBe("/me");
        cut.FindAll("[data-testid='parties-overview-no-access']").ShouldBeEmpty();
    }

    [Fact]
    public void NeitherPolicyShowsOneSafeNoAccessState()
    {
        IRenderedComponent<PartiesOverview> cut = Render<PartiesOverview>();

        cut.FindAll("[data-testid='parties-overview-admin']").ShouldBeEmpty();
        cut.FindAll("[data-testid='parties-overview-consumer']").ShouldBeEmpty();
        cut.FindAll("[data-testid='parties-overview-no-access']").Count.ShouldBe(1);
        cut.Find("[data-testid='parties-overview-no-access']").TextContent.ShouldContain("No Parties destinations");
    }
}
