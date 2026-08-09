using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Search;

/// <summary>
/// Default indexer used when no external search backend is supplied directly to
/// <c>PartyIndexSdkProjectionHandler</c>.
/// </summary>
public sealed class NoOpPartyIndexSearchIndexer : IPartyIndexSearchIndexer
{
    /// <inheritdoc/>
    public Task<bool> NotifyIndexedAsync(
        string tenantId,
        PartyIndexEntry entry,
        string eventType,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken) => Task.FromResult(true);

    /// <inheritdoc/>
    public Task<bool> NotifyRemovedAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken) => Task.FromResult(true);
}

