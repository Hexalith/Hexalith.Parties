using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Search;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Projections.Handlers;

/// <summary>Persists the canonical shared Party tenant index with optimistic merge-on-write.</summary>
public sealed class PartyIndexSdkProjectionHandler(
    IReadModelStore readModelStore,
    IOptions<PartySdkReadModelOptions> options,
    IPartyIndexSearchIndexer? searchIndexer = null) :
    IAsyncDomainSharedProjectionRebuildHandler,
    IDeclaresProjectionReadModelSlots
{
    private readonly IPartyIndexSearchIndexer _searchIndexer = searchIndexer ?? new NoOpPartyIndexSearchIndexer();

    private static readonly JsonSerializerOptions s_candidateJsonOptions = PartiesJsonOptions.Default;

    public static IReadOnlyList<ProjectionReadModelSlotDeclaration> ProjectionReadModelSlots { get; } =
    [
        new("party", PartyProjectionNames.Index, PartySdkReadModelAddresses.IndexSlot,
            ProjectionReadModelSlotKind.Shared, declaresCanonicalWriter: true),
    ];

    public string Domain => "party";

    public string ProjectionType => PartyProjectionNames.Index;

    public string RebuildStoreName => StoreName;

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

        string indexKey = PartySdkReadModelAddresses.Index(request.TenantId);
        ReadModelEntry<PartyIndexSdkReadModel> currentEntry = await readModelStore
            .GetAsync<PartyIndexSdkReadModel>(StoreName, indexKey, cancellationToken)
            .ConfigureAwait(false);
        PartyIndexFoldResult foldResult = FoldCore(request, currentEntry.Value);
        if (foldResult.FailureReason is not null)
        {
            return DeliveryFailure(foldResult.FailureReason);
        }

        PartyIndexSdkReadModel folded = foldResult.Model;

        if (IsIdempotentNoOp(currentEntry.Value, folded, request.AggregateId))
        {
            return DomainProjectionHandlerResult.AlreadyCompleted();
        }

        PartyIndexFoldResult persistedFold = foldResult;
        try
        {
            await ReadModelWritePolicy.UpdateAsync<PartyIndexSdkReadModel>(
                readModelStore,
                StoreName,
                indexKey,
                current =>
                {
                    // Revalidate against every optimistic retry snapshot before producing a
                    // candidate. A concurrent writer can expose a cross-delivery gap or make an
                    // event unresolved relative to the current aggregate checkpoint.
                    persistedFold = FoldCore(request, current);
                    if (persistedFold.FailureReason is not null)
                    {
                        throw new PartyProjectionDeliveryException(persistedFold.FailureReason);
                    }

                    return persistedFold.Model;
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (PartyProjectionDeliveryException exception)
        {
            return DeliveryFailure(exception.Message);
        }

        await NotifySearchIndexerAsync(request, persistedFold, cancellationToken).ConfigureAwait(false);

        return DomainProjectionHandlerResult.Completed();
    }

    private async Task NotifySearchIndexerAsync(
        ProjectionRequest request,
        PartyIndexFoldResult foldResult,
        CancellationToken cancellationToken)
    {
        // Indexing an external search backend (e.g. Hexalith.Memories) is best effort: it must
        // never fail or block the projection write it follows.
        try
        {
            if (foldResult.Removed)
            {
                await _searchIndexer
                    .NotifyRemovedAsync(request.TenantId, request.AggregateId, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (!foldResult.Model.Entries.TryGetValue(request.AggregateId, out PartyIndexEntry? entry)
                || entry is null
                || foldResult.LastIndexedEvent is null)
            {
                return;
            }

            await _searchIndexer
                .NotifyIndexedAsync(
                    request.TenantId,
                    entry,
                    foldResult.LastIndexedEvent.EventTypeName,
                    foldResult.LastIndexedEvent.Timestamp,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Best effort per IPartyIndexSearchIndexer's contract: swallow and move on.
        }
    }

    public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ToCandidate(new PartyIndexSdkReadModel()));
    }

    public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        Validate(aggregateHistory, identity.OperationId);
        if (!string.Equals(identity.TenantId, aggregateHistory.TenantId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Projection request tenant does not match the shared rebuild identity.", nameof(aggregateHistory));
        }

        cancellationToken.ThrowIfCancellationRequested();
        PartyIndexSdkReadModel current = FromCandidate(candidate);
        if (aggregateHistory.Events.Length == 0)
        {
            return Task.FromResult(ToCandidate(current));
        }

        PartyIndexFoldResult foldResult = FoldCore(aggregateHistory, current);
        if (foldResult.FailureReason is not null)
        {
            throw new InvalidOperationException(foldResult.FailureReason);
        }

        return Task.FromResult(ToCandidate(foldResult.Model));
    }

    public Task<DomainProjectionRebuildPlan> FinalizeAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();
        PartyIndexSdkReadModel rebuiltIndex = FromCandidate(candidate);
        return Task.FromResult(new DomainProjectionRebuildPlan(
            RebuildStoreName,
            [ReadModelBatchOperation.Write(
                PartySdkReadModelAddresses.Index(identity.TenantId),
                rebuiltIndex,
                ReadModelBatchConcurrency.LastWrite)]));
    }

    internal static PartyIndexSdkReadModel Fold(ProjectionRequest request, PartyIndexSdkReadModel? current)
        => FoldCore(request, current).Model;

    private static PartyIndexFoldResult FoldCore(ProjectionRequest request, PartyIndexSdkReadModel? current)
    {
        var entries = new Dictionary<string, PartyIndexEntry>(
            current?.Entries ?? new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var sequences = new Dictionary<string, long>(
            current?.LastSequenceNumbers ?? new Dictionary<string, long>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var erasureSequences = new Dictionary<string, long>(
            current?.ErasureSequenceNumbers ?? new Dictionary<string, long>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        long lastSequence = sequences.GetValueOrDefault(request.AggregateId, long.MinValue);
        string? failureReason = PartySdkProjectionFold.GetDeliveryFailureReason(request.Events, lastSequence);
        if (failureReason is not null)
        {
            return new PartyIndexFoldResult(current ?? new PartyIndexSdkReadModel(), null, false, failureReason);
        }

        bool isErased = erasureSequences.ContainsKey(request.AggregateId);
        DateTimeOffset projectedAt = current?.ProjectedAt ?? DateTimeOffset.UnixEpoch;
        entries.TryGetValue(request.AggregateId, out PartyIndexEntry? entry);
        bool hadEntry = entry is not null;
        ProjectionEventDto? lastIndexedEvent = null;

        foreach ((ProjectionEventDto @event, IEventPayload? payload, bool advance) in
            PartySdkProjectionFold.DeserializeNew(request.Events, lastSequence))
        {
            if (payload is not null)
            {
                if (payload is PartyErased)
                {
                    _ = entries.Remove(request.AggregateId);
                    entry = null;
                    isErased = true;
                    erasureSequences[request.AggregateId] = Math.Max(
                        erasureSequences.GetValueOrDefault(request.AggregateId, long.MinValue),
                        @event.SequenceNumber);
                    lastIndexedEvent = null;
                }
                else if (!isErased)
                {
                    PartyIndexEntry? applied = PartyIndexProjectionHandler.Apply(request.AggregateId, payload, entry);
                    if (applied is not null)
                    {
                        if (!ReferenceEquals(applied, entry))
                        {
                            DateTimeOffset eventTimestamp = @event.Timestamp.ToUniversalTime();
                            applied = applied with
                            {
                                CreatedAt = entry?.CreatedAt ?? eventTimestamp,
                                LastModifiedAt = eventTimestamp,
                            };
                        }

                        entry = applied;
                        entries[request.AggregateId] = applied;
                        lastIndexedEvent = @event;
                    }
                }
            }

            if (advance)
            {
                lastSequence = Math.Max(lastSequence, @event.SequenceNumber);
                projectedAt = PartySdkProjectionFold.ProjectedAt([@event], projectedAt);
            }
        }

        if (lastSequence != long.MinValue)
        {
            sequences[request.AggregateId] = lastSequence;
        }

        long maxGlobalPosition = Math.Max(
            request.Events.Max(static item => item.GlobalPosition),
            ParseGlobalPosition(current?.ProjectionVersion));
        string? version = maxGlobalPosition > 0
            ? $"global:{maxGlobalPosition.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : lastSequence == long.MinValue
                ? current?.ProjectionVersion
                : $"{request.AggregateId}:{lastSequence.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var model = new PartyIndexSdkReadModel
        {
            Entries = entries,
            LastSequenceNumbers = sequences,
            ErasureSequenceNumbers = erasureSequences,
            ProjectedAt = projectedAt,
            ProjectionVersion = version,
        };
        bool removed = hadEntry && entry is null;
        return new PartyIndexFoldResult(model, lastIndexedEvent, removed, null);
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

    private static DomainProjectionHandlerResult DeliveryFailure(string reason)
        => string.Equals(reason, PartySdkProjectionFold.DeliverySequenceGapReason, StringComparison.Ordinal)
            ? DomainProjectionHandlerResult.Retryable(reason)
            : DomainProjectionHandlerResult.Failed(reason);

    private static bool IsIdempotentNoOp(
        PartyIndexSdkReadModel? current,
        PartyIndexSdkReadModel folded,
        string aggregateId)
    {
        if (current is null)
        {
            return false;
        }

        long priorSequence = current.LastSequenceNumbers.GetValueOrDefault(aggregateId, long.MinValue);
        long nextSequence = folded.LastSequenceNumbers.GetValueOrDefault(aggregateId, long.MinValue);
        if (priorSequence != nextSequence)
        {
            return false;
        }

        long priorErasure = current.ErasureSequenceNumbers.GetValueOrDefault(aggregateId, long.MinValue);
        long nextErasure = folded.ErasureSequenceNumbers.GetValueOrDefault(aggregateId, long.MinValue);
        if (priorErasure != nextErasure)
        {
            return false;
        }

        current.Entries.TryGetValue(aggregateId, out PartyIndexEntry? priorEntry);
        folded.Entries.TryGetValue(aggregateId, out PartyIndexEntry? nextEntry);
        if (priorEntry is null && nextEntry is null)
        {
            return true;
        }

        if (priorEntry is null || nextEntry is null)
        {
            return false;
        }

        return string.Equals(
            JsonSerializer.Serialize(priorEntry, s_candidateJsonOptions),
            JsonSerializer.Serialize(nextEntry, s_candidateJsonOptions),
            StringComparison.Ordinal);
    }

    private static PartyIndexSdkReadModel FromCandidate(DomainSharedProjectionRebuildCandidate candidate)
        => JsonSerializer.Deserialize<PartyIndexSdkReadModel>(candidate.State.Span, s_candidateJsonOptions)
            ?? throw new InvalidOperationException("The shared Party index rebuild candidate is empty or malformed.");

    private static long ParseGlobalPosition(string? projectionVersion)
        => projectionVersion is not null
            && projectionVersion.StartsWith("global:", StringComparison.Ordinal)
            && long.TryParse(
                projectionVersion.AsSpan("global:".Length),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out long position)
            ? position
            : 0;

    private static DomainSharedProjectionRebuildCandidate ToCandidate(PartyIndexSdkReadModel value)
        => new(JsonSerializer.SerializeToUtf8Bytes(value, s_candidateJsonOptions));

    private static void Validate(DomainSharedProjectionRebuildIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        _ = PartySdkReadModelAddresses.Index(identity.TenantId);
        if (!string.Equals(identity.Domain, "party", StringComparison.Ordinal)
            || !string.Equals(identity.ProjectionType, PartyProjectionNames.Index, StringComparison.Ordinal))
        {
            throw new ArgumentException("Shared rebuild identity does not target the Party index projection.", nameof(identity));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(identity.OperationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.CatalogFingerprint);
    }

    private static void Validate(ProjectionRequest request, string dispatchId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchId);
        if (!string.Equals(request.Domain, "party", StringComparison.Ordinal))
        {
            throw new ArgumentException("Projection request domain is not supported.", nameof(request));
        }

        _ = PartySdkReadModelAddresses.Index(request.TenantId);
        _ = PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId);
    }

}
