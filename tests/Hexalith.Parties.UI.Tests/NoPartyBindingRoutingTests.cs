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
/// Story 1.4 AC1/AC2/AC5 — the fail-closed party-binding routing proof for
/// <see cref="RoleLandingRedirect"/>. bUnit injects a fake authorization context and NavigationManager;
/// each case sets a signed-in Consumer principal (with or without a <c>party_id</c> claim) and asserts the
/// landing route. The absent-claim case asserts the <strong>negative</strong>
/// (<c>ShouldNotEndWith("/me")</c>) — the fail-closed contract that an unbound Consumer never reaches the
/// data area — exactly like the no-area test in <c>RoleLandingRedirectTests</c>. Independent of Keycloak
/// (test principals carry the claim directly), so these are the binding proof of the resolution ACs.
/// </summary>
public sealed class NoPartyBindingRoutingTests : BunitContext
{
    [Fact]
    public void ConsumerWithPartyId_LandsOnConsumerArea()
    {
        Services.AddPartiesUiClaimsResolution();

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
    public void ConsumerWithoutPartyId_IsRoutedToNoPartyBinding_NeverToMe()
    {
        Services.AddPartiesUiClaimsResolution();

        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("consumer");
        auth.SetRoles("Consumer");
        // No party_id claim → fail closed.

        IRenderedComponent<RoleLandingRedirect> cut = Render<RoleLandingRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.ShouldEndWith("/no-party-binding"));

        // Fail closed: the unbound Consumer is NEVER routed into the /me data area.
        nav.Uri.ShouldNotEndWith("/me");
    }

    [Fact]
    public void ConsumerWithAmbiguousPartyIds_IsRoutedToNoPartyBinding_NeverToMe()
    {
        Services.AddPartiesUiClaimsResolution();

        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("consumer");
        auth.SetRoles("Consumer");
        auth.SetClaims(
            new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a"),
            new Claim(PartiesClaimTypes.PartyId, "party-1"),
            new Claim(PartiesClaimTypes.PartyId, "party-2"));

        IRenderedComponent<RoleLandingRedirect> cut = Render<RoleLandingRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.ShouldEndWith("/no-party-binding"));
        nav.Uri.ShouldNotEndWith("/me");
    }

    [Fact]
    public void ConsumerWithEmptyPartyId_IsRoutedToNoPartyBinding_NeverToMe()
    {
        Services.AddPartiesUiClaimsResolution();

        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("consumer");
        auth.SetRoles("Consumer");
        auth.SetClaims(
            new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a"),
            new Claim(PartiesClaimTypes.PartyId, " "));

        IRenderedComponent<RoleLandingRedirect> cut = Render<RoleLandingRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.ShouldEndWith("/no-party-binding"));
        nav.Uri.ShouldNotEndWith("/me");
    }

    [Fact]
    public void ConsumerWithoutTenant_IsRoutedToNoPartyBinding_NeverToMe()
    {
        Services.AddPartiesUiClaimsResolution();

        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("consumer");
        auth.SetRoles("Consumer");
        auth.SetClaims(new Claim(PartiesClaimTypes.PartyId, "party-123"));

        IRenderedComponent<RoleLandingRedirect> cut = Render<RoleLandingRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.ShouldEndWith("/no-party-binding"));
        nav.Uri.ShouldNotEndWith("/me");
    }
}
