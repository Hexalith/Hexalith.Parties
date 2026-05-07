using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.Parties.Middleware;

/// <summary>
/// Middleware that injects degradation headers on GET responses when the service
/// is in a degraded state (e.g., pub/sub unavailable but cached reads still work).
/// <para>
/// Failure mode behavior:
/// <list type="bullet">
/// <item>State store unavailable: write commands fail with ProblemDetails (handled by exception handler),
///   reads may serve cached/stale data with X-Service-Degraded and X-Stale-Data-Age headers.</item>
/// <item>Pub/sub unavailable: events committed but not published, retry on recovery.
///   Reads continue normally with degradation headers indicating reduced freshness.</item>
/// <item>Sidecar unavailable: full degradation — health reports Unhealthy, readiness false.</item>
/// </list>
/// </para>
/// </summary>
public sealed class DegradedResponseMiddleware(RequestDelegate next, HealthCheckService healthCheckService)
{
    private DateTimeOffset? _degradedSince;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Skip infrastructure endpoints to avoid circular health check invocation:
        // - Health probes: middleware runs health checks → would recurse
        // - Actor invocations: projection-actors health check calls actors via DAPR
        //   sidecar → routed back to /actors/* → middleware runs health checks again → infinite loop
        string path = context.Request.Path.Value ?? string.Empty;
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/alive", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/ready", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/actors/", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        HealthReport report = await healthCheckService.CheckHealthAsync(
            _ => true,
            context.RequestAborted).ConfigureAwait(false);

        // Reads can keep serving stale data when command durability is impaired but
        // the sidecar and projection actors are still responsive.
        bool canServeStaleReads = CanServeStaleReads(report);

        if (canServeStaleReads)
        {
            _degradedSince ??= DateTimeOffset.UtcNow;

            if (HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.Headers["X-Service-Degraded"] = "true";
                context.Response.Headers["X-Stale-Data-Age"] = "0";

                context.Response.OnStarting(static state =>
                {
                    var (httpContext, degradedSince) = ((HttpContext, DateTimeOffset))state;
                    if (httpContext.Response.StatusCode < StatusCodes.Status500InternalServerError)
                    {
                        long staleSeconds = (long)(DateTimeOffset.UtcNow - degradedSince).TotalSeconds;
                        httpContext.Response.Headers["X-Service-Degraded"] = "true";
                        httpContext.Response.Headers["X-Stale-Data-Age"] = staleSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        httpContext.Response.Headers.Remove("X-Service-Degraded");
                        httpContext.Response.Headers.Remove("X-Stale-Data-Age");
                    }

                    return Task.CompletedTask;
                }, (context, _degradedSince.Value));
            }
        }
        else
        {
            _degradedSince = null;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool CanServeStaleReads(HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.Entries.TryGetValue("dapr-sidecar", out HealthReportEntry sidecarEntry)
            && sidecarEntry.Status == HealthStatus.Unhealthy)
        {
            return false;
        }

        if (report.Entries.TryGetValue("projection-actors", out HealthReportEntry projectionEntry)
            && projectionEntry.Status == HealthStatus.Unhealthy)
        {
            return false;
        }

        bool stateStoreUnavailable = report.Entries.TryGetValue("dapr-statestore", out HealthReportEntry stateStoreEntry)
            && stateStoreEntry.Status == HealthStatus.Unhealthy;
        bool pubSubDegraded = report.Entries.TryGetValue("dapr-pubsub", out HealthReportEntry pubSubEntry)
            && pubSubEntry.Status == HealthStatus.Degraded;

        return stateStoreUnavailable || pubSubDegraded;
    }
}
