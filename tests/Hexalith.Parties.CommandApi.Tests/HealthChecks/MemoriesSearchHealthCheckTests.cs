using System.Net;

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
        // healthy hybrid result + a cleanup-route handler that returns 404 (route reachable,
        // unit doesn't exist) + a mapping-store stub that round-trips synthetic entries. The
        // check should report Healthy and surface endpoint, tenant, case, search-health,
        // cleanup-route, and mapping-store metadata so operators can confirm provisioning,
        // auth, search-health, AND cleanup-capability from /health alone — the AC6 promise.
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
        var memoriesClient = new ProbingMemoriesClient
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
        var cleanupHandler = new RecordingCleanupHandler(HttpStatusCode.NotFound);
        var cleanupService = new PartyMemoryCleanupService(
            new HttpClient(cleanupHandler) { BaseAddress = options.Endpoint },
            new RecordingMappingStore(),
            NullLogger<PartyMemoryCleanupService>.Instance);
        var mappingStore = new RecordingMappingStore();

        var services = new ServiceCollection()
            .AddSingleton<MemoriesClient>(memoriesClient)
            .AddSingleton(cleanupService)
            .AddSingleton<IPartyMemoryUnitMappingStore>(mappingStore)
            .BuildServiceProvider();
        var check = new MemoriesSearchHealthCheck(new StubOptionsMonitor(options), services);

        // Act
        HealthCheckResult result = await check
            .CheckHealthAsync(new HealthCheckContext { Registration = new HealthCheckRegistration("test", check, null, null) }, CancellationToken.None)
            .ConfigureAwait(true);

        // Assert: AC6 requires endpoint, auth, tenant/case provisioning, search-health AND
        // cleanup-capability all reported. Each probe produces its own status field so an
        // operator inspecting /health can pinpoint exactly which capability is degraded.
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data.ShouldContainKey("endpoint");
        result.Data["endpoint"].ToString().ShouldBe("https://memories.example/");
        result.Data.ShouldContainKey("tenantId");
        result.Data["tenantId"].ShouldBe("tenant-a");
        result.Data.ShouldContainKey("caseId");
        result.Data["caseId"].ShouldBe("case-a");
        result.Data.ShouldContainKey("searchReachable");
        result.Data["searchReachable"].ShouldBe(true);
        result.Data.ShouldContainKey("degradedReportedByMemories");
        result.Data["degradedReportedByMemories"].ShouldBe(false);
        result.Data.ShouldContainKey("cleanupRouteReachable");
        result.Data["cleanupRouteReachable"].ShouldBe(true);
        result.Data.ShouldContainKey("cleanupRouteStatus");
        result.Data["cleanupRouteStatus"].ShouldBe(404);
        result.Data.ShouldContainKey("mappingStoreReachable");
        result.Data["mappingStoreReachable"].ShouldBe(true);
        memoriesClient.ProbeCalls.ShouldBe(1);
        cleanupHandler.RequestCount.ShouldBe(1);
        cleanupHandler.LastMethod.ShouldBe(HttpMethod.Delete);
        // The synthetic id reserves the _health-probe- namespace so probe traffic cannot
        // collide with a real memory unit id.
        cleanupHandler.LastUri.ShouldNotBeNull();
        cleanupHandler.LastUri!.AbsolutePath.ShouldStartWith("/api/tenants/tenant-a/cases/case-a/memory-units/_health-probe-");
        // The mapping store stub records a sentinel tenant for the round-trip; verify the
        // probe cleared its sentinel so /health does not pollute production state.
        mappingStore.AllPartyKeys.ShouldBeEmpty();
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
        var cleanupHandler = new RecordingCleanupHandler(HttpStatusCode.NotFound);
        var services = new ServiceCollection()
            .AddSingleton<MemoriesClient>(new ThrowingMemoriesClient(new HttpRequestException("connection refused")))
            .AddSingleton(new PartyMemoryCleanupService(
                new HttpClient(cleanupHandler) { BaseAddress = options.Endpoint },
                new RecordingMappingStore(),
                NullLogger<PartyMemoryCleanupService>.Instance))
            .AddSingleton<IPartyMemoryUnitMappingStore>(new RecordingMappingStore())
            .BuildServiceProvider();
        var check = new MemoriesSearchHealthCheck(new StubOptionsMonitor(options), services);

        HealthCheckResult result = await check
            .CheckHealthAsync(new HealthCheckContext { Registration = new HealthCheckRegistration("test", check, null, null) }, CancellationToken.None)
            .ConfigureAwait(true);

        // Degraded — not unhealthy — because local fallback search remains operational.
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Exception.ShouldNotBeNull();
        result.Data.ShouldContainKey("searchReachable");
        result.Data["searchReachable"].ShouldBe(false);
        // Cleanup probe still runs and should still succeed (404 sentinel) so an operator
        // can tell that ONLY search is down, not the whole integration.
        result.Data["cleanupRouteReachable"].ShouldBe(true);
    }

    [Fact]
    public async Task BrokenCleanupRouteReportsDegradedSoErasureCannotSilentlyShip()
    {
        // The named test for the AC5 cleanup re-architecture (resolved decision #2) — a
        // Memories server that doesn't expose DELETE for memory units would produce 405; the
        // probe must surface this as Degraded rather than Healthy so erasure is never silently
        // shipped against a server that cannot honor it.
        PartyMemorySearchOptions options = new()
        {
            Enabled = true,
            Endpoint = new Uri("https://memories.example/"),
            TenantId = "tenant-a",
            CaseId = "case-a",
            ApiToken = "token-x",
            RequireApiToken = true,
        };
        var cleanupHandler = new RecordingCleanupHandler(HttpStatusCode.MethodNotAllowed);
        var services = new ServiceCollection()
            .AddSingleton<MemoriesClient>(new ProbingMemoriesClient())
            .AddSingleton(new PartyMemoryCleanupService(
                new HttpClient(cleanupHandler) { BaseAddress = options.Endpoint },
                new RecordingMappingStore(),
                NullLogger<PartyMemoryCleanupService>.Instance))
            .AddSingleton<IPartyMemoryUnitMappingStore>(new RecordingMappingStore())
            .BuildServiceProvider();
        var check = new MemoriesSearchHealthCheck(new StubOptionsMonitor(options), services);

        HealthCheckResult result = await check
            .CheckHealthAsync(new HealthCheckContext { Registration = new HealthCheckRegistration("test", check, null, null) }, CancellationToken.None)
            .ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Data["searchReachable"].ShouldBe(true);
        result.Data["cleanupRouteReachable"].ShouldBe(false);
        result.Data["cleanupRouteStatus"].ShouldBe(405);
        result.Data.ShouldContainKey("cleanupRouteReason");
        string reason = result.Data["cleanupRouteReason"]?.ToString() ?? string.Empty;
        reason.ShouldContain("AC5 regression");
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

    private sealed class RecordingCleanupHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public Uri? LastUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastUri = request.RequestUri;
            LastMethod = request.Method;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class RecordingMappingStore : IPartyMemoryUnitMappingStore
    {
        private readonly Dictionary<string, List<PartyMemoryUnitMappingEntry>> _mappings = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> AllPartyKeys => _mappings.Keys;

        public Task RecordMappingAsync(string tenantId, string partyId, string memoryUnitId, string sourceUri, CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (!_mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? list))
            {
                list = [];
                _mappings[key] = list;
            }

            list.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PartyMemoryUnitMappingEntry>>(
                _mappings.TryGetValue($"{tenantId}:{partyId}", out List<PartyMemoryUnitMappingEntry>? list) ? list : []);

        public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        {
            _mappings.Remove($"{tenantId}:{partyId}");
            return Task.CompletedTask;
        }

        public Task ReplaceMappingsAsync(string tenantId, string partyId, IReadOnlyList<PartyMemoryUnitMappingEntry> entries, CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (entries.Count == 0)
            {
                _mappings.Remove(key);
            }
            else
            {
                _mappings[key] = [.. entries];
            }

            return Task.CompletedTask;
        }
    }
}
