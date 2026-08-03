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
    private const string UnresolvedOrUnsupportedEventReason = "unresolved-or-unsupported-event";

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
        IndexFoldResult foldResult = FoldCore(request, currentEntry.Value);
        PartyIndexSdkReadModel folded = foldResult.Model;

        if (IsIdempotentNoOp(currentEntry.Value, folded, request.AggregateId))
        {
            long priorSequence = currentEntry.Value?.LastSequenceNumbers.GetValueOrDefault(
                request.AggregateId,
                long.MinValue) ?? long.MinValue;
            if (request.Events.Any(item => item.SequenceNumber > priorSequence))
            {
                return DomainProjectionHandlerResult.Failed(UnresolvedOrUnsupportedEventReason);
            }

            return DomainProjectionHandlerResult.AlreadyCompleted();
        }

        await ReadModelWritePolicy.UpdateAsync<PartyIndexSdkReadModel>(
            readModelStore,
            StoreName,
            indexKey,
            _ => folded,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await NotifySearchIndexerAsync(request, foldResult, cancellationToken).ConfigureAwait(false);

        return DomainProjectionHandlerResult.Completed();
    }

    private async Task NotifySearchIndexerAsync(
        ProjectionRequest request,
        IndexFoldResult foldResult,
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

        return Task.FromResult(ToCandidate(Fold(aggregateHistory, current)));
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

        // Best-effort reindex of the rebuilt tenant index so Memories is not left on pre-rebuild
        // documents. Failures here must not block returning the rebuild plan.
        await NotifyRebuildIndexedAsync(identity.TenantId, rebuiltIndex, cancellationToken).ConfigureAwait(false);

        return new DomainProjectionRebuildPlan(
            RebuildStoreName,
            [ReadModelBatchOperation.Write(
                PartySdkReadModelAddresses.Index(identity.TenantId),
                rebuiltIndex,
                ReadModelBatchConcurrency.LastWrite)]);
    }

    private async Task NotifyRebuildIndexedAsync(
        string tenantId,
        PartyIndexSdkReadModel rebuiltIndex,
        CancellationToken cancellationToken)
    {
        DateTimeOffset timestamp = rebuiltIndex.ProjectedAt ?? DateTimeOffset.UtcNow;
        foreach (PartyIndexEntry entry in rebuiltIndex.Entries.Values)
        {
            try
            {
                await _searchIndexer
                    .NotifyIndexedAsync(tenantId, entry, "PartyIndexRebuild", timestamp, cancellationToken)
                    .ConfigureAwait(false);
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
    }

    internal static PartyIndexSdkReadModel Fold(ProjectionRequest request, PartyIndexSdkReadModel? current)
        => FoldCore(request, current).Model;

    private static IndexFoldResult FoldCore(ProjectionRequest request, PartyIndexSdkReadModel? current)
    {
        var entries = new Dictionary<string, PartyIndexEntry>(
            current?.Entries ?? new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var sequences = new Dictionary<string, long>(
            current?.LastSequenceNumbers ?? new Dictionary<string, long>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        long lastSequence = sequences.GetValueOrDefault(request.AggregateId, long.MinValue);
        DateTimeOffset projectedAt = current?.ProjectedAt ?? DateTimeOffset.UnixEpoch;
        entries.TryGetValue(request.AggregateId, out PartyIndexEntry? entry);
        bool hadEntry = entry is not null;
        ProjectionEventDto? lastIndexedEvent = null;

        // Tracks the sequence number of a PartyErased event only while it remains the most recent
        // meaningful event folded for this aggregate. A later non-erasure event that actually
        // mutates the entry (a same-batch recreate) clears it again, so the checkpoint write below
        // only special-cases the case where erasure is genuinely the terminal event.
        long? erasedAtSequence = null;

        foreach ((ProjectionEventDto @event, IEventPayload? payload, bool advance) in
            PartySdkProjectionFold.DeserializeNew(request.Events, lastSequence))
        {
            if (payload is not null)
            {
                if (payload is PartyErased)
                {
                    _ = entries.Remove(request.AggregateId);
                    entry = null;
                    erasedAtSequence = @event.SequenceNumber;
                    lastIndexedEvent = null;
                }
                else
                {
                    PartyIndexEntry? applied = PartyIndexProjectionHandler.Apply(request.AggregateId, payload, entry);
                    if (applied is not null)
                    {
                        // Only clear terminal-erasure tracking when a later event actually mutates
                        // the entry. No-op payloads must leave erasedAtSequence intact.
                        erasedAtSequence = null;
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

        if (entry is null && erasedAtSequence is not null)
        {
            // Erasure removed the entry and no later event restored it (including same-batch
            // no-ops that still advance the deserialize checkpoint). Drop the watermark so a
            // party recreated with the same id is not skipped by DeserializeNew.
            _ = sequences.Remove(request.AggregateId);
        }
        else if (lastSequence != long.MinValue)
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
            ProjectedAt = projectedAt,
            ProjectionVersion = version,
        };
        bool removed = hadEntry && entry is null;
        return new IndexFoldResult(model, lastIndexedEvent, removed);
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

    internal readonly record struct IndexFoldResult(
        PartyIndexSdkReadModel Model,
        ProjectionEventDto? LastIndexedEvent,
        bool Removed);
}
