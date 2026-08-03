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
internal sealed partial class PartyReadModelHealthCheck(
    IReadModelStore readModelStore,
    IOptions<PartySdkReadModelOptions> options,
    ILogger<PartyReadModelHealthCheck> logger)
    : IHealthCheck
{
    private const string ProbeKey = "party-sdk-read-model-health-probe";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        string stateStoreName = options.Value.ReadModelStateStoreName;
        var data = new Dictionary<string, object> { ["stateStore"] = stateStoreName };
        try
        {
            _ = await readModelStore
                .GetAsync<ReadModelProbeSentinel>(stateStoreName, ProbeKey, cancellationToken)
                .ConfigureAwait(false);
            return HealthCheckResult.Healthy(
                "SDK read-model store is reachable.",
                data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Registration timeout cancels the token; report Degraded (never Unhealthy) so
            // readiness stays non-blocking, matching the retired ProjectionActorsHealthCheck.
            LogProbeTimedOut(logger, stateStoreName);
            return HealthCheckResult.Degraded(
                "SDK read-model health probe timed out.",
                data: data);
        }
        catch (Exception ex)
        {
            LogProbeFailed(logger, stateStoreName, ex);
            return HealthCheckResult.Degraded(
                "SDK read-model store is unreachable; projection reads may serve stale last-known data.",
                ex,
                data);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "SDK read-model health probe timed out for state store {StateStoreName}.")]
    private static partial void LogProbeTimedOut(ILogger logger, string stateStoreName);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "SDK read-model health probe failed for state store {StateStoreName}.")]
    private static partial void LogProbeFailed(ILogger logger, string stateStoreName, Exception exception);

    private sealed record ReadModelProbeSentinel;
}
