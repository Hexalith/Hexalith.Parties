using System.Collections.Concurrent;
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
using Hexalith.Parties.CommandApi;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.IntegrationTests.Tenants;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Enums;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests;

public sealed class PartyApiRoundTripIntegrationTests : IClassFixture<PartyApiRoundTripTestFactory>
{
    private readonly PartyApiRoundTripTestFactory _factory;

    public PartyApiRoundTripIntegrationTests(PartyApiRoundTripTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateThenGetParty_ReturnsAcceptedThenOk_WithGdprHeaderAsync()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        string partyId = Guid.NewGuid().ToString();
        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            type = "person",
            personDetails = new
            {
                firstName = "Ada",
                lastName = "Lovelace",
            },
        });

        HttpResponseMessage createResponse = await client.PostAsync("/api/v1/parties", body);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        createResponse.Headers.TryGetValues("X-GDPR-Warning", out IEnumerable<string>? createWarningValues).ShouldBeTrue();
        createWarningValues.ShouldNotBeNull();
        createWarningValues.Single().ShouldContain("encryption at rest is enabled");

        HttpResponseMessage getResponse = await client.GetAsync($"/api/v1/parties/{partyId}");

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Headers.TryGetValues("X-GDPR-Warning", out IEnumerable<string>? getWarningValues).ShouldBeTrue();
        getWarningValues.ShouldNotBeNull();
        getWarningValues.Single().ShouldContain("encryption at rest is enabled");

        JsonDocument payload = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("id").GetString().ShouldBe(partyId);
        payload.RootElement.GetProperty("displayName").GetString().ShouldBe("Ada Lovelace");
        payload.RootElement.GetProperty("type").GetString().ShouldBe("Person");
    }

    [Fact]
    public async Task ReadyEndpoint_ReturnsSuccessAsync()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

public sealed class PartyApiRoundTripTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var partyStore = new ConcurrentDictionary<string, PartyDetail>(StringComparer.Ordinal);

        builder.UseEnvironment("Development");

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

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        proxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
            .Returns(callInfo =>
            {
                string actorIdStr = callInfo.Arg<ActorId>().GetId();
                string[] segments = actorIdStr.Split(':', StringSplitOptions.RemoveEmptyEntries);
                string partyId = segments.Length >= 3 ? segments[^1] : string.Empty;

                IPartyDetailProjectionActor detailProxy = Substitute.For<IPartyDetailProjectionActor>();
                detailProxy.GetDetailAsync().Returns(_ =>
                    partyStore.TryGetValue(partyId, out PartyDetail? detail)
                        ? Task.FromResult<PartyDetail?>(detail)
                        : Task.FromResult<PartyDetail?>(null));
                return detailProxy;
            });

        IPartyIndexProjectionActor indexProxy = Substitute.For<IPartyIndexProjectionActor>();
        indexProxy.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>()));
        proxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
            .Returns(indexProxy);

        // Mock DaprClient for health checks (no sidecar in test environment)
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(true);
        daprClient.GetStateAsync<string?>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string?)null);
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprMetadata(
                id: "test",
                actors: [],
                extended: new Dictionary<string, string>(),
                components: [new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", [])]));

        builder.ConfigureTestServices(services =>
        {
            InMemoryTenantProjectionStore tenantStore = TenantIntegrationTestSeeder.CreateProjectionStore(
                new TenantMemberSeed("tenant-a", "integration-test-user", TenantRole.TenantContributor));

            services.RemoveAll<ITenantProjectionStore>();
            services.AddSingleton<ITenantProjectionStore>(tenantStore);
            services.RemoveAll<ICommandRouter>();
            services.AddSingleton<ICommandRouter>(_ => new RecordingCommandRouter(partyStore));
            services.RemoveAll<IActorProxyFactory>();
            services.AddSingleton(proxyFactory);
            services.RemoveAll<DaprClient>();
            services.AddSingleton(daprClient);
        });
    }
}

internal sealed class RecordingCommandRouter : ICommandRouter
{
    private readonly ConcurrentDictionary<string, PartyDetail> _partyStore;

    public RecordingCommandRouter(ConcurrentDictionary<string, PartyDetail> partyStore)
    {
        _partyStore = partyStore;
    }

    public Task<CommandProcessingResult> RouteCommandAsync(SubmitCommand command, CancellationToken cancellationToken = default)
    {
        if (string.Equals(command.CommandType, "CreateParty", StringComparison.Ordinal))
        {
            using JsonDocument payload = JsonDocument.Parse(command.Payload);
            JsonElement root = payload.RootElement;
            PartyType type = ResolvePartyType(root);

            string firstName = string.Empty;
            string lastName = string.Empty;
            string legalName = string.Empty;
            PersonDetails? personDetails = null;
            OrganizationDetails? organizationDetails = null;

            if (TryGetProperty(root, "PersonDetails", out JsonElement personEl))
            {
                firstName = GetStringProperty(personEl, "FirstName");
                lastName = GetStringProperty(personEl, "LastName");
                personDetails = new PersonDetails
                {
                    FirstName = firstName,
                    LastName = lastName,
                };
            }

            if (TryGetProperty(root, "OrganizationDetails", out JsonElement orgEl))
            {
                legalName = GetStringProperty(orgEl, "LegalName");
                organizationDetails = new OrganizationDetails
                {
                    LegalName = legalName,
                };
            }

            (string displayName, string sortName) = DeriveNames(type, firstName, lastName, legalName);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            _partyStore[command.AggregateId] = new PartyDetail
            {
                Id = command.AggregateId,
                Type = type,
                IsActive = true,
                DisplayName = displayName,
                SortName = sortName,
                PersonDetails = personDetails,
                OrganizationDetails = organizationDetails,
                CreatedAt = now,
                LastModifiedAt = now,
            };
        }

        return Task.FromResult(new CommandProcessingResult(true));
    }

    private static PartyType ResolvePartyType(JsonElement root)
    {
        if (!TryGetProperty(root, "Type", out JsonElement typeElement))
        {
            return PartyType.Person;
        }

        return typeElement.ValueKind switch
        {
            JsonValueKind.Number when typeElement.TryGetInt32(out int value) => (PartyType)value,
            JsonValueKind.String => typeElement.GetString() switch
            {
                "Organization" => PartyType.Organization,
                "Person" => PartyType.Person,
                _ => PartyType.Person,
            },
            _ => PartyType.Person,
        };
    }

    private static (string DisplayName, string SortName) DeriveNames(PartyType type, string firstName, string lastName, string legalName)
    {
        if (type == PartyType.Organization)
        {
            string orgName = legalName.Trim();
            return (orgName, orgName);
        }

        string displayName = $"{firstName} {lastName}".Trim();
        string sortName = $"{lastName}, {firstName}".Trim().Trim(',').Trim();
        return (displayName, sortName);
    }

    private static string GetStringProperty(JsonElement parent, string propertyName)
        => TryGetProperty(parent, propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool TryGetProperty(JsonElement parent, string propertyName, out JsonElement value)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (parent.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        if (propertyName.Length > 0)
        {
            string camelCase = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
            if (parent.TryGetProperty(camelCase, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
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
            new("sub", "integration-test-user"),
        };

        if (includeTenantClaim)
        {
            claims.Add(new Claim("eventstore:tenant", "tenant-a"));
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
