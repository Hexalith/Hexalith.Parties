using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Projections.Services;

/// <summary>Erases one Party from canonical SDK read models without deleting the shared tenant index.</summary>
public sealed class PartySdkReadModelEraser(
    IReadModelStore readModelStore,
    IOptions<PartySdkReadModelOptions> options)
{
    public async Task EraseAsync(string tenantId, string partyId, CancellationToken cancellationToken)
    {
        string storeName = options.Value.ReadModelStateStoreName;
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);

        // Redact the canonical detail in place rather than deleting the row: a persisted,
        // PII-free tombstone (IsErased=true) is the architecture's stated invariant for an erased
        // party's detail reads, and matches what a full rebuild already produces when it replays
        // a PartyErased event through PartyDetailSdkProjectionHandler.Fold — keeping the eraser
        // and the rebuild path in agreement.
        await ReadModelWritePolicy.UpdateAsync<PartyDetailSdkReadModel>(
            readModelStore,
            storeName,
            PartySdkReadModelAddresses.Detail(tenantId, partyId),
            current => RedactDetail(current, partyId),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Reset the processing-activity checkpoint so a party recreated with the same id is not
        // silently skipped by the stale watermark, while preserving prior records as Art.30
        // processing-activity history (the records themselves are PII-free and are not erased).
        await ReadModelWritePolicy.UpdateAsync<PartyProcessingSdkReadModel>(
            readModelStore,
            storeName,
            PartySdkReadModelAddresses.Processing(tenantId, partyId),
            ResetProcessingCheckpoint,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await ReadModelWritePolicy.UpdateAsync<PartyIndexSdkReadModel>(
            readModelStore,
            storeName,
            PartySdkReadModelAddresses.Index(tenantId),
            current => RemoveParty(current, partyId),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    internal static PartyDetailSdkReadModel RedactDetail(PartyDetailSdkReadModel? current, string partyId)
        => current is null
            ? new PartyDetailSdkReadModel()
            : current with { Detail = PartyDetailProjectionHandler.ApplyErasure(partyId, current.Detail) };

    internal static PartyProcessingSdkReadModel ResetProcessingCheckpoint(PartyProcessingSdkReadModel? current)
        => current is null
            ? new PartyProcessingSdkReadModel()
            : current with { LastSequenceNumber = long.MinValue };

    internal static PartyIndexSdkReadModel RemoveParty(PartyIndexSdkReadModel? current, string partyId)
    {
        var entries = new Dictionary<string, Hexalith.Parties.Contracts.Models.PartyIndexEntry>(
            current?.Entries ?? new Dictionary<string, Hexalith.Parties.Contracts.Models.PartyIndexEntry>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        _ = entries.Remove(partyId);

        // Clear the companion sequence checkpoint too. Without this, recreating a party with the
        // same id leaves a stale watermark: events at or below it are silently skipped as
        // "already applied" by PartySdkProjectionFold.DeserializeNew, so the recreated party can
        // vanish from the tenant index until its own event count exceeds the old high-water mark.
        var sequences = new Dictionary<string, long>(
            current?.LastSequenceNumbers ?? new Dictionary<string, long>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        _ = sequences.Remove(partyId);

        return new PartyIndexSdkReadModel
        {
            Entries = entries,
            LastSequenceNumbers = sequences,
            ProjectedAt = current?.ProjectedAt,
            ProjectionVersion = current?.ProjectionVersion,
        };
    }
}
