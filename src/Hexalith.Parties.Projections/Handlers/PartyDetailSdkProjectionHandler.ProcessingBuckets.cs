using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Models;

namespace Hexalith.Parties.Projections.Handlers;

public sealed partial class PartyDetailSdkProjectionHandler
{
    /// <inheritdoc/>
    public async Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId, CancellationToken cancellationToken)
    {
        Validate(request, dispatchId);
#if !HEXALITH_EVENTSTORE_NONEXPIRING_RECOVERY
        return await ProjectInlineAsync(request, dispatchId, cancellationToken).ConfigureAwait(false);
#else
        if (request.Events.Length <= 64)
        {
            return await ProjectInlineAsync(request, dispatchId, cancellationToken).ConfigureAwait(false);
        }
        if (!await RecoverBucketDeliveryAsync(request, dispatchId, cancellationToken).ConfigureAwait(false))
            return DeliveryFailure("projection-recovery-incomplete");
        var detail = await readModelStore.GetAsync<PartyDetailSdkReadModel>(StoreName,
                    PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId), cancellationToken).ConfigureAwait(false);
        var processing = await readModelStore.GetAsync<PartyProcessingSdkReadModel>(StoreName,
            PartySdkReadModelAddresses.Processing(request.TenantId, request.AggregateId), cancellationToken).ConfigureAwait(false);
        string? failure = PartySdkProjectionFold.GetDeliveryFailureReason(request.Events,
            Math.Min(detail.Value?.LastSequenceNumber ?? long.MinValue, processing.Value?.LastSequenceNumber ?? long.MinValue));
        bool auditOnly = failure == PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason;
        if (failure is not null && !auditOnly)
        {
            return DeliveryFailure(failure);
        }
        DomainProjectionHandlerResult? result = null;
        int ordinal = 0;
        foreach (var events in request.Events.OrderBy(item => item.SequenceNumber).GroupBy(item => item.SequenceNumber).Select(group => group.First()).Chunk(64))
        {
            string partId = "part-" + Generation(dispatchId + ":" + (ordinal++).ToString(CultureInfo.InvariantCulture));
            var part = request with
            {
                Events = events
            };
            result = auditOnly
                ? await ProjectBucketsAsync(part, partId, cancellationToken, auditOnly: true).ConfigureAwait(false)
                : await ProjectInlineAsync(part, partId, cancellationToken).ConfigureAwait(false);
            if (auditOnly && result.Status == ProjectionDispatchStatus.Retryable && result.ReasonCode == failure)
            {
                continue;
            }
            if (result.Status is not ProjectionDispatchStatus.Completed and not ProjectionDispatchStatus.AlreadyCompleted)
            {
                return result;
            }
        }
        if (auditOnly)
        {
            return DeliveryFailure(failure!);
        }
        return result ?? DomainProjectionHandlerResult.Completed();
#endif
    }

    private async Task<DomainProjectionHandlerResult> ProjectBucketsAsync(ProjectionRequest request, string dispatchId, CancellationToken token, bool auditOnly = false)
    {
        string detailKey = PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId);
        string processingKey = PartySdkReadModelAddresses.Processing(request.TenantId, request.AggregateId);
        var scope = new ReadModelBatchScope(StoreName, request.TenantId, Domain, request.AggregateId, ProjectionType, dispatchId);
        scope.Validate();
        if (!await RecoverBucketDeliveryAsync(request, dispatchId, token).ConfigureAwait(false))
            return DeliveryFailure("projection-recovery-incomplete");
        var detail = await readModelStore.GetAsync<PartyDetailSdkReadModel>(StoreName, detailKey, token).ConfigureAwait(false);
        var processing = await readModelStore.GetAsync<PartyProcessingSdkReadModel>(StoreName, processingKey, token).ConfigureAwait(false);
        if ((detail.Value is null) != (processing.Value is null))
        {
            return DeliveryFailure(ProjectionRebuildRequiredReason);
        }
        PartyProcessingSdkReadModel prior = processing.Value ?? new();
        long oldestCheckpoint = Math.Min(detail.Value?.LastSequenceNumber ?? long.MinValue, prior.LastSequenceNumber);
        string? failure = auditOnly ? PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason
            : PartySdkProjectionFold.GetDeliveryFailureReason(request.Events, oldestCheckpoint);
        if (failure is not null && failure != PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason)
            return DeliveryFailure(failure);
        if (auditOnly) failure = PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason;
        if (processing.Value is not null && request.Events.All(item => item.SequenceNumber <= oldestCheckpoint))
            return DomainProjectionHandlerResult.AlreadyCompleted();
        bool migrating = prior.BucketGeneration is null;
        string generation = prior.BucketGeneration ?? Generation(dispatchId + ":" + processing.ETag);
        var reads = new Dictionary<long, ReadModelEntry<PartyProcessingBucket>>();
        if (!migrating)
        {
            foreach (long id in request.Events.Where(item => item.SequenceNumber > oldestCheckpoint)
                .Select(item => PartyProcessingBucket.Number(item.SequenceNumber)).Distinct().Order())
            {
                var entry = await readModelStore.GetAsync<PartyProcessingBucket>(StoreName,
                    PartySdkReadModelAddresses.ProcessingBucket(request.TenantId, request.AggregateId, generation, id), token).ConfigureAwait(false);
                if (entry.Value is null && prior.FirstBucket <= id && id <= prior.LastBucket)
                {
                    return DeliveryFailure("processing-bucket-unavailable");
                }
                reads.Add(id, entry);
            }
        }
        var loaded = migrating ? prior.Records : reads.Values.SelectMany(entry => entry.Value?.Records ?? []).ToArray();
        var folded = PartyProcessingActivityFold.Fold(request, prior with
        {
            Records = loaded
        }, _processingFoldLogger);
        if (failure is not null)
        {
            folded = folded with
            {
                LastSequenceNumber = prior.LastSequenceNumber,
                ProjectedAt = prior.ProjectedAt,
                ProjectionVersion = prior.ProjectionVersion
            };
        }
        if (failure is not null && processing.Value is not null && folded.Records.SequenceEqual(loaded)
            && folded.ErasureSequenceNumber == prior.ErasureSequenceNumber)
            return DeliveryFailure(failure);
        // Conditional manifests are immutable. A changed authoritative snapshot gets a distinct,
        // bounded identity; pending envelopes are recovered before these ETags are observed.
        if (failure is not null)
            scope = scope with { BatchId = "audit-" + Generation(dispatchId + ":" + detail.ETag + ":" + processing.ETag) };
        PartyDetailSdkReadModel nextDetail = failure is null ? Fold(request, detail.Value) : detail.Value ?? new();
        long total = migrating ? folded.Records.Count : prior.RecordCount + folded.Records.Count - loaded.Count;
        PartyProcessingSdkReadModel metadata = ToBucketMetadata(folded, generation, folded.Records, total);
        List<ReadModelBatchOperation> operations =
        [ReadModelBatchOperation.Write(detailKey, nextDetail, Concurrency(detail.ETag)),
            ReadModelBatchOperation.Write(processingKey, metadata, Concurrency(processing.ETag))];
        var buckets = BucketOperations(request, generation, folded.Records);
        if (migrating)
        {
#if HEXALITH_EVENTSTORE_NONEXPIRING_RECOVERY
            await ReadModelGenerationStaging.StageAsync(batchStore, scope, buckets, token).ConfigureAwait(false);
#else
            return DeliveryFailure("processing-bucket-migration-requires-published-sdk-generation-support");
#endif
        }
        else
        {
            foreach (var operation in buckets)
            {
                long id = long.Parse(operation.Key[(operation.Key.LastIndexOf('.') + 1)..], CultureInfo.InvariantCulture);
                operations.Add(operation.WithConcurrency(Concurrency(reads[id].ETag)));
            }
        }
        ReadModelBatchResult saved = await batchStore.ExecuteAsync(new ReadModelBatch(scope, operations) { RetainManifestForRecovery = true }, token).ConfigureAwait(false);
        return saved.IsSuccess && failure is not null ? DeliveryFailure(failure) : ReadModelBatchProjectionResultMapper.Map(saved);
    }

    private Task<bool> RecoverBucketDeliveryAsync(ProjectionRequest request, string dispatchId, CancellationToken token)
    {
        var scope = new ReadModelBatchScope(StoreName, request.TenantId, Domain, request.AggregateId, ProjectionType, dispatchId);
        var initialization = scope with { ProjectionType = ProjectionType + ".initialization", BatchId = "empty-v1" };
        initialization = initialization with { BatchId = "initialize-" + initialization.ComputeScopeHash() };
        return RecoverInitializationAsync(initialization, scope, PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId),
            PartySdkReadModelAddresses.Processing(request.TenantId, request.AggregateId), token);
    }

    private static ReadModelBatchConcurrency Concurrency(string? etag)
        => string.IsNullOrEmpty(etag) ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(etag);

    internal static string Generation(string operation)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(operation)));

    internal static ReadModelBatchOperation[] BucketOperations(ProjectionRequest request, string generation, IReadOnlyList<ProcessingActivityRecord> records)
        => records.GroupBy(record => PartyProcessingBucket.Number(record.SequenceNumber)).OrderBy(group => group.Key)
            .Select(group => ReadModelBatchOperation.Write(PartySdkReadModelAddresses.ProcessingBucket(request.TenantId, request.AggregateId, generation, group.Key),
                new PartyProcessingBucket { Records = group.OrderBy(record => record.SequenceNumber).ToArray() }, ReadModelBatchConcurrency.CreateOnly)).ToArray();

    internal static PartyProcessingSdkReadModel ToBucketMetadata(PartyProcessingSdkReadModel model, string generation, IReadOnlyList<ProcessingActivityRecord> records, long total)
    {
        long? first = records.Count == 0 ? model.FirstBucket : PartyProcessingBucket.Number(records.Min(record => record.SequenceNumber));
        long? last = records.Count == 0 ? model.LastBucket : PartyProcessingBucket.Number(records.Max(record => record.SequenceNumber));
        return model with
        {
            Records = [],
            BucketGeneration = generation,
            RecordCount = total,
            FirstRecordSequenceNumber = records.Count == 0 ? model.FirstRecordSequenceNumber
                : Math.Min(model.FirstRecordSequenceNumber ?? long.MaxValue, records.Min(record => record.SequenceNumber)),
            LastRecordSequenceNumber = records.Count == 0 ? model.LastRecordSequenceNumber
                : Math.Max(model.LastRecordSequenceNumber ?? long.MinValue, records.Max(record => record.SequenceNumber)),
            FirstBucket = model.FirstBucket is { } low && first is { } nextLow ? Math.Min(low, nextLow) : first,
            LastBucket = model.LastBucket is { } high && last is { } nextHigh ? Math.Max(high, nextHigh) : last
        };
    }
}
