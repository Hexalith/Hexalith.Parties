using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Projections;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Services;

namespace Hexalith.Parties.Domain;

internal sealed class EventStorePartyProjectionPlatformAdapter(
    IProjectionCheckpointTracker checkpointTracker,
    IProjectionRebuildCheckpointStore rebuildCheckpointStore) : IPartyProjectionPlatformAdapter
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

        if (scope.PartyId is not null)
        {
            ProjectionRebuildCheckpoint? checkpoint = await rebuildCheckpointStore
                .ReadAsync(ToEventStoreScope(scope, scope.PartyId), cancellationToken)
                .ConfigureAwait(false);
            return ToPartyCheckpoint(checkpoint);
        }

        ProjectionRebuildCheckpoint? latest = null;
        await foreach (AggregateIdentity identity in checkpointTracker
            .EnumerateTrackedIdentitiesAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            if (!string.Equals(identity.TenantId, scope.TenantId, StringComparison.Ordinal)
                || !string.Equals(identity.Domain, Domain, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ProjectionRebuildCheckpoint? checkpoint = await rebuildCheckpointStore
                .ReadAsync(ToEventStoreScope(scope, identity.AggregateId), cancellationToken)
                .ConfigureAwait(false);
            if (checkpoint is null || IsTerminal(checkpoint.Status))
            {
                continue;
            }

            if (latest is null || checkpoint.UpdatedAt > latest.UpdatedAt)
            {
                latest = checkpoint;
            }
        }

        return ToPartyCheckpoint(latest);
    }

    public async Task SaveRebuildCheckpointAsync(
        PartyProjectionRebuildScope scope,
        PartyProjectionRebuildCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(checkpoint);

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

        if (scope.PartyId is not null)
        {
            await SaveTerminalCheckpointAsync(ToEventStoreScope(scope, scope.PartyId), cancellationToken).ConfigureAwait(false);
            return;
        }

        await foreach (AggregateIdentity identity in checkpointTracker
            .EnumerateTrackedIdentitiesAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            if (!string.Equals(identity.TenantId, scope.TenantId, StringComparison.Ordinal)
                || !string.Equals(identity.Domain, Domain, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await SaveTerminalCheckpointAsync(ToEventStoreScope(scope, identity.AggregateId), cancellationToken).ConfigureAwait(false);
        }

        await SaveTerminalCheckpointAsync(ToEventStoreScope(scope, null), cancellationToken).ConfigureAwait(false);
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
    {
        if (freshness == PartyProjectionPlatformFreshness.Degraded && !isRebuilding)
        {
            return ProjectionFreshnessMetadata.Create(
                ProjectionFreshnessStatus.Degraded,
                ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable);
        }

        return MapFreshness(ToEventStoreFreshness(freshness), isRebuilding, stateStoreUnavailable, hasSafeCachedData);
    }

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

    private static PartyProjectionRebuildCheckpoint? ToPartyCheckpoint(ProjectionRebuildCheckpoint? checkpoint)
    {
        if (checkpoint is null || IsTerminal(checkpoint.Status) || string.IsNullOrWhiteSpace(checkpoint.AggregateId))
        {
            return null;
        }

        return new PartyProjectionRebuildCheckpoint(checkpoint.AggregateId, checkpoint.LastAppliedSequence);
    }

    private async Task SaveTerminalCheckpointAsync(
        ProjectionRebuildCheckpointScope scope,
        CancellationToken cancellationToken)
    {
        ProjectionRebuildCheckpointSaveResult result = await rebuildCheckpointStore
            .SaveAsync(scope, 0, ProjectionRebuildStatus.Succeeded, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Projection rebuild checkpoint completion save failed.");
        }
    }

    private static bool IsTerminal(ProjectionRebuildStatus status)
        => status is ProjectionRebuildStatus.Succeeded
            or ProjectionRebuildStatus.Failed
            or ProjectionRebuildStatus.Canceled;

    private static ReadModelFreshnessState ToEventStoreFreshness(PartyProjectionPlatformFreshness freshness)
        => freshness switch
        {
            PartyProjectionPlatformFreshness.Current => ReadModelFreshnessState.Current,
            PartyProjectionPlatformFreshness.Aging => ReadModelFreshnessState.Aging,
            PartyProjectionPlatformFreshness.Stale or PartyProjectionPlatformFreshness.Degraded => ReadModelFreshnessState.Stale,
            _ => ReadModelFreshnessState.Unknown,
        };
}
