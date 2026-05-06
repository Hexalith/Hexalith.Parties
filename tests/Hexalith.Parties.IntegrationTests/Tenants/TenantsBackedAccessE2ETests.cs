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
            return;
        }

        await _fixture.SeedTenantAsync("tenant-a", "user-1", TenantRole.TenantContributor);

        string token = TenantIntegrationTestSeeder.CreateToken("tenant-a", "user-1");

        HttpResponseMessage response = await PollAsync(
            () => SendAsync(HttpMethod.Get, "/api/v1/parties", token),
            until: r => r.StatusCode == HttpStatusCode.OK);

        try
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Aspire_GivenTenantDisabledViaTenantsApi_RestPartiesReturns403Async()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await _fixture.SeedTenantAsync("tenant-d", "user-1", TenantRole.TenantOwner);
        await _fixture.DisableTenantAsync("tenant-d");

        string token = TenantIntegrationTestSeeder.CreateToken("tenant-d", "user-1");

        HttpResponseMessage response = await PollAsync(
            () => SendAsync(HttpMethod.Get, "/api/v1/parties", token),
            until: r => r.StatusCode == HttpStatusCode.Forbidden);

        try
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
        finally
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Aspire_GivenUserRemovedViaTenantsApi_RestPartiesReturns403Async()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await _fixture.SeedTenantAsync("tenant-r", "user-1", TenantRole.TenantContributor);
        await _fixture.RemoveUserFromTenantAsync("tenant-r", "user-1");

        string token = TenantIntegrationTestSeeder.CreateToken("tenant-r", "user-1");

        HttpResponseMessage response = await PollAsync(
            () => SendAsync(HttpMethod.Get, "/api/v1/parties", token),
            until: r => r.StatusCode == HttpStatusCode.Forbidden);

        try
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            await AssertReasonCodeAsync(response, expectedReasonCode: "not-member");
        }
        finally
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Aspire_GivenValidJwtTenantClaimWithoutMembership_RestPartiesReturns403NotMemberAsync()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        // Active tenant exists with an unrelated owner; the calling user has never been added.
        await _fixture.SeedTenantAsync("tenant-nm", "owner-only", TenantRole.TenantOwner);

        string token = TenantIntegrationTestSeeder.CreateToken("tenant-nm", "user-without-membership");

        HttpResponseMessage response = await PollAsync(
            () => SendAsync(HttpMethod.Get, "/api/v1/parties", token),
            until: r => r.StatusCode == HttpStatusCode.Forbidden);

        try
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            await AssertReasonCodeAsync(response, expectedReasonCode: "not-member");
        }
        finally
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Aspire_GivenReaderRoleOnWriteEndpoint_RestPartiesReturns403InsufficientRoleAsync()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await _fixture.SeedTenantAsync("tenant-ir", "reader-user", TenantRole.TenantReader);

        string token = TenantIntegrationTestSeeder.CreateToken("tenant-ir", "reader-user");
        const string writePayload = """{"name":{"display":"Should Not Be Created"}}""";

        HttpResponseMessage response = await PollAsync(
            () => SendAsync(HttpMethod.Post, "/api/v1/parties", token, writePayload),
            until: r => r.StatusCode == HttpStatusCode.Forbidden);

        try
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            await AssertReasonCodeAsync(response, expectedReasonCode: "insufficient-role");
        }
        finally
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Aspire_GivenTwoTenants_TenantACannotEnumerateOrFetchTenantBPartiesAsync()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await _fixture.SeedTenantAsync("tenant-iso-a", "user-a", TenantRole.TenantContributor);
        await _fixture.SeedTenantAsync("tenant-iso-b", "user-b", TenantRole.TenantOwner);

        string tokenB = TenantIntegrationTestSeeder.CreateToken("tenant-iso-b", "user-b");
        const string tenantBPayload = """{"name":{"display":"Tenant B Only"}}""";

        HttpResponseMessage createResponse = await PollAsync(
            () => SendAsync(HttpMethod.Post, "/api/v1/parties", tokenB, tenantBPayload),
            until: r => r.StatusCode == HttpStatusCode.Accepted
                || r.StatusCode == HttpStatusCode.Created
                || r.StatusCode == HttpStatusCode.OK);

        try
        {
            createResponse.IsSuccessStatusCode.ShouldBeTrue(
                $"Tenant B create should succeed; got {(int)createResponse.StatusCode} {createResponse.StatusCode}");
        }
        finally
        {
            createResponse.Dispose();
        }

        string tokenA = TenantIntegrationTestSeeder.CreateToken("tenant-iso-a", "user-a");
        HttpResponseMessage listResponse = await PollAsync(
            () => SendAsync(HttpMethod.Get, "/api/v1/parties", tokenA),
            until: r => r.StatusCode == HttpStatusCode.OK);

        try
        {
            listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            string body = await listResponse.Content.ReadAsStringAsync();

            body.ShouldNotContain("tenant-iso-b");
            body.ShouldNotContain("Tenant B Only");
        }
        finally
        {
            listResponse.Dispose();
        }
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string requestUri,
        string token,
        string? jsonBody = null)
    {
        // Per-request HttpRequestMessage avoids mutating the shared fixture HttpClient's
        // DefaultRequestHeaders, which would race across parallel tests.
        HttpRequestMessage request = new(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
        }

        return _fixture.CommandApiClient.SendAsync(request);
    }

    private static async Task AssertReasonCodeAsync(HttpResponseMessage response, string expectedReasonCode)
    {
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using JsonDocument payload = JsonDocument.Parse(body);
        if (!payload.RootElement.TryGetProperty("reasonCode", out JsonElement reasonElement))
        {
            throw new InvalidOperationException(
                $"Response did not contain 'reasonCode'. Status: {(int)response.StatusCode}. Body: {body}");
        }

        reasonElement.GetString().ShouldBe(expectedReasonCode);
    }

    /// <summary>
    /// Eventual-consistency poll — required because Story 11.2's local Tenants projection
    /// converges asynchronously after Tenants commands. Throws TimeoutException with the
    /// last observed status when the until-condition is never satisfied.
    /// </summary>
    private static async Task<HttpResponseMessage> PollAsync(
        Func<Task<HttpResponseMessage>> action,
        Func<HttpResponseMessage, bool> until,
        int attempts = 20,
        int delayMs = 250,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage last = await action().ConfigureAwait(false);
        if (until(last))
        {
            return last;
        }

        for (int i = 0; i < attempts - 1; i++)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            last.Dispose();
            last = await action().ConfigureAwait(false);
            if (until(last))
            {
                return last;
            }
        }

        HttpStatusCode lastStatus = last.StatusCode;
        last.Dispose();
        throw new TimeoutException(
            $"Eventual-consistency poll exhausted {attempts} attempts (delay {delayMs}ms). Last status: {(int)lastStatus} {lastStatus}.");
    }
}
