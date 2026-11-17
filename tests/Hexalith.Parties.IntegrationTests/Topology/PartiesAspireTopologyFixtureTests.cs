extern alias apphost;

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Hexalith.Parties.TestInfrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Verifies infrastructure failures remain unavailable with their actual diagnostic.</summary>
[Collection("Non-parallel")]
public sealed class PartiesAspireTopologyFixtureTests
{
    /// <summary>Cleanup permanently closes admission, retaining the fact of any already admitted startup.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupAdmissionAndCleanupPreserveBothOrderings(bool startupFirst)
    {
        var fixture = new PartiesAspireTopologyFixture();
        await using var lifetime = fixture.ConfigureAwait(true);
        if (startupFirst) { fixture.AdmitStartup(); }
        await fixture.DisposeAsync().ConfigureAwait(true);
        Should.Throw<InvalidOperationException>(() => fixture.AdmitStartup()).Message.ShouldContain("no late startup was admitted");
        ((bool)typeof(PartiesAspireTopologyFixture).GetField("_startupScheduled", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!).ShouldBe(startupFirst);
    }

    /// <summary>Ownership precedes model creation and a failing failure-observer cannot abandon partial startup disposal.</summary>
    [Fact]
    public async Task OwnershipProgressPrecedesCreationAndFailureObservationCannotSkipDisposal()
    {
        var phases = new List<string>();
        var fixture = new PartiesAspireTopologyFixture(
            (session, cancellation) =>
            {
                phases.ShouldContain("not-started");
                return DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                    [.. session.Arguments(false, false), "--Parties:Testing:UsePackagedGateway=false"], cancellation);
            }, (_, _) => throw new InvalidOperationException("owned startup failed"));
        fixture.ObserveOwnershipProgressAsync = () => { phases.Add(fixture.InitializationPhase); return Task.CompletedTask; };
        fixture.ObserveInitializationFailureAsync = _ => throw new IOException("failure custody writer failed");
        try
        {
            await fixture.InitializeAsync().ConfigureAwait(true);
            fixture.IsAvailable.ShouldBeFalse();
            fixture.UnavailableReason.ShouldNotBeNull().ShouldContain("owned startup failed");
            fixture.UnavailableReason.ShouldContain("failure custody writer failed");
            fixture.CurrentApplication.ShouldBeNull("Failed observation must not skip disposal of the built application.");
            phases.ShouldContain("start-application");
        }
        finally { await fixture.DisposeAsync().ConfigureAwait(true); }
    }

    /// <summary>Sidecar probes resolve actual secret parameters and respect a later fixture credential override.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SidecarCredentialResolvesActualParameterAndFinalOverride(bool overridden)
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
            ["--operation=run", "--EnableKeycloak=false", "--Parties:Testing:UsePackagedGateway=false"], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var lifetime = builder.ConfigureAwait(true);
        string secret = Guid.NewGuid().ToString("N");
        var parameter = builder.AddParameter("probe-token", secret, secret: true);
        var sidecar = builder.AddExecutable("credential-probe", "noop", ".").WithEnvironment("DAPR_API_TOKEN", parameter);
        if (overridden) { sidecar.WithEnvironment("DAPR_API_TOKEN", "actual-fixture-override"); }
        string? resolved = await PartiesAspireTopologyFixture.ResolveModeledSidecarApiTokenAsync(builder.ExecutionContext,
            sidecar.Resource, TestContext.Current.CancellationToken).ConfigureAwait(true);
        resolved.ShouldBe(overridden ? "actual-fixture-override" : secret);
        var absent = builder.AddExecutable("credential-absent", "noop", ".");
        (await PartiesAspireTopologyFixture.ResolveModeledSidecarApiTokenAsync(builder.ExecutionContext,
            absent.Resource, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
        absent.Resource.Annotations.Add(new EnvironmentCallbackAnnotation(context => context.EnvironmentVariables["DAPR_API_TOKEN"] = new object()));
        await Should.ThrowAsync<InvalidOperationException>(() => PartiesAspireTopologyFixture.ResolveModeledSidecarApiTokenAsync(builder.ExecutionContext,
            absent.Resource, TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    [Theory]
    [InlineData("""{"actors":[{"type":"AggregateActor","count":0}]}""", true)]
    [InlineData("""{"actorRuntime":{"hostReady":true,"runtimeStatus":"RUNNING"},"actors":[]}""", false)]
    [InlineData("""{"actors":[{"type":"ProjectionActor","count":1}]}""", false)]
    [InlineData("""{}""", false)]
    public void GatewayReadiness_RequiresRegisteredAggregateActorWithoutRequiringActivation(string value, bool ready)
    {
        using JsonDocument metadata = JsonDocument.Parse(value);
        if (ready)
        {
            PartiesAspireTopologyFixture.RequireGatewayActorRegistration(metadata.RootElement);
        }
        else
        {
            Should.Throw<InvalidOperationException>(() => PartiesAspireTopologyFixture.RequireGatewayActorRegistration(metadata.RootElement))
                .Message.ShouldContain("AggregateActor");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializationFailure_ReportsActualReasonAndRestoresEnvironmentOnDisposal(bool failsDuringStartup)
    {
        string? previousEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        int startAttempts = 0;
        IServiceCollection? modeledServices = null;
        var fixture = new PartiesAspireTopologyFixture(
            async (session, cancellationToken) =>
            {
                if (!failsDuringStartup) { throw new FileNotFoundException("missing test infrastructure"); }
                IDistributedApplicationTestingBuilder builder = await CreateConfigurationBuilderAsync(session, cancellationToken).ConfigureAwait(false);
                modeledServices = builder.Services;
                return builder;
            },
            (_, _) =>
            {
                startAttempts++;
                using ServiceProvider services = modeledServices!.BuildServiceProvider();
                services.GetRequiredService<IHostLifetime>().ShouldBeOfType<PartiesTopologyLifetime>();
                throw new InvalidOperationException("test topology startup failed");
            });
        fixture.ObserveInitializationFailureAsync = failure =>
        {
            TestContext.Current.TestOutputHelper?.WriteLine(failure.ToString());
            return Task.CompletedTask;
        };
        try
        {
            await fixture.InitializeAsync().ConfigureAwait(true);
            fixture.IsAvailable.ShouldBeFalse();
            fixture.UnavailableReason.ShouldNotBeNull();
            fixture.UnavailableReason.ShouldContain(failsDuringStartup
                ? "InvalidOperationException: test topology startup failed"
                : "FileNotFoundException: missing test infrastructure");
            startAttempts.ShouldBe(failsDuringStartup ? 1 : 0);
            ((bool)typeof(PartiesAspireTopologyFixture).GetField("_startupScheduled", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!)
                .ShouldBeFalse("Configuration inspection must not initiate the testing factory's SDK startup.");
            fixture.CurrentApplication.ShouldBeNull("Controlled initialization failure must complete unstarted-model disposal.");
            Should.Throw<InvalidOperationException>(() => fixture.EventStoreClient).Message
                .ShouldContain(fixture.UnavailableReason!);
        }
        finally
        {
            await fixture.DisposeAsync().ConfigureAwait(true);
        }

        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT").ShouldBe(previousEnvironment);
    }

    /// <summary>Builds the configuration fixture's model without a background AppHost Run call.</summary>
    private static Task<IDistributedApplicationTestingBuilder> CreateConfigurationBuilderAsync(PartiesTopologySession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var model = new PartiesConfigurationTestingBuilder(new DistributedApplicationOptions
        {
            Args = [.. session.Arguments(false, false), "--Parties:Testing:UsePackagedGateway=false"],
            DisableDashboard = true,
        });
        model.AddDapr();
        IResourceBuilder<IDaprComponentResource> state = model.AddDaprComponent("statestore", "state.redis");
        IResourceBuilder<IDaprComponentResource> pubsub = model.AddDaprComponent("pubsub", "pubsub.redis");
        foreach (string name in new[] { "eventstore", "eventstore-admin", "parties", "parties-mcp", "tenants" })
        {
            _ = model.AddResource(new ProjectResource(name)).WithHttpEndpoint()
                .WithDaprSidecar(sidecar => sidecar.WithReference(state).WithReference(pubsub));
        }
        return Task.FromResult<IDistributedApplicationTestingBuilder>(model);
    }

    /// <summary>The fixture wraps only an ordinary CLI in its subreaper launcher, once, and removes it on disposal.</summary>
    /// <param name="configured">The configured Dapr path's file name; "dapr" is an ordinary CLI.</param>
    [Theory]
    [InlineData("dapr")]
    [InlineData("dapr-session")]
    [InlineData(PartiesDaprCliSubreaper.LauncherName)]
    public async Task ConfiguredDaprPath_WrapsOnlyAnOrdinaryCliInTheFixtureSubreaper(string configured)
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Linux subreaper installation."); }
        string configuredPath = Path.Combine(Path.GetTempPath(), $"parties-dapr-path-{Guid.NewGuid():N}", configured);
        string? effective = null;
        string? launcherBody = null;
        IServiceCollection? modeledServices = null;
        var fixture = new PartiesAspireTopologyFixture(
            async (session, cancellationToken) =>
            {
                IDistributedApplicationTestingBuilder builder = await CreateConfigurationBuilderAsync(session, cancellationToken).ConfigureAwait(false);
                builder.Services.Configure<DaprOptions>(options => options.DaprPath = configuredPath);
                modeledServices = builder.Services;
                return builder;
            },
            (_, _) =>
            {
                // Inspect the actual configured registrations. The directly built model
                // has no background AppHost entrypoint that can independently call Run.
                using ServiceProvider services = modeledServices!.BuildServiceProvider();
                effective = services.GetRequiredService<IOptions<DaprOptions>>().Value.DaprPath;
                launcherBody = effective is not null && File.Exists(effective) ? File.ReadAllText(effective) : null;
                throw new InvalidOperationException("controlled startup failure after reading the Dapr options");
            });
        fixture.ObserveInitializationFailureAsync = failure =>
        {
            TestContext.Current.TestOutputHelper?.WriteLine(failure.ToString());
            return Task.CompletedTask;
        };
        var trace = new List<string>();
        using var traceCancellation = new CancellationTokenSource();
        Task traceTask = Environment.GetEnvironmentVariable("PARTIES_FIXTURE_DISPOSAL_DIAGNOSTICS") == "1" ? TraceAsync() : Task.CompletedTask;
        async Task TraceAsync()
        {
            var elapsed = Stopwatch.StartNew();
            string? previous = null;
            try
            {
                while (true)
                {
                    DistributedApplication? app = fixture.CurrentApplication;
                    // Record state without resolving or constructing additional services.
                    bool Flag(string name) => (bool)typeof(PartiesAspireTopologyFixture).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!;
                    string state = $"quiesced={Flag("_workersQuiesced")}; startupScheduled={Flag("_startupScheduled")}; appPresent={app is not null}";
                    if (state != previous) { trace.Add($"{elapsed.Elapsed.TotalSeconds:F3}s {state}"); previous = state; }
                    await Task.Delay(10, traceCancellation.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (traceCancellation.IsCancellationRequested) { }
        }
        try
        {
            await fixture.InitializeAsync().ConfigureAwait(true);
            fixture.UnavailableReason.ShouldNotBeNull().ShouldContain("controlled startup failure after reading the Dapr options");
            ((bool)typeof(PartiesAspireTopologyFixture).GetField("_startupScheduled", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!)
                .ShouldBeFalse("Configuration inspection must not initiate the testing factory's SDK startup.");
            fixture.CurrentApplication.ShouldBeNull("The unstarted model must complete its actual fixture disposal.");
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("PARTIES_FIXTURE_DISPOSAL_DIAGNOSTICS") == "1")
            { TestContext.Current.TestOutputHelper?.WriteLine("Initial auto-cleanup receipt before explicit disposal: " + fixture.UnavailableReason); }
            try { await fixture.DisposeAsync().ConfigureAwait(true); }
            finally
            {
                await traceCancellation.CancelAsync().ConfigureAwait(true);
                await traceTask.ConfigureAwait(true);
                if (trace.Count != 0)
                {
                    TestContext.Current.TestOutputHelper?.WriteLine(string.Join("\n", trace));
                }
            }
        }

        if (configured == "dapr")
        {
            Path.GetFileName(effective).ShouldBe(PartiesDaprCliSubreaper.LauncherName);
            Path.GetDirectoryName(effective).ShouldNotBe(Path.GetDirectoryName(configuredPath));
            launcherBody.ShouldNotBeNull().Split('\n').ShouldContain("CLI = " + JsonSerializer.Serialize(configuredPath));
            Directory.Exists(Path.GetDirectoryName(effective)).ShouldBeFalse("The fixture removes its private launcher after disposal.");
        }
        else
        {
            // The session launcher keeps its PID continuity, and an existing launcher is never wrapped again.
            effective.ShouldBe(configuredPath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedInfrastructure_CleansUpThroughFixtureAndRetainsOwnershipForFailedCleanupRetry(bool cleanupFails)
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fixture's automatic mTLS bootstrap runs on Linux CI.");
        }

        using var commands = new MtlsBootstrapCommandDouble();
        commands.ApplyToCurrentProcess(new Dictionary<string, string?>
        {
            ["GITHUB_ACTIONS"] = "true",
            ["Dapr__Mtls__Enabled"] = null,
            ["Dapr__Mtls__CertificateDirectory"] = null,
        });
        string? certificateDirectory = null;
        var fixture = new PartiesAspireTopologyFixture(
            (session, _) =>
            {
                certificateDirectory = session.CertificateDirectory;
                commands.SetFailure(cleanupFails ? "unavailable" : null);
                throw new FileNotFoundException("model construction failed after scoped preparation");
            },
            (_, _) => throw new InvalidOperationException("Model construction must fail before startup."),
            prepareCiBootstrap: true);
        try
        {
            await fixture.InitializeAsync().ConfigureAwait(true);
            fixture.IsAvailable.ShouldBeFalse();
            fixture.UnavailableReason.ShouldNotBeNull();
            fixture.UnavailableReason.ShouldContain("model construction failed after scoped preparation");
            certificateDirectory.ShouldNotBeNull();
            commands.Requests.ShouldContain(request => request.StartsWith("docker run ", StringComparison.Ordinal));
            if (cleanupFails)
            {
                fixture.UnavailableReason.ShouldContain("topology cleanup failed");
                commands.OwnedRoles.Length.ShouldBe(3);
                Directory.Exists(certificateDirectory).ShouldBeTrue();
                File.Exists(Path.Combine(certificateDirectory, ".owned-control-plane-network")).ShouldBeTrue();
                File.Exists(Path.Combine(certificateDirectory, "issuer.key")).ShouldBeTrue();
                AggregateException failures = await Should.ThrowAsync<AggregateException>(() => fixture.DisposeAsync().AsTask()).ConfigureAwait(true);
                failures.InnerExceptions.Count.ShouldBe(2);
                failures.InnerExceptions[0].ShouldBeOfType<FileNotFoundException>().Message
                    .ShouldBe("model construction failed after scoped preparation");
                failures.InnerExceptions[1].ShouldBeOfType<InvalidOperationException>().Message.ShouldContain("Docker unavailable");
                Directory.Exists(certificateDirectory).ShouldBeTrue();
                commands.SetFailure(null);
            }

            await fixture.DisposeAsync().ConfigureAwait(true);
            Directory.Exists(certificateDirectory).ShouldBeFalse();
            commands.OwnedRoles.ShouldBeEmpty();
            commands.OwnedNetworkExists.ShouldBeFalse();
            commands.ForeignResourcesPreserved.ShouldBeTrue();
            int requestCount = commands.Requests.Length;
            await fixture.DisposeAsync().ConfigureAwait(true);
            commands.Requests.Length.ShouldBe(requestCount, "Successful cleanup must clear ownership and be idempotent.");
        }
        finally
        {
            commands.SetFailure(null);
            await fixture.DisposeAsync().ConfigureAwait(true);
            if (certificateDirectory is not null && Directory.Exists(certificateDirectory))
            {
                Directory.Delete(certificateDirectory, recursive: true);
            }
        }
    }
    [Fact]
    public async Task CallerCancellation_ReachesModelConstructionAndRestoresEveryProfileEnvironmentValue()
    {
        string[] names = ["EnableKeycloak", "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT",
            "Dapr__Mtls__Enabled", "Dapr__Mtls__CertificateDirectory", "EnableMemoriesSearch", "EnableEventStoreSampleUi"];
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        using var cancellation = new CancellationTokenSource();
        bool receivedCancellation = false;
        var fixture = new PartiesAspireTopologyFixture(
            async (session, token) =>
            {
                cancellation.Cancel();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    receivedCancellation = token.IsCancellationRequested;
                    throw;
                }
                throw new InvalidOperationException("Cancelled construction must not continue.");
            },
            (_, _) => throw new InvalidOperationException("Cancelled construction must not start."),
            completeMtlsProfile: true);
        await using var lifetime = fixture.ConfigureAwait(true);
        await fixture.InitializeAsync(cancellation.Token).ConfigureAwait(true);
        fixture.IsAvailable.ShouldBeFalse();
        receivedCancellation.ShouldBeTrue();
        fixture.UnavailableReason.ShouldNotBeNull();
        fixture.UnavailableReason.ShouldContain("CanceledException");
        foreach (string name in names)
        {
            Environment.GetEnvironmentVariable(name).ShouldBe(previous[name]);
        }
        await fixture.DisposeAsync().ConfigureAwait(true);
        foreach (string name in names)
        {
            Environment.GetEnvironmentVariable(name).ShouldBe(previous[name]);
        }
    }

    [Fact]
    public async Task ReadinessRequestCancellation_InterruptsPendingHttpAndCleansOwnedProfileInfrastructure()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fixture's scoped control-plane bootstrap runs on Linux.");
        }

        using var commands = new MtlsBootstrapCommandDouble();
        commands.ApplyToCurrentProcess(new Dictionary<string, string?>
        {
            ["GITHUB_ACTIONS"] = "true",
            ["Dapr__Mtls__Enabled"] = null,
            ["Dapr__Mtls__CertificateDirectory"] = null,
        });
        string[] names = ["EnableKeycloak", "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT",
            "Dapr__Mtls__Enabled", "Dapr__Mtls__CertificateDirectory", "EnableMemoriesSearch", "EnableEventStoreSampleUi"];
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        WebApplicationBuilder pendingBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [], EnvironmentName = "Test", ContentRootPath = AppContext.BaseDirectory,
        });
        pendingBuilder.WebHost.UseTestServer();
        WebApplication pending = pendingBuilder.Build();
        await using var pendingLifetime = pending.ConfigureAwait(true);
        pending.Run(context =>
        {
            requestStarted.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
        });
        await pending.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using HttpClient client = pending.GetTestClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        string? certificateDirectory = null;
        var fixture = new PartiesAspireTopologyFixture(
            async (session, token) =>
            {
                certificateDirectory = session.CertificateDirectory;
                // Exercise the readiness poll directly after scoped preparation, without starting DCP.
                await PartiesAspireTopologyFixture.WaitForEndpointAsync(client, "/pending", [HttpStatusCode.OK],
                    TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(10), token).ConfigureAwait(false);
                throw new InvalidOperationException("A cancelled pending readiness request must not complete.");
            },
            (_, _) => throw new InvalidOperationException("The pending readiness probe must cancel before startup."),
            prepareCiBootstrap: true,
            completeMtlsProfile: true);
        try
        {
            Task initialization = fixture.InitializeAsync(cancellation.Token).AsTask();
            await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
            var elapsed = Stopwatch.StartNew();
            cancellation.Cancel();
            await initialization.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
            elapsed.Elapsed.ShouldBeLessThan(client.Timeout);
            fixture.IsAvailable.ShouldBeFalse();
            fixture.UnavailableReason.ShouldNotBeNull();
            fixture.UnavailableReason.ShouldContain("CanceledException");
            certificateDirectory.ShouldNotBeNull();
            Directory.Exists(certificateDirectory).ShouldBeFalse();
            commands.OwnedRoles.ShouldBeEmpty();
            commands.OwnedNetworkExists.ShouldBeFalse();
            commands.ForeignResourcesPreserved.ShouldBeTrue();
            foreach (string name in names)
            {
                Environment.GetEnvironmentVariable(name).ShouldBe(previous[name]);
            }
        }
        finally
        {
            cancellation.Cancel();
            await fixture.DisposeAsync().ConfigureAwait(true);
        }
        foreach (string name in names)
        {
            Environment.GetEnvironmentVariable(name).ShouldBe(previous[name]);
        }
    }

}
