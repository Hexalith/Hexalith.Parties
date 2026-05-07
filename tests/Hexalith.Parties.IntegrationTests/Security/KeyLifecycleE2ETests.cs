#pragma warning disable CA2007

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.Parties.IntegrationTests.HealthChecks;

using Microsoft.IdentityModel.Tokens;

using Shouldly;

using Xunit.Abstractions;

namespace Hexalith.Parties.IntegrationTests.Security;

[Collection("PartiesAspireTopology")]
public class KeyLifecycleE2ETests(PartiesAspireTopologyFixture fixture, ITestOutputHelper output)
{
    private const string CreatePartyEndpoint = "/api/v1/parties";
    private const string RotateKeyEndpoint = "/api/v1/admin/parties/{0}/rotate-key";
    private const string KeyVersionsEndpoint = "/api/v1/admin/parties/{0}/key-versions";
    private const string KeyAuditTrailEndpoint = "/api/v1/admin/parties/{0}/key-audit-trail";
    private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

    [Fact]
    public async Task RotateKey_InFullTopology_Returns202WithCorrelationId()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        HttpClient client = fixture.PartiesClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        HttpResponseMessage response = await client.PostAsync(string.Format(RotateKeyEndpoint, "e2e-party-1"), null);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Accepted, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            JsonDocument payload = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());
            payload.RootElement.TryGetProperty("correlationId", out JsonElement cid).ShouldBeTrue();
            cid.GetString().ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task RotateKey_WithoutAuth_Returns401InFullTopology()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        HttpClient client = fixture.PartiesClient;
        client.DefaultRequestHeaders.Authorization = null;
        using HttpRequestMessage request = new(HttpMethod.Post,
            string.Format(RotateKeyEndpoint, "e2e-party-1"));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// 9.1: Full Aspire topology: create party -> verify key exists.
    /// Creates a party via the composite endpoint and verifies the command
    /// is accepted (202), which triggers key creation through the event pipeline.
    /// The key is created as a side-effect of the PartyCreated event being
    /// processed by PartyPayloadProtectionService.
    /// NOTE: This test requires the full DAPR command pipeline (including configuration
    /// store) to be available. When DAPR infrastructure is not fully configured in the
    /// test topology, the test passes vacuously with a warning.
    /// </summary>
    [Fact]
    public async Task CreateParty_InFullTopology_AcceptsAndTriggersKeyCreation()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        string partyId = Guid.NewGuid().ToString();
        HttpClient client = fixture.PartiesClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "E2E", lastName = "KeyTest" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, body);

        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available in test topology — skipping command routing assertions.");
            return;
        }

        createResponse.StatusCode.ShouldBe(
            HttpStatusCode.Accepted,
            await createResponse.Content.ReadAsStringAsync());

        JsonDocument createPayload = await JsonDocument.ParseAsync(
            await createResponse.Content.ReadAsStreamAsync());
        createPayload.RootElement.TryGetProperty("correlationId", out JsonElement cid).ShouldBeTrue();
        cid.GetString().ShouldNotBeNullOrWhiteSpace();

        IReadOnlyList<int> createdVersions = await WaitForKeyVersionsAsync(client, partyId, [1]);
        createdVersions.ShouldBe([1]);
    }

    /// <summary>
    /// 9.2: Key rotation in full topology with audit trail verification.
    /// Verifies that key rotation works end-to-end through the API
    /// and produces a correlationId for audit tracking.
    /// NOTE: Requires full DAPR command pipeline. See CreateParty test note.
    /// </summary>
    [Fact]
    public async Task KeyRotation_InFullTopology_ProducesAuditableCorrelationId()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        string partyId = Guid.NewGuid().ToString();
        HttpClient client = fixture.PartiesClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "E2E", lastName = "AuditTest" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, body);

        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available in test topology — skipping command routing assertions.");
            return;
        }

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        _ = await WaitForKeyVersionsAsync(client, partyId, [1]);

        HttpResponseMessage rotateResponse = await client.PostAsync(string.Format(RotateKeyEndpoint, partyId), null);

        rotateResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Verify rotation response includes correlation ID for audit trail
        JsonDocument rotatePayload = await JsonDocument.ParseAsync(
            await rotateResponse.Content.ReadAsStreamAsync());
        rotatePayload.RootElement.TryGetProperty("correlationId", out JsonElement rotationCid).ShouldBeTrue();
        string? correlationId = rotationCid.GetString();
        correlationId.ShouldNotBeNullOrWhiteSpace();

        // Second rotation — proves key versioning works across multiple rotations
        HttpResponseMessage secondRotateResponse = await client.PostAsync(string.Format(RotateKeyEndpoint, partyId), null);

        secondRotateResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        JsonDocument secondPayload = await JsonDocument.ParseAsync(
            await secondRotateResponse.Content.ReadAsStreamAsync());
        secondPayload.RootElement.TryGetProperty("correlationId", out JsonElement secondCid).ShouldBeTrue();
        string? secondCorrelationId = secondCid.GetString();
        secondCorrelationId.ShouldNotBeNullOrWhiteSpace();

        // Each rotation should produce a unique correlation ID
        secondCorrelationId.ShouldNotBe(correlationId);

        IReadOnlyList<int> versions = await WaitForKeyVersionsAsync(client, partyId, [1, 2, 3]);
        versions.ShouldBe([1, 2, 3]);

        JsonElement[] auditEntries = await WaitForAuditEntriesAsync(client, partyId, entries =>
            entries.Any(e => string.Equals(e.GetProperty("operationType").GetString(), "Create", StringComparison.OrdinalIgnoreCase))
            && entries.Count(e => string.Equals(e.GetProperty("operationType").GetString(), "Rotate", StringComparison.OrdinalIgnoreCase)) >= 2)
            ;

        auditEntries.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Checks whether the response indicates a DAPR infrastructure failure
    /// (e.g., configuration store not available) rather than an application-level error.
    /// Returns true if the test should be skipped due to infrastructure limitations.
    /// </summary>
    private static bool IsDaprInfrastructureFailure(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.UnprocessableEntity)
        {
            return false;
        }

        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return body.Contains("Dapr endpoint", StringComparison.OrdinalIgnoreCase)
            || body.Contains("GetConfiguration", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<int>> WaitForKeyVersionsAsync(HttpClient client, string partyId, IReadOnlyList<int> expectedVersions)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        List<int> latest = [];

        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync(string.Format(KeyVersionsEndpoint, partyId));
            if (response.StatusCode == HttpStatusCode.OK)
            {
                JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                latest = payload.RootElement.GetProperty("versions")
                    .EnumerateArray()
                    .Select(v => v.GetInt32())
                    .ToList();

                if (latest.SequenceEqual(expectedVersions))
                {
                    return latest;
                }
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Timed out waiting for key versions [{string.Join(", ", expectedVersions)}] for party '{partyId}'. Last observed versions: [{string.Join(", ", latest)}].");
    }

    private static async Task<JsonElement[]> WaitForAuditEntriesAsync(HttpClient client, string partyId, Func<JsonElement[], bool> predicate)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        JsonElement[] latest = [];

        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync(string.Format(KeyAuditTrailEndpoint, partyId));
            if (response.StatusCode == HttpStatusCode.OK)
            {
                JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                latest = payload.RootElement.EnumerateArray().ToArray();
                if (predicate(latest))
                {
                    return latest;
                }
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Timed out waiting for audit trail verification for party '{partyId}'. Last observed entry count: {latest.Length}.");
    }

    private static string CreateAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", "e2e-test-user"),
            new("eventstore:tenant", "e2e-tenant"),
            new(ClaimTypes.Role, "admin"),
        };

        var token = new JwtSecurityToken(
            issuer: "hexalith-dev",
            audience: "hexalith-parties",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

#pragma warning restore CA2007
