extern alias apphost;

using System.Net;

using Aspire.Hosting;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>
/// Shared fixture that starts the full Parties Aspire topology (CommandApi + DAPR sidecar
/// + in-memory state store/pub/sub) for Tier 3 health endpoint E2E tests.
/// Keycloak is disabled for fast startup; uses symmetric key JWT auth.
/// </summary>
public class PartiesAspireTopologyFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private string? _previousAspNetCoreEnvironment;
    private string? _previousDotNetEnvironment;
    private HttpClient? _commandApiClient;

    /// <summary>
    /// Gets the HTTP client for the CommandApi service.
    /// Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public HttpClient CommandApiClient => _commandApiClient ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    /// <summary>
    /// Gets the running Aspire distributed application instance.
    /// </summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    public async Task InitializeAsync()
    {
        // Disable Keycloak for health check tests — symmetric key JWT auth is sufficient.
        _previousEnableKeycloak = Environment.GetEnvironmentVariable("EnableKeycloak");
        Environment.SetEnvironmentVariable("EnableKeycloak", "false");

        // Force Development environment so CommandApi loads appsettings.Development.json.
        _previousAspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        _previousDotNetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        // 5-minute timeout for full Aspire topology startup including DAPR sidecar
        // initialization and health check stabilization.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        _builder = await DistributedApplicationTestingBuilder
            .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>()
            .ConfigureAwait(false);

        _builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter(_builder.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Warning);
        });

        _app = await _builder.BuildAsync().ConfigureAwait(false);
        await _app.StartAsync(cts.Token).ConfigureAwait(false);

        // Wait for commandapi to be healthy. This includes the DAPR health checks
        // (sidecar, state store, pub/sub) becoming healthy after sidecar initialization.
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("commandapi", cts.Token)
            .WaitAsync(TimeSpan.FromMinutes(5), cts.Token)
            .ConfigureAwait(false);

        _commandApiClient = _app.CreateHttpClient("commandapi");
        _commandApiClient.Timeout = TimeSpan.FromSeconds(60);

        // Wait for the /health endpoint to actually return 200 via HTTP.
        // WaitForResourceHealthyAsync signals orchestrator-level readiness, but we
        // want to confirm the health endpoint responds via the HTTP pipeline.
        await WaitForEndpointAsync(
            _commandApiClient,
            "/health",
            [HttpStatusCode.OK],
            TimeSpan.FromMinutes(3),
            TimeSpan.FromSeconds(3)).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        _commandApiClient?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        if (_builder is not null)
        {
            await _builder.DisposeAsync().ConfigureAwait(false);
        }

        Environment.SetEnvironmentVariable("EnableKeycloak", _previousEnableKeycloak);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _previousAspNetCoreEnvironment);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _previousDotNetEnvironment);
    }

    private static async Task WaitForEndpointAsync(
        HttpClient client,
        string url,
        IReadOnlyCollection<HttpStatusCode> expectedStatusCodes,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        Exception? lastException = null;
        HttpStatusCode? lastStatusCode = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using HttpResponseMessage response = await client
                    .GetAsync(url)
                    .ConfigureAwait(false);

                lastStatusCode = response.StatusCode;
                if (expectedStatusCodes.Contains(response.StatusCode))
                {
                    return;
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }
            catch (TaskCanceledException ex)
            {
                lastException = ex;
            }

            await Task.Delay(pollInterval).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Endpoint did not become ready within {timeout}. Url: {url}. "
            + $"Last status: {lastStatusCode?.ToString() ?? "n/a"}. "
            + $"Last error: {lastException?.Message ?? "n/a"}");
    }
}
