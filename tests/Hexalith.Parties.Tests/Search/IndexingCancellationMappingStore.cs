using Hexalith.Parties.Search;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Controls the mapping acknowledgement boundary without allowing inventory deletion.</summary>
/// <param name="record">The mapping persistence and acknowledgement operation.</param>
internal sealed class IndexingCancellationMappingStore(
    Func<string, string, string, string, string, CancellationToken, Task> record) : IPartyMemoryUnitMappingStore
{
    /// <summary>Gets the number of mapping attempts.</summary>
    internal int RecordCount { get; private set; }

    /// <inheritdoc/>
    public Task RecordMappingAsync(string tenantId, string partyId, string memoryUnitId, string sourceUri,
        string caseId, CancellationToken cancellationToken)
    {
        RecordCount++;
        return record(tenantId, partyId, memoryUnitId, sourceUri, caseId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
        string tenantId, string partyId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Indexing compensation must not read the mapping inventory.");

    /// <inheritdoc/>
    public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Indexing compensation must preserve the mapping inventory.");

    /// <inheritdoc/>
    public Task<int> RemoveMappingsAsync(string tenantId, string partyId,
        IReadOnlyList<PartyMemoryUnitMappingEntry> cleanedEntries, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Indexing compensation must preserve the mapping inventory.");
}
