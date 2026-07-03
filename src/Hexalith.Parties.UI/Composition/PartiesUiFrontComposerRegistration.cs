using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.UI.Authentication;

namespace Hexalith.Parties.UI.Composition;

/// <summary>
/// Contributes the Parties UI host's left-navigation entries and domain manifest to the FrontComposer
/// shell (Story 1.3). Discovered by <c>AddHexalithDomain&lt;PartiesUiDomainMarker&gt;()</c>, which
/// reflection-scans the marker's assembly for a type whose name ends in <c>Registration</c> exposing a
/// static <see cref="Manifest"/> (a <see cref="DomainManifest"/>) AND a static
/// <see cref="RegisterDomain(IFrontComposerRegistry)"/> method — both members are required or the type
/// is skipped with a warning. Modelled name-for-name on
/// <c>Hexalith.Tenants.UI.Composition.TenantsFrontComposerRegistration</c>.
/// </summary>
public static class PartiesUiFrontComposerRegistration
{
    /// <summary>
    /// The domain manifest supplying the "Parties" left-navigation category. The bounded context is
    /// <c>"parties"</c> (lowercase) so the entries below group under one category — the shell matches
    /// <c>entry.BoundedContext</c> to <c>manifest.BoundedContext</c> ordinally.
    /// </summary>
    public static DomainManifest Manifest { get; } = new(
        "Parties",
        "parties",
        [],
        [],
        Icon: "Regular.Size20.PersonBoard");

    /// <summary>
    /// Registers the manifest and the navigation group exposed by the current FrontComposer contract.
    /// </summary>
    /// <param name="registry">The FrontComposer registry to contribute the manifest and entries to.</param>
    public static void RegisterDomain(IFrontComposerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        // The manifest provides the "Parties" category title for the shell's left navigation and, by
        // making HasNavigation true, is what lights up <FrontComposerNavigation /> at all.
        registry.RegisterDomain(Manifest);
        registry.AddNavGroup(Manifest.Name, Manifest.BoundedContext);
        registry.AddNavEntry(new FrontComposerNavEntry(
            Manifest.BoundedContext,
            "Parties",
            PartiesAdminPortalManifest.Route,
            Order: 0,
            RequiredPolicy: PartiesUiAuthorization.AdminPolicy));
        registry.AddNavEntry(new FrontComposerNavEntry(
            Manifest.BoundedContext,
            "My space",
            "/me",
            Order: 1,
            RequiredPolicy: PartiesUiAuthorization.ConsumerPolicy));
    }
}
