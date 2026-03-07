using System.Net;
using System.Text;

using Aspire.Hosting.ApplicationModel;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>
/// Tier 3 end-to-end health endpoint tests running against the full Aspire topology.
/// Validates that health, readiness, and liveness endpoints behave correctly with
/// real DAPR sidecar, state store, and pub/sub infrastructure.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("PartiesAspireTopology")]
public sealed class HealthEndpointE2ETests
{
    /// <summary>
    /// Resource names observed for the DAPR sidecar attached to CommandApi.
    /// Aspire exposes the sidecar as a logical resource and a runnable CLI-backed resource.
    /// </summary>
    private static readonly string[] s_sidecarResourceNames = ["commandapi-dapr-cli", "commandapi-dapr"];

    private readonly PartiesAspireTopologyFixture _fixture;

    public HealthEndpointE2ETests(PartiesAspireTopologyFixture fixture)
    {
        _fixture = fixture;
    }

    // ------------------------------------------------------------------
    // Task 7, Subtask 1: Full topology — /health returns healthy
    // ------------------------------------------------------------------

    [Fact]
    public async Task HealthEndpoint_WithAllDaprComponentsRunning_Returns200Async()
    {
        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .GetAsync("/health");

        // Assert: composite health status should be Healthy (200)
        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            "All DAPR components are running — /health should return 200 (Healthy).");
    }

    [Fact]
    public async Task ReadyEndpoint_WithAllDaprComponentsRunning_Returns200Async()
    {
        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .GetAsync("/ready");

        // Assert: readiness gated on sidecar + state store "ready" tags should pass
        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            "All DAPR components are running — /ready should return 200.");
    }

    [Fact]
    public async Task AliveEndpoint_WithAllDaprComponentsRunning_Returns200Async()
    {
        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .GetAsync("/alive");

        // Assert: liveness is always 200 when the process is running
        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            "/alive should always return 200 when the service process is running.");
    }

    [Fact]
    public async Task HealthEndpoint_WithAllDaprComponentsRunning_DoesNotIncludeDegradationHeadersAsync()
    {
        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .GetAsync("/health");

        // Assert: no degradation headers when everything is healthy
        response.Headers.Contains("X-Service-Degraded").ShouldBeFalse(
            "Healthy topology should not include X-Service-Degraded header.");
    }

    // ------------------------------------------------------------------
    // Task 7, Subtask 2: DAPR sidecar stopped — /health and /ready return 503,
    // then recover after restart (AC #1, #5, partial #6)
    // ------------------------------------------------------------------
    // Uses Aspire 13.x ResourceCommandService to stop and restart the sidecar
    // resource during the running topology.

    [Fact]
    public async Task HealthAndReadyEndpoints_WithDaprSidecarStopped_Return503ThenRecoverAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        // Arrange: Stop the DAPR sidecar via Aspire resource commands
        string sidecarResourceName = await ExecuteSidecarCommandAsync(KnownResourceCommands.StopCommand, cts.Token);

        // Wait for /health to detect the sidecar failure (health check timeout is 3s)
        await WaitForEndpointStatusAsync(
            "/health",
            HttpStatusCode.ServiceUnavailable,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(2));

        // Assert: /health returns 503 (sidecar check reports Unhealthy)
        using (HttpResponseMessage healthResponse = await _fixture.CommandApiClient.GetAsync("/health"))
        {
            healthResponse.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable,
                "DAPR sidecar is stopped — /health should return 503 (Unhealthy).");
        }

        // Assert: /ready returns 503 (sidecar check is tagged "ready")
        using (HttpResponseMessage readyResponse = await _fixture.CommandApiClient.GetAsync("/ready"))
        {
            readyResponse.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable,
                "DAPR sidecar is stopped — /ready should return 503.");
        }

        // Act: Restart the sidecar and verify recovery (AC #6 crash recovery behavior)
        await ExecuteSidecarCommandAsync(KnownResourceCommands.StartCommand, cts.Token);

        // Wait for sidecar resource to reach Running state in Aspire
        // (DaprSidecarResource has no HealthCheckAnnotation, so "healthy" = Running)
        await _fixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync(sidecarResourceName, cts.Token)
            .WaitAsync(TimeSpan.FromMinutes(2), cts.Token);

        // Wait for /health to recover to 200 via the HTTP pipeline
        await WaitForEndpointStatusAsync(
            "/health",
            HttpStatusCode.OK,
            TimeSpan.FromMinutes(2),
            TimeSpan.FromSeconds(3));

        // Assert: /health recovers after sidecar restart
        using (HttpResponseMessage recoveryResponse = await _fixture.CommandApiClient.GetAsync("/health"))
        {
            recoveryResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
                "After sidecar restart, /health should recover to 200.");
        }
    }

    // ------------------------------------------------------------------
    // Task 7, Subtask 3: Pub/sub unavailable — cached data with degradation headers
    // ------------------------------------------------------------------
    // In the Aspire topology, state store and pub/sub both use in-memory DAPR
    // components running inside the same sidecar process. Pub/sub cannot be
    // independently failed without also losing the state store, which changes
    // the composite status from Degraded (200 + headers) to Unhealthy (503).
    // ResourceCommandService can stop/start resources, but only at the sidecar
    // level — there is no per-component control for in-memory DAPR components.
    // This degradation scenario is thoroughly covered by:
    //   - Tier 1: DegradedResponseMiddlewareTests (mock HealthCheckService returns Degraded)
    //   - Tier 2: HealthEndpointIntegrationTests (WebApplicationFactory with degraded mocks)

    [Fact(Skip = "In-memory DAPR components share the sidecar process — pub/sub cannot be failed "
        + "independently of the state store. Degradation header scenarios are covered by "
        + "Tier 1 (DegradedResponseMiddlewareTests) and Tier 2 (HealthEndpointIntegrationTests).")]
    public void QueryEndpoints_WithPubSubUnavailable_ReturnCachedDataWithDegradationHeaders()
    {
    }

    /// <summary>
    /// Polls an endpoint until it returns the expected HTTP status code.
    /// </summary>
    private async Task WaitForEndpointStatusAsync(
        string url,
        HttpStatusCode expectedStatus,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        HttpStatusCode? lastStatus = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using HttpResponseMessage response = await _fixture.CommandApiClient
                    .GetAsync(url)
                    .ConfigureAwait(false);

                lastStatus = response.StatusCode;
                if (response.StatusCode == expectedStatus)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Expected during sidecar transitions
            }
            catch (TaskCanceledException)
            {
                // Timeout during transitions
            }

            await Task.Delay(pollInterval).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Endpoint {url} did not reach {expectedStatus} within {timeout}. "
            + $"Last status: {lastStatus?.ToString() ?? "n/a"}");
    }

    private async Task<string> ExecuteSidecarCommandAsync(string commandName, CancellationToken cancellationToken)
    {
        var failures = new StringBuilder();

        foreach (string resourceName in s_sidecarResourceNames)
        {
            try
            {
                await _fixture.App.ResourceCommands
                    .ExecuteCommandAsync(resourceName, commandName, cancellationToken)
                    .ConfigureAwait(false);
                return resourceName;
            }
            catch (Exception ex)
            {
                _ = failures.AppendLine($"- {resourceName}: {ex.Message}");
            }
        }

        throw new InvalidOperationException(
            $"Unable to execute sidecar command '{commandName}' for any known resource name.{Environment.NewLine}{failures}");
    }
}
