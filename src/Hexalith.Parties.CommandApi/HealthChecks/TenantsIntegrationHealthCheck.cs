using Hexalith.Parties.CommandApi.Configuration;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.CommandApi.HealthChecks;

internal interface ITenantsReadinessProbe
{
    Task<bool> IsReadyAsync(string serviceName, CancellationToken cancellationToken);
}

internal sealed class DaprTenantsReadinessProbe(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : ITenantsReadinessProbe
{
    internal const string HttpClientName = "tenants-readiness-probe";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory
        ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly IConfiguration _configuration = configuration
        ?? throw new ArgumentNullException(nameof(configuration));

    public async Task<bool> IsReadyAsync(string serviceName, CancellationToken cancellationToken)
    {
        string daprHttpPort = _configuration["DAPR_HTTP_PORT"] ?? "3500";
        string escapedServiceName = Uri.EscapeDataString(serviceName);
        var uri = new Uri($"http://127.0.0.1:{daprHttpPort}/v1.0/invoke/{escapedServiceName}/method/ready");

        HttpClient httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using HttpResponseMessage response = await httpClient
            .GetAsync(uri, cancellationToken)
            .ConfigureAwait(false);

        return response.IsSuccessStatusCode;
    }
}

internal sealed class TenantsIntegrationHealthCheck(
    IOptions<TenantIntegrationOptions> options,
    ITenantsReadinessProbe readinessProbe) : IHealthCheck
{
    private readonly TenantIntegrationOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));
    private readonly ITenantsReadinessProbe _readinessProbe = readinessProbe
        ?? throw new ArgumentNullException(nameof(readinessProbe));

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy("Tenants integration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.ServiceName))
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Tenants integration is enabled but Tenants:ServiceName is not configured.");
        }

        try
        {
            bool isReady = await _readinessProbe
                .IsReadyAsync(_options.ServiceName, cancellationToken)
                .ConfigureAwait(false);

            return isReady
                ? HealthCheckResult.Healthy($"Tenants service '{_options.ServiceName}' is ready.")
                : new HealthCheckResult(
                    context.Registration.FailureStatus,
                    $"Tenants service '{_options.ServiceName}' is not ready.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Tenants integration health check failed: {ex.GetType().Name}",
                exception: ex);
        }
    }
}
