using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Search;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Projections.Services;

/// <summary>
/// Erases one Party from canonical SDK read models without deleting the shared tenant index row.
/// Removes the party's index entry and sequence checkpoint; the tenant index document itself remains.
/// </summary>
public sealed class PartySdkReadModelEraser(
    IReadModelStore readModelStore,
    IOptions<PartySdkReadModelOptions> options,
    IPartyIndexSearchIndexer? searchIndexer = null)
{
    private readonly IPartyIndexSearchIndexer _searchIndexer = searchIndexer ?? new NoOpPartyIndexSearchIndexer();

    public async Task EraseAsync(string tenantId, string partyId, CancellationToken cancellationToken)
    {
        string storeName = options.Value.ReadModelStateStoreName;
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);

        // When a detail row already exists, redact it in place to a PII-free tombstone
        // (IsErased=true), matching what a full rebuild produces when it replays PartyErased
        // through PartyDetailSdkProjectionHandler.Fold. When no detail row exists (or Detail is
        // null), leave Detail null — do not invent a tombstone. Authoritative erasure status
        // remains IPartyErasureRecordStore; the detail read model is only a secondary hint when
        // a prior projected row existed to redact. Also reset the detail sequence watermark so a
        // party recreated with the same id is not skipped by DeserializeNew.
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

        // Best-effort external search removal (Memories). Implementations must not throw; still
        // guard so a buggy adapter cannot fail GDPR erasure cleanup.
        try
        {
            await _searchIndexer.NotifyRemovedAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Best effort.
        }
    }

    internal static PartyDetailSdkReadModel RedactDetail(PartyDetailSdkReadModel? current, string partyId)
    {
        if (current is null)
        {
            return new PartyDetailSdkReadModel();
        }

        return current with
        {
            Detail = PartyDetailProjectionHandler.ApplyErasure(partyId, current.Detail),
            LastSequenceNumber = long.MinValue,
            ProjectionVersion = null,
        };
    }

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
