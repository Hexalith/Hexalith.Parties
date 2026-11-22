extern alias apphost;

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Aspire;
using Hexalith.Parties.IntegrationTests.HealthChecks;
using Hexalith.Parties.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Owns the actual authenticated selected-source Memories qualification topology; no in-memory command store.</summary>
[Collection("Non-parallel")]
public sealed class PartyMemoryOwnedRuntimeTests
{
    [Fact(Timeout = 600000)]
    public Task ActualSelectedMemoriesRunsTheRecoveryAndPhysicalErasureControl() => ExecuteOwnedRuntimeAsync(focused: false, TestContext.Current.CancellationToken);

    /// <summary>Qualifies bounded unchanged-URI refresh, erasure, survivors and authoritative replay independently of the broader failed-producer gate.</summary>
    [Fact(Timeout = 600000)]
    public Task ActualSelectedMemoriesRefreshErasureAndReplayControl() => ExecuteOwnedRuntimeAsync(focused: true, TestContext.Current.CancellationToken);

    /// <summary>Qualifies the current source issuer only within this new disposable session's custody.</summary>
    [Fact(Timeout = 600000)]
    public Task ActualCurrentSourceMemoriesRunsAuthorityAndPhysicalErasureControl()
        => ExecuteOwnedRuntimeAsync(focused: false, TestContext.Current.CancellationToken, currentSourceAuthority: true);

    /// <summary>Qualifies five recovery boundaries through six observed replacements of the owned producer workload.</summary>
    [Fact(Timeout = 2100000)]
    public Task ActualCurrentSourceMemoriesRestartsAcrossFiveRecoveryBoundaries()
        => ExecuteOwnedRuntimeAsync(focused: false, TestContext.Current.CancellationToken, currentSourceAuthority: true, producerRestart: true);

    /// <summary>Cancels the owned admitted replacement without stopping Aspire's control plane and requires completed cleanup.</summary>
    [Fact(Timeout = 600000)]
    public Task ActualCurrentSourceCanceledRestartCompletesOwnedCleanup()
        => ExecuteOwnedRuntimeAsync(focused: false, TestContext.Current.CancellationToken,
            currentSourceAuthority: true, producerRestart: true, cancelRestart: true);

    private static async Task ExecuteOwnedRuntimeAsync(bool focused, CancellationToken token, bool currentSourceAuthority = false,
        bool producerRestart = false, bool cancelRestart = false)
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PARTIES_MEMORIES_OWNED_RUNTIME") == "1",
            "Set PARTIES_MEMORIES_OWNED_RUNTIME=1 to start the owned authenticated source topology.");
        string child = MemoryRuntimeIdentity.SelectChild(typeof(PartyMemoryOwnedRuntimeTests).Assembly);
        PartyMemoryRunnerInterruptionHandoff? interruption = producerRestart ? PartyMemoryRunnerInterruptionHandoff.Read() : null;
        PartiesTopologySession session = interruption?.CreateSession() ?? PartiesTopologySession.Create(isolatedIdentities: true);
        string generation = Guid.NewGuid().ToString("N");
        string exclusion = "owned-disposable-session:" + session.Id + ":" + generation;
        string appToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string apiToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using RSA proofKey = RSA.Create(2048);
        string proofPrivateKey = Convert.ToBase64String(proofKey.ExportPkcs8PrivateKey());
        string proofPublicKey = Convert.ToBase64String(proofKey.ExportSubjectPublicKeyInfo());
        string privateRoot = Path.Combine(Path.GetTempPath(), "parties-five-memory-runtime-" + session.Id);
        Directory.CreateDirectory(privateRoot);
        string restartDirectory = Path.Combine(privateRoot, "producer-restart");
        if (producerRestart) { Directory.CreateDirectory(restartDirectory); }
        string restartSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        if (!OperatingSystem.IsWindows()) { File.SetUnixFileMode(privateRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        var fixture = new PartiesAspireTopologyFixture(async (owned, cancellation) =>
        {
            IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                [.. owned.Arguments(false, true, includeMemoriesDomainService: true), "--EnableMemoriesSearch=true", "--EnableEventStoreSampleUi=true"], cancellation).ConfigureAwait(false);
            PartiesMtlsProfile.ConfigureOptionalDependencies(builder);
            IResourceBuilder<ProjectResource> memories = builder.CreateResourceBuilder<ProjectResource>("memories");
            _ = memories.WithEnvironment("Authentication__JwtBearer__Authority", string.Empty)
                .WithEnvironment("Authentication__JwtBearer__Issuer", PartiesAspireTopologyFixture.JwtIssuer)
                .WithEnvironment("Authentication__JwtBearer__SigningKey", PartiesAspireTopologyFixture.JwtSigningKey)
                .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-memories")
                .WithEnvironment("Authentication__JwtBearer__TenantClaimName", "tenant")
                .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
                .WithEnvironment("Authentication__WorkloadIssuer__Workload", owned.AppId("memories"))
                .WithEnvironment("EventStore__CommandGateway__AppId", owned.AppId("eventstore"))
                .WithEnvironment("EventStore__CommandGateway__Audience", owned.AppId("eventstore"))
                .WithEnvironment("Memories__Testing__UseFakeEmbedding", "true")
                .WithEnvironment("Memories__Testing__EnableSourceInventoryQualification", "true")
                .WithEnvironment("Memories__Testing__SourceInventoryAdmissionDelayMilliseconds", "5000")
                .WithEnvironment("Memories__Testing__SourceInventoryOutputDelayMilliseconds", "5000")
                .WithEnvironment("Memories__Testing__SourceInventoryDeletionDelayMilliseconds", "5000")
                .WithEnvironment("EventStore__CommandGateway__BoundCommand__PrivateKeyPkcs8Base64", builder.AddParameter("owned-command-proof-key", proofPrivateKey, secret: true))
                .WithEnvironment("DAPR_API_TOKEN", apiToken);
            if (currentSourceAuthority)
            {
                _ = memories.WithEnvironment("Ingestion__OwnershipGeneration", generation)
                    .WithEnvironment("Ingestion__OwnershipWriterExclusionEvidenceId", exclusion);
            }
            _ = ConfigureOwnedAppChannel(memories, appToken);
            DaprSidecarAnnotation sidecar = memories.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single();
            memories.Resource.Annotations.Add(new EnvironmentCallbackAnnotation(async context =>
            {
                IResourceWithEndpoints cli = (IResourceWithEndpoints)builder.Resources.Single(resource => resource.Name == sidecar.Sidecar.Name + "-cli");
                string? port = await new EndpointReference(cli, "http").Property(EndpointProperty.Port)
                    .GetValueAsync(context.CancellationToken).ConfigureAwait(false);
                context.EnvironmentVariables["EventStoreIntegration__CommandGateway__BaseAddress"] =
                    $"http://localhost:{port}/v1.0/invoke/{owned.AppId("eventstore")}/method/";
            }));
            sidecar.Sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context => context.EnvironmentVariables["DAPR_API_TOKEN"] = apiToken));
            foreach (string targetName in new[] { "parties", "tenants", "sample" })
            {
                IResourceBuilder<ProjectResource> target = builder.CreateResourceBuilder<ProjectResource>(targetName);
                _ = ConfigureOwnedAppChannel(target, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
                if (targetName == "sample")
                {
                    _ = target.WithEnvironment("Authentication__JwtBearer__Authority", string.Empty)
                        .WithEnvironment("Authentication__JwtBearer__Issuer", PartiesAspireTopologyFixture.JwtIssuer)
                        .WithEnvironment("Authentication__JwtBearer__SigningKey", PartiesAspireTopologyFixture.JwtSigningKey)
                        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
                        .WithEnvironment("Authentication__Workload__AllowedCallers__0", owned.AppId("eventstore"))
                        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
                        .WithEnvironment("DOTNET_ENVIRONMENT", "Development");
                }
            }
            IResourceBuilder<ProjectResource> gateway = builder.CreateResourceBuilder<ProjectResource>("eventstore");
            _ = ConfigureOwnedAppChannel(gateway, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
                .WithEnvironment("Authentication__DaprInternal__AllowedCallers__10", owned.AppId("memories"))
                .WithEnvironment("Authentication__DaprInternal__Audience", owned.AppId("eventstore"))
                .WithEnvironment("Authentication__BoundStreamRead__PublicKeys__" + owned.AppId("memories"), proofPublicKey)
                .WithEnvironment("Authentication__BoundCommand__AllowedDomains__" + owned.AppId("memories"), "memories-tenants,memories-cases,memories-memory-units")
                .WithEnvironment("Authentication__BoundStreamRead__AllowedDomains__" + owned.AppId("memories"), "memories-tenants,memories-cases,memories-memory-units")
                // This component scope never invokes the unrelated Party/sample/system domains.
                .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__AppId", string.Empty)
                .WithEnvironment("EventStore__DomainServices__Registrations__*|counter|v1__AppId", string.Empty)
                .WithEnvironment("EventStore__DomainServices__Registrations__*|greeting|v1__AppId", string.Empty)
                .WithEnvironment("EventStore__DomainServices__Registrations__system|tenants|v1__AppId", string.Empty)
                .WithEnvironment("EventStore__DomainServices__Registrations__system|global-administrators|v1__AppId", string.Empty);
            _ = gateway.WithEnvironment("Authentication__WorkloadIssuer__Workload", owned.AppId("eventstore"));
            IResourceBuilder<ProjectResource> domainService = builder.CreateResourceBuilder<ProjectResource>("memories-domain-service");
            _ = ConfigureOwnedAppChannel(domainService, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
                .WithEnvironment("DAPR_API_TOKEN", apiToken);
            domainService.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations
                .Add(new EnvironmentCallbackAnnotation(context => context.EnvironmentVariables["DAPR_API_TOKEN"] = apiToken));
            foreach (IResourceBuilder<ProjectResource> resource in new[] { memories, gateway, domainService })
            {
                _ = resource.WithEnvironment("Authentication__WorkloadJwtBearer__Authority", string.Empty)
                    .WithEnvironment("Authentication__WorkloadJwtBearer__Issuer", PartiesAspireTopologyFixture.JwtIssuer)
                    .WithEnvironment("Authentication__WorkloadJwtBearer__SigningKey", PartiesAspireTopologyFixture.JwtSigningKey)
                    .WithEnvironment("Authentication__WorkloadJwtBearer__AllowedAlgorithms__0", "HS256")
                    .WithEnvironment("Authentication__WorkloadJwtBearer__Audience", owned.AppId(resource == domainService ? "memories-domain-service" : "eventstore"));
            }
            string acl = Path.Combine(owned.ConfigurationDirectory, "accesscontrol.yaml");
            await File.AppendAllTextAsync(acl, "\n      - appId: " + owned.AppId("memories")
                + "\n        namespace: " + owned.Namespace + "\n        trustDomain: public\n        defaultAction: deny\n        operations:\n          - name: /api/v1/commands/workload\n            httpVerb: ['POST']\n            action: allow\n          - name: /api/v1/commands/bound/workload\n            httpVerb: ['POST']\n            action: allow\n          - name: /api/v1/streams/read/workload\n            httpVerb: ['POST']\n            action: allow\n", cancellation).ConfigureAwait(false);
            foreach (ProjectResource unrelated in builder.Resources.OfType<ProjectResource>().Where(resource => resource.Name is not ("eventstore" or "memories" or "memories-domain-service")).ToArray())
            {
                _ = builder.CreateResourceBuilder<ProjectResource>(unrelated.Name).WithExplicitStart();
                // Omit app-less sidecars from this isolated component graph. Applying
                // explicit-start to a nested sidecar blocks the toolkit startup hook.
                foreach (DaprSidecarAnnotation unused in unrelated.Annotations.OfType<DaprSidecarAnnotation>().ToArray())
                {
                    unrelated.Annotations.Remove(unused);
                    builder.Resources.Remove(unused.Sidecar);
                }
            }
            return builder;
        }, (application, cancellation) => application.StartAsync(cancellation), prepareCiBootstrap: true, session: session, memoriesReadinessScope: true, sidecarApiTokens: new Dictionary<string, string> { ["memories"] = apiToken, ["memories-domain-service"] = apiToken }, workerOwnershipMarker: interruption?.WorkerMarker);
        fixture.ObserveOwnershipProgressAsync = interruption is null ? null : () => interruption.PublishCustodyAsync(fixture, privateRoot);
        fixture.ObserveInitializationFailureAsync = exception => CaptureTargetFailureAsync(fixture, privateRoot, "target-initialization-before-cleanup", exception);
        Exception? primaryFailure = null;
        bool controlledCancellationObserved = false;
        string phase = "target-initialization";
        try
        {
            await fixture.InitializeAsync(token).ConfigureAwait(true);
            fixture.IsAvailable.ShouldBeTrue(fixture.UnavailableReason);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            // Allow the full consumer matrix to finish its six-minute budget and return its teardown receipts.
            deadline.CancelAfter(TimeSpan.FromMinutes(producerRestart ? 27 : focused ? 5 : 7));
            await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync("memories", deadline.Token).ConfigureAwait(true);
            phase = "authenticated-workflow-and-erasure-control";
            ProjectResource memoriesResource = fixture.App.Services.GetRequiredService<DistributedApplicationModel>().Resources
                .OfType<ProjectResource>().Single(resource => resource.Name == "memories");
            string sidecarResource = memoriesResource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Name + "-cli";
            Uri sidecarHttp = fixture.App.GetEndpoint(sidecarResource, "http");
            Uri sidecarGrpc = fixture.App.GetEndpoint(sidecarResource, "grpc");
            fixture.App.ResourceNotifications.TryGetCurrentState("memories", out ResourceEvent? memoriesState).ShouldBeTrue();
            string appHttpPort = memoriesState!.Snapshot.EnvironmentVariables.Single(variable => variable.Name == "DAPR_HTTP_PORT").Value
                ?? throw new InvalidOperationException("The actual Memories Dapr HTTP listener is absent.");
            string appGrpcPort = memoriesState.Snapshot.EnvironmentVariables.Single(variable => variable.Name == "DAPR_GRPC_PORT").Value
                ?? throw new InvalidOperationException("The actual Memories Dapr gRPC listener is absent.");
            appHttpPort.ShouldBe(sidecarHttp.Port.ToString(CultureInfo.InvariantCulture));
            appGrpcPort.ShouldBe(sidecarGrpc.Port.ToString(CultureInfo.InvariantCulture));
            await File.WriteAllTextAsync(Path.Combine(privateRoot, "endpoint-binding.json"), JsonSerializer.Serialize(new
            { session.Id, SidecarHttp = sidecarHttp.ToString(), SidecarGrpc = sidecarGrpc.ToString(), AppHttpPort = appHttpPort, AppGrpcPort = appGrpcPort }), token).ConfigureAwait(true);
            await AssertCredentialNegativesAsync(sidecarHttp, apiToken, session.AppId("eventstore"), privateRoot, token).ConfigureAwait(true);
            await AssertDomainServiceAclDenialsAsync(fixture, session, apiToken, privateRoot, token).ConfigureAwait(true);
            string custody = Path.Combine(privateRoot, "source-authority-custody.json");
            Uri search = fixture.App.GetEndpoint("fixture-memories-search", "tcp");
            if (currentSourceAuthority)
            {
                memoriesState.Snapshot.EnvironmentVariables.Single(variable => variable.Name == "Ingestion__OwnershipGeneration").Value.ShouldBe(generation);
                memoriesState.Snapshot.EnvironmentVariables.Single(variable => variable.Name == "Ingestion__OwnershipWriterExclusionEvidenceId").Value.ShouldBe(exclusion);
                memoriesState.Snapshot.EnvironmentVariables.Single(variable => variable.Name == "ConnectionStrings__redis").Value.ShouldBe(search.Authority);
                List<object> resources = [];
                foreach (string name in new[] { "fixture-redis", "fixture-memories-search", "fixture-memories-graph" })
                {
                    fixture.App.ResourceNotifications.TryGetCurrentState(name, out ResourceEvent? state).ShouldBeTrue();
                    state.ShouldNotBeNull().Snapshot.State.ShouldNotBeNull().Text.ShouldBe("Running");
                    resources.Add(new { Name = name, State = state.Snapshot.State!.Text, Endpoint = fixture.App.GetEndpoint(name, "tcp").ToString() });
                }
                await File.WriteAllTextAsync(custody, JsonSerializer.Serialize(new
                { Session = session.Id, Generation = generation, WriterExclusionEvidenceId = exclusion,
                    Scope = "new private nonpersistent fixture resources only; no production writer exclusion", Resources = resources }), token).ConfigureAwait(true);
            }
            string configPath = Path.Combine(privateRoot, "runtime.json");
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
            {
                endpoint = fixture.App.GetEndpoint("memories", "http").ToString(),
                daprHttpEndpoint = sidecarHttp.ToString(),
                daprGrpcEndpoint = sidecarGrpc.ToString(),
                daprApiToken = apiToken, appApiToken = appToken, jwtIssuer = PartiesAspireTopologyFixture.JwtIssuer,
                jwtAudience = "hexalith-memories", jwtSigningKey = PartiesAspireTopologyFixture.JwtSigningKey,
                tenantClaimName = "tenant", stateStoreName = "statestore", fakeEmbeddingEnabled = true, ownedSourceInventoryQualificationEnabled = true,
                ownershipGeneration = currentSourceAuthority ? generation : null,
                ownershipWriterExclusionEvidenceId = currentSourceAuthority ? exclusion : null,
                ownershipSessionId = currentSourceAuthority ? session.Id : null,
                ownershipCustodyPath = currentSourceAuthority ? custody : null,
                ownershipRedisEndpoint = currentSourceAuthority ? search.Authority : null,
                producerRestartDirectory = producerRestart ? restartDirectory : null,
                producerRestartSecret = producerRestart ? restartSecret : null,
                ownershipServerAssemblyPath = producerRestart ? PartyMemoryProducerRestartRuntime.ServerAssemblyPath(memoriesState) : null,
            }), token).ConfigureAwait(true);
            string root = ResolveRoot();
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root };
            string provenance = Path.Combine(privateRoot, "executed-identities.json");
            Dictionary<string, string> identities = MemoryRuntimeIdentity.Files(typeof(PartyMemoryOwnedRuntimeTests).Assembly, child);
            if (producerRestart)
            {
                string ownerAssembly = PartyMemoryProducerRestartRuntime.ServerAssemblyPath(memoriesState);
                identities.Add(ownerAssembly, MemoryRuntimeIdentity.Hash(ownerAssembly));
                if (interruption is not null)
                {
                    foreach (string file in new[] { Path.ChangeExtension(typeof(PartyMemoryOwnedRuntimeTests).Assembly.Location, ".deps.json"),
                        Path.ChangeExtension(typeof(PartyMemoryOwnedRuntimeTests).Assembly.Location, ".runtimeconfig.json"),
                        Environment.GetEnvironmentVariable("Parties__Testing__PreparedRuntimeManifest")
                            ?? throw new InvalidOperationException("Runner interruption requires an exact prepared manifest.") })
                    { identities.Add(file, MemoryRuntimeIdentity.Hash(file)); }
                }
            }
            await File.WriteAllTextAsync(provenance, JsonSerializer.Serialize(identities), token).ConfigureAwait(true);
            if (interruption is not null)
            {
                await interruption.PublishCustodyAsync(fixture, privateRoot).ConfigureAwait(true);
            }
            foreach (string argument in new[] { child,
                "-method", producerRestart ? "*SelectedMemoriesRecoverAcrossProducerWorkloadRestarts" : focused ? "*SelectedMemoriesRefreshAndCleanupPersistAcrossClientRecreation" : "*SelectedMemoriesIngestionAndCleanupPersistAcrossRestart",
                "-failSkips", "-parallelMode", "none", "-result-xml", Path.Combine(privateRoot, "runtime.xml") }) { start.ArgumentList.Add(argument); }
            start.Environment["PARTIES_MEMORIES_RUNTIME_CONFIG"] = configPath;
            start.Environment["PARTIES_MEMORIES_RUNTIME_PROVENANCE"] = provenance;
            if (interruption is not null) { start.Environment[PartyMemoryProducerRestartProtocol.ObservationOnlyVariable] = "1"; }
            start.Environment[PartiesAspireTopologyFixture.WorkerOwnershipVariable] = fixture.WorkerOwnershipMarker;
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("The owned runtime qualification process did not start.");
            fixture.CaptureOwnedWorker(process.Id);
            using var outputCancellation = new CancellationTokenSource();
            Task<string> output = process.StandardOutput.ReadToEndAsync(outputCancellation.Token), error = process.StandardError.ReadToEndAsync(outputCancellation.Token);
            using var coordination = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var restarts = new PartyMemoryProducerRestartRuntime(fixture, search.Authority, sidecarHttp, apiToken, generation, exclusion, privateRoot)
            {
                PendingRunnerInterruptionCheckpoint = interruption?.Phase == "admitted-empty-mappings"
                    ? (observation, cancellation) => interruption.PublishAsync(fixture, privateRoot, observation, token: cancellation) : null,
                ReplacementObserved = cancelRestart ? observed =>
                {
                    if (observed == "admitted-empty-mappings") { controlledCancellationObserved = true; coordination.Cancel(); }
                } : null,
            };
            Task server = producerRestart ? PartyMemoryProducerRestartProtocol.ServeAsync(restartDirectory, session.Id, restartSecret,
                async (checkpoint, evidence, cancellation) =>
                {
                    if (interruption is null) { await restarts.RestartAsync(checkpoint, evidence, cancellation).ConfigureAwait(false); }
                    else if (checkpoint == interruption.Phase)
                    {
                        if (checkpoint == "admitted-empty-mappings") { await restarts.RestartAsync(checkpoint, evidence, cancellation).ConfigureAwait(false); }
                        else
                        {
                            await restarts.ObserveInterruptionAsync(checkpoint, evidence,
                                (observation, phaseToken) => interruption.PublishAsync(fixture, privateRoot, observation, token: phaseToken), cancellation).ConfigureAwait(false);
                        }
                    }
                    // Earlier authenticated workload checkpoints advance without replacements;
                    // the target remains frozen until actual runner SIGINT cancels execution.
                }, coordination.Token, observationOnly: interruption is not null) : Task.CompletedTask;
            Exception? childFailure = null;
            try
            {
                Task exited = process.WaitForExitAsync(deadline.Token);
                if (producerRestart)
                {
                    Task completed = await Task.WhenAny(server, exited).ConfigureAwait(true);
                    if (completed == server) { await server.ConfigureAwait(true); }
                }
                await exited.ConfigureAwait(true);
                if (producerRestart && process.ExitCode == 0) { await server.WaitAsync(TimeSpan.FromSeconds(1), deadline.Token).ConfigureAwait(true); restarts.Count.ShouldBe(6); }
                process.ExitCode.ShouldBe(0, "Exact private runtime evidence: " + privateRoot);
            }
            catch (Exception failure) { childFailure = failure; throw; }
            finally
            {
                await CompleteChildRunAsync(
                    () => { if (!process.HasExited) { process.Kill(entireProcessTree: true); } return Task.CompletedTask; },
                    async () =>
                    {
                        using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await process.WaitForExitAsync(reap.Token).ConfigureAwait(true);
                    },
                    () => coordination.CancelAsync(),
                    async () =>
                    {
                        try { await server.ConfigureAwait(true); }
                        catch (OperationCanceledException) when (coordination.IsCancellationRequested) { }
                    },
                    () => producerRestart ? restarts.QuiesceIncompleteAsync() : Task.CompletedTask,
                    async () =>
                    {
                        string stdout = await PartyMemoryRunnerInterruptionProcess.DrainAsync(process, output, error, outputCancellation, TimeSpan.FromSeconds(3)).ConfigureAwait(true);
                        await File.WriteAllTextAsync(Path.Combine(privateRoot, "runtime.log"), stdout + await error.ConfigureAwait(true), CancellationToken.None).ConfigureAwait(true);
                    }, childFailure).ConfigureAwait(true);
            }
            var executed = System.Xml.Linq.XDocument.Load(Path.Combine(privateRoot, "runtime.xml")).Descendants("assembly").ShouldHaveSingleItem();
            executed.Attribute("total")!.Value.ShouldBe("1");
            executed.Attribute("passed")!.Value.ShouldBe("1");
            executed.Attribute("skipped")!.Value.ShouldBe("0");
            phase = "authoritative-stream-and-domain-replay";
            await MemoriesAuthoritativePersistence.AssertAsync(fixture, sidecarHttp, apiToken, proofPrivateKey, privateRoot, deadline.Token).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            if (!cancelRestart || !controlledCancellationObserved || exception is not OperationCanceledException) { throw; }
        }
        finally
        {
            await CompleteOwnedRunAsync(
                () => CaptureTargetFailureAsync(fixture, privateRoot, phase, primaryFailure),
                () => fixture.DisposeAsync().AsTask(),
                (disposed, cleanupFailure) => File.WriteAllTextAsync(Path.Combine(privateRoot, "ownership.json"), JsonSerializer.Serialize(new
                { session.Id, session.Root, PrivateEvidence = privateRoot, OwnedTopologyDisposed = disposed, SessionDirectoryExists = Directory.Exists(session.Root),
                    fixture.IsAvailable, fixture.UnavailableReason, Phase = phase, ControlledCancellation = controlledCancellationObserved,
                    PrimaryFailure = primaryFailure?.ToString(), CleanupFailure = cleanupFailure?.ToString() }), CancellationToken.None),
                primaryFailure).ConfigureAwait(true);
        }
        if (cancelRestart) { controlledCancellationObserved.ShouldBeTrue("The actual admitted replacement must be observed before controlled cancellation."); }
    }

    /// <summary>Defers private package-profile custody until full composition has reused the generated SDK binding.</summary>
    internal static IResourceBuilder<ProjectResource> ConfigureOwnedAppChannel(IResourceBuilder<ProjectResource> resource, string token)
    {
        IResourceBuilder<ParameterResource> parameter = resource.ApplicationBuilder.AddParameter("fixture-" + resource.Resource.Name + "-channel-token", token, secret: true);
        if (!resource.ApplicationBuilder.Configuration.GetValue<bool>("Parties:Testing:UsePackagedGateway"))
        {
            _ = resource.WithEventStoreAppChannelToken(parameter);
        }
        return resource;
    }

    /// <summary>Settles every child/coordination cleanup step even when termination or reaping fails.</summary>
    internal static async Task CompleteChildRunAsync(Func<Task> terminate, Func<Task> reap, Func<Task> cancel,
        Func<Task> settle, Func<Task> quiesce, Func<Task> persist, Exception? primaryFailure)
    {
        List<Exception> failures = [];
        foreach (Func<Task> operation in new[] { terminate, reap, cancel, settle, quiesce, persist })
        {
            try { await operation().ConfigureAwait(true); }
            catch (Exception failure) { failures.Add(failure); }
        }
        if (failures.Count != 0)
        {
            if (primaryFailure is not null) { failures.Insert(0, primaryFailure); }
            throw new AggregateException("Owned child qualification or cleanup failed.", failures);
        }
    }

    /// <summary>Always disposes owned resources, retaining primary, capture, disposal and receipt failures.</summary>
    internal static async Task CompleteOwnedRunAsync(Func<Task> capture, Func<Task> dispose,
        Func<bool, Exception?, Task> receipt, Exception? primaryFailure)
    {
        List<Exception> failures = [];
        bool disposed = false;
        try { await capture().ConfigureAwait(true); }
        catch (Exception exception) { failures.Add(exception); }
        finally
        {
            try { await dispose().ConfigureAwait(true); disposed = true; }
            catch (Exception exception) { failures.Add(exception); }
            finally
            {
                try { await receipt(disposed, failures.Count == 0 ? null : new AggregateException(failures)).ConfigureAwait(true); }
                catch (Exception exception) { failures.Add(exception); }
            }
        }
        if (failures.Count != 0)
        {
            if (primaryFailure is not null) { failures.Insert(0, primaryFailure); }
            throw new AggregateException("Owned qualification evidence or cleanup failed.", failures);
        }
    }

    private static async Task AssertDomainServiceAclDenialsAsync(PartiesAspireTopologyFixture fixture, PartiesTopologySession session,
        string apiToken, string privateRoot, CancellationToken token)
    {
        List<object> observations = [];
        foreach ((string caller, HttpMethod verb, string path) in new[]
        {
            ("memories", HttpMethod.Post, "process"),
            ("eventstore", HttpMethod.Get, "process"),
            ("eventstore", HttpMethod.Post, "health"),
        })
        {
            using HttpClient client = fixture.CreateSidecarClient(caller);
            // The fixture resolves the actual modeled credential, including this profile's override.
            using var request = new HttpRequestMessage(verb, "/v1.0/invoke/" + session.AppId("memories-domain-service") + "/method/" + path);
            using HttpResponseMessage denied = await client.SendAsync(request, token).ConfigureAwait(false);
            string body = await denied.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            using JsonDocument evidence = JsonDocument.Parse(body);
            evidence.RootElement.GetProperty("errorCode").GetString().ShouldBe("ERR_DIRECT_INVOKE");
            string diagnostic = evidence.RootElement.GetProperty("message").GetString()!;
            diagnostic.ShouldContain("PermissionDenied");
            diagnostic.ShouldContain("access control policy has denied access");
            observations.Add(new { Caller = caller, Verb = verb.Method, Path = path, Status = (int)denied.StatusCode, Diagnostic = diagnostic });
        }
        await File.WriteAllTextAsync(Path.Combine(privateRoot, "domain-service-acl-negative-controls.json"), JsonSerializer.Serialize(observations), token).ConfigureAwait(false);
    }

    private static async Task AssertCredentialNegativesAsync(Uri sidecar, string apiToken, string gateway, string privateRoot, CancellationToken token)
    {
        var observations = new List<object>();
        using var client = new HttpClient { BaseAddress = sidecar, Timeout = TimeSpan.FromSeconds(5), MaxResponseContentBufferSize = 8192 };
        foreach (string? credential in new string?[] { null, "incorrect-owned-token" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1.0/metadata");
            if (credential is not null) { request.Headers.Add("dapr-api-token", credential); }
            using HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false);
            observations.Add(new { Control = credential is null ? "missing-sidecar-token" : "incorrect-sidecar-token", Status = (int)response.StatusCode });
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        using var unsigned = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands/bound/workload")
        { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        unsigned.Headers.Add("dapr-app-id", gateway);
        unsigned.Headers.Add("dapr-api-token", apiToken);
        using HttpResponseMessage denied = await client.SendAsync(unsigned, token).ConfigureAwait(false);
        string denialBody = await denied.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        observations.Add(new { Control = "missing-workload-assertion-and-principal-proof", Status = (int)denied.StatusCode, Body = denialBody });
        await File.WriteAllTextAsync(Path.Combine(privateRoot, "authentication-negative-controls.json"), JsonSerializer.Serialize(observations), token).ConfigureAwait(false);
        denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task CaptureTargetFailureAsync(PartiesAspireTopologyFixture fixture, string privateRoot, string phase, Exception? failure)
    {
        var targets = new List<object>();
        foreach (string name in new[] { "eventstore", "memories", "memories-domain-service" })
        {
            string? state = null;
            var endpoints = new List<object>();
            try
            {
                if (fixture.App.ResourceNotifications.TryGetCurrentState(name, out ResourceEvent? resource))
                {
                    state = resource.Snapshot.State?.Text;
                }
                ProjectResource target = fixture.App.Services.GetRequiredService<DistributedApplicationModel>().Resources
                    .OfType<ProjectResource>().Single(resource => resource.Name == name);
                AllocatedEndpoint endpoint = target.Annotations.OfType<EndpointAnnotation>().Single(annotation => annotation.Name == "http").AllocatedEndpoint
                    ?? throw new InvalidOperationException("The target HTTP endpoint has not been allocated.");
                using var client = new HttpClient { BaseAddress = new Uri(endpoint.UriString) };
                foreach (string path in new[] { "/alive", "/ready" })
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    try
                    {
                        using HttpResponseMessage response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                        using Stream body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                        byte[] bytes = new byte[4096];
                        int count = await body.ReadAsync(bytes, timeout.Token).ConfigureAwait(false);
                        endpoints.Add(new { Path = path, Status = (int)response.StatusCode, Body = System.Text.Encoding.UTF8.GetString(bytes, 0, count) });
                    }
                    catch (Exception exception) { endpoints.Add(new { Path = path, Failure = exception.ToString() }); }
                }
            }
            catch (Exception exception) { endpoints.Add(new { Failure = exception.ToString() }); }
            targets.Add(new { Name = name, State = state, Endpoints = endpoints });
        }
        await File.WriteAllTextAsync(Path.Combine(privateRoot, phase == "target-initialization-before-cleanup" ? "startup-failure.json" : "phase.json"), JsonSerializer.Serialize(new
        {
            Phase = phase, fixture.InitializationPhase, fixture.IsAvailable, fixture.UnavailableReason, PrimaryFailure = failure?.ToString(), Targets = targets,
        }), CancellationToken.None).ConfigureAwait(false);
    }

    private static string ResolveRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.Parties.slnx"))) { directory = directory.Parent; }
        return directory?.FullName ?? throw new InvalidOperationException("Parties workspace not found.");
    }
}
