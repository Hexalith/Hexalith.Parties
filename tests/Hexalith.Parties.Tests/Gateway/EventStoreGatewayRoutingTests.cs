extern alias eventstore;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using eventstore::Hexalith.EventStore.Models;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Parties.Contracts.Commands;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.Parties.Tests.Gateway;

public sealed class EventStoreGatewayRoutingTests
{
    [Fact]
    public async Task PostCommands_PartyDomain_ReachesEventStoreGatewayAndRoutesPartyEnvelopeAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient();

        var request = new
        {
            messageId = "cmd-12-4-create-party",
            tenant = "tenant-a",
            domain = "party",
            aggregateId = "party-12-4",
            commandType = typeof(CreatePartyComposite).FullName,
            payload = new
            {
                partyId = "party-12-4",
                type = "Person",
                personDetails = new { firstName = "Ada", lastName = "Lovelace" },
            },
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.Location!.ToString().ShouldContain("/api/v1/commands/status/cmd-12-4-create-party");

        CommandEnvelope command = factory.CommandActor.ReceivedCommands.Single();
        command.TenantId.ShouldBe("tenant-a");
        command.Domain.ShouldBe("party");
        command.AggregateId.ShouldBe("party-12-4");
        command.CommandType.ShouldBe(typeof(CreatePartyComposite).FullName);
        command.Payload.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task PostCommands_UnauthorizedTenant_Returns403BeforePartyInvocationAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(tenants: ["tenant-b"]);

        var request = new
        {
            messageId = "cmd-12-4-denied-tenant",
            tenant = "tenant-a",
            domain = "party",
            aggregateId = "party-denied",
            commandType = typeof(CreatePartyComposite).FullName,
            payload = new { partyId = "party-denied" },
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.CommandActor.ReceivedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task PostCommands_InvalidGatewayShape_Returns400BeforePartyInvocationAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient();

        var request = new
        {
            messageId = "cmd-12-4-invalid-domain",
            tenant = "tenant-a",
            domain = "Party",
            aggregateId = "party-invalid",
            commandType = typeof(CreatePartyComposite).FullName,
            payload = new { partyId = "party-invalid" },
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        factory.CommandActor.ReceivedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task PostQueries_PartyDomain_UsesEventStoreQueryGatewayAndPartyProjectionDefaultsAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient();

        factory.QueryRouter.ResultPayload = JsonSerializer.SerializeToElement(new
        {
            id = "party-12-4",
            displayName = "Ada Lovelace",
        });

        var request = new
        {
            tenant = "tenant-a",
            domain = "party",
            aggregateId = "party-12-4",
            queryType = "PartyDetail",
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        SubmitQuery query = factory.QueryRouter.ReceivedQueries.Single();
        query.Tenant.ShouldBe("tenant-a");
        query.Domain.ShouldBe("party");
        query.AggregateId.ShouldBe("party-12-4");
        query.QueryType.ShouldBe("PartyDetail");
        query.EntityId.ShouldBe("party-12-4");
        query.ProjectionType.ShouldBe("party");
    }

    [Fact]
    public async Task PostQueries_UnauthorizedTenant_Returns403BeforeQueryRoutingAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(tenants: ["tenant-b"]);

        var request = new
        {
            tenant = "tenant-a",
            domain = "party",
            aggregateId = "party-denied",
            queryType = "PartyDetail",
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.QueryRouter.ReceivedQueries.ShouldBeEmpty();
    }

    private sealed class EventStoreGatewayTestFactory : WebApplicationFactory<EventStoreProgram>
    {
        public FakeAggregateActor CommandActor { get; } = new();

        public CapturingQueryRouter QueryRouter { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            _ = builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = GatewayJwt.Issuer,
                ["Authentication:JwtBearer:Audience"] = GatewayJwt.Audience,
                ["Authentication:JwtBearer:SigningKey"] = GatewayJwt.SigningKey,
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                ["EventStore:OpenApi:Enabled"] = "false",
                ["EventStore:RateLimiting:PermitLimit"] = "10000",
            }));

            _ = builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICommandStatusStore>();
                services.AddSingleton<ICommandStatusStore, InMemoryCommandStatusStore>();

                services.RemoveAll<ICommandArchiveStore>();
                services.AddSingleton<ICommandArchiveStore, InMemoryCommandArchiveStore>();

                var commandRouter = new FakeCommandRouter { FakeActor = CommandActor };
                TestServiceOverrides.ReplaceCommandRouter(services, commandRouter);

                services.RemoveAll<IQueryRouter>();
                services.AddSingleton<IQueryRouter>(QueryRouter);

                TestServiceOverrides.RemoveDaprHealthChecks(services);
            });
        }

        public HttpClient CreateAuthenticatedClient(string[]? tenants = null, string[]? domains = null)
        {
            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                GatewayJwt.GenerateToken(tenants ?? ["tenant-a"], domains ?? ["party"]));
            return client;
        }
    }

    private sealed class CapturingQueryRouter : IQueryRouter
    {
        private readonly List<SubmitQuery> _queries = [];

        public JsonElement ResultPayload { get; set; } = JsonSerializer.SerializeToElement(new { ok = true });

        public IReadOnlyList<SubmitQuery> ReceivedQueries => _queries;

        public Task<QueryRouterResult> RouteQueryAsync(SubmitQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            _queries.Add(query);
            return Task.FromResult(new QueryRouterResult(
                Success: true,
                Payload: ResultPayload,
                NotFound: false,
                ProjectionType: query.ProjectionType));
        }
    }

    private static class GatewayJwt
    {
        public const string SigningKey = "DevOnlySigningKey-AtLeast32Chars!";
        public const string Issuer = "hexalith-dev";
        public const string Audience = "hexalith-eventstore";

        private static readonly SymmetricSecurityKey SecurityKey = new(Encoding.UTF8.GetBytes(SigningKey));

        public static string GenerateToken(string[] tenants, string[] domains)
        {
            Claim[] claims =
            [
                new("sub", "story-12-4-user"),
                new("tenants", JsonSerializer.Serialize(tenants)),
                new("domains", JsonSerializer.Serialize(domains)),
            ];

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                NotBefore = DateTime.UtcNow.AddMinutes(-1),
                Expires = DateTime.UtcNow.AddHours(1),
                IssuedAt = DateTime.UtcNow,
                Issuer = Issuer,
                Audience = Audience,
                SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256Signature),
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(descriptor));
        }
    }
}
