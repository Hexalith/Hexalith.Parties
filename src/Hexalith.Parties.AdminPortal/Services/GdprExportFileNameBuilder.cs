namespace Hexalith.Parties.AdminPortal.Services;

public static class GdprExportFileNameBuilder
{
    public static string Build(string partyId, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        string token = new(partyId
            .Select(static c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')
            .ToArray());

        return $"party-{token}-export-{timestamp:yyyyMMddHHmmss}.json";
    }
}
