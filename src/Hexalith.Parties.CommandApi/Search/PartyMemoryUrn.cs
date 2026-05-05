using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;

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
        => TryParse(sourceUri, logger: null, out tenantId, out partyId);

    /// <summary>
    /// Parses a party source URN. When <paramref name="logger"/> is supplied, structured warnings
    /// are emitted for each rejection reason so silent drops surface in observability — repair
    /// tooling, manual reindex, or future writers that escape values differently can be diagnosed.
    /// </summary>
    public static bool TryParse(string? sourceUri, ILogger? logger, out string tenantId, out string partyId)
    {
        tenantId = string.Empty;
        partyId = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceUri))
        {
            logger?.LogWarning("PartyMemoryUrn.TryParse rejected null/whitespace source URI.");
            return false;
        }

        string[] parts = sourceUri.Split(':', StringSplitOptions.None);
        if (parts.Length != 6)
        {
            // P10: Hash the URI rather than emit it verbatim — attacker-controlled URN parts
            // can carry log-injection sequences or PII, and warning floods leak content
            // into ops dashboards. The hash is reproducible across log lines so an operator
            // can correlate occurrences without seeing the raw value.
            logger?.LogWarning(
                "PartyMemoryUrn.TryParse rejected source URI hash {SourceUriHash}: expected 6 colon-separated parts, found {PartCount}. All URNs MUST be built via PartyMemoryUrn.Build so reserved characters are percent-encoded.",
                Hash(sourceUri),
                parts.Length);
            return false;
        }

        if (!string.Equals(parts[0], Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[1], Namespace, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[2], ResourceType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[4], PartyMarker, StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning(
                "PartyMemoryUrn.TryParse rejected source URI hash {SourceUriHash}: scheme/namespace/resource/marker do not match the canonical urn:hexalith:parties:*:party:* shape.",
                Hash(sourceUri));
            return false;
        }

        tenantId = Uri.UnescapeDataString(parts[3]);
        partyId = Uri.UnescapeDataString(parts[5]);
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(partyId))
        {
            logger?.LogWarning(
                "PartyMemoryUrn.TryParse rejected source URI hash {SourceUriHash}: decoded tenant or party id is empty.",
                Hash(sourceUri));
            return false;
        }

        return true;
    }

    private static string Hash(string value)
    {
        Span<byte> hash = stackalloc byte[32];
        _ = SHA256.HashData(Encoding.UTF8.GetBytes(value), hash);
        // 12 hex chars = 48 bits — plenty for log correlation without leaking the source.
        return Convert.ToHexString(hash[..6]).ToLowerInvariant();
    }
}
