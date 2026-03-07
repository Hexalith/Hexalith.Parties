using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.Parties.IntegrationTests.HealthChecks;

using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Security;

[Collection("PartiesAspireTopology")]
public class KeyLifecycleE2ETests(PartiesAspireTopologyFixture fixture)
{
    private const string RotateKeyEndpoint = "/api/v1/admin/parties/{0}/rotate-key?tenantId={1}";
    private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

    [Fact]
    public async Task RotateKey_InFullTopology_Returns202WithCorrelationId()
    {
        HttpClient client = fixture.CommandApiClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        HttpResponseMessage response = await client.PostAsync(
            string.Format(RotateKeyEndpoint, "e2e-party-1", "e2e-tenant"), null);

        // The endpoint returns 202 even if the key doesn't exist yet
        // (background task handles errors). This tests the endpoint reachability.
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.TryGetProperty("correlationId", out JsonElement cid).ShouldBeTrue();
        cid.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RotateKey_WithoutAuth_Returns401InFullTopology()
    {
        HttpClient client = fixture.CommandApiClient;
        // Remove any existing auth header
        using HttpRequestMessage request = new(HttpMethod.Post,
            string.Format(RotateKeyEndpoint, "e2e-party-1", "e2e-tenant"));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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
