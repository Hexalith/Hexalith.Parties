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
    public void MainLayout_uses_frontcomposer_skip_links_as_first_two_focusable_anchors()
    {
        IRenderedComponent<CascadingAuthenticationState> cut = RenderMainLayout();

        IElement[] anchors = cut.FindAll("a[href]").Take(2).ToArray();

        anchors.Length.ShouldBe(2);
        anchors[0].TextContent.Trim().ShouldBe("Skip to content");
        anchors[0].GetAttribute("href").ShouldEndWith("#fc-main-content");
        anchors[1].TextContent.Trim().ShouldBe("Skip to navigation");
        anchors[1].GetAttribute("href").ShouldEndWith("#fc-nav");
    }

    [Fact]
    public void MainLayout_skip_links_resolve_to_programmatic_focus_targets()
    {
        IRenderedComponent<CascadingAuthenticationState> cut = RenderMainLayout();

        foreach (IElement anchor in cut.FindAll("a[href]").Take(2))
        {
            // Split on the fragment rather than trimming one leading character. FrontComposerShell
            // composes hrefs as {AbsolutePath}{Query}#fragment, so [1..] only happens to work at the
            // root URL and would build a selector like "##fc-main-content" on any other route.
            string targetId = anchor.GetAttribute("href")!.Split('#')[^1];
            IElement target = cut.Find($"#{targetId}");

            target.GetAttribute("tabindex").ShouldBe("-1");
        }
    }

    [Fact]
    public void MainLayout_exposes_one_named_navigation_and_one_content_landmark()
    {
        IRenderedComponent<CascadingAuthenticationState> cut = RenderMainLayout();

        IElement[] navigationLandmarks = cut.FindAll("[role='navigation']").ToArray();
        navigationLandmarks.Length.ShouldBe(1);
        navigationLandmarks[0].GetAttribute("aria-label").ShouldBe("Primary navigation");
        navigationLandmarks[0].GetAttribute("data-testid").ShouldBe("fc-navigation-rail");

        IElement[] contentLandmarks = cut.FindAll("[role='main']").ToArray();
        contentLandmarks.Length.ShouldBe(1);
        IElement content = contentLandmarks[0];
        content.Id.ShouldBe("fc-main-content");
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
