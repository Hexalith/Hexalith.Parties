using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
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
/// <remarks>
/// Live delivery and shared rebuild accumulation use the same source-version rules documented on
/// <see cref="Fold"/>. The index version is a diagnostic position hint; it does not certify a
/// complete tenant event prefix or identify every content change. Per-aggregate sequence and
/// erasure checkpoints govern replay, while <see cref="PartyIndexSdkReadModel.ProjectedAt"/>
/// supplies timestamp-based query freshness without proving tenant-wide convergence.
/// A rebuild starts with an empty candidate, so its version need not continue the previous
/// live index's version.
/// </remarks>
public sealed partial class PartyIndexSdkProjectionHandler(
    IReadModelStore readModelStore,
    IOptions<PartySdkReadModelOptions> options,
    IPartyIndexSearchIndexer? searchIndexer = null,
    ILogger<PartyIndexSdkProjectionHandler>? logger = null,
    ILoggerFactory? loggerFactory = null) :
    IAsyncDomainSharedProjectionRebuildCompletionHandler,
    IDeclaresProjectionReadModelSlots
{
    private const string SearchReconciliationReason = "search-reconciliation-required";
    private const string ProjectionRebuildRequiredReason = "projection-rebuild-required";
    private const string RebuildEventType = "PartyProjectionRebuilt";
    private const string ReconciledEventType = "PartyProjectionReconciled";
    private readonly IPartyIndexSearchIndexer _searchIndexer = searchIndexer ?? new NoOpPartyIndexSearchIndexer();
    private readonly ILogger? _foldLogger = loggerFactory?.CreateLogger(
        "Hexalith.Parties.Projections.Handlers.PartySdkProjectionFold");

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
            if (_foldLogger is not null
                && string.Equals(
                    foldResult.FailureReason,
                    PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason,
                    StringComparison.Ordinal))
            {
                _ = FoldCore(request, currentEntry.Value, _foldLogger);
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
                    persistedFold = FoldCore(request, current, loggedThisDelivery ? null : _foldLogger);
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

    private Task<bool> NotifySearchIndexerAsync(
        ProjectionRequest request,
        PartyIndexFoldResult foldResult,
        CancellationToken cancellationToken)
        => ReconcileSearchAsync(
            request.TenantId,
            request.AggregateId,
            removeIfAbsent: foldResult.Removed || foldResult.Model.Entries.ContainsKey(request.AggregateId),
            rebuilding: false,
            cancellationToken,
            replayRequest: request);

    /// <summary>Reconciles one work item against canonical state before and after external work.</summary>
    /// <remarks>
    /// A notification snapshot cannot authorize indexing after erasure. The second read catches
    /// erasure during awaited ingestion and mapping persistence; cleanup must converge before
    /// this delivery may complete. This is retryable reconciliation, not a distributed transaction
    /// or proof that an asynchronous external ingestion/deletion workflow has reached its end.
    /// </remarks>
    private async Task<bool> ReconcileSearchAsync(
        string tenantId,
        string partyId,
        bool removeIfAbsent,
        bool rebuilding,
        CancellationToken cancellationToken,
        ProjectionRequest? replayRequest = null)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_searchIndexer is NoOpPartyIndexSearchIndexer)
            {
                return true;
            }

            PartyIndexSdkReadModel current = await ReadSearchIndexAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (!TryGetSearchEntry(current, partyId, out PartyIndexEntry? entry))
            {
                if (!removeIfAbsent && !current.ErasureSequenceNumbers.ContainsKey(partyId))
                {
                    return true;
                }

                return await RemoveSearchEntryAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
            }

            PartyIndexEventMetadata? source = current.LastIndexedEvents.GetValueOrDefault(partyId);
            if (!rebuilding)
            {
                // Serialized canonical entries deliberately omit contact and identifier content.
                // Recreate it only from a complete, independently folded matching replay. A tail,
                // older delivery or changed source cannot authorize a first ingestion.
                if (replayRequest is null || !TryReplaySearchTarget(replayRequest, current, out PartyIndexEntry? complete))
                {
                    return false;
                }

                entry = complete;
            }

            bool indexed = await _searchIndexer
                .NotifyIndexedAsync(
                    tenantId,
                    entry,
                    rebuilding ? RebuildEventType : source?.EventTypeName ?? ReconciledEventType,
                    rebuilding ? entry.LastModifiedAt : source?.Timestamp ?? entry.LastModifiedAt,
                    source?.SequenceNumber,
                    cancellationToken)
                .ConfigureAwait(false);

            PartyIndexSdkReadModel latest = await ReadSearchIndexAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (!TryGetSearchEntry(latest, partyId, out _))
            {
                return await RemoveSearchEntryAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
            }

            // An unrelated aggregate's write must not force a retry. A changed target needs
            // another bounded dispatch so a late older notification cannot claim convergence.
            return indexed && MatchesSearchTarget(current, latest, partyId);
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

    /// <summary>Requires a readable canonical index rather than treating an outage as erasure.</summary>
    private async Task<PartyIndexSdkReadModel> ReadSearchIndexAsync(string tenantId, CancellationToken cancellationToken)
    {
        ReadModelEntry<PartyIndexSdkReadModel> read = await readModelStore
            .GetAsync<PartyIndexSdkReadModel>(StoreName, PartySdkReadModelAddresses.Index(tenantId), cancellationToken)
            .ConfigureAwait(false);
        return read.Value is { } model && HasValidDictionaries(model)
            ? model
            : throw new InvalidOperationException("The canonical Party search index is unavailable.");
    }

    /// <summary>Never selects an entry whose permanent erasure checkpoint is present.</summary>
    private static bool TryGetSearchEntry(PartyIndexSdkReadModel model, string partyId, [NotNullWhen(true)] out PartyIndexEntry? entry)
    {
        entry = null;
        if (model.ErasureSequenceNumbers.ContainsKey(partyId) || !model.Entries.TryGetValue(partyId, out entry))
        {
            return false;
        }

        if (entry is null || !string.Equals(entry.Id, partyId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The canonical Party search target is malformed.");
        }

        return !entry.IsErased;
    }

    /// <summary>Compares only this aggregate, including the checkpoint for transient search fields.</summary>
    private static bool MatchesSearchTarget(PartyIndexSdkReadModel left, PartyIndexSdkReadModel right, string partyId)
        => TryGetSearchEntry(left, partyId, out PartyIndexEntry? leftEntry)
            && TryGetSearchEntry(right, partyId, out PartyIndexEntry? rightEntry)
            && left.LastSequenceNumbers.GetValueOrDefault(partyId, long.MinValue)
                == right.LastSequenceNumbers.GetValueOrDefault(partyId, long.MinValue)
            && Equals(left.LastIndexedEvents.GetValueOrDefault(partyId), right.LastIndexedEvents.GetValueOrDefault(partyId))
            && string.Equals(left.SearchContentHashes?.GetValueOrDefault(partyId),
                right.SearchContentHashes?.GetValueOrDefault(partyId), StringComparison.Ordinal)
            && string.Equals(JsonSerializer.Serialize(leftEntry, s_candidateJsonOptions),
                JsonSerializer.Serialize(rightEntry, s_candidateJsonOptions), StringComparison.Ordinal);

    /// <summary>Restores transient search content only from a complete canonical-matching replay.</summary>
    private static bool TryReplaySearchTarget(
        ProjectionRequest request,
        PartyIndexSdkReadModel canonical,
        [NotNullWhen(true)] out PartyIndexEntry? entry)
    {
        entry = null;
        if (request.Events.Length == 0
            || PartySdkProjectionFold.GetDeliveryFailureReason(request.Events, long.MinValue) is not null
            || PartySdkProjectionFold.DeserializeNew(request.Events, long.MinValue).Any(static item => item.Payload is null))
        {
            return false;
        }

        PartyIndexSdkReadModel replayed = FoldCore(request, current: null).Model;
        if (!canonical.LastIndexedEvents.ContainsKey(request.AggregateId))
        {
            // Historical canonical rows predate event descriptors. Content plus their exact
            // checkpoint still proves the replay target without rewriting their provenance.
            replayed = replayed with { LastIndexedEvents = canonical.LastIndexedEvents };
        }

        string? expected = canonical.SearchContentHashes?.GetValueOrDefault(request.AggregateId);
        string actual = replayed.SearchContentHashes?.GetValueOrDefault(request.AggregateId) ?? string.Empty;
        // Missing historical hashes cannot provide the stronger content witness.
        replayed = replayed with { SearchContentHashes = canonical.SearchContentHashes };
        if (!MatchesSearchTarget(replayed, canonical, request.AggregateId))
        {
            return false;
        }

        if (!TryGetSearchEntry(replayed, request.AggregateId, out entry))
        {
            return false;
        }

        return expected is null || string.Equals(expected, actual, StringComparison.Ordinal);
    }

    /// <summary>Binds ignored search collections without persisting their personal content.</summary>
    private static string SearchContentHash(PartyIndexEntry entry)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            entry.SearchableContactChannels,
            entry.SearchableIdentifiers,
        }, s_candidateJsonOptions)));

    /// <summary>Detects a live re-add that races an awaited external removal.</summary>
    private async Task<bool> RemoveSearchEntryAsync(string tenantId, string partyId, CancellationToken cancellationToken)
    {
        bool removed = await _searchIndexer.NotifyRemovedAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        PartyIndexSdkReadModel latest = await ReadSearchIndexAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return removed && !TryGetSearchEntry(latest, partyId, out _);
    }

    public async Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        cancellationToken.ThrowIfCancellationRequested();
        ReadModelEntry<PartyIndexSdkReadModel> current = await readModelStore.GetAsync<PartyIndexSdkReadModel>(
            RebuildStoreName, PartySdkReadModelAddresses.Index(identity.TenantId), cancellationToken).ConfigureAwait(false);
        // The SDK reconstructs this plan for Stage, Commit and Verify. Its original concurrency
        // must survive publication; rereading the committed ETag would change the batch identity.
        ReadModelBatchConcurrency concurrency = current?.ETag is { Length: > 0 } etag
            ? ReadModelBatchConcurrency.Match(etag)
            : ReadModelBatchConcurrency.CreateOnly;
        return ToCandidate(new PartyIndexRebuildCandidate(
            new PartyIndexSdkReadModel(), new PartyProjectionRebuildDiagnostics(), concurrency));
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
        PartyIndexRebuildCandidate current = FromCandidate(candidate);
        if (aggregateHistory.Events.Length == 0)
        {
            return Task.FromResult(ToCandidate(current));
        }

        // Validation probes stay silent. Only the actual fold (or its terminal failure walk)
        // records drops, and the serialized candidate owns the counts across handler instances.
        PartyIndexFoldResult foldResult = FoldCore(aggregateHistory, current.Model, diagnostics: current.Diagnostics);
        if (foldResult.FailureReason is not null)
        {
            WriteRebuildDiagnostics(current.Diagnostics, completed: false);
            throw new InvalidOperationException(foldResult.FailureReason);
        }

        return Task.FromResult(ToCandidate(current with { Model = foldResult.Model }));
    }

    public async Task<DomainProjectionRebuildPlan> FinalizeAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();
        PartyIndexRebuildCandidate rebuildState = FromCandidate(candidate);
        PartyIndexSdkReadModel rebuiltIndex = rebuildState.Model;
        ReadModelEntry<PartyIndexSdkReadModel> current = await readModelStore
            .GetAsync<PartyIndexSdkReadModel>(
                RebuildStoreName,
                PartySdkReadModelAddresses.Index(identity.TenantId),
                cancellationToken)
            .ConfigureAwait(false);
        rebuiltIndex = PreserveErasureFences(rebuiltIndex, current?.Value);
        string[] removedPartyIds = [.. (current?.Value?.Entries?.Keys ?? [])
            .Concat(current?.Value?.ErasureSequenceNumbers?.Keys ?? [])
            .Except(rebuiltIndex.Entries.Keys, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static partyId => partyId, StringComparer.Ordinal)];
        PartyIndexSearchRebuildEntry[] rebuiltEntries = [.. rebuiltIndex.Entries.Values
            .OrderBy(static entry => entry.Id, StringComparer.Ordinal)
            .Select(static entry => new PartyIndexSearchRebuildEntry(
                entry,
                RebuildEventType,
                entry.LastModifiedAt))];
        byte[] completionState = JsonSerializer.SerializeToUtf8Bytes(
            new PartyIndexSearchRebuildManifest(rebuiltEntries, removedPartyIds, rebuildState.Diagnostics),
            s_candidateJsonOptions);
        // Legacy staged candidates did not capture publication concurrency. Preserve their
        // compatibility path; newly created sessions keep the admitted conditional policy.
        ReadModelBatchConcurrency concurrency = rebuildState.Concurrency ?? (current?.ETag is { Length: > 0 } etag
            ? ReadModelBatchConcurrency.Match(etag)
            : ReadModelBatchConcurrency.CreateOnly);
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

        // The manifest supplies only the work inventory. Every target is reread around its
        // external operation; no canonical snapshot is held across another party's awaits.
        foreach (string partyId in manifest.RemovedPartyIds ?? Array.Empty<string>())
        {
            if (!await ReconcileSearchAsync(identity.TenantId, partyId, removeIfAbsent: true, rebuilding: true, cancellationToken)
                .ConfigureAwait(false))
            {
                return DomainProjectionHandlerResult.Retryable(SearchReconciliationReason);
            }
        }

        foreach (PartyIndexSearchRebuildEntry item in manifest.Entries ?? Array.Empty<PartyIndexSearchRebuildEntry>())
        {
            if (!await ReconcileSearchAsync(identity.TenantId, item.Entry.Id, removeIfAbsent: true, rebuilding: true, cancellationToken)
                .ConfigureAwait(false))
            {
                return DomainProjectionHandlerResult.Retryable(SearchReconciliationReason);
            }
        }

        // Completion is at-least-once: each successful invocation emits at most one summary.
        // Retryable or canceled search reconciliation must not announce a completed rebuild.
        cancellationToken.ThrowIfCancellationRequested();
        if (manifest.Diagnostics is not null)
        {
            WriteRebuildDiagnostics(manifest.Diagnostics, completed: true);
        }
        return DomainProjectionHandlerResult.Completed();
    }

    /// <summary>Folds one aggregate's delivery into the shared tenant index.</summary>
    /// <param name="request">The aggregate delivery, including source sequence and global positions.</param>
    /// <param name="current">The current index, or null to start an empty fold.</param>
    /// <returns>The folded index; delivery failures retain the current model or an empty model.</returns>
    /// <remarks>
    /// After delivery validation succeeds, the version is <c>global:N</c> when the maximum of all
    /// supplied global positions and the current parseable global version is positive. Otherwise it is
    /// <c>{aggregateId}:{sequence}</c> for this request's retained or advanced aggregate checkpoint;
    /// if no checkpoint is known, the prior version (possibly null) is retained. Both numbers use
    /// invariant decimal formatting. Parsing requires an exact lowercase <c>global:</c> prefix
    /// and unsigned decimal digits fitting <see cref="long"/>, without signs or whitespace.
    /// The accepted aggregate identifier <c>global</c> produces a fallback indistinguishable from
    /// a global token to a later fold; the prefix alone does not prove a supplied global position.
    /// A global maximum includes supplied events that do not change an entry and does not prove
    /// gap-free tenant coverage. Aggregate fallback versions cannot
    /// order different aggregates. Empty live deliveries return before folding; duplicate-only
    /// live deliveries can skip persistence even if folding computes a different version.
    /// Cleanup can remove an entry without changing its version, and rebuilds use a fresh candidate.
    /// Consumers should use query freshness/degradation metadata for freshness and the store ETag
    /// for optimistic concurrency. Timestamp freshness also does not prove tenant-wide convergence.
    /// </remarks>
    internal static PartyIndexSdkReadModel Fold(ProjectionRequest request, PartyIndexSdkReadModel? current)
        => FoldCore(request, current).Model;

    private static PartyIndexFoldResult FoldCore(
        ProjectionRequest request,
        PartyIndexSdkReadModel? current,
        ILogger? logger = null,
        PartyProjectionRebuildDiagnostics? diagnostics = null)
    {
        // A missing model can start from zero; a malformed existing model cannot. In
        // particular, replacing missing erasure or sequence evidence would permit resurrection.
        if (current is not null && !HasValidDictionaries(current))
        {
            return new PartyIndexFoldResult(current, null, false, ProjectionRebuildRequiredReason);
        }

        var entries = new Dictionary<string, PartyIndexEntry>(
            current?.Entries ?? new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var lastIndexedEvents = new Dictionary<string, PartyIndexEventMetadata>(
            current?.LastIndexedEvents ?? new Dictionary<string, PartyIndexEventMetadata>(StringComparer.Ordinal),
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
            if ((logger is not null || diagnostics is not null)
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
                _ = PartySdkProjectionFold.HasUnresolvedNewEvent(request.Events, lastSequence, logger, diagnostics);
            }

            return new PartyIndexFoldResult(current ?? new PartyIndexSdkReadModel(), null, false, failureReason);
        }

        bool isErased = erasureSequences.ContainsKey(request.AggregateId);
        DateTimeOffset projectedAt = current?.ProjectedAt ?? DateTimeOffset.UnixEpoch;
        entries.TryGetValue(request.AggregateId, out PartyIndexEntry? entry);
        bool completeSearchContent = lastSequence == long.MinValue
            && !PartySdkProjectionFold.DeserializeNew(request.Events, long.MinValue).Any(static item => item.Payload is null);
        if (!isErased && current is not null && entry is not null)
        {
            ProjectionRequest prefix = request with
            {
                Events = [.. request.Events.Where(item => item.SequenceNumber <= lastSequence)],
            };
            if (TryReplaySearchTarget(prefix, current, out PartyIndexEntry? complete))
            {
                // New updates/removals must see the complete prior ignored collections.
                entry = complete;
                completeSearchContent = true;
            }
            else if (prefix.Events.Length > 0
                && prefix.Events.Max(static item => item.SequenceNumber) == lastSequence
                && PartySdkProjectionFold.GetDeliveryFailureReason(prefix.Events, long.MinValue) is null
                && !PartySdkProjectionFold.DeserializeNew(prefix.Events, long.MinValue).Any(static item => item.Payload is null))
            {
                return new PartyIndexFoldResult(current, null, false, SearchReconciliationReason);
            }
        }

        bool searchContentChanged = false;
        bool incompleteSearchMutation = false;
        bool removalObserved = isErased;
        PartyIndexEventMetadata? lastIndexedEvent = lastIndexedEvents.GetValueOrDefault(request.AggregateId);

        foreach ((ProjectionEventDto @event, IEventPayload? payload, bool advance) in
            PartySdkProjectionFold.DeserializeNew(request.Events, lastSequence, logger, diagnostics))
        {
            searchContentChanged |= payload is null or ContactChannelAdded or ContactChannelUpdated
                or ContactChannelRemoved or IdentifierAdded or IdentifierRemoved;
            if (payload is not null)
            {
                if (payload is PartyErased)
                {
                    _ = entries.Remove(request.AggregateId);
                    _ = lastIndexedEvents.Remove(request.AggregateId);
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
                    incompleteSearchMutation |= !completeSearchContent && payload is ContactChannelAdded or ContactChannelUpdated
                        or ContactChannelRemoved or IdentifierAdded or IdentifierRemoved;

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
                        lastIndexedEvent = new PartyIndexEventMetadata(
                            @event.SequenceNumber, @event.EventTypeName, @event.Timestamp);
                        lastIndexedEvents[request.AggregateId] = lastIndexedEvent;
                    }
                }
            }

            if (advance)
            {
                lastSequence = Math.Max(lastSequence, @event.SequenceNumber);
                projectedAt = PartySdkProjectionFold.ProjectedAt([@event], projectedAt);
            }
        }

        if (!isErased && incompleteSearchMutation)
        {
            // Serialized rows omit these collections. Keep the live checkpoint and witness
            // until matching complete replay arrives; a later erasure may still remove them.
            return new PartyIndexFoldResult(current ?? new PartyIndexSdkReadModel(), null, false, SearchReconciliationReason);
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
        var contentHashes = new Dictionary<string, string>(current?.SearchContentHashes
            ?? new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);
        if (entry is not null && !isErased && completeSearchContent)
        {
            contentHashes[request.AggregateId] = SearchContentHash(entry);
        }
        else if (isErased || searchContentChanged)
        {
            contentHashes.Remove(request.AggregateId);
        }

        var model = new PartyIndexSdkReadModel
        {
            Entries = entries,
            LastIndexedEvents = lastIndexedEvents,
            SearchContentHashes = contentHashes.Count == 0 ? null : contentHashes,
            LastSequenceNumbers = sequences,
            ErasureSequenceNumbers = erasureSequences,
            ProjectedAt = projectedAt,
            ProjectionVersion = version,
        };
        return new PartyIndexFoldResult(model, lastIndexedEvent, removalObserved, null);
    }

    private string StoreName => options.Value.GetStoreName();

    private static DomainProjectionHandlerResult DeliveryFailure(string reason)
        => DomainProjectionHandlerResult.Retryable(reason);

    private static PartyIndexFoldResult BuildReconciliationFold(
        ProjectionRequest request,
        PartyIndexSdkReadModel current)
    {
        // A retry can carry older events than the current entry. Its retained descriptor, rather
        // than any independently selected request event, identifies the canonical application.
        return new PartyIndexFoldResult(
            current,
            current.LastIndexedEvents.GetValueOrDefault(request.AggregateId),
            current.ErasureSequenceNumbers.ContainsKey(request.AggregateId),
            null);
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

        if (!string.Equals(current.SearchContentHashes?.GetValueOrDefault(aggregateId),
            folded.SearchContentHashes?.GetValueOrDefault(aggregateId), StringComparison.Ordinal))
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

    private static PartyIndexRebuildCandidate FromCandidate(DomainSharedProjectionRebuildCandidate candidate)
    {
        if (IsWrappedCandidate(candidate.State.Span))
        {
            PartyIndexRebuildCandidate? state = JsonSerializer
                .Deserialize<PartyIndexRebuildCandidate>(candidate.State.Span, s_candidateJsonOptions);
            return state is { Model: not null, Diagnostics: not null } && HasValidDictionaries(state.Model)
                && (state.Concurrency is null || state.Concurrency is { Mode: ReadModelBatchConcurrencyMode.ExpectedETag, ExpectedETag: not null })
                ? state
                : throw new InvalidOperationException("The shared Party index rebuild candidate is empty or malformed.");
        }

        // Older staged rebuilds stored only the raw index. Their previously observed drop counts
        // cannot be recovered, but subsequent accumulation can start counting without a migration.
        PartyIndexSdkReadModel model = JsonSerializer.Deserialize<PartyIndexSdkReadModel>(candidate.State.Span, s_candidateJsonOptions)
            ?? throw new InvalidOperationException("The shared Party index rebuild candidate is empty or malformed.");
        if (!HasValidDictionaries(model))
        {
            throw new InvalidOperationException("The shared Party index rebuild candidate is empty or malformed.");
        }

        return new PartyIndexRebuildCandidate(model, new PartyProjectionRebuildDiagnostics());
    }

    private static PartyIndexSdkReadModel PreserveErasureFences(PartyIndexSdkReadModel rebuilt, PartyIndexSdkReadModel? current)
    {
        if (current is null)
        {
            return rebuilt;
        }

        // Cleanup can commit before PartyErased is persisted. Event replay cannot prove
        // the absence of those fences if their dictionary was explicitly lost.
        if (current.ErasureSequenceNumbers is null)
        {
            throw new InvalidOperationException(ProjectionRebuildRequiredReason);
        }

        if (current.ErasureSequenceNumbers.Count == 0)
        {
            return rebuilt;
        }

        var entries = new Dictionary<string, PartyIndexEntry>(rebuilt.Entries, StringComparer.Ordinal);
        var descriptors = new Dictionary<string, PartyIndexEventMetadata>(rebuilt.LastIndexedEvents, StringComparer.Ordinal);
        var sequences = new Dictionary<string, long>(rebuilt.LastSequenceNumbers, StringComparer.Ordinal);
        var fences = new Dictionary<string, long>(rebuilt.ErasureSequenceNumbers, StringComparer.Ordinal);
        var contentHashes = new Dictionary<string, string>(rebuilt.SearchContentHashes
            ?? new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach ((string partyId, long fence) in current.ErasureSequenceNumbers)
        {
            _ = entries.Remove(partyId);
            _ = descriptors.Remove(partyId);
            _ = contentHashes.Remove(partyId);
            fences[partyId] = Math.Max(fences.GetValueOrDefault(partyId, long.MinValue), fence);
            sequences[partyId] = Math.Max(sequences.GetValueOrDefault(partyId, long.MinValue),
                Math.Max(fences[partyId], current.LastSequenceNumbers?.GetValueOrDefault(partyId, long.MinValue) ?? long.MinValue));
        }

        return rebuilt with
        {
            Entries = entries,
            LastIndexedEvents = descriptors,
            SearchContentHashes = contentHashes.Count == 0 ? null : contentHashes,
            LastSequenceNumbers = sequences,
            ErasureSequenceNumbers = fences,
            ProjectedAt = current.ProjectedAt is { } cleanupAt && (rebuilt.ProjectedAt is null || cleanupAt > rebuilt.ProjectedAt)
                ? cleanupAt : rebuilt.ProjectedAt,
        };
    }

    private static bool HasValidDictionaries(PartyIndexSdkReadModel model)
        => model is { Entries: not null, LastIndexedEvents: not null, LastSequenceNumbers: not null, ErasureSequenceNumbers: not null };

    private static bool IsWrappedCandidate(ReadOnlySpan<byte> state)
    {
        // Inspect root property presence without materializing the index. Null envelope members
        // are malformed envelopes, not legacy indexes with unknown properties to ignore.
        var reader = new Utf8JsonReader(state);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new InvalidOperationException("The shared Party index rebuild candidate is empty or malformed.");
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string? name = reader.GetString();
            if (string.Equals(name, "model", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            _ = reader.Read();
            reader.Skip();
        }

        return false;
    }

    private void WriteRebuildDiagnostics(PartyProjectionRebuildDiagnostics diagnostics, bool completed)
    {
        if (logger is null || (completed && !diagnostics.HasDrops))
        {
            return;
        }

        LogRebuildDropSummary(
            logger,
            !completed || diagnostics.HasUnexpectedDrops ? LogLevel.Warning : LogLevel.Information,
            completed,
            diagnostics.NonJson,
            diagnostics.UnknownType,
            diagnostics.AmbiguousType,
            diagnostics.CorruptPayload,
            diagnostics.RedactedPayload,
            diagnostics.WholePayloadRedacted,
            diagnostics.NullPayload);
    }

    [LoggerMessage(
        EventId = 4801,
        Message = "Party index rebuild drop summary: Completed={Completed}, NonJson={NonJson}, UnknownType={UnknownType}, AmbiguousType={AmbiguousType}, CorruptPayload={CorruptPayload}, RedactedPayload={RedactedPayload}, WholePayloadRedacted={WholePayloadRedacted}, NullPayload={NullPayload}.")]
    private static partial void LogRebuildDropSummary(
        ILogger logger,
        LogLevel level,
        bool completed,
        long nonJson,
        long unknownType,
        long ambiguousType,
        long corruptPayload,
        long redactedPayload,
        long wholePayloadRedacted,
        long nullPayload);

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

    private static DomainSharedProjectionRebuildCandidate ToCandidate(PartyIndexRebuildCandidate value)
        => new(JsonSerializer.SerializeToUtf8Bytes(value, s_candidateJsonOptions));

    private sealed record PartyIndexSearchRebuildEntry(
        PartyIndexEntry Entry,
        string EventType,
        DateTimeOffset Timestamp);

    private sealed record PartyIndexSearchRebuildManifest(
        IReadOnlyList<PartyIndexSearchRebuildEntry> Entries,
        IReadOnlyList<string> RemovedPartyIds,
        PartyProjectionRebuildDiagnostics? Diagnostics = null);

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
        PartySdkReadModelAddresses.ValidateSegment(request.AggregateId, nameof(request.AggregateId));
    }

}
