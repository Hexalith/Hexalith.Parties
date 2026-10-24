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
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Owns the actual authenticated selected-source Memories qualification topology; no in-memory command store.</summary>
[Collection("Non-parallel")]
public sealed class PartyMemoryOwnedRuntimeTests
{
    [Fact(Timeout = 600000)]
    public async Task ActualSelectedMemoriesRunsTheRecoveryAndPhysicalErasureControl()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PARTIES_MEMORIES_OWNED_RUNTIME") == "1",
            "Set PARTIES_MEMORIES_OWNED_RUNTIME=1 to start the owned authenticated source topology.");
        CancellationToken token = TestContext.Current.CancellationToken;
        PartiesTopologySession session = PartiesTopologySession.Create(isolatedIdentities: true);
        string appToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string apiToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using RSA proofKey = RSA.Create(2048);
        string proofPrivateKey = Convert.ToBase64String(proofKey.ExportPkcs8PrivateKey());
        string proofPublicKey = Convert.ToBase64String(proofKey.ExportSubjectPublicKeyInfo());
        string privateRoot = Path.Combine(Path.GetTempPath(), "parties-five-memory-runtime-" + session.Id);
        Directory.CreateDirectory(privateRoot);
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
                .WithEnvironment("DAPR_API_TOKEN", apiToken)
                .WithEventStoreAppChannelToken(builder.AddParameter("owned-memories-app-token", appToken, secret: true));
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
                _ = target.WithEventStoreAppChannelToken(builder.AddParameter("owned-" + targetName + "-app-token",
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), secret: true));
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
            _ = gateway.WithEventStoreAppChannelToken(builder.AddParameter("owned-gateway-app-token",
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), secret: true))
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
            _ = domainService.WithEventStoreAppChannelToken(builder.AddParameter("owned-domain-service-app-token",
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), secret: true))
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
        }, (application, cancellation) => application.StartAsync(cancellation), prepareCiBootstrap: true, session: session, memoriesReadinessScope: true, sidecarApiTokens: new Dictionary<string, string> { ["memories"] = apiToken, ["memories-domain-service"] = apiToken });
        fixture.ObserveInitializationFailureAsync = exception => CaptureTargetFailureAsync(fixture, privateRoot, "target-initialization-before-cleanup", exception);
        Exception? primaryFailure = null;
        string phase = "target-initialization";
        try
        {
            await fixture.InitializeAsync(token).ConfigureAwait(true);
            fixture.IsAvailable.ShouldBeTrue(fixture.UnavailableReason);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromMinutes(5));
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
            string configPath = Path.Combine(privateRoot, "runtime.json");
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
            {
                endpoint = fixture.App.GetEndpoint("memories", "http").ToString(),
                daprHttpEndpoint = sidecarHttp.ToString(),
                daprGrpcEndpoint = sidecarGrpc.ToString(),
                daprApiToken = apiToken, appApiToken = appToken, jwtIssuer = PartiesAspireTopologyFixture.JwtIssuer,
                jwtAudience = "hexalith-memories", jwtSigningKey = PartiesAspireTopologyFixture.JwtSigningKey,
                tenantClaimName = "tenant", stateStoreName = "statestore", fakeEmbeddingEnabled = true, ownedSourceInventoryQualificationEnabled = true,
            }), token).ConfigureAwait(true);
            string root = ResolveRoot();
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root };
            foreach (string argument in new[] { Path.Combine(root, "tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll"),
                "-method", "*SelectedMemoriesIngestionAndCleanupPersistAcrossRestart", "-result-xml", Path.Combine(privateRoot, "runtime.xml") }) { start.ArgumentList.Add(argument); }
            start.Environment["PARTIES_MEMORIES_RUNTIME_CONFIG"] = configPath;
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("The owned runtime qualification process did not start.");
            Task<string> output = process.StandardOutput.ReadToEndAsync(token), error = process.StandardError.ReadToEndAsync(token);
            try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(true); }
            finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
            await File.WriteAllTextAsync(Path.Combine(privateRoot, "runtime.log"), (await output.ConfigureAwait(true)) + (await error.ConfigureAwait(true)), token).ConfigureAwait(true);
            process.ExitCode.ShouldBe(0, "Exact private runtime evidence: " + privateRoot);
            phase = "authoritative-stream-and-domain-replay";
            await MemoriesAuthoritativePersistence.AssertAsync(fixture, sidecarHttp, apiToken, proofPrivateKey, privateRoot, deadline.Token).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            throw;
        }
        finally
        {
            await CaptureTargetFailureAsync(fixture, privateRoot, phase, primaryFailure).ConfigureAwait(true);
            bool disposed = false;
            Exception? cleanupFailure = null;
            try { await fixture.DisposeAsync().ConfigureAwait(true); disposed = true; }
            catch (Exception exception)
            {
                cleanupFailure = exception;
                if (primaryFailure is not null)
                {
                    throw new AggregateException("Owned Memories qualification and cleanup failed; exact private evidence: " + privateRoot,
                        primaryFailure, exception);
                }
                throw;
            }
            finally
            {
                await File.WriteAllTextAsync(Path.Combine(privateRoot, "ownership.json"), JsonSerializer.Serialize(new
                { session.Id, session.Root, PrivateEvidence = privateRoot, OwnedTopologyDisposed = disposed, SessionDirectoryExists = Directory.Exists(session.Root),
                    fixture.IsAvailable, fixture.UnavailableReason, Phase = phase, PrimaryFailure = primaryFailure?.ToString(), CleanupFailure = cleanupFailure?.ToString() }), CancellationToken.None).ConfigureAwait(true);
            }
        }
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
