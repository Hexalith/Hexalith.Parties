using System.Net.Http.Headers;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.CommandApi.Search;

internal sealed record PartyMemoryCleanupResult(
    string PartyId,
    string MemoryUnitId,
    string SourceUri,
    bool Cleaned,
    string? BlockedReason);

internal sealed class PartyMemoryCleanupService(HttpClient httpClient, ILogger<PartyMemoryCleanupService> logger)
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
    /// Delete every memory unit whose source URI matches the canonical party URN. This is the
    /// erasure-side path used by <c>IErasureVerificationService</c>: erasure does not know
    /// individual memory unit ids, only the (tenant, party) pair, so we ask Memories to remove
    /// all units backed by the canonical party source URI.
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

        string path = $"api/tenants/{Uri.EscapeDataString(tenantId)}/cases/{Uri.EscapeDataString(caseId)}/memory-units?sourceUri={Uri.EscapeDataString(sourceUri)}";

        try
        {
            using HttpResponseMessage response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound
                ? new PartyMemoryCleanupResult(partyId, MemoryUnitId: string.Empty, sourceUri, Cleaned: true, BlockedReason: null)
                : new PartyMemoryCleanupResult(
                    partyId,
                    MemoryUnitId: string.Empty,
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
                MemoryUnitId: string.Empty,
                sourceUri,
                Cleaned: false,
                BlockedReason: $"Memories cleanup transport error: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "Memories cleanup timed out for {TenantId}/{PartyId}.", tenantId, partyId);
            return new PartyMemoryCleanupResult(
                partyId,
                MemoryUnitId: string.Empty,
                sourceUri,
                Cleaned: false,
                BlockedReason: $"Memories cleanup timed out: {ex.Message}");
        }
    }

    public static PartyMemoryCleanupResult CreateBlockedResult(
        string tenantId,
        string partyId,
        string memoryUnitId,
        string reason)
        => new(
            partyId,
            memoryUnitId,
            PartyMemoryUrn.Build(tenantId, partyId),
            Cleaned: false,
            BlockedReason: reason);

    /// <summary>
    /// Configures the API token bearer header on the supplied HttpClient if a token is provided.
    /// Called by DI when registering the typed HttpClient.
    /// </summary>
    public static void ConfigureAuthorization(HttpClient httpClient, string? apiToken)
    {
        if (httpClient is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(apiToken))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        }
    }
}
