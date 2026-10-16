extern alias apphost;

using Aspire.Hosting;
using Aspire.Hosting.Testing;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Hexalith.Parties.TestInfrastructure;

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
            () => failsDuringStartup
                ? DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>()
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
            () =>
            {
                certificateDirectory = Environment.GetEnvironmentVariable("Dapr__Mtls__CertificateDirectory");
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
}
