using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Search;

/// <summary>
/// Default indexer used when no external search backend is configured (also the constructor
/// default for <c>PartyIndexSdkProjectionHandler</c> when Memories DI is disabled).
/// </summary>
public sealed class NoOpPartyIndexSearchIndexer : IPartyIndexSearchIndexer
{
    /// <inheritdoc/>
    public Task NotifyIndexedAsync(
        string tenantId,
        PartyIndexEntry entry,
        string eventType,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task NotifyRemovedAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

