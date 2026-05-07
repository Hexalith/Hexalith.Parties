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

/// <summary>
/// Tier 3 E2E tests that verify encryption roundtrip through the full Aspire topology.
/// These tests require Docker + DAPR and skip gracefully when unavailable.
/// </summary>
[Collection("PartiesAspireTopology")]
public class EncryptionE2ETests(PartiesAspireTopologyFixture fixture, ITestOutputHelper output)
{
    private const string CreatePartyEndpoint = "/api/v1/parties";
    private const string RotateKeyEndpoint = "/api/v1/admin/parties/{0}/rotate-key";
    private const string KeyVersionsEndpoint = "/api/v1/admin/parties/{0}/key-versions";
    private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

    // ─── Task 8.1: Full topology — create party, verify encryption roundtrip ───

    [Fact]
    public async Task FullTopology_CreateParty_EncryptionRoundtrip()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        string partyId = Guid.NewGuid().ToString();
        HttpClient client = fixture.PartiesClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        // Create party with personal data
        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "E2E-Enc", lastName = "RoundTrip" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, body);

        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Wait for key to be created (confirms encryption pipeline ran)
        IReadOnlyList<int> versions = await WaitForKeyVersionsAsync(client, partyId, [1]);
        versions.ShouldBe([1]);

        // Query the party detail — should return decrypted data via projection
        HttpResponseMessage getResponse = await WaitForPartyDetailAsync(client, partyId);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument detail = await JsonDocument.ParseAsync(
            await getResponse.Content.ReadAsStreamAsync());

        // The GET response should contain plaintext personal data (decrypted at publish time)
        string displayName = detail.RootElement.GetProperty("displayName").GetString() ?? string.Empty;
        displayName.ShouldContain("E2E-Enc");
        displayName.ShouldContain("RoundTrip");
    }

    // ─── Task 8.2: Key rotation — old events still decryptable ───

    [Fact]
    public async Task FullTopology_KeyRotation_OldEventsStillDecryptable()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        string partyId = Guid.NewGuid().ToString();
        HttpClient client = fixture.PartiesClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        // Create party (v1 key)
        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "E2E-Rotate", lastName = "V1Key" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, body);

        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await WaitForKeyVersionsAsync(client, partyId, [1]);

        // Rotate key (v2)
        HttpResponseMessage rotateResponse = await client.PostAsync(string.Format(RotateKeyEndpoint, partyId), null);
        rotateResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Wait for v2 key
        IReadOnlyList<int> versions = await WaitForKeyVersionsAsync(client, partyId, [1, 2]);
        versions.ShouldContain(1);
        versions.ShouldContain(2);

        // Query party detail — old events (encrypted with v1) should still be readable
        HttpResponseMessage getResponse = await WaitForPartyDetailAsync(client, partyId);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument detail = await JsonDocument.ParseAsync(
            await getResponse.Content.ReadAsStreamAsync());

        string displayName = detail.RootElement.GetProperty("displayName").GetString() ?? string.Empty;
        displayName.ShouldContain("E2E-Rotate");
    }

    private static bool IsDaprInfrastructureFailure(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.UnprocessableEntity)
        {
            return false;
        }

        string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return responseBody.Contains("Dapr endpoint", StringComparison.OrdinalIgnoreCase)
            || responseBody.Contains("GetConfiguration", StringComparison.OrdinalIgnoreCase);
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

        throw new TimeoutException($"Timed out waiting for key versions [{string.Join(", ", expectedVersions)}] for party '{partyId}'. Last observed: [{string.Join(", ", latest)}].");
    }

    private static async Task<HttpResponseMessage> WaitForPartyDetailAsync(HttpClient client, string partyId)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        HttpResponseMessage? lastResponse = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            lastResponse = await client.GetAsync($"/api/v1/parties/{partyId}");
            if (lastResponse.StatusCode == HttpStatusCode.OK)
            {
                return lastResponse;
            }

            lastResponse.Dispose();
            await Task.Delay(500);
        }

        return lastResponse ?? throw new TimeoutException($"Party detail for '{partyId}' never returned 200.");
    }

    private static string CreateAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", "e2e-encryption-test"),
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
