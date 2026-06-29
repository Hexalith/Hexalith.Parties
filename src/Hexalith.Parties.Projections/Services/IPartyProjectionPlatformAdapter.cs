using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Services;

public interface IPartyProjectionPlatformAdapter
{
    Task<long> ReadDeliveredSequenceAsync(string tenantId, string partyId, CancellationToken cancellationToken);

    Task<bool> TrySaveDeliveredSequenceAsync(string tenantId, string partyId, long sequenceNumber, CancellationToken cancellationToken);

    Task<PartyProjectionRebuildCheckpoint?> ReadRebuildCheckpointAsync(
        PartyProjectionRebuildScope scope,
        CancellationToken cancellationToken);

    Task SaveRebuildCheckpointAsync(
        PartyProjectionRebuildScope scope,
        PartyProjectionRebuildCheckpoint checkpoint,
        CancellationToken cancellationToken);

    Task DeleteRebuildCheckpointAsync(PartyProjectionRebuildScope scope, CancellationToken cancellationToken);

    Task<bool> HasActiveRebuildAsync(string tenantId, CancellationToken cancellationToken);

    ProjectionFreshnessMetadata MapFreshness(
        PartyProjectionPlatformFreshness freshness,
        bool isRebuilding = false,
        bool stateStoreUnavailable = false,
        bool hasSafeCachedData = false);
}
