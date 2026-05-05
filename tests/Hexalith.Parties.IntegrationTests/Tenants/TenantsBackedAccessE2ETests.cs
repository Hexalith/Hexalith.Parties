// ATDD red-phase scaffolds for Story 11.4 — Tier 3 Aspire-topology proof that
// Tenants-backed authorization works end-to-end through the real CommandApi.
// These tests skip gracefully when Aspire/DAPR/Docker is unavailable, matching
// the existing PartiesAspireTopologyFixture pattern.

using System.Net;
using System.Net.Http.Headers;

using Hexalith.Parties.IntegrationTests.HealthChecks;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Tenants;

/// <summary>
/// Story 11.4 — AC2: full-topology Tenants integration tests. These add confidence
/// beyond the sidecar-free CommandApi tests by seeding tenant state through
/// Hexalith.Tenants APIs and exercising the real REST/MCP surface.
/// </summary>
[Collection("PartiesAspireTopology")]
public sealed class TenantsBackedAccessE2ETests
{
    private const string SkipReason =
        "TDD red phase — Story 11.4 must add a Tenants-seeding helper that drives the " +
        "active topology's Tenants service before the test runs, so REST/MCP requests " +
        "can be authorized through the real local projection.";

    private readonly PartiesAspireTopologyFixture _fixture;

    public TenantsBackedAccessE2ETests(PartiesAspireTopologyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Skip = SkipReason)]
    public async Task Aspire_GivenTenantsSeededActiveTenant_RestPartiesAccessIsAuthorizedAsync()
    {
        if (!_fixture.IsAvailable)
        {
            // Mirror existing fixture skip pattern — don't invent infrastructure failures.
            return;
        }

        // Arrange — seed an active tenant with a contributor user via Hexalith.Tenants APIs.
        await SeedTenantAsync(tenantId: "tenant-a", userId: "user-1", role: "TenantContributor");

        HttpClient client = _fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await IssueTokenAsync(tenantId: "tenant-a", userId: "user-1"));

        // Act — round-trip a write then read.
        HttpResponseMessage create = await client.PostAsync("/api/v1/parties", BuildCreatePartyContent());
        create.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.Accepted, HttpStatusCode.OK);

        // Eventual consistency: Story 11.2 + 11.3 + 11.4 must converge so the read succeeds.
        HttpResponseMessage read = await PollAsync(
            () => client.GetAsync("/api/v1/parties"),
            until: r => r.StatusCode == HttpStatusCode.OK);

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact(Skip = SkipReason)]
    public async Task Aspire_GivenTenantDisabledViaTenantsApi_RestPartiesReturns403Async()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange — create then disable the tenant via Hexalith.Tenants commands.
        await SeedTenantAsync(tenantId: "tenant-d", userId: "user-1", role: "TenantOwner");
        await DisableTenantAsync(tenantId: "tenant-d");

        HttpClient client = _fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await IssueTokenAsync(tenantId: "tenant-d", userId: "user-1"));

        // Wait for projection to converge on disabled state.
        HttpResponseMessage response = await PollAsync(
            () => client.GetAsync("/api/v1/parties"),
            until: r => r.StatusCode == HttpStatusCode.Forbidden);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact(Skip = SkipReason)]
    public async Task Aspire_GivenUserRemovedViaTenantsApi_McpToolThrowsForbiddenAsync()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange — add then remove the user via Hexalith.Tenants commands.
        await SeedTenantAsync(tenantId: "tenant-r", userId: "user-1", role: "TenantContributor");
        await RemoveUserFromTenantAsync(tenantId: "tenant-r", userId: "user-1");

        // Act / Assert — MCP write must fail with the appropriate denial.
        Exception ex = await Should.ThrowAsync<Exception>(
            () => InvokeCreatePartyMcpAsync(tenantId: "tenant-r", userId: "user-1"));

        ex.Message.ShouldContain("not-member");
    }

    /// <summary>
    /// Story 11.4 must add a Hexalith.Tenants seeding helper that issues real Tenants
    /// commands (CreateTenant + AddUserToTenant) against the running topology and
    /// waits for the local projection to converge before the test continues.
    /// </summary>
    private static Task SeedTenantAsync(string tenantId, string userId, string role)
        => throw new NotImplementedException(
            "Story 11.4: add Tenants topology seeding helper using Hexalith.Tenants client APIs.");

    private static Task DisableTenantAsync(string tenantId)
        => throw new NotImplementedException(
            "Story 11.4: add DisableTenant helper using Hexalith.Tenants client APIs.");

    private static Task RemoveUserFromTenantAsync(string tenantId, string userId)
        => throw new NotImplementedException(
            "Story 11.4: add RemoveUserFromTenant helper using Hexalith.Tenants client APIs.");

    private static Task<string> IssueTokenAsync(string tenantId, string userId)
        => throw new NotImplementedException(
            "Story 11.4: add JWT issuing helper aligned with the Aspire topology's symmetric-key configuration.");

    private static HttpContent BuildCreatePartyContent()
        => throw new NotImplementedException("Story 11.4: build a representative create-party request body.");

    private static Task InvokeCreatePartyMcpAsync(string tenantId, string userId)
        => throw new NotImplementedException("Story 11.4: add MCP CreateParty E2E invocation helper.");

    /// <summary>
    /// Eventual-consistency poll — required because Story 11.2's local Tenants projection
    /// converges asynchronously after Tenants commands.
    /// </summary>
    private static async Task<HttpResponseMessage> PollAsync(
        Func<Task<HttpResponseMessage>> action,
        Func<HttpResponseMessage, bool> until,
        int attempts = 20,
        int delayMs = 250)
    {
        HttpResponseMessage last = await action().ConfigureAwait(false);
        for (int i = 0; i < attempts && !until(last); i++)
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            last.Dispose();
            last = await action().ConfigureAwait(false);
        }

        return last;
    }
}
