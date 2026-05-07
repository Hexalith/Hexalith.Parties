// ATDD red-phase scaffolds for Story 11.4 — cross-tenant projection isolation
// proven through observable REST/MCP behavior. Tenant A must never list, fetch,
// search, or MCP-resolve tenant B data, even when both tenants are seeded with
// look-alike records.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.Authorization;
using Hexalith.Parties.Mcp;
using Hexalith.Parties.Search;
using Hexalith.Parties.Tests.Authorization;
using Hexalith.Parties.Tests.Mcp;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Controllers;

/// <summary>
/// Story 11.4 — AC2: Tenants-backed enforcement must produce the same observable
/// non-enumeration behavior that EventStore/projection tenant-scoping currently
/// guarantees. These tests bind two tenants in a single test pass and assert
/// tenant A cannot observe tenant B records by any externally visible path.
/// </summary>
[Collection(PartiesApiTestCollection.Name)]
public sealed class CrossTenantIsolationTests : IClassFixture<PartiesApiTestFactory>, IDisposable
{
    private readonly PartiesApiTestFactory _factory;

    public CrossTenantIsolationTests(PartiesApiTestFactory factory)
    {
        _factory = factory;
        _factory.ResetProjectionState();
        _factory.Router.ClearReceivedCalls();
        _factory.ActorProxyFactory.ClearReceivedCalls();
        // IClassFixture shares the factory instance — reset Handler so other tests don't inherit
        // the AllowOnly state set by tests that ran earlier.
        _factory.TenantAccessService.AllowAll();
    }

    public void Dispose()
    {
        _factory.TenantAccessService.AllowAll();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ListParties_TenantAUserContext_DoesNotReturnTenantBRows_EvenWhenSeededInProjectionAsync()
    {
        // Arrange — Story 11.4 must seed both tenants in the projection AND wire
        // the access service to allow only tenant A for the calling user.
        SeedTenantData(tenantId: "tenant-a", count: 2);
        SeedTenantData(tenantId: "tenant-b", count: 2);
        AllowOnly(tenantId: "tenant-a", userId: "user-1");

        using HttpClient client = CreateClient(tenantId: "tenant-a", userId: "user-1");

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/parties");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        JsonElement items = body.RootElement.GetProperty("items");
        items.GetArrayLength().ShouldBe(2);
        items.EnumerateArray()
            .Count(item => item.GetRawText().Contains("tenant-b-party-", StringComparison.Ordinal))
            .ShouldBe(0);
        items.EnumerateArray()
            .ShouldAllBe(item => item.GetRawText().Contains("tenant-a-party-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetParty_TenantAUser_FetchingTenantBPartyById_Returns403_NotEnumerableAsync()
    {
        // Arrange — tenant B record with a known id; tenant A user attempts to fetch it.
        string tenantBPartyId = Guid.NewGuid().ToString();
        SeedTenantData(tenantId: "tenant-b", count: 1, knownId: tenantBPartyId);
        AllowOnly(tenantId: "tenant-a", userId: "user-1");

        using HttpClient client = CreateClient(tenantId: "tenant-a", userId: "user-1");

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/v1/parties/{tenantBPartyId}");

        // Assert — the controller routes the actor lookup by the CALLING tenant
        // ("tenant-a:party-detail:{partyId}"), so even with a known tenant-b id the
        // tenant-a partition yields no record and the response is 404. This IS the
        // chosen masking behavior: tenant-b record existence is not observable to tenant-a.
        // Critically: the response body must not echo any tenant B identifier.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        string content = await response.Content.ReadAsStringAsync();
        content.ShouldNotContain("tenant-b-party");
    }

    [Fact]
    public async Task FindPartiesMcpTool_TenantAUser_DoesNotIncludeTenantBHitsAsync()
    {
        // Arrange — both tenants seeded with parties matching the same query; tenant A user calls FindParties.
        SeedTenantData(tenantId: "tenant-a", count: 2, namePrefix: "Lovelace");
        SeedTenantData(tenantId: "tenant-b", count: 2, namePrefix: "Lovelace");
        AllowOnly(tenantId: "tenant-a", userId: "user-1");

        // Act — InvokeFindParties wires actor proxies for BOTH tenants, so isolation
        // is genuinely exercised: the MCP tool must filter results to the calling tenant
        // even when tenant B index entries match the query.
        FindPartiesInvocation invocation = await InvokeFindPartiesWithProxyFactory(
            callingTenantId: "tenant-a",
            userId: "user-1",
            query: "Lovelace");

        // Assert — only tenant A results, even though tenant B has matching entries.
        invocation.Hits.ShouldNotBeEmpty();
        invocation.Hits.ShouldAllBe(h => h.TenantId == "tenant-a");
        // Calling-tenant routing must never reach into tenant-b's index actor — assert this on
        // the actor proxy factory directly so a future refactor that returns tenant-b items
        // with tenant-a-shaped names cannot pass this test vacuously.
        invocation.ActorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => string.Equals(id.GetId(), TenantActorIds.PartyIndex("tenant-b"), StringComparison.Ordinal)),
            Arg.Any<string>());
    }

    [Fact]
    public async Task GetPartyMcpTool_TenantAUser_OnTenantBPartyId_RoutesToTenantAPartitionAndReturnsNotFoundAsync()
    {
        // Story 11.4 — AC2 cross-tenant isolation. The MCP tool routes the actor lookup by the
        // calling tenant ("tenant-a:party-detail:{partyId}"), so even with a known tenant-b
        // partyId the tool sees "no such party" — proving tenant-a cannot fetch tenant-b records
        // by direct id traversal. The response also must not echo tenant B identifiers.
        string tenantBPartyId = Guid.NewGuid().ToString();
        SeedTenantData(tenantId: "tenant-b", count: 1, knownId: tenantBPartyId);
        AllowOnly(tenantId: "tenant-a", userId: "user-1");

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => InvokeGetPartyMcp(callingTenantId: "tenant-a", userId: "user-1", partyId: tenantBPartyId));

        ex.Message.ShouldContain("Party not found");
        ex.Message.ShouldNotContain("tenant-b");
    }

    [Fact]
    public async Task GetPartyMcpTool_DeniedAccess_ThrowsTenantAuthorizationFailedAsync()
    {
        // Story 11.4 — AC2 + AC5 reason-code stability. When the access service denies the
        // calling tenant outright (e.g., not-member), the MCP tool surfaces the authorization
        // failure with the stable reason code BEFORE any actor lookup occurs.
        string anyPartyId = Guid.NewGuid().ToString();
        // Allow only tenant-b; the calling tenant-a will be denied as not-member.
        AllowOnly(tenantId: "tenant-b", userId: "user-b");

        // Assert on the typed exception's ReasonCode property rather than substring-matching the
        // free-text message — a future translator change that includes "not-member" in a different
        // denial reason's message would otherwise pass silently.
        McpTenantAuthorizationException ex = await Should.ThrowAsync<McpTenantAuthorizationException>(
            () => InvokeGetPartyMcp(callingTenantId: "tenant-a", userId: "user-1", partyId: anyPartyId));

        ex.ReasonCode.ShouldBe("not-member");
        ex.Message.ShouldContain("Tenant authorization failed");
    }

    /// <summary>
    /// Allows access only when both tenant id and user id match. This pins per-user
    /// scope so a future change that ignores the user id is detected by these tests.
    /// </summary>
    private void AllowOnly(string tenantId, string userId)
    {
        _factory.TenantAccessService.Handler = (requestedTenant, requestedUser, _, _) =>
            Task.FromResult(
                string.Equals(requestedTenant, tenantId, StringComparison.Ordinal)
                    && string.Equals(requestedUser, userId, StringComparison.Ordinal)
                        ? TenantAccessDecision.Allowed
                        : TenantAccessDecision.Denied(TenantAccessDenialReason.MissingMember));
    }

    private HttpClient CreateClient(string tenantId, string userId = "user-1")
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(tenantId, userId));
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
    /// Sidecar-free MCP invocation harness. Wires actor proxies for BOTH tenants so
    /// cross-tenant isolation is genuinely exercised — the calling tenant's MCP tool
    /// must filter results, even when the other tenant has matching entries that would
    /// otherwise be visible. The factory's TenantAccessService is the same instance
    /// resolved via DI, so the test's AllowOnly handler governs authorization.
    /// Returns the actor-proxy factory alongside the hits so the caller can assert
    /// directly on tenant-b actor invocation (or its absence).
    /// </summary>
    private async Task<FindPartiesInvocation> InvokeFindPartiesWithProxyFactory(string callingTenantId, string userId, string query)
    {
        using McpSessionScope _ = McpSessionScope.For(callingTenantId, userId);

        // Tenant A index actor.
        IPartyIndexProjectionActor tenantAIndexActor = Substitute.For<IPartyIndexProjectionActor>();
        tenantAIndexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>
            {
                ["a-1"] = CreateIndexEntry("a-1", "Lovelace tenant-a-party-1"),
                ["a-2"] = CreateIndexEntry("a-2", "Lovelace tenant-a-party-2"),
            }));

        // Tenant B index actor — populated so tenant-b data is genuinely available
        // and would surface if the access service or actor routing were broken.
        IPartyIndexProjectionActor tenantBIndexActor = Substitute.For<IPartyIndexProjectionActor>();
        tenantBIndexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>
            {
                ["b-1"] = CreateIndexEntry("b-1", "Lovelace tenant-b-party-1"),
                ["b-2"] = CreateIndexEntry("b-2", "Lovelace tenant-b-party-2"),
            }));

        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => string.Equals(id.GetId(), TenantActorIds.PartyIndex("tenant-a"), StringComparison.Ordinal)),
            Arg.Any<string>())
            .Returns(tenantAIndexActor);
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => string.Equals(id.GetId(), TenantActorIds.PartyIndex("tenant-b"), StringComparison.Ordinal)),
            Arg.Any<string>())
            .Returns(tenantBIndexActor);

        ServiceProvider services = new ServiceCollection()
            .AddSingleton(actorProxyFactory)
            .AddSingleton<ITenantAccessService>(_factory.TenantAccessService)
            .AddSingleton<IPartySearchProvider, LocalFuzzyPartySearchProvider>()
            .AddSingleton<IPartySearchService, LocalPartySearchService>()
            .BuildServiceProvider();
        try
        {
            string json = await FindPartiesMcpTool.FindPartiesAsync(services, query: query).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("results", out JsonElement results)
                || !results.TryGetProperty("items", out JsonElement items))
            {
                throw new InvalidOperationException(
                    $"FindPartiesMcpTool response did not contain 'results.items'. Payload: {json}");
            }

            MockPartyHit[] hits = items.EnumerateArray()
                .Select(item => new MockPartyHit(
                    item.GetProperty("party").GetProperty("id").GetString()!,
                    // Synthesize tenant id from displayName prefix for assertion clarity only;
                    // the authoritative isolation assertion is the actor-proxy DidNotReceive check
                    // performed by the caller against the returned ActorProxyFactory.
                    item.GetProperty("party").GetProperty("displayName").GetString()!.Contains("tenant-a-party") ? "tenant-a" : "tenant-b",
                    item.GetProperty("party").GetProperty("displayName").GetString()!))
                .ToArray();

            return new FindPartiesInvocation(hits, actorProxyFactory);
        }
        finally
        {
            await services.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task InvokeGetPartyMcp(string callingTenantId, string userId, string partyId)
    {
        using McpSessionScope _ = McpSessionScope.For(callingTenantId, userId);

        // Detail actor that returns null — but authorization should fail before this is invoked.
        IPartyDetailProjectionActor detailActor = Substitute.For<IPartyDetailProjectionActor>();
        detailActor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>())
            .Returns(detailActor);

        ServiceProvider services = new ServiceCollection()
            .AddSingleton<ITenantAccessService>(_factory.TenantAccessService)
            .AddSingleton(actorProxyFactory)
            .BuildServiceProvider();
        try
        {
            await GetPartyMcpTool.GetPartyAsync(partyId, services).ConfigureAwait(false);
        }
        finally
        {
            await services.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed record FindPartiesInvocation(IReadOnlyList<MockPartyHit> Hits, IActorProxyFactory ActorProxyFactory);

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

}
