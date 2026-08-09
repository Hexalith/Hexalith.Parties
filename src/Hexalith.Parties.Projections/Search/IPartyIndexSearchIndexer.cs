using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Search;

/// <summary>
/// Hook invoked after a Party index entry is durably persisted or removed, so an optional
/// external search index (e.g. Hexalith.Memories) can be kept in sync. Implementations return
/// <see langword="true"/> only after the requested external state has converged. Cancellation
/// must propagate; other failures should be reported as <see langword="false"/>.
/// </summary>
public interface IPartyIndexSearchIndexer
{
    /// <summary>Notifies the indexer that <paramref name="entry"/> was just persisted.</summary>
    Task<bool> NotifyIndexedAsync(
        string tenantId,
        PartyIndexEntry entry,
        string eventType,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);

    /// <summary>Notifies the indexer that <paramref name="partyId"/> was removed from the index.</summary>
    Task<bool> NotifyRemovedAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken);
}

