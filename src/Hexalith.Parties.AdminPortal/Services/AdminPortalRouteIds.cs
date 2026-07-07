using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.AdminPortal.Services;

public static class AdminPortalRouteIds
{
    public static string? Normalize(string? routePartyId)
    {
        if (string.IsNullOrWhiteSpace(routePartyId))
        {
            return null;
        }

        try
        {
            return Uri.UnescapeDataString(routePartyId.Trim());
        }
        catch (UriFormatException)
        {
            return routePartyId.Trim();
        }
    }

    public static bool IsSafe(string? partyId)
        => PartyIdentifier.IsValid(partyId);
}
