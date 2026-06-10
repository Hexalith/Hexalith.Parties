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
    {
        if (string.IsNullOrWhiteSpace(partyId) || partyId.Length > 128)
        {
            return false;
        }

        if (partyId is "." or "..")
        {
            return false;
        }

        foreach (char c in partyId)
        {
            if (!IsSafeCharacter(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeCharacter(char c)
        => (c >= 'a' && c <= 'z')
            || (c >= 'A' && c <= 'Z')
            || (c >= '0' && c <= '9')
            || c is '-' or '_' or '.' or '~';
}
