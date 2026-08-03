using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Projections.Configuration;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.HealthChecks;

/// <summary>
/// Best-effort connectivity health check for the SDK read-model store that replaced the
/// retired Dapr projection actors. This can only observe store reachability, not a true
/// "rebuilding" state: <see cref="IReadModelStore"/> exposes no rebuild-in-progress signal.
/// Projection degradation must remain non-readiness-blocking, so failures here report
/// <see cref="HealthStatus.Degraded"/>, never <see cref="HealthStatus.Unhealthy"/>.
/// </summary>
internal sealed class PartyReadModelHealthCheck(
    IReadModelStore readModelStore,
    IOptions<PartySdkReadModelOptions> options)
    : IHealthCheck
{
    private const string ProbeKey = "party-sdk-read-model-health-probe";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        string stateStoreName = options.Value.ReadModelStateStoreName;
        try
        {
            _ = await readModelStore
                .GetAsync<ReadModelProbeSentinel>(stateStoreName, ProbeKey, cancellationToken)
                .ConfigureAwait(false);
            return HealthCheckResult.Healthy(
                "SDK read-model store is reachable.",
                new Dictionary<string, object> { ["stateStore"] = stateStoreName });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded(
                "SDK read-model store is unreachable; projection reads may serve stale last-known data.",
                ex,
                new Dictionary<string, object> { ["stateStore"] = stateStoreName });
        }
    }

    private sealed record ReadModelProbeSentinel;
}
