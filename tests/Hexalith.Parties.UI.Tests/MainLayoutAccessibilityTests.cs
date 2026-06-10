using System.Security.Claims;

using AngleSharp.Dom;

using Bunit;

using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.Components.Layout;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class MainLayoutAccessibilityTests : BunitContext
{
    public MainLayoutAccessibilityTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddPartiesUiAuthorization();
        Services.AddSingleton<AuthenticationStateProvider>(
            new FakeAuthStateProvider(new ClaimsPrincipal(new ClaimsIdentity())));
        Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
        Services.AddFluentUIComponents();
        Services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(PartiesUiDomainMarker).Assembly));
        Services.AddHexalithDomain<PartiesUiDomainMarker>();
        BunitJSModuleInterop navModule = JSInterop.SetupModule("./_content/Microsoft.FluentUI.AspNetCore.Components/Components/Nav/FluentNav.razor.js");
        navModule.SetupVoid("Microsoft.FluentUI.Blazor.Nav.Initialize", _ => true);
        JSInterop.SetupVoid(
            "Microsoft.FluentUI.Blazor.Utilities.Attributes.observeAttributeChange",
            _ => true);
    }

    [Fact]
    public void MainLayout_renders_app_skip_links_as_first_two_focusable_anchors()
    {
        IRenderedComponent<CascadingAuthenticationState> cut = RenderMainLayout();

        IElement[] anchors = cut.FindAll("a[href]").Take(2).ToArray();

        anchors.Length.ShouldBe(2);
        anchors[0].TextContent.Trim().ShouldBe("Skip to content");
        anchors[0].GetAttribute("href").ShouldBe("#parties-main-content");
        anchors[1].TextContent.Trim().ShouldBe("Skip to navigation");
        anchors[1].GetAttribute("href").ShouldBe("#parties-app-navigation");
    }

    [Fact]
    public void MainLayout_skip_links_resolve_to_programmatic_focus_targets()
    {
        IRenderedComponent<CascadingAuthenticationState> cut = RenderMainLayout();

        foreach (IElement anchor in cut.FindAll("a[href]").Take(2))
        {
            string targetId = anchor.GetAttribute("href")![1..];
            IElement target = cut.Find($"#{targetId}");

            target.GetAttribute("tabindex").ShouldBe("-1");
        }
    }

    [Fact]
    public void MainLayout_exposes_named_navigation_and_content_landmarks()
    {
        IRenderedComponent<CascadingAuthenticationState> cut = RenderMainLayout();

        IElement navigation = cut.Find("#parties-app-navigation");
        navigation.GetAttribute("role").ShouldBe("navigation");
        navigation.GetAttribute("aria-label").ShouldBe("Application navigation");
        navigation.QuerySelector("[data-testid='fc-navigation-full'], [data-testid='fc-collapsed-rail']").ShouldNotBeNull();

        IElement content = cut.Find("#parties-main-content");
        content.GetAttribute("role").ShouldBe("main");
        content.GetAttribute("aria-label").ShouldBe("Main content");
        content.TextContent.ShouldContain("Sample content");
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderMainLayout()
        => Render<CascadingAuthenticationState>(parameters => parameters
            .Add(component => component.ChildContent, (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<MainLayout>(2);
                childBuilder.AddAttribute(3, nameof(MainLayout.Body), (RenderFragment)(bodyBuilder =>
                    bodyBuilder.AddMarkupContent(4, "<h1>Sample content</h1>")));
                childBuilder.CloseComponent();
            })));

    private sealed class AllowAllAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }
}
