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
/// Tier 3 E2E tests that verify semantic search through the full Aspire topology.
/// These tests require Docker + DAPR and skip gracefully when unavailable.
/// </summary>
[Collection("PartiesAspireTopology")]
public class SemanticSearchE2ETests(PartiesAspireTopologyFixture fixture, ITestOutputHelper output)
{
    private const string CreatePartyEndpoint = "/api/v1/parties";
    private const string SearchEndpoint = "/api/v1/parties/search";
    private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

    // ─── Task 9.1: Full topology — create 3 parties → semantic search → verify ranked results ───

    [Fact]
    public async Task FullTopology_SemanticSearch_FuzzyQuery_ReturnsRankedResults()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        HttpClient client = fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        string party1Id = Guid.NewGuid().ToString();
        string party2Id = Guid.NewGuid().ToString();
        string party3Id = Guid.NewGuid().ToString();

        // Create 3 parties: Jean Dupont (Person), Acme Corp (Org), Marie Curie (Person)
        HttpResponseMessage r1 = await CreatePartyAsync(client, party1Id, "Person",
            personDetails: new { firstName = "Jean", lastName = "Dupont" });
        HttpResponseMessage r2 = await CreatePartyAsync(client, party2Id, "Organization",
            organizationDetails: new { legalName = "Acme Corporation" });
        HttpResponseMessage r3 = await CreatePartyAsync(client, party3Id, "Person",
            personDetails: new { firstName = "Marie", lastName = "Curie" });

        if (IsDaprInfrastructureFailure(r1) || IsDaprInfrastructureFailure(r2) || IsDaprInfrastructureFailure(r3))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }

        r1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        r2.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        r3.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Wait for all 3 parties to be visible in GET /api/v1/parties/{id}
        await WaitForPartyDetailAsync(client, party1Id);
        await WaitForPartyDetailAsync(client, party2Id);
        await WaitForPartyDetailAsync(client, party3Id);

        // Search with fuzzy query "Dupnt" (misspelled — should match "Dupont" via Jaro-Winkler)
        JsonDocument searchResult = await WaitForSearchResultsAsync(client, "Dupnt", minCount: 1);
        JsonElement items = searchResult.RootElement.GetProperty("items");

        items.GetArrayLength().ShouldBeGreaterThanOrEqualTo(1);

        // The first result should be Jean Dupont (fuzzy match on "Dupnt" → "Dupont")
        JsonElement firstItem = items[0];
        string displayName = firstItem.GetProperty("party").GetProperty("displayName").GetString() ?? string.Empty;
        displayName.ShouldContain("Dupont");

        // RelevanceScore should be populated (> 0)
        double relevanceScore = firstItem.GetProperty("relevanceScore").GetDouble();
        relevanceScore.ShouldBeGreaterThan(0.0);

        output.WriteLine($"Search for 'Dupnt' returned {items.GetArrayLength()} result(s). Top: {displayName} (score: {relevanceScore:F3})");
    }

    private static async Task<HttpResponseMessage> CreatePartyAsync(
        HttpClient client,
        string partyId,
        string type,
        object? personDetails = null,
        object? organizationDetails = null)
    {
        object body = type == "Organization"
            ? new { partyId, type, organizationDetails }
            : new { partyId, type, personDetails };

        using HttpContent content = JsonContent.Create(body);
        return await client.PostAsync(CreatePartyEndpoint, content);
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

    private static async Task WaitForPartyDetailAsync(HttpClient client, string partyId)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync($"/api/v1/parties/{partyId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Party detail for '{partyId}' never returned 200 within 30s.");
    }

    private static async Task<JsonDocument> WaitForSearchResultsAsync(HttpClient client, string query, int minCount)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        string? lastContent = null;
        HttpStatusCode? lastStatusCode = null;
        JsonDocument? lastResult = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync($"{SearchEndpoint}?q={Uri.EscapeDataString(query)}");
            lastStatusCode = response.StatusCode;
            lastContent = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                string content = lastContent;
                JsonDocument doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("items", out JsonElement items) && items.GetArrayLength() >= minCount)
                {
                    return doc;
                }

                lastResult?.Dispose();
                lastResult = doc;
            }

            await Task.Delay(500);
        }

        if (lastResult is not null)
        {
            throw new TimeoutException(
                $"Search for '{query}' never returned {minCount}+ results within 30s. Last response: {lastContent}");
        }

        throw new TimeoutException(
            $"Search for '{query}' never returned {minCount}+ results within 30s. "
            + $"Last status: {lastStatusCode?.ToString() ?? "none"}. Last response: {lastContent ?? "<none>"}");
    }

    private static string CreateAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", "e2e-search-test"),
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
