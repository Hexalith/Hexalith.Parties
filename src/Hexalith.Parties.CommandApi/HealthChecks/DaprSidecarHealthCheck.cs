using Dapr.Client;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.CommandApi.HealthChecks;

/// <summary>
/// Health check that verifies DAPR sidecar responsiveness.
/// Failure mode: When the sidecar is unavailable, all DAPR operations fail —
/// the service enters full degradation. Health reports Unhealthy, readiness
/// reports false, and the failure is logged at Error level.
/// </summary>
public class DaprSidecarHealthCheck(DaprClient daprClient, ILogger<DaprSidecarHealthCheck> logger) : IHealthCheck
{
    private readonly DaprClient _daprClient = daprClient
        ?? throw new ArgumentNullException(nameof(daprClient));
    private readonly ILogger<DaprSidecarHealthCheck> _logger = logger
        ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            bool isHealthy = await _daprClient.CheckHealthAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!isHealthy)
            {
                _logger.LogError("Dapr sidecar health probe returned not responsive.");
            }

            return isHealthy
                ? HealthCheckResult.Healthy("Dapr sidecar is responsive.")
                : new HealthCheckResult(
                    context.Registration.FailureStatus,
                    "Dapr sidecar is not responsive.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dapr sidecar health probe failed.");
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Dapr sidecar health check failed: {ex.GetType().Name}",
                exception: ex);
        }
    }
}
