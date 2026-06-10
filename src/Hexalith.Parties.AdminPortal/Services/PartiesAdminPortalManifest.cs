using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.Parties.AdminPortal.Components;

namespace Hexalith.Parties.AdminPortal.Services;

public static class PartiesAdminPortalManifest
{
    public const string Route = "/admin/parties";
    public const string CreateRoute = $"{Route}/new";
    public const string DetailRoute = $"{Route}/{{partyId}}";
    public const string EditRoute = $"{Route}/{{partyId}}/edit";
    public const string GdprRoute = $"{Route}/{{partyId}}/gdpr";

    public static DomainManifest Manifest { get; } = new(
        Name: "Parties",
        BoundedContext: "Parties",
        Projections: [typeof(PartiesAdminPortal).FullName!],
        Commands: []);
}
