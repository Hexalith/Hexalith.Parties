using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.Parties.Contracts.Security;

using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Controllers;

public sealed class KeyRotationEndpointTests : IClassFixture<AdminEndpointIntegrationTests.AdminTestFactory>
{
    private const string RotateKeyEndpoint = "/api/v1/admin/parties/{0}/rotate-key?tenantId={1}";
    private readonly AdminEndpointIntegrationTests.AdminTestFactory _factory;

    public KeyRotationEndpointTests(AdminEndpointIntegrationTests.AdminTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RotateKey_WithAdminToken_Returns202AcceptedAsync()
    {
        _factory.KeyManagementService.RotateKeyAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns(new PartyKeyInfo
            {
                KeyId = "acme/parties/p1/v2",
                Version = 2,
                TenantId = "acme",
                PartyId = "p1",
                Algorithm = EncryptionAlgorithm.AES256GCM,
                CreatedAt = DateTimeOffset.UtcNow,
            });

        using HttpClient client = CreateAdminClient();
        HttpResponseMessage response = await client.PostAsync(
            string.Format(RotateKeyEndpoint, "p1", "acme"), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.TryGetProperty("correlationId", out JsonElement correlationId).ShouldBeTrue();
        correlationId.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RotateKey_WithoutToken_Returns401Async()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync(
            string.Format(RotateKeyEndpoint, "p1", "acme"), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RotateKey_WithRegularUserToken_Returns403Async()
    {
        using HttpClient client = CreateRegularUserClient();
        HttpResponseMessage response = await client.PostAsync(
            string.Format(RotateKeyEndpoint, "p1", "acme"), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RotateKey_MissingTenantId_Returns400Async()
    {
        using HttpClient client = CreateAdminClient();
        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/admin/parties/p1/rotate-key", null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private HttpClient CreateAdminClient()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(includeAdminRole: true));
        return client;
    }

    private HttpClient CreateRegularUserClient()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(includeAdminRole: false));
        return client;
    }

    private static string CreateToken(bool includeAdminRole)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(AdminEndpointIntegrationTests.AdminTestFactory.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", "test-user"),
            new("eventstore:tenant", "tenant-a"),
        };

        if (includeAdminRole)
        {
            claims.Add(new Claim(ClaimTypes.Role, "admin"));
        }

        var token = new JwtSecurityToken(
            issuer: AdminEndpointIntegrationTests.AdminTestFactory.Issuer,
            audience: AdminEndpointIntegrationTests.AdminTestFactory.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
