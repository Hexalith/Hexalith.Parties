namespace Hexalith.Parties.CommandApi.Search;

internal sealed record PartyMemoryCleanupResult(
    string PartyId,
    string MemoryUnitId,
    string SourceUri,
    bool Cleaned,
    string? BlockedReason);

internal sealed class PartyMemoryCleanupService(HttpClient httpClient)
{
    public async Task<PartyMemoryCleanupResult> DeleteMemoryUnitAsync(
        string tenantId,
        string caseId,
        string partyId,
        string memoryUnitId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryUnitId);

        string path = $"api/tenants/{Uri.EscapeDataString(tenantId)}/cases/{Uri.EscapeDataString(caseId)}/memory-units/{Uri.EscapeDataString(memoryUnitId)}";
        using HttpResponseMessage response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
        string sourceUri = $"urn:hexalith:parties:{tenantId}:party:{partyId}";

        return response.IsSuccessStatusCode
            ? new PartyMemoryCleanupResult(partyId, memoryUnitId, sourceUri, Cleaned: true, BlockedReason: null)
            : new PartyMemoryCleanupResult(
                partyId,
                memoryUnitId,
                sourceUri,
                Cleaned: false,
                BlockedReason: $"Memories cleanup failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    public static PartyMemoryCleanupResult CreateBlockedResult(
        string tenantId,
        string partyId,
        string memoryUnitId,
        string reason)
        => new(
            partyId,
            memoryUnitId,
            $"urn:hexalith:parties:{tenantId}:party:{partyId}",
            Cleaned: false,
            BlockedReason: reason);
}
