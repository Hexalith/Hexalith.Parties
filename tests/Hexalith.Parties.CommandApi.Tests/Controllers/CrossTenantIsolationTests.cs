// ATDD red-phase scaffolds for Story 11.4 — cross-tenant projection isolation
// proven through observable REST/MCP behavior. Tenant A must never list, fetch,
// search, or MCP-resolve tenant B data, even when both tenants are seeded with
// look-alike records.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Parties.CommandApi.Tests.Authorization;

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
    private const string SkipReason =
        "TDD red phase — Story 11.4 must add a multi-tenant projection seed in " +
        "PartiesApiTestFactory and wire ITenantAccessService so tenant A's caller " +
        "cannot list/get/search tenant B records via REST or MCP.";

    private readonly PartiesApiTestFactory _factory;

    public CrossTenantIsolationTests(PartiesApiTestFactory factory)
    {
        _factory = factory;
        _factory.Router.ClearReceivedCalls();
        _factory.ActorProxyFactory.ClearReceivedCalls();
    }

    [Fact(Skip = SkipReason)]
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

        foreach (JsonElement entry in body.RootElement.GetProperty("items").EnumerateArray())
        {
            entry.GetProperty("tenantId").GetString().ShouldBe("tenant-a");
        }
    }

    [Fact(Skip = SkipReason)]
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
        // Critically: the response body must not echo tenant B identifiers/PII.
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        string content = await response.Content.ReadAsStringAsync();
        content.ShouldNotContain(tenantBPartyId);
    }

    [Fact(Skip = SkipReason)]
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

    [Fact(Skip = SkipReason)]
    public async Task GetPartyMcpTool_TenantAUser_OnTenantBPartyId_ThrowsAccessDeniedAsync()
    {
        string tenantBPartyId = Guid.NewGuid().ToString();
        SeedTenantData(tenantId: "tenant-b", count: 1, knownId: tenantBPartyId);
        AllowOnly(tenantId: "tenant-a");

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => InvokeGetPartyMcp(tenantId: "tenant-a", userId: "user-1", partyId: tenantBPartyId));

        // Diagnostic should not include tenant B identifiers.
        ex.Message.ShouldNotContain(tenantBPartyId);
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
        => throw new NotImplementedException(
            "Story 11.4: extend PartiesApiTestFactory with multi-tenant projection seeding.");

    /// <summary>
    /// Story 11.4 must add a sidecar-free MCP invocation harness wired to the same
    /// projection seam used by REST controllers.
    /// </summary>
    private static Task<IReadOnlyList<MockPartyHit>> InvokeFindParties(string tenantId, string userId, string query)
        => throw new NotImplementedException(
            "Story 11.4: add MCP FindParties invocation harness for cross-tenant assertions.");

    private static Task InvokeGetPartyMcp(string tenantId, string userId, string partyId)
        => throw new NotImplementedException(
            "Story 11.4: add MCP GetParty invocation harness for cross-tenant assertions.");

    public sealed record MockPartyHit(string PartyId, string TenantId, string Name);
}
