using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.HealthChecks;
using Hexalith.Parties.Projections.Abstractions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

public sealed class HealthEndpointIntegrationTests : IClassFixture<HealthEndpointIntegrationTests.HealthTestFactory>
{
    private readonly HealthTestFactory _factory;

    public HealthEndpointIntegrationTests(HealthTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_AllComponentsHealthy_Returns200AndIncludesProjectionStatusAsync()
    {
        ConfigureHealthyDaprClient();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("results").GetProperty("projection-actors").GetProperty("status").GetString()
            .ShouldBe("Healthy");
    }

    [Fact]
    public async Task ReadyEndpoint_AllComponentsHealthy_Returns200Async()
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

    public sealed class HealthTestFactory : WebApplicationFactory<Program>
    {
        internal DaprClient DaprClient { get; } = Substitute.For<DaprClient>();
        internal ICommandRouter CommandRouter { get; } = Substitute.For<ICommandRouter>();
        internal IActorProxyFactory ActorProxyFactory { get; } = Substitute.For<IActorProxyFactory>();
        internal IPartyDetailProjectionActor DetailProjectionActor { get; } = Substitute.For<IPartyDetailProjectionActor>();
        internal IPartyIndexProjectionActor IndexProjectionActor { get; } = Substitute.For<IPartyIndexProjectionActor>();
        internal SwitchableTenantsReadinessProbe TenantsReadinessProbe { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");

            DetailProjectionActor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
            DetailProjectionActor.IsRebuildingAsync().Returns(false);
            IndexProjectionActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
                new Dictionary<string, PartyIndexEntry>()));
            IndexProjectionActor.IsRebuildingAsync().Returns(false);

            ActorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
                .Returns(DetailProjectionActor);
            ActorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
                .Returns(IndexProjectionActor);

            CommandRouter.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new CommandProcessingResult(true)));

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:JwtBearer:Issuer"] = JwtTokenHelper.Issuer,
                    ["Authentication:JwtBearer:Audience"] = JwtTokenHelper.Audience,
                    ["Authentication:JwtBearer:SigningKey"] = JwtTokenHelper.SigningKey,
                    ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DaprClient>();
                services.AddSingleton(DaprClient);
                services.RemoveAll<ICommandRouter>();
                services.AddSingleton(CommandRouter);
                services.RemoveAll<IActorProxyFactory>();
                services.AddSingleton(ActorProxyFactory);
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
