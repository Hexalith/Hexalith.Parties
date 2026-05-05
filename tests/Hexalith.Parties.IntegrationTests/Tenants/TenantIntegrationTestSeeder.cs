using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Client.Subscription;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Events;

using Microsoft.IdentityModel.Tokens;

namespace Hexalith.Parties.IntegrationTests.Tenants;

internal static class TenantIntegrationTestSeeder
{
    private const string Issuer = "hexalith-dev";
    private const string Audience = "hexalith-parties";
    private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

    internal static InMemoryTenantProjectionStore CreateProjectionStore(params TenantMemberSeed[] members)
    {
        InMemoryTenantProjectionStore store = new();
        foreach (IGrouping<string, TenantMemberSeed> tenantGroup in members.GroupBy(m => m.TenantId, StringComparer.Ordinal))
        {
            var state = new TenantLocalState
            {
                TenantId = tenantGroup.Key,
                Name = $"Test {tenantGroup.Key}",
                Status = TenantStatus.Active,
            };

            foreach (TenantMemberSeed member in tenantGroup)
            {
                state.Members[member.UserId] = member.Role;
            }

            store.SaveAsync(state).GetAwaiter().GetResult();
        }

        return store;
    }

    internal static async Task SeedActiveTenantAsync(
        HttpClient client,
        string tenantId,
        IReadOnlyCollection<TenantMemberSeed> members,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(members);

        await PublishTenantEventAsync(
            client,
            tenantId,
            new TenantCreated(tenantId, $"Test {tenantId}", null, DateTimeOffset.UtcNow),
            sequenceNumber: 1,
            cancellationToken).ConfigureAwait(false);

        long sequenceNumber = 2;
        foreach (TenantMemberSeed member in members)
        {
            await PublishTenantEventAsync(
                client,
                tenantId,
                new UserAddedToTenant(tenantId, member.UserId, member.Role),
                sequenceNumber++,
                cancellationToken).ConfigureAwait(false);
        }
    }

    internal static Task DisableTenantAsync(
        HttpClient client,
        string tenantId,
        long sequenceNumber = 100,
        CancellationToken cancellationToken = default)
        => PublishTenantEventAsync(
            client,
            tenantId,
            new TenantDisabled(tenantId, DateTimeOffset.UtcNow),
            sequenceNumber,
            cancellationToken);

    internal static Task RemoveUserFromTenantAsync(
        HttpClient client,
        string tenantId,
        string userId,
        long sequenceNumber = 100,
        CancellationToken cancellationToken = default)
        => PublishTenantEventAsync(
            client,
            tenantId,
            new UserRemovedFromTenant(tenantId, userId),
            sequenceNumber,
            cancellationToken);

    internal static string CreateToken(string tenantId, string userId, bool includeAdminRole = false)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", userId),
            new("eventstore:tenant", tenantId),
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

    private static async Task PublishTenantEventAsync<TEvent>(
        HttpClient client,
        string tenantId,
        TEvent @event,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        var envelope = new TenantEventEnvelope(
            MessageId: Guid.NewGuid().ToString("N"),
            AggregateId: tenantId,
            TenantId: "system",
            EventTypeName: typeof(TEvent).FullName!,
            SequenceNumber: sequenceNumber,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString("N"),
            SerializationFormat: "json",
            Payload: JsonSerializer.SerializeToUtf8Bytes(@event));

        using HttpResponseMessage response = await client
            .PostAsJsonAsync("/tenants/events", envelope, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}

internal sealed record TenantMemberSeed(string TenantId, string UserId, TenantRole Role);
