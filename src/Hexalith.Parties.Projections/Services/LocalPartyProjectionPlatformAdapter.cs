using System.Net;
using System.Net.Http.Json;

using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Services;

public sealed class LocalPartyProjectionPlatformAdapter(HttpClient daprHttpClient) : IPartyProjectionPlatformAdapter
{
    private const string IndexActorType = "PartyIndexProjectionActor";
    private const string RebuildCheckpointPrefix = "rebuild-checkpoint";

    public Task<long> ReadDeliveredSequenceAsync(string tenantId, string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        return Task.FromResult(0L);
    }

    public Task<bool> TrySaveDeliveredSequenceAsync(
        string tenantId,
        string partyId,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentOutOfRangeException.ThrowIfNegative(sequenceNumber);

        return Task.FromResult(true);
    }

    public async Task<PartyProjectionRebuildCheckpoint?> ReadRebuildCheckpointAsync(
        PartyProjectionRebuildScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        string actorId = GetIndexActorId(scope.TenantId);
        string encodedActorId = Uri.EscapeDataString(actorId);
        string encodedKey = Uri.EscapeDataString(GetCheckpointStateKey(scope));
        string url = $"/v1.0/actors/{IndexActorType}/{encodedActorId}/state/{encodedKey}";

        HttpResponseMessage response = await daprHttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PartyProjectionRebuildCheckpoint>(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveRebuildCheckpointAsync(
        PartyProjectionRebuildScope scope,
        PartyProjectionRebuildCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(checkpoint);

        await WriteActorStateAsync(
                scope.TenantId,
                GetCheckpointStateKey(scope),
                checkpoint,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteRebuildCheckpointAsync(PartyProjectionRebuildScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        string actorId = GetIndexActorId(scope.TenantId);
        string url = $"/v1.0/actors/{IndexActorType}/{Uri.EscapeDataString(actorId)}/state";
        var stateTransaction = new[]
        {
            new
            {
                operation = "delete",
                request = new { key = GetCheckpointStateKey(scope) },
            },
        };

        HttpResponseMessage response = await daprHttpClient
            .PutAsJsonAsync(url, stateTransaction, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public Task<bool> HasActiveRebuildAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return Task.FromResult(false);
    }

    public ProjectionFreshnessMetadata MapFreshness(
        PartyProjectionPlatformFreshness freshness,
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
            PartyProjectionPlatformFreshness.Current or PartyProjectionPlatformFreshness.Aging =>
                ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current),
            PartyProjectionPlatformFreshness.Stale =>
                ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Stale),
            _ => ProjectionFreshnessMetadata.Create(
                ProjectionFreshnessStatus.Unavailable,
                ProjectionFreshnessMetadata.WarningProjectionContextUnavailable),
        };
    }

    private async Task WriteActorStateAsync<T>(
        string tenantId,
        string stateKey,
        T value,
        CancellationToken cancellationToken)
    {
        string actorId = GetIndexActorId(tenantId);
        string url = $"/v1.0/actors/{IndexActorType}/{Uri.EscapeDataString(actorId)}/state";

        var stateTransaction = new[]
        {
            new
            {
                operation = "upsert",
                request = new { key = stateKey, value },
            },
        };

        HttpResponseMessage response = await daprHttpClient
            .PutAsJsonAsync(url, stateTransaction, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private static string GetIndexActorId(string tenantId)
        => $"{tenantId}:party-index";

    private static string GetCheckpointStateKey(PartyProjectionRebuildScope scope)
        => $"{scope.TenantId}:{RebuildCheckpointPrefix}:{GetProjectionKey(scope)}";

    private static string GetProjectionKey(PartyProjectionRebuildScope scope)
        => scope.ProjectionName switch
        {
            "party-detail" when scope.PartyId is not null => $"detail:{scope.PartyId}",
            "party-detail" => "detail",
            "party-index" => "index",
            _ => scope.ProjectionName,
        };
}
