using System.Globalization;

namespace Hexalith.Parties.Contracts;

public static class PartyExportFileName
{
    private const int MaxTokenLength = 64;

    public static string Build(string partyId, DateTimeOffset exportedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        string sanitized = new(partyId
            .Select(static c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')
            .ToArray());
        string token = sanitized.Length > MaxTokenLength ? sanitized[..MaxTokenLength] : sanitized;
        token = token.Trim('-');
        if (string.IsNullOrWhiteSpace(token))
        {
            token = "party";
        }

        return $"party-{token}-{exportedAt.UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture)}.json";
    }
}
