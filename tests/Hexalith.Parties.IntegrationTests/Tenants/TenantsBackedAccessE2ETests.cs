using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Hexalith.Tenants.Contracts.Enums;

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
    private readonly PartiesAspireTopologyFixture _fixture;

    public TenantsBackedAccessE2ETests(PartiesAspireTopologyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Aspire_GivenTenantsSeededActiveTenant_RestPartiesAccessIsAuthorizedAsync()
    {
        if (!_fixture.IsAvailable)
        {
            // Mirror existing fixture skip pattern — don't invent infrastructure failures.
            return;
        }

        // Arrange — seed an active tenant with a contributor user via Hexalith.Tenants APIs.
        await _fixture.SeedTenantAsync("tenant-a", "user-1", TenantRole.TenantContributor);

        HttpClient client = _fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TenantIntegrationTestSeeder.CreateToken("tenant-a", "user-1"));

        // Act — authorized read proves the real CommandApi consumed the Tenants authority event stream.
        HttpResponseMessage response = await PollAsync(
            () => client.GetAsync("/api/v1/parties"),
            until: r => r.StatusCode == HttpStatusCode.OK);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Aspire_GivenTenantDisabledViaTenantsApi_RestPartiesReturns403Async()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange — create then disable the tenant via Hexalith.Tenants commands.
        await _fixture.SeedTenantAsync("tenant-d", "user-1", TenantRole.TenantOwner);
        await _fixture.DisableTenantAsync("tenant-d");

        HttpClient client = _fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TenantIntegrationTestSeeder.CreateToken("tenant-d", "user-1"));

        // Wait for projection to converge on disabled state.
        HttpResponseMessage response = await PollAsync(
            () => client.GetAsync("/api/v1/parties"),
            until: r => r.StatusCode == HttpStatusCode.Forbidden);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Aspire_GivenUserRemovedViaTenantsApi_RestPartiesReturns403Async()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Arrange — add then remove the user via Hexalith.Tenants commands.
        await _fixture.SeedTenantAsync("tenant-r", "user-1", TenantRole.TenantContributor);
        await _fixture.RemoveUserFromTenantAsync("tenant-r", "user-1");

        HttpClient client = _fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TenantIntegrationTestSeeder.CreateToken("tenant-r", "user-1"));

        HttpResponseMessage response = await PollAsync(
            () => client.GetAsync("/api/v1/parties"),
            until: r => r.StatusCode == HttpStatusCode.Forbidden);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("reasonCode").GetString().ShouldBe("not-member");
    }

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
