using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.Memories.Client.Rest;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.HealthChecks;
using Hexalith.Parties.Search;
using Hexalith.Parties.Tests.Search;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

public sealed class HealthEndpointIntegrationTests : IDisposable
{
    private readonly HealthTestFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task HealthEndpoint_RegisteredInfrastructureHealthy_Returns200WithSdkReadModelCheckAsync()
    {
        ConfigureHealthyDaprClient();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("results").TryGetProperty("projection-actors", out _).ShouldBeFalse();
        payload.RootElement.GetProperty("results").GetProperty("dapr-statestore").GetProperty("status").GetString()
            .ShouldBe("Healthy");
        payload.RootElement.GetProperty("results").GetProperty("party-read-models").GetProperty("status").GetString()
            .ShouldBe("Healthy");
        payload.RootElement.GetProperty("results").GetProperty("memories-search").GetProperty("status").GetString()
            .ShouldBe("Healthy");
        _factory.MappingState.KeyExists.ShouldBeFalse();
        _factory.MappingState.SaveAttempts.ShouldBe(1);
        _factory.MappingState.DeleteAttempts.ShouldBe(1);
        _factory.InventoryState.SerializedValues.ShouldBeEmpty();
    }

    [Fact]
    public async Task HealthEndpoint_ReadModelStoreUnreachable_ReturnsDegradedWithoutBlockingReadinessAsync()
    {
        ConfigureHealthyDaprClient();
        _factory.DaprClient.GetByteStateAndETagAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync<HttpRequestException>();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage healthResponse = await client.GetAsync("/health");
        HttpResponseMessage readyResponse = await client.GetAsync("/ready");

        healthResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        readyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(await healthResponse.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("results").GetProperty("party-read-models").GetProperty("status").GetString()
            .ShouldBe("Degraded");
    }

    [Fact]
    public async Task DisabledMemoriesMappingOutageDegradesHealthWithoutBlockingReadinessAsync()
    {
        ConfigureHealthyDaprClient();
        _factory.MappingState.ReadFailure = new InvalidOperationException("private mapping backend details");
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage health = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        using HttpResponseMessage ready = await client.GetAsync("/ready", TestContext.Current.CancellationToken);

        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await JsonDocument.ParseAsync(
            await health.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        JsonElement memories = payload.RootElement.GetProperty("results").GetProperty("memories-search");
        memories.GetProperty("status").GetString().ShouldBe("Degraded");
        memories.GetProperty("data").GetProperty("enabled").GetBoolean().ShouldBeFalse();
        memories.GetProperty("data").GetProperty("mappingStoreReachable").GetBoolean().ShouldBeFalse();
        payload.RootElement.ToString().ShouldNotContain("private mapping backend details");
    }

    [Fact]
    public async Task DisabledCleanupUsesProductionRegistrationAndRetainedCredentialsAsync()
    {
        using var handler = new MemoriesCleanupProbeHandler();
        using HealthTestFactory factory = new(new Dictionary<string, string>
        {
            ["Parties:MemoriesSearch:Enabled"] = "false",
            ["Parties:MemoriesSearch:Endpoint"] = "https://memories.example/retained/",
            ["Parties:MemoriesSearch:TenantId"] = "retained-tenant",
            ["Parties:MemoriesSearch:CaseId"] = "retained-case",
            ["Parties:MemoriesSearch:ApiToken"] = "synthetic-cleanup-token",
            ["Parties:MemoriesSearch:RequireApiToken"] = "true",
        }) { CleanupHandler = handler };
        using HttpClient client = factory.CreateClient();
        var check = ActivatorUtilities.CreateInstance<MemoriesSearchHealthCheck>(factory.Services);

        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult result = await check.CheckHealthAsync(
            new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy);
        result.Data["enabled"].ShouldBe(false);
        result.Data["cleanupRouteReachable"].ShouldBe(true);
        result.Data["cleanupRouteStatus"].ShouldBe(204);
        result.Data["ingestionStatusRouteReachable"].ShouldBe(true);
        result.Data["ingestionStatusRouteStatus"].ShouldBe(404);
        result.Data["ingestionStatusAuthorizationVerified"].ShouldBe(false);
        result.Data["mappingStoreReachable"].ShouldBe(true);
        result.Data["ingestionInventoryReachable"].ShouldBe(true);
        factory.InventoryState.SerializedValues.ShouldBeEmpty();
        factory.Services.GetService<MemoriesClient>().ShouldBeNull();
        handler.Requests.Count.ShouldBe(2);
        var cleanup = handler.Requests.Where(request => request.Method == HttpMethod.Delete).ShouldHaveSingleItem();
        cleanup.Uri.ToString().ShouldStartWith(
            "https://memories.example/retained/api/v1/tenants/retained-tenant/cases/retained-case/memory-units/health-probe-");
        var statusRead = handler.Requests.Where(request => request.Method == HttpMethod.Get).ShouldHaveSingleItem();
        statusRead.Uri.ToString().ShouldStartWith("https://memories.example/retained/api/v1/ingest/health-probe-");
        handler.Requests.ShouldAllBe(request => request.Authorization == "Bearer synthetic-cleanup-token");
        factory.MappingState.KeyExists.ShouldBeFalse();
    }

    [Fact]
    public async Task ReadyEndpoint_RequiredDependenciesHealthy_Returns200Async()
    {
        ConfigureHealthyDaprClient();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadyEndpoint_PubSubDegraded_Returns200Async()
    {
        _factory.DaprClient.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(true);
        _factory.DaprClient.GetStateAsync<string?>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _factory.DaprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprMetadata("test", [], new Dictionary<string, string>(), []));
        _factory.TenantsReadinessProbe.IsReady = true;

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadyEndpoint_TenantsIntegrationUnreachable_Returns503Async()
    {
        ConfigureHealthyDaprClient();
        _factory.TenantsReadinessProbe.IsReady = false;

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("results").GetProperty("tenants-integration").GetProperty("status").GetString()
            .ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task AliveEndpoint_Always_Returns200Async()
    {
        _factory.DaprClient.CheckHealthAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync<HttpRequestException>();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/alive");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_SidecarDown_Returns503Async()
    {
        _factory.DaprClient.CheckHealthAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync<HttpRequestException>();
        _factory.DaprClient.GetStateAsync<string?>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string?)null);
        ConfigureHealthyPubSub();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ReadyEndpoint_SidecarDown_Returns503Async()
    {
        _factory.DaprClient.CheckHealthAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync<HttpRequestException>();
        _factory.DaprClient.GetStateAsync<string?>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string?)null);
        ConfigureHealthyPubSub();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task HealthEndpoint_StateStoreDown_Returns503Async()
    {
        _factory.DaprClient.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(true);
        _factory.DaprClient.GetStateAsync<string?>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync<HttpRequestException>();
        ConfigureHealthyPubSub();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ConfiguredComponentsAreUsedByProgramReadinessAndHealthAsync()
    {
        const string stateStore = "configured-security";
        const string readModelStore = "configured-read-models";
        const string pubSub = "configured-events";
        using HealthTestFactory factory = new(new Dictionary<string, string>
        {
            ["Parties:Security:StateStoreName"] = stateStore,
            ["EventStore:Projections:ReadModelStateStoreName"] = readModelStore,
            ["Tenants:PubSubName"] = pubSub,
        });
        factory.DaprClient.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(true);
        factory.DaprClient.GetStateAsync<string?>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string>(0) == stateStore
                ? (string?)null
                : throw new InvalidOperationException("Only the configured state store is available."));
        factory.DaprClient.GetByteStateAndETagAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string>(0) == readModelStore
                ? (Array.Empty<byte>(), string.Empty)
                : throw new InvalidOperationException("Only the configured read-model state store is available."));
        factory.DaprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(new DaprMetadata(
            "test", [], new Dictionary<string, string>(),
            [new DaprComponentsMetadata(pubSub, "pubsub.redis", "v1", [])]));

        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage ready = await client.GetAsync("/ready");
        using HttpResponseMessage health = await client.GetAsync("/health");
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await JsonDocument.ParseAsync(await health.Content.ReadAsStreamAsync());
        JsonElement results = payload.RootElement.GetProperty("results");
        results.GetProperty("dapr-statestore").GetProperty("status").GetString().ShouldBe("Healthy");
        results.GetProperty("dapr-statestore").GetProperty("description").GetString().ShouldNotBeNull().ShouldContain(stateStore);
        results.GetProperty("dapr-pubsub").GetProperty("status").GetString().ShouldBe("Healthy");
        results.GetProperty("dapr-pubsub").GetProperty("description").GetString().ShouldNotBeNull().ShouldContain(pubSub);
        results.GetProperty("party-read-models").GetProperty("status").GetString().ShouldBe("Healthy");
        await factory.DaprClient.Received().GetStateAsync<string?>(
            stateStore, "__health_check__", cancellationToken: Arg.Any<CancellationToken>());
        await factory.DaprClient.Received().GetByteStateAndETagAsync(
            readModelStore, Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    private HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));
        return client;
    }

    private void ConfigureHealthyDaprClient()
    {
        _factory.DaprClient.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(true);
        _factory.DaprClient.GetStateAsync<string?>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string?)null);
        ConfigureHealthyPubSub();
        _factory.TenantsReadinessProbe.IsReady = true;
    }

    private void ConfigureHealthyPubSub()
    {
        _factory.DaprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprMetadata(
                "test",
                [],
                new Dictionary<string, string>(),
                [new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", [])]));
    }

    private static PartyDetail CreatePartyDetail(string partyId) => new()
    {
        Id = partyId,
        Type = PartyType.Person,
        DisplayName = "Ada Lovelace",
        SortName = "Lovelace, Ada",
        IsActive = true,
        PersonDetails = new PersonDetails
        {
            FirstName = "Ada",
            LastName = "Lovelace",
        },
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow,
    };

    public sealed class HealthTestFactory(IReadOnlyDictionary<string, string>? settings = null) : WebApplicationFactory<Program>
    {
        internal DaprClient DaprClient { get; } = Substitute.For<DaprClient>();
        internal ICommandRouter CommandRouter { get; } = Substitute.For<ICommandRouter>();
        internal SwitchableTenantsReadinessProbe TenantsReadinessProbe { get; } = new();
        internal PartyMemoryMappingState MappingState { get; } = new();
        internal PartyMemoryTestReadModelStore InventoryState { get; } = new();
        internal HttpMessageHandler? CleanupHandler { get; init; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");
            // These tests isolate local-only health from the Development Memories endpoint.
            builder.UseSetting("Parties:MemoriesSearch:Enabled", "false");
            builder.UseSetting("Parties:MemoriesSearch:Endpoint", string.Empty);
            builder.UseSetting("Parties:MemoriesSearch:TenantId", string.Empty);
            builder.UseSetting("Parties:MemoriesSearch:CaseId", string.Empty);
            builder.UseSetting("Parties:MemoriesSearch:ApiToken", string.Empty);
            foreach (KeyValuePair<string, string> setting in settings ?? new Dictionary<string, string>())
            {
                builder.UseSetting(setting.Key, setting.Value);
            }


            CommandRouter.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new CommandProcessingResult(true)));

            builder.UseSetting("Authentication:JwtBearer:Issuer", JwtTokenHelper.Issuer);
            builder.UseSetting("Authentication:JwtBearer:Audience", JwtTokenHelper.Audience);
            builder.UseSetting("Authentication:JwtBearer:SigningKey", JwtTokenHelper.SigningKey);
            builder.UseSetting("Authentication:JwtBearer:RequireHttpsMetadata", "false");

            builder.ConfigureTestServices(services =>
            {
#if HEXALITH_DEVELOPMENT_CATALOG_SOURCE
                // This fixture supplies local health dependencies; activation itself is covered
                // by the descriptor-only Platform suite and EventStore owner tests.
                var catalog = Substitute.For<Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority>();
                catalog.ReadbackAsync(Arg.Any<CancellationToken>()).Returns(
                    new Hexalith.EventStore.Contracts.Routing.RouteCatalogGenerationReadback(
                        1, 1, "isolated", "isolated", "isolated", "Development", [], true));
                services.AddSingleton(catalog);
#endif
                services.RemoveAll<DaprClient>();
                services.AddSingleton(DaprClient);
                if (CleanupHandler is not null)
                {
                    services.AddHttpClient<PartyMemoryCleanupService>().ConfigurePrimaryHttpMessageHandler(() => CleanupHandler);
                }

                services.RemoveAll<IPartyMemoryUnitMappingStore>();
                services.AddSingleton<IPartyMemoryUnitMappingStore>(new PartyMemoryUnitMappingStore(
                    MappingState.Client,
                    Options.Create(new PartyMemoryUnitMappingStoreOptions()),
                    NullLogger<PartyMemoryUnitMappingStore>.Instance));
                services.RemoveAll<IPartyMemoryIngestionIntentStore>();
                services.AddSingleton<IPartyMemoryIngestionIntentStore>(new PartyMemoryIngestionIntentStore(
                    InventoryState, Options.Create(new PartyMemoryUnitMappingStoreOptions())));
                services.RemoveAll<ICommandRouter>();
                services.AddSingleton(CommandRouter);
                services.RemoveAll<ITenantsReadinessProbe>();
                services.AddSingleton<ITenantsReadinessProbe>(TenantsReadinessProbe);
                services.RemoveAll<Hexalith.Parties.Authorization.ITenantAccessService>();
                services.AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>();
            });
        }
    }

    internal sealed class SwitchableTenantsReadinessProbe : ITenantsReadinessProbe
    {
        public bool IsReady { get; set; } = true;

        public Task<bool> IsReadyAsync(string serviceName, CancellationToken cancellationToken)
            => Task.FromResult(IsReady);
    }
}

internal static class JwtTokenHelper
{
    internal const string Issuer = "hexalith-dev";
    internal const string Audience = "hexalith-parties";
    internal const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

    internal static string CreateToken(bool includeTenantClaim)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(PartiesClaimTypes.Subject, "integration-test-user"),
        };

        if (includeTenantClaim)
        {
            claims.Add(new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a"));
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
