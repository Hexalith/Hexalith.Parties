using Hexalith.Parties.Projections.Models;

namespace Hexalith.Parties.Queries;

/// <summary>
/// Keeps tenant-scoped last-known SDK read models for bounded degraded reads. Eviction advances a
/// generation so a canonical read that started before erasure cannot restore an evicted value.
/// </summary>
public sealed class PartySdkLastKnownReadModelCache
{
    private const int DefaultMaximumEntries = 1024;
    private static readonly TimeSpan s_defaultRetention = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, PartySdkLastKnownReadModelCacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly int _maximumEntries;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _retention;
    private long _generation;

    /// <summary>Initializes a cache with bounded production defaults.</summary>
    public PartySdkLastKnownReadModelCache()
        : this(TimeProvider.System, DefaultMaximumEntries, s_defaultRetention)
    {
    }

    /// <summary>Initializes a cache with explicit time, capacity, and retention controls.</summary>
    /// <param name="timeProvider">The clock used for retention checks.</param>
    /// <param name="maximumEntries">The maximum number of cached values across all slots.</param>
    /// <param name="retention">The maximum age of a cached value.</param>
    public PartySdkLastKnownReadModelCache(
        TimeProvider timeProvider,
        int maximumEntries,
        TimeSpan retention)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumEntries, 1);
        if (retention <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retention));
        }

        _timeProvider = timeProvider;
        _maximumEntries = maximumEntries;
        _retention = retention;
    }

    /// <summary>Captures the generation that must still be current when an asynchronous read completes.</summary>
    /// <returns>The current invalidation generation.</returns>
    public long BeginRead()
    {
        lock (_gate)
        {
            return _generation;
        }
    }

    /// <summary>Stores a detail value using the current generation.</summary>
    public void StoreDetail(string tenantId, string partyId, PartyDetailSdkReadModel value)
        => _ = StoreDetailIfCurrent(tenantId, partyId, BeginRead(), value);

    /// <summary>Stores a detail value only if no eviction occurred since the read began.</summary>
    public bool StoreDetailIfCurrent(string tenantId, string partyId, long generation, PartyDetailSdkReadModel value)
        => StoreIfCurrent(PartySdkReadModelAddresses.Detail(tenantId, partyId), generation, value);

    /// <summary>Attempts to get a non-expired detail value.</summary>
    public bool TryGetDetail(string tenantId, string partyId, out PartyDetailSdkReadModel? value)
        => TryGet(PartySdkReadModelAddresses.Detail(tenantId, partyId), out value);

    /// <summary>Evicts a detail entry and invalidates reads that began before the eviction.</summary>
    public void EvictDetail(string tenantId, string partyId)
        => Evict(PartySdkReadModelAddresses.Detail(tenantId, partyId));

    /// <summary>Stores an index value using the current generation.</summary>
    public void StoreIndex(string tenantId, PartyIndexSdkReadModel value)
        => _ = StoreIndexIfCurrent(tenantId, BeginRead(), value);

    /// <summary>Stores an index value only if no eviction occurred since the read began.</summary>
    public bool StoreIndexIfCurrent(string tenantId, long generation, PartyIndexSdkReadModel value)
        => StoreIfCurrent(PartySdkReadModelAddresses.Index(tenantId), generation, value);

    /// <summary>Attempts to get a non-expired index value.</summary>
    public bool TryGetIndex(string tenantId, out PartyIndexSdkReadModel? value)
        => TryGet(PartySdkReadModelAddresses.Index(tenantId), out value);

    /// <summary>Evicts a tenant index and invalidates reads that began before the eviction.</summary>
    public void EvictIndex(string tenantId)
        => Evict(PartySdkReadModelAddresses.Index(tenantId));

    /// <summary>Stores a processing value using the current generation.</summary>
    public void StoreProcessing(string tenantId, string partyId, PartyProcessingSdkReadModel value)
        => _ = StoreProcessingIfCurrent(tenantId, partyId, BeginRead(), value);

    /// <summary>Stores a processing value only if no eviction occurred since the read began.</summary>
    public bool StoreProcessingIfCurrent(
        string tenantId,
        string partyId,
        long generation,
        PartyProcessingSdkReadModel value)
        => StoreIfCurrent(PartySdkReadModelAddresses.Processing(tenantId, partyId), generation, value);

    /// <summary>Attempts to get a non-expired processing value.</summary>
    public bool TryGetProcessing(string tenantId, string partyId, out PartyProcessingSdkReadModel? value)
        => TryGet(PartySdkReadModelAddresses.Processing(tenantId, partyId), out value);

    /// <summary>Evicts a processing value and invalidates reads that began before the eviction.</summary>
    public void EvictProcessing(string tenantId, string partyId)
        => Evict(PartySdkReadModelAddresses.Processing(tenantId, partyId));

    private void Evict(string key)
    {
        lock (_gate)
        {
            _generation++;
            _ = _entries.Remove(key);
        }
    }

    private bool StoreIfCurrent<TValue>(string key, long generation, TValue value)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            if (generation != _generation)
            {
                return false;
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            RemoveExpired(now);
            while (_entries.Count >= _maximumEntries && !_entries.ContainsKey(key))
            {
                string oldestKey = _entries.MinBy(static pair => pair.Value.StoredAt).Key;
                _ = _entries.Remove(oldestKey);
            }

            _entries[key] = new PartySdkLastKnownReadModelCacheEntry(value, now);
            return true;
        }
    }

    private bool TryGet<TValue>(string key, out TValue? value)
        where TValue : class
    {
        lock (_gate)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            RemoveExpired(now);
            if (_entries.TryGetValue(key, out PartySdkLastKnownReadModelCacheEntry? entry)
                && entry.Value is TValue typed)
            {
                value = typed;
                return true;
            }

            value = null;
            return false;
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        string[] expired = _entries
            .Where(pair => now - pair.Value.StoredAt >= _retention)
            .Select(static pair => pair.Key)
            .ToArray();
        foreach (string key in expired)
        {
            _ = _entries.Remove(key);
        }
    }
}
