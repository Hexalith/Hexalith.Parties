using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Projections.Handlers;

/// <summary>
/// Persists the canonical aggregate-owned Party detail and processing-activity read models.
/// </summary>
public sealed class PartyDetailSdkProjectionHandler(
    IReadModelStore readModelStore,
    IReadModelBatchStore batchStore,
    IOptions<PartySdkReadModelOptions> options,
    ILogger<PartyDetailSdkProjectionHandler>? logger = null) :
    IAsyncDomainProjectionRebuildHandler,
    IDeclaresProjectionReadModelSlots
{
    public static IReadOnlyList<ProjectionReadModelSlotDeclaration> ProjectionReadModelSlots { get; } =
    [
        new("party", PartyProjectionNames.Detail, PartySdkReadModelAddresses.DetailSlot,
            ProjectionReadModelSlotKind.AggregateOwned, declaresCanonicalWriter: true),
        new("party", PartyProjectionNames.Detail, PartySdkReadModelAddresses.ProcessingSlot,
            ProjectionReadModelSlotKind.AggregateOwned, declaresCanonicalWriter: true),
    ];

    public string Domain => "party";

    public string ProjectionType => PartyProjectionNames.Detail;

    public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;

    public async Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken)
    {
        Validate(request, dispatchId);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Events.Length == 0)
        {
            return DomainProjectionHandlerResult.Completed();
        }

        string storeName = StoreName;
        string key = PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId);
        ReadModelEntry<PartyDetailSdkReadModel> current = await readModelStore
            .GetAsync<PartyDetailSdkReadModel>(storeName, key, cancellationToken)
            .ConfigureAwait(false);
        string processingKey = PartySdkReadModelAddresses.Processing(request.TenantId, request.AggregateId);
        ReadModelEntry<PartyProcessingSdkReadModel> currentProcessing = await readModelStore
            .GetAsync<PartyProcessingSdkReadModel>(storeName, processingKey, cancellationToken)
            .ConfigureAwait(false);
        // When only one of the detail/processing slots exists, do not Min() the present
        // watermark with long.MinValue for the missing slot — that invents a false
        // delivery-sequence-gap on the next contiguous event and stalls ProjectAsync.
        long oldestCheckpoint;
        if (current.Value is null && currentProcessing.Value is not null)
        {
            oldestCheckpoint = currentProcessing.Value.LastSequenceNumber;
        }
        else if (current.Value is not null && currentProcessing.Value is null)
        {
            oldestCheckpoint = current.Value.LastSequenceNumber;
        }
        else
        {
            oldestCheckpoint = Math.Min(
                current.Value?.LastSequenceNumber ?? long.MinValue,
                currentProcessing.Value?.LastSequenceNumber ?? long.MinValue);
        }
        string? deliveryFailure = PartySdkProjectionFold.GetDeliveryFailureReason(request.Events, oldestCheckpoint);
        if (deliveryFailure is not null)
        {
            if (string.Equals(
                deliveryFailure,
                PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason,
                StringComparison.Ordinal))
            {
                try
                {
                    await RecordUnresolvedProcessingAsync(request, storeName, processingKey, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    // The audit-record write lost the optimistic-concurrency race (retry-budget
                    // exhausted). The delivery is still retryable — a future attempt retries both
                    // the audit write and the main fold, so this must not crash dispatch.
                }
            }

            return DeliveryFailure(deliveryFailure);
        }

        // Fold (Detail slot) stays silent by design — see its own XML doc for why the Processing
        // fold below is the sole logger carrier for this handler.
        PartyDetailSdkReadModel next = Fold(request, current.Value);
        PartyProcessingSdkReadModel nextProcessing = PartyProcessingActivityFold.Fold(request, currentProcessing.Value, logger);
        if (current.Value is not null
            && next.LastSequenceNumber == current.Value.LastSequenceNumber
            && currentProcessing.Value is not null
            && nextProcessing.LastSequenceNumber == currentProcessing.Value.LastSequenceNumber)
        {
            return DomainProjectionHandlerResult.AlreadyCompleted();
        }

        ReadModelBatchConcurrency concurrency = current.ETag is { Length: > 0 } etag
            ? ReadModelBatchConcurrency.Match(etag)
            : ReadModelBatchConcurrency.CreateOnly;
        ReadModelBatchConcurrency processingConcurrency = currentProcessing.ETag is { Length: > 0 } processingEtag
            ? ReadModelBatchConcurrency.Match(processingEtag)
            : ReadModelBatchConcurrency.CreateOnly;
        var batch = new ReadModelBatch(
            new ReadModelBatchScope(storeName, request.TenantId, Domain, request.AggregateId, ProjectionType, dispatchId),
            [
                ReadModelBatchOperation.Write(key, next, concurrency),
                ReadModelBatchOperation.Write(processingKey, nextProcessing, processingConcurrency),
            ]);

        ReadModelBatchResult result = await batchStore.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        return ReadModelBatchProjectionResultMapper.Map(result);
    }

    public Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(
        ProjectionRequest request,
        string operationId,
        CancellationToken cancellationToken)
    {
        Validate(request, operationId);
        cancellationToken.ThrowIfCancellationRequested();
        string? deliveryFailure = PartySdkProjectionFold.GetDeliveryFailureReason(request.Events, long.MinValue);
        if (deliveryFailure is not null)
        {
            throw new InvalidOperationException(deliveryFailure);
        }

        // Fold (Detail slot) stays silent by design here too — same invariant as ProjectAsync.
        PartyDetailSdkReadModel candidate = Fold(request, current: null);
        PartyProcessingSdkReadModel processingCandidate = PartyProcessingActivityFold.Fold(request, current: null, logger);
        string key = PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId);
        return Task.FromResult(new DomainProjectionRebuildPlan(
            StoreName,
            [
                ReadModelBatchOperation.Write(key, candidate, ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write(
                    PartySdkReadModelAddresses.Processing(request.TenantId, request.AggregateId),
                    processingCandidate,
                    ReadModelBatchConcurrency.LastWrite),
            ]));
    }

    /// <summary>
    /// Folds new events into the Detail read model. Deliberately does not pass a logger to
    /// <see cref="PartySdkProjectionFold.DeserializeNew"/>: this handler's Processing-slot fold
    /// (<see cref="PartyProcessingActivityFold.Fold"/>) is the sole logger carrier for the whole
    /// Detail-slot handler, because every code path that calls this method
    /// (<see cref="ProjectAsync"/>, <see cref="PrepareRebuildAsync"/>) also calls
    /// <see cref="PartyProcessingActivityFold.Fold"/> over the exact same <c>request.Events</c> in
    /// the same call. That invariant depends on the Detail and Processing checkpoints staying in
    /// lockstep (both slots are aggregate-owned and advance from the same event stream); if a
    /// future change ever lets one slot's fold run without the other for the same delivery, this
    /// silence would start dropping diagnostics instead of merely avoiding a duplicate.
    /// </summary>
    internal static PartyDetailSdkReadModel Fold(ProjectionRequest request, PartyDetailSdkReadModel? current)
    {
        PartyDetail? detail = current?.Detail;
        long lastSequence = current?.LastSequenceNumber ?? long.MinValue;
        long? erasureSequence = current?.ErasureSequenceNumber;
        DateTimeOffset? erasedAt = current?.ErasedAt;
        DateTimeOffset projectedAt = current?.ProjectedAt ?? DateTimeOffset.UnixEpoch;
        foreach ((ProjectionEventDto @event, IEventPayload? payload, bool advance) in
            PartySdkProjectionFold.DeserializeNew(request.Events, lastSequence))
        {
            if (payload is PartyErased erased)
            {
                detail = PartyDetailProjectionHandler.ApplyErasure(request.AggregateId, detail) is { } redacted
                        ? redacted with
                        {
                            ErasedAt = erased.ErasedAt,
                            LastModifiedAt = erased.ErasedAt,
                        }
                        : null;
                erasureSequence = Math.Max(erasureSequence ?? long.MinValue, @event.SequenceNumber);
                erasedAt = erased.ErasedAt;
            }
            else if (payload is not null && erasureSequence is null)
            {
                PartyDetail? applied = PartyDetailProjectionHandler.Apply(request.AggregateId, payload, detail);
                if (applied is not null && !ReferenceEquals(applied, detail))
                {
                    applied = NormalizeEventTimestamps(applied, detail, @event.Timestamp.ToUniversalTime());
                }

                detail = applied ?? detail;
            }

            if (advance)
            {
                lastSequence = Math.Max(lastSequence, @event.SequenceNumber);
                projectedAt = PartySdkProjectionFold.ProjectedAt([@event], projectedAt);
            }
        }

        return new PartyDetailSdkReadModel
        {
            Detail = detail,
            LastSequenceNumber = lastSequence,
            ErasureSequenceNumber = erasureSequence,
            ErasedAt = erasedAt,
            ProjectedAt = projectedAt,
            ProjectionVersion = lastSequence == long.MinValue ? null : lastSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    private string StoreName
    {
        get
        {
            string value = options.Value.ReadModelStateStoreName;
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            return value;
        }
    }

    private async Task RecordUnresolvedProcessingAsync(
        ProjectionRequest request,
        string storeName,
        string processingKey,
        CancellationToken cancellationToken)
    {
        // ReadModelWritePolicy.UpdateAsync re-invokes this callback on every optimistic-concurrency
        // retry with a fresh `current` snapshot; request.Events never changes across retries, so
        // logging on every attempt would repeat the same diagnostic under writer contention. Log at
        // most once per delivery by only passing the real logger on the first attempt.
        bool loggedThisDelivery = false;
        await ReadModelWritePolicy.UpdateAsync<PartyProcessingSdkReadModel>(
            readModelStore,
            storeName,
            processingKey,
            current =>
            {
                PartyProcessingSdkReadModel next = PartyProcessingActivityFold.Fold(request, current, loggedThisDelivery ? null : logger);
                loggedThisDelivery = true;
                return next;
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static DomainProjectionHandlerResult DeliveryFailure(string reason)
        => DomainProjectionHandlerResult.Retryable(reason);

    private static PartyDetail NormalizeEventTimestamps(
        PartyDetail applied,
        PartyDetail? previous,
        DateTimeOffset eventTimestamp)
    {
        IReadOnlyList<NameHistoryEntry> nameHistory = applied.NameHistory;
        int existingHistoryCount = previous?.NameHistory.Count ?? 0;
        if (nameHistory.Count > existingHistoryCount)
        {
            NameHistoryEntry[] normalized = nameHistory.ToArray();
            for (int index = existingHistoryCount; index < normalized.Length; index++)
            {
                normalized[index] = normalized[index] with { ChangedAt = eventTimestamp };
            }

            nameHistory = normalized;
        }

        return applied with
        {
            NameHistory = nameHistory,
            CreatedAt = previous?.CreatedAt ?? eventTimestamp,
            LastModifiedAt = eventTimestamp,
        };
    }

    private static void Validate(ProjectionRequest request, string operationId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (!string.Equals(request.Domain, "party", StringComparison.Ordinal))
        {
            throw new ArgumentException("Projection request domain is not supported.", nameof(request));
        }

        _ = PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId);
    }
}
