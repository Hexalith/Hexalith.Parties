using Hexalith.Parties.Search;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Supplies a controlled mapping inventory for erasure cleanup tests.</summary>
/// <param name="inventory">The inventory read to await, or an empty inventory when omitted.</param>
internal sealed class ErasureCleanupMappingStore(
    Task<IReadOnlyList<PartyMemoryUnitMappingEntry>>? inventory = null) : IPartyMemoryUnitMappingStore
{
    /// <inheritdoc />
    public Task RecordMappingAsync(
        string tenantId,
        string partyId,
        string memoryUnitId,
        string sourceUri,
        string caseId,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
        => inventory ?? Task.FromResult<IReadOnlyList<PartyMemoryUnitMappingEntry>>([]);

    /// <inheritdoc />
    public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task<int> RemoveMappingsAsync(
        string tenantId,
        string partyId,
        IReadOnlyList<PartyMemoryUnitMappingEntry> cleanedEntries,
        CancellationToken cancellationToken)
        => Task.FromResult(0);
}
