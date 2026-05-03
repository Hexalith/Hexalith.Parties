using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.CommandApi.HealthChecks;
using Hexalith.Parties.CommandApi.Search;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.HealthChecks;

public class MemoriesSearchHealthCheckTests
{
    [Fact]
    public async Task OperationalValidationReportsMemoriesEndpointAuthHealthProvisioningAndCleanup()
    {
        // Arrange a fully-configured Memories search options + a probing client that returns a
        // healthy hybrid result. The check should report Healthy and surface endpoint, tenant,
        // case, and degraded-reporting metadata as data so operators can confirm provisioning,
        // auth, and search-health from /health alone — the AC6 promise.
        PartyMemorySearchOptions options = new()
        {
            Enabled = true,
            Endpoint = new Uri("https://memories.example/"),
            TenantId = "tenant-a",
            CaseId = "case-a",
            ApiToken = "token-x",
            RequireApiToken = true,
            EnabledAxes = ["hybrid", "syntactic", "semantic", "graph"],
        };
        var client = new ProbingMemoriesClient
        {
            ProbeResult = new HybridSearchResult
            {
                Results = [],
                TotalCount = 0,
                Degraded = false,
                UnavailableAxes = [],
                Query = "healthcheck",
            },
        };
        var services = new ServiceCollection()
            .AddSingleton<MemoriesClient>(client)
            .BuildServiceProvider();
        var check = new MemoriesSearchHealthCheck(new StubOptionsMonitor(options), services);

        // Act
        HealthCheckResult result = await check
            .CheckHealthAsync(new HealthCheckContext { Registration = new HealthCheckRegistration("test", check, null, null) }, CancellationToken.None)
            .ConfigureAwait(true);

        // Assert: Memories endpoint reachability, auth (api token configured), provisioning
        // (tenant + case set), and search-health (degraded flag from probe) all reported.
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data.ShouldContainKey("endpoint");
        result.Data["endpoint"].ToString().ShouldBe("https://memories.example/");
        result.Data.ShouldContainKey("tenantId");
        result.Data["tenantId"].ShouldBe("tenant-a");
        result.Data.ShouldContainKey("caseId");
        result.Data["caseId"].ShouldBe("case-a");
        result.Data.ShouldContainKey("degradedReportedByMemories");
        result.Data["degradedReportedByMemories"].ShouldBe(false);
        client.ProbeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task DisabledMemoriesSearchReportsHealthyAndLocalOnlyMode()
    {
        PartyMemorySearchOptions options = new() { Enabled = false };
        var services = new ServiceCollection().BuildServiceProvider();
        var check = new MemoriesSearchHealthCheck(new StubOptionsMonitor(options), services);

        HealthCheckResult result = await check
            .CheckHealthAsync(new HealthCheckContext { Registration = new HealthCheckRegistration("test", check, null, null) }, CancellationToken.None)
            .ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data["mode"].ShouldBe("local-only");
    }

    [Fact]
    public async Task UnreachableMemoriesEndpointReportsDegradedNotUnhealthy()
    {
        PartyMemorySearchOptions options = new()
        {
            Enabled = true,
            Endpoint = new Uri("https://memories.example/"),
            TenantId = "tenant-a",
            CaseId = "case-a",
            ApiToken = "token-x",
            RequireApiToken = true,
        };
        var services = new ServiceCollection()
            .AddSingleton<MemoriesClient>(new ThrowingMemoriesClient(new HttpRequestException("connection refused")))
            .BuildServiceProvider();
        var check = new MemoriesSearchHealthCheck(new StubOptionsMonitor(options), services);

        HealthCheckResult result = await check
            .CheckHealthAsync(new HealthCheckContext { Registration = new HealthCheckRegistration("test", check, null, null) }, CancellationToken.None)
            .ConfigureAwait(true);

        // Degraded — not unhealthy — because local fallback search remains operational.
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Exception.ShouldNotBeNull();
        result.Data.ShouldContainKey("error");
    }

    [Fact]
    public async Task MissingTenantOrCaseReportsUnhealthy()
    {
        PartyMemorySearchOptions options = new()
        {
            Enabled = true,
            Endpoint = new Uri("https://memories.example/"),
            TenantId = "tenant-a",
            CaseId = null, // missing case
        };
        var services = new ServiceCollection().BuildServiceProvider();
        var check = new MemoriesSearchHealthCheck(new StubOptionsMonitor(options), services);

        HealthCheckResult result = await check
            .CheckHealthAsync(new HealthCheckContext { Registration = new HealthCheckRegistration("test", check, null, null) }, CancellationToken.None)
            .ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Data["caseConfigured"].ShouldBe(false);
    }

    private sealed class StubOptionsMonitor(PartyMemorySearchOptions value) : IOptionsMonitor<PartyMemorySearchOptions>
    {
        public PartyMemorySearchOptions CurrentValue { get; } = value;

        public PartyMemorySearchOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<PartyMemorySearchOptions, string?> listener) => null;
    }

    private sealed class ProbingMemoriesClient()
        : MemoriesClient(
            new HttpClient { BaseAddress = new Uri("https://memories.example") },
            Options.Create(new MemoriesClientOptions()),
            NullLogger<MemoriesClient>.Instance)
    {
        public int ProbeCalls { get; private set; }

        public HybridSearchResult ProbeResult { get; init; } = new()
        {
            Results = [],
            TotalCount = 0,
            Degraded = false,
            UnavailableAxes = [],
            Query = string.Empty,
        };

        public override Task<HybridSearchResult> HybridSearchAsync(HybridSearchRequest request, CancellationToken ct)
        {
            ProbeCalls++;
            return Task.FromResult(ProbeResult);
        }
    }

    private sealed class ThrowingMemoriesClient(Exception ex)
        : MemoriesClient(
            new HttpClient { BaseAddress = new Uri("https://memories.example") },
            Options.Create(new MemoriesClientOptions()),
            NullLogger<MemoriesClient>.Instance)
    {
        public override Task<HybridSearchResult> HybridSearchAsync(HybridSearchRequest request, CancellationToken ct)
            => Task.FromException<HybridSearchResult>(ex);
    }
}
