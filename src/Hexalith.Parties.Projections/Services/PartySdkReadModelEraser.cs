using System.Security.Cryptography;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Search;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Projections.Services;

/// <summary>
/// Redacts one Party across the three canonical SDK read models in a coordinated same-store batch.
/// </summary>
public sealed class PartySdkReadModelEraser(
    IReadModelStore readModelStore,
    IReadModelBatchStore batchStore,
    IOptions<PartySdkReadModelOptions> options,
    IPartyIndexSearchIndexer? searchIndexer = null,
    TimeProvider? timeProvider = null)
{
    private const int MaxOptimisticAttempts = 3;
    private const int MaxIncompleteResumes = 2;
    private readonly IPartyIndexSearchIndexer _searchIndexer = searchIndexer ?? new NoOpPartyIndexSearchIndexer();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>Redacts the Party read models and retains permanent anti-resurrection tombstones.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="partyId">The Party aggregate identifier.</param>
    /// <param name="cancellationToken">A token that cancels the cleanup.</param>
    /// <returns>A task that completes only after the coordinated store batch is durably proven.</returns>
    public async Task EraseAsync(string tenantId, string partyId, CancellationToken cancellationToken)
    {
        string storeName = options.Value.ReadModelStateStoreName;
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        string detailKey = PartySdkReadModelAddresses.Detail(tenantId, partyId);
        string processingKey = PartySdkReadModelAddresses.Processing(tenantId, partyId);
        string indexKey = PartySdkReadModelAddresses.Index(tenantId);
        DateTimeOffset cleanupAt = _timeProvider.GetUtcNow();
        string invocationId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        for (int attempt = 0; attempt < MaxOptimisticAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadModelEntry<PartyDetailSdkReadModel> detail = await readModelStore
                .GetAsync<PartyDetailSdkReadModel>(storeName, detailKey, cancellationToken)
                .ConfigureAwait(false);
            ReadModelEntry<PartyProcessingSdkReadModel> processing = await readModelStore
                .GetAsync<PartyProcessingSdkReadModel>(storeName, processingKey, cancellationToken)
                .ConfigureAwait(false);
            ReadModelEntry<PartyIndexSdkReadModel> index = await readModelStore
                .GetAsync<PartyIndexSdkReadModel>(storeName, indexKey, cancellationToken)
                .ConfigureAwait(false);

            var batch = new ReadModelBatch(
                new ReadModelBatchScope(
                    storeName,
                    tenantId,
                    "party",
                    partyId,
                    "party-erasure",
                    $"{invocationId}-{attempt}"),
                [
                    ReadModelBatchOperation.Write(
                        detailKey,
                        RedactDetail(detail.Value, partyId, cleanupAt),
                        Concurrency(detail.ETag)),
                    ReadModelBatchOperation.Write(
                        processingKey,
                        ResetProcessingCheckpoint(processing.Value, cleanupAt),
                        Concurrency(processing.ETag)),
                    ReadModelBatchOperation.Write(
                        indexKey,
                        RemoveParty(index.Value, partyId, cleanupAt),
                        Concurrency(index.ETag)),
                ]);

            ReadModelBatchResult result = await ExecuteWithResumeAsync(batch, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                await NotifySearchRemovalAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (result.Status is ReadModelBatchStatus.Conflict
                && result.ConflictKind is ReadModelBatchConflictKind.Optimistic)
            {
                continue;
            }

            throw new InvalidOperationException("sdk-read-model-cleanup-failed");
        }

        throw new InvalidOperationException("sdk-read-model-cleanup-conflict");
    }

    internal static PartyDetailSdkReadModel RedactDetail(
        PartyDetailSdkReadModel? current,
        string partyId,
        DateTimeOffset? cleanupAt = null)
    {
        DateTimeOffset requestedAt = cleanupAt ?? DateTimeOffset.UtcNow;
        DateTimeOffset effectiveAt = current?.ErasedAt ?? requestedAt;
        long retainedSequence = Math.Max(
            current?.ErasureSequenceNumber ?? long.MinValue,
            current?.LastSequenceNumber ?? long.MinValue);
        return new PartyDetailSdkReadModel
        {
            Detail = PartyDetailProjectionHandler.ApplyErasure(partyId, current?.Detail) is { } redacted
                ? redacted with { ErasedAt = effectiveAt, LastModifiedAt = effectiveAt }
                : null,
            LastSequenceNumber = current?.LastSequenceNumber ?? long.MinValue,
            ErasureSequenceNumber = retainedSequence,
            ErasedAt = effectiveAt,
            ProjectedAt = current?.ErasureSequenceNumber is not null
                ? current.ProjectedAt
                : Max(current?.ProjectedAt, requestedAt),
            ProjectionVersion = current?.ProjectionVersion,
        };
    }

    internal static PartyProcessingSdkReadModel ResetProcessingCheckpoint(
        PartyProcessingSdkReadModel? current,
        DateTimeOffset? cleanupAt = null)
    {
        DateTimeOffset requestedAt = cleanupAt ?? DateTimeOffset.UtcNow;
        DateTimeOffset effectiveAt = current?.ErasedAt ?? requestedAt;
        return new PartyProcessingSdkReadModel
        {
            Records = current?.Records ?? [],
            LastSequenceNumber = current?.LastSequenceNumber ?? long.MinValue,
            ErasureSequenceNumber = Math.Max(
                current?.ErasureSequenceNumber ?? long.MinValue,
                current?.LastSequenceNumber ?? long.MinValue),
            ErasedAt = effectiveAt,
            ProjectedAt = current?.ErasureSequenceNumber is not null
                ? current.ProjectedAt
                : Max(current?.ProjectedAt, requestedAt),
            ProjectionVersion = current?.ProjectionVersion,
        };
    }

    internal static PartyIndexSdkReadModel RemoveParty(
        PartyIndexSdkReadModel? current,
        string partyId,
        DateTimeOffset? cleanupAt = null)
    {
        DateTimeOffset requestedAt = cleanupAt ?? DateTimeOffset.UtcNow;
        var entries = new Dictionary<string, Hexalith.Parties.Contracts.Models.PartyIndexEntry>(
            current?.Entries ?? new Dictionary<string, Hexalith.Parties.Contracts.Models.PartyIndexEntry>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        _ = entries.Remove(partyId);
        var sequences = new Dictionary<string, long>(
            current?.LastSequenceNumbers ?? new Dictionary<string, long>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var erasureSequences = new Dictionary<string, long>(
            current?.ErasureSequenceNumbers ?? new Dictionary<string, long>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        bool alreadyErased = erasureSequences.ContainsKey(partyId);
        erasureSequences[partyId] = Math.Max(
            erasureSequences.GetValueOrDefault(partyId, long.MinValue),
            sequences.GetValueOrDefault(partyId, long.MinValue));

        return new PartyIndexSdkReadModel
        {
            Entries = entries,
            LastSequenceNumbers = sequences,
            ErasureSequenceNumbers = erasureSequences,
            ProjectedAt = alreadyErased ? current?.ProjectedAt : Max(current?.ProjectedAt, requestedAt),
            ProjectionVersion = current?.ProjectionVersion,
        };
    }

    private static ReadModelBatchConcurrency Concurrency(string? etag)
        => string.IsNullOrEmpty(etag)
            ? ReadModelBatchConcurrency.CreateOnly
            : ReadModelBatchConcurrency.Match(etag);

    private static DateTimeOffset Max(DateTimeOffset? left, DateTimeOffset right)
        => left is { } value && value >= right ? value : right;

    private async Task<ReadModelBatchResult> ExecuteWithResumeAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken)
    {
        ReadModelBatchResult result = await batchStore.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        for (int resume = 0;
            resume < MaxIncompleteResumes && result.Status is ReadModelBatchStatus.Incomplete;
            resume++)
        {
            result = await batchStore.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task NotifySearchRemovalAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
    {
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
            // External search cleanup is best effort after the canonical batch commits.
        }
    }
}
