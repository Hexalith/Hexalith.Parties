using Hexalith.Parties.Search;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Retains scoped mapping inventory across the two independent test adapters.</summary>
internal sealed class PartySearchErasureMappingStore : IPartyMemoryUnitMappingStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string Tenant, string Party), List<PartyMemoryUnitMappingEntry>> _entries = [];

    /// <summary>Gets the number of recorded mappings.</summary>
    public int RecordCount { get; private set; }

    /// <summary>Gets the number of completed mapping removal calls.</summary>
    public int RemovalCount { get; private set; }

    /// <inheritdoc/>
    public Task RecordMappingAsync(string tenantId, string partyId, string memoryUnitId, string sourceUri, string caseId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_entries.TryGetValue((tenantId, partyId), out List<PartyMemoryUnitMappingEntry>? current))
            {
                current = [];
                _entries[(tenantId, partyId)] = current;
            }

            current.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri, caseId));
            RecordCount++;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PartyMemoryUnitMappingEntry>>(
                _entries.TryGetValue((tenantId, partyId), out List<PartyMemoryUnitMappingEntry>? current) ? [.. current] : []);
        }
    }

    /// <inheritdoc/>
    public Task<int> RemoveMappingsAsync(string tenantId, string partyId, IReadOnlyList<PartyMemoryUnitMappingEntry> cleanedEntries, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RemovalCount++;
            if (!_entries.TryGetValue((tenantId, partyId), out List<PartyMemoryUnitMappingEntry>? current))
            {
                return Task.FromResult(0);
            }

            current.RemoveAll(cleanedEntries.Contains);
            return Task.FromResult(current.Count);
        }
    }

    /// <inheritdoc/>
    public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Domain cleanup must subtract exact mappings.");
}
