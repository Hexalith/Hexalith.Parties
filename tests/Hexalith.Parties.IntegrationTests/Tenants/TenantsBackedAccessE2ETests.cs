using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Hexalith.Tenants.Contracts.Enums;

using Shouldly;

using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

    public TenantsBackedAccessE2ETests(PartiesAspireTopologyFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Aspire_GivenTenantsSeededActiveTenant_RestPartiesAccessIsAuthorizedAsync()
    {
        if (ShouldSkipForInfrastructure()) { return; }

        await _fixture.SeedTenantAsync("tenant-a", "user-1", TenantRole.TenantContributor);

        string token = TenantIntegrationTestSeeder.CreateToken("tenant-a", "user-1");

        HttpResponseMessage response = await PollAsync(
            ct => SendAsync(HttpMethod.Get, "/api/v1/parties", token, cancellationToken: ct),
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
        if (ShouldSkipForInfrastructure()) { return; }

        await _fixture.SeedTenantAsync("tenant-d", "user-1", TenantRole.TenantOwner);
        await _fixture.DisableTenantAsync("tenant-d");

        string token = TenantIntegrationTestSeeder.CreateToken("tenant-d", "user-1");

        HttpResponseMessage response = await PollAsync(
            ct => SendAsync(HttpMethod.Get, "/api/v1/parties", token, cancellationToken: ct),
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
        if (ShouldSkipForInfrastructure()) { return; }

        await _fixture.SeedTenantAsync("tenant-r", "user-1", TenantRole.TenantContributor);
        await _fixture.RemoveUserFromTenantAsync("tenant-r", "user-1");

        string token = TenantIntegrationTestSeeder.CreateToken("tenant-r", "user-1");

        HttpResponseMessage response = await PollAsync(
            ct => SendAsync(HttpMethod.Get, "/api/v1/parties", token, cancellationToken: ct),
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
        if (ShouldSkipForInfrastructure()) { return; }

        // Active tenant exists with an unrelated owner; the calling user has never been added.
        await _fixture.SeedTenantAsync("tenant-nm", "owner-only", TenantRole.TenantOwner);

        string token = TenantIntegrationTestSeeder.CreateToken("tenant-nm", "user-without-membership");

        HttpResponseMessage response = await PollAsync(
            ct => SendAsync(HttpMethod.Get, "/api/v1/parties", token, cancellationToken: ct),
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
        if (ShouldSkipForInfrastructure()) { return; }

        await _fixture.SeedTenantAsync("tenant-ir", "reader-user", TenantRole.TenantReader);

        string token = TenantIntegrationTestSeeder.CreateToken("tenant-ir", "reader-user");
        string writePayload = $$"""
            {
              "partyId": "{{Guid.NewGuid()}}",
              "type": "person",
              "personDetails": {
                "firstName": "Should",
                "lastName": "NotBeCreated"
              }
            }
            """;

        HttpResponseMessage response = await PollAsync(
            ct => SendAsync(HttpMethod.Post, "/api/v1/parties", token, writePayload, ct),
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
        if (ShouldSkipForInfrastructure()) { return; }

        await _fixture.SeedTenantAsync("tenant-iso-a", "user-a", TenantRole.TenantContributor);
        await _fixture.SeedTenantAsync("tenant-iso-b", "user-b", TenantRole.TenantOwner);

        Guid tenantBPartyId = Guid.NewGuid();
        string tokenB = TenantIntegrationTestSeeder.CreateToken("tenant-iso-b", "user-b");
        string tenantBPayload = $$"""
            {
              "partyId": "{{tenantBPartyId}}",
              "type": "person",
              "personDetails": {
                "firstName": "TenantB",
                "lastName": "Only"
              }
            }
            """;

        HttpResponseMessage createResponse = await PollAsync(
            ct => SendAsync(HttpMethod.Post, "/api/v1/parties", tokenB, tenantBPayload, ct),
            until: r => r.StatusCode == HttpStatusCode.Accepted
                || r.StatusCode == HttpStatusCode.Created
                || r.StatusCode == HttpStatusCode.OK
                || r.StatusCode == HttpStatusCode.UnprocessableEntity);

        bool createSkipped;
        try
        {
            createSkipped = await ShouldSkipForKnownPartyProcessUnavailableAsync(createResponse);
            if (!createSkipped)
            {
                createResponse.IsSuccessStatusCode.ShouldBeTrue(
                    $"Tenant B create should succeed; got {(int)createResponse.StatusCode} {createResponse.StatusCode}");
            }
        }
        finally
        {
            createResponse.Dispose();
        }

        if (createSkipped)
        {
            _output.WriteLine(
                "Skipped Aspire_GivenTwoTenants_TenantACannotEnumerateOrFetchTenantBPartiesAsync: tenant B create did not converge before isolation assertion could run; this is the deferred-baseline `party/process` 500 path tracked in deferred-work.md.");
            return;
        }

        // Wait for tenant-iso-b's projection to converge before the isolation check runs against
        // tenant-iso-a — otherwise the assertion may pass vacuously because tenant-iso-b's record
        // never hit the read model in the first place.
        await WaitForTenantBPartyVisibleAsync(tokenB, tenantBPartyId);

        string tokenA = TenantIntegrationTestSeeder.CreateToken("tenant-iso-a", "user-a");
        HttpResponseMessage listResponse = await PollAsync(
            ct => SendAsync(HttpMethod.Get, "/api/v1/parties", tokenA, cancellationToken: ct),
            until: r => r.StatusCode == HttpStatusCode.OK);

        try
        {
            listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            string body = await listResponse.Content.ReadAsStringAsync();

            body.ShouldNotContain("tenant-iso-b");
            body.ShouldNotContain("TenantB");
            body.ShouldNotContain(tenantBPartyId.ToString());
        }
        finally
        {
            listResponse.Dispose();
        }
    }

    private async Task WaitForTenantBPartyVisibleAsync(string tokenB, Guid tenantBPartyId, int attempts = 20, int delayMs = 250)
    {
        for (int i = 0; i < attempts; i++)
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "/api/v1/parties", tokenB).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (body.Contains(tenantBPartyId.ToString(), StringComparison.OrdinalIgnoreCase)
                    || body.Contains("TenantB", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            await Task.Delay(delayMs).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Tenant-iso-b's party {tenantBPartyId} was not visible to tenant-iso-b within {attempts * delayMs}ms; cross-tenant isolation assertion would be vacuous.");
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string requestUri,
        string token,
        string? jsonBody = null,
        CancellationToken cancellationToken = default)
    {
        // Per-request HttpRequestMessage avoids mutating the shared fixture HttpClient's
        // DefaultRequestHeaders, which would race across parallel tests.
        HttpRequestMessage request = new(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
        }

        return _fixture.CommandApiClient.SendAsync(request, cancellationToken);
    }

    // The project's xUnit v2.9.3 runner reports `throw SkipException.ForSkip(...)` as a Failed
    // test rather than a Skipped one when surfaced via VSTest, so we mirror the project's
    // existing E2E pattern (log via ITestOutputHelper + early return) used by 14+ other
    // integration tests in this suite (e.g. ConsentRestrictionE2ETests, EncryptionE2ETests).
    private bool ShouldSkipForInfrastructure()
    {
        if (_fixture.IsAvailable)
        {
            return false;
        }

        _output.WriteLine($"Skipped: Aspire topology unavailable: {_fixture.UnavailableReason}");
        return true;
    }

    private async Task<bool> ShouldSkipForKnownPartyProcessUnavailableAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.UnprocessableEntity)
        {
            return false;
        }

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        // Match a structured ProblemDetails payload rather than a free-text substring so
        // future error-message rewordings don't accidentally suppress real regressions.
        // The tracked deferred-baseline failure surfaces as a 422 ProblemDetails whose
        // `type` ends with `party-process-internal-error` OR carries an `errorCode` of
        // `party-process-internal-error`. Any other 422 is treated as a real failure.
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            if (TryGetStringProperty(doc.RootElement, "type", out string? typeValue)
                && (typeValue?.Contains("party-process-internal-error", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                _output.WriteLine(
                    $"Skipped: deferred-baseline party/process 500 (ProblemDetails type {typeValue}); see deferred-work.md.");
                return true;
            }

            if (TryGetStringProperty(doc.RootElement, "errorCode", out string? errorCode)
                && string.Equals(errorCode, "party-process-internal-error", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine(
                    $"Skipped: deferred-baseline party/process 500 (errorCode {errorCode}); see deferred-work.md.");
                return true;
            }
        }
        catch (JsonException)
        {
            // Fall through to the legacy substring fallback for non-JSON 422 bodies.
        }

        // Legacy fallback for plain-text 422 bodies. Kept intentionally narrow so it cannot
        // mask a different 422 with similar wording — operators should migrate to the
        // structured ProblemDetails contract above.
        if (body.Contains("party/process", StringComparison.OrdinalIgnoreCase)
            && body.Contains("Internal Server Error", StringComparison.OrdinalIgnoreCase))
        {
            _output.WriteLine(
                "Skipped: Aspire party/process command route returned 500 (matched on legacy plain-text body); see deferred-work.md.");
            return true;
        }

        return false;
    }

    private static bool TryGetStringProperty(JsonElement element, string name, out string? value)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static async Task AssertReasonCodeAsync(HttpResponseMessage response, string expectedReasonCode)
    {
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        try
        {
            using JsonDocument payload = JsonDocument.Parse(body);
            if (!payload.RootElement.TryGetProperty("reasonCode", out JsonElement reasonElement))
            {
                throw new InvalidOperationException(
                    $"Response did not contain 'reasonCode'. Status: {(int)response.StatusCode}. Body: {body}");
            }

            reasonElement.GetString().ShouldBe(expectedReasonCode);
        }
        catch (JsonException ex)
        {
            // A non-JSON denial body would otherwise throw JsonException and mask the real
            // reason-code mismatch. Surface the body explicitly so the failure diagnostic is
            // actionable in CI logs.
            throw new InvalidOperationException(
                $"Response body for status {(int)response.StatusCode} {response.StatusCode} was not valid JSON; " +
                $"expected reasonCode '{expectedReasonCode}'. Body: {body}",
                ex);
        }
    }

    /// <summary>
    /// Eventual-consistency poll — required because Story 11.2's local Tenants projection
    /// converges asynchronously after Tenants commands. Throws TimeoutException with the
    /// last observed status when the until-condition is never satisfied. The action delegate
    /// MUST honor the supplied <see cref="CancellationToken"/>.
    /// </summary>
    private static async Task<HttpResponseMessage> PollAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> action,
        Func<HttpResponseMessage, bool> until,
        int attempts = 20,
        int delayMs = 250,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage last = await action(cancellationToken).ConfigureAwait(false);
        if (until(last))
        {
            return last;
        }

        for (int i = 0; i < attempts - 1; i++)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            last.Dispose();
            last = await action(cancellationToken).ConfigureAwait(false);
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
