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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Projections.Handlers;

/// <summary>Persists the canonical shared Party tenant index with optimistic merge-on-write.</summary>
public sealed class PartyIndexSdkProjectionHandler(
    IReadModelStore readModelStore,
    IOptions<PartySdkReadModelOptions> options,
    IPartyIndexSearchIndexer? searchIndexer = null,
    ILogger<PartyIndexSdkProjectionHandler>? logger = null) :
    IAsyncDomainSharedProjectionRebuildCompletionHandler,
    IDeclaresProjectionReadModelSlots
{
    private const string SearchReconciliationReason = "search-reconciliation-required";
    private const string RebuildEventType = "PartyProjectionRebuilt";
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
        // Keep the preflight silent. A successful delivery is folded again by UpdateAsync to
        // produce the actual persistence candidate, and logging both walks would duplicate every
        // skip-and-advance diagnostic on the first write. An unresolved failure is re-walked once
        // with the logger below because no persistence callback will run for that delivery.
        PartyIndexFoldResult foldResult = FoldCore(request, currentEntry.Value);
        if (foldResult.FailureReason is not null)
        {
            if (logger is not null
                && string.Equals(
                    foldResult.FailureReason,
                    PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason,
                    StringComparison.Ordinal))
            {
                _ = FoldCore(request, currentEntry.Value, logger);
            }

            return DeliveryFailure(foldResult.FailureReason);
        }

        PartyIndexSdkReadModel folded = foldResult.Model;

        if (IsIdempotentNoOp(currentEntry.Value, folded, request.AggregateId))
        {
            // Reconciliation-only: IsIdempotentNoOp being true means this exact delivery already
            // fully applied and persisted on an earlier, separate ProjectAsync dispatch — not
            // later in this call. That earlier dispatch's own persisting FoldCore invocation (in
            // the try/UpdateAsync block below, but from that prior call, not this one) already
            // logged any drops for it then. This walk stays silent so a redelivered no-op does not
            // re-log the same drop every time it is retried.
            PartyIndexFoldResult reconciliationFold = BuildReconciliationFold(request, folded);
            bool reconciled = await NotifySearchIndexerAsync(request, reconciliationFold, cancellationToken)
                .ConfigureAwait(false);
            return reconciled
                ? DomainProjectionHandlerResult.AlreadyCompleted()
                : DomainProjectionHandlerResult.Retryable(SearchReconciliationReason);
        }

        PartyIndexFoldResult persistedFold = foldResult;
        bool loggedThisDelivery = false;
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
                    //
                    // ReadModelWritePolicy.UpdateAsync re-invokes this callback on every
                    // optimistic-concurrency retry (up to DefaultMaxAttempts), each with a fresh
                    // `current` snapshot. request.Events — the only input the drop diagnostics
                    // depend on — never changes across retries, so passing the logger on every
                    // attempt would re-emit the same diagnostic once per retry under writer
                    // contention. Log at most once per delivery by only passing the real logger on
                    // the first attempt.
                    persistedFold = FoldCore(request, current, loggedThisDelivery ? null : logger);
                    loggedThisDelivery = true;
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

        bool indexed = await NotifySearchIndexerAsync(request, persistedFold, cancellationToken).ConfigureAwait(false);
        return indexed
            ? DomainProjectionHandlerResult.Completed()
            : DomainProjectionHandlerResult.Retryable(SearchReconciliationReason);
    }

    private async Task<bool> NotifySearchIndexerAsync(
        ProjectionRequest request,
        PartyIndexFoldResult foldResult,
        CancellationToken cancellationToken)
    {
        try
        {
            if (foldResult.Removed)
            {
                return await _searchIndexer
                    .NotifyRemovedAsync(request.TenantId, request.AggregateId, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!foldResult.Model.Entries.TryGetValue(request.AggregateId, out PartyIndexEntry? entry)
                || entry is null
                || foldResult.LastIndexedEvent is null)
            {
                return true;
            }

            return await _searchIndexer
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
            return false;
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

        PartyIndexFoldResult foldResult = FoldCore(aggregateHistory, current, logger);
        if (foldResult.FailureReason is not null)
        {
            throw new InvalidOperationException(foldResult.FailureReason);
        }

        return Task.FromResult(ToCandidate(foldResult.Model));
    }

    public async Task<DomainProjectionRebuildPlan> FinalizeAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();
        PartyIndexSdkReadModel rebuiltIndex = FromCandidate(candidate);
        ReadModelEntry<PartyIndexSdkReadModel> current = await readModelStore
            .GetAsync<PartyIndexSdkReadModel>(
                RebuildStoreName,
                PartySdkReadModelAddresses.Index(identity.TenantId),
                cancellationToken)
            .ConfigureAwait(false);
        string[] removedPartyIds = [.. (current?.Value?.Entries.Keys ?? [])
            .Except(rebuiltIndex.Entries.Keys, StringComparer.Ordinal)
            .OrderBy(static partyId => partyId, StringComparer.Ordinal)];
        PartyIndexSearchRebuildEntry[] rebuiltEntries = [.. rebuiltIndex.Entries.Values
            .OrderBy(static entry => entry.Id, StringComparer.Ordinal)
            .Select(static entry => new PartyIndexSearchRebuildEntry(
                entry,
                RebuildEventType,
                entry.LastModifiedAt))];
        byte[] completionState = JsonSerializer.SerializeToUtf8Bytes(
            new PartyIndexSearchRebuildManifest(rebuiltEntries, removedPartyIds),
            s_candidateJsonOptions);
        ReadModelBatchConcurrency concurrency = current?.ETag is { Length: > 0 } etag
            ? ReadModelBatchConcurrency.Match(etag)
            : ReadModelBatchConcurrency.CreateOnly;
        return new DomainProjectionRebuildPlan(
            RebuildStoreName,
            [ReadModelBatchOperation.Write(
                PartySdkReadModelAddresses.Index(identity.TenantId),
                rebuiltIndex,
                concurrency)],
            completionState);
    }

    public async Task<DomainProjectionHandlerResult> CompleteRebuildAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        ReadOnlyMemory<byte> completionState,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();
        if (completionState.IsEmpty)
        {
            return DomainProjectionHandlerResult.Completed();
        }

        PartyIndexSearchRebuildManifest manifest = JsonSerializer
            .Deserialize<PartyIndexSearchRebuildManifest>(completionState.Span, s_candidateJsonOptions)
            ?? throw new InvalidOperationException("The Party index search rebuild manifest is empty or malformed.");

        // The manifest's removal list was computed from a FinalizeAsync-time snapshot, which can
        // predate a live write that (re)added one of those parties during the rebuild-accumulation
        // window. Re-check against the index as it stands now so a still-live party is never
        // reported as removed to the search indexer.
        ReadModelEntry<PartyIndexSdkReadModel> currentEntry = await readModelStore
            .GetAsync<PartyIndexSdkReadModel>(
                RebuildStoreName,
                PartySdkReadModelAddresses.Index(identity.TenantId),
                cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<string, PartyIndexEntry> currentEntries = currentEntry.Value?.Entries
            ?? new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal);

        foreach (string partyId in manifest.RemovedPartyIds ?? Array.Empty<string>())
        {
            if (currentEntries.ContainsKey(partyId))
            {
                continue;
            }

            if (!await _searchIndexer.NotifyRemovedAsync(identity.TenantId, partyId, cancellationToken).ConfigureAwait(false))
            {
                return DomainProjectionHandlerResult.Retryable(SearchReconciliationReason);
            }
        }

        foreach (PartyIndexSearchRebuildEntry item in manifest.Entries ?? Array.Empty<PartyIndexSearchRebuildEntry>())
        {
            // A live write can update or erase an entry after the rebuild batch commits but
            // before completion runs. The manifest is only a bounded work inventory; canonical
            // state supplies the value to publish and absence means there is nothing to reindex.
            if (!currentEntries.TryGetValue(item.Entry.Id, out PartyIndexEntry? canonicalEntry))
            {
                continue;
            }

            if (!await _searchIndexer
                .NotifyIndexedAsync(
                    identity.TenantId,
                    canonicalEntry,
                    item.EventType,
                    canonicalEntry.LastModifiedAt,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return DomainProjectionHandlerResult.Retryable(SearchReconciliationReason);
            }
        }

        return DomainProjectionHandlerResult.Completed();
    }

    internal static PartyIndexSdkReadModel Fold(ProjectionRequest request, PartyIndexSdkReadModel? current)
        => FoldCore(request, current).Model;

    private static PartyIndexFoldResult FoldCore(
        ProjectionRequest request,
        PartyIndexSdkReadModel? current,
        ILogger? logger = null)
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
            if (logger is not null
                && string.Equals(failureReason, PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason, StringComparison.Ordinal))
            {
                // GetDeliveryFailureReason's own detection pass above ran silently (it is shared
                // with PartyDetailSdkProjectionHandler, which logs this case through a different,
                // already-logged path). The shared Party index projection has no equivalent
                // fallback, so without this it would never emit NonJsonEventDropped /
                // UnknownEventTypeDropped / AmbiguousEventTypeDropped for an unresolvable event —
                // exactly the case where operators most need to see it, because the delivery is
                // now stuck Retryable. Re-walk once with the logger to emit that diagnostic before
                // returning; FoldCore returns immediately afterward, so the main fold loop below
                // never re-walks the same events and cannot double-log.
                _ = PartySdkProjectionFold.HasUnresolvedNewEvent(request.Events, lastSequence, logger);
            }

            return new PartyIndexFoldResult(current ?? new PartyIndexSdkReadModel(), null, false, failureReason);
        }

        bool isErased = erasureSequences.ContainsKey(request.AggregateId);
        DateTimeOffset projectedAt = current?.ProjectedAt ?? DateTimeOffset.UnixEpoch;
        entries.TryGetValue(request.AggregateId, out PartyIndexEntry? entry);
        bool removalObserved = false;
        ProjectionEventDto? lastIndexedEvent = null;

        foreach ((ProjectionEventDto @event, IEventPayload? payload, bool advance) in
            PartySdkProjectionFold.DeserializeNew(request.Events, lastSequence, logger))
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
                    removalObserved = true;
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

        // Empty deliveries are a no-op fold (already-completed / probe); Max() on an empty
        // sequence throws InvalidOperationException and must not abort the handler.
        long maxGlobalPosition = request.Events.Length == 0
            ? ParseGlobalPosition(current?.ProjectionVersion)
            : Math.Max(
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
        return new PartyIndexFoldResult(model, lastIndexedEvent, removalObserved, null);
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
        => DomainProjectionHandlerResult.Retryable(reason);

    private static PartyIndexFoldResult BuildReconciliationFold(
        ProjectionRequest request,
        PartyIndexSdkReadModel current)
    {
        if (current.ErasureSequenceNumbers.ContainsKey(request.AggregateId))
        {
            return new PartyIndexFoldResult(current, null, true, null);
        }

        ProjectionEventDto? lastIndexedEvent = request.Events.Length == 0
            ? null
            : PartySdkProjectionFold
                .DeserializeNew(request.Events, request.Events.Min(static item => item.SequenceNumber) - 1)
                .Where(static item => item.Payload is not null and not PartyErased)
                .Select(static item => item.Event)
                .LastOrDefault();
        return new PartyIndexFoldResult(current, lastIndexedEvent, false, null);
    }

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

    private sealed record PartyIndexSearchRebuildEntry(
        PartyIndexEntry Entry,
        string EventType,
        DateTimeOffset Timestamp);

    private sealed record PartyIndexSearchRebuildManifest(
        IReadOnlyList<PartyIndexSearchRebuildEntry> Entries,
        IReadOnlyList<string> RemovedPartyIds);

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
