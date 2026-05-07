using System.Net.Http.Headers;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Search;

internal sealed record PartyMemoryCleanupResult(
    string PartyId,
    string MemoryUnitId,
    string SourceUri,
    bool Cleaned,
    string? BlockedReason);

internal sealed record PartyMemoryCleanupProbeResult(
    bool Reachable,
    int? StatusCode,
    string? Reason);

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
    /// endpoint.
    /// <para>
    /// <b>Atomicity (P17 / P25).</b> After each successful per-unit DELETE the mapping is
    /// rewritten with only the still-failed entries so a cancellation or partial failure
    /// leaves a consistent audit trail: a re-run will retry exactly the units that still
    /// need cleanup, and the aggregate <c>n of N deleted</c> ratio reflects truth across
    /// re-runs rather than collapsing to "{remaining} of {remaining}".
    /// </para>
    /// <para>
    /// <b>Reporting (P36).</b> The aggregate <c>BlockedReason</c> includes the deleted-count
    /// even on success so audit trails record per-erasure throughput; <c>MemoryUnitId</c>
    /// carries the single-unit id when only one unit was mapped (otherwise the per-unit
    /// detail lives in the per-unit results which the orchestrator can iterate).
    /// </para>
    /// <para>
    /// <b>Empty-mapping invariant (P38).</b> Reporting <c>Cleaned=true</c> on
    /// <c>mappings.Count == 0</c> assumes the mapping store and the indexing service share
    /// the same view of "what was indexed". A misconfiguration where indexing wrote to a
    /// different state store than cleanup reads from would silently report success here.
    /// The validator pins the state-store name at startup (see
    /// <see cref="PartyMemoryUnitMappingStoreOptions"/>); deployment validation should also
    /// probe the mapping store round-trip (AC6 follow-up).
    /// </para>
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
            return new PartyMemoryCleanupResult(
                partyId,
                MemoryUnitId: string.Empty,
                sourceUri,
                Cleaned: true,
                BlockedReason: null);
        }

        int initialCount = mappings.Count;
        List<PartyMemoryUnitMappingEntry> remaining = [.. mappings];
        string? firstBlockedReason = null;
        string? firstBlockedUnitId = null;
        int deleted = 0;
        try
        {
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
                    _ = remaining.RemoveAll(e =>
                        string.Equals(e.MemoryUnitId, mapping.MemoryUnitId, StringComparison.Ordinal));
                }
                else if (firstBlockedReason is null)
                {
                    firstBlockedReason = perUnit.BlockedReason;
                    firstBlockedUnitId = perUnit.MemoryUnitId;
                }
            }
        }
        finally
        {
            // Persist whatever we have done so far so a re-run after cancellation /
            // exception sees the same audit trail. ReplaceMappingsAsync handles the
            // empty-list case as a Clear.
            if (remaining.Count != initialCount)
            {
                await mappingStore
                    .ReplaceMappingsAsync(tenantId, partyId, remaining, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (firstBlockedReason is not null)
        {
            return new PartyMemoryCleanupResult(
                partyId,
                MemoryUnitId: firstBlockedUnitId ?? string.Empty,
                sourceUri,
                Cleaned: false,
                BlockedReason: $"Memories cleanup partially failed ({deleted} of {initialCount} units deleted): {firstBlockedReason}");
        }

        return new PartyMemoryCleanupResult(
            partyId,
            MemoryUnitId: initialCount == 1 ? mappings[0].MemoryUnitId : string.Empty,
            sourceUri,
            Cleaned: true,
            BlockedReason: deleted > 1 ? $"Memories cleanup deleted {deleted} of {initialCount} units." : null);
    }

    /// <summary>
    /// Lightweight health probe of the per-unit cleanup route. Issues a <c>DELETE</c> against a
    /// synthetic memory-unit id; 404 means the route is reachable and the unit (correctly) does
    /// not exist. Any other outcome surfaces an AC6 deployment-validation gap:
    /// <list type="bullet">
    ///   <item><description>200/204 — route reachable and the synthetic id improbably matched (still healthy).</description></item>
    ///   <item><description>404 — route reachable, no unit exists (expected healthy outcome).</description></item>
    ///   <item><description>401/403 — auth misconfigured (the token in <see cref="PartyMemorySearchOptions.ApiToken"/> is rejected).</description></item>
    ///   <item><description>405 — DELETE not allowed at this shape (the AC5 gap that resolved decision #2 fixed; signals a Memories-server regression).</description></item>
    ///   <item><description>5xx / network error — endpoint unreachable.</description></item>
    /// </list>
    /// </summary>
    public async Task<PartyMemoryCleanupProbeResult> ProbeCleanupRouteAsync(
        string tenantId,
        string caseId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);

        if (httpClient.BaseAddress is null)
        {
            return new PartyMemoryCleanupProbeResult(false, null, "HttpClient has no BaseAddress.");
        }

        string syntheticUnitId = $"_health-probe-{Guid.NewGuid():N}";
        string path = $"api/tenants/{Uri.EscapeDataString(tenantId)}/cases/{Uri.EscapeDataString(caseId)}/memory-units/{Uri.EscapeDataString(syntheticUnitId)}";

        try
        {
            using HttpResponseMessage response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            int code = (int)response.StatusCode;
            // 404 = route reachable, no unit (expected); 200/204 = route reachable + unit deleted
            // (improbable but acceptable). Any other status is a deployment-validation gap.
            bool reachable = response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
            string? reason = reachable
                ? null
                : code switch
                {
                    401 or 403 => $"Memories cleanup auth misconfigured (HTTP {code}).",
                    405 => "Memories cleanup route does not accept DELETE (AC5 regression — server may have removed the per-unit DELETE endpoint).",
                    _ => $"Memories cleanup probe returned unexpected HTTP {code} {response.ReasonPhrase}.",
                };
            return new PartyMemoryCleanupProbeResult(reachable, code, reason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            logger.LogWarning(ex, "Memories cleanup probe failed for tenant {TenantId}/case {CaseId}.", tenantId, caseId);
            return new PartyMemoryCleanupProbeResult(false, null, $"Memories cleanup endpoint unreachable: {ex.GetType().Name}.");
        }
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
