extern alias apphost;

using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Security.Claims;
using System.Text;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

using StackExchange.Redis;

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
/// <remarks>The ordinary profile retains unseeded defaults. The explicit complete mTLS profile
/// enables both optional services and qualifies the real subscription route. Its in-process
/// controlled host uses production subscription/access registrations without adding an endpoint.</remarks>
public class PartiesAspireTopologyFixture : IAsyncLifetime
{
    /// <summary>The symmetric signing key used only by this test topology and its gateway token.</summary>
    internal const string JwtSigningKey = "DevOnlySigningKey-AtLeast32Chars!";

    /// <summary>The issuer used only by this test topology and its gateway token.</summary>
    internal const string JwtIssuer = "hexalith-dev";

    private readonly Func<PartiesTopologySession, CancellationToken, Task<IDistributedApplicationTestingBuilder>> _createBuilder;
    private readonly bool _completeMtlsProfile;

    private readonly string _appChannelToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private readonly Func<DistributedApplication, CancellationToken, Task> _startApplication;
    private readonly bool _prepareCiBootstrap;
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _ciMtlsCertificateDirectory;
    private string? _ciMtlsBootstrapScript;
    private HttpClient? _partiesClient;
    private HttpClient? _eventStoreClient;
    private IResourceBuilder<ContainerResource>? _redis;

    /// <summary>Initializes the ordinary health-check topology.</summary>
    public PartiesAspireTopologyFixture() : this(completeMtlsProfile: false)
    {
    }

    /// <summary>Initializes a fixture that owns its complete disposable topology.</summary>
    internal PartiesAspireTopologyFixture(bool completeMtlsProfile, bool isolatedIdentities = false)
        : this(
            (session, cancellationToken) => DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                session.Arguments(completeMtlsProfile, File.Exists(Path.Combine(session.CertificateDirectory, "ca.crt"))), cancellationToken),
            (application, cancellationToken) => application.StartAsync(cancellationToken),
            prepareCiBootstrap: true,
            completeMtlsProfile: completeMtlsProfile,
            session: PartiesTopologySession.Create(isolatedIdentities))
    {
    }

    /// <summary>Uses controlled construction/startup seams for infrastructure failure tests.</summary>
    /// <param name="createBuilder">Constructs the application model.</param>
    /// <param name="startApplication">Starts the constructed application.</param>
    /// <param name="prepareCiBootstrap">Whether this fixture must prepare a real secure control plane.</param>
    internal PartiesAspireTopologyFixture(
        Func<PartiesTopologySession, CancellationToken, Task<IDistributedApplicationTestingBuilder>> createBuilder,
        Func<DistributedApplication, CancellationToken, Task> startApplication,
        bool prepareCiBootstrap = false,
        bool completeMtlsProfile = false,
        PartiesTopologySession? session = null)
    {
        Session = session ?? PartiesTopologySession.Create();
        _createBuilder = createBuilder;
        _startApplication = startApplication;
        _prepareCiBootstrap = prepareCiBootstrap;
        _completeMtlsProfile = completeMtlsProfile;
    }

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

    /// <summary>Gets this fixture's immutable private topology configuration.</summary>
    internal PartiesTopologySession Session { get; }

    /// <summary>Gets the controlled in-process host used to inspect production tenant-access effects.</summary>
    internal PartiesTenantBootstrapHost? TenantBootstrapHost { get; private set; }

    /// <summary>Gets the exact identities required by the complete qualification profile.</summary>
    internal static IReadOnlyList<string> CompleteSidecarIds { get; } =
        ["eventstore", "eventstore-admin", "eventstore-admin-ui", "memories", "parties", "sample", "sample-blazor-ui", "tenants"];

    /// <summary>Starts the topology with the current test's cancellation token.</summary>
    public ValueTask InitializeAsync() => InitializeAsync(TestContext.Current.CancellationToken);

    /// <summary>Starts the topology within a bounded lifetime linked to caller cancellation.</summary>
    internal async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 5-minute timeout for full Aspire topology startup including DAPR sidecar
            // initialization and health check stabilization.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(5));

            if (_prepareCiBootstrap)
            {
                await PrepareCiDaprMtlsAsync(cts.Token).ConfigureAwait(false);
            }

            _builder = await _createBuilder(Session, cts.Token).ConfigureAwait(false);

            _redis = PartiesTestRedis.Configure(_builder);
            if (_completeMtlsProfile)
            {
                PartiesMtlsProfile.ConfigureOptionalDependencies(_builder);
                IResourceBuilder<ParameterResource> token = _builder.AddParameter("fixture-parties-channel-token", _appChannelToken, secret: true);
                _ = _builder.CreateResourceBuilder<ProjectResource>("parties").WithEventStoreAppChannelToken(token);
            }

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

            _ = _builder.CreateResourceBuilder<ProjectResource>("eventstore")
                .WithEnvironment("EventStore__Actors__AggregateActorTypeName", "AggregateActor");
            foreach (ProjectResource project in _builder.Resources.OfType<ProjectResource>())
            {
                _ = _builder.CreateResourceBuilder(project)
                    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
                    .WithEnvironment("DOTNET_ENVIRONMENT", "Development");
            }

            _builder.Services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddFilter(_builder.Environment.ApplicationName, LogLevel.Debug);
                logging.AddFilter("Aspire.", LogLevel.Warning);
            });

            _app = await _builder.BuildAsync(cts.Token).ConfigureAwait(false);
            await _startApplication(_app, cts.Token).ConfigureAwait(false);
            if (_completeMtlsProfile)
            {
                // Keep the AppHost's explicit-start semantics and start the optional UI through Aspire.
                await _app.ResourceNotifications.WaitForResourceAsync("eventstore-admin-ui", KnownResourceStates.NotStarted, cts.Token)
                    .ConfigureAwait(false);
                ExecuteCommandResult start = await _app.Services.GetRequiredService<ResourceCommandService>()
                    .ExecuteCommandAsync("eventstore-admin-ui", KnownResourceCommands.StartCommand, cts.Token).ConfigureAwait(false);
                if (!start.Success)
                {
                    throw new InvalidOperationException("The explicit EventStore admin UI start command failed.");
                }
                await WaitForAllSidecarsAsync(cts.Token).ConfigureAwait(false);
            }

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
            // EventStore's operator API; shared developer Redis is never contacted.
            string sourceCommit = await ProjectionDeliveryWriterProtocolTestLease
                .ReadExactSourceCommitAsync(cts.Token)
                .ConfigureAwait(false);
            await ProjectionDeliveryWriterProtocolTestLease.ActivateAsync(
                $"{_redis.GetEndpoint("tcp").Host}:{_redis.GetEndpoint("tcp").Port}",
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
                TimeSpan.FromSeconds(3), cts.Token).ConfigureAwait(false);
            await WaitForEndpointAsync(
                _eventStoreClient,
                "/health",
                [HttpStatusCode.OK],
                TimeSpan.FromMinutes(3),
                TimeSpan.FromSeconds(3), cts.Token).ConfigureAwait(false);

            if (_completeMtlsProfile)
            {
                TenantBootstrapHost = await PartiesTenantBootstrapHost.StartAsync(_appChannelToken, cts.Token).ConfigureAwait(false);
                using HttpClient localClient = TenantBootstrapHost.CreateClient();
                await PartiesTenantBootstrapHost.SeedAsync(_partiesClient, _appChannelToken, cts.Token).ConfigureAwait(false);
                await PartiesTenantBootstrapHost.SeedAsync(localClient, _appChannelToken, cts.Token).ConfigureAwait(false);
            }
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            UnavailableReason = $"{ex.GetType().Name}: {ex.Message}";
            // Tear down partial startup immediately; no test can use an unavailable fixture.
            try
            {
                await DisposeApplicationAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                UnavailableReason += $"; topology cleanup failed: {cleanupException}";
            }
        }
    }

    /// <summary>Rejects tenant-dependent tests using the ordinary unseeded health profile.</summary>
    /// <remarks>Tenant bootstrap qualification uses the explicit complete mTLS profile and its
    /// registered subscription handler; ordinary shared health fixtures remain unseeded.</remarks>
    /// <exception cref="InvalidOperationException">The ordinary profile has no seeded tenant context.</exception>
    public static void RequireSeededTenants() => throw new InvalidOperationException(
        "The ordinary PartiesAspireTopologyFixture health profile does not seed tenants. "
        + "Use the explicit complete mTLS qualification profile and authenticated subscription "
        + "bootstrap when testing tenant access. Health/ready/alive endpoints do not require seeded tenants.");

    public async ValueTask DisposeAsync() => await DisposeApplicationAsync().ConfigureAwait(false);

    /// <summary>Creates an HTTP client for the named project's own Dapr metadata/invocation API.</summary>
    internal HttpClient CreateSidecarClient(string appId)
    {
        ProjectResource project = App.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<ProjectResource>().Single(resource => resource.Name == appId);
        DaprSidecarAnnotation sidecar = project.Annotations.OfType<DaprSidecarAnnotation>().Single();
        return App.CreateHttpClient(sidecar.Sidecar.Name + "-cli", "http");
    }

    /// <summary>Reads one exact private Redis key, never using scans or shared developer state.</summary>
    internal async Task<string?> ReadRedisDataAsync(string key, CancellationToken cancellationToken)
    {
        if (_redis is null) { throw new InvalidOperationException("The session has no owned Redis."); }
        using IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
        {
            EndPoints = { $"{_redis.GetEndpoint("tcp").Host}:{_redis.GetEndpoint("tcp").Port}" },
            ConnectTimeout = 5000, SyncTimeout = 5000, AbortOnConnectFail = true, AllowAdmin = false,
        }).WaitAsync(cancellationToken).ConfigureAwait(false);
        RedisValue value = await redis.GetDatabase().HashGetAsync(key, "data").WaitAsync(cancellationToken).ConfigureAwait(false);
        return value.IsNull ? null : value.ToString();
    }

    /// <summary>Reads actor-owned metadata and every exact event key independently of command status.</summary>
    internal async Task<(AggregateMetadata Metadata, EventEnvelope[] Events)> ReadStoredStreamAsync(AggregateIdentity identity, CancellationToken cancellationToken)
    {
        string prefix = $"{Session.AppId("eventstore")}||AggregateActor||{identity.ActorId}||";
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string metadataJson = await ReadRedisDataAsync(prefix + identity.MetadataKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The exact aggregate metadata key has no persisted bytes.");
        AggregateMetadata metadata = JsonSerializer.Deserialize<AggregateMetadata>(metadataJson, options)
            ?? throw new InvalidOperationException("The aggregate metadata could not be deserialized.");
        if (metadata.CurrentSequence < 1 || metadata.CurrentSequence > 100 || metadata.RetainedFloor != 1)
        {
            throw new InvalidOperationException("A fresh synthetic create must have a bounded, complete persisted stream.");
        }
        var events = new List<EventEnvelope>();
        for (long sequence = 1; sequence <= metadata.CurrentSequence; sequence++)
        {
            string json = await ReadRedisDataAsync(prefix + identity.EventStreamKeyPrefix + sequence, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("An exact aggregate event key has no persisted bytes.");
            events.Add(JsonSerializer.Deserialize<EventEnvelope>(json, options)
                ?? throw new InvalidOperationException("A persisted event envelope could not be deserialized."));
        }
        return (metadata, events.ToArray());
    }

    private async Task WaitForAllSidecarsAsync(CancellationToken cancellationToken)
    {
        string[] actual = App.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<ProjectResource>()
            .SelectMany(project => project.Annotations.OfType<DaprSidecarAnnotation>())
            .Select(sidecar => sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.AppId!)
            .Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(CompleteSidecarIds.Select(Session.AppId).Order(StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("The complete mTLS model does not contain the exact eight expected sidecars.");
        }
        foreach (string appId in CompleteSidecarIds)
        {
            using HttpClient client = CreateSidecarClient(appId);
            await WaitForEndpointAsync(client, "/v1.0/healthz", [HttpStatusCode.NoContent],
                TimeSpan.FromMinutes(2), TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            using JsonDocument metadata = await client.GetFromJsonAsync<JsonDocument>("/v1.0/metadata", cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Sidecar '{appId}' returned no metadata.");
            if (metadata.RootElement.GetProperty("id").GetString() != Session.AppId(appId))
            {
                throw new InvalidOperationException($"Sidecar '{appId}' returned another application's metadata.");
            }
        }
    }

    private async Task DisposeApplicationAsync()
    {
        _partiesClient?.Dispose();
        _eventStoreClient?.Dispose();
        await PartiesTopologyCleanup.RunAsync(
            async () =>
            {
                if (_app is not null)
                {
                    // Stop all workers and sidecars before disposal removes the owned Redis container.
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await _app.StopAsync(timeout.Token).ConfigureAwait(false);
                }
            },
            async () =>
            {
                if (_app is not null)
                {
                    await _app.DisposeAsync().ConfigureAwait(false);
                    _app = null;
                }
            },
            async () =>
            {
                if (_builder is not null)
                {
                    await _builder.DisposeAsync().ConfigureAwait(false);
                    _builder = null;
                }
            },
            CleanupCiDaprMtlsAsync,
            async () =>
            {
                if (TenantBootstrapHost is not null)
                {
                    await TenantBootstrapHost.DisposeAsync().ConfigureAwait(false);
                    TenantBootstrapHost = null;
                }
            }).ConfigureAwait(false);
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
        if (!_completeMtlsProfile && (!OperatingSystem.IsLinux()
                || !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)))
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

        string certificateDirectory = Session.CertificateDirectory;
        _ciMtlsCertificateDirectory = certificateDirectory;
        _ciMtlsBootstrapScript = script;
        await RunCiDaprBootstrapScriptAsync(script, certificateDirectory, "--prepare-only", cancellationToken)
            .ConfigureAwait(false);

    }

    private async Task CleanupCiDaprMtlsAsync()
    {
        if (_ciMtlsCertificateDirectory is not { } certificateDirectory
            || _ciMtlsBootstrapScript is not { } script)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await RunCiDaprBootstrapScriptAsync(script, certificateDirectory, "--cleanup-only", timeout.Token)
            .ConfigureAwait(false);
        if (Directory.Exists(Session.Root))
        {
            Directory.Delete(Session.Root, recursive: true);
        }

        // Retain ownership evidence when cleanup fails so disposal can retry safely.
        _ciMtlsCertificateDirectory = null;
        _ciMtlsBootstrapScript = null;
    }

    private async Task RunCiDaprBootstrapScriptAsync(
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
        Session.ConfigureBootstrapEnvironment(start.Environment);

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

    /// <summary>Polls readiness while honoring cancellation of an in-flight HTTP request.</summary>
    internal static async Task WaitForEndpointAsync(
        HttpClient client,
        string url,
        IReadOnlyCollection<HttpStatusCode> expectedStatusCodes,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        Exception? lastException = null;
        HttpStatusCode? lastStatusCode = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using HttpResponseMessage response = await client
                    .GetAsync(url, cancellationToken)
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
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
            }

            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Endpoint did not become ready within {timeout}. Url: {url}. "
            + $"Last status: {lastStatusCode?.ToString() ?? "n/a"}. "
            + $"Last error: {lastException?.Message ?? "n/a"}");
    }
}
