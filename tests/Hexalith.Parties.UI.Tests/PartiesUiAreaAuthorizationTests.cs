using System.Reflection;

using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.Components.Account;
using Hexalith.Parties.UI.Components.Areas;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

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
public sealed class PartiesUiAreaAuthorizationTests
{
    [Fact]
    public void AdminLanding_Routes_Admin_AndRequires_AdminPolicy()
    {
        RouteTemplates(typeof(AdminLanding)).ShouldContain("/admin");
        RequiredPolicy(typeof(AdminLanding)).ShouldBe(PartiesUiAuthorization.AdminPolicy);
    }

    [Fact]
    public void ConsumerLanding_Routes_Me_AndRequires_ConsumerPolicy()
    {
        RouteTemplates(typeof(ConsumerLanding)).ShouldContain("/me");
        RequiredPolicy(typeof(ConsumerLanding)).ShouldBe(PartiesUiAuthorization.ConsumerPolicy);
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
}
