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

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Projections.Handlers;

/// <summary>
/// Persists the canonical aggregate-owned Party detail and processing-activity read models.
/// </summary>
public sealed class PartyDetailSdkProjectionHandler(
    IReadModelStore readModelStore,
    IReadModelBatchStore batchStore,
    IOptions<PartySdkReadModelOptions> options) :
    IAsyncDomainProjectionRebuildHandler,
    IDeclaresProjectionReadModelSlots
{
    private const string UnresolvedOrUnsupportedEventReason = "unresolved-or-unsupported-event";

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
        PartyDetailSdkReadModel next = Fold(request, current.Value);
        PartyProcessingSdkReadModel nextProcessing = PartyProcessingActivityFold.Fold(request, currentProcessing.Value);
        if (current.Value is not null
            && next.LastSequenceNumber == current.Value.LastSequenceNumber
            && currentProcessing.Value is not null
            && nextProcessing.LastSequenceNumber == currentProcessing.Value.LastSequenceNumber)
        {
            long priorSequence = current.Value.LastSequenceNumber;
            if (request.Events.Any(item => item.SequenceNumber > priorSequence))
            {
                // New events were present but none advanced the checkpoint (unresolved / non-JSON).
                // AlreadyCompleted would stop retries while the events were never applied.
                return DomainProjectionHandlerResult.Failed(UnresolvedOrUnsupportedEventReason);
            }

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
        PartyDetailSdkReadModel candidate = Fold(request, current: null);
        PartyProcessingSdkReadModel processingCandidate = PartyProcessingActivityFold.Fold(request, current: null);
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

    internal static PartyDetailSdkReadModel Fold(ProjectionRequest request, PartyDetailSdkReadModel? current)
    {
        PartyDetail? detail = current?.Detail;
        long lastSequence = current?.LastSequenceNumber ?? long.MinValue;
        DateTimeOffset projectedAt = current?.ProjectedAt ?? DateTimeOffset.UnixEpoch;
        foreach ((ProjectionEventDto @event, IEventPayload? payload, bool advance) in
            PartySdkProjectionFold.DeserializeNew(request.Events, lastSequence))
        {
            if (payload is not null)
            {
                PartyDetail? applied = payload is PartyErased erased
                    ? PartyDetailProjectionHandler.ApplyErasure(request.AggregateId, detail) is { } redacted
                        ? redacted with
                        {
                            ErasedAt = erased.ErasedAt,
                            LastModifiedAt = erased.ErasedAt,
                        }
                        : null
                    : PartyDetailProjectionHandler.Apply(request.AggregateId, payload, detail);
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
