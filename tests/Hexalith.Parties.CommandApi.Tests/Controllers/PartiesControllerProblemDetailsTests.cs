using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Parties.CommandApi;
using Hexalith.Parties.CommandApi.Tests.Authorization;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Security;
using Hexalith.Parties.Contracts.Security;

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
        _factory.TenantAccessService.AllowAll();
        _factory.CommandGuard
            .GetBlockingReasonAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
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
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Authentication context missing");
    }

    [Fact]
    public async Task ListParties_TenantAccessDenied_ReturnsForbiddenBeforeProjectionReadAsync()
    {
        _factory.TenantAccessService.Handler = (_, _, requirement, _) => Task.FromResult(
            requirement == TenantAccessRequirement.Read
                ? TenantAccessDecision.Denied(TenantAccessDenialReason.UnknownTenant)
                : TenantAccessDecision.Allowed);

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));
        _factory.ActorProxyFactory.ClearReceivedCalls();

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("reasonCode").GetString().ShouldBe("unknown-tenant");

        _factory.ActorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => string.Equals(id.ToString(), "tenant-a:party-index", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task CreateParty_TenantWriteDenied_DoesNotGuardOrDispatchCommandAsync()
    {
        _factory.Router.ClearReceivedCalls();
        _factory.CommandGuard.ClearReceivedCalls();
        _factory.TenantAccessService.Handler = (_, _, requirement, _) => Task.FromResult(
            requirement == TenantAccessRequirement.Write
                ? TenantAccessDecision.Denied(TenantAccessDenialReason.InsufficientRole)
                : TenantAccessDecision.Allowed);

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
        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("reasonCode").GetString().ShouldBe("insufficient-role");

        await _factory.CommandGuard.DidNotReceive()
            .GetBlockingReasonAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
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
    public async Task UpdatePersonDetails_CryptoUnavailable_ReturnsUnprocessableEntityProblemDetailsAsync()
    {
        _factory.CommandGuard
            .GetBlockingReasonAsync("tenant-a", Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("Personal data writes are blocked while the party encryption key is in CryptoPending state.");

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        string partyId = Guid.NewGuid().ToString();
        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            personDetails = new
            {
                firstName = "Ada",
                lastName = "Lovelace",
            },
        });

        HttpResponseMessage response = await client.PostAsync($"/api/v1/parties/{partyId}/update-person-details", body);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Personal Data Write Blocked");
        problem.RootElement.GetProperty("type").GetString().ShouldBe("urn:hexalith:parties:error:CryptoUnavailable");
        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task ListParties_WithFiltersAndDateRange_ReturnsExpectedSubsetAsync()
    {
        _factory.SetIndexEntries(
            new PartyIndexEntry
            {
                Id = "p1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Alice Dupont",
                CreatedAt = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero),
            },
            new PartyIndexEntry
            {
                Id = "p2",
                Type = PartyType.Organization,
                IsActive = true,
                DisplayName = "Beta Corp",
                CreatedAt = new DateTimeOffset(2026, 1, 12, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 21, 0, 0, 0, TimeSpan.Zero),
            },
            new PartyIndexEntry
            {
                Id = "p3",
                Type = PartyType.Person,
                IsActive = false,
                DisplayName = "Charles Martin",
                CreatedAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 22, 0, 0, 0, TimeSpan.Zero),
            },
            new PartyIndexEntry
            {
                Id = "p4",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Zoey Outside",
                CreatedAt = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 3, 12, 0, 0, 0, TimeSpan.Zero),
            });

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/parties?type=person&active=true&createdAfter=2026-01-01&createdBefore=2026-02-01&modifiedAfter=2026-01-01&modifiedBefore=2026-02-01");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);

        payload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(1);
        payload.RootElement.GetProperty("totalPages").GetInt32().ShouldBe(1);

        JsonElement items = payload.RootElement.GetProperty("items");
        items.GetArrayLength().ShouldBe(1);
        items[0].GetProperty("id").GetString().ShouldBe("p1");
        items[0].GetProperty("displayName").GetString().ShouldBe("Alice Dupont");
    }

    [Fact]
    public async Task ListParties_InvalidPaginationBounds_AreClampedAsync()
    {
        var entries = new List<PartyIndexEntry>();
        for (int index = 0; index < 150; index++)
        {
            entries.Add(new PartyIndexEntry
            {
                Id = $"p-{index:D3}",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = $"Name {index:D3}",
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(index),
                LastModifiedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(index),
            });
        }

        _factory.SetIndexEntries(entries.ToArray());

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties?page=0&pageSize=500");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);

        payload.RootElement.GetProperty("page").GetInt32().ShouldBe(1);
        payload.RootElement.GetProperty("pageSize").GetInt32().ShouldBe(100);
        payload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(150);
        payload.RootElement.GetProperty("totalPages").GetInt32().ShouldBe(2);
        payload.RootElement.GetProperty("items").GetArrayLength().ShouldBe(100);
    }

    [Fact]
    public async Task SearchParties_WithDisplayNameMatches_ReturnsRankedMatchesWithMetadataAsync()
    {
        _factory.SetIndexEntries(
            new PartyIndexEntry
            {
                Id = "exact",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Dupont",
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new PartyIndexEntry
            {
                Id = "prefix",
                Type = PartyType.Organization,
                IsActive = true,
                DisplayName = "Dupont Group",
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new PartyIndexEntry
            {
                Id = "contains",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Jean Dupont",
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new PartyIndexEntry
            {
                Id = "none",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Martin",
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            });

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/search?q=Dupont&page=1&pageSize=20");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);

        JsonElement results = payload.RootElement.GetProperty("results");
        results.GetProperty("totalCount").GetInt32().ShouldBe(3);
        JsonElement items = results.GetProperty("items");
        items.GetArrayLength().ShouldBe(3);

        items[0].GetProperty("party").GetProperty("id").GetString().ShouldBe("exact");
        items[0].GetProperty("matches")[0].GetProperty("matchedField").GetString().ShouldBe("displayName");
        items[0].GetProperty("matches")[0].GetProperty("matchType").GetString().ShouldBe("exact");

        items[1].GetProperty("party").GetProperty("id").GetString().ShouldBe("prefix");
        items[1].GetProperty("matches")[0].GetProperty("matchType").GetString().ShouldBe("prefix");

        items[2].GetProperty("party").GetProperty("id").GetString().ShouldBe("contains");
        items[2].GetProperty("matches")[0].GetProperty("matchType").GetString().ShouldBe("contains");
    }

    [Fact]
    public async Task SearchParties_EmptyQuery_ReturnsEmptyPagedResultAsync()
    {
        _factory.SetIndexEntries(
            new PartyIndexEntry
            {
                Id = "p1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Someone",
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            });

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/search");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);

        JsonElement results = payload.RootElement.GetProperty("results");
        results.GetProperty("totalCount").GetInt32().ShouldBe(0);
        results.GetProperty("totalPages").GetInt32().ShouldBe(1);
        results.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ListParties_UsesTenantScopedIndexActorIdAsync()
    {
        _factory.ActorProxyFactory.ClearReceivedCalls();
        _factory.SetIndexEntries(
            new PartyIndexEntry
            {
                Id = "tenant-entry",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Tenant Scoped",
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            });

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        _factory.ActorProxyFactory.Received().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => string.Equals(id.GetId(), "tenant-a:party-index", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // --- 5-party search scenario tests (AC #3) ---

    [Fact]
    public async Task SearchParties_FivePartyScenario_ReturnsDupontMatchesWithMetadataAsync()
    {
        SetFivePartyScenario();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/search?q=Dupont");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);

        // 3 matches: Dupont Alice, Dupont Bernard, Dupont Industries (all prefix)
        JsonElement results = payload.RootElement.GetProperty("results");
        results.GetProperty("totalCount").GetInt32().ShouldBe(3);
        JsonElement items = results.GetProperty("items");
        items.GetArrayLength().ShouldBe(3);

        // Sorted by priority (all prefix=1) then by displayName alphabetically
        items[0].GetProperty("party").GetProperty("id").GetString().ShouldBe("p1");
        items[0].GetProperty("party").GetProperty("displayName").GetString().ShouldBe("Dupont Alice");
        items[0].GetProperty("matches")[0].GetProperty("matchedField").GetString().ShouldBe("displayName");
        items[0].GetProperty("matches")[0].GetProperty("matchType").GetString().ShouldBe("prefix");

        items[1].GetProperty("party").GetProperty("id").GetString().ShouldBe("p2");
        items[1].GetProperty("matches")[0].GetProperty("matchType").GetString().ShouldBe("prefix");

        items[2].GetProperty("party").GetProperty("id").GetString().ShouldBe("p4");
        items[2].GetProperty("party").GetProperty("displayName").GetString().ShouldBe("Dupont Industries");
        items[2].GetProperty("matches")[0].GetProperty("matchType").GetString().ShouldBe("prefix");
    }

    [Fact]
    public async Task ListParties_FivePartyScenario_TypeFilterPerson_ReturnsOnlyPersonsAsync()
    {
        SetFivePartyScenario();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties?type=person");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);

        payload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(3);
        JsonElement items = payload.RootElement.GetProperty("items");
        items.GetArrayLength().ShouldBe(3);

        for (int i = 0; i < items.GetArrayLength(); i++)
        {
            items[i].GetProperty("type").GetString().ShouldBe("Person");
        }
    }

    [Fact]
    public async Task ListParties_FivePartyScenario_TypeFilterOrganization_ReturnsOnlyOrganizationsAsync()
    {
        SetFivePartyScenario();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties?type=organization");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);

        payload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(2);
        JsonElement items = payload.RootElement.GetProperty("items");
        items.GetArrayLength().ShouldBe(2);

        for (int i = 0; i < items.GetArrayLength(); i++)
        {
            items[i].GetProperty("type").GetString().ShouldBe("Organization");
        }
    }

    [Fact]
    public async Task ListParties_FivePartyScenario_ActiveFilter_WorksCorrectlyAsync()
    {
        SetFivePartyScenario(deactivateSecond: true);

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        // active=true excludes deactivated p2
        using HttpResponseMessage activeResponse = await client.GetAsync("/api/v1/parties?active=true");
        activeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument activePayload = await ReadJsonAsync(activeResponse);
        activePayload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(4);

        // active=false returns only deactivated p2
        using HttpResponseMessage inactiveResponse = await client.GetAsync("/api/v1/parties?active=false");
        inactiveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument inactivePayload = await ReadJsonAsync(inactiveResponse);
        inactivePayload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(1);
        inactivePayload.RootElement.GetProperty("items")[0].GetProperty("id").GetString().ShouldBe("p2");
    }

    [Fact]
    public async Task ListParties_FivePartyScenario_DateRangeFilters_ReturnCorrectSubsetsAsync()
    {
        SetFivePartyScenario();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        // createdAfter=2026-01-14 -> 3 parties (p2 Jan 15, p3 Feb 01, p5 Feb 15)
        using HttpResponseMessage createdAfterResponse = await client.GetAsync("/api/v1/parties?createdAfter=2026-01-14");
        createdAfterResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument createdAfterPayload = await ReadJsonAsync(createdAfterResponse);
        createdAfterPayload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(3);
        List<string?> createdAfterIds = ExtractItemIds(createdAfterPayload);
        createdAfterIds.ShouldContain("p2");
        createdAfterIds.ShouldContain("p3");
        createdAfterIds.ShouldContain("p5");

        // createdBefore=2026-01-13 -> 2 parties (p1 Jan 10, p4 Jan 12)
        using HttpResponseMessage createdBeforeResponse = await client.GetAsync("/api/v1/parties?createdBefore=2026-01-13");
        createdBeforeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument createdBeforePayload = await ReadJsonAsync(createdBeforeResponse);
        createdBeforePayload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(2);
        List<string?> createdBeforeIds = ExtractItemIds(createdBeforePayload);
        createdBeforeIds.ShouldContain("p1");
        createdBeforeIds.ShouldContain("p4");

        // modifiedAfter=2026-02-01 -> 2 parties (p3 Feb 10, p5 Feb 20)
        using HttpResponseMessage modifiedAfterResponse = await client.GetAsync("/api/v1/parties?modifiedAfter=2026-02-01");
        modifiedAfterResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument modifiedAfterPayload = await ReadJsonAsync(modifiedAfterResponse);
        modifiedAfterPayload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(2);
        List<string?> modifiedAfterIds = ExtractItemIds(modifiedAfterPayload);
        modifiedAfterIds.ShouldContain("p3");
        modifiedAfterIds.ShouldContain("p5");

        // modifiedBefore=2026-01-23 -> 2 parties (p1 Jan 20, p4 Jan 22)
        using HttpResponseMessage modifiedBeforeResponse = await client.GetAsync("/api/v1/parties?modifiedBefore=2026-01-23");
        modifiedBeforeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument modifiedBeforePayload = await ReadJsonAsync(modifiedBeforeResponse);
        modifiedBeforePayload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(2);
        List<string?> modifiedBeforeIds = ExtractItemIds(modifiedBeforePayload);
        modifiedBeforeIds.ShouldContain("p1");
        modifiedBeforeIds.ShouldContain("p4");
    }

    [Fact]
    public async Task SearchParties_FivePartyScenario_EmailQuery_ReturnsEmailMatchMetadataAsync()
    {
        SetFivePartyScenario();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/search?q=alice%40example.com");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);
        JsonElement results = payload.RootElement.GetProperty("results");
        results.GetProperty("totalCount").GetInt32().ShouldBe(1);
        JsonElement first = results.GetProperty("items")[0];
        first.GetProperty("party").GetProperty("id").GetString().ShouldBe("p1");
        first.GetProperty("matches")[0].GetProperty("matchedField").GetString().ShouldBe("email");
    }

    [Fact]
    public async Task SearchParties_MultiTermQuery_ReturnsNameAndOrganizationMatchesAsync()
    {
        _factory.SetIndexEntries(
            new PartyIndexEntry
            {
                Id = "person-match",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Jean Dupont",
                CreatedAt = DateTimeOffset.UtcNow,
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            new PartyIndexEntry
            {
                Id = "org-match",
                Type = PartyType.Organization,
                IsActive = true,
                DisplayName = "Acme Corp",
                CreatedAt = DateTimeOffset.UtcNow,
                LastModifiedAt = DateTimeOffset.UtcNow,
            });

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/search?q=Dupont%20Acme");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);
        payload.RootElement.GetProperty("results").GetProperty("totalCount").GetInt32().ShouldBe(2);
        List<string?> ids = ExtractSearchItemIds(payload);
        ids.ShouldContain("person-match");
        ids.ShouldContain("org-match");
    }

    // --- Tenant isolation tests (AC #4) ---

    [Fact]
    public async Task TenantIsolation_ListAndSearch_TenantsOnlySeeOwnPartiesAsync()
    {
        // Arrange: set up tenant-specific proxies with different data
        var tenantAEntries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
        {
            ["a1"] = new() { Id = "a1", Type = PartyType.Person, IsActive = true, DisplayName = "Alice", CreatedAt = DateTimeOffset.UtcNow, LastModifiedAt = DateTimeOffset.UtcNow },
            ["a2"] = new() { Id = "a2", Type = PartyType.Person, IsActive = true, DisplayName = "Bob", CreatedAt = DateTimeOffset.UtcNow, LastModifiedAt = DateTimeOffset.UtcNow },
            ["a3"] = new() { Id = "a3", Type = PartyType.Organization, IsActive = true, DisplayName = "Corp A", CreatedAt = DateTimeOffset.UtcNow, LastModifiedAt = DateTimeOffset.UtcNow },
        };

        var tenantBEntries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
        {
            ["b1"] = new() { Id = "b1", Type = PartyType.Person, IsActive = true, DisplayName = "Xavier", CreatedAt = DateTimeOffset.UtcNow, LastModifiedAt = DateTimeOffset.UtcNow },
            ["b2"] = new() { Id = "b2", Type = PartyType.Organization, IsActive = true, DisplayName = "Corp B", CreatedAt = DateTimeOffset.UtcNow, LastModifiedAt = DateTimeOffset.UtcNow },
        };

        IPartyIndexProjectionActor tenantAProxy = Substitute.For<IPartyIndexProjectionActor>();
        tenantAProxy.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(tenantAEntries));

        IPartyIndexProjectionActor tenantBProxy = Substitute.For<IPartyIndexProjectionActor>();
        tenantBProxy.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(tenantBEntries));

        _factory.ActorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => string.Equals(id.GetId(), "tenant-a:party-index", StringComparison.Ordinal)),
            Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
            .Returns(tenantAProxy);

        _factory.ActorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => string.Equals(id.GetId(), "tenant-b:party-index", StringComparison.Ordinal)),
            Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
            .Returns(tenantBProxy);

        try
        {
            // List as Tenant A -> only 3 parties
            using HttpClient clientA = _factory.CreateClient();
            clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken("tenant-a"));

            using HttpResponseMessage listA = await clientA.GetAsync("/api/v1/parties");
            listA.StatusCode.ShouldBe(HttpStatusCode.OK);
            using JsonDocument listAPayload = await ReadJsonAsync(listA);
            listAPayload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(3);
            List<string?> listAIds = ExtractItemIds(listAPayload);
            listAIds.ShouldContain("a1");
            listAIds.ShouldContain("a2");
            listAIds.ShouldContain("a3");
            listAIds.ShouldNotContain("b1");
            listAIds.ShouldNotContain("b2");

            // List as Tenant B -> only 2 parties (zero Tenant A leakage)
            using HttpClient clientB = _factory.CreateClient();
            clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken("tenant-b"));

            using HttpResponseMessage listB = await clientB.GetAsync("/api/v1/parties");
            listB.StatusCode.ShouldBe(HttpStatusCode.OK);
            using JsonDocument listBPayload = await ReadJsonAsync(listB);
            listBPayload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(2);
            List<string?> listBIds = ExtractItemIds(listBPayload);
            listBIds.ShouldContain("b1");
            listBIds.ShouldContain("b2");
            listBIds.ShouldNotContain("a1");
            listBIds.ShouldNotContain("a2");
            listBIds.ShouldNotContain("a3");

            // Search "Alice" as Tenant A -> 1 match
            using HttpResponseMessage searchA = await clientA.GetAsync("/api/v1/parties/search?q=Alice");
            searchA.StatusCode.ShouldBe(HttpStatusCode.OK);
            using JsonDocument searchAPayload = await ReadJsonAsync(searchA);
            searchAPayload.RootElement.GetProperty("results").GetProperty("totalCount").GetInt32().ShouldBe(1);
            List<string?> searchAIds = ExtractSearchItemIds(searchAPayload);
            searchAIds.ShouldContain("a1");

            // Search "Alice" as Tenant B -> 0 matches (Alice belongs to Tenant A)
            using HttpResponseMessage searchB = await clientB.GetAsync("/api/v1/parties/search?q=Alice");
            searchB.StatusCode.ShouldBe(HttpStatusCode.OK);
            using JsonDocument searchBPayload = await ReadJsonAsync(searchB);
            searchBPayload.RootElement.GetProperty("results").GetProperty("totalCount").GetInt32().ShouldBe(0);
        }
        finally
        {
            _factory.ResetIndexProxy();
        }
    }

    [Fact]
    public async Task GetParty_TenantIsolation_VerifiesActorIdIsTenantScopedAsync()
    {
        _factory.ActorProxyFactory.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken("tenant-a"));

        string partyId = Guid.NewGuid().ToString();
        await client.GetAsync($"/api/v1/parties/{partyId}");

        _factory.ActorProxyFactory.Received().CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Is<ActorId>(id => string.Equals(id.GetId(), $"tenant-a:party-detail:{partyId}", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // --- Query endpoint edge cases (AC #5) ---

    [Fact]
    public async Task ListParties_PageBeyondTotal_ReturnsEmptyItemsWithCorrectTotalAsync()
    {
        SetFivePartyScenario();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        // 5 parties with pageSize=2 -> 3 pages. Request page 4 (beyond total).
        HttpResponseMessage response = await client.GetAsync("/api/v1/parties?page=4&pageSize=2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);

        payload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(5);
        payload.RootElement.GetProperty("totalPages").GetInt32().ShouldBe(3);
        payload.RootElement.GetProperty("page").GetInt32().ShouldBe(4);
        payload.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ListParties_EmptyIndex_ReturnsEmptyPagedResultAsync()
    {
        _factory.SetIndexEntries();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);

        payload.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(0);
        payload.RootElement.GetProperty("totalPages").GetInt32().ShouldBe(1);
        payload.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task SearchParties_EmptyIndex_WithQuery_ReturnsEmptyPagedResultAsync()
    {
        _factory.SetIndexEntries();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/search?q=Dupont");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = await ReadJsonAsync(response);

        JsonElement results = payload.RootElement.GetProperty("results");
        results.GetProperty("totalCount").GetInt32().ShouldBe(0);
        results.GetProperty("totalPages").GetInt32().ShouldBe(1);
        results.GetProperty("items").GetArrayLength().ShouldBe(0);
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

    private void SetFivePartyScenario(bool deactivateSecond = false)
    {
        _factory.SetIndexEntries(
            new PartyIndexEntry
            {
                Id = "p1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Dupont Alice",
                SearchableContactChannels =
                [
                    new ContactChannel
                    {
                        Id = "cc-p1-email",
                        Type = ContactChannelType.Email,
                        Value = "alice@example.com",
                        IsPreferred = true,
                    },
                ],
                SearchableIdentifiers =
                [
                    new PartyIdentifier
                    {
                        Id = "id-p1-vat",
                        Type = IdentifierType.VAT,
                        Value = "FR12345678901",
                    },
                ],
                CreatedAt = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero),
            },
            new PartyIndexEntry
            {
                Id = "p2",
                Type = PartyType.Person,
                IsActive = !deactivateSecond,
                DisplayName = "Dupont Bernard",
                SearchableContactChannels = [],
                SearchableIdentifiers =
                [
                    new PartyIdentifier
                    {
                        Id = "id-p2-national",
                        Type = IdentifierType.NationalId,
                        Value = "BNR-2026-42",
                    },
                ],
                CreatedAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 25, 0, 0, 0, TimeSpan.Zero),
            },
            new PartyIndexEntry
            {
                Id = "p3",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Martin Claire",
                SearchableContactChannels =
                [
                    new ContactChannel
                    {
                        Id = "cc-p3-email",
                        Type = ContactChannelType.Email,
                        Value = "claire@example.com",
                        IsPreferred = true,
                    },
                ],
                SearchableIdentifiers = [],
                CreatedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero),
            },
            new PartyIndexEntry
            {
                Id = "p4",
                Type = PartyType.Organization,
                IsActive = true,
                DisplayName = "Dupont Industries",
                SearchableContactChannels =
                [
                    new ContactChannel
                    {
                        Id = "cc-p4-email",
                        Type = ContactChannelType.Email,
                        Value = "contact@example.com",
                        IsPreferred = true,
                    },
                ],
                SearchableIdentifiers =
                [
                    new PartyIdentifier
                    {
                        Id = "id-p4-org",
                        Type = IdentifierType.Other,
                        Value = "ACME-42",
                    },
                ],
                CreatedAt = new DateTimeOffset(2026, 1, 12, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 1, 22, 0, 0, 0, TimeSpan.Zero),
            },
            new PartyIndexEntry
            {
                Id = "p5",
                Type = PartyType.Organization,
                IsActive = true,
                DisplayName = "Global Tech",
                SearchableContactChannels = [],
                SearchableIdentifiers =
                [
                    new PartyIdentifier
                    {
                        Id = "id-p5-org",
                        Type = IdentifierType.Other,
                        Value = "ACME-GLOBAL",
                    },
                ],
                CreatedAt = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero),
                LastModifiedAt = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            });
    }

    [Fact]
    public async Task CreatePartyComposite_ValidPayload_ReturnsAcceptedWithCorrelationIdAsync()
    {
        _factory.Router.ClearReceivedCalls();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(
                Accepted: true,
                EventCount: 4)));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
            personDetails = new { firstName = "Ada", lastName = "Lovelace" },
            contactChannels = new[]
            {
                new
                {
                    partyId = Guid.NewGuid().ToString(),
                    contactChannelId = Guid.NewGuid().ToString(),
                    type = "email",
                    value = "ada@example.com",
                },
            },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties/create-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using JsonDocument payload = await ReadJsonAsync(response);
        payload.RootElement.TryGetProperty("correlationId", out JsonElement correlationId).ShouldBeTrue();
        Guid.TryParse(correlationId.GetString(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task CreatePartyComposite_InvalidPartyId_ReturnsBadRequestAsync()
    {
        _factory.Router.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = "not-a-guid",
            type = "person",
            personDetails = new { firstName = "Ada", lastName = "Lovelace" },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties/create-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePartyComposite_MissingPersonDetails_ReturnsBadRequestAsync()
    {
        _factory.Router.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties/create-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePartyComposite_MissingOrganizationDetails_ReturnsBadRequestAsync()
    {
        _factory.Router.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "organization",
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties/create-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePartyComposite_DomainRejection_ReturnsUnprocessableEntityAsync()
    {
        _factory.Router.ClearReceivedCalls();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(
                Accepted: false,
                ErrorMessage: "Domain rejection: Hexalith.Parties.Contracts.Events.CompositeOperationConflict")));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
            personDetails = new { firstName = "Ada", lastName = "Lovelace" },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties/create-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(422);
        problem.RootElement.GetProperty("type").GetString()
            .ShouldBe("urn:hexalith:parties:rejection:CompositeOperationConflict");
    }

    [Fact]
    public async Task CreatePartyComposite_TenantMissing_ReturnsUnauthorizedAsync()
    {
        _factory.Router.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: false));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
            personDetails = new { firstName = "Ada", lastName = "Lovelace" },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties/create-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePartyComposite_ValidPayload_ReturnsAcceptedWithCorrelationIdAsync()
    {
        _factory.Router.ClearReceivedCalls();

        string partyId = Guid.NewGuid().ToString();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(
                Accepted: true,
                EventCount: 2)));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            personDetails = new { firstName = "Ada", lastName = "Lovelace" },
        });

        HttpResponseMessage response = await client.PostAsync($"/api/v1/parties/{partyId}/update-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using JsonDocument payload = await ReadJsonAsync(response);
        payload.RootElement.TryGetProperty("correlationId", out JsonElement correlationId).ShouldBeTrue();
        Guid.TryParse(correlationId.GetString(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task UpdatePartyComposite_InvalidPartyId_ReturnsBadRequestAsync()
    {
        _factory.Router.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        string routeId = Guid.NewGuid().ToString();
        using HttpContent body = JsonContent.Create(new
        {
            partyId = "not-a-guid",
            personDetails = new { firstName = "Ada", lastName = "Lovelace" },
        });

        HttpResponseMessage response = await client.PostAsync($"/api/v1/parties/{routeId}/update-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyComposite_RouteBodyPartyIdMismatch_ReturnsBadRequestAsync()
    {
        _factory.Router.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        string routeId = Guid.NewGuid().ToString();
        string bodyId = Guid.NewGuid().ToString();
        using HttpContent body = JsonContent.Create(new
        {
            partyId = bodyId,
            personDetails = new { firstName = "Ada", lastName = "Lovelace" },
        });

        HttpResponseMessage response = await client.PostAsync($"/api/v1/parties/{routeId}/update-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyComposite_DomainRejection_ReturnsUnprocessableEntityAsync()
    {
        _factory.Router.ClearReceivedCalls();

        string partyId = Guid.NewGuid().ToString();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(
                Accepted: false,
                ErrorMessage: "Domain rejection: Hexalith.Parties.Contracts.Events.ContactChannelNotFound")));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            removeContactChannelIds = new[] { Guid.NewGuid().ToString() },
        });

        HttpResponseMessage response = await client.PostAsync($"/api/v1/parties/{partyId}/update-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(422);
        problem.RootElement.GetProperty("type").GetString()
            .ShouldBe("urn:hexalith:parties:rejection:ContactChannelNotFound");
    }

    [Fact]
    public async Task CreatePartyComposite_InvalidContactChannelId_ReturnsBadRequestAsync()
    {
        _factory.Router.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
            personDetails = new { firstName = "Ada", lastName = "Lovelace" },
            contactChannels = new[]
            {
                new
                {
                    partyId = Guid.NewGuid().ToString(),
                    contactChannelId = "not-a-guid",
                    type = "email",
                    value = "ada@example.com",
                },
            },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties/create-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyComposite_InvalidRemoveContactChannelId_ReturnsBadRequestAsync()
    {
        _factory.Router.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        string partyId = Guid.NewGuid().ToString();
        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            removeContactChannelIds = new[] { "not-a-guid" },
        });

        HttpResponseMessage response = await client.PostAsync($"/api/v1/parties/{partyId}/update-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyComposite_AddContactChannelMissingValue_ReturnsBadRequestAsync()
    {
        _factory.Router.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        string partyId = Guid.NewGuid().ToString();
        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            addContactChannels = new[]
            {
                new
                {
                    partyId,
                    contactChannelId = Guid.NewGuid().ToString(),
                    type = "email",
                    value = "",
                },
            },
        });

        HttpResponseMessage response = await client.PostAsync($"/api/v1/parties/{partyId}/update-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyComposite_AddIdentifierMissingValue_ReturnsBadRequestAsync()
    {
        _factory.Router.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        string partyId = Guid.NewGuid().ToString();
        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            addIdentifiers = new[]
            {
                new
                {
                    partyId,
                    identifierId = Guid.NewGuid().ToString(),
                    type = "vat",
                    value = "",
                },
            },
        });

        HttpResponseMessage response = await client.PostAsync($"/api/v1/parties/{partyId}/update-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        await _factory.Router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePartyComposite_AcceptedWithoutRouterCorrelationId_ReturnsGeneratedCorrelationIdOnlyAsync()
    {
        _factory.Router.ClearReceivedCalls();

        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(Accepted: true)));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
            personDetails = new { firstName = "Ada", lastName = "Lovelace" },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties/create-composite", body);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using JsonDocument payload = await ReadJsonAsync(response);
        payload.RootElement.TryGetProperty("correlationId", out JsonElement correlationId).ShouldBeTrue();
        Guid.TryParse(correlationId.GetString(), out _).ShouldBeTrue();
    }

    private static List<string?> ExtractItemIds(JsonDocument payload)
    {
        JsonElement items = payload.RootElement.GetProperty("items");
        List<string?> ids = [];
        for (int i = 0; i < items.GetArrayLength(); i++)
        {
            ids.Add(items[i].GetProperty("id").GetString());
        }

        return ids;
    }

    private static List<string?> ExtractSearchItemIds(JsonDocument payload)
    {
        // The /search endpoint returns a PartySearchResponse envelope; items live under .results.items
        JsonElement root = payload.RootElement;
        JsonElement items = root.TryGetProperty("results", out JsonElement results)
            ? results.GetProperty("items")
            : root.GetProperty("items");
        List<string?> ids = [];
        for (int i = 0; i < items.GetArrayLength(); i++)
        {
            ids.Add(items[i].GetProperty("party").GetProperty("id").GetString());
        }

        return ids;
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

    internal IActorProxyFactory ActorProxyFactory { get; } = Substitute.For<IActorProxyFactory>();

    internal IPersonalDataCommandGuard CommandGuard { get; } = Substitute.For<IPersonalDataCommandGuard>();

    internal TestTenantAccessService TenantAccessService { get; } = new();

    private IReadOnlyDictionary<string, PartyIndexEntry> _indexEntries =
        new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal);

    private readonly Dictionary<string, IReadOnlyDictionary<string, PartyIndexEntry>> _indexEntriesByActorId =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, PartyDetail> _detailsByActorId = new(StringComparer.Ordinal);

    internal void SetIndexEntries(params PartyIndexEntry[] entries)
    {
        _indexEntries = entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
    }

    internal void SetTenantParties(string tenantId, params PartyDetail[] details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(details);

        var indexEntries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal);
        foreach (PartyDetail detail in details)
        {
            _detailsByActorId[$"{tenantId}:party-detail:{detail.Id}"] = detail;
            indexEntries[detail.Id] = new PartyIndexEntry
            {
                Id = detail.Id,
                Type = detail.Type,
                IsActive = detail.IsActive,
                DisplayName = detail.DisplayName,
                SearchableContactChannels = detail.ContactChannels,
                SearchableIdentifiers = detail.Identifiers,
                CreatedAt = detail.CreatedAt,
                LastModifiedAt = detail.LastModifiedAt,
                IsErased = detail.IsErased,
            };
        }

        _indexEntriesByActorId[$"{tenantId}:party-index"] = indexEntries;
        ResetIndexProxy();
    }

    internal void ResetIndexProxy()
    {
        ActorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
            .Returns(call =>
            {
                string actorId = call.ArgAt<ActorId>(0).GetId();
                IReadOnlyDictionary<string, PartyIndexEntry> entries =
                    _indexEntriesByActorId.TryGetValue(actorId, out IReadOnlyDictionary<string, PartyIndexEntry>? tenantEntries)
                        ? tenantEntries
                        : _indexEntries;
                IPartyIndexProjectionActor indexProxy = Substitute.For<IPartyIndexProjectionActor>();
                indexProxy.GetEntriesAsync().Returns(_ => Task.FromResult(entries));
                return indexProxy;
            });
    }

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

        ActorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
            .Returns(call =>
            {
                string actorId = call.ArgAt<ActorId>(0).GetId();
                _ = _detailsByActorId.TryGetValue(actorId, out PartyDetail? detail);
                IPartyDetailProjectionActor detailProxy = Substitute.For<IPartyDetailProjectionActor>();
                detailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(detail));
                return detailProxy;
            });

        ResetIndexProxy();

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICommandRouter>();
            services.AddSingleton(Router);
            services.RemoveAll<IActorProxyFactory>();
            services.AddSingleton(ActorProxyFactory);
            services.RemoveAll<IPersonalDataCommandGuard>();
            services.AddSingleton(CommandGuard);
            services.RemoveAll<ITenantAccessService>();
            services.AddSingleton<ITenantAccessService>(TenantAccessService);
        });
    }
}

internal static class JwtTokenHelper
{
    internal const string Issuer = "hexalith-dev";
    internal const string Audience = "hexalith-parties";
    internal const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

    internal static string CreateToken(bool includeTenantClaim)
        => includeTenantClaim ? CreateToken("tenant-a") : CreateTokenCore(null, "test-user");

    internal static string CreateToken(string tenantId)
        => CreateTokenCore(tenantId, "test-user");

    internal static string CreateToken(string tenantId, string userId)
        => CreateTokenCore(tenantId, userId);

    internal static string CreateTokenWithoutSub(string tenantId)
        => CreateTokenCore(tenantId, subject: null);

    private static string CreateTokenCore(string? tenantId, string? subject)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>();

        if (subject is not null)
        {
            claims.Add(new Claim("sub", subject));
        }

        if (tenantId is not null)
        {
            claims.Add(new Claim("eventstore:tenant", tenantId));
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
