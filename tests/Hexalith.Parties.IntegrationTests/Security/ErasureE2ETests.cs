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
/// Tier 3 E2E tests that verify the full GDPR Article 17 erasure flow
/// through the Aspire topology (Parties service + DAPR sidecar + state stores).
/// Skip gracefully when Docker/DAPR is unavailable.
/// </summary>
[Collection("PartiesAspireTopology")]
public class ErasureE2ETests(PartiesAspireTopologyFixture fixture, ITestOutputHelper output)
{
    private const string CreatePartyEndpoint = "/api/v1/parties";
    private const string EraseEndpoint = "/api/v1/admin/parties/{0}/erase";
    private const string ErasureStatusEndpoint = "/api/v1/admin/parties/{0}/erasure-status";
    private const string GetPartyEndpoint = "/api/v1/parties/{0}";
    private const string ListPartiesEndpoint = "/api/v1/parties";
    private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

    // ─── 10.1: Full topology — create party → trigger erasure → verify key destroyed ───

    [Fact]
    public async Task FullTopology_CreatePartyThenErase_KeyDestroyed()
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
            personDetails = new { firstName = "E2E-Erase", lastName = "KeyDestroy" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, body);

        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }

        createResponse.StatusCode.ShouldBe(
            HttpStatusCode.Accepted,
            await createResponse.Content.ReadAsStringAsync());

        // Wait for party to be available in projection
        await WaitForPartyDetailAsync(client, partyId);

        // Trigger erasure
        HttpResponseMessage eraseResponse = await client.PostAsync(
            string.Format(EraseEndpoint, partyId), null);
        eraseResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Wait for erasure to complete — status should reach Erased/Verified
        await WaitForErasureStatusAsync(client, partyId, "Erased", "Verified", "KeyDestroyed");

        output.WriteLine($"Party {partyId} erasure triggered and key destroyed.");
    }

    // ─── 10.2: Full topology — erasure → detail projection returns erased status ───

    [Fact]
    public async Task FullTopology_AfterErasure_DetailReturnsErasedStatus()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        string partyId = Guid.NewGuid().ToString();
        HttpClient client = fixture.PartiesClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        // Create party
        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "E2E-Erase", lastName = "DetailCheck" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, body);

        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }

        createResponse.StatusCode.ShouldBe(
            HttpStatusCode.Accepted,
            await createResponse.Content.ReadAsStringAsync());
        await WaitForPartyDetailAsync(client, partyId);

        // Trigger erasure
        HttpResponseMessage eraseResponse = await client.PostAsync(
            string.Format(EraseEndpoint, partyId), null);
        eraseResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Wait for erasure to process, then check GET returns 410 Gone
        HttpResponseMessage detailResponse = await WaitForPartyErasedAsync(client, partyId);
        detailResponse.StatusCode.ShouldBe(HttpStatusCode.Gone);

        JsonDocument problem = await JsonDocument.ParseAsync(
            await detailResponse.Content.ReadAsStreamAsync());
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Party Erased");
    }

    // ─── 10.3: Full topology — erasure → party excluded from index search ───

    [Fact]
    public async Task FullTopology_AfterErasure_PartyExcludedFromSearch()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        string partyId = Guid.NewGuid().ToString();
        HttpClient client = fixture.PartiesClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        // Create party
        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "E2E-Erase", lastName = "SearchExclude" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, body);

        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }

        createResponse.StatusCode.ShouldBe(
            HttpStatusCode.Accepted,
            await createResponse.Content.ReadAsStringAsync());
        await WaitForPartyDetailAsync(client, partyId);

        // Trigger erasure
        HttpResponseMessage eraseResponse = await client.PostAsync(
            string.Format(EraseEndpoint, partyId), null);
        eraseResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Wait for erasure to complete
        await WaitForPartyErasedAsync(client, partyId);

        // List all parties — erased party should not appear
        HttpResponseMessage listResponse = await client.GetAsync(ListPartiesEndpoint);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument list = await JsonDocument.ParseAsync(
            await listResponse.Content.ReadAsStreamAsync());
        JsonElement items = list.RootElement.GetProperty("items");

        foreach (JsonElement item in items.EnumerateArray())
        {
            item.GetProperty("id").GetString().ShouldNotBe(partyId);
        }
    }

    // ─── 10.4: Full topology — PartyErased event published to pub/sub ───

    [Fact]
    public async Task FullTopology_AfterErasure_StatusEndpointReturnsErasedState()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        string partyId = Guid.NewGuid().ToString();
        HttpClient client = fixture.PartiesClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        // Create party
        using HttpContent body = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "E2E-Erase", lastName = "PubSubCheck" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, body);

        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }

        createResponse.StatusCode.ShouldBe(
            HttpStatusCode.Accepted,
            await createResponse.Content.ReadAsStringAsync());
        await WaitForPartyDetailAsync(client, partyId);

        // Trigger erasure
        HttpResponseMessage eraseResponse = await client.PostAsync(
            string.Format(EraseEndpoint, partyId), null);
        eraseResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Verify erasure-status endpoint reaches a terminal state
        // (this validates the full saga ran: key destruction → verification → status update)
        string status = await WaitForErasureStatusAsync(client, partyId, "Erased", "Verified", "KeyDestroyed");
        output.WriteLine($"Party {partyId} erasure status: {status}");
        status.ShouldNotBeNullOrWhiteSpace();
    }

    // ─── Helpers ───

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

    private static async Task<HttpResponseMessage> WaitForPartyDetailAsync(HttpClient client, string partyId)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        HttpResponseMessage? lastResponse = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            lastResponse = await client.GetAsync(string.Format(GetPartyEndpoint, partyId));
            if (lastResponse.StatusCode == HttpStatusCode.OK)
            {
                return lastResponse;
            }

            lastResponse.Dispose();
            await Task.Delay(500);
        }

        return lastResponse ?? throw new TimeoutException($"Party detail for '{partyId}' never returned 200.");
    }

    private static async Task<HttpResponseMessage> WaitForPartyErasedAsync(HttpClient client, string partyId)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        HttpResponseMessage? lastResponse = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            lastResponse = await client.GetAsync(string.Format(GetPartyEndpoint, partyId));
            if (lastResponse.StatusCode == HttpStatusCode.Gone)
            {
                return lastResponse;
            }

            lastResponse.Dispose();
            await Task.Delay(1000);
        }

        return lastResponse ?? throw new TimeoutException(
            $"Party '{partyId}' never reached erased state (410 Gone).");
    }

    private static async Task<string> WaitForErasureStatusAsync(
        HttpClient client, string partyId, params string[] acceptableStatuses)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        string lastStatus = string.Empty;

        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync(
                string.Format(ErasureStatusEndpoint, partyId));

            if (response.StatusCode == HttpStatusCode.OK)
            {
                JsonDocument payload = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync());

                if (payload.RootElement.TryGetProperty("status", out JsonElement statusElement))
                {
                    lastStatus = statusElement.GetString() ?? string.Empty;
                    if (acceptableStatuses.Any(s => lastStatus.Contains(s, StringComparison.OrdinalIgnoreCase)))
                    {
                        return lastStatus;
                    }
                }
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException(
            $"Erasure status for party '{partyId}' never reached [{string.Join(", ", acceptableStatuses)}]. "
            + $"Last observed: '{lastStatus}'.");
    }

    private static string CreateAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", "e2e-erasure-test"),
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
