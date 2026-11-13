#if HEXALITH_EVENTSTORE_CATALOG_API
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Contracts.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.AppHost;

/// <summary>Prepares the explicit published-SDK test composition; the actual SDK hosts retain all activation authority.</summary>
internal sealed class PartiesDevelopmentCatalogBootstrap : IDistributedApplicationEventingSubscriber, IAsyncDisposable
{
#if HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
    private static readonly ConditionalWeakTable<IServiceCollection, PartiesDevelopmentCatalogBootstrap> Pending = new();
    private readonly List<AssemblyLoadContext> _metadataContexts = [];
    private DevelopmentRouteCatalogComposition? _composition;
    private Func<CancellationToken, Task<JsonObject>>? _modelRootSelection;
    private Func<string, string, CancellationToken, Task<JsonObject>>? _modelPreparation;
#endif
    private readonly IDistributedApplicationBuilder _builder;
    private readonly DirectoryInfo _root;
    private readonly string _instance;
    private readonly string _directory;
    private readonly string _componentName;
    private readonly string _configuration;
    private readonly string _owner = Guid.NewGuid().ToString("N");
    private bool _ownsDirectory;
    private bool _cleanupBlocked;
    private readonly Lock _disposalGate = new();
    private Task? _disposal;
    private string? _targetFramework;
    private readonly Dictionary<string, JsonObject> _prepared = new(StringComparer.Ordinal);
    private JsonObject? _families;

    private PartiesDevelopmentCatalogBootstrap(IDistributedApplicationBuilder builder, DirectoryInfo root)
    {
        _builder = builder;
        _root = root;
        _configuration = builder.AppHostAssembly?.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
            ?? throw new InvalidDataException("The actual AppHost launch build configuration is unavailable.");
        if (_configuration is not ("Debug" or "Release"))
        {
            throw new NotSupportedException("The package profile supports actual Debug or Release launches only.");
        }
        string session = builder.Configuration["Dapr:Mtls:SessionId"] ?? Guid.NewGuid().ToString("N");
        if (session.Length is 0 or > 64 || session.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new InvalidDataException("A private catalog requires one bounded run identity.");
        }
        _instance = "parties-" + session + "-catalog";
        _componentName = "routing-" + _instance;
        _directory = Path.Combine(builder.Configuration["Dapr:Mtls:ConfigurationDirectory"] ?? Path.GetTempPath(), _instance);
        if (!Path.IsPathFullyQualified(_directory))
        {
            throw new InvalidDataException("A private catalog requires an absolute owned directory.");
        }
#if !HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
        IResourceBuilder<IDaprComponentResource> component = builder.AddDaprComponent(_componentName, "state.redis")
            .WithMetadata("redisHost", "127.0.0.1:6379").WithMetadata("redisPassword", "")
            .WithMetadata("keyPrefix", "none").WithMetadata("actorStateStore", "false");
#endif
        ProjectResource[] projects = builder.Resources.OfType<ProjectResource>().Where(Participates).ToArray();
        foreach (ProjectResource project in projects)
        {
            DaprSidecarAnnotation sidecar = project.Annotations.OfType<DaprSidecarAnnotation>().Single();
            if (project.Name is "sample-blazor-ui" or "eventstore-admin-ui")
            {
                DaprSidecarOptionsAnnotation options = sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                if (options.Options.Config is null)
                {
                    sidecar.Sidecar.Annotations.Remove(options);
                    sidecar.Sidecar.Annotations.Add(new DaprSidecarOptionsAnnotation(options.Options with
                    {
                        Config = Path.Combine(root.FullName, "src", "Hexalith.Parties.AppHost", "DaprComponents", "accesscontrol.client-only.yaml"),
                    }));
                }
            }
#if !HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
            _ = builder.CreateResourceBuilder(sidecar.Sidecar).WithReference(component);
#endif
            IResourceBuilder<ProjectResource> resource = builder.CreateResourceBuilder(project);
            _ = CreatePrivateAppChannel(resource);
            IResourceBuilder<ParameterResource> sidecarToken = builder.AddParameter("catalog-" + project.Name + "-dapr-token",
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), secret: true);
            _ = resource.WithEnvironment("DAPR_API_TOKEN", sidecarToken);
            sidecar.Sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
                context.EnvironmentVariables["DAPR_API_TOKEN"] = sidecarToken.Resource));
        }
        ProjectResource gateway = projects.Single(project => project.Name == "eventstore");
        IResourceBuilder<ProjectResource> gatewayResource = builder.CreateResourceBuilder(gateway);
        // Two private catalog sessions recapture topology and authentication inside the invocation budget.
        // Keep the production default untouched while allowing bounded cold test-host calls to complete.
        _ = gatewayResource.WithEnvironment("EventStore__DomainServices__InvocationTimeoutSeconds", "30");
#if !HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
        IResourceBuilder<ParameterResource> digest = builder.AddParameter("catalog-idempotency-digest-key",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), secret: true);
        _ = gatewayResource
            .WithEnvironment("EventStore__IdempotencyAdmission__Enabled", "true")
            .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySource", "Configuration")
            .WithEnvironment("EventStore__IdempotencyAdmission__ActiveDigestKeyVersion", "development-v1")
            .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeys__development-v1", digest)
            .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySecretStoreName", "secretstore")
            .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySecretName", "eventstore-idempotency-ring")
            .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySecretGeneration", "development-v1");
#endif
        ProjectResource? tenants = projects.SingleOrDefault(project => project.Name == "tenants");
        if (tenants is not null)
        {
            using RSA key = RSA.Create(2048);
            string caller = AppId(tenants);
            IResourceBuilder<ParameterResource> proof = builder.AddParameter("catalog-tenants-stream-proof-key",
                Convert.ToBase64String(key.ExportPkcs8PrivateKey()), secret: true);
            _ = builder.CreateResourceBuilder(tenants).WithEnvironment("Authentication__BoundStreamRead__PrivateKey", proof);
            _ = gatewayResource.WithEnvironment("Authentication__BoundStreamRead__PublicKeys__" + caller,
                Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
        }
    }

    /// <summary>Creates private channel custody without superseding the SDK's generated binding before full composition.</summary>
    internal static IResourceBuilder<ParameterResource> CreatePrivateAppChannel(IResourceBuilder<ProjectResource> project)
    {
        IResourceBuilder<ParameterResource> channel = project.ApplicationBuilder.AddParameter("catalog-" + project.Resource.Name + "-app-token",
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), secret: true);
#if !HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
        _ = project.WithEventStoreAppChannelToken(channel);
#endif
        // SDK 4.1 full composition calls WithGeneratedEventStoreAppChannelToken. Keep
        // the original generated binding intact until that call reuses it, then apply
        // the selected catalog/fixture binding in CompleteCoreAsync's final handoff.
        return channel;
    }

    /// <summary>Registers only the explicit run-only package profile; native and publish graphs retain their existing behavior.</summary>
    internal static void Register(IDistributedApplicationBuilder builder, DirectoryInfo root)
    {
        if (builder.ExecutionContext.IsRunMode && builder.Configuration.GetValue<bool>("Parties:Testing:UsePackagedGateway"))
        {
#if HEXALITH_EVENTSTORE_CATALOG_HOSTS
            PartiesDevelopmentCatalogBootstrap bootstrap = new(builder, root);
#if HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
            Pending.Add(builder.Services, bootstrap);
#endif
            // Factory ownership lets DI dispose the private preparation after the application stops.
            builder.Services.AddSingleton<IDistributedApplicationEventingSubscriber>(_ => bootstrap);
#else
            throw new NotSupportedException("The complete published catalog host profile requires the qualified EventStore3.130.0 cohort;3.129.0 supplies declaration APIs only.");
#endif
        }
    }

    /// <inheritdoc />
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
#if HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
        _ = eventing.Subscribe<BeforeStartEvent>(async (startup, token) =>
        {
            if (_composition is null)
            {
                throw new InvalidOperationException("The explicit package fixture must finalize its owned fresh backend before Build.");
            }
            ConfigurePrivateOwnerHttpListener(_builder, startup.Model);
            foreach (JsonObject prepared in _prepared.Values)
            {
                await VerifySelectedPreparationAsync(prepared, token).ConfigureAwait(false);
            }
        });
#else
        _ = eventing.Subscribe<BeforeStartEvent>(async (startup, token) => await PrepareAsync(startup.Model, token).ConfigureAwait(false));
#endif
        return Task.CompletedTask;
    }

#if HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
    /// <summary>Uses the session's actual reserved Dapr HTTP listener instead of a randomized proxy in the private owner-read profile.</summary>
    internal static void ConfigurePrivateOwnerHttpListener(IDistributedApplicationBuilder builder, DistributedApplicationModel model)
    {
        ProjectResource gateway = model.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore");
        IDaprSidecarResource sidecar = gateway.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        DaprSidecarOptions options = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
        if (options.DaprHttpPort is not int port || port is < 1024 or > 65535)
            throw new InvalidDataException("The private owner transport requires its already selected fixed Dapr HTTP port.");
        ExecutableResource cli = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == sidecar.Name + "-cli");
        EndpointAnnotation listener = cli.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http");
        if (listener.UriScheme != "http" || listener.Protocol != System.Net.Sockets.ProtocolType.Tcp
            || listener.AllocatedEndpoint is not null)
            throw new InvalidDataException("The private owner listener must be finalized as HTTP before endpoint allocation.");
        // The SDK observes the actual CLI endpoint and arguments after DCP allocation.
        // Disable only this proxy so both that endpoint and daprd bind the session's
        // reserved port. Other projects and listeners keep ordinary randomization.
        _ = builder.CreateResourceBuilder(cli).WithEndpoint("http", endpoint =>
        {
            endpoint.IsProxied = false;
            endpoint.Port = port;
            endpoint.TargetPort = port;
        }, createIfNotExists: false);
    }

    /// <summary>Finalizes only this registered package fixture after its fresh Redis and endpoint overrides, before Build.</summary>
    internal static async Task CompleteOwnedTestModelAsync(IDistributedApplicationBuilder builder,
        IResourceBuilder<ContainerResource> redis, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(redis);
        if (!Pending.TryGetValue(builder.Services, out PartiesDevelopmentCatalogBootstrap? bootstrap)) { return; }
        RequireFreshTestBackend(builder, redis);
        if (bootstrap._composition is not null)
        {
            throw new InvalidOperationException("An owned package catalog model can be finalized only once.");
        }
        try
        {
            await PartiesDevelopmentCatalogTool.RunOwnedPreparationAsync(bootstrap._directory, bootstrap._owner, async () =>
            {
                bootstrap._ownsDirectory = true;
                await bootstrap.CompleteCoreAsync(redis, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            bootstrap._cleanupBlocked = failure.Data.Contains("CatalogOwnedProcessStillRunning");
            if (!bootstrap._cleanupBlocked) { await bootstrap.DisposeAsync().ConfigureAwait(false); }
            throw;
        }
    }

    /// <summary>Releases an enrolled package fixture whose application has not deterministically disposed this subscriber.</summary>
    /// <remarks>
    /// A Build fault leaves no application DI to dispose this factory singleton, so the finalized composition and
    /// private directory would be retained. A built application's DI can finish disposing it only after the caller's
    /// disposal returns. Disposal is idempotent: an already disposed subscriber is no longer pending, and directory
    /// deletion keeps its owned-process refusal. Callers must invoke this only after every owned worker has quiesced.
    /// </remarks>
    /// <param name="builder">The fixture builder that registered the package profile.</param>
    /// <returns>A task that represents the release.</returns>
    internal static async Task ReleaseUnstartedOwnedTestModelAsync(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (Pending.TryGetValue(builder.Services, out PartiesDevelopmentCatalogBootstrap? bootstrap))
        {
            await bootstrap.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Supplies immutable prepared inputs for an enrolled model test while retaining the real composition and fixture finalization path.</summary>
    internal static void UsePreparedInputsForModelTest(IDistributedApplicationBuilder builder,
        Func<CancellationToken, Task<JsonObject>> selection,
        Func<string, string, CancellationToken, Task<JsonObject>> preparation)
    {
        if (!Pending.TryGetValue(builder.Services, out PartiesDevelopmentCatalogBootstrap? bootstrap) || bootstrap._composition is not null)
            throw new InvalidOperationException("Prepared model inputs require an enrolled, unfinalized package fixture.");
        bootstrap._modelRootSelection = selection;
        bootstrap._modelPreparation = preparation;
    }

    /// <summary>Refuses retained storage or resources outside the fixture model before provisioning a new digest ring.</summary>
    internal static void RequireFreshTestBackend(IDistributedApplicationBuilder builder, IResourceBuilder<ContainerResource> redis)
    {
        if (!builder.Resources.Any(resource => ReferenceEquals(resource, redis.Resource))
            || redis.Resource.Name != "fixture-redis"
            || redis.Resource.Annotations.OfType<ContainerMountAnnotation>().Any()
            || redis.Resource.Annotations.OfType<ContainerLifetimeAnnotation>().Any(annotation => annotation.Lifetime == ContainerLifetime.Persistent))
        {
            throw new InvalidOperationException("A new package catalog digest ring requires this fixture's nonpersistent, unmounted Redis resource.");
        }
    }

    private async Task CompleteCoreAsync(IResourceBuilder<ContainerResource> redis, CancellationToken token)
    {
        using CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
        bounded.CancelAfter(TimeSpan.FromMinutes(4));
        token = bounded.Token;
        ProjectResource[] projects = _builder.Resources.OfType<ProjectResource>().Where(Participates)
            .OrderBy(project => project.Name, StringComparer.Ordinal).ToArray();
        Dictionary<string, string> selected = _builder.Resources.OfType<ProjectResource>()
            .Where(project => project.Annotations.OfType<DaprSidecarAnnotation>().Any())
            .ToDictionary(project => project.Name, AppId, StringComparer.Ordinal);
        if (!projects.Any(project => project.Name == "eventstore") || projects.Select(AppId).Distinct(StringComparer.Ordinal).Count() != projects.Length)
        {
            throw new InvalidDataException("The exact private catalog host set is missing or ambiguous.");
        }
        JsonObject rootSelection = await RootSelectionAsync(token).ConfigureAwait(false);
        string sdkVersion = rootSelection["HexalithEventStoreVersion"]!.GetValue<string>();
        _targetFramework = rootSelection["TargetFramework"]!.GetValue<string>();
        if (_targetFramework is not ("net10.0" or "net11.0"))
            throw new InvalidDataException("The prepared runtime requires an exact qualified target framework.");
        _families = new JsonObject();
        foreach (string name in new[] { "HexalithFrontComposerVersion", "HexalithTenantsVersion", "HexalithMemoriesVersion", "HexalithMcpCliVersion" })
        {
            _families[name] = rootSelection[name]!.DeepClone();
        }
        string[] preparedNames = ["eventstore", "eventstore-admin", "eventstore-admin-ui", "tenants", "memories", "memories-domain-service", "sample", "sample-blazor-ui", "parties", "parties-ui"];
        foreach (ProjectResource project in _builder.Resources.OfType<ProjectResource>().Where(project => preparedNames.Contains(project.Name, StringComparer.Ordinal)))
        {
            JsonObject prepared = await PreparedAsync(project.GetProjectMetadata().ProjectPath, sdkVersion, token).ConfigureAwait(false);
            _prepared[project.Name] = prepared;
            BindPreparedLaunch(_builder.CreateResourceBuilder(project), prepared, async cancellationToken =>
            {
                await VerifySelectedPreparationAsync(prepared, cancellationToken).ConfigureAwait(false);
            });
        }
        List<DevelopmentRouteCatalogOwner> owners = [];
        foreach (ProjectResource project in projects)
        {
            string target = _prepared[project.Name]["targetPath"]!.GetValue<string>();
            string output = Path.GetDirectoryName(target)!;
            string? ownerPath = null;
            string? adapterPath = null;
            if (project.Name == "parties")
            {
                JsonObject adapter = await PreparedAsync(Path.Combine(_root.FullName, "src", "Hexalith.Parties.EventStoreExtensions", "Hexalith.Parties.EventStoreExtensions.csproj"), sdkVersion, token).ConfigureAwait(false);
                _prepared["parties-adapter"] = adapter;
                ownerPath = Path.Combine(output, "Hexalith.Parties.Contracts.dll");
                adapterPath = adapter["targetPath"]!.GetValue<string>();
            }
            else if (project.Name == "sample") { ownerPath = target; adapterPath = target; }
            else if (project.Name == "tenants")
            {
                ownerPath = Path.Combine(output, "Hexalith.Tenants.Contracts.dll");
                adapterPath = Path.Combine(output, "Hexalith.Tenants.Server.dll");
            }
            else if (project.Name == "memories-domain-service")
            {
                ownerPath = Path.Combine(output, "Hexalith.Memories.EventStore.dll");
                adapterPath = Path.Combine(output, "Hexalith.Memories.EventStoreExtensions.dll");
            }
            if (ownerPath is not null)
            {
                (Assembly declarations, Assembly adapters) = LoadPreparedOwner(ownerPath, adapterPath!);
                owners.Add(new(_builder.CreateResourceBuilder(project), declarations, adapters));
            }
        }
        IResourceBuilder<ProjectResource> gateway = _builder.CreateResourceBuilder(projects.Single(project => project.Name == "eventstore"));
        List<DevelopmentRouteCatalogReceiver> receivers = [];
        foreach (ProjectResource project in projects)
        {
            string host = project.Name;
            DaprSidecarAnnotation sidecar = project.Annotations.OfType<DaprSidecarAnnotation>().Single();
            DaprSidecarOptionsAnnotation options = sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
            string source = Path.GetFullPath(options.Options.Config ?? throw new InvalidDataException("The actual receiver ACL is missing."));
            bool auxiliary = host is "memories" or "sample-blazor-ui" or "eventstore-admin-ui";
            string? owner = host switch { "eventstore" or "eventstore-admin" => "Hexalith.EventStore", "tenants" => "Hexalith.Tenants", "parties" => "Hexalith.Parties", "memories-domain-service" => "Hexalith.Memories", _ => null };
            string? declaration = owner is null ? null : owner == "Hexalith.Parties" ? Path.Combine(_root.FullName, "module", "hexalith.module-manifest.v2.json")
                : ReferenceProjectResolver.ResolveRequired(_root, owner, Path.Combine("module", "hexalith.module-manifest.v2.json"));
            var profile = PartiesDevelopmentCatalogConfiguration.Receiver(source, declaration, host == "eventstore" ? "gateway" : host == "eventstore-admin" ? "admin" : "domain", selected, auxiliary, technical: host is "eventstore" or "eventstore-admin");
            string input = Path.Combine(_directory, "accesscontrol." + host + ".yaml");
            await File.WriteAllTextAsync(input, profile.Configuration.ToJsonString(), token).ConfigureAwait(false);
            sidecar.Sidecar.Annotations[sidecar.Sidecar.Annotations.IndexOf(options)] = new DaprSidecarOptionsAnnotation(options.Options with { Config = input });
            IResourceBuilder<ProjectResource> resource = _builder.CreateResourceBuilder(project);
            List<DevelopmentRouteCatalogInvocation> invocations = profile.Invocations.Select(invocation => new DevelopmentRouteCatalogInvocation(
                _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(caller => selected.TryGetValue(caller.Name, out string? app) && app == invocation!["CallerAppId"]!.GetValue<string>())),
                invocation!["Method"]!.GetValue<string>(), invocation["HttpMethods"]!.AsArray().Select(verb => verb!.GetValue<string>()).ToArray())).ToList();
            receivers.Add(new(resource, invocations)
            {
                TechnicalHttp = host is "eventstore" or "eventstore-admin",
                ActorStateOwners = host == "eventstore-admin" ? [gateway] : [],
            });
            if (host is "sample-blazor-ui" or "eventstore-admin-ui")
            {
                _ = resource.WithEnvironment("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "Hexalith.EventStore.ServiceDefaults")
                    .WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__Role", "aux-client");
            }
            _ = resource.WithEnvironment("EventStore__DomainService__ServiceVersion", "v1")
                .WithEnvironment("Authentication__Workload__Audience", AppId(project))
                .WithEnvironment("Authentication__WorkloadIssuer__Workload", AppId(project));
        }
        var custody = new DevelopmentRouteCatalogCompositionOptions(Path.Combine(_directory, "custody"), _componentName,
            _instance + "-authority", "development-v1", new Dictionary<string, string>(StringComparer.Ordinal)
            { ["development-v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }, "development-v1");
        _composition = _builder.AddEventStoreDevelopmentRouteCatalog(gateway, owners, receivers,
            _builder.CreateResourceBuilder<IDaprComponentResource>("statestore"), custody,
            _builder.Configuration["Dapr:Mtls:Namespace"] ?? "default");
        foreach (ProjectResource project in projects)
        {
            // Full composition supplies a generated default. Retain the explicit private
            // channel selected by this wrapper and the fixture's stronger test overrides.
            ParameterResource channel = _builder.Resources.OfType<ParameterResource>().SingleOrDefault(parameter => parameter.Name == "fixture-" + project.Name + "-channel-token")
                ?? _builder.Resources.OfType<ParameterResource>().Single(parameter => parameter.Name == "catalog-" + project.Name + "-app-token");
            _ = _builder.CreateResourceBuilder(project).WithEventStoreAppChannelToken(_builder.CreateResourceBuilder(channel));
            if (project.Name is "sample-blazor-ui" or "eventstore-admin-ui")
            {
                BindAuxiliaryClientProfile(_builder.CreateResourceBuilder(project), _componentName);
            }
        }
        // The new SDK authority is created after the fixture selected its backend. Bind it to
        // that same fresh resource using supported metadata providers before toolkit rendering.
        EndpointReference endpoint = redis.GetEndpoint("tcp");
        ParameterResource password = _builder.Resources.OfType<ParameterResource>().Single(parameter => parameter.Name == "fixture-redis-password");
        _ = _builder.CreateResourceBuilder<IDaprComponentResource>(_componentName)
            .WithMetadata("redisHost", ReferenceExpression.Create($"{endpoint.Property(EndpointProperty.Host)}:{endpoint.Property(EndpointProperty.Port)}"))
            .WithMetadata("redisPassword", password);
        _ = gateway.WithEnvironment("Authentication__DaprInternal__Audience", AppId(gateway.Resource));
        string[] callers = projects.Where(project => project.Name is "tenants" or "parties" or "memories").Select(AppId).ToArray();
        for (int index = 0; index < callers.Length; index++) { _ = gateway.WithEnvironment("Authentication__DaprInternal__AllowedCallers__" + index, callers[index]); }
        using JsonDocument catalog = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(_composition.Directory, "catalog.json"), token).ConfigureAwait(false));
        if (selected.TryGetValue("tenants", out string? tenantApp))
        {
            string[] domains = catalog.RootElement.GetProperty("routes").EnumerateArray().Where(route => route.GetProperty("appId").GetString() == tenantApp)
                .Select(route => catalog.RootElement.GetProperty("declarations").EnumerateArray().Single(item => item.GetProperty("routeId").GetString() == route.GetProperty("routeId").GetString()).GetProperty("domain").GetString()!)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            _ = gateway.WithEnvironment("Authentication__BoundStreamRead__AllowedDomains__" + tenantApp, string.Join(',', domains));
        }
    }

    /// <summary>Retains strict receiver-free UI enrollment while independently requiring its actual dedicated authority component.</summary>
    internal static void BindAuxiliaryClientProfile(IResourceBuilder<ProjectResource> resource, string authorityComponent)
    {
        if (resource.Resource.Name is not ("sample-blazor-ui" or "eventstore-admin-ui"))
            throw new InvalidOperationException("Only the selected receiver-free UI profiles can use auxiliary enrollment.");
        _ = resource.WithEnvironment(async context =>
        {
            const string Prefix = "EventStore__RouteCatalog__DevelopmentBootstrap__";
            if (!context.EnvironmentVariables.TryGetValue(Prefix + "Role", out object? role) || role is not "aux-client"
                || !context.EnvironmentVariables.TryGetValue(Prefix + "AuthorityRole", out object? authorityRole) || authorityRole is not RouteCatalogAuthorityComponentPolicy.IsolatedRole
                || !context.EnvironmentVariables.TryGetValue("EventStore__RouteCatalog__StateStore", out object? stateStore) || !Equals(stateStore, authorityComponent)
                || !context.EnvironmentVariables.TryGetValue(Prefix + "ComponentPath", out object? component))
                throw new InvalidDataException("The auxiliary UI requires the selected SDK composition and its dedicated authority.");
            string? path = component switch
            {
                string literal => literal,
                IValueProvider provider => await provider.GetValueAsync(context.CancellationToken).ConfigureAwait(false),
                _ => null,
            };
            if (path is null || !Path.IsPathFullyQualified(path)) throw new InvalidDataException("The actual auxiliary authority component is unavailable.");
            FileInfo file = new(path);
            if (!file.Exists || file.LinkTarget is not null || file.Length is <= 0 or > 1_048_576)
                throw new InvalidDataException("The auxiliary authority must be an actual bounded SDK component.");
            using JsonDocument actual = JsonDocument.Parse(await File.ReadAllBytesAsync(path, context.CancellationToken).ConfigureAwait(false));
            if (!RouteCatalogAuthorityComponentPolicy.IsDedicatedComponent(actual.RootElement, authorityComponent))
                throw new InvalidDataException("The actual auxiliary authority lost its dedicated role or isolated keyspace.");
            // The installed SDK aux-client profile rejects a competing host-profile selector.
            // Its loaded component retains the SDK's dedicated role/name keyspace, checked
            // above and by the SDK component observer before every resource start.
            _ = context.EnvironmentVariables.Remove(Prefix + "AuthorityRole");
        });
    }

    private (Assembly Declarations, Assembly Adapters) LoadPreparedOwner(string ownerPath, string adapterPath)
    {
        string[] directories = new[] { Path.GetDirectoryName(ownerPath)!, Path.GetDirectoryName(adapterPath)! }.Distinct(StringComparer.Ordinal).ToArray();
        var files = directories.SelectMany(directory => Directory.EnumerateFiles(directory, "*.dll")).GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key!, group =>
            {
                string first = group.First();
                if (group.Any(path => !File.ReadAllBytes(path).AsSpan().SequenceEqual(File.ReadAllBytes(first))))
                    throw new InvalidDataException("Prepared owner and adapter dependencies differ: " + group.Key);
                return first;
            }, StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string path) in files.Where(file => file.Key.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal)))
        {
            Assembly shared = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            using var stream = File.OpenRead(shared.Location);
            using var pe = new PEReader(stream);
            if (pe.GetMetadataReader().GetGuid(pe.GetMetadataReader().GetModuleDefinition().Mvid) != shared.ManifestModule.ModuleVersionId
                || !File.ReadAllBytes(shared.Location).AsSpan().SequenceEqual(File.ReadAllBytes(path)))
                throw new InvalidDataException("The actual AppHost SDK differs from its prepared owner: " + name);
        }
        AssemblyLoadContext context = new("parties-catalog-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        _metadataContexts.Add(context);
        context.Resolving += (_, identity) => files.TryGetValue(identity.Name + ".dll", out string? path) ? context.LoadFromAssemblyPath(path) : null;
        // Preload owner libraries into this private context so a prior in-process fixture's
        // same-named domain assembly cannot replace the actual validated prepared output.
        foreach ((string name, string path) in files.Where(file => file.Key.StartsWith("Hexalith.", StringComparison.Ordinal)
            && !file.Key.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal)))
            _ = context.LoadFromAssemblyPath(path);
        return (context.LoadFromAssemblyPath(ownerPath), context.LoadFromAssemblyPath(adapterPath));
    }

    /// <summary>Launches the validated entry PE directly; source edits cannot cause a later dotnet-run rebuild.</summary>
    internal static void BindPreparedLaunch(IResourceBuilder<ProjectResource> resource, JsonObject prepared,
        Func<CancellationToken, Task> verify)
    {
        string target = prepared["targetPath"]!.GetValue<string>();
        _ = resource.WithAnnotation(new ExecutableAnnotation { Command = "dotnet", WorkingDirectory = Path.GetDirectoryName(target)! });
        _ = resource.WithEnvironment("ASPNETCORE_CONTENTROOT", Path.GetDirectoryName(target))
            .WithEnvironment("DOTNET_CONTENTROOT", Path.GetDirectoryName(target));
        // Aspire caches launch-tool arguments. Its per-start event remains the guard
        // for restarts after that prefix has already been evaluated.
        _ = resource.ApplicationBuilder.Eventing.Subscribe<BeforeResourceStartedEvent>(async (startup, token) =>
        {
            if (ReferenceEquals(startup.Resource, resource.Resource)) { await verify(token).ConfigureAwait(false); }
        });
        // Aspire 13.6 publishes this experimental launch override; opting in here keeps
        // every other compiler diagnostic and the repository's warning policy intact.
#pragma warning disable ASPIREEXTENSION001
        _ = resource.WithLaunchToolArgs(async context =>
        {
            await verify(context.CancellationToken).ConfigureAwait(false);
            context.Args.Add("exec");
            context.Args.Add(target);
        });
#pragma warning restore ASPIREEXTENSION001
    }
#endif

    private static bool Participates(ProjectResource project) => project.Name is "eventstore" or "parties" or "tenants" or "sample"
        or "memories" or "memories-domain-service" or "sample-blazor-ui" or "eventstore-admin" or "eventstore-admin-ui"
        && project.Annotations.OfType<DaprSidecarAnnotation>().Any();

    private static string AppId(ProjectResource project) => project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations
        .OfType<DaprSidecarOptionsAnnotation>().Single().Options.AppId
        ?? throw new InvalidDataException("A catalog participant has no actual sidecar application identity.");

    private async Task PrepareAsync(DistributedApplicationModel model, CancellationToken token)
    {
        try
        {
            await PartiesDevelopmentCatalogTool.RunOwnedPreparationAsync(_directory, _owner, () =>
            {
                _ownsDirectory = true;
                return PrepareCoreAsync(model, token);
            }).ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            _cleanupBlocked = failure.Data.Contains("CatalogOwnedProcessStillRunning");
            if (!_cleanupBlocked) { Cleanup(); }
            throw;
        }
    }

    private async Task VerifySelectedPreparationAsync(JsonObject prepared, CancellationToken token)
    {
        JsonObject actual = await PreparedAsync(prepared["project"]!.GetValue<string>(), prepared["sdkVersion"]!.GetValue<string>(), token).ConfigureAwait(false);
        RequireSelectedPreparation(prepared, actual);
    }

    /// <summary>Refuses re-preparation that replaces any originally selected owner or runtime receipt after composition.</summary>
    internal static void RequireSelectedPreparation(JsonObject selected, JsonObject actual)
    {
        if (!JsonNode.DeepEquals(selected, actual))
            throw new InvalidDataException("The selected prepared runtime receipt changed after catalog composition.");
    }

    private async Task PrepareCoreAsync(DistributedApplicationModel model, CancellationToken token)
    {
        using CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
        bounded.CancelAfter(TimeSpan.FromMinutes(4));
        token = bounded.Token;
        ProjectResource[] projects = model.Resources.OfType<ProjectResource>().Where(Participates).OrderBy(project => project.Name, StringComparer.Ordinal).ToArray();
        Dictionary<string, string> selected = model.Resources.OfType<ProjectResource>()
            .Where(project => project.Annotations.OfType<DaprSidecarAnnotation>().Any())
            .ToDictionary(project => project.Name, AppId, StringComparer.Ordinal);
        if (!projects.Any(project => project.Name == "eventstore") || projects.Select(AppId).Distinct(StringComparer.Ordinal).Count() != projects.Length)
        {
            throw new InvalidDataException("The exact private catalog host set is missing or ambiguous.");
        }
        string gateway = selected["eventstore"];
        JsonObject rootSelection = await RootSelectionAsync(token).ConfigureAwait(false);
        string sdkVersion = rootSelection["HexalithEventStoreVersion"]!.GetValue<string>();
        _targetFramework = rootSelection["TargetFramework"]!.GetValue<string>();
        if (_targetFramework is not ("net10.0" or "net11.0"))
            throw new InvalidDataException("The prepared runtime requires an exact qualified target framework.");
        _families = new JsonObject();
        foreach (string name in new[] { "HexalithFrontComposerVersion", "HexalithTenantsVersion", "HexalithMemoriesVersion", "HexalithMcpCliVersion" })
        {
            _families[name] = rootSelection[name]!.DeepClone();
        }
        // The same explicit properties must reach dotnet-run/rebuilder processes after preparation.
        foreach (ProjectResource runtime in model.Resources.OfType<ProjectResource>())
        {
            IResourceBuilder<ProjectResource> resource = _builder.CreateResourceBuilder(runtime)
                .WithEnvironment("HexalithEventStoreVersion", sdkVersion)
                .WithEnvironment("HexalithUsePublishedSdkForHostedTests", "true")
                .WithEnvironment("HexalithPartiesRuntimeTargetFramework", _targetFramework)
                .WithEnvironment("CustomAfterMicrosoftCommonProps", Path.Combine(_root.FullName, "scripts", "PartiesHostedRuntime.props"));
            foreach ((string key, JsonNode? value) in _families) { _ = resource.WithEnvironment(key, value!.GetValue<string>()); }
        }
        string[] preparedNames = ["eventstore", "eventstore-admin", "eventstore-admin-ui", "tenants", "memories", "memories-domain-service", "sample", "sample-blazor-ui", "parties"];
        foreach (ProjectResource runtime in model.Resources.OfType<ProjectResource>().Where(project => preparedNames.Contains(project.Name, StringComparer.Ordinal)))
        {
            _prepared[runtime.Name] = await PreparedAsync(runtime.GetProjectMetadata().ProjectPath, sdkVersion, token).ConfigureAwait(false);
        }
        List<(string OwnerPath, string AdapterPath, string AppId)> owners = [];
        foreach (ProjectResource project in projects)
        {
            JsonObject prepared = _prepared[project.Name];
            string output = Path.GetDirectoryName(prepared["targetPath"]!.GetValue<string>())!;
            if (project.Name == "parties")
            {
                JsonObject adapter = await PreparedAsync(Path.Combine(_root.FullName, "src", "Hexalith.Parties.EventStoreExtensions", "Hexalith.Parties.EventStoreExtensions.csproj"), sdkVersion, token).ConfigureAwait(false);
                _prepared["parties-adapter"] = adapter;
                owners.Add((Path.Combine(output, "Hexalith.Parties.Contracts.dll"), adapter["targetPath"]!.GetValue<string>(), AppId(project)));
            }
            else if (project.Name == "sample")
            {
                string target = prepared["targetPath"]!.GetValue<string>();
                owners.Add((target, target, AppId(project)));
            }
            else if (project.Name == "tenants")
            {
                owners.Add((Path.Combine(output, "Hexalith.Tenants.Contracts.dll"), Path.Combine(output, "Hexalith.Tenants.Server.dll"), AppId(project)));
            }
            else if (project.Name == "memories-domain-service")
            {
                // The explicit owner profile places its actual published adapter beside its actual owner PE.
                owners.Add((Path.Combine(output, "Hexalith.Memories.EventStore.dll"), Path.Combine(output, "Hexalith.Memories.EventStoreExtensions.dll"), AppId(project)));
            }
        }
        using JsonDocument receipt = await PartiesDevelopmentCatalogTool.GenerateAsync(_root, _directory, _instance, gateway,
            sdkVersion, owners, token).ConfigureAwait(false);
        string rootDigest = receipt.RootElement.GetProperty("rootDigest").GetString()!;
        string componentDirectory = Path.Combine(_directory, "component");
        PartiesDevelopmentCatalogTool.CreatePrivateDirectory(componentDirectory);
        string componentPath = Path.Combine(componentDirectory, _componentName + ".yaml");
        string? originalComponentDirectory = null;
        Dictionary<string, ExecutableResource> sidecars = [];
        foreach (ProjectResource project in projects)
        {
            DaprSidecarAnnotation sidecar = project.Annotations.OfType<DaprSidecarAnnotation>().Single();
            ExecutableResource cli = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == sidecar.Sidecar.Name + "-cli");
            sidecars.Add(project.Name, cli);
            List<object> arguments = [];
            CommandLineArgsCallbackContext context = new(arguments, cli, token) { ExecutionContext = _builder.ExecutionContext };
            foreach (CommandLineArgsCallbackAnnotation annotation in cli.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            {
                await annotation.Callback(context).ConfigureAwait(false);
            }
            string[] directories = arguments.Select((argument, index) => (argument, index))
                .Where(item => Equals(item.argument, "--resources-path") && item.index + 1 < arguments.Count)
                .Select(item => arguments[item.index + 1]).OfType<string>()
                .Where(path => File.Exists(Path.Combine(path, _componentName + ".yaml"))).ToArray();
            if (directories.Length != 1 || originalComponentDirectory is not null && originalComponentDirectory != directories[0])
            {
                throw new InvalidDataException("Every actual sidecar must select the same modeled shared catalog component.");
            }
            originalComponentDirectory = directories[0];
        }
        JsonObject component = PartiesDevelopmentCatalogConfiguration.Read(Path.Combine(originalComponentDirectory!, _componentName + ".yaml"));
        component["scopes"] = new JsonArray(projects.Select(AppId).Order(StringComparer.Ordinal).Select(appId => (JsonNode?)JsonValue.Create(appId)).ToArray());
        await File.WriteAllTextAsync(componentPath, component.ToJsonString(), token).ConfigureAwait(false);
        foreach (ProjectResource project in projects)
        {
            string host = project.Name;
            string appId = AppId(project);
            DaprSidecarOptions options = project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
            string source = Path.GetFullPath(options.Config ?? throw new InvalidDataException("The actual receiver ACL is missing."));
            bool auxiliary = host is "memories" or "sample-blazor-ui" or "eventstore-admin-ui";
            string? owner = host switch { "eventstore" or "eventstore-admin" => "Hexalith.EventStore", "tenants" => "Hexalith.Tenants", "parties" => "Hexalith.Parties", "memories-domain-service" => "Hexalith.Memories", _ => null };
            string? declaration = owner is null ? null : owner == "Hexalith.Parties" ? Path.Combine(_root.FullName, "module", "hexalith.module-manifest.v2.json")
                : ReferenceProjectResolver.ResolveRequired(_root, owner, Path.Combine("module", "hexalith.module-manifest.v2.json"));
            var profile = PartiesDevelopmentCatalogConfiguration.Receiver(source, declaration, host == "eventstore" ? "gateway" : host == "eventstore-admin" ? "admin" : "domain", selected, auxiliary, technical: host is "eventstore" or "eventstore-admin");
            string acl = Path.Combine(_directory, "accesscontrol." + host + ".yaml");
            await File.WriteAllTextAsync(acl, profile.Configuration.ToJsonString(), token).ConfigureAwait(false);
            string oldComponent = originalComponentDirectory!;
            sidecars[host].Annotations.Add(new CommandLineArgsCallbackAnnotation(arguments =>
            {
                for (int index = 0; index + 1 < arguments.Count; index++)
                {
                    if (Equals(arguments[index], "--resources-path") && Equals(arguments[index + 1], oldComponent)) { arguments[index + 1] = componentDirectory; }
                    if (Equals(arguments[index], "--config") && Equals(arguments[index + 1], source)) { arguments[index + 1] = acl; }
                }
            }));
            IResourceBuilder<ProjectResource> resource = _builder.CreateResourceBuilder(project);
            const string prefix = "EventStore__RouteCatalog__DevelopmentBootstrap__";
            _ = resource.WithEnvironment(prefix + "Directory", _directory).WithEnvironment(prefix + "HostId", host)
                .WithEnvironment(prefix + "CoordinatorHostId", "eventstore").WithEnvironment(prefix + "AttemptId", _instance)
                .WithEnvironment(prefix + "InstanceId", _instance).WithEnvironment(prefix + "CatalogPath", Path.Combine(_directory, "catalog.json"))
                .WithEnvironment(prefix + "RootDigest", rootDigest).WithEnvironment(prefix + "BindingVersion", "2")
                .WithEnvironment(prefix + "ComponentPath", componentPath)
                .WithEnvironment(prefix + "AccessControlPath", acl).WithEnvironment(prefix + "DeclaredInvocations", profile.Invocations.ToJsonString())
                .WithEnvironment("EventStore__RouteCatalog__StateStore", _componentName).WithEnvironment("EventStore__RouteCatalog__StateKey", _instance + "-authority")
                .WithEnvironment("EventStore__DomainService__AppId", appId).WithEnvironment("EventStore__DomainService__ServiceVersion", "v1")
                .WithEnvironment("Authentication__Workload__Audience", appId).WithEnvironment("Authentication__WorkloadIssuer__Workload", appId);
            for (int index = 0; index < projects.Length; index++) { _ = resource.WithEnvironment(prefix + "RequiredHostIds__" + index, projects[index].Name); }
            string[] apps = selected.Values.Order(StringComparer.Ordinal).ToArray();
            for (int index = 0; index < apps.Length; index++) { _ = resource.WithEnvironment(prefix + "SelectedAppIds__" + index, apps[index]); }
            if (host is "sample-blazor-ui" or "eventstore-admin-ui")
            {
                _ = resource.WithEnvironment("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "Hexalith.EventStore.ServiceDefaults")
                    .WithEnvironment(prefix + "Role", "aux-client");
            }
            if (host is "eventstore" or "eventstore-admin")
            {
                _ = resource.WithEnvironment(prefix + "ReceiverProfile", RouteCatalogTechnicalHttpPolicy.FenceVersion);
            }
            if (host == "eventstore-admin")
            {
                ParameterResource gatewayToken = model.Resources.OfType<ParameterResource>().Single(parameter => parameter.Name == "catalog-eventstore-dapr-token");
                const string actorOwner = "EventStore__RouteCatalog__DevelopmentActorStateOwners__0__";
                _ = resource.WithEnvironment(prefix + "Role", "admin-receiver")
                    .WithEnvironment(actorOwner + "AppId", gateway)
                    .WithEnvironment(actorOwner + "HttpEndpoint", sidecars["eventstore"].GetEndpoint("http"))
                    .WithEnvironment(actorOwner + "ApiToken", _builder.CreateResourceBuilder(gatewayToken));
            }
            if (host == "eventstore")
            {
                _ = resource.WithEnvironment("Authentication__DaprInternal__Audience", appId);
                string[] callers = projects.Where(peer => peer.Name is "tenants" or "parties" or "memories").Select(AppId).ToArray();
                for (int index = 0; index < callers.Length; index++) { _ = resource.WithEnvironment("Authentication__DaprInternal__AllowedCallers__" + index, callers[index]); }
                foreach (string kind in new[] { "owner", "adapter" })
                {
                    string name = kind + "Assemblies";
                    string[] assemblies = receipt.RootElement.GetProperty(name).EnumerateArray().Select(value => value.GetString()!).ToArray();
                    for (int index = 0; index < assemblies.Length; index++)
                    {
                        _ = resource.WithEnvironment(prefix + char.ToUpperInvariant(kind[0]) + kind[1..] + "Assemblies__" + index, assemblies[index]);
                    }
                }
                using JsonDocument catalog = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(_directory, "catalog.json"), token).ConfigureAwait(false));
                if (selected.TryGetValue("tenants", out string? tenantApp))
                {
                    string[] domains = catalog.RootElement.GetProperty("routes").EnumerateArray().Where(route => route.GetProperty("appId").GetString() == tenantApp)
                        .Select(route => catalog.RootElement.GetProperty("declarations").EnumerateArray().Single(item => item.GetProperty("routeId").GetString() == route.GetProperty("routeId").GetString()).GetProperty("domain").GetString()!)
                        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                    _ = resource.WithEnvironment("Authentication__BoundStreamRead__AllowedDomains__" + tenantApp, string.Join(',', domains));
                }
            }
        }

        // Recheck after the reader build/copy work, immediately before the owning event permits any runtime launch.
        foreach (JsonObject prepared in _prepared.Values)
        {
            await VerifySelectedPreparationAsync(prepared, token).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Application DI can finish disposing this subscriber in the background while a fixture releases an unstarted
    /// model. Concurrent callers share one attempt; a completed attempt is final. A failed composition disposal can be
    /// retried, but it permanently blocks directory deletion because owned processes can no longer be proven stopped.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        Task disposal;
        lock (_disposalGate)
        {
            if (_disposal is null || _disposal.IsFaulted || _disposal.IsCanceled)
            {
                _disposal = DisposeCoreAsync();
            }

            disposal = _disposal;
        }

        return new ValueTask(disposal);
    }

    private async Task DisposeCoreAsync()
    {
#if HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
        if (_composition is not null)
        {
            try { await _composition.DisposeAsync().ConfigureAwait(false); }
            catch { _cleanupBlocked = true; throw; }
            _composition = null;
        }
        foreach (AssemblyLoadContext context in _metadataContexts) { context.Unload(); }
        _metadataContexts.Clear();
        _ = Pending.Remove(_builder.Services);
#else
        await Task.CompletedTask.ConfigureAwait(false);
#endif
        Cleanup();
    }

    private void Cleanup()
    {
        if (!_ownsDirectory || _cleanupBlocked) { return; }
        PartiesDevelopmentCatalogTool.DeleteOwnedDirectory(_directory, _owner);
        _ownsDirectory = false;
    }

    private async Task<JsonObject> RootSelectionAsync(CancellationToken token)
    {
#if HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
        if (_modelRootSelection is not null) { return await _modelRootSelection(token).ConfigureAwait(false); }
#endif
        string project = Path.Combine(_root.FullName, "tests", "Hexalith.Parties.EventStoreGateway.TestHost", "Hexalith.Parties.EventStoreGateway.TestHost.csproj");
        string output = await ProcessAsync("dotnet", ["msbuild", project, "-nologo", "-p:Configuration=" + _configuration,
            "-getProperty:HexalithEventStoreVersion,HexalithTenantsVersion,HexalithMemoriesVersion,HexalithFrontComposerVersion,HexalithMcpCliVersion,TargetFramework"], token).ConfigureAwait(false);
        return JsonNode.Parse(output)!["Properties"]!.AsObject();
    }

    private async Task<JsonObject> PreparedAsync(string project, string version, CancellationToken token)
    {
#if HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
        if (_modelPreparation is not null) { return await _modelPreparation(project, version, token).ConfigureAwait(false); }
#endif
        string manifest = PreparedManifestPath(_builder.Configuration, _root.FullName, _configuration);
        string output = await ProcessAsync("python3", [Path.Combine(_root.FullName, "scripts", "validate-aspire-runtime-cohort.py"),
            "--manifest", manifest, "--project", project, "--version", version, "--configuration", _configuration,
            "--families-json", _families!.ToJsonString(), "--target-framework", _targetFramework!], token).ConfigureAwait(false);
        return JsonNode.Parse(output)!.AsObject();
    }

    /// <summary>Selects only an absolute, existing, regular manifest for the explicit private package profile.</summary>
    internal static string PreparedManifestPath(IConfiguration configuration, string root, string buildConfiguration)
    {
        string path = configuration["Parties:Testing:PreparedRuntimeManifest"]
            ?? Path.Combine(root, "artifacts", "runtime-profile", buildConfiguration, "prepared.json");
        if (!Path.IsPathFullyQualified(path)) { throw new InvalidDataException("The private prepared runtime manifest must be absolute."); }
        FileInfo file = new(path);
        if (!file.Exists || file.LinkTarget is not null || (file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0
            || file.Length is <= 0 or > 16_777_216)
            throw new InvalidDataException("The private prepared runtime manifest must be a bounded regular non-linked file.");
        return path;
    }

    private async Task<string> ProcessAsync(string executable, IReadOnlyList<string> arguments, CancellationToken token)
    {
        ProcessStartInfo start = new(executable) { WorkingDirectory = _root.FullName, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(token);
        Task<string> error = process.StandardError.ReadToEndAsync(token);
        try { await process.WaitForExitAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException cancellation)
        {
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(30));
            try
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); }
                await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                if (!process.HasExited)
                {
                    cancellation.Data["CatalogOwnedProcessStillRunning"] = true;
                    cancellation.Data["CatalogOwnedProcessCleanupFailure"] = failure;
                }
            }
            throw;
        }
        string stdout = await output.ConfigureAwait(false);
        string stderr = await error.ConfigureAwait(false);
        if (process.ExitCode != 0) { throw new InvalidDataException("Actual runtime preparation validation failed: " + stderr); }
        return stdout;
    }
}
#endif
