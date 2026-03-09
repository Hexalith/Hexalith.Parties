using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;

using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class CachedPartyKeyManagementService(IPartyKeyManagementService inner) : IPartyKeyManagementService
{
    private static readonly TimeSpan MinTtl = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan TtlJitter = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    static CachedPartyKeyManagementService()
    {
        PartyKeyManagementService.s_meter.CreateObservableGauge(
            "parties.keys.cache_hit_ratio",
            observeValue: () =>
            {
                long hits = Interlocked.Read(ref s_sharedHits);
                long misses = Interlocked.Read(ref s_sharedMisses);
                long total = hits + misses;
                return total == 0 ? 0.0 : (double)hits / total;
            },
            description: "Cache hit ratio for key lookups");
    }

    private static long s_sharedHits;
    private static long s_sharedMisses;

    public Task<PartyKeyInfo> CreateKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
        => inner.CreateKeyAsync(tenantId, partyId, cancellationToken);

    public async Task<byte[]> GetKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildCacheKey(tenantId, partyId);

        if (_cache.TryGetValue(cacheKey, out CacheEntry? entry) && !entry.IsExpired)
        {
            Interlocked.Increment(ref s_sharedHits);
            return (byte[])entry.KeyMaterial.Clone();
        }

        Interlocked.Increment(ref s_sharedMisses);

        byte[] key = await inner.GetKeyAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);

        EvictIfPresent(cacheKey);
        _cache[cacheKey] = new CacheEntry((byte[])key.Clone(), DateTimeOffset.UtcNow + JitteredTtl());

        return key;
    }

    public async Task<byte[]> GetKeyVersionAsync(string tenantId, string partyId, int version, CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildVersionedCacheKey(tenantId, partyId, version);

        if (_cache.TryGetValue(cacheKey, out CacheEntry? entry) && !entry.IsExpired)
        {
            Interlocked.Increment(ref s_sharedHits);
            return (byte[])entry.KeyMaterial.Clone();
        }

        Interlocked.Increment(ref s_sharedMisses);

        byte[] key = await inner.GetKeyVersionAsync(tenantId, partyId, version, cancellationToken).ConfigureAwait(false);

        EvictIfPresent(cacheKey);
        _cache[cacheKey] = new CacheEntry((byte[])key.Clone(), DateTimeOffset.UtcNow + JitteredTtl());

        return key;
    }

    public async Task<PartyKeyInfo> RotateKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        PartyKeyInfo result = await inner.RotateKeyAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        EvictAllForParty(tenantId, partyId);
        return result;
    }

    public async Task<ErasureCertificate> DeleteKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        ErasureCertificate result = await inner.DeleteKeyAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        EvictAllForParty(tenantId, partyId);
        return result;
    }

    private void EvictAllForParty(string tenantId, string partyId)
    {
        string prefix = $"{tenantId}:{partyId}";
        foreach (string key in _cache.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                EvictIfPresent(key);
            }
        }
    }

    private void EvictIfPresent(string cacheKey)
    {
        if (_cache.TryRemove(cacheKey, out CacheEntry? removed))
        {
            CryptographicOperations.ZeroMemory(removed.KeyMaterial);
        }
    }

    private static string BuildCacheKey(string tenantId, string partyId) => $"{tenantId}:{partyId}";

    private static string BuildVersionedCacheKey(string tenantId, string partyId, int version) => $"{tenantId}:{partyId}:v{version}";

    private static TimeSpan JitteredTtl() => MinTtl + (TtlJitter * Random.Shared.NextDouble());

    private sealed record CacheEntry(byte[] KeyMaterial, DateTimeOffset ExpiresAt)
    {
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    }
}
