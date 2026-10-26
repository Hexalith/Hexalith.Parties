extern alias apphost;

using System.Collections.Concurrent;
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
using Microsoft.Extensions.Options;
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
    private readonly bool _memoriesReadinessScope;
    private readonly IReadOnlyDictionary<string, string>? _sidecarApiTokens;

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
    /// <summary>The inherited marker used only by this fixture's worker lifetimes.</summary>
    internal const string WorkerOwnershipVariable = "PARTIES_TEST_TOPOLOGY_OWNER";

    /// <summary>Gets this fixture lifetime's unguessable inherited ownership marker.</summary>
    internal string WorkerOwnershipMarker { get; } = Guid.NewGuid().ToString("N");

    private readonly ConcurrentDictionary<int, long> _ownedWorkers = [];
    private CancellationTokenSource? _workerObservationCancellation;
    private Task? _workerObservation;
    private string[]? _workerResourceNames;
    private readonly HashSet<string> _stoppedWorkerNames = new(StringComparer.Ordinal);
    private bool _workersQuiesced;
    private bool _startupScheduled;

    /// <summary>Observes worker exit while backing containers and control-plane ownership still exist.</summary>
    internal Func<DistributedApplication, CancellationToken, Task>? ObserveQuiescenceAsync { get; set; }

    /// <summary>Captures private startup evidence before owned partial-startup teardown.</summary>
    internal Func<Exception, Task>? ObserveInitializationFailureAsync { get; set; }

    /// <summary>Gets the exact bounded initialization step for private failure evidence.</summary>
    internal string InitializationPhase { get; private set; } = "not-started";

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
        PartiesTopologySession? session = null,
        bool memoriesReadinessScope = false,
        IReadOnlyDictionary<string, string>? sidecarApiTokens = null)
    {
        Session = session ?? PartiesTopologySession.Create();
        _createBuilder = createBuilder;
        _startApplication = startApplication;
        _prepareCiBootstrap = prepareCiBootstrap;
        _completeMtlsProfile = completeMtlsProfile;
        _memoriesReadinessScope = memoriesReadinessScope;
        _sidecarApiTokens = sidecarApiTokens;
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
                InitializationPhase = "owned-control-plane-bootstrap";
                await PrepareCiDaprMtlsAsync(cts.Token).ConfigureAwait(false);
            }

            InitializationPhase = "create-builder";
            _builder = await _createBuilder(Session, cts.Token).ConfigureAwait(false);

            PartiesMtlsProfile.ConfigureInternalGrpcEndpoints(_builder, Session);
            _redis = PartiesTestRedis.Configure(_builder);
            if (_completeMtlsProfile)
            {
                PartiesMtlsProfile.ConfigureOptionalDependencies(_builder);
                IResourceBuilder<ParameterResource> token = _builder.AddParameter("fixture-parties-channel-token", _appChannelToken, secret: true);
                _ = _builder.CreateResourceBuilder<ProjectResource>("parties").WithEventStoreAppChannelToken(token);
                IResourceBuilder<ParameterResource> sampleToken = _builder.AddParameter("fixture-sample-channel-token",
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), secret: true);
                _ = _builder.CreateResourceBuilder<ProjectResource>("sample").WithEventStoreAppChannelToken(sampleToken);
            }

            // The normal AppHost clears symmetric keys for its OIDC topology. Override
            // the test resources after model construction when Keycloak is disabled.
            foreach (string resourceName in new[] { "eventstore", "eventstore-admin", "parties", "parties-mcp", "tenants", "sample" })
            {
                if (resourceName == "sample" && !_completeMtlsProfile)
                {
                    continue;
                }
                _ = _builder.CreateResourceBuilder<ProjectResource>(resourceName)
                    .WithEnvironment("Authentication__JwtBearer__Authority", string.Empty)
                    .WithEnvironment("Authentication__JwtBearer__SigningKey", JwtSigningKey)
                    .WithEnvironment("Authentication__JwtBearer__Issuer", JwtIssuer)
                    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false");
            }

            if (_completeMtlsProfile)
            {
                // Sample commands/queries use the same real workload verifier as the domain hosts.
                // Accept only this fixture's gateway, whose workload id is session-suffixed.
                _ = _builder.CreateResourceBuilder<ProjectResource>("sample")
                    .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
                    .WithEnvironment("Authentication__Workload__AllowedCallers__0", Session.AppId("eventstore"));
                // Exercise the UI-owned bearer acquisition and SDK handlers against this
                // fixture's real protected APIs; development keys remain scoped to test resources.
                foreach (string ui in new[] { "eventstore-admin-ui", "sample-blazor-ui" })
                {
                    _ = _builder.CreateResourceBuilder<ProjectResource>(ui)
                        .WithEnvironment("EventStore__Authentication__Authority", string.Empty)
                        .WithEnvironment("EventStore__Authentication__SigningKey", JwtSigningKey)
                        .WithEnvironment("EventStore__Authentication__Issuer", JwtIssuer)
                        .WithEnvironment("EventStore__Authentication__Audience", "hexalith-eventstore")
                        .WithEnvironment("EventStore__Authentication__Subject", PartiesTenantBootstrapHost.UserId)
                        .WithEnvironment("EventStore__Authentication__Tenants__0", PartiesTenantBootstrapHost.TenantId)
                        .WithEnvironment("EventStore__Counter__TenantId", PartiesTenantBootstrapHost.TenantId)
                        .WithEnvironment("EventStore__Authentication__Domains__0", "counter")
                        .WithEnvironment("EventStore__Authentication__Permissions__0", "admin:read")
                        .WithEnvironment("EventStore__Authentication__Permissions__1", "query:read");
                }
            }

            _ = _builder.CreateResourceBuilder<ProjectResource>("eventstore")
                .WithEnvironment("EventStore__Actors__AggregateActorTypeName", "AggregateActor");
            foreach (ProjectResource project in _builder.Resources.OfType<ProjectResource>())
            {
                _ = _builder.CreateResourceBuilder(project)
                    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
                    .WithEnvironment("DOTNET_ENVIRONMENT", "Development");
            }

            foreach (IResource resource in _builder.Resources.Cast<IResource>().Concat(
                _builder.Resources.SelectMany(resource => resource.Annotations.OfType<DaprSidecarAnnotation>())
                    .Select(annotation => (IResource)annotation.Sidecar)))
            {
                if (resource is ProjectResource or ExecutableResource or IDaprSidecarResource)
                {
                    resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
                        context.EnvironmentVariables[WorkerOwnershipVariable] = WorkerOwnershipMarker));
                }
            }

            _builder.Services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddFilter(_builder.Environment.ApplicationName, LogLevel.Debug);
                logging.AddFilter("Aspire.", LogLevel.Warning);
            });

            _builder.Eventing.Subscribe<BeforeStartEvent>((_, _) =>
            {
                _startupScheduled = true;
                if (_app is not null && !_workersQuiesced && _workerObservation is null)
                {
                    _workerObservationCancellation = new CancellationTokenSource();
                    _workerObservation = ObserveOwnedWorkersAsync(_app, _workerObservationCancellation.Token);
                }
                return Task.CompletedTask;
            });
            InitializationPhase = "build-application";
            _app = await _builder.BuildAsync(cts.Token).ConfigureAwait(false);
            InitializationPhase = "start-application";
            await _startApplication(_app, cts.Token).ConfigureAwait(false);
            if (_completeMtlsProfile && !_memoriesReadinessScope)
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

            if (_memoriesReadinessScope)
            {
                // This explicitly named scope qualifies Memories workflow/persistence only.
                // The ordinary and complete topology paths retain their Parties/Tenants gates.
                InitializationPhase = "target-sidecars";
                await WaitForSidecarsAsync(["eventstore", "memories", "memories-domain-service"], cts.Token).ConfigureAwait(false);
                InitializationPhase = "eventstore-alive";
                _eventStoreClient = _app.CreateHttpClient("eventstore");
                _eventStoreClient.Timeout = TimeSpan.FromSeconds(60);
                await WaitForEndpointAsync(_eventStoreClient, "/alive", [HttpStatusCode.OK], TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(3), cts.Token).ConfigureAwait(false);
                InitializationPhase = "writer-protocol-activation";
                string selectedCommit = await ProjectionDeliveryWriterProtocolTestLease.ReadExactSourceCommitAsync(cts.Token).ConfigureAwait(false);
                await ProjectionDeliveryWriterProtocolTestLease.ActivateAsync(
                    $"{_redis.GetEndpoint("tcp").Host}:{_redis.GetEndpoint("tcp").Port}", _eventStoreClient, CreateAdministratorToken(),
                    selectedCommit, "disposable-memories-workflow-fixture-backup", cts.Token).ConfigureAwait(false);
                InitializationPhase = "eventstore-resource-health";
                await _app.ResourceNotifications.WaitForResourceHealthyAsync("eventstore", cts.Token).ConfigureAwait(false);
                InitializationPhase = "memories-resource-health";
                await _app.ResourceNotifications.WaitForResourceHealthyAsync("memories", cts.Token).ConfigureAwait(false);
                InitializationPhase = "memories-domain-service-resource-health";
                await _app.ResourceNotifications.WaitForResourceHealthyAsync("memories-domain-service", cts.Token).ConfigureAwait(false);
                using HttpClient domainService = _app.CreateHttpClient("memories-domain-service");
                using HttpClient memories = _app.CreateHttpClient("memories");
                InitializationPhase = "target-ready-endpoints";
                await WaitForEndpointAsync(_eventStoreClient, "/ready", [HttpStatusCode.OK], TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(3), cts.Token).ConfigureAwait(false);
                await WaitForEndpointAsync(memories, "/ready", [HttpStatusCode.OK], TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(3), cts.Token).ConfigureAwait(false);
                await WaitForEndpointAsync(domainService, "/ready", [HttpStatusCode.OK], TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(3), cts.Token).ConfigureAwait(false);
                IsAvailable = true;
                return;
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
            if (ObserveInitializationFailureAsync is not null)
            {
                await ObserveInitializationFailureAsync(ex).ConfigureAwait(false);
            }
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
        await WaitForSidecarsAsync(CompleteSidecarIds, cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForSidecarsAsync(IReadOnlyList<string> appIds, CancellationToken cancellationToken)
    {
        foreach (string appId in appIds)
        {
            using HttpClient client = CreateSidecarClient(appId);
            if (_sidecarApiTokens?.TryGetValue(appId, out string? apiToken) == true)
            {
                client.DefaultRequestHeaders.Add("dapr-api-token", apiToken);
            }
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
        // One cumulative worker-quiescence budget spans host/sidecar stop commands,
        // fresh exit observations and the automatic fallback within this cleanup attempt.
        using var quiescence = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await PartiesTopologyCleanup.RunAsync(
            async () =>
            {
                if (_app is not null)
                {
                    if (!_workersQuiesced) { await QuiesceOwnedWorkersAsync(_app, quiescence.Token).ConfigureAwait(false); }
                    // Stop all workers and sidecars before the testing host removes owned Redis.
                    // Aspire.Hosting.Testing 13.6.1 delegates StopAsync to its factory disposal:
                    // HostOptions.ShutdownTimeout bounds that path and DCP waits for resource
                    // cleanup. Keep awaiting disposal below even if this observation cancels;
                    // a caller-side WaitAsync timeout must never authorize control-plane removal.
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await _app.StopAsync(timeout.Token).ConfigureAwait(false);
                }
            },
            async () =>
            {
                if (_app is not null)
                {
                    // A failed stop must not let testing-factory disposal remove infrastructure
                    // while owned workers remain active. Retry quiescence before starting disposal.
                    if (!_workersQuiesced) { await QuiesceOwnedWorkersAsync(_app, quiescence.Token).ConfigureAwait(false); }
                    await _app.DisposeAsync().ConfigureAwait(false);
                    _app = null;
                    _stoppedWorkerNames.Clear();
                }
            },
            async () =>
            {
                if (_builder is not null)
                {
                    if (_app is not null && !_workersQuiesced)
                    {
                        // The application callback already reports the failed observation.
                        // Retain the builder as well; disposing it would dispose the same app.
                        return;
                    }
                    await _builder.DisposeAsync().ConfigureAwait(false);
                    _builder = null;
                }
            },
            async () =>
            {
                // Retain the final check for retry after any SDK stop/disposal failure.
                await ConfirmOwnedWorkersStoppedAsync(quiescence.Token).ConfigureAwait(false);
                await CleanupCiDaprMtlsAsync().ConfigureAwait(false);
                if (_workerObservationCancellation is not null)
                {
                    await _workerObservationCancellation.CancelAsync().ConfigureAwait(false);
                    if (_workerObservation is not null) { await _workerObservation.ConfigureAwait(false); }
                    _workerObservationCancellation.Dispose();
                    _workerObservationCancellation = null;
                    _workerObservation = null;
                }
                _ownedWorkers.Clear();
                _workerResourceNames = null;
            },
            async () =>
            {
                if (TenantBootstrapHost is not null)
                {
                    await TenantBootstrapHost.DisposeAsync().ConfigureAwait(false);
                    TenantBootstrapHost = null;
                }
            }).ConfigureAwait(false);
    }

    internal void CaptureOwnedWorkers(DistributedApplication application)
    {
        DistributedApplicationModel model = application.Services.GetRequiredService<DistributedApplicationModel>();
        // The first observation runs synchronously in BeforeStartEvent, before workers
        // launch. Keep declared names and planned Dapr CLI names while Aspire adds its
        // generated resources; enumerating that live collection on later ticks races startup.
        string[] names = _workerResourceNames ??= model.Resources.Select(resource => resource.Name)
            .Concat(model.Resources.SelectMany(resource => resource.Annotations.OfType<DaprSidecarAnnotation>())
                .Select(sidecar => sidecar.Sidecar.Name + "-cli"))
            .Distinct(StringComparer.Ordinal).ToArray();
        foreach (string name in names)
        {
            if (!application.ResourceNotifications.TryGetCurrentState(name, out ResourceEvent? state)) { continue; }
            object? value = state.Snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value;
            if (value is null || !int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out int id)) { continue; }
            if (OperatingSystem.IsLinux() && (state.Snapshot.State?.Text == KnownResourceStates.Exited || state.Snapshot.State?.Text == KnownResourceStates.Finished
                || state.Snapshot.State?.Text == KnownResourceStates.FailedToStart || state.Snapshot.State?.Text == KnownResourceStates.NotStarted)) { continue; }
            if (ReadWorkerStartIdentity(id) is long started) { CaptureOwnedProcessTree(id, started); }
        }
        foreach ((int id, long started) in _ownedWorkers.ToArray()) { CaptureOwnedProcessTree(id, started); }
    }

    /// <summary>Retains runtime identities observed while a CLI wrapper is still alive.</summary>
    private async Task ObserveOwnedWorkersAsync(DistributedApplication application, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
        try
        {
            do { CaptureOwnedWorkers(application); }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    /// <summary>Captures only descendants of the exact previously observed OS lifetime.</summary>
    private void CaptureOwnedProcessTree(int id, long started)
    {
        bool linux = OperatingSystem.IsLinux();
        // Other platforms retain the existing root exit/SDK-stop observation;
        // only Linux establishes inherited custody and follows descendants.
        if (linux ? !IsOwnedLifetime(id, started) : !IsWorkerRunning(id, started)) { return; }
        _ownedWorkers.AddOrUpdate(id, started, (_, previous) =>
        {
            if (previous == started) { return previous; }
            return (linux ? IsOwnedLifetime(id, started) : IsWorkerRunning(id, started)) ? started : previous;
        });
        if (!linux) { return; }
        try
        {
            foreach (string task in Directory.EnumerateDirectories($"/proc/{id}/task"))
            {
                foreach (string child in File.ReadAllText(Path.Combine(task, "children")).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    int childId = int.Parse(child, System.Globalization.CultureInfo.InvariantCulture);
                    if (ReadLinuxWorkerIdentity(childId) is { } childIdentity && childIdentity.Parent == id
                        && childIdentity.Started >= started && IsOwnedLifetime(id, started)
                        && IsOwnedLifetime(childId, childIdentity.Started, id))
                    {
                        CaptureOwnedProcessTree(childId, childIdentity.Started);
                    }
                }
            }
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }

    private void TerminateOwnedWorkers()
    {
        if (!OperatingSystem.IsLinux()) { return; }
        foreach ((int id, long started) in _ownedWorkers.ToArray())
        {
            if (!IsOwnedLifetime(id, started)) { continue; }
            try
            {
                using Process process = Process.GetProcessById(id);
                // Do not name-match or sweep a process group after its leader exits.
                // Every target was observed before loss and still has its captured identity.
                if (IsOwnedLifetime(id, started)) { process.Kill(); }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) when (!IsWorkerRunning(id, started)) { }
            catch (System.ComponentModel.Win32Exception) when (!IsWorkerRunning(id, started)) { }
        }
    }

    // A snapshot/children PID is only a hint. Require the fixture's launch marker,
    // coherent kernel lifetime and (during descendant discovery) the same parent.
    private bool IsOwnedLifetime(int id, long started, int? expectedParent = null)
    {
        if (!OperatingSystem.IsLinux()) { return false; }
        try
        {
            if (ReadLinuxWorkerIdentity(id) is not { } before || before.Started != started
                || (expectedParent is int expected && before.Parent != expected)) { return false; }
            string marker = WorkerOwnershipVariable + "=" + WorkerOwnershipMarker;
            bool owned = File.ReadAllText($"/proc/{id}/environ").Split('\0').Contains(marker, StringComparer.Ordinal);
            return owned && ReadLinuxWorkerIdentity(id) == before;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static (long Started, int Parent)? ReadLinuxWorkerIdentity(int id)
    {
        try
        {
            string stat = File.ReadAllText($"/proc/{id}/stat");
            string[] fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return fields[0] is "Z" or "X" ? null
                : (long.Parse(fields[19], System.Globalization.CultureInfo.InvariantCulture),
                   int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private Task QuiesceOwnedWorkersAsync(DistributedApplication application, CancellationToken cancellationToken)
    {
        // A constructed model whose SDK StartAsync was never entered has no queued
        // launches. Preserve its existing disposal path without waiting for snapshots.
        if (!_startupScheduled) { _workersQuiesced = true; return Task.CompletedTask; }
        return QuiesceStartedWorkersAsync(application, cancellationToken);
    }

    /// <summary>Drains a started model, retaining completed stops across cleanup retries.</summary>
    /// <param name="application">The application whose workers remain owned.</param>
    /// <param name="cancellationToken">The cumulative cleanup-attempt deadline.</param>
    internal async Task QuiesceStartedWorkersAsync(DistributedApplication application, CancellationToken cancellationToken)
    {
        _workersQuiesced = false;
        if (_workerObservation is { IsFaulted: true }) { await _workerObservation.ConfigureAwait(false); }
        ResourceCommandService commands = application.Services.GetRequiredService<ResourceCommandService>();
        DistributedApplicationModel model = application.Services.GetRequiredService<DistributedApplicationModel>();
        string[] hosts = model.Resources.OfType<ProjectResource>().Select(resource => resource.Name).ToArray();
        HashSet<string> sidecarNames = model.Resources.SelectMany(resource => resource.Annotations.OfType<DaprSidecarAnnotation>())
            .Select(sidecar => sidecar.Sidecar.Name + "-cli").ToHashSet(StringComparer.Ordinal);
        bool directRuntimeLauncher = Path.GetFileName(application.Services.GetService<IOptions<DaprOptions>>()?.Value.DaprPath) == "dapr-session";
        string[] executables = model.Resources.OfType<ExecutableResource>().Select(resource => resource.Name)
            .Concat(sidecarNames)
            .Distinct(StringComparer.Ordinal).ToArray();
        // An SDK stop can leave an exited worker Unknown with stop disabled. Keep
        // its completed stop across observer failures until this app is disposed.
        HashSet<string> stoppedNames = _stoppedWorkerNames;
        using var observations = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Keep dependencies available until every scheduled worker has a stable
            // stopped state or an enabled stop command. Absent/Waiting/Starting and
            // disabled-stop snapshots cannot authorize infrastructure removal.
            await WaitForPendingStartsAsync(application.ResourceNotifications,
                hosts.Concat(executables).Where(name => !stoppedNames.Contains(name)), cancellationToken).ConfigureAwait(false);
            CaptureOwnedWorkers(application);
            if (!directRuntimeLauncher)
            {
                foreach (string name in sidecarNames.Where(name => !_stoppedWorkerNames.Contains(name)))
                {
                    if (application.ResourceNotifications.TryGetCurrentState(name, out ResourceEvent? sidecarState))
                    {
                        try { EnsureCliWrapperOwnership(sidecarState.Snapshot); }
                        catch (InvalidOperationException)
                        {
                            TerminateOwnedWorkers();
                            await ConfirmOwnedWorkersStoppedAsync(cancellationToken).ConfigureAwait(false);
                            throw;
                        }
                    }
                }
            }
            // Hosts drain while their sidecars and backing services remain available.
            // Re-observe partial startup: a worker can become stoppable during this pass.
            await StopGroupAsync(hosts).ConfigureAwait(false);
            await StopGroupAsync(executables).ConfigureAwait(false);
            CaptureOwnedWorkers(application);
            TerminateOwnedWorkers();
            if (!_ownedWorkers.Any(worker => IsWorkerRunning(worker.Key, worker.Value))) { break; }
            await observations.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
        while (true);

        async Task StopGroupAsync(string[] names)
        {
            Task<string?>[] stops = names.Where(name => !stoppedNames.Contains(name)).Select(StopAsync).ToArray();
            try
            {
                await Task.WhenAll(stops).ConfigureAwait(false);
            }
            finally
            {
                // WhenAll settles every sibling before throwing. Preserve successful
                // stop proofs even if another worker fails, with no concurrent writes.
                foreach (Task<string?> stop in stops)
                {
                    if (stop.IsCompletedSuccessfully && await stop.ConfigureAwait(false) is string name) { stoppedNames.Add(name); }
                }
            }
        }

        async Task<string?> StopAsync(string name)
        {
            if (!application.ResourceNotifications.TryGetCurrentState(name, out ResourceEvent? state)
                || !state.Snapshot.Commands.Any(command => command.Name == KnownResourceCommands.StopCommand
                    && command.State == ResourceCommandState.Enabled)) { return null; }
            if (sidecarNames.Contains(name) && !directRuntimeLauncher)
            {
                try { EnsureCliWrapperOwnership(state.Snapshot); }
                catch (InvalidOperationException)
                {
                    TerminateOwnedWorkers();
                    await ConfirmOwnedWorkersStoppedAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }
            object? pid = state.Snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value;
            int.TryParse(Convert.ToString(pid, System.Globalization.CultureInfo.InvariantCulture), out int processId);
            long? started = _ownedWorkers.TryGetValue(processId, out long captured) ? captured : null;
            ExecuteCommandResult stopped = await commands.ExecuteCommandAsync(state.ResourceId, KnownResourceCommands.StopCommand, cancellationToken)
                .ConfigureAwait(false);
            // DCP can report an unsuccessful stop after an already exited executable's
            // status becomes Unknown. Only the exact observed OS exit resolves that race.
            if (!stopped.Success && (started is null || IsWorkerRunning(processId, started.Value)))
            {
                throw new InvalidOperationException("An owned worker stop command failed; topology ownership was retained.");
            }
            return name;
        }
        // Re-check any resource that changed lifecycle state during the stop passes.
        await WaitForPendingStartsAsync(application.ResourceNotifications,
            hosts.Concat(executables).Where(name => !stoppedNames.Contains(name)), cancellationToken).ConfigureAwait(false);
        CaptureOwnedWorkers(application);
        await ConfirmOwnedWorkersStoppedAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (ObserveQuiescenceAsync is not null) { await ObserveQuiescenceAsync(application, cancellationToken).ConfigureAwait(false); }
        if (_workerObservationCancellation is not null)
        {
            await _workerObservationCancellation.CancelAsync().ConfigureAwait(false);
            if (_workerObservation is not null) { await _workerObservation.ConfigureAwait(false); }
        }
        _workersQuiesced = true;
    }

    /// <summary>On Linux, fails closed when the ordinary CLI disappears before SDK-owned stop can prove custody.</summary>
    /// <param name="snapshot">The current ordinary CLI snapshot.</param>
    internal void EnsureCliWrapperOwnership(CustomResourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        // Preserve SDK stop and root-exit observations elsewhere. This guard
        // and fallback containment qualify only Linux process ownership.
        if (!OperatingSystem.IsLinux()) { return; }
        if (snapshot.State?.Text == KnownResourceStates.NotStarted || snapshot.State?.Text == KnownResourceStates.FailedToStart) { return; }
        object? value = snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value;
        if (value is null || !int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out int pid)
            || !_ownedWorkers.TryGetValue(pid, out long started) || !IsOwnedLifetime(pid, started)
            || snapshot.State?.Text == KnownResourceStates.Exited || snapshot.State?.Text == KnownResourceStates.Finished)
        {
            throw new InvalidOperationException("An ordinary Dapr CLI wrapper exited before runtime custody could be proved; backing resources were retained. Polling does not qualify unobserved descendants.");
        }
    }

    /// <summary>Fences scheduled launches before their dependencies can be drained.</summary>
    /// <param name="notifications">The SDK's current owned-resource snapshots.</param>
    /// <param name="names">Workers not already stopped by this cleanup attempt.</param>
    /// <param name="cancellationToken">The cumulative cleanup-attempt deadline.</param>
    internal static async Task WaitForPendingStartsAsync(ResourceNotificationService notifications,
        IEnumerable<string> names, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] workers = names.Distinct(StringComparer.Ordinal).ToArray();
        using var observations = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        while (workers.Any(name => !notifications.TryGetCurrentState(name, out ResourceEvent? state)
            || !IsStoppedOrStoppable(state.Snapshot)))
        {
            await observations.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }

        static bool IsStoppedOrStoppable(CustomResourceSnapshot snapshot)
        {
            string? state = snapshot.State?.Text;
            return state == KnownResourceStates.NotStarted || state == KnownResourceStates.Finished
                || state == KnownResourceStates.Exited || state == KnownResourceStates.FailedToStart
                || snapshot.Commands.Any(command => command.Name == KnownResourceCommands.StopCommand
                    && command.State == ResourceCommandState.Enabled
                    && state != KnownResourceStates.Starting && state != KnownResourceStates.Waiting && state != KnownResourceStates.Building);
        }
    }

    private async Task ConfirmOwnedWorkersStoppedAsync(CancellationToken cancellationToken)
    {
        using var observations = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try
        {
            // A fresh Process instance can share .NET's cached non-child wait state.
            // Compare kernel identities on Linux; a reused PID cannot match its start.
            while (_ownedWorkers.Any(worker => IsWorkerRunning(worker.Key, worker.Value)))
            {
                await observations.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Owned topology workers have not exited; control-plane ownership was retained.");
        }
    }

    /// <summary>Checks an exact OS process lifetime rather than a cached exit observation.</summary>
    /// <param name="processId">The PID observed in this topology's live resource snapshot.</param>
    /// <param name="started">The OS start identity captured before shutdown.</param>
    /// <returns>Whether that exact owned process is still running.</returns>
    internal static bool IsWorkerRunning(int processId, long started) => ReadWorkerStartIdentity(processId) == started;

    /// <summary>Reads a live process's immutable start identity, or null after exit.</summary>
    /// <param name="processId">The PID in an owned resource snapshot.</param>
    /// <returns>Kernel start-clock ticks on Linux; process start-time ticks elsewhere.</returns>
    internal static long? ReadWorkerStartIdentity(int processId)
    {
        if (processId <= 0) { return null; }
        try
        {
            if (OperatingSystem.IsLinux())
            {
                string stat = File.ReadAllText($"/proc/{processId}/stat");
                // comm is parenthesized and may itself contain spaces or parentheses.
                string[] fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return fields[0] is "Z" or "X" ? null
                    : long.Parse(fields[19], System.Globalization.CultureInfo.InvariantCulture);
            }
            using Process current = Process.GetProcessById(processId);
            return current.HasExited ? null : current.StartTime.ToUniversalTime().Ticks;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (System.ComponentModel.Win32Exception exception) when (exception.NativeErrorCode == 3) { return null; }
    }

    internal static string CreateAdministratorToken()
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
        if (!_completeMtlsProfile && !_memoriesReadinessScope && (!OperatingSystem.IsLinux()
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
