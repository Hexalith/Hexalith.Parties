using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Projections;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Services;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Domain;

internal sealed partial class EventStorePartyProjectionPlatformAdapter(
    IProjectionCheckpointTracker checkpointTracker,
    IProjectionRebuildCheckpointStore rebuildCheckpointStore,
    LocalPartyProjectionPlatformAdapter localAdapter,
    ILogger<EventStorePartyProjectionPlatformAdapter> logger) : IPartyProjectionPlatformAdapter
{
    private const string Domain = "party";

    public Task<long> ReadDeliveredSequenceAsync(string tenantId, string partyId, CancellationToken cancellationToken)
    {
        AggregateIdentity identity = CreateIdentity(tenantId, partyId);
        return checkpointTracker.ReadLastDeliveredSequenceAsync(identity, cancellationToken);
    }

    public async Task<bool> TrySaveDeliveredSequenceAsync(
        string tenantId,
        string partyId,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        AggregateIdentity identity = CreateIdentity(tenantId, partyId);

        await checkpointTracker.TrackIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
        return await checkpointTracker
            .SaveDeliveredSequenceAsync(identity, sequenceNumber, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PartyProjectionRebuildCheckpoint?> ReadRebuildCheckpointAsync(
        PartyProjectionRebuildScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        try
        {
            _ = await rebuildCheckpointStore
                .ReadAsync(ToEventStoreScope(scope, scope.PartyId), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.EventStoreRebuildCheckpointReadUnavailable(logger, ex, scope.ProjectionName);
        }

        return await localAdapter.ReadRebuildCheckpointAsync(scope, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveRebuildCheckpointAsync(
        PartyProjectionRebuildScope scope,
        PartyProjectionRebuildCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(checkpoint);

        await localAdapter.SaveRebuildCheckpointAsync(scope, checkpoint, cancellationToken).ConfigureAwait(false);

        ProjectionRebuildCheckpointScope eventStoreScope = ToEventStoreScope(scope, checkpoint.PartyId);
        ProjectionRebuildCheckpointSaveResult result = await rebuildCheckpointStore
            .SaveAsync(
                eventStoreScope,
                checkpoint.SequenceNumber,
                ProjectionRebuildStatus.Running,
                cancellationToken: cancellationToken,
                isPerAggregateProgress: scope.PartyId is null)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Projection rebuild checkpoint save failed.");
        }
    }

    public async Task DeleteRebuildCheckpointAsync(PartyProjectionRebuildScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        await localAdapter.DeleteRebuildCheckpointAsync(scope, cancellationToken).ConfigureAwait(false);

        ProjectionRebuildCheckpointScope eventStoreScope = ToEventStoreScope(scope, scope.PartyId);
        ProjectionRebuildCheckpointSaveResult result = await rebuildCheckpointStore
            .SaveAsync(eventStoreScope, 0, ProjectionRebuildStatus.Succeeded, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Projection rebuild checkpoint completion save failed.");
        }
    }

    public Task<bool> HasActiveRebuildAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return rebuildCheckpointStore.HasActiveOperatorRebuildForDomainAsync(tenantId, Domain, cancellationToken);
    }

    public ProjectionFreshnessMetadata MapFreshness(
        PartyProjectionPlatformFreshness freshness,
        bool isRebuilding = false,
        bool stateStoreUnavailable = false,
        bool hasSafeCachedData = false)
        => MapFreshness(ToEventStoreFreshness(freshness), isRebuilding, stateStoreUnavailable, hasSafeCachedData);

    internal static ProjectionFreshnessMetadata MapFreshness(
        ReadModelFreshnessState freshness,
        bool isRebuilding = false,
        bool stateStoreUnavailable = false,
        bool hasSafeCachedData = false)
    {
        if (isRebuilding)
        {
            return ProjectionFreshnessMetadata.Create(
                ProjectionFreshnessStatus.Rebuilding,
                ProjectionFreshnessMetadata.WarningProjectionRebuilding);
        }

        if (stateStoreUnavailable)
        {
            return hasSafeCachedData
                ? ProjectionFreshnessMetadata.Create(
                    ProjectionFreshnessStatus.Stale,
                    ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable)
                : ProjectionFreshnessMetadata.Create(
                    ProjectionFreshnessStatus.Unavailable,
                    ProjectionFreshnessMetadata.WarningProjectionStateUnavailable);
        }

        return freshness switch
        {
            ReadModelFreshnessState.Current or ReadModelFreshnessState.Aging =>
                ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current),
            ReadModelFreshnessState.Stale =>
                ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Stale),
            _ => ProjectionFreshnessMetadata.Create(
                ProjectionFreshnessStatus.Unavailable,
                ProjectionFreshnessMetadata.WarningProjectionContextUnavailable),
        };
    }

    private static AggregateIdentity CreateIdentity(string tenantId, string partyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return new AggregateIdentity(tenantId, Domain, partyId);
    }

    private static ProjectionRebuildCheckpointScope ToEventStoreScope(
        PartyProjectionRebuildScope scope,
        string? aggregateId)
        => new(
            scope.TenantId,
            Domain,
            scope.ProjectionName,
            aggregateId,
            scope.OperationId);

    private static ReadModelFreshnessState ToEventStoreFreshness(PartyProjectionPlatformFreshness freshness)
        => freshness switch
        {
            PartyProjectionPlatformFreshness.Current => ReadModelFreshnessState.Current,
            PartyProjectionPlatformFreshness.Aging => ReadModelFreshnessState.Aging,
            PartyProjectionPlatformFreshness.Stale => ReadModelFreshnessState.Stale,
            _ => ReadModelFreshnessState.Unknown,
        };

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 8420,
            Level = LogLevel.Warning,
            Message = "EventStore rebuild checkpoint read unavailable for projection {ProjectionName}; using Parties local checkpoint fallback.")]
        public static partial void EventStoreRebuildCheckpointReadUnavailable(
            ILogger logger,
            Exception exception,
            string projectionName);
    }
}
