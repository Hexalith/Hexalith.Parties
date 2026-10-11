extern alias apphost;

using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>
/// Shared fixture that starts the full Parties Aspire topology (Parties service + DAPR sidecar
/// + in-memory state store/pub/sub) for Tier 3 health endpoint E2E tests.
/// Keycloak is disabled for fast startup; uses symmetric key JWT auth.
/// When the infrastructure is unavailable (no Docker, no DAPR), the fixture
/// captures the failure and exposes <see cref="IsAvailable"/> so tests can skip gracefully.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This fixture does NOT seed any tenants.</strong> Story 12.2 retired the in-process
/// REST/MCP surface and the integration test project Compile-Removes
/// <c>Tenants/**/*.cs</c> (including <c>TenantIntegrationTestSeeder</c>). All current consumers
/// of this fixture exercise only the health/ready/alive endpoints, which do not require an
/// authenticated tenant.
/// </para>
/// <para>
/// If you add a test that requires tenant access (a tenant-scoped projection, an RBAC-protected
/// route, an authenticated query), call <see cref="RequireSeededTenants"/> as a precondition so
/// the failure is loud and self-explanatory instead of a silent 401/403. You will also need to
/// reinstate the seeder before that test can pass.
/// </para>
/// </remarks>
public class PartiesAspireTopologyFixture : IAsyncLifetime
{
    /// <summary>The symmetric signing key used only by this test topology and its gateway token.</summary>
    internal const string JwtSigningKey = "DevOnlySigningKey-AtLeast32Chars!";

    /// <summary>The issuer used only by this test topology and its gateway token.</summary>
    internal const string JwtIssuer = "hexalith-dev";

    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private string? _previousAspNetCoreEnvironment;
    private string? _previousDotNetEnvironment;
    private string? _previousDaprMtlsEnabled;
    private string? _previousDaprMtlsCertificateDirectory;
    private string? _ciMtlsCertificateDirectory;
    private string? _ciMtlsBootstrapScript;
    private HttpClient? _partiesClient;
    private HttpClient? _eventStoreClient;
    private readonly ProjectionDeliveryWriterProtocolTestLease _writerProtocolLease = new();

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
    /// Gets the HTTP client for the EventStore public gateway.
    /// Available after <see cref="InitializeAsync"/> completes with <see cref="IsAvailable"/> = true.
    /// </summary>
    public HttpClient EventStoreClient => _eventStoreClient ?? throw new InvalidOperationException(
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

    public async ValueTask InitializeAsync()
    {
        // Disable Keycloak for health check tests — symmetric key JWT auth is sufficient.
        _previousEnableKeycloak = Environment.GetEnvironmentVariable("EnableKeycloak");
        Environment.SetEnvironmentVariable("EnableKeycloak", "false");

        // Force Development environment so Parties service loads appsettings.Development.json.
        _previousAspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        _previousDotNetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        _previousDaprMtlsEnabled = Environment.GetEnvironmentVariable("Dapr__Mtls__Enabled");
        _previousDaprMtlsCertificateDirectory = Environment.GetEnvironmentVariable("Dapr__Mtls__CertificateDirectory");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        try
        {
            // 5-minute timeout for full Aspire topology startup including DAPR sidecar
            // initialization and health check stabilization.
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            await PrepareCiDaprMtlsAsync(cts.Token).ConfigureAwait(false);

            _builder = await DistributedApplicationTestingBuilder
                .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>()
                .ConfigureAwait(false);

            // The normal AppHost clears symmetric keys for its OIDC topology. Override
            // the test resources after model construction when Keycloak is disabled.
            foreach (string resourceName in new[] { "eventstore", "eventstore-admin", "parties", "parties-mcp", "tenants" })
            {
                _ = _builder.CreateResourceBuilder<ProjectResource>(resourceName)
                    .WithEnvironment("Authentication__JwtBearer__Authority", string.Empty)
                    .WithEnvironment("Authentication__JwtBearer__SigningKey", JwtSigningKey)
                    .WithEnvironment("Authentication__JwtBearer__Issuer", JwtIssuer)
                    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false");
            }

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
            await _app.ResourceNotifications
                .WaitForResourceHealthyAsync("eventstore", cts.Token)
                .WaitAsync(TimeSpan.FromMinutes(5), cts.Token)
                .ConfigureAwait(false);

            _partiesClient = _app.CreateHttpClient("parties");
            _partiesClient.Timeout = TimeSpan.FromSeconds(60);
            _eventStoreClient = _app.CreateHttpClient("eventstore");
            _eventStoreClient.Timeout = TimeSpan.FromSeconds(60);

            // The disposable topology starts with no v2 writer cutover. Activate through
            // EventStore's operator API and restore the previous Redis marker on disposal.
            string sourceCommit = await ProjectionDeliveryWriterProtocolTestLease
                .ReadExactSourceCommitAsync(cts.Token)
                .ConfigureAwait(false);
            await _writerProtocolLease.ActivateAsync(
                _eventStoreClient,
                CreateAdministratorToken(),
                sourceCommit,
                "disposable-parties-aspire-fixture-backup",
                cts.Token).ConfigureAwait(false);

            // Wait for the /health endpoint to actually return 200 via HTTP.
            await WaitForEndpointAsync(
                _partiesClient,
                "/health",
                [HttpStatusCode.OK],
                TimeSpan.FromMinutes(3),
                TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            await WaitForEndpointAsync(
                _eventStoreClient,
                "/health",
                [HttpStatusCode.OK],
                TimeSpan.FromMinutes(3),
                TimeSpan.FromSeconds(3)).ConfigureAwait(false);

            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            UnavailableReason = $"{ex.GetType().Name}: {ex.Message}";
            try
            {
                await _writerProtocolLease.RestoreAsync().ConfigureAwait(false);
            }
            catch (Exception restorationException)
            {
                UnavailableReason += $"; marker restoration failed: {restorationException}";
            }
        }
    }

    /// <summary>
    /// Precondition guard for tests that require seeded tenant access. Always throws because
    /// Story 12.2 retired the in-process tenant-seeding plumbing. Call this at the top of any
    /// new test that depends on tenant context so the failure is explicit.
    /// </summary>
    /// <exception cref="InvalidOperationException">Always thrown.</exception>
    public static void RequireSeededTenants() => throw new InvalidOperationException(
        "PartiesAspireTopologyFixture does not seed tenants. Story 12.2 retired the in-process "
        + "REST/MCP surface and TenantIntegrationTestSeeder is Compile-Removed in the integration "
        + "test project (Tenants/**/*.cs). Reinstate the seeder and update this fixture if your "
        + "test needs authenticated tenant access. Health/ready/alive endpoints do not require "
        + "seeded tenants.");

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _writerProtocolLease.RestoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _partiesClient?.Dispose();
            _eventStoreClient?.Dispose();
            try
            {
                if (_app is not null)
                {
                    await _app.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                try
                {
                    if (_builder is not null)
                    {
                        await _builder.DisposeAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    try
                    {
                        await CleanupCiDaprMtlsAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        Environment.SetEnvironmentVariable("EnableKeycloak", _previousEnableKeycloak);
                        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _previousAspNetCoreEnvironment);
                        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _previousDotNetEnvironment);
                        Environment.SetEnvironmentVariable("Dapr__Mtls__Enabled", _previousDaprMtlsEnabled);
                        Environment.SetEnvironmentVariable("Dapr__Mtls__CertificateDirectory", _previousDaprMtlsCertificateDirectory);
                    }
                }
            }
        }
    }

    private static string CreateAdministratorToken()
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new(ClaimTypes.Role, "GlobalAdministrator")]),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(30),
            IssuedAt = DateTime.UtcNow,
            Issuer = JwtIssuer,
            Audience = "hexalith-eventstore",
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
                SecurityAlgorithms.HmacSha256Signature),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private async Task PrepareCiDaprMtlsAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux()
            || !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("Dapr__Mtls__Enabled"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null
            && !File.Exists(Path.Combine(repository.FullName, "scripts", "aspire-start-mtls.sh")))
        {
            repository = repository.Parent;
        }

        string repositoryRoot = repository?.FullName
            ?? throw new FileNotFoundException("The CI Dapr mTLS bootstrap script was not found above the test output directory.");
        string script = Path.Combine(repositoryRoot, "scripts", "aspire-start-mtls.sh");

        string certificateDirectory = Path.Combine(
            Path.GetTempPath(),
            $"hexalith-parties-ci-dapr-certs-{Guid.NewGuid():N}");
        _ciMtlsCertificateDirectory = certificateDirectory;
        _ciMtlsBootstrapScript = script;
        await RunCiDaprBootstrapScriptAsync(script, certificateDirectory, "--prepare-only", cancellationToken)
            .ConfigureAwait(false);

        Environment.SetEnvironmentVariable("Dapr__Mtls__Enabled", "true");
        Environment.SetEnvironmentVariable("Dapr__Mtls__CertificateDirectory", certificateDirectory);
    }

    private async Task CleanupCiDaprMtlsAsync()
    {
        if (_ciMtlsCertificateDirectory is not { } certificateDirectory
            || _ciMtlsBootstrapScript is not { } script)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await RunCiDaprBootstrapScriptAsync(script, certificateDirectory, "--cleanup-only", timeout.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(certificateDirectory))
            {
                Directory.Delete(certificateDirectory, recursive: true);
            }
        }
    }

    private static async Task RunCiDaprBootstrapScriptAsync(
        string script,
        string certificateDirectory,
        string option,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new("bash")
        {
            WorkingDirectory = Path.GetDirectoryName(Path.GetDirectoryName(script)!)!,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(script);
        start.ArgumentList.Add(option);
        start.Environment["DAPR_MTLS_CERTIFICATE_DIRECTORY"] = certificateDirectory;

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The CI Dapr mTLS bootstrap could not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }

        string stdout = await output.ConfigureAwait(false);
        string stderr = await error.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The CI Dapr mTLS bootstrap {option} exited {process.ExitCode}: {stderr}{stdout}");
        }
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
