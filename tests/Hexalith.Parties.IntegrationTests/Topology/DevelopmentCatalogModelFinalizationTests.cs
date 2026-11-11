extern alias apphost;

using System.Text.Json;
using System.Security.Cryptography;
using Aspire.Hosting.Testing;
using Hexalith.Parties.IntegrationTests.HealthChecks;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Nodes;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Bootstrap = apphost::Hexalith.Parties.AppHost.PartiesDevelopmentCatalogBootstrap;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises actual Aspire launch and backend ownership boundaries without starting infrastructure.</summary>
[Collection("Non-parallel")]
public sealed class DevelopmentCatalogModelFinalizationTests
{
#if HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
    /// <summary>The actual fixture caller finalizes all modeled runtime projects and refuses replacement preparation on restart.</summary>
    /// <returns>A task that represents the verification.</returns>
    [Fact]
    public async Task ActualFixtureFinalizationBindsEveryRuntimeIncludingThePortal()
        => await RunActualFixtureFinalizationAsync(faultBeforeBuild: false).ConfigureAwait(true);

    /// <summary>A fault after successful package finalization and before Build completes still releases the owned private catalog.</summary>
    /// <returns>A task that represents the verification.</returns>
    [Fact]
    public async Task FaultBeforeBuildAfterFinalizationReleasesOwnedCatalog()
        => await RunActualFixtureFinalizationAsync(faultBeforeBuild: true).ConfigureAwait(true);

    private static async Task RunActualFixtureFinalizationAsync(bool faultBeforeBuild)
    {
        string directory = Path.Combine(Path.GetTempPath(), "parties-complete-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        bool observed = false;
        IDistributedApplicationTestingBuilder? selectedBuilder = null;
        Dictionary<string, JsonObject> receipts = new(StringComparer.Ordinal);
        Dictionary<string, JsonObject> byName = new(StringComparer.Ordinal);
        string sdk = typeof(RouteContractDeclaration).Assembly.GetName().Version!.ToString(3);
        try
        {
            var fixture = new PartiesAspireTopologyFixture(async (session, token) =>
            {
                IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                    [.. session.Arguments(false, false, includeMemoriesDomainService: true), "--EnableMemoriesSearch=true", "--EnableEventStoreSampleUi=true",
                        "--Parties:Testing:UsePackagedGateway=true", "--Dapr:Mtls:Enabled=false",
                        "--Dapr:Mtls:ConfigurationDirectory=" + directory], token).ConfigureAwait(false);
                selectedBuilder = builder;
                // The launch model remains the real AppHost graph. Only two receivers are needed
                // to exercise the actual public SDK composition without starting infrastructure.
                string acl = Path.Combine(directory, "closed-acl.json");
                await File.WriteAllTextAsync(acl, "{\"apiVersion\":\"dapr.io/v1alpha1\",\"kind\":\"Configuration\",\"metadata\":{\"name\":\"model\"},\"spec\":{\"accessControl\":{\"defaultAction\":\"deny\",\"trustDomain\":\"public\",\"policies\":[]}}}", token).ConfigureAwait(false);
                foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>())
                {
                    foreach (DaprSidecarAnnotation sidecar in project.Annotations.OfType<DaprSidecarAnnotation>().ToArray())
                    {
                        if (project.Name is not ("eventstore" or "parties")) { project.Annotations.Remove(sidecar); continue; }
                        DaprSidecarOptionsAnnotation options = sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                        sidecar.Sidecar.Annotations[sidecar.Sidecar.Annotations.IndexOf(options)] = new DaprSidecarOptionsAnnotation(options.Options with { Config = acl });
                    }
                    if (project.Name is "parties-mcp") { continue; }
                    string target;
                    if (project.Name == "parties") { target = typeof(Hexalith.Parties.Domain.PartyAggregate).Assembly.Location; }
                    else
                    {
                        target = Path.Combine(directory, "prepared", project.Name, "Host.dll");
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.Copy(typeof(Bootstrap).Assembly.Location, target);
                    }
                    JsonObject receipt = new()
                    {
                        ["project"] = project.GetProjectMetadata().ProjectPath, ["sdkVersion"] = sdk,
                        ["targetPath"] = target, ["completedNs"] = 1,
                        ["files"] = new JsonObject { [target] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(target))) },
                    };
                    receipts[project.GetProjectMetadata().ProjectPath] = receipt;
                    byName[project.Name] = receipt;
                }
                string repo = new DirectoryInfo(builder.AppHostDirectory).Parent!.Parent!.FullName;
                string adapterProject = Path.Combine(repo, "src", "Hexalith.Parties.EventStoreExtensions", "Hexalith.Parties.EventStoreExtensions.csproj");
                string adapter = typeof(Hexalith.Parties.EventStoreExtensions.PartiesCatalogAdapters).Assembly.Location;
                receipts[adapterProject] = new JsonObject { ["project"] = adapterProject, ["sdkVersion"] = sdk,
                    ["targetPath"] = adapter, ["completedNs"] = 1,
                    ["files"] = new JsonObject { [adapter] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(adapter))) } };
                Bootstrap.UsePreparedInputsForModelTest(builder, _ => Task.FromResult(new JsonObject
                {
                    ["HexalithEventStoreVersion"] = sdk, ["HexalithTenantsVersion"] = "7.0.5", ["HexalithMemoriesVersion"] = "3.0.25",
                    ["HexalithFrontComposerVersion"] = "4.7.6", ["HexalithMcpCliVersion"] = "2.0.1",
                }), (project, version, _) =>
                {
                    version.ShouldBe(sdk);
                    JsonObject current = receipts[project];
                    foreach ((string file, JsonNode? hash) in current["files"]!.AsObject())
                        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))).ShouldBe(hash!.GetValue<string>());
                    return Task.FromResult(current.DeepClone().AsObject());
                });
                if (faultBeforeBuild)
                {
                    // AddResource admits this name under the default policy. Aspire Build re-validates it
                    // under the last policy after the suspended entry point resumes, so BuildAsync itself
                    // faults after package finalization and returns no application.
                    _ = builder.AddResource(new ParameterResource("parties-build-fault", _ => "fault"))
                        .WithAnnotation(new NameValidationPolicyAnnotation { MaxLength = 1 });
                }
                return builder;
            }, async (app, token) =>
            {
                DistributedApplicationModel model = app.Services.GetRequiredService<DistributedApplicationModel>();
                // This controlled startup never schedules a process. Publish that model
                // state for projects and Toolkit CLI identities so ordinary cleanup can
                // observe that every worker is still not started.
                foreach (IResource resource in model.Resources.Where(resource => resource is ProjectResource or ExecutableResource))
                    await app.ResourceNotifications.PublishUpdateAsync(resource, snapshot => snapshot with
                    { State = new ResourceStateSnapshot(KnownResourceStates.NotStarted, null), Commands = [] }).ConfigureAwait(false);
                foreach (DaprSidecarAnnotation sidecar in model.Resources.SelectMany(resource => resource.Annotations.OfType<DaprSidecarAnnotation>()))
                    await app.ResourceNotifications.PublishUpdateAsync(sidecar.Sidecar, sidecar.Sidecar.Name + "-cli", snapshot => snapshot with
                    { State = new ResourceStateSnapshot(KnownResourceStates.NotStarted, null), Commands = [] }).ConfigureAwait(false);
                ProjectResource[] projects = model.Resources.OfType<ProjectResource>()
                    .Where(project => byName.ContainsKey(project.Name)).ToArray();
                projects.Length.ShouldBe(10);
                projects.ShouldContain(project => project.Name == "parties-ui");
                foreach (ProjectResource project in projects)
                {
                    ExecutableAnnotation executable = project.Annotations.OfType<ExecutableAnnotation>().Single();
                    executable.Command.ShouldBe("dotnet");
                    IExecutionConfigurationResult launch = await ExecutionConfigurationBuilder.Create(project).WithArgumentsConfig()
                        .BuildAsync(selectedBuilder!.ExecutionContext, NullLogger.Instance, token).ConfigureAwait(false);
                    launch.Exception.ShouldBeNull();
                    launch.Arguments.Select(argument => argument.Value).ShouldBe(new[] { "exec", byName[project.Name]["targetPath"]!.GetValue<string>() });
                }
                ProjectResource portal = projects.Single(project => project.Name == "parties-ui");
                for (int start = 0; start < 2; start++)
                    await selectedBuilder!.Eventing.PublishAsync(new BeforeResourceStartedEvent(portal, app.Services), token).ConfigureAwait(false);
                byName["parties-ui"]["completedNs"] = 2;
                await Should.ThrowAsync<InvalidDataException>(() => selectedBuilder!.Eventing.PublishAsync(
                    new BeforeResourceStartedEvent(portal, app.Services), token)).ConfigureAwait(false);
                Directory.EnumerateDirectories(directory, "parties-*-catalog").ShouldHaveSingleItem();
                observed = true;
                throw new InvalidOperationException("Model inspection finished before any infrastructure start.");
            });
            bool faulted = false;
            fixture.ObserveInitializationFailureAsync = failure =>
            {
                // Runs before partial-startup teardown: the finalized private catalog still exists.
                Directory.EnumerateDirectories(directory, "parties-*-catalog").ToArray().ShouldHaveSingleItem(fixture.UnavailableReason);
                faulted = failure is ArgumentException && failure.Message.Contains("parties-build-fault", StringComparison.Ordinal)
                    && fixture.InitializationPhase == "build-application";
                return Task.CompletedTask;
            };

            await using (fixture.ConfigureAwait(true))
            {
                await fixture.InitializeAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                fixture.IsAvailable.ShouldBeFalse();
                faulted.ShouldBe(faultBeforeBuild, fixture.UnavailableReason);
                observed.ShouldBe(!faultBeforeBuild, fixture.UnavailableReason);
            }

            // Neither path starts the application. A Build fault leaves no application DI, and a built
            // application's DI can finish only after fixture disposal returns, so the fixture must
            // release the owned composition and private directory before its disposal completes.
            Directory.EnumerateDirectories(directory, "parties-*-catalog").ToArray().ShouldBeEmpty();
            // A later release after fixture disposal is an idempotent no-op.
            await Bootstrap.ReleaseUnstartedOwnedTestModelAsync(selectedBuilder!).ConfigureAwait(true);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Only the actual gateway sidecar HTTP listener becomes direct and uses the selected session port.</summary>
    /// <param name="proxied">Whether Toolkit initially models a proxy for this listener.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OwnerHttpListenerUsesReservedPortWithoutChangingOtherEndpoints(bool proxied)
    {
        const int Port = 24681;
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        IResourceBuilder<ProjectResource> gateway = builder.AddResource(new ProjectResource("eventstore"))
            .WithHttpEndpoint(name: "http", port: 24682, targetPort: 8080)
            .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = "eventstore-private", DaprHttpPort = Port }));
        IDaprSidecarResource sidecar = gateway.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        IResourceBuilder<ExecutableResource> cli = builder.AddExecutable(sidecar.Name + "-cli", "dapr", Path.GetTempPath())
            .WithHttpEndpoint(name: "http", port: Port, targetPort: proxied ? null : Port, isProxied: proxied)
            .WithEndpoint(name: "grpc", port: 24683, targetPort: 24683, scheme: "tcp");
        _ = cli.WithArgs("--app-id", "eventstore-private", "--dapr-http-port", cli.GetEndpoint("http").Property(EndpointProperty.TargetPort));
        EndpointAnnotation gatewayHttp = gateway.Resource.Annotations.OfType<EndpointAnnotation>().Single();
        EndpointAnnotation grpc = cli.Resource.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc");
        var model = new DistributedApplicationModel(builder.Resources);
        Bootstrap.ConfigurePrivateOwnerHttpListener(builder, model);
        EndpointAnnotation http = cli.Resource.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http");
        http.IsProxied.ShouldBeFalse();
        http.Port.ShouldBe(Port);
        http.TargetPort.ShouldBe(Port);
        http.AllocatedEndpoint.ShouldBeNull("Only the orchestrator may allocate the actual listener");
        gatewayHttp.IsProxied.ShouldBeTrue();
        gatewayHttp.Port.ShouldBe(24682);
        gatewayHttp.TargetPort.ShouldBe(8080);
        grpc.IsProxied.ShouldBeTrue();
        grpc.Port.ShouldBe(24683);
        grpc.TargetPort.ShouldBe(24683);
        // A model allocation fixture checks that the unchanged public CLI value provider
        // now resolves the same port required by the SDK's independent owner observer.
        http.AllocatedEndpoint = new AllocatedEndpoint(http, "localhost", Port);
        List<object> arguments = [];
        foreach (CommandLineArgsCallbackAnnotation annotation in cli.Resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            await annotation.Callback(new CommandLineArgsCallbackContext(arguments, cli.Resource, TestContext.Current.CancellationToken)
            { ExecutionContext = builder.ExecutionContext }).ConfigureAwait(true);
        arguments[0].ShouldBe("--app-id");
        arguments[1].ShouldBe("eventstore-private");
        arguments[2].ShouldBe("--dapr-http-port");
        (await ((IValueProvider)arguments[3]).GetValueAsync(TestContext.Current.CancellationToken)).ShouldBe(Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>A missing fixed transport, wrong protocol or already allocated listener cannot be rewritten into owner evidence.</summary>
    /// <param name="boundary">The selected owner transport violation.</param>
    [Theory]
    [InlineData("missing-port")]
    [InlineData("invalid-port")]
    [InlineData("wrong-scheme")]
    [InlineData("allocated")]
    public void OwnerHttpListenerRefusesInvalidOrAlreadyAllocatedTransport(string boundary)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        IResourceBuilder<ProjectResource> gateway = builder.AddResource(new ProjectResource("eventstore"))
            .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions
            { AppId = "eventstore-private", DaprHttpPort = boundary == "missing-port" ? null : boundary == "invalid-port" ? 80 : 24681 }));
        IDaprSidecarResource sidecar = gateway.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        IResourceBuilder<ExecutableResource> cli = builder.AddExecutable(sidecar.Name + "-cli", "dapr", Path.GetTempPath())
            .WithEndpoint(name: "http", port: 24681, targetPort: 24681, scheme: boundary == "wrong-scheme" ? "https" : "http");
        EndpointAnnotation http = cli.Resource.Annotations.OfType<EndpointAnnotation>().Single();
        if (boundary == "allocated") { http.AllocatedEndpoint = new AllocatedEndpoint(http, "localhost", 24681); }
        Should.Throw<InvalidDataException>(() => Bootstrap.ConfigurePrivateOwnerHttpListener(builder, new DistributedApplicationModel(builder.Resources)));
        http.IsProxied.ShouldBeTrue();
    }

    /// <summary>The owned Memories wrapper preserves generated custody until the package catalog hands off its selected token.</summary>
    [Theory]
    [InlineData("eventstore")]
    [InlineData("memories")]
    [InlineData("memories-domain-service")]
    [InlineData("parties")]
    [InlineData("tenants")]
    [InlineData("sample")]
    public async Task OwnedMemoriesChannelOverrideDefersUntilGeneratedCustodyHasBeenReused(string host)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Parties:Testing:UsePackagedGateway"] = "true" });
        IResourceBuilder<ProjectResource> project = builder.AddResource(new ProjectResource(host))
            .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = host }));
        _ = project.WithGeneratedEventStoreAppChannelToken();
        ParameterResource original = builder.Resources.OfType<ParameterResource>().Single(parameter => parameter.Name == host + "-app-api-token");
        _ = PartyMemoryOwnedRuntimeTests.ConfigureOwnedAppChannel(project, "owned-synthetic-token");
        _ = project.WithGeneratedEventStoreAppChannelToken();
        builder.Resources.OfType<ParameterResource>().Single(parameter => parameter.Name == host + "-app-api-token").ShouldBeSameAs(original);
        ParameterResource selected = builder.Resources.OfType<ParameterResource>().Single(parameter => parameter.Name == "fixture-" + host + "-channel-token");
        _ = project.WithEventStoreAppChannelToken(builder.CreateResourceBuilder(selected));
        foreach (IResource resource in new IResource[] { project.Resource, project.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar })
        {
            var environment = new EnvironmentCallbackContext(builder.ExecutionContext, resource, new Dictionary<string, object>(), TestContext.Current.CancellationToken);
            foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await annotation.Callback(environment).ConfigureAwait(true);
            environment.EnvironmentVariables["APP_API_TOKEN"].ShouldBeSameAs(selected);
        }
    }

    /// <summary>The actual SDK full-composition callee reuses the native gateway token, then preserves the chosen private channel.</summary>
    /// <param name="host">The original gateway or a fixture-overridden domain host.</param>
    /// <param name="fixtureOverride">Whether the fixture already selected its stronger explicit token.</param>
    [Theory]
    [InlineData("eventstore", false)]
    [InlineData("parties", true)]
    [InlineData("sample", true)]
    public async Task PublishedSdkChannelEnrollmentPreservesNativeGenerationAndFinalPrivateBindings(string host, bool fixtureOverride)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        IResourceBuilder<ProjectResource> project = builder.AddResource(new ProjectResource(host))
            .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = host }));
        ParameterResource? original = null;
        if (host == "eventstore")
        {
            _ = project.WithGeneratedEventStoreAppChannelToken();
            original = builder.Resources.OfType<ParameterResource>().Single(parameter => parameter.Name == host + "-app-api-token");
        }
        IResourceBuilder<ParameterResource> selected = Bootstrap.CreatePrivateAppChannel(project);
        if (fixtureOverride)
        {
            selected = builder.AddParameter("fixture-" + host + "-channel-token", Guid.NewGuid().ToString("N"), secret: true);
            _ = project.WithEventStoreAppChannelToken(selected);
        }
        // This is the published method invoked by full catalog composition. An early
        // explicit gateway override would clear its SDK binding and duplicate the name.
        _ = project.WithGeneratedEventStoreAppChannelToken();
        ParameterResource generated = builder.Resources.OfType<ParameterResource>().Single(parameter => parameter.Name == host + "-app-api-token");
        if (original is not null) { generated.ShouldBeSameAs(original); }
        generated.Secret.ShouldBeTrue();
        _ = project.WithEventStoreAppChannelToken(selected);
        IDaprSidecarResource sidecarResource = project.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        foreach (IResource resource in new IResource[] { project.Resource, sidecarResource })
        {
            var environment = new EnvironmentCallbackContext(builder.ExecutionContext, resource,
                new Dictionary<string, object>(), TestContext.Current.CancellationToken);
            foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await annotation.Callback(environment).ConfigureAwait(true);
            environment.EnvironmentVariables["APP_API_TOKEN"].ShouldBeSameAs(selected.Resource);
        }
        builder.Resources.ShouldContain(resource => ReferenceEquals(resource, generated));
        builder.Resources.ShouldContain(resource => ReferenceEquals(resource, selected.Resource));
    }

    /// <summary>A successfully re-created receipt cannot replace the owner bytes originally bound to the catalog.</summary>
    /// <param name="changed">The independently validated replacement receipt field.</param>
    [Theory]
    [InlineData("targetPath")]
    [InlineData("files")]
    [InlineData("completedNs")]
    public void RepreparedRuntimeReceiptCannotReplaceTheSelectedReceipt(string changed)
    {
        JsonObject selected = JsonNode.Parse("""{"targetPath":"/private/first/owner.dll","files":{"owner.dll":"original"},"completedNs":1}""")!.AsObject();
        JsonObject replacement = selected.DeepClone().AsObject();
        if (changed == "files") { replacement["files"]!["owner.dll"] = "replacement"; }
        else if (changed == "completedNs") { replacement[changed] = 2; }
        else { replacement[changed] = "/private/second/owner.dll"; }
        Bootstrap.RequireSelectedPreparation(selected, selected.DeepClone().AsObject());
        Should.Throw<InvalidDataException>(() => Bootstrap.RequireSelectedPreparation(selected, replacement));
    }

    /// <summary>Both ordinary and isolated preparation use their actual absolute manifest path.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparedManifestSelectsDefaultOrExplicitIsolatedArtifact(bool isolated)
    {
        string root = Path.Combine(Path.GetTempPath(), "parties-manifest-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = isolated ? Path.Combine(root, "isolated", "prepared.json")
                : Path.Combine(root, "artifacts", "runtime-profile", "Debug", "prepared.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{}");
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Parties:Testing:PreparedRuntimeManifest"] = isolated ? path : null }).Build();
            Bootstrap.PreparedManifestPath(configuration, root, "Debug").ShouldBe(path);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>Unavailable, nonregular, empty, oversized and linked manifests cannot qualify any prepared owner.</summary>
    [Theory]
    [InlineData("relative")]
    [InlineData("missing")]
    [InlineData("directory")]
    [InlineData("empty")]
    [InlineData("oversized")]
    [InlineData("symlink")]
    public void PreparedManifestRefusesUnavailableOrNonregularPaths(string boundary)
    {
        string root = Path.Combine(Path.GetTempPath(), "parties-manifest-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = boundary switch
            {
                "relative" => "prepared.json",
                "missing" => Path.Combine(root, "missing.json"),
                "directory" => root,
                _ => Path.Combine(root, "prepared.json"),
            };
            if (boundary == "empty") { File.WriteAllBytes(path, []); }
            else if (boundary == "oversized")
            {
                using FileStream oversized = new(path, FileMode.CreateNew);
                oversized.SetLength(16_777_217);
            }
            else if (boundary == "symlink")
            {
                string target = Path.Combine(root, "actual.json");
                File.WriteAllText(target, "{}");
                _ = File.CreateSymbolicLink(path, target);
            }
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Parties:Testing:PreparedRuntimeManifest"] = path }).Build();
            Should.Throw<InvalidDataException>(() => Bootstrap.PreparedManifestPath(configuration, root, "Debug"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>A new ring cannot bind retained storage, mounted storage, or another model's resource.</summary>
    /// <param name="boundary">The backend ownership violation.</param>
    [Theory]
    [InlineData("persistent")]
    [InlineData("mounted")]
    [InlineData("foreign")]
    public void FreshBackendRefusesRetainedOrForeignStorage(string boundary)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        IDistributedApplicationBuilder foreign = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        IResourceBuilder<ContainerResource> redis = (boundary == "foreign" ? foreign : builder).AddContainer("fixture-redis", "redis");
        if (boundary == "persistent") { _ = redis.WithLifetime(ContainerLifetime.Persistent); }
        if (boundary == "mounted") { _ = redis.WithVolume("retained-catalog-model-test", "/data"); }
        Should.Throw<InvalidOperationException>(() => Bootstrap.RequireFreshTestBackend(builder, redis));
    }

    /// <summary>The fixture's fresh unmounted session resource may provision a new ring.</summary>
    [Fact]
    public void FreshSessionBackendIsAccepted()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        IResourceBuilder<ContainerResource> redis = builder.AddContainer("fixture-redis", "redis");
        Should.NotThrow(() => Bootstrap.RequireFreshTestBackend(builder, redis));
    }

    /// <summary>The real launch prefix selects the prepared PE, and each native start rechecks its preparation.</summary>
    [Fact]
    public async Task PreparedLaunchRechecksAndExecutesTheExactEntryPE()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        IResourceBuilder<ProjectResource> project = builder.AddResource(new ProjectResource("prepared-owner"));
        string target = Path.Combine(Path.GetTempPath(), "prepared-owner", "Hexalith.Owner.dll");
        int checks = 0;
        Bootstrap.BindPreparedLaunch(project, new JsonObject { ["targetPath"] = target }, _ =>
        {
            checks++;
            return Task.CompletedTask;
        });
        for (int attempt = 0; attempt < 2; attempt++)
        {
            IExecutionConfigurationResult launch = await ExecutionConfigurationBuilder.Create(project.Resource).WithArgumentsConfig()
                .BuildAsync(builder.ExecutionContext, NullLogger.Instance, TestContext.Current.CancellationToken).ConfigureAwait(true);
            launch.Exception.ShouldBeNull();
            launch.Arguments.Select(argument => argument.Value).ShouldBe(new[] { "exec", target });
        }
        checks.ShouldBe(1, "Aspire caches the tool prefix");
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        for (int attempt = 0; attempt < 2; attempt++)
        {
            await builder.Eventing.PublishAsync(new BeforeResourceStartedEvent(project.Resource, services), TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        checks.ShouldBe(3, "the per-start guard must run even after the prefix was cached");
    }

    /// <summary>Changed preparation refuses before any launch prefix can execute the stale owner.</summary>
    [Fact]
    public async Task ChangedPreparationRefusesBeforeSelectingLaunchArguments()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        IResourceBuilder<ProjectResource> project = builder.AddResource(new ProjectResource("prepared-owner"));
        bool changed = false;
        Bootstrap.BindPreparedLaunch(project, new JsonObject { ["targetPath"] = Path.Combine(Path.GetTempPath(), "Hexalith.Owner.dll") },
            _ => changed ? Task.FromException(new InvalidDataException("prepared PE changed")) : Task.CompletedTask);
        IExecutionConfigurationResult launch = await ExecutionConfigurationBuilder.Create(project.Resource).WithArgumentsConfig()
            .BuildAsync(builder.ExecutionContext, NullLogger.Instance, TestContext.Current.CancellationToken).ConfigureAwait(true);
        launch.Exception.ShouldBeNull();
        changed = true;
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await Should.ThrowAsync<InvalidDataException>(() => builder.Eventing.PublishAsync(
            new BeforeResourceStartedEvent(project.Resource, services), TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    /// <summary>Both UIs retain strict receiver-free SDK enrollment with the same dedicated name-prefixed component.</summary>
    /// <param name="host">The selected private UI participant.</param>
    [Theory]
    [InlineData("sample-blazor-ui")]
    [InlineData("eventstore-admin-ui")]
    public async Task InstalledAuxiliaryProfileRetainsDedicatedComponentAndRejectsSdkReceivers(string host)
    {
        string directory = Path.Combine(Path.GetTempPath(), "parties-aux-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string component = Path.Combine(directory, "component.json");
            string acl = Path.Combine(directory, "acl.json");
            await File.WriteAllTextAsync(component, Component(host).ToJsonString(), TestContext.Current.CancellationToken).ConfigureAwait(true);
            await File.WriteAllTextAsync(acl, """{"spec":{"accessControl":{"defaultAction":"deny","trustDomain":"public","policies":[]}}}""", TestContext.Current.CancellationToken).ConfigureAwait(true);
            byte[] original = await File.ReadAllBytesAsync(component, TestContext.Current.CancellationToken).ConfigureAwait(true);
            IDictionary<string, object> environment = await RenderAsync(host, component).ConfigureAwait(true);
            environment.ShouldNotContainKey("EventStore__RouteCatalog__DevelopmentBootstrap__AuthorityRole");
            environment["EventStore__RouteCatalog__DevelopmentBootstrap__Role"].ShouldBe("aux-client");
            environment["EventStore__RouteCatalog__StateStore"].ShouldBe("routing-model-authority");
            (await File.ReadAllBytesAsync(component, TestContext.Current.CancellationToken)).ShouldBe(original);
            Dictionary<string, string?> values = environment.ToDictionary(pair => pair.Key.Replace("__", ":", StringComparison.Ordinal), pair => (string?)pair.Value, StringComparer.Ordinal);
            const string Prefix = "EventStore:RouteCatalog:DevelopmentBootstrap:";
            values[Prefix + "Directory"] = directory;
            values[Prefix + "HostId"] = host;
            values[Prefix + "CoordinatorHostId"] = "gateway";
            values[Prefix + "AttemptId"] = "model-attempt";
            values[Prefix + "RequiredHostIds:0"] = "gateway";
            values[Prefix + "RequiredHostIds:1"] = host;
            values[Prefix + "SelectedAppIds:0"] = "gateway";
            values[Prefix + "SelectedAppIds:1"] = host;
            values[Prefix + "AccessControlPath"] = acl;
            values[Prefix + "DeclaredInvocations"] = "[]";
            values["EventStore:DomainService:AppId"] = host;
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.Configuration.AddInMemoryCollection(values);
            new DevelopmentAuxiliaryClientCatalogHostingStartup().Configure(builder.WebHost);
            Should.NotThrow(() => builder.Services.AddEventStoreDevelopmentCallerCatalog(builder.Configuration, host));
            WebApplication app = builder.Build();
            await using var lifetime = app.ConfigureAwait(true);
            app.UseEventStoreDevelopmentCallerCatalog().ShouldBeSameAs(app);
            app.MapGet("/ui", () => "UI");
            var observation = await DevelopmentAuxiliaryClientCatalog.CaptureAsync(app.Services, app.Configuration, TestContext.Current.CancellationToken).ConfigureAwait(true);
            observation.Declarations.ShouldBeEmpty();
            observation.AuxiliaryOperations.ShouldBeEmpty();
            observation.ReceiverSurface!.FenceVersion.ShouldBe("eventstore-development-client-only-v1");
            RouteCatalogTopologyValidator.ValidateDevelopment(original, await File.ReadAllBytesAsync(acl, TestContext.Current.CancellationToken),
                new(3, 1, "Development", false, [], [], [], "", "", []),
                [new("gateway", "gateway", "", "", ""), new(host, host, "", "", "")], host, [], observation.ReceiverSurface,
                ["gateway", host], []);
            // Materialize the forbidden endpoint explicitly: an unstarted WebApplication
            // has not connected its route-builder sources to the hosting data source yet.
            using ServiceProvider receiver = new ServiceCollection().AddSingleton<IHostEnvironment>(builder.Environment)
                .AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource(new RouteEndpoint(_ => Task.CompletedTask,
                    RoutePatternFactory.Parse("/query"), 0, new EndpointMetadataCollection(new HttpMethodMetadata(["POST"])), "SDK receiver control")))
                .BuildServiceProvider();
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => DevelopmentAuxiliaryClientCatalog.CaptureAsync(
                receiver, app.Configuration, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>The auxiliary profile cannot use legacy/shared, actor-owned, foreign or incorrectly labelled state.</summary>
    /// <param name="mutation">The dedicated component policy violation.</param>
    [Theory]
    [InlineData("shared-prefix")]
    [InlineData("missing-role")]
    [InlineData("actor-store")]
    [InlineData("foreign-name")]
    public async Task AuxiliaryProfileRefusesIncompatibleAuthority(string mutation)
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-aux-component-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonObject component = Component("sample-blazor-ui");
            if (mutation == "shared-prefix") { component["spec"]!["metadata"]![0]!["value"] = "none"; }
            else if (mutation == "missing-role") { component["metadata"]!["labels"]!.AsObject().Clear(); }
            else if (mutation == "foreign-name") { component["metadata"]!["name"] = "another-authority"; }
            else { component["spec"]!["metadata"]!.AsArray().Add(new JsonObject { ["name"] = "actorStateStore", ["value"] = "true" }); }
            await File.WriteAllTextAsync(path, component.ToJsonString(), TestContext.Current.CancellationToken).ConfigureAwait(true);
            await Should.ThrowAsync<InvalidDataException>(() => RenderAsync("sample-blazor-ui", path)).ConfigureAwait(true);
        }
        finally { File.Delete(path); }
    }

    private static JsonObject Component(string host) => JsonNode.Parse($$$"""
        {"kind":"Component","metadata":{"name":"routing-model-authority","labels":{"hexalith.io/component-role":"eventstore-routing-authority-v1"}},
         "spec":{"type":"state.redis","metadata":[{"name":"keyPrefix","value":"name"}]},"scopes":["gateway","{{{host}}}"]}
        """)!.AsObject();

    private static async Task<IDictionary<string, object>> RenderAsync(string host, string component)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        IResourceBuilder<ProjectResource> project = builder.AddResource(new ProjectResource(host))
            .WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__Role", "aux-client")
            .WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__AuthorityRole", RouteCatalogAuthorityComponentPolicy.IsolatedRole)
            .WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__ComponentPath", component)
            .WithEnvironment("EventStore__RouteCatalog__StateStore", "routing-model-authority");
        Bootstrap.BindAuxiliaryClientProfile(project, "routing-model-authority");
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, project.Resource, new Dictionary<string, object>(), TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation annotation in project.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(context).ConfigureAwait(true);
        return context.EnvironmentVariables;
    }
#endif
}
