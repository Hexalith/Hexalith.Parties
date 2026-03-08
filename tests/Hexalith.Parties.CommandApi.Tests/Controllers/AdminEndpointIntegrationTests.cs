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
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Security;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Controllers;

public sealed class AdminEndpointIntegrationTests : IClassFixture<AdminEndpointIntegrationTests.AdminTestFactory>
{
    private const string RebuildEndpoint = "/api/v1/admin/projections/rebuild";
    private readonly AdminTestFactory _factory;

    public AdminEndpointIntegrationTests(AdminTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RebuildProjections_WithAdminToken_Returns202AcceptedAsync()
    {
        using HttpClient client = CreateAdminClient();
        using HttpContent body = JsonContent.Create(new
        {
            tenantId = "tenant-a",
            projection = "all",
        });

        HttpResponseMessage response = await client.PostAsync(RebuildEndpoint, body);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.TryGetProperty("correlationId", out JsonElement correlationId).ShouldBeTrue();
        correlationId.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RebuildProjections_WithoutToken_Returns401Async()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpContent body = JsonContent.Create(new
        {
            tenantId = "tenant-a",
            projection = "all",
        });

        HttpResponseMessage response = await client.PostAsync(RebuildEndpoint, body);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RebuildProjections_WithRegularUserToken_Returns403Async()
    {
        using HttpClient client = CreateRegularUserClient();
        using HttpContent body = JsonContent.Create(new
        {
            tenantId = "tenant-a",
            projection = "all",
        });

        HttpResponseMessage response = await client.PostAsync(RebuildEndpoint, body);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RebuildProjections_MissingTenantId_Returns400ValidationErrorAsync()
    {
        using HttpClient client = CreateAdminClient();
        using HttpContent body = JsonContent.Create(new
        {
            projection = "all",
        });

        HttpResponseMessage response = await client.PostAsync(RebuildEndpoint, body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("errors").GetProperty("TenantId").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RebuildProjections_InvalidProjection_Returns400ProblemDetailsAsync()
    {
        using HttpClient client = CreateAdminClient();
        using HttpContent body = JsonContent.Create(new
        {
            tenantId = "tenant-a",
            projection = "invalid-type",
        });

        HttpResponseMessage response = await client.PostAsync(RebuildEndpoint, body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        string? projectionDetail = payload.RootElement.GetProperty("detail").GetString();
        projectionDetail.ShouldNotBeNull();
        projectionDetail.ShouldContain("projection");
    }

    private HttpClient CreateAdminClient()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(includeAdminRole: true));
        return client;
    }

    private HttpClient CreateRegularUserClient()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(includeAdminRole: false));
        return client;
    }

    private static string CreateToken(bool includeAdminRole)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AdminTestFactory.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", "test-user"),
            new("eventstore:tenant", "tenant-a"),
        };

        if (includeAdminRole)
        {
            claims.Add(new Claim(ClaimTypes.Role, "admin"));
        }

        var token = new JwtSecurityToken(
            issuer: AdminTestFactory.Issuer,
            audience: AdminTestFactory.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public sealed class AdminTestFactory : WebApplicationFactory<Program>
    {
        internal const string Issuer = "hexalith-dev";
        internal const string Audience = "hexalith-parties";
        internal const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

        internal IProjectionRebuildService RebuildService { get; } = Substitute.For<IProjectionRebuildService>();
        internal IPartyKeyManagementService KeyManagementService { get; } = Substitute.For<IPartyKeyManagementService>();
        internal IErasureVerificationService ErasureVerificationService { get; } = Substitute.For<IErasureVerificationService>();
        internal IKeyStorageBackend KeyStorageBackend { get; } = Substitute.For<IKeyStorageBackend>();
        internal IKeyOperationAuditService KeyOperationAuditService { get; } = Substitute.For<IKeyOperationAuditService>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:JwtBearer:Issuer"] = Issuer,
                    ["Authentication:JwtBearer:Audience"] = Audience,
                    ["Authentication:JwtBearer:SigningKey"] = SigningKey,
                    ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                });
            });

            ICommandRouter router = Substitute.For<ICommandRouter>();
            router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new CommandProcessingResult(true)));

            IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
            IPartyDetailProjectionActor detailProxy = Substitute.For<IPartyDetailProjectionActor>();
            detailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
            actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
                .Returns(detailProxy);

            IPartyIndexProjectionActor indexProxy = Substitute.For<IPartyIndexProjectionActor>();
            indexProxy.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
                new Dictionary<string, PartyIndexEntry>()));
            actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
                .Returns(indexProxy);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICommandRouter>();
                services.AddSingleton(router);
                services.RemoveAll<IActorProxyFactory>();
                services.AddSingleton(actorProxyFactory);
                services.RemoveAll<IProjectionRebuildService>();
                services.AddSingleton(RebuildService);
                services.RemoveAll<IPartyKeyManagementService>();
                services.AddSingleton(KeyManagementService);
                services.RemoveAll<IKeyStorageBackend>();
                services.AddSingleton(KeyStorageBackend);
                services.RemoveAll<IKeyOperationAuditService>();
                services.AddSingleton(KeyOperationAuditService);
                services.RemoveAll<IErasureVerificationService>();
                services.AddSingleton(ErasureVerificationService);
                services.RemoveAll<PartyErasureOrchestrator>();
                services.AddSingleton<PartyErasureOrchestrator>();
            });
        }
    }
}
