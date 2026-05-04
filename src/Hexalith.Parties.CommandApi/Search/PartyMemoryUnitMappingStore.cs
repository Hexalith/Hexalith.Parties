using Dapr.Client;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.CommandApi.Search;

/// <summary>
/// Per-party → memory-unit-id mapping. Recorded by the indexing service after successful
/// <c>IngestAsync</c> calls; consumed by the cleanup service so erasure can iterate per-unit
/// DELETEs against the existing per-unit Memories endpoint. Implements the AC5 cleanup
/// re-architecture described in resolved decision #2 of the story spec — without this
/// mapping the erasure flow has no way to know which memory unit ids belong to a party.
/// </summary>
internal sealed record PartyMemoryUnitMappingEntry(string MemoryUnitId, string SourceUri);

internal interface IPartyMemoryUnitMappingStore
{
    Task RecordMappingAsync(
        string tenantId,
        string partyId,
        string memoryUnitId,
        string sourceUri,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken);

    Task ClearMappingsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Dapr-state-store backed mapping. Storage shape: a single state key per (tenant, party)
/// holding a list of <see cref="PartyMemoryUnitMappingEntry"/>. Memories deduplicates by
/// source URI server-side, so calling <c>IngestAsync</c> twice for the same source URI
/// returns the same workflow id; the mapping list dedupes by memory-unit-id on read so
/// repeated re-indexing does not bloat the list.
/// </summary>
internal sealed class PartyMemoryUnitMappingStore(
    DaprClient daprClient,
    ILogger<PartyMemoryUnitMappingStore> logger) : IPartyMemoryUnitMappingStore
{
    private const string StateStoreName = "statestore";

    public async Task RecordMappingAsync(
        string tenantId,
        string partyId,
        string memoryUnitId,
        string sourceUri,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryUnitId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUri);

        string key = BuildKey(tenantId, partyId);
        IReadOnlyList<PartyMemoryUnitMappingEntry> current = await ReadAsync(key, cancellationToken).ConfigureAwait(false);

        // Skip the write if the mapping already records this memory-unit-id (Memories returns
        // the same id for repeated ingests of the same source URI). Avoids unbounded growth
        // and a no-op state-store round trip.
        foreach (PartyMemoryUnitMappingEntry existing in current)
        {
            if (string.Equals(existing.MemoryUnitId, memoryUnitId, StringComparison.Ordinal))
            {
                return;
            }
        }

        List<PartyMemoryUnitMappingEntry> updated = [.. current, new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri)];

        try
        {
            await daprClient
                .SaveStateAsync(StateStoreName, key, updated, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Failed to record memory-unit mapping for {TenantId}/{PartyId} -> {MemoryUnitId}. Erasure cleanup may miss this unit until it is re-recorded.",
                tenantId,
                partyId,
                memoryUnitId);
        }
    }

    public async Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return await ReadAsync(BuildKey(tenantId, partyId), cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearMappingsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        try
        {
            await daprClient
                .DeleteStateAsync(StateStoreName, BuildKey(tenantId, partyId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Failed to clear memory-unit mapping for {TenantId}/{PartyId}; the mapping may be re-discovered on next indexing.",
                tenantId,
                partyId);
        }
    }

    private async Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> ReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            List<PartyMemoryUnitMappingEntry>? state = await daprClient
                .GetStateAsync<List<PartyMemoryUnitMappingEntry>>(StateStoreName, key, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return (IReadOnlyList<PartyMemoryUnitMappingEntry>?)state ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Failed to read memory-unit mapping for state key {Key}; treating as empty. Cleanup may not find prior mappings.",
                key);
            return [];
        }
    }

    private static string BuildKey(string tenantId, string partyId)
        => $"parties:memory-mapping:{Uri.EscapeDataString(tenantId)}:{Uri.EscapeDataString(partyId)}";
}
