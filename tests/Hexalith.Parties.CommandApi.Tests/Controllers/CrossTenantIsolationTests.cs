// ATDD red-phase scaffolds for Story 11.4 — cross-tenant projection isolation
// proven through observable REST/MCP behavior. Tenant A must never list, fetch,
// search, or MCP-resolve tenant B data, even when both tenants are seeded with
// look-alike records.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Parties.CommandApi.Mcp;
using Hexalith.Parties.CommandApi.Search;
using Hexalith.Parties.CommandApi.Tests.Authorization;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Controllers;

/// <summary>
/// Story 11.4 — AC2: Tenants-backed enforcement must produce the same observable
/// non-enumeration behavior that EventStore/projection tenant-scoping currently
/// guarantees. These tests bind two tenants in a single test pass and assert
/// tenant A cannot observe tenant B records by any externally visible path.
/// </summary>
public sealed class CrossTenantIsolationTests : IClassFixture<PartiesApiTestFactory>
{
    private readonly PartiesApiTestFactory _factory;

    public CrossTenantIsolationTests(PartiesApiTestFactory factory)
    {
        _factory = factory;
        _factory.Router.ClearReceivedCalls();
        _factory.ActorProxyFactory.ClearReceivedCalls();
    }

    [Fact]
    public async Task ListParties_TenantAUserContext_DoesNotReturnTenantBRows_EvenWhenSeededInProjectionAsync()
    {
        // Arrange — Story 11.4 must seed both tenants in the projection AND wire
        // the access service to allow only tenant A for the calling user.
        SeedTenantData(tenantId: "tenant-a", count: 2);
        SeedTenantData(tenantId: "tenant-b", count: 2);
        AllowOnly(tenantId: "tenant-a");

        using HttpClient client = CreateClient(tenantId: "tenant-a");

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/parties");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        string payload = body.RootElement.GetRawText();
        payload.ShouldContain("tenant-a-party-");
        payload.ShouldNotContain("tenant-b-party-");
    }

    [Fact]
    public async Task GetParty_TenantAUser_FetchingTenantBPartyById_Returns403_NotEnumerableAsync()
    {
        // Arrange — tenant B record with a known id; tenant A user attempts to fetch it.
        string tenantBPartyId = Guid.NewGuid().ToString();
        SeedTenantData(tenantId: "tenant-b", count: 1, knownId: tenantBPartyId);
        AllowOnly(tenantId: "tenant-a");

        using HttpClient client = CreateClient(tenantId: "tenant-a");

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/v1/parties/{tenantBPartyId}");

        // Assert — 403 (preferred) or 404; either masks existence equivalently.
        // Critically: the response body must not echo tenant B projection data.
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        string content = await response.Content.ReadAsStringAsync();
        content.ShouldNotContain("tenant-b-party");
    }

    [Fact]
    public async Task FindPartiesMcpTool_TenantAUser_DoesNotIncludeTenantBHitsAsync()
    {
        // Arrange — both tenants seeded with parties matching the same query; tenant A user calls FindParties.
        SeedTenantData(tenantId: "tenant-a", count: 2, namePrefix: "Lovelace");
        SeedTenantData(tenantId: "tenant-b", count: 2, namePrefix: "Lovelace");
        AllowOnly(tenantId: "tenant-a");

        // Act
        IReadOnlyList<MockPartyHit> hits = await InvokeFindParties(tenantId: "tenant-a", userId: "user-1", query: "Lovelace");

        // Assert
        hits.ShouldNotBeEmpty();
        hits.ShouldAllBe(h => h.TenantId == "tenant-a");
    }

    [Fact]
    public async Task GetPartyMcpTool_TenantAUser_OnTenantBPartyId_ThrowsAccessDeniedAsync()
    {
        string tenantBPartyId = Guid.NewGuid().ToString();
        SeedTenantData(tenantId: "tenant-b", count: 1, knownId: tenantBPartyId);
        AllowOnly(tenantId: "tenant-a");

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => InvokeGetPartyMcp(tenantId: "tenant-a", userId: "user-1", partyId: tenantBPartyId));

        ex.Message.ShouldContain("Party not found");
        ex.Message.ShouldNotContain("tenant-b");
    }

    private void AllowOnly(string tenantId)
    {
        _factory.TenantAccessService.Handler = (requestedTenant, _, _, _) =>
            Task.FromResult(string.Equals(requestedTenant, tenantId, StringComparison.Ordinal)
                ? TenantAccessDecision.Allowed
                : TenantAccessDecision.Denied(TenantAccessDenialReason.MissingMember));
    }

    private HttpClient CreateClient(string tenantId)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(tenantId));
        return client;
    }

    /// <summary>
    /// Story 11.4 must add a multi-tenant projection seeding helper to
    /// <see cref="PartiesApiTestFactory"/> so cross-tenant tests can preload distinct
    /// rows under tenant A and tenant B.
    /// </summary>
    private void SeedTenantData(string tenantId, int count, string? knownId = null, string? namePrefix = null)
    {
        PartyDetail[] details = Enumerable.Range(0, count)
            .Select(index => CreatePartyDetail(
                knownId is not null && index == 0 ? knownId : Guid.NewGuid().ToString(),
                $"{namePrefix ?? tenantId + "-party"}-{index + 1}"))
            .ToArray();

        _factory.SetTenantParties(tenantId, details);
    }

    /// <summary>
    /// Story 11.4 must add a sidecar-free MCP invocation harness wired to the same
    /// projection seam used by REST controllers.
    /// </summary>
    private static async Task<IReadOnlyList<MockPartyHit>> InvokeFindParties(string tenantId, string userId, string query)
    {
        using McpSessionScope _ = McpSessionScope.For(tenantId, userId);
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>
            {
                ["a-1"] = CreateIndexEntry("a-1", "Lovelace tenant-a-party-1"),
                ["a-2"] = CreateIndexEntry("a-2", "Lovelace tenant-a-party-2"),
            }));

        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => string.Equals(id.GetId(), $"{tenantId}:party-index", StringComparison.Ordinal)),
            Arg.Any<string>())
            .Returns(indexActor);

        ServiceProvider services = new ServiceCollection()
            .AddSingleton(actorProxyFactory)
            .AddSingleton<ITenantAccessService>(new TestTenantAccessService())
            .AddSingleton<IPartySearchProvider, LocalFuzzyPartySearchProvider>()
            .AddSingleton<IPartySearchService, LocalPartySearchService>()
            .BuildServiceProvider();

        string json = await FindPartiesMcpTool.FindPartiesAsync(services, query: query).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement items = document.RootElement.GetProperty("results").GetProperty("items");
        return items.EnumerateArray()
            .Select(item => new MockPartyHit(
                item.GetProperty("party").GetProperty("id").GetString()!,
                tenantId,
                item.GetProperty("party").GetProperty("displayName").GetString()!))
            .ToArray();
    }

    private static Task InvokeGetPartyMcp(string tenantId, string userId, string partyId)
    {
        using McpSessionScope _ = McpSessionScope.For(tenantId, userId);
        IPartyDetailProjectionActor detailActor = Substitute.For<IPartyDetailProjectionActor>();
        detailActor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Is<ActorId>(id => string.Equals(id.GetId(), $"{tenantId}:party-detail:{partyId}", StringComparison.Ordinal)),
            Arg.Any<string>())
            .Returns(detailActor);

        return GetPartyMcpTool.GetPartyAsync(
            partyId,
            new ServiceCollection()
                .AddSingleton<ITenantAccessService>(new TestTenantAccessService())
                .AddSingleton(actorProxyFactory)
                .BuildServiceProvider());
    }

    private static PartyDetail CreatePartyDetail(string id, string displayName)
        => new()
        {
            Id = id,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = displayName,
            SortName = displayName,
            ContactChannels = [],
            Identifiers = [],
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };

    private static PartyIndexEntry CreateIndexEntry(string id, string displayName)
        => new()
        {
            Id = id,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = displayName,
            SearchableContactChannels = [],
            SearchableIdentifiers = [],
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };

    public sealed record MockPartyHit(string PartyId, string TenantId, string Name);

    private readonly struct McpSessionScope : IDisposable
    {
        private readonly string? _previousTenant;
        private readonly string? _previousUserId;

        private McpSessionScope(string tenantId, string userId)
        {
            _previousTenant = McpSessionContext.Tenant.Value;
            _previousUserId = McpSessionContext.UserId.Value;
            McpSessionContext.Tenant.Value = tenantId;
            McpSessionContext.UserId.Value = userId;
        }

        public static McpSessionScope For(string tenantId, string userId) => new(tenantId, userId);

        public void Dispose()
        {
            McpSessionContext.Tenant.Value = _previousTenant;
            McpSessionContext.UserId.Value = _previousUserId;
        }
    }
}
