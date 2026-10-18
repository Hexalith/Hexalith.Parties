namespace Hexalith.Parties.Search;

/// <summary>
/// Retains per-party Memories unit identities for resumable cleanup, across rebuilds.
/// </summary>
internal interface IPartyMemoryUnitMappingStore
{
    /// <summary>Records a successfully ingested unit in its captured case.</summary>
    Task RecordMappingAsync(
        string tenantId,
        string partyId,
        string memoryUnitId,
        string sourceUri,
        string caseId,
        CancellationToken cancellationToken);

    /// <summary>Reads the current mapping inventory; failures are never treated as empty.</summary>
    Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes a unique reserved health-probe inventory. Only the reserved probe tenant and
    /// GUID-suffixed, single-use probe party IDs are accepted; domain inventories must use
    /// <see cref="RemoveMappingsAsync"/> so their empty state retains its ETag generation.
    /// </summary>
    Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken);

    /// <summary>
    /// Subtracts exact successfully cleaned snapshot entries from the live inventory using
    /// bounded concurrency retries. Already-absent targets are idempotent. The final empty
    /// inventory is saved as an empty list to preserve its ETag generation across reuse;
    /// provider failures and exhausted conflicts propagate.
    /// </summary>
    /// <returns>The retained entry count at the successful mutation or no-op read.</returns>
    Task<int> RemoveMappingsAsync(
        string tenantId,
        string partyId,
        IReadOnlyList<PartyMemoryUnitMappingEntry> cleanedEntries,
        CancellationToken cancellationToken);
}
