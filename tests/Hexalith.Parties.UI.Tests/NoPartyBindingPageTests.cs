using Bunit;

using Hexalith.Parties.UI.Components.Account;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.4 AC2 — the fail-closed <see cref="NoPartyBinding"/> page renders as a non-data onboarding
/// state. It leads with a focusable <c>&lt;h1&gt;</c> (the <c>&lt;FocusOnNavigate Selector="h1"&gt;</c>
/// target in <c>Routes.razor</c>) carrying static reassuring copy, and never fetches or displays party
/// data. The policy gate is pinned separately in <see cref="PartiesUiAreaAuthorizationTests"/>; these
/// tests prove what a diverted unbound Consumer actually sees.
/// </summary>
public sealed class NoPartyBindingPageTests : BunitContext
{
    [Fact]
    public void NoPartyBinding_LeadsWithASingleFocusableHeading()
    {
        IRenderedComponent<NoPartyBinding> cut = Render<NoPartyBinding>();

        // A single <h1> exists and is non-empty — the FocusOnNavigate target the router moves focus to.
        cut.FindAll("h1").Count.ShouldBe(1);
        cut.Find("h1").TextContent.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void NoPartyBinding_RendersStaticReassuringOnboardingCopy_NotADataScreen()
    {
        IRenderedComponent<NoPartyBinding> cut = Render<NoPartyBinding>();

        // Reassuring, neutral onboarding copy pointing the user to support/their administrator.
        cut.Markup.ShouldContain("profile");
        cut.Markup.ShouldContain("administrator");

        // It is NOT a data screen: only static copy renders — no form inputs or interactive data controls.
        cut.FindAll("input").ShouldBeEmpty();
        cut.FindAll("button").ShouldBeEmpty();
    }
}
