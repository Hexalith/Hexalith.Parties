using Hexalith.Parties.Search;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Forwards to the real ledger while intercepting progress writes and read completion.</summary>
/// <param name="store">The real mapping store backed by serialized test state.</param>
/// <param name="remove">The controlled progress-persistence operation.</param>
/// <param name="afterRead">An optional cancellation boundary after reading the inventory.</param>
internal sealed class InterceptedPartyMemoryMappingStore(
    IPartyMemoryUnitMappingStore store,
    Func<string, string, IReadOnlyList<PartyMemoryUnitMappingEntry>, CancellationToken, Task<int>> remove,
    Action? afterRead = null) : IPartyMemoryUnitMappingStore
{
    /// <inheritdoc/>
    public Task RecordMappingAsync(string tenantId, string partyId, string memoryUnitId, string sourceUri,
        string caseId, CancellationToken cancellationToken) =>
        store.RecordMappingAsync(tenantId, partyId, memoryUnitId, sourceUri, caseId, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
        string tenantId, string partyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<PartyMemoryUnitMappingEntry> entries =
            await store.GetMappingsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        afterRead?.Invoke();
        return entries;
    }

    /// <inheritdoc/>
    public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken) =>
        store.ClearMappingsAsync(tenantId, partyId, cancellationToken);

    /// <inheritdoc/>
    public Task<int> RemoveMappingsAsync(string tenantId, string partyId,
        IReadOnlyList<PartyMemoryUnitMappingEntry> cleanedEntries, CancellationToken cancellationToken) =>
        remove(tenantId, partyId, cleanedEntries, cancellationToken);
}
