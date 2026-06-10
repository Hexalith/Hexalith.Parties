using System.Reflection;

using Bunit;

using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.Components.Account;
using Hexalith.Parties.UI.Components.Areas;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.3 AC3 — "one <c>[Authorize(Policy = …)]</c> per area" + the load-bearing route gating the
/// Dev Notes flag as "the single most likely omission" (a Consumer reaching <c>/admin</c> by URL). Plain
/// <c>&lt;RouteView&gt;</c> ignores <c>[Authorize]</c> attributes; <c>&lt;AuthorizeRouteView&gt;</c>
/// (Routes.razor) enforces them — but only if the area pages actually carry them. This reflects the
/// compiled routable components and pins, statically (no render), that:
/// <list type="bullet">
/// <item><c>/admin</c> (AdminLanding) requires the <c>Admin</c> policy,</item>
/// <item><c>/me</c> (ConsumerLanding) requires the <c>Consumer</c> policy,</item>
/// <item><c>/</c> (RoleLandingRedirect) requires authentication only (no policy) so anonymous hits fall to
/// the router's NotAuthorized → OIDC challenge path.</item>
/// </list>
/// </summary>
public sealed class PartiesUiAreaAuthorizationTests : BunitContext
{
    [Fact]
    public void AdminLanding_Routes_Admin_AndRequires_AdminPolicy()
    {
        RouteTemplates(typeof(AdminLanding)).ShouldContain("/admin");
        RequiredPolicy(typeof(AdminLanding)).ShouldBe(PartiesUiAuthorization.AdminPolicy);
    }

    [Fact]
    public void AdminLanding_NavigatesToAdminPortalEntryPoint()
    {
        IRenderedComponent<AdminLanding> cut = Render<AdminLanding>();

        Services.GetRequiredService<NavigationManager>().Uri.ShouldEndWith(PartiesAdminPortalManifest.Route);
        cut.Markup.ShouldNotContain("coming soon");
    }

    [Fact]
    public void Routes_ForbiddenCopyIsAdminRoleNeededAndPiiFree()
    {
        string source = File.ReadAllText(ProjectRoot("src/Hexalith.Parties.UI/Components/Routes.razor"));

        source.ShouldContain("IsAdminRoute");
        source.ShouldContain("Admin role");
        source.ShouldContain("You are not authorized to view this area.");
        source.ShouldNotContain("tenant", Case.Insensitive);
        source.ShouldNotContain("user id", Case.Insensitive);
        source.ShouldNotContain("party id", Case.Insensitive);
    }

    [Fact]
    public void ConsumerLanding_Routes_Me_AndRequires_ConsumerPolicy()
    {
        RouteTemplates(typeof(ConsumerLanding)).ShouldContain("/me");
        RequiredPolicy(typeof(ConsumerLanding)).ShouldBe(PartiesUiAuthorization.ConsumerPolicy);
    }

    [Fact]
    public void NoPartyBinding_Routes_NoPartyBinding_AndRequires_ConsumerPolicy()
    {
        // Story 1.4 — the fail-closed Consumer binding state is itself gated on the Consumer policy
        // (keeps non-Consumers out) and enforced by the same AuthorizeRouteView.
        RouteTemplates(typeof(NoPartyBinding)).ShouldContain("/no-party-binding");
        RequiredPolicy(typeof(NoPartyBinding)).ShouldBe(PartiesUiAuthorization.ConsumerPolicy);
    }

    [Fact]
    public void RoleLandingRedirect_RoutesAppEntry_AndRequiresAuthenticationButNoPolicy()
    {
        RouteTemplates(typeof(RoleLandingRedirect)).ShouldContain("/");

        // [Authorize] with no policy: present (gates anonymous → challenge) but carries no Policy.
        AuthorizeAttribute authorize = typeof(RoleLandingRedirect)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            .ShouldHaveSingleItem();
        authorize.Policy.ShouldBeNullOrEmpty();
    }

    private static IReadOnlyCollection<string> RouteTemplates(Type component)
        => component
            .GetCustomAttributes<RouteAttribute>(inherit: false)
            .Select(static route => route.Template)
            .ToList();

    private static string RequiredPolicy(Type component)
        => component
            .GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            .ShouldHaveSingleItem()
            .Policy
            .ShouldNotBeNull();

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
