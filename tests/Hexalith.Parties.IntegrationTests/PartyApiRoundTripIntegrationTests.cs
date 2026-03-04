using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.CommandApi;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

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
        createWarningValues.Single().ShouldContain("does not include GDPR compliance features");

        HttpResponseMessage getResponse = await client.GetAsync($"/api/v1/parties/{partyId}");

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Headers.TryGetValues("X-GDPR-Warning", out IEnumerable<string>? getWarningValues).ShouldBeTrue();
        getWarningValues.ShouldNotBeNull();
        getWarningValues.Single().ShouldContain("does not include GDPR compliance features");

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

        var daprState = new ConcurrentDictionary<string, StoredPartyState>(StringComparer.Ordinal);

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

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICommandRouter>();
            services.AddSingleton<ICommandRouter>(_ => new RecordingCommandRouter(daprState));

            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(_ => new FakeDaprHttpClientFactory(daprState));
        });
    }
}

internal sealed class RecordingCommandRouter : ICommandRouter
{
    private readonly ConcurrentDictionary<string, StoredPartyState> _state;

    public RecordingCommandRouter(ConcurrentDictionary<string, StoredPartyState> state)
    {
        _state = state;
    }

    public Task<CommandProcessingResult> RouteCommandAsync(SubmitCommand command, CancellationToken cancellationToken = default)
    {
        if (string.Equals(command.CommandType, "CreateParty", StringComparison.Ordinal))
        {
            using JsonDocument payload = JsonDocument.Parse(command.Payload);
            string firstName = payload.RootElement
                .GetProperty("PersonDetails")
                .GetProperty("FirstName")
                .GetString() ?? string.Empty;
            string lastName = payload.RootElement
                .GetProperty("PersonDetails")
                .GetProperty("LastName")
                .GetString() ?? string.Empty;

            _state[command.AggregateId] = new StoredPartyState(
                DisplayName: $"{firstName} {lastName}".Trim(),
                SortName: $"{lastName}, {firstName}".Trim().Trim(',').Trim(),
                FirstName: firstName,
                LastName: lastName);
        }

        return Task.FromResult(new CommandProcessingResult(true));
    }
}

internal sealed class FakeDaprHttpClientFactory : IHttpClientFactory
{
    private readonly ConcurrentDictionary<string, StoredPartyState> _state;

    public FakeDaprHttpClientFactory(ConcurrentDictionary<string, StoredPartyState> state)
    {
        _state = state;
    }

    public HttpClient CreateClient(string name)
    {
        var handler = new FakeDaprHandler(_state);
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost"),
        };
    }
}

internal sealed class FakeDaprHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, StoredPartyState> _state;

    public FakeDaprHandler(ConcurrentDictionary<string, StoredPartyState> state)
    {
        _state = state;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri?.AbsolutePath ?? string.Empty;
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 7
            && string.Equals(segments[0], "v1.0", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[1], "actors", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "AggregateActor", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[4], "state", StringComparison.OrdinalIgnoreCase))
        {
            string actorId = Uri.UnescapeDataString(segments[3]);
            string[] actorParts = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (actorParts.Length == 3)
            {
                string aggregateId = actorParts[2];
                if (_state.TryGetValue(aggregateId, out StoredPartyState? party))
                {
                    string json = $$"""
                    {
                      "State": {
                        "Type": 1,
                        "IsActive": true,
                        "IsNaturalPerson": true,
                        "DisplayName": "{{party.DisplayName}}",
                        "SortName": "{{party.SortName}}",
                        "Person": {
                          "FirstName": "{{party.FirstName}}",
                          "LastName": "{{party.LastName}}"
                        },
                        "Organization": null,
                        "ContactChannels": [],
                        "Identifiers": []
                      },
                      "CreatedAt": "2026-03-04T00:00:00Z"
                    }
                    """;

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json"),
                    });
                }
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

internal sealed record StoredPartyState(string DisplayName, string SortName, string FirstName, string LastName);

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
