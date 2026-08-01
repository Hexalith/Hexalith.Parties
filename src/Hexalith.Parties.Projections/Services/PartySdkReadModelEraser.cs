using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Models;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Projections.Services;

/// <summary>Erases one Party from canonical SDK read models without deleting the shared tenant index.</summary>
public sealed class PartySdkReadModelEraser(
    IReadModelStore readModelStore,
    IReadModelConditionalEraser conditionalEraser,
    IOptions<PartySdkReadModelOptions> options)
{
    public async Task EraseAsync(string tenantId, string partyId, CancellationToken cancellationToken)
    {
        string storeName = options.Value.ReadModelStateStoreName;
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);

        string detailKey = PartySdkReadModelAddresses.Detail(tenantId, partyId);
        (bool present, string etag) = await conditionalEraser
            .TryReadEtagAsync(storeName, detailKey, cancellationToken)
            .ConfigureAwait(false);
        if (present && !await conditionalEraser
            .TryEraseAsync(storeName, detailKey, etag, cancellationToken)
            .ConfigureAwait(false))
        {
            throw new InvalidOperationException("Canonical detail erasure encountered an optimistic-concurrency conflict.");
        }

        await ReadModelWritePolicy.UpdateAsync<PartyIndexSdkReadModel>(
            readModelStore,
            storeName,
            PartySdkReadModelAddresses.Index(tenantId),
            current => RemoveParty(current, partyId),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    internal static PartyIndexSdkReadModel RemoveParty(PartyIndexSdkReadModel? current, string partyId)
    {
        var entries = new Dictionary<string, Hexalith.Parties.Contracts.Models.PartyIndexEntry>(
            current?.Entries ?? new Dictionary<string, Hexalith.Parties.Contracts.Models.PartyIndexEntry>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        _ = entries.Remove(partyId);
        return new PartyIndexSdkReadModel
        {
            Entries = entries,
            LastSequenceNumbers = current?.LastSequenceNumbers
                ?? new Dictionary<string, long>(StringComparer.Ordinal),
            ProjectedAt = current?.ProjectedAt,
            ProjectionVersion = current?.ProjectionVersion,
        };
    }
}
