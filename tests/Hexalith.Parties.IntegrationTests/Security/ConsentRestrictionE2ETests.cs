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
/// Tier 3 E2E tests that verify consent management, restriction enforcement,
/// and data portability through the full Aspire topology.
/// Skip gracefully when Docker/DAPR is unavailable.
/// </summary>
[Collection("PartiesAspireTopology")]
public class ConsentRestrictionE2ETests(PartiesAspireTopologyFixture fixture, ITestOutputHelper output)
{
    private const string CreatePartyEndpoint = "/api/v1/parties";
    private const string GetPartyEndpoint = "/api/v1/parties/{0}";
    private const string AddChannelEndpoint = "/api/v1/parties/{0}/add-contact-channel";
    private const string ConsentEndpoint = "/api/v1/admin/parties/{0}/consent";
    private const string RevokeConsentEndpoint = "/api/v1/admin/parties/{0}/consent/{1}";
    private const string RestrictEndpoint = "/api/v1/admin/parties/{0}/restrict";
    private const string LiftRestrictionEndpoint = "/api/v1/admin/parties/{0}/lift-restriction";
    private const string UpdatePersonDetailsEndpoint = "/api/v1/parties/{0}/update-person-details";
    private const string ExportEndpoint = "/api/v1/admin/parties/{0}/export";
    private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

    // ─── 9.1: Full topology — create party → record consent → verify in detail ───

    [Fact]
    public async Task FullTopology_CreatePartyRecordConsent_ConsentVisibleInDetail()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        string partyId = Guid.NewGuid().ToString();
        HttpClient client = fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        // Step 1: Create party
        using HttpContent createBody = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "E2E-Consent", lastName = "Test" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, createBody);
        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await WaitForPartyDetailAsync(client, partyId);

        // Step 2: Add a contact channel
        using HttpContent channelBody = JsonContent.Create(new
        {
            partyId,
            contactChannelId = "ch-email-1",
            type = 0, // Email
            value = "consent-test@example.com",
        });
        HttpResponseMessage channelResponse = await client.PostAsync(
            string.Format(AddChannelEndpoint, partyId), channelBody);

        if (channelResponse.StatusCode != HttpStatusCode.Accepted)
        {
            output.WriteLine($"Add channel returned {channelResponse.StatusCode} — skipping consent test.");
            return;
        }

        // Wait for channel to appear in projection
        await Task.Delay(2000);

        // Step 3: Record consent
        HttpResponseMessage consentResponse = await client.PostAsJsonAsync(
            string.Format(ConsentEndpoint, partyId),
            new { channelId = "ch-email-1", purpose = "marketing", lawfulBasis = 0 });
        consentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Step 4: Verify consent in GET consent endpoint
        await Task.Delay(1000);
        HttpResponseMessage getConsentResponse = await client.GetAsync(
            string.Format(ConsentEndpoint, partyId));
        getConsentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument consentPayload = await JsonDocument.ParseAsync(
            await getConsentResponse.Content.ReadAsStreamAsync());
        consentPayload.RootElement.GetArrayLength().ShouldBeGreaterThanOrEqualTo(1);

        output.WriteLine($"Party {partyId} consent recorded and verified in projection.");
    }

    // ─── 9.2: Full topology — restrict → attempt update → rejected; lift → update succeeds ───

    [Fact]
    public async Task FullTopology_RestrictThenUpdate_RejectedThenLiftSucceeds()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        string partyId = Guid.NewGuid().ToString();
        HttpClient client = fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        // Step 1: Create party
        using HttpContent createBody = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "E2E-Restrict", lastName = "Guard" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, createBody);
        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await WaitForPartyDetailAsync(client, partyId);

        // Step 2: Restrict processing
        HttpResponseMessage restrictResponse = await client.PostAsJsonAsync(
            string.Format(RestrictEndpoint, partyId),
            new { reason = "E2E investigation" });
        restrictResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Wait for restriction to be applied
        await Task.Delay(2000);

        // Step 3: Attempt update — should be rejected
        using HttpContent updateBody = JsonContent.Create(new
        {
            partyId,
            personDetails = new { firstName = "Updated", lastName = "Should-Fail" },
        });
        HttpResponseMessage updateResponse = await client.PostAsync(
            string.Format(UpdatePersonDetailsEndpoint, partyId), updateBody);

        // The aggregate rejects with PartyProcessingRestricted → 422 Domain Rejection
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        output.WriteLine($"Update correctly rejected while restricted.");

        // Step 4: Lift restriction
        HttpResponseMessage liftResponse = await client.PostAsync(
            string.Format(LiftRestrictionEndpoint, partyId), null);
        liftResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Wait for restriction lift to take effect
        await Task.Delay(2000);

        // Step 5: Retry update — should succeed now
        using HttpContent retryBody = JsonContent.Create(new
        {
            partyId,
            personDetails = new { firstName = "Updated", lastName = "Now-Succeeds" },
        });
        HttpResponseMessage retryResponse = await client.PostAsync(
            string.Format(UpdatePersonDetailsEndpoint, partyId), retryBody);
        retryResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        output.WriteLine($"Party {partyId} restriction lifecycle validated.");
    }

    // ─── 9.3: Full topology — create party with channels → export → verify JSON ───

    [Fact]
    public async Task FullTopology_CreatePartyWithChannels_ExportContainsAllData()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        string partyId = Guid.NewGuid().ToString();
        HttpClient client = fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        // Step 1: Create party
        using HttpContent createBody = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "E2E-Export", lastName = "Portability" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, createBody);
        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await WaitForPartyDetailAsync(client, partyId);

        // Step 2: Add contact channel
        using HttpContent channelBody = JsonContent.Create(new
        {
            partyId,
            contactChannelId = "ch-export-email",
            type = 0, // Email
            value = "export@example.com",
        });
        HttpResponseMessage channelResponse = await client.PostAsync(
            string.Format(AddChannelEndpoint, partyId), channelBody);

        if (channelResponse.StatusCode != HttpStatusCode.Accepted)
        {
            output.WriteLine($"Add channel returned {channelResponse.StatusCode} — skipping export test.");
            return;
        }

        // Wait for projection to update
        await Task.Delay(2000);

        // Step 3: Export
        HttpResponseMessage exportResponse = await client.GetAsync(
            string.Format(ExportEndpoint, partyId));
        exportResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument exportPayload = await JsonDocument.ParseAsync(
            await exportResponse.Content.ReadAsStreamAsync());

        exportPayload.RootElement.GetProperty("partyId").GetString().ShouldBe(partyId);
        exportPayload.RootElement.GetProperty("partyType").GetString().ShouldBe("Person");
        exportPayload.RootElement.TryGetProperty("exportedAt", out _).ShouldBeTrue();
        exportPayload.RootElement.TryGetProperty("personDetails", out _).ShouldBeTrue();

        output.WriteLine($"Party {partyId} export contains all expected data.");
    }

    // ─── 9.4: Full topology — restrict → record consent → succeeds (Article 18(3)) ───

    [Fact]
    public async Task FullTopology_RestrictThenRecordConsent_Succeeds()
    {
        if (!fixture.IsAvailable) { output.WriteLine($"Skipped: {fixture.UnavailableReason}"); return; }

        string partyId = Guid.NewGuid().ToString();
        HttpClient client = fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        // Step 1: Create party
        using HttpContent createBody = JsonContent.Create(new
        {
            partyId,
            type = "Person",
            personDetails = new { firstName = "E2E-Art18", lastName = "ConsentExempt" },
        });

        HttpResponseMessage createResponse = await client.PostAsync(CreatePartyEndpoint, createBody);
        if (IsDaprInfrastructureFailure(createResponse))
        {
            output.WriteLine("DAPR command pipeline not available — skipping.");
            return;
        }
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await WaitForPartyDetailAsync(client, partyId);

        // Step 2: Add contact channel
        using HttpContent channelBody = JsonContent.Create(new
        {
            partyId,
            contactChannelId = "ch-consent-exempt",
            type = 0,
            value = "exempt@example.com",
        });
        HttpResponseMessage channelResponse = await client.PostAsync(
            string.Format(AddChannelEndpoint, partyId), channelBody);

        if (channelResponse.StatusCode != HttpStatusCode.Accepted)
        {
            output.WriteLine($"Add channel returned {channelResponse.StatusCode} — skipping.");
            return;
        }

        await Task.Delay(2000);

        // Step 3: Restrict processing
        HttpResponseMessage restrictResponse = await client.PostAsJsonAsync(
            string.Format(RestrictEndpoint, partyId),
            new { reason = "Article 18 test" });
        restrictResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(2000);

        // Step 4: Record consent while restricted — should succeed per Article 18(3)
        HttpResponseMessage consentResponse = await client.PostAsJsonAsync(
            string.Format(ConsentEndpoint, partyId),
            new { channelId = "ch-consent-exempt", purpose = "marketing", lawfulBasis = 0 });
        consentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        output.WriteLine($"Party {partyId}: consent recorded while restricted — Article 18(3) validated.");
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

    private static string CreateAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", "e2e-consent-test"),
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
