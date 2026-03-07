using Dapr.Client;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.Parties.CommandApi.HealthChecks;

/// <summary>
/// Extension methods for registering DAPR health checks for the Parties service.
/// </summary>
public static class PartiesHealthCheckExtensions
{
    /// <summary>
    /// Registers all DAPR infrastructure health checks for the Parties CommandApi.
    /// Readiness is gated only on command-processing dependencies tagged "ready":
    /// the DAPR sidecar and the state store. Pub/sub and projection actor health
    /// still contribute to /health, but pub/sub degradation must not block writes.
    /// </summary>
    public static IHealthChecksBuilder AddPartiesDaprHealthChecks(
        this IHealthChecksBuilder builder,
        string stateStoreName = "statestore",
        string pubSubName = "pubsub")
    {
        ArgumentNullException.ThrowIfNull(builder);

        var healthCheckTimeout = TimeSpan.FromSeconds(3);
        _ = builder
            .AddCheck<DaprSidecarHealthCheck>(
                "dapr-sidecar",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: healthCheckTimeout)
            .Add(new HealthCheckRegistration(
                "dapr-statestore",
                sp => new DaprStateStoreHealthCheck(
                    sp.GetRequiredService<DaprClient>(), stateStoreName),
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: healthCheckTimeout))
            .Add(new HealthCheckRegistration(
                "dapr-pubsub",
                sp => new DaprPubSubHealthCheck(
                    sp.GetRequiredService<DaprClient>(), pubSubName),
                failureStatus: HealthStatus.Degraded,
                tags: [],
                timeout: healthCheckTimeout))
            .AddCheck<ProjectionActorsHealthCheck>(
                "projection-actors",
                failureStatus: HealthStatus.Degraded,
                tags: [],
                timeout: healthCheckTimeout);

        return builder;
    }
}
