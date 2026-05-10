namespace Hexalith.Parties.AdminPortal.Services;

public static class GdprExportFileNameBuilder
{
    private const int MaxTokenLength = 64;

    public static string Build(string partyId, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        string sanitized = new(partyId
            .Select(static c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')
            .ToArray());

        string token = sanitized.Length > MaxTokenLength ? sanitized[..MaxTokenLength] : sanitized;

        return $"party-{token}-export-{timestamp.UtcDateTime:yyyyMMddHHmmss}Z.json";
    }
}
