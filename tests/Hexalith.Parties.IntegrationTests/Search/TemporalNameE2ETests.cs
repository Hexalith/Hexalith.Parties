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

namespace Hexalith.Parties.IntegrationTests.Search;

/// <summary>
/// Tier 3 E2E tests that verify temporal name queries through the full Aspire topology.
/// These tests require Docker + DAPR and skip gracefully when unavailable.
/// </summary>
[Collection("PartiesAspireTopology")]
public class TemporalNameE2ETests(PartiesAspireTopologyFixture fixture, ITestOutputHelper output)
{
    private const string CreatePartyEndpoint = "/api/v1/parties";
    private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

    // ─── Task 9.2: Full topology — create party → update name → query at original timestamp ───

    [Fact]
    public async Task FullTopology_TemporalNameQuery_ReturnsOriginalNameAtCreationTimestamp()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        HttpClient client = fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        string partyId = Guid.NewGuid().ToString();

        // Record timestamp before creation
        DateTimeOffset beforeCreation = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Step 1: Create party with original name
        using HttpContent createBody = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "John", lastName = "Doe" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, createBody);

        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Wait for party to appear with original name
        JsonDocument originalDetail = await WaitForPartyDetailAsync(client, partyId, "John Doe");
        string originalDisplayName = originalDetail.RootElement.GetProperty("displayName").GetString()!;
        originalDisplayName.ShouldBe("John Doe");

        // Record timestamp after creation is confirmed
        DateTimeOffset afterCreation = DateTimeOffset.UtcNow;

        // Wait a moment to ensure temporal separation
        await Task.Delay(1000);

        // Step 2: Update person details (name change)
        using HttpContent updateBody = JsonContent.Create(new
        {
            partyId,
            firstName = "Jane",
            lastName = "Smith",
        });

        HttpResponseMessage updateResponse = await client.PostAsync($"/api/v1/parties/{partyId}/update-person-details", updateBody);

        if (IsDaprInfrastructureFailure(updateResponse))
        {
            output.WriteLine("DAPR command pipeline not available during update — skipping.");
            return;
        }

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Wait for the name to change in the projection
        await WaitForPartyDetailAsync(client, partyId, "Jane Smith");

        // Step 3: Query name at the original creation timestamp
        string atParam = Uri.EscapeDataString(afterCreation.ToString("O"));
        HttpResponseMessage nameResponse = await client.GetAsync($"/api/v1/parties/{partyId}/name?at={atParam}");

        nameResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonDocument nameResult = await JsonDocument.ParseAsync(await nameResponse.Content.ReadAsStreamAsync());

        string historicalName = nameResult.RootElement.GetProperty("displayName").GetString()!;
        historicalName.ShouldBe("John Doe");
        nameResult.RootElement.GetProperty("partyId").GetString().ShouldBe(partyId);

        output.WriteLine($"Temporal query at {afterCreation:O} returned '{historicalName}' (current: 'Jane Smith')");

        // Step 4: Query name-history — should have at least 2 entries
        HttpResponseMessage historyResponse = await client.GetAsync($"/api/v1/parties/{partyId}/name-history");
        historyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument historyResult = await JsonDocument.ParseAsync(await historyResponse.Content.ReadAsStreamAsync());
        historyResult.RootElement.GetArrayLength().ShouldBeGreaterThanOrEqualTo(2);

        output.WriteLine($"Name history has {historyResult.RootElement.GetArrayLength()} entries");

        // Step 5: Query before creation — should return 404
        string beforeParam = Uri.EscapeDataString(beforeCreation.AddDays(-1).ToString("O"));
        HttpResponseMessage beforeResponse = await client.GetAsync($"/api/v1/parties/{partyId}/name?at={beforeParam}");

        // Either 404 (no name at that time) or the first name entry (depending on projection timing)
        // The acceptance criteria says "before party existed returns 404"
        beforeResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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

    private static async Task<JsonDocument> WaitForPartyDetailAsync(HttpClient client, string partyId, string expectedDisplayName)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync($"/api/v1/parties/{partyId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                string content = await response.Content.ReadAsStringAsync();
                JsonDocument doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("displayName", out JsonElement nameEl)
                    && string.Equals(nameEl.GetString(), expectedDisplayName, StringComparison.Ordinal))
                {
                    return doc;
                }

                doc.Dispose();
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Party '{partyId}' never showed displayName='{expectedDisplayName}' within 30s.");
    }

    private static string CreateAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", "e2e-temporal-name-test"),
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
