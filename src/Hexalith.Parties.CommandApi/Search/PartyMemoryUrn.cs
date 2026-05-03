namespace Hexalith.Parties.CommandApi.Search;

/// <summary>
/// Builds and parses the canonical Memories source URN for parties:
/// <c>urn:hexalith:parties:{tenantId}:party:{partyId}</c>. Tenant id and party id are
/// percent-encoded so that values containing colons or other reserved characters do not
/// break the parser. Always round-trip through this helper — never interpolate raw values.
/// </summary>
internal static class PartyMemoryUrn
{
    public const string Scheme = "urn";
    public const string Namespace = "hexalith";
    public const string ResourceType = "parties";
    public const string PartyMarker = "party";

    public static string Build(string tenantId, string partyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return $"{Scheme}:{Namespace}:{ResourceType}:{Uri.EscapeDataString(tenantId)}:{PartyMarker}:{Uri.EscapeDataString(partyId)}";
    }

    public static bool TryParse(string? sourceUri, out string tenantId, out string partyId)
    {
        tenantId = string.Empty;
        partyId = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceUri))
        {
            return false;
        }

        string[] parts = sourceUri.Split(':', StringSplitOptions.None);
        if (parts.Length != 6
            || !string.Equals(parts[0], Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[1], Namespace, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[2], ResourceType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[4], PartyMarker, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        tenantId = Uri.UnescapeDataString(parts[3]);
        partyId = Uri.UnescapeDataString(parts[5]);
        return !string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(partyId);
    }
}
