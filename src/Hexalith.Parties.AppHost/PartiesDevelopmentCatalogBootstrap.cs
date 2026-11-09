#if HEXALITH_EVENTSTORE_CATALOG_API
using System.Diagnostics;
using System.Reflection;
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
    private readonly IDistributedApplicationBuilder _builder;
    private readonly DirectoryInfo _root;
    private readonly string _instance;
    private readonly string _directory;
    private readonly string _componentName;
    private readonly string _configuration;
    private readonly string _owner = Guid.NewGuid().ToString("N");
    private bool _ownsDirectory;
    private bool _cleanupBlocked;
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
        IResourceBuilder<IDaprComponentResource> component = builder.AddDaprComponent(_componentName, "state.redis")
            .WithMetadata("redisHost", "127.0.0.1:6379").WithMetadata("redisPassword", "")
            .WithMetadata("keyPrefix", "none").WithMetadata("actorStateStore", "false");
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
            _ = builder.CreateResourceBuilder(sidecar.Sidecar).WithReference(component);
            IResourceBuilder<ProjectResource> resource = builder.CreateResourceBuilder(project);
            IResourceBuilder<ParameterResource> channel = builder.AddParameter("catalog-" + project.Name + "-app-token",
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), secret: true);
            _ = resource.WithEventStoreAppChannelToken(channel);
            IResourceBuilder<ParameterResource> sidecarToken = builder.AddParameter("catalog-" + project.Name + "-dapr-token",
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), secret: true);
            _ = resource.WithEnvironment("DAPR_API_TOKEN", sidecarToken);
            sidecar.Sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
                context.EnvironmentVariables["DAPR_API_TOKEN"] = sidecarToken.Resource));
        }
        ProjectResource gateway = projects.Single(project => project.Name == "eventstore");
        IResourceBuilder<ProjectResource> gatewayResource = builder.CreateResourceBuilder(gateway);
        IResourceBuilder<ParameterResource> digest = builder.AddParameter("catalog-idempotency-digest-key",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), secret: true);
        // Two private catalog sessions recapture topology and authentication inside the invocation budget.
        // Keep the production default untouched while allowing bounded cold test-host calls to complete.
        _ = gatewayResource.WithEnvironment("EventStore__DomainServices__InvocationTimeoutSeconds", "30")
            .WithEnvironment("EventStore__IdempotencyAdmission__Enabled", "true")
            .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySource", "Configuration")
            .WithEnvironment("EventStore__IdempotencyAdmission__ActiveDigestKeyVersion", "development-v1")
            .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeys__development-v1", digest)
            .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySecretStoreName", "secretstore")
            .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySecretName", "eventstore-idempotency-ring")
            .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySecretGeneration", "development-v1");
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

    /// <summary>Registers only the explicit run-only package profile; native and publish graphs retain their existing behavior.</summary>
    internal static void Register(IDistributedApplicationBuilder builder, DirectoryInfo root)
    {
        if (builder.ExecutionContext.IsRunMode && builder.Configuration.GetValue<bool>("Parties:Testing:UsePackagedGateway"))
        {
#if HEXALITH_EVENTSTORE_CATALOG_HOSTS
            PartiesDevelopmentCatalogBootstrap bootstrap = new(builder, root);
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
        _ = eventing.Subscribe<BeforeStartEvent>(async (startup, token) => await PrepareAsync(startup.Model, token).ConfigureAwait(false));
        return Task.CompletedTask;
    }

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
                .WithEnvironment("HexalithUsePublishedSdkForHostedTests", "true");
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
            _ = await PreparedAsync(prepared["project"]!.GetValue<string>(), sdkVersion, token).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Cleanup();
        return ValueTask.CompletedTask;
    }

    private void Cleanup()
    {
        if (!_ownsDirectory || _cleanupBlocked) { return; }
        PartiesDevelopmentCatalogTool.DeleteOwnedDirectory(_directory, _owner);
        _ownsDirectory = false;
    }

    private async Task<JsonObject> RootSelectionAsync(CancellationToken token)
    {
        string project = Path.Combine(_root.FullName, "tests", "Hexalith.Parties.EventStoreGateway.TestHost", "Hexalith.Parties.EventStoreGateway.TestHost.csproj");
        string output = await ProcessAsync("dotnet", ["msbuild", project, "-nologo", "-p:Configuration=" + _configuration,
            "-getProperty:HexalithEventStoreVersion,HexalithTenantsVersion,HexalithMemoriesVersion,HexalithFrontComposerVersion,HexalithMcpCliVersion"], token).ConfigureAwait(false);
        return JsonNode.Parse(output)!["Properties"]!.AsObject();
    }

    private async Task<JsonObject> PreparedAsync(string project, string version, CancellationToken token)
    {
        string manifest = Path.Combine(_root.FullName, "artifacts", "runtime-profile", _configuration, "prepared.json");
        string output = await ProcessAsync("python3", [Path.Combine(_root.FullName, "scripts", "validate-aspire-runtime-cohort.py"),
            "--manifest", manifest, "--project", project, "--version", version, "--configuration", _configuration,
            "--families-json", _families!.ToJsonString()], token).ConfigureAwait(false);
        return JsonNode.Parse(output)!.AsObject();
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
