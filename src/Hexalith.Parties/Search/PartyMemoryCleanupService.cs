using System.Net.Http.Headers;

using Hexalith.Memories.Contracts.V1;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Search;

/// <summary>
/// Deletes mapped Memories units and probes the versioned per-unit cleanup route.
/// </summary>
internal sealed partial class PartyMemoryCleanupService(
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

        // The shared route builder returns an escaped relative V1 path, preserving
        // any configured endpoint prefix as well as the captured tenant/case scope.
        string path = MemoriesRoutes.CaseMemoryUnitPath(tenantId, caseId, memoryUnitId);

        try
        {
            using HttpResponseMessage response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            return await PartyMemoryCleanupResponse.IsAcceptedAsync(response, cancellationToken).ConfigureAwait(false)
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
                BlockedReason: $"Memories cleanup transport error: {ex.GetType().Name}");
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "Memories cleanup timed out for {TenantId}/{PartyId}.", tenantId, partyId);
            return new PartyMemoryCleanupResult(
                partyId,
                memoryUnitId,
                sourceUri,
                Cleaned: false,
                BlockedReason: $"Memories cleanup timed out: {ex.GetType().Name}");
        }
    }

    /// <summary>
    /// Erase every memory unit indexed for a party. AC5 (resolved decision #2): Memories does
    /// not expose a batch <c>?sourceUri=</c> DELETE, so we read the per-party →
    /// memory-unit-id mapping that the indexing service records, then iterate per-unit
    /// DELETEs against the existing <c>DELETE /api/v1/tenants/{t}/cases/{c}/memory-units/{id}</c>
    /// endpoint. The CaseId stored with each mapping is authoritative; <paramref name="caseId"/>
    /// is used only for legacy mappings written before CaseId persistence was introduced.
    /// <para>
    /// <b>Inventory progress (P17 / P25).</b> Successfully cleaned exact snapshot entries
    /// are removed in the final mapping update using optimistic concurrency. Failed entries,
    /// concurrent additions and changed case/source identities remain available for a retry.
    /// Progress persistence has a five-second cancellation budget independent of the caller;
    /// caller cancellation propagates after that attempt. A recovery failure during caller
    /// cancellation is logged without masking cancellation; other persistence failures propagate.
    /// Deletion counts describe this call's snapshot, not terminal physical-erasure proof.
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
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        string sourceUri = PartyMemoryUrn.Build(tenantId, partyId);

        IReadOnlyList<PartyMemoryUnitMappingEntry> mappings;
        try
        {
            mappings = await mappingStore
                .GetMappingsAsync(tenantId, partyId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Memories cleanup could not read its mapping inventory because of {ExceptionType}.",
                ex.GetType().Name);
            return new PartyMemoryCleanupResult(
                partyId,
                MemoryUnitId: string.Empty,
                sourceUri,
                Cleaned: false,
                BlockedReason: "Memories cleanup could not verify its mapping inventory.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (mappings.Count == 0)
        {
            return new PartyMemoryCleanupResult(
                partyId,
                MemoryUnitId: string.Empty,
                sourceUri,
                Cleaned: true,
                BlockedReason: null);
        }

        if (httpClient.BaseAddress is null)
        {
            return new PartyMemoryCleanupResult(
                partyId,
                MemoryUnitId: string.Empty,
                sourceUri,
                Cleaned: false,
                BlockedReason: "Memories cleanup blocked: HttpClient has no BaseAddress (Parties:MemoriesSearch:Endpoint missing).");
        }

        int initialCount = mappings.Count;
        List<PartyMemoryUnitMappingEntry> cleanedEntries = [];
        int retainedCount = initialCount;
        string? firstBlockedReason = null;
        string? firstBlockedUnitId = null;
        int deleted = 0;
        try
        {
            foreach (PartyMemoryUnitMappingEntry mapping in mappings)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string resolvedCaseId = string.IsNullOrWhiteSpace(mapping.CaseId) ? caseId : mapping.CaseId;
                if (string.IsNullOrWhiteSpace(resolvedCaseId))
                {
                    firstBlockedReason ??= "Memories cleanup blocked: the indexed unit has no persisted CaseId and no fallback CaseId is configured.";
                    firstBlockedUnitId ??= mapping.MemoryUnitId;
                    continue;
                }

                PartyMemoryCleanupResult perUnit = await DeleteMemoryUnitAsync(
                    tenantId,
                    resolvedCaseId,
                    partyId,
                    mapping.MemoryUnitId,
                    cancellationToken).ConfigureAwait(false);

                if (perUnit.Cleaned)
                {
                    deleted++;
                    cleanedEntries.Add(mapping);
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
            // Preserve confirmed progress even when the caller cancels during a DELETE
            // or this write. The independent budget bounds cooperative store recovery.
            if (cleanedEntries.Count > 0)
            {
                using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    retainedCount = await mappingStore
                        .RemoveMappingsAsync(tenantId, partyId, cleanedEntries, recovery.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                {
                    LogCancelledProgressPersistenceFailure(logger, ex.GetType().Name);
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (firstBlockedReason is not null)
        {
            return new PartyMemoryCleanupResult(
                partyId,
                MemoryUnitId: firstBlockedUnitId ?? string.Empty,
                sourceUri,
                Cleaned: false,
                BlockedReason: $"Memories cleanup partially failed ({deleted} of {initialCount} units deleted): {firstBlockedReason}");
        }

        if (retainedCount > 0)
        {
            return new PartyMemoryCleanupResult(
                partyId,
                MemoryUnitId: string.Empty,
                sourceUri,
                Cleaned: false,
                BlockedReason: "Memories cleanup retained additional mappings; another cleanup pass is required.");
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
    /// synthetic memory-unit id. Only a structured MEMORY_UNIT_NOT_FOUND response proves
    /// that the route handled a miss in the requested case. Other 404s surface a deployment gap:
    /// <list type="bullet">
    ///   <item><description>200/204 — route reachable and the synthetic id improbably matched (still healthy).</description></item>
    ///   <item><description>404 with MEMORY_UNIT_NOT_FOUND — route reachable, unit absent in the requested case.</description></item>
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

        string syntheticUnitId = $"health-probe-{Guid.NewGuid():N}";
        string path = MemoriesRoutes.CaseMemoryUnitPath(tenantId, caseId, syntheticUnitId);

        try
        {
            using HttpResponseMessage response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            int code = (int)response.StatusCode;
            // An unqualified 404 can be a routing or provisioning failure. Reuse the same
            // response contract as erasure cleanup and indexing compensation.
            bool reachable = await PartyMemoryCleanupResponse.IsAcceptedAsync(response, cancellationToken).ConfigureAwait(false);
            string? reason = reachable
                ? null
                : code switch
                {
                    401 or 403 => $"Memories cleanup auth misconfigured (HTTP {code}).",
                    404 => "Memories cleanup probe returned an unqualified HTTP 404; route or tenant/case provisioning could not be verified.",
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

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Memories cleanup progress persistence failed after caller cancellation with {ExceptionType}; cleanup must be retried.")]
    private static partial void LogCancelledProgressPersistenceFailure(ILogger logger, string exceptionType);
}
