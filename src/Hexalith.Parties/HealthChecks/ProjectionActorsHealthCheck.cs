using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.HealthChecks;

/// <summary>
/// Health check that verifies projection actor endpoints are reachable through DAPR actor routing.
/// Failure mode: when actor placement or projection actor invocation is unavailable, query reads
/// may become stale, so the service reports Degraded until actor calls recover.
/// </summary>
public sealed class ProjectionActorsHealthCheck(
    IActorProxyFactory actorProxyFactory,
    ILogger<ProjectionActorsHealthCheck> logger) : IHealthCheck
{
    private static readonly ActorId s_indexActorId = new("health:party-index");
    private static readonly ActorId s_detailActorId = new("health:party-detail:probe");
    private readonly IActorProxyFactory _actorProxyFactory = actorProxyFactory
        ?? throw new ArgumentNullException(nameof(actorProxyFactory));
    private readonly ILogger<ProjectionActorsHealthCheck> _logger = logger
        ?? throw new ArgumentNullException(nameof(logger));

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            IPartyIndexProjectionActor indexProxy = _actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                s_indexActorId,
                nameof(PartyIndexProjectionActor));

            // Use remoting-safe ping methods so the health probe validates actor routing
            // and responsiveness without depending on collection serialization details.
            // IsRebuildingAsync is invoked here for contract-completeness: the static probe
            // actor instances (health:party-index, health:party-detail:probe) carry per-instance
            // _isRebuilding flags that are not shared with real tenant actors, so this branch
            // primarily guards the probe activations themselves. Detecting tenant-actor
            // rebuilding state from this surface would require enumerating tenants, which is
            // forbidden by the story's non-enumeration rule for health output.
            _ = await indexProxy.PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            bool indexRebuilding = await indexProxy.IsRebuildingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

            IPartyDetailProjectionActor detailProxy = _actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                s_detailActorId,
                nameof(PartyDetailProjectionActor));
            _ = await detailProxy.PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            bool detailRebuilding = await detailProxy.IsRebuildingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

            if (indexRebuilding || detailRebuilding)
            {
                return new HealthCheckResult(
                    context.Registration.FailureStatus,
                    "Projection actors are rebuilding.");
            }

            return HealthCheckResult.Healthy("Projection actors are responsive.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Projection actor health probe canceled or timed out.");
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Projection actor health check canceled or timed out.");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Projection actor health probe failed.");
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Projection actor health check failed.");
        }
    }
}
