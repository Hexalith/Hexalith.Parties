using Bunit;
using Bunit.TestDoubles;

using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.Composition;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.3 AC1/AC2 — the "never cross-render" proof, in-repo. The registration test pins the gating
/// INPUTS (each entry's <see cref="FrontComposerNavEntry.RequiredPolicy"/>); this test renders the actual
/// registered entries through the same <c>&lt;AuthorizeView Policy="@entry.RequiredPolicy"&gt;</c> gate the
/// shell uses (FrontComposerNavigation.razor:51-55, reproduced in <see cref="NavEntryGatingHarness"/>) and
/// asserts the OUTPUT: an Admin principal sees only the Administration entry, a Consumer principal sees only
/// the My-space entry, and an unauthenticated principal sees neither — they never cross-render. bUnit's fake
/// authorization grants AuthorizeView policy checks via <c>SetPolicies(...)</c>, so this exercises the
/// gating wiring independently of the real role→policy mapping (which
/// <see cref="PartiesUiAuthorizationPolicyTests"/> proves).
/// </summary>
public sealed class PartiesUiNavEntryGatingTests : BunitContext
{
    [Fact]
    public void AdminPrincipal_SeesOnlyTheAdminEntry_NeverTheConsumerEntry()
    {
        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("admin");
        auth.SetPolicies(PartiesUiAuthorization.AdminPolicy);

        IRenderedComponent<NavEntryGatingHarness> cut = RenderRegisteredEntries();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("href=\"/admin\""));
        cut.WaitForAssertion(() => cut.Markup.ShouldNotContain("href=\"/me\""));
    }

    [Fact]
    public void ConsumerPrincipal_SeesOnlyTheConsumerEntry_NeverTheAdminEntry()
    {
        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetAuthorized("consumer");
        auth.SetPolicies(PartiesUiAuthorization.ConsumerPolicy);

        IRenderedComponent<NavEntryGatingHarness> cut = RenderRegisteredEntries();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("href=\"/me\""));
        cut.WaitForAssertion(() => cut.Markup.ShouldNotContain("href=\"/admin\""));
    }

    [Fact]
    public void UnauthenticatedPrincipal_SeesNeitherAreaEntry()
    {
        BunitAuthorizationContext auth = AddAuthorization();
        auth.SetNotAuthorized();

        IRenderedComponent<NavEntryGatingHarness> cut = RenderRegisteredEntries();

        cut.WaitForAssertion(() => cut.Markup.ShouldNotContain("href=\"/admin\""));
        cut.WaitForAssertion(() => cut.Markup.ShouldNotContain("href=\"/me\""));
    }

    private IRenderedComponent<NavEntryGatingHarness> RenderRegisteredEntries()
        => Render<NavEntryGatingHarness>(parameters => parameters
            .Add(p => p.Entries, RegisteredAreaEntries()));

    // The real entries the host registers — so the proof tracks production policy values, not literals.
    private static IReadOnlyList<FrontComposerNavEntry> RegisteredAreaEntries()
    {
        var registry = Substitute.For<IFrontComposerRegistry, IFrontComposerNavEntryRegistry>();
        var entries = new List<FrontComposerNavEntry>();
        ((IFrontComposerNavEntryRegistry)registry).AddNavEntry(Arg.Do<FrontComposerNavEntry>(entries.Add));

        PartiesUiFrontComposerRegistration.RegisterDomain(registry);

        return entries;
    }
}
