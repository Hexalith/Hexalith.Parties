using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.Composition;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.3 AC3 — pins the gating inputs the shell's <c>&lt;AuthorizeView&gt;</c> consumes:
/// <see cref="PartiesUiFrontComposerRegistration.RegisterDomain"/> must register the "parties" manifest
/// and exactly two policy-gated area nav entries (Parties→/admin/parties gated on <c>Admin</c>,
/// My space→/me gated on <c>Consumer</c>). The framework's actual hide/show rendering of a gated entry
/// is covered upstream by FrontComposerNavigationNavEntryTests; here we lock the contract those tests
/// rely on. The registry substitute also implements <see cref="IFrontComposerNavEntryRegistry"/> so the
/// <c>AddNavEntry</c> extension routes through it.
/// </summary>
public sealed class PartiesUiNavigationRegistrationTests
{
    [Fact]
    public void RegisterDomain_RegistersPartiesManifest_AndTwoPolicyGatedAreaEntries()
    {
        var registry = Substitute.For<IFrontComposerRegistry, IFrontComposerNavEntryRegistry>();
        var navRegistry = (IFrontComposerNavEntryRegistry)registry;
        var entries = new List<FrontComposerNavEntry>();
        navRegistry.AddNavEntry(Arg.Do<FrontComposerNavEntry>(entries.Add));

        PartiesUiFrontComposerRegistration.RegisterDomain(registry);

        // Manifest registered under the lowercase "parties" bounded context the entries group on.
        registry.Received(1).RegisterDomain(
            Arg.Is<DomainManifest>(m => m != null && m.BoundedContext == "parties" && m.Name == "Parties"));

        entries.Count.ShouldBe(2);

        FrontComposerNavEntry admin = entries.Single(e => e.Href == PartiesAdminPortalManifest.Route);
        admin.BoundedContext.ShouldBe("parties");
        admin.Title.ShouldBe("Parties");
        admin.RequiredPolicy.ShouldBe(PartiesUiAuthorization.AdminPolicy);
        admin.Order.ShouldBe(0);

        FrontComposerNavEntry consumer = entries.Single(e => e.Href == "/me");
        consumer.BoundedContext.ShouldBe("parties");
        consumer.Title.ShouldBe("My space");
        consumer.RequiredPolicy.ShouldBe(PartiesUiAuthorization.ConsumerPolicy);
        consumer.Order.ShouldBe(1);
    }

    [Fact]
    public void Manifest_UsesTheLowercasePartiesBoundedContext()
    {
        PartiesUiFrontComposerRegistration.Manifest.Name.ShouldBe("Parties");
        PartiesUiFrontComposerRegistration.Manifest.BoundedContext.ShouldBe("parties");
    }

    [Fact]
    public void RegisterDomain_Throws_WhenRegistryIsNull()
        => Should.Throw<ArgumentNullException>(
            () => PartiesUiFrontComposerRegistration.RegisterDomain(null!));
}
