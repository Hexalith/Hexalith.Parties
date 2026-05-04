using System.Net.Http.Headers;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.CommandApi.Search;

internal sealed record PartyMemoryCleanupResult(
    string PartyId,
    string MemoryUnitId,
    string SourceUri,
    bool Cleaned,
    string? BlockedReason);

internal sealed class PartyMemoryCleanupService(
    HttpClient httpClient,
    IPartyMemoryUnitMappingStore mappingStore,
    ILogger<PartyMemoryCleanupService> logger)
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

        string sourceUri = PartyMemoryUrn.Build(tenantId, partyId);

        if (httpClient.BaseAddress is null)
        {
            return new PartyMemoryCleanupResult(
                partyId,
                memoryUnitId,
                sourceUri,
                Cleaned: false,
                BlockedReason: "Memories cleanup blocked: HttpClient has no BaseAddress (Parties:MemoriesSearch:Endpoint missing).");
        }

        // Relative path (no leading slash) so it resolves against BaseAddress's path
        // component. Combined with the `Endpoint must end with /` validator, this gives a
        // predictable URL shape: BaseAddress="https://memories.example.com/v1/" + path =
        // "https://memories.example.com/v1/api/...".
        string path = $"api/tenants/{Uri.EscapeDataString(tenantId)}/cases/{Uri.EscapeDataString(caseId)}/memory-units/{Uri.EscapeDataString(memoryUnitId)}";

        try
        {
            using HttpResponseMessage response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound
                ? new PartyMemoryCleanupResult(partyId, memoryUnitId, sourceUri, Cleaned: true, BlockedReason: null)
                : new PartyMemoryCleanupResult(
                    partyId,
                    memoryUnitId,
                    sourceUri,
                    Cleaned: false,
                    BlockedReason: $"Memories cleanup failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Memories cleanup transport failure for {TenantId}/{PartyId}.", tenantId, partyId);
            return new PartyMemoryCleanupResult(
                partyId,
                memoryUnitId,
                sourceUri,
                Cleaned: false,
                BlockedReason: $"Memories cleanup transport error: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "Memories cleanup timed out for {TenantId}/{PartyId}.", tenantId, partyId);
            return new PartyMemoryCleanupResult(
                partyId,
                memoryUnitId,
                sourceUri,
                Cleaned: false,
                BlockedReason: $"Memories cleanup timed out: {ex.Message}");
        }
    }

    /// <summary>
    /// Erase every memory unit indexed for a party. AC5 (resolved decision #2): Memories does
    /// not expose a batch <c>?sourceUri=</c> DELETE, so we read the per-party →
    /// memory-unit-id mapping that the indexing service records, then iterate per-unit
    /// DELETEs against the existing <c>DELETE /api/tenants/{t}/cases/{c}/memory-units/{id}</c>
    /// endpoint. The result aggregates: <c>Cleaned=true</c> only when every unit deleted
    /// successfully (or the mapping was empty). On any per-unit failure the aggregate result
    /// reports the first blocked reason — the GDPR verifier blocks erasure completion until
    /// the operator either re-runs cleanup or records a manual override.
    /// </summary>
    public async Task<PartyMemoryCleanupResult> DeleteByPartyAsync(
        string tenantId,
        string caseId,
        string partyId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        string sourceUri = PartyMemoryUrn.Build(tenantId, partyId);

        if (httpClient.BaseAddress is null)
        {
            return new PartyMemoryCleanupResult(
                partyId,
                MemoryUnitId: string.Empty,
                sourceUri,
                Cleaned: false,
                BlockedReason: "Memories cleanup blocked: HttpClient has no BaseAddress (Parties:MemoriesSearch:Endpoint missing).");
        }

        IReadOnlyList<PartyMemoryUnitMappingEntry> mappings = await mappingStore
            .GetMappingsAsync(tenantId, partyId, cancellationToken)
            .ConfigureAwait(false);

        if (mappings.Count == 0)
        {
            // No recorded mapping = nothing to delete. This is the legitimate "party was
            // never indexed" case (e.g., Memories was disabled at the time of party creation
            // or indexing has not yet caught up). Reporting Cleaned=true is correct here:
            // AC5 requires us to ensure no party-related memory units remain, and there are
            // none to begin with.
            return new PartyMemoryCleanupResult(
                partyId,
                MemoryUnitId: string.Empty,
                sourceUri,
                Cleaned: true,
                BlockedReason: null);
        }

        string? firstBlockedReason = null;
        string? firstBlockedUnitId = null;
        int deleted = 0;
        foreach (PartyMemoryUnitMappingEntry mapping in mappings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PartyMemoryCleanupResult perUnit = await DeleteMemoryUnitAsync(
                tenantId,
                caseId,
                partyId,
                mapping.MemoryUnitId,
                cancellationToken).ConfigureAwait(false);

            if (perUnit.Cleaned)
            {
                deleted++;
            }
            else if (firstBlockedReason is null)
            {
                firstBlockedReason = perUnit.BlockedReason;
                firstBlockedUnitId = perUnit.MemoryUnitId;
            }
        }

        if (firstBlockedReason is not null)
        {
            // Leave the mapping in place so a re-run can pick up where we left off; clearing
            // it would erase the audit trail of which units still need cleanup. The aggregate
            // identifies the first blocked unit so an operator can act on it specifically.
            return new PartyMemoryCleanupResult(
                partyId,
                MemoryUnitId: firstBlockedUnitId ?? string.Empty,
                sourceUri,
                Cleaned: false,
                BlockedReason: $"Memories cleanup partially failed ({deleted} of {mappings.Count} units deleted): {firstBlockedReason}");
        }

        // All units deleted — clear the mapping so a recreated party with the same id starts
        // with an empty mapping rather than inheriting stale references to deleted units.
        await mappingStore.ClearMappingsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        return new PartyMemoryCleanupResult(
            partyId,
            MemoryUnitId: mappings.Count == 1 ? mappings[0].MemoryUnitId : string.Empty,
            sourceUri,
            Cleaned: true,
            BlockedReason: null);
    }

    /// <summary>
    /// Configures the API token bearer header on the supplied HttpClient if a token is provided.
    /// Called by DI when registering the typed HttpClient. Throws on a null client (programming
    /// error) and falls through cleanly when the token is malformed (whitespace / control chars
    /// would otherwise blow up <see cref="AuthenticationHeaderValue"/> with a
    /// <see cref="FormatException"/>).
    /// </summary>
    public static void ConfigureAuthorization(HttpClient httpClient, string? apiToken, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return;
        }

        try
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        }
        catch (FormatException ex)
        {
            logger?.LogError(
                ex,
                "Memories cleanup API token is not a valid HTTP header value (likely contains whitespace or control characters). The token has been ignored; cleanup calls will be unauthenticated.");
        }
    }
}
