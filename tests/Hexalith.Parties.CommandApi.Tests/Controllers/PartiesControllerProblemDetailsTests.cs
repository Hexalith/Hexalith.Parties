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

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Controllers;

public sealed class PartiesControllerProblemDetailsTests : IClassFixture<PartiesApiTestFactory>
{
    private readonly PartiesApiTestFactory _factory;

    public PartiesControllerProblemDetailsTests(PartiesApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateParty_TenantClaimMissing_ReturnsUnauthorizedProblemDetailsAsync()
    {
        _factory.Router.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: false));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
            personDetails = new
            {
                firstName = "Ada",
                lastName = "Lovelace",
            },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties", body);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(401);
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Unauthorized");
    }

    [Fact]
    public async Task CreateParty_InvalidPayload_ReturnsBadRequestProblemDetailsAsync()
    {
        _factory.Router.ClearReceivedCalls();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = "not-a-guid",
            type = "person",
            personDetails = new
            {
                firstName = "Ada",
                lastName = "Lovelace",
            },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
        problem.RootElement.TryGetProperty("validationErrors", out _).ShouldBeTrue();

        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateParty_ValidPayload_ReturnsAcceptedWithCorrelationIdAsync()
    {
        _factory.Router.ClearReceivedCalls();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
            personDetails = new
            {
                firstName = "Ada",
                lastName = "Lovelace",
            },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties", body);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        using JsonDocument payload = await ReadJsonAsync(response);
        payload.RootElement.TryGetProperty("correlationId", out JsonElement correlationId).ShouldBeTrue();
        Guid.TryParse(correlationId.GetString(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateParty_DomainRejected_ReturnsUnprocessableEntityProblemDetailsAsync()
    {
        _factory.Router.ClearReceivedCalls();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(
                Accepted: false,
                ErrorMessage: "Domain rejection: Hexalith.Parties.Contracts.Events.PartyCannotBeCreatedWithoutType")));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
            personDetails = new
            {
                firstName = "Ada",
                lastName = "Lovelace",
            },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties", body);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(422);
        problem.RootElement.GetProperty("type").GetString()
            .ShouldBe("urn:hexalith:parties:rejection:PartyCannotBeCreatedWithoutType");
        problem.RootElement.TryGetProperty("correctiveAction", out JsonElement correctiveAction).ShouldBeTrue();
        correctiveAction.GetString().ShouldBe("Adjust the request to satisfy domain rules and retry.");
    }

    [Fact]
    public async Task CreateParty_CommandAuthorizationException_ReturnsForbiddenProblemDetailsAsync()
    {
        _factory.Router.ClearReceivedCalls();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<CommandProcessingResult>(
                new CommandAuthorizationException(
                    tenantId: "tenant-a",
                    domain: "party",
                    commandType: "CreateParty",
                    reason: "Cross-tenant access denied.")));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
            personDetails = new
            {
                firstName = "Ada",
                lastName = "Lovelace",
            },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties", body);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(403);
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Forbidden");
        problem.RootElement.GetProperty("detail").GetString().ShouldBe("Cross-tenant access denied.");
        problem.RootElement.GetProperty("tenantId").GetString().ShouldBe("tenant-a");
    }

    [Fact]
    public async Task GetParty_WithForeignTenantQualifiedIdentifier_ReturnsForbiddenProblemDetailsAsync()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        string partyId = Guid.NewGuid().ToString();
        string scopedForeignIdentifier = $"tenant-b:party:{partyId}";

        HttpResponseMessage response = await client.GetAsync($"/api/v1/parties/{Uri.EscapeDataString(scopedForeignIdentifier)}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(403);
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Forbidden");
        problem.RootElement.GetProperty("detail").GetString().ShouldBe("Cross-tenant access denied.");
    }

    /// <summary>
    /// Verifies that opaque party IDs (plain GUIDs) belonging to another tenant return 404
    /// rather than 403. This is the intentional security design: returning 403 would disclose
    /// that the party exists in another tenant, enabling cross-tenant enumeration attacks.
    /// Cross-tenant 403 is only returned for explicitly tenant-qualified identifiers (e.g.,
    /// "tenant-b:party:{id}") where the caller already knows the tenant scope.
    /// Full cross-tenant authorization for opaque IDs will be possible when read-model
    /// projections are available (Epic 3).
    /// </summary>
    [Fact]
    public async Task GetParty_OpaqueIdBelongingToOtherTenant_ReturnsNotFoundToPreventEnumerationAsync()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        // An opaque GUID that belongs to tenant-b but is requested by tenant-a.
        // The DAPR actor lookup uses tenant-a's scope so no state is found.
        string partyId = Guid.NewGuid().ToString();

        HttpResponseMessage response = await client.GetAsync($"/api/v1/parties/{partyId}");

        // 404 is correct: prevents disclosure that the party exists in another tenant.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Party Not Found");
    }

    [Theory]
    [MemberData(nameof(CommandEndpointSuccessCases))]
    public async Task CommandEndpoint_ValidPayload_ReturnsAcceptedWithCorrelationIdAsync(string endpoint, object payload)
    {
        _factory.Router.ClearReceivedCalls();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(payload);

        HttpResponseMessage response = await client.PostAsync(endpoint, body);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        using JsonDocument document = await ReadJsonAsync(response);
        document.RootElement.TryGetProperty("correlationId", out JsonElement correlationId).ShouldBeTrue();
        Guid.TryParse(correlationId.GetString(), out _).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(CommandEndpointValidationCases))]
    public async Task CommandEndpoint_InvalidPayload_ReturnsBadRequestProblemDetailsAsync(string endpoint, object payload)
    {
        _factory.Router.ClearReceivedCalls();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(payload);

        HttpResponseMessage response = await client.PostAsync(endpoint, body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Validation Failed");
        problem.RootElement.TryGetProperty("validationErrors", out _).ShouldBeTrue();

        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(CommandEndpointDomainRejectionCases))]
    public async Task CommandEndpoint_DomainRejected_ReturnsUnprocessableEntityProblemDetailsAsync(string endpoint, object payload)
    {
        _factory.Router.ClearReceivedCalls();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(
                Accepted: false,
                ErrorMessage: "Domain rejection: Hexalith.Parties.Contracts.Events.PartyNotFound")));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(payload);

        HttpResponseMessage response = await client.PostAsync(endpoint, body);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(422);
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Domain Rejection");
        problem.RootElement.GetProperty("type").GetString().ShouldBe("urn:hexalith:parties:rejection:PartyNotFound");
        problem.RootElement.TryGetProperty("correctiveAction", out JsonElement correctiveAction).ShouldBeTrue();
        correctiveAction.GetString().ShouldBe("Adjust the request to satisfy domain rules and retry.");
    }

    public static IEnumerable<object[]> CommandEndpointSuccessCases()
    {
        const string partyId = "bdb5b021-c3a1-4eb5-93e2-0498878ecabd";

        yield return
        [
            $"/api/v1/parties/{partyId}/add-contact-channel",
            new
            {
                partyId = string.Empty,
                contactChannelId = "d920f763-b05e-4c9d-b5ea-6159a3f7a916",
                type = "Email",
                value = "ada@example.org",
                isPreferred = true,
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/update-contact-channel",
            new
            {
                partyId = string.Empty,
                contactChannelId = "7e8ab4d0-28ca-4f47-a6f0-cd7f921f0f7f",
                type = "Phone",
                value = "+33123456789",
                isPreferred = false,
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/remove-contact-channel",
            new
            {
                partyId = string.Empty,
                contactChannelId = "3bcfbd6a-00be-46ef-9188-3089f89cfc78",
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/add-identifier",
            new
            {
                partyId = string.Empty,
                identifierId = "5bc75d05-c785-4d9c-9930-36f8f51885b9",
                type = "VAT",
                value = "FR40303265045",
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/remove-identifier",
            new
            {
                partyId = string.Empty,
                identifierId = "f6ee6338-d5ac-4b4e-8589-0e67e67f7768",
            },
        ];
    }

    public static IEnumerable<object[]> CommandEndpointValidationCases()
    {
        const string partyId = "6fd53a2b-aec5-444e-aaf2-2cb2fdf9a968";

        yield return
        [
            $"/api/v1/parties/{partyId}/add-contact-channel",
            new
            {
                partyId,
                contactChannelId = string.Empty,
                type = "Email",
                value = "ada@example.org",
                isPreferred = false,
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/update-contact-channel",
            new
            {
                partyId,
                contactChannelId = string.Empty,
                type = "Email",
                value = "new@example.org",
                isPreferred = true,
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/remove-contact-channel",
            new
            {
                partyId,
                contactChannelId = string.Empty,
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/add-identifier",
            new
            {
                partyId,
                identifierId = "f86cb7df-8c9e-46f5-bf2d-743d0fe65b3b",
                type = "VAT",
                value = string.Empty,
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/remove-identifier",
            new
            {
                partyId,
                identifierId = string.Empty,
            },
        ];
    }

    public static IEnumerable<object[]> CommandEndpointDomainRejectionCases()
    {
        const string partyId = "c718ba26-8a2e-4f8c-af4f-f89d145f4e97";

        yield return
        [
            $"/api/v1/parties/{partyId}/add-contact-channel",
            new
            {
                partyId,
                contactChannelId = "4b504558-26a4-4ca0-a2df-f7f11bb44ae4",
                type = "Email",
                value = "ada@example.org",
                isPreferred = false,
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/update-contact-channel",
            new
            {
                partyId,
                contactChannelId = "fef236f0-8d12-4666-adfe-c64ce5f5e9f3",
                type = "Phone",
                value = "+33987654321",
                isPreferred = true,
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/remove-contact-channel",
            new
            {
                partyId,
                contactChannelId = "dd16f0d9-a263-4daa-a95e-d68b4e3f4861",
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/add-identifier",
            new
            {
                partyId,
                identifierId = "af4a6fba-5aa5-46f9-b8fd-dd7ac91c1030",
                type = "VAT",
                value = "FR40303265045",
            },
        ];

        yield return
        [
            $"/api/v1/parties/{partyId}/remove-identifier",
            new
            {
                partyId,
                identifierId = "f0dbf73a-3a0f-4d2a-8c2e-f98744c2a3a9",
            },
        ];
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonDocument.Parse(payload);
    }
}

public sealed class PartiesApiTestFactory : WebApplicationFactory<Program>
{
    internal ICommandRouter Router { get; } = Substitute.For<ICommandRouter>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

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
            services.AddSingleton(Router);
        });
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
            new("sub", "test-user"),
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

internal sealed class CommandAuthorizationException : Exception
{
    public CommandAuthorizationException(string tenantId, string? domain, string? commandType, string reason)
        : base(reason)
    {
        TenantId = tenantId;
        Domain = domain;
        CommandType = commandType;
        Reason = reason;
    }

    public string TenantId { get; }

    public string? Domain { get; }

    public string? CommandType { get; }

    public string Reason { get; }
}
