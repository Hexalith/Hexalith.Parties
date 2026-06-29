using System.Security.Claims;

using Bunit;
using Bunit.TestDoubles;

using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.Components.Account;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.3 AC1/AC2 — role→landing routing for <see cref="RoleLandingRedirect"/> (the <c>@page "/"</c>
/// app entry). bUnit injects a fake authorization context (<see cref="BunitAuthorizationContext"/>) and
/// NavigationManager; each case sets the signed-in principal's roles and asserts the resulting landing
/// route. The no-role case proves the fail-closed contract: an authenticated user with NEITHER role is
/// never routed into a data area — they get the no-area state. Independent of Keycloak (test principals
/// carry role claims directly), so these are the binding proof of the role-routing ACs.
/// </summary>
public sealed class RoleLandingRedirectTests : BunitContext
{
    // Story 1.4 — RoleLandingRedirect now injects PartyIdClaimResolver to fail-closed-check the Consumer
    // party binding. Register it for every test so the component resolves; the Admin / no-role paths never
    // invoke it, and the Consumer-landing cases supply a party_id claim so a *bound* Consumer still lands
    // on /me. The unbound/ambiguous negatives are proven in NoPartyBindingRoutingTests.
    public RoleLandingRedirectTests() => Services.AddPartiesUiClaimsResolution();

    [Fact]
    public void AdminRole_LandsOnAdminArea()
    {
        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");

        IRenderedComponent<RoleLandingRedirect> cut = Render<RoleLandingRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.ShouldEndWith("/admin"));
    }

    [Fact]
    public void TenantOwnerRole_LandsOnAdminArea()
    {
        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("owner");
        auth.SetRoles("TenantOwner");

        IRenderedComponent<RoleLandingRedirect> cut = Render<RoleLandingRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.ShouldEndWith("/admin"));
    }

    [Fact]
    public void ConsumerRole_LandsOnConsumerArea()
    {
        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("consumer");
        auth.SetRoles("Consumer");
        auth.SetClaims(
            new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a"),
            new Claim(PartiesClaimTypes.PartyId, "party-123"));

        IRenderedComponent<RoleLandingRedirect> cut = Render<RoleLandingRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.ShouldEndWith("/me"));
    }

    [Fact]
    public void AuthenticatedUserWithNoRole_IsNotRoutedToADataArea_AndSeesTheNoAreaState()
    {
        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("nobody");

        IRenderedComponent<RoleLandingRedirect> cut = Render<RoleLandingRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();

        // Fail closed: the no-area state renders and no redirect into /admin or /me occurs.
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No area assigned"));
        nav.Uri.ShouldBe(nav.BaseUri);
        nav.Uri.ShouldNotEndWith("/admin");
        nav.Uri.ShouldNotEndWith("/me");
    }

    // The redirect routes on the SAME single source of truth the policies accept (IsInRole is ordinal /
    // case-sensitive). Driving every declared variant through landing proves the routing honours each
    // casing the arrays enumerate — not just the canonical "Admin"/"TenantOwner"/"Consumer".

    [Theory]
    [MemberData(nameof(AdminRoleNameCases))]
    public void EveryDeclaredAdminRoleName_LandsOnAdminArea(string roleName)
    {
        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles(roleName);

        IRenderedComponent<RoleLandingRedirect> cut = Render<RoleLandingRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.ShouldEndWith("/admin"));
    }

    [Theory]
    [MemberData(nameof(ConsumerRoleNameCases))]
    public void EveryDeclaredConsumerRoleName_LandsOnConsumerArea(string roleName)
    {
        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("consumer");
        auth.SetRoles(roleName);
        auth.SetClaims(
            new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a"),
            new Claim(PartiesClaimTypes.PartyId, "party-123"));

        IRenderedComponent<RoleLandingRedirect> cut = Render<RoleLandingRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.ShouldEndWith("/me"));
    }

    [Fact]
    public void UserWithBothAdminAndConsumerRoles_LandsOnAdminArea_AdminTakesPrecedence()
    {
        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("both");

        // Order asserts precedence, not luck: Consumer first, yet the Admin branch (checked first) wins.
        auth.SetRoles("Consumer", "Admin");

        IRenderedComponent<RoleLandingRedirect> cut = Render<RoleLandingRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.ShouldEndWith("/admin"));
        nav.Uri.ShouldNotEndWith("/me");
    }

    public static TheoryData<string> AdminRoleNameCases()
    {
        var data = new TheoryData<string>();
        foreach (string role in PartiesUiAuthorization.AdminRoleNames)
        {
            data.Add(role);
        }

        return data;
    }

    public static TheoryData<string> ConsumerRoleNameCases()
    {
        var data = new TheoryData<string>();
        foreach (string role in PartiesUiAuthorization.ConsumerRoleNames)
        {
            data.Add(role);
        }

        return data;
    }
}
