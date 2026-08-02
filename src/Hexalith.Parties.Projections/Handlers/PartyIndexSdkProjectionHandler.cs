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

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Projections.Handlers;

/// <summary>Persists the canonical shared Party tenant index with optimistic merge-on-write.</summary>
public sealed class PartyIndexSdkProjectionHandler(
    IReadModelStore readModelStore,
    IOptions<PartySdkReadModelOptions> options) :
    IAsyncDomainSharedProjectionRebuildHandler,
    IDeclaresProjectionReadModelSlots
{
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

        await ReadModelWritePolicy.UpdateAsync<PartyIndexSdkReadModel>(
            readModelStore,
            StoreName,
            PartySdkReadModelAddresses.Index(request.TenantId),
            current => Fold(request, current),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return DomainProjectionHandlerResult.Completed();
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

        // Tracks the sequence number of a PartyErased event only while it remains the most recent
        // meaningful event folded for this aggregate. A later non-erasure event (a same-batch
        // recreate) clears it again, so the checkpoint write below only special-cases the case
        // where erasure is genuinely the terminal event.
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
                }
                else
                {
                    erasedAtSequence = null;
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
                    }
                }
            }

            if (advance)
            {
                lastSequence = Math.Max(lastSequence, @event.SequenceNumber);
                projectedAt = PartySdkProjectionFold.ProjectedAt([@event], projectedAt);
            }
        }

        if (erasedAtSequence is not null && erasedAtSequence == lastSequence)
        {
            // Erasure was the terminal event for this party: drop the checkpoint instead of
            // persisting a stale watermark. Without this, a party recreated with the same id would
            // have its own early events silently skipped as "already applied" by
            // PartySdkProjectionFold.DeserializeNew on the next delivery. Full replay-from-zero
            // makes re-folding the already-erased history on a future delivery idempotent-safe.
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
        return new PartyIndexSdkReadModel
        {
            Entries = entries,
            LastSequenceNumbers = sequences,
            ProjectedAt = projectedAt,
            ProjectionVersion = version,
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
