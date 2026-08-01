using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.HealthChecks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public async Task HealthEndpoint_AllComponentsHealthy_Returns200WithoutRetiredProjectionActorCheckAsync()
    {
        ConfigureHealthyDaprClient();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("results").TryGetProperty("projection-actors", out _).ShouldBeFalse();
        payload.RootElement.GetProperty("results").GetProperty("dapr-statestore").GetProperty("status").GetString()
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
        internal SwitchableTenantsReadinessProbe TenantsReadinessProbe { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");

            CommandRouter.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new CommandProcessingResult(true)));

            builder.UseSetting("Authentication:JwtBearer:Issuer", JwtTokenHelper.Issuer);
            builder.UseSetting("Authentication:JwtBearer:Audience", JwtTokenHelper.Audience);
            builder.UseSetting("Authentication:JwtBearer:SigningKey", JwtTokenHelper.SigningKey);
            builder.UseSetting("Authentication:JwtBearer:RequireHttpsMetadata", "false");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DaprClient>();
                services.AddSingleton(DaprClient);
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
