using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.IntegrationTests.HealthChecks;

using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Gateway;

[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("PartiesAspireTopology")]
public sealed class EventStoreGatewayE2ETests
{
    private readonly PartiesAspireTopologyFixture _fixture;

    public EventStoreGatewayE2ETests(PartiesAspireTopologyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreatePartyCommand_ThroughEventStoreGateway_CompletesWithPersistedEventCountAsync()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        string partyId = Guid.NewGuid().ToString("D");
        string correlationId = $"cmd-{Guid.NewGuid():N}";
        HttpClient client = _fixture.EventStoreClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GatewayJwt.GenerateToken());

        var request = new
        {
            messageId = correlationId,
            tenant = "tenant-a",
            domain = "party",
            aggregateId = partyId,
            commandType = typeof(CreatePartyComposite).FullName,
            payload = JsonSerializer.SerializeToElement(new CreatePartyComposite
            {
                PartyId = partyId,
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            }),
        };

        using HttpResponseMessage submit = await client.PostAsJsonAsync("/api/v1/commands", request).ConfigureAwait(true);

        submit.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        JsonDocument status = await PollStatusAsync(client, correlationId).ConfigureAwait(true);
        ReadString(status, "status").ShouldBe("Completed");
        ReadInt(status, "eventCount").ShouldBeGreaterThan(0);
        ReadString(status, "aggregateId").ShouldBe(partyId);
    }

    private static async Task<JsonDocument> PollStatusAsync(HttpClient client, string correlationId)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        JsonDocument? lastStatus = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync($"/api/v1/commands/status/{correlationId}").ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                lastStatus?.Dispose();
                lastStatus = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                string? status = ReadStringOrNull(lastStatus, "status");
                if (status is "Completed" or "Rejected" or "PublishFailed" or "TimedOut")
                {
                    return lastStatus;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }

        lastStatus?.Dispose();
        throw new TimeoutException($"Command {correlationId} did not reach a terminal status within 30 seconds.");
    }

    private static string ReadString(JsonDocument document, string propertyName)
        => ReadStringOrNull(document, propertyName)
        ?? throw new InvalidOperationException($"Missing JSON string property '{propertyName}'.");

    private static string? ReadStringOrNull(JsonDocument document, string propertyName)
        => TryGetProperty(document, propertyName, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static int ReadInt(JsonDocument document, string propertyName)
        => TryGetProperty(document, propertyName, out JsonElement element) && element.ValueKind == JsonValueKind.Number
            ? element.GetInt32()
            : throw new InvalidOperationException($"Missing JSON number property '{propertyName}'.");

    private static bool TryGetProperty(JsonDocument document, string propertyName, out JsonElement element)
    {
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                element = property.Value;
                return true;
            }
        }

        element = default;
        return false;
    }

    private static class GatewayJwt
    {
        private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars!";
        private const string Issuer = "hexalith-dev";
        private const string Audience = "hexalith-eventstore";

        public static string GenerateToken()
        {
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                    new(PartiesClaimTypes.Subject, "story-12-4-e2e-user"),
                    new("tenants", JsonSerializer.Serialize(new[] { "tenant-a" })),
                    new("domains", JsonSerializer.Serialize(new[] { "party" })),
                    new("permissions", JsonSerializer.Serialize(new[] { "commands:*", "query:read" })),
                ]),
                NotBefore = DateTime.UtcNow.AddMinutes(-1),
                Expires = DateTime.UtcNow.AddMinutes(30),
                IssuedAt = DateTime.UtcNow,
                Issuer = Issuer,
                Audience = Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                    SecurityAlgorithms.HmacSha256Signature),
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(descriptor));
        }
    }
}
