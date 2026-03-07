using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Admin;

/// <summary>
/// Tier 3 end-to-end tests for the admin rebuild endpoint running against the full
/// Aspire topology. Validates endpoint accessibility and authorization with real
/// DAPR sidecar and infrastructure.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("PartiesAspireTopology")]
public sealed class AdminEndpointE2ETests
{
    private const string RebuildEndpoint = "/api/v1/admin/projections/rebuild";
    private const string Issuer = "hexalith-dev";
    private const string Audience = "hexalith-parties";
    private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

    private readonly HealthChecks.PartiesAspireTopologyFixture _fixture;

    public AdminEndpointE2ETests(HealthChecks.PartiesAspireTopologyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RebuildEndpoint_WithoutToken_Returns401Async()
    {
        using HttpContent body = JsonContent.Create(new
        {
            tenantId = "tenant-a",
            projection = "all",
        });

        using HttpResponseMessage response = await _fixture.CommandApiClient
            .PostAsync(RebuildEndpoint, body);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,
            "Admin endpoint should return 401 without authentication token.");
    }

    [Fact]
    public async Task RebuildEndpoint_WithRegularUserToken_Returns403Async()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, RebuildEndpoint)
        {
            Content = JsonContent.Create(new
            {
                tenantId = "tenant-a",
                projection = "all",
            }),
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(includeAdminRole: false));

        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden,
            "Admin endpoint should return 403 for users without admin role.");
    }

    [Fact]
    public async Task RebuildEndpoint_WithAdminToken_Returns202AcceptedAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, RebuildEndpoint)
        {
            Content = JsonContent.Create(new
            {
                tenantId = "tenant-a",
                projection = "all",
            }),
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(includeAdminRole: true));

        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted,
            "Admin endpoint should return 202 Accepted with valid admin token.");
    }

    private static string CreateToken(bool includeAdminRole)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", "e2e-test-admin"),
            new("eventstore:tenant", "tenant-a"),
        };

        if (includeAdminRole)
        {
            claims.Add(new Claim(ClaimTypes.Role, "admin"));
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
