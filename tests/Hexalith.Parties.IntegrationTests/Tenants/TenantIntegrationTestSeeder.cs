using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Client.Subscription;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Events;

using Microsoft.IdentityModel.Tokens;

namespace Hexalith.Parties.IntegrationTests.Tenants;

/// <summary>
/// Helpers for seeding Tenants-backed authorization state into the running
/// Aspire CommandApi during full-topology tests.
/// <para>
/// <b>Why this seeds via /tenants/events instead of through Hexalith.Tenants commands:</b>
/// In the local Aspire topology used by these tests, Hexalith.Tenants does not yet expose
/// a stable HTTP command surface that Parties tests can call directly to provision tenants
/// and memberships. Until that surface exists, this helper simulates DAPR delivery to
/// Parties' own Tenants event ingress, which exercises Story 11.2's local projection,
/// authorization, and fail-closed behavior end-to-end inside the Parties process.
/// The simulated envelope shape mirrors the production Tenants pub/sub envelope and
/// must be kept in sync with <see cref="TenantEventEnvelope"/> upstream.
/// Follow-up: replace with real Hexalith.Tenants command invocations once the topology
/// exposes them. See deferred-work.md (Story 11-4 review, item D1).
/// </para>
/// </summary>
internal static class TenantIntegrationTestSeeder
{
    private const string Issuer = "hexalith-dev";
    private const string Audience = "hexalith-parties";
    private const string SigningKeyEnvVar = "HEXALITH_PARTIES_TEST_SIGNING_KEY";

    // Per-tenant monotonic sequence counter. Shared across SeedActiveTenantAsync,
    // DisableTenantAsync, and RemoveUserFromTenantAsync so consecutive operations on
    // the same tenant produce strictly-increasing SequenceNumber values that the local
    // Tenants projection accepts in order.
    private static readonly ConcurrentDictionary<string, long> s_sequenceCounters = new(StringComparer.Ordinal);

    private static readonly Lazy<string> s_signingKey = new(ResolveSigningKey);

    internal static async Task<InMemoryTenantProjectionStore> CreateProjectionStoreAsync(params TenantMemberSeed[] members)
    {
        InMemoryTenantProjectionStore store = new();
        foreach (IGrouping<string, TenantMemberSeed> tenantGroup in members.GroupBy(m => m.TenantId, StringComparer.Ordinal))
        {
            await store.SaveAsync(BuildState(tenantGroup)).ConfigureAwait(false);
        }

        return store;
    }

    /// <summary>
    /// Synchronous overload for callers (such as <c>WebApplicationFactory.ConfigureTestServices</c>)
    /// that cannot await. Safe because <see cref="InMemoryTenantProjectionStore.SaveAsync"/>
    /// completes synchronously — no I/O, no SynchronizationContext capture, no deadlock risk.
    /// New async callers should prefer <see cref="CreateProjectionStoreAsync"/>.
    /// </summary>
    internal static InMemoryTenantProjectionStore CreateProjectionStore(params TenantMemberSeed[] members)
    {
        InMemoryTenantProjectionStore store = new();
        foreach (IGrouping<string, TenantMemberSeed> tenantGroup in members.GroupBy(m => m.TenantId, StringComparer.Ordinal))
        {
            // SaveAsync on the in-memory store completes synchronously; safe to GetResult.
            store.SaveAsync(BuildState(tenantGroup)).GetAwaiter().GetResult();
        }

        return store;
    }

    private static TenantLocalState BuildState(IGrouping<string, TenantMemberSeed> tenantGroup)
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

        return state;
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
            NextSequenceNumber(tenantId),
            cancellationToken).ConfigureAwait(false);

        foreach (TenantMemberSeed member in members)
        {
            await PublishTenantEventAsync(
                client,
                tenantId,
                new UserAddedToTenant(tenantId, member.UserId, member.Role),
                NextSequenceNumber(tenantId),
                cancellationToken).ConfigureAwait(false);
        }
    }

    internal static Task DisableTenantAsync(
        HttpClient client,
        string tenantId,
        CancellationToken cancellationToken = default)
        => PublishTenantEventAsync(
            client,
            tenantId,
            new TenantDisabled(tenantId, DateTimeOffset.UtcNow),
            NextSequenceNumber(tenantId),
            cancellationToken);

    internal static Task RemoveUserFromTenantAsync(
        HttpClient client,
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default)
        => PublishTenantEventAsync(
            client,
            tenantId,
            new UserRemovedFromTenant(tenantId, userId),
            NextSequenceNumber(tenantId),
            cancellationToken);

    internal static string CreateToken(string tenantId, string userId, bool includeAdminRole = false)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(s_signingKey.Value));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", userId),
            new("eventstore:tenant", tenantId),
        };

        if (includeAdminRole)
        {
            // Keycloak realm role used by legacy admin-endpoint authorization filters.
            // This is NOT a Hexalith.Tenants role enum (TenantOwner/TenantContributor/TenantReader)
            // and does not authorize tenant-scoped operations on its own.
            claims.Add(new Claim(ClaimTypes.Role, "admin"));
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Resets the per-tenant sequence counters. Call between test runs that re-seed
    /// the same tenant id from a clean projection state.
    /// </summary>
    internal static void ResetSequenceCounters() => s_sequenceCounters.Clear();

    private static long NextSequenceNumber(string tenantId)
        => s_sequenceCounters.AddOrUpdate(tenantId, _ => 1L, (_, current) => current + 1);

    private static string ResolveSigningKey()
    {
        string? configured = Environment.GetEnvironmentVariable(SigningKeyEnvVar);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Length < 32
                ? throw new InvalidOperationException(
                    $"{SigningKeyEnvVar} must be at least 32 characters for HMAC-SHA256.")
                : configured;
        }

        string? appSettingsKey = ReadDevelopmentSigningKey();
        if (!string.IsNullOrWhiteSpace(appSettingsKey))
        {
            return appSettingsKey.Length < 32
                ? throw new InvalidOperationException(
                    "Authentication:JwtBearer:SigningKey in CommandApi development settings must be at least 32 characters for HMAC-SHA256.")
                : appSettingsKey;
        }

        // Generate a cryptographically random per-process key for tests when no
        // environment-provided or CommandApi development key is supplied. Process-stable
        // so all tokens minted in the same test run share the same signing key.
        byte[] random = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(random);
    }

    private static string? ReadDevelopmentSigningKey()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "src",
                "Hexalith.Parties.CommandApi",
                "appsettings.Development.json");
            if (File.Exists(candidate))
            {
                using FileStream stream = File.OpenRead(candidate);
                using JsonDocument document = JsonDocument.Parse(stream);
                if (document.RootElement.TryGetProperty("Authentication:JwtBearer", out JsonElement auth)
                    && auth.TryGetProperty("SigningKey", out JsonElement signingKey))
                {
                    return signingKey.GetString();
                }
            }

            current = current.Parent;
        }

        return null;
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

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Failed to publish simulated Tenants event {typeof(TEvent).Name} for tenant '{tenantId}' to /tenants/events. " +
                $"Status: {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        }
    }
}

internal sealed record TenantMemberSeed(string TenantId, string UserId, TenantRole Role);
