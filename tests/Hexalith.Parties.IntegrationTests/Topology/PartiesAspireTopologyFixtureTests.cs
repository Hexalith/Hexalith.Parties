extern alias apphost;

using System.Diagnostics;
using System.Net;

using Aspire.Hosting;
using Aspire.Hosting.Testing;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Hexalith.Parties.TestInfrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Verifies infrastructure failures remain unavailable with their actual diagnostic.</summary>
[Collection("Non-parallel")]
public sealed class PartiesAspireTopologyFixtureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializationFailure_ReportsActualReasonAndRestoresEnvironmentOnDisposal(bool failsDuringStartup)
    {
        string? previousEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        int startAttempts = 0;
        var fixture = new PartiesAspireTopologyFixture(
            (session, cancellationToken) => failsDuringStartup
                ? DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(session.Arguments(false, false), cancellationToken)
                : Task.FromException<IDistributedApplicationTestingBuilder>(new FileNotFoundException("missing test infrastructure")),
            (_, _) =>
            {
                startAttempts++;
                throw new InvalidOperationException("test topology startup failed");
            });
        try
        {
            await fixture.InitializeAsync().ConfigureAwait(true);
            fixture.IsAvailable.ShouldBeFalse();
            fixture.UnavailableReason.ShouldNotBeNull();
            fixture.UnavailableReason.ShouldContain(failsDuringStartup
                ? "InvalidOperationException: test topology startup failed"
                : "FileNotFoundException: missing test infrastructure");
            startAttempts.ShouldBe(failsDuringStartup ? 1 : 0);
            Should.Throw<InvalidOperationException>(() => fixture.EventStoreClient).Message
                .ShouldContain(fixture.UnavailableReason!);
        }
        finally
        {
            await fixture.DisposeAsync().ConfigureAwait(true);
        }

        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT").ShouldBe(previousEnvironment);
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
                (await Should.ThrowAsync<InvalidOperationException>(() => fixture.DisposeAsync().AsTask()).ConfigureAwait(true))
                    .Message.ShouldContain("Docker unavailable");
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
