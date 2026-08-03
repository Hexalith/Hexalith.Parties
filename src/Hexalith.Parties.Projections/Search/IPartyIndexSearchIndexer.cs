using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Search;

/// <summary>
/// Best-effort hook invoked after a Party index entry is durably persisted, so an optional
/// external search index (e.g. Hexalith.Memories) can be kept in sync. Implementations must
/// never throw — indexing is not required for local search, so a failure here must not block
/// or fail the projection write it is notified from.
/// </summary>
public interface IPartyIndexSearchIndexer
{
    /// <summary>Notifies the indexer that <paramref name="entry"/> was just persisted.</summary>
    Task NotifyIndexedAsync(
        string tenantId,
        PartyIndexEntry entry,
        string eventType,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);
}

/// <summary>Default indexer used when no external search backend is configured.</summary>
public sealed class NoOpPartyIndexSearchIndexer : IPartyIndexSearchIndexer
{
    /// <inheritdoc/>
    public Task NotifyIndexedAsync(
        string tenantId,
        PartyIndexEntry entry,
        string eventType,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
