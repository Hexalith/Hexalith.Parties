extern alias apphost;

using System.Net;

using Aspire.Hosting;
using Aspire.Hosting.Testing;

using Hexalith.Parties.IntegrationTests.Tenants;
using Hexalith.Tenants.Contracts.Enums;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>
/// Shared fixture that starts the full Parties Aspire topology (Parties service + DAPR sidecar
/// + in-memory state store/pub/sub) for Tier 3 health endpoint E2E tests.
/// Keycloak is disabled for fast startup; uses symmetric key JWT auth.
/// When the infrastructure is unavailable (no Docker, no DAPR), the fixture
/// captures the failure and exposes <see cref="IsAvailable"/> so tests can skip gracefully.
/// </summary>
public class PartiesAspireTopologyFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private string? _previousAspNetCoreEnvironment;
    private string? _previousDotNetEnvironment;
    private HttpClient? _partiesClient;

    /// <summary>
    /// Gets a value indicating whether the Aspire topology started successfully.
    /// Tests should check this and skip when false.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Gets the initialization failure reason, if any.
    /// </summary>
    public string? UnavailableReason { get; private set; }

    /// <summary>
    /// Gets the HTTP client for the Parties service.
    /// Available after <see cref="InitializeAsync"/> completes with <see cref="IsAvailable"/> = true.
    /// </summary>
    public HttpClient PartiesClient => _partiesClient ?? throw new InvalidOperationException(
        IsAvailable
            ? "Test infrastructure not initialized. Ensure InitializeAsync has completed."
            : $"Test infrastructure unavailable: {UnavailableReason}");

    /// <summary>
    /// Gets the running Aspire distributed application instance.
    /// </summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException(
        IsAvailable
            ? "Test infrastructure not initialized. Ensure InitializeAsync has completed."
            : $"Test infrastructure unavailable: {UnavailableReason}");

    public async Task InitializeAsync()
    {
        // Disable Keycloak for health check tests — symmetric key JWT auth is sufficient.
        _previousEnableKeycloak = Environment.GetEnvironmentVariable("EnableKeycloak");
        Environment.SetEnvironmentVariable("EnableKeycloak", "false");

        // Force Development environment so Parties service loads appsettings.Development.json.
        _previousAspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        _previousDotNetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        // Reset per-tenant sequence counters so a re-init in the same test process starts
        // at sequence 1 again (the local Tenants projection accepts only strictly-increasing
        // sequence numbers, so leftover counter state from a previous fixture run can cause
        // the first seeded event to be silently rejected).
        TenantIntegrationTestSeeder.ResetSequenceCounters();

        try
        {
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

            // Wait for parties to be healthy. This includes the DAPR health checks
            // (sidecar, state store, pub/sub) becoming healthy after sidecar initialization.
            await _app.ResourceNotifications
                .WaitForResourceHealthyAsync("parties", cts.Token)
                .WaitAsync(TimeSpan.FromMinutes(5), cts.Token)
                .ConfigureAwait(false);

            _partiesClient = _app.CreateHttpClient("parties");
            _partiesClient.Timeout = TimeSpan.FromSeconds(60);

            // Wait for the /health endpoint to actually return 200 via HTTP.
            await WaitForEndpointAsync(
                _partiesClient,
                "/health",
                [HttpStatusCode.OK],
                TimeSpan.FromMinutes(3),
                TimeSpan.FromSeconds(3)).ConfigureAwait(false);

            await SeedDefaultTenantAccessAsync(cts.Token).ConfigureAwait(false);

            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            UnavailableReason = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    public Task SeedTenantAsync(
        string tenantId,
        string userId,
        TenantRole role,
        CancellationToken cancellationToken = default)
        => TenantIntegrationTestSeeder.SeedActiveTenantAsync(
            PartiesClient,
            tenantId,
            [new TenantMemberSeed(tenantId, userId, role)],
            cancellationToken);

    public Task DisableTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
        => TenantIntegrationTestSeeder.DisableTenantAsync(PartiesClient, tenantId, cancellationToken);

    public Task RemoveUserFromTenantAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default)
        => TenantIntegrationTestSeeder.RemoveUserFromTenantAsync(PartiesClient, tenantId, userId, cancellationToken);

    /// <summary>
    /// Pre-seeds tenant access state so unrelated E2E tests in this fixture (search,
    /// temporal-name, consent, encryption, erasure, admin) do not all need to seed
    /// their own users before exercising tenant-scoped endpoints.
    /// <para>
    /// <b>Known coupling:</b> this list is hand-maintained against the user ids each
    /// E2E test class expects. Adding a new E2E test that uses a new user id requires
    /// either adding it here or seeding it in the test class itself. A future refactor
    /// (deferred-work.md, Story 11-4 review item D8) should move this seeding into
    /// each E2E class so the fixture is no longer the implicit coupling point.
    /// </para>
    /// </summary>
    private async Task SeedDefaultTenantAccessAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await TenantIntegrationTestSeeder.SeedActiveTenantAsync(
                PartiesClient,
                "tenant-a",
                [
                    new TenantMemberSeed("tenant-a", "e2e-test-admin", TenantRole.TenantOwner),
                ],
                cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            await TenantIntegrationTestSeeder.SeedActiveTenantAsync(
                PartiesClient,
                "e2e-tenant",
                [
                    new TenantMemberSeed("e2e-tenant", "e2e-test-user", TenantRole.TenantOwner),
                    new TenantMemberSeed("e2e-tenant", "e2e-search-test", TenantRole.TenantOwner),
                    new TenantMemberSeed("e2e-tenant", "e2e-temporal-name-test", TenantRole.TenantOwner),
                    new TenantMemberSeed("e2e-tenant", "e2e-consent-test", TenantRole.TenantOwner),
                    new TenantMemberSeed("e2e-tenant", "e2e-encryption-test", TenantRole.TenantOwner),
                    new TenantMemberSeed("e2e-tenant", "e2e-erasure-test", TenantRole.TenantOwner),
                ],
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Surface the seeding failure as fixture-unavailable so dependent tests skip
            // instead of producing misleading authorization failures downstream. If the
            // seeder fell back to a random JWT signing key, every PublishTenantEventAsync
            // would have produced 401s — name that explicitly so the failure mode is obvious.
            string signingKeyHint = TenantIntegrationTestSeeder.SigningKeyIsRandomFallback
                ? " (signing key fell back to a per-process random value because no env var or appsettings.Development.json key was found; tokens cannot be validated by the running Parties service)"
                : string.Empty;

            throw new InvalidOperationException(
                $"Failed to seed default Tenants access state for the Aspire topology fixture{signingKeyHint}. " +
                $"Cause: {ex.GetType().Name}: {ex.Message}",
                ex);
        }
    }

    public async Task DisposeAsync()
    {
        _partiesClient?.Dispose();

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
