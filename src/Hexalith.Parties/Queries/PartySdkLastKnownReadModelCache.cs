using Hexalith.Parties.Projections.Models;

namespace Hexalith.Parties.Queries;

/// <summary>
/// Keeps tenant-scoped last-known SDK read models for bounded degraded reads. Active read tokens
/// are bounded separately by the value capacity. Eviction invalidates only the affected key's
/// tokens so an earlier canonical read cannot restore an evicted value.
/// </summary>
public sealed class PartySdkLastKnownReadModelCache
{
    private const int DefaultMaximumEntries = 1024;
    private static readonly TimeSpan s_defaultRetention = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, PartySdkLastKnownReadModelCacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<long, (string Key, bool Superseded)> _activeReads = new();
    private readonly object _gate = new();
    private readonly int _maximumEntries;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _retention;
    private long _lastReadToken;

    /// <summary>Initializes a cache with bounded production defaults.</summary>
    public PartySdkLastKnownReadModelCache()
        : this(TimeProvider.System, DefaultMaximumEntries, s_defaultRetention)
    {
    }

    /// <summary>
    /// Initializes a cache with the DI-registered clock and bounded production defaults.
    /// Preferred by the container when <see cref="TimeProvider"/> is registered so host
    /// overrides of <c>TryAddSingleton(TimeProvider.System)</c> reach retention checks.
    /// </summary>
    /// <param name="timeProvider">The clock used for retention checks.</param>
    public PartySdkLastKnownReadModelCache(TimeProvider timeProvider)
        : this(timeProvider, DefaultMaximumEntries, s_defaultRetention)
    {
    }

    /// <summary>Initializes a cache with explicit time, capacity, and retention controls.</summary>
    /// <param name="timeProvider">The clock used for retention checks.</param>
    /// <param name="maximumEntries">The separate limits for cached values and active read tokens across all slots.</param>
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

    /// <summary>
    /// Registers a canonical read until <see cref="EndRead"/> or eviction. The returned token
    /// must still be registered for <paramref name="cacheKey"/> when the read completes.
    /// </summary>
    /// <param name="cacheKey">The exact cache key that will later be stored or evicted.</param>
    /// <returns>A unique positive token, or zero when tracking capacity or token space is exhausted.
    /// An untracked read can never store a value.</returns>
    public long BeginRead(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        lock (_gate)
        {
            if (_activeReads.Count >= _maximumEntries || _lastReadToken == long.MaxValue)
            {
                return 0;
            }

            long generation = ++_lastReadToken;
            _activeReads.Add(generation, (cacheKey, false));
            return generation;
        }
    }

    /// <summary>Ends a read without retaining its key. Repeated calls and untracked tokens are harmless.</summary>
    /// <param name="generation">The token returned by <see cref="BeginRead"/>.</param>
    public void EndRead(long generation)
    {
        lock (_gate)
        {
            _ = _activeReads.Remove(generation);
        }
    }

    /// <summary>Attempts to store a detail value within a temporary read registration.
    /// Skips storage if tracking capacity or token space is exhausted.</summary>
    public void StoreDetail(string tenantId, string partyId, PartyDetailSdkReadModel value)
        => Store(PartySdkReadModelAddresses.Detail(tenantId, partyId), value);

    /// <summary>Stores a detail value only while its read token is registered for that key.</summary>
    public bool StoreDetailIfCurrent(string tenantId, string partyId, long generation, PartyDetailSdkReadModel value)
        => StoreIfCurrent(PartySdkReadModelAddresses.Detail(tenantId, partyId), generation, value);

    /// <summary>Attempts to get a non-expired detail value.</summary>
    public bool TryGetDetail(string tenantId, string partyId, out PartyDetailSdkReadModel? value)
        => TryGet(PartySdkReadModelAddresses.Detail(tenantId, partyId), out value);

    /// <summary>Evicts a detail entry and invalidates reads that began before the eviction of that key.</summary>
    public void EvictDetail(string tenantId, string partyId)
        => Evict(PartySdkReadModelAddresses.Detail(tenantId, partyId));

    /// <summary>Attempts to store an index value within a temporary read registration.
    /// Skips storage if tracking capacity or token space is exhausted.</summary>
    public void StoreIndex(string tenantId, PartyIndexSdkReadModel value)
        => Store(PartySdkReadModelAddresses.Index(tenantId), value);

    /// <summary>Stores an index value only while its read token is registered for that key.</summary>
    public bool StoreIndexIfCurrent(string tenantId, long generation, PartyIndexSdkReadModel value)
        => StoreIfCurrent(PartySdkReadModelAddresses.Index(tenantId), generation, value);

    /// <summary>Attempts to get a non-expired index value.</summary>
    public bool TryGetIndex(string tenantId, out PartyIndexSdkReadModel? value)
        => TryGet(PartySdkReadModelAddresses.Index(tenantId), out value);

    /// <summary>Evicts a tenant index and invalidates reads that began before the eviction of that key.</summary>
    public void EvictIndex(string tenantId)
        => Evict(PartySdkReadModelAddresses.Index(tenantId));

    /// <summary>Attempts to store a processing value within a temporary read registration.
    /// Skips storage if tracking capacity or token space is exhausted.</summary>
    public void StoreProcessing(string tenantId, string partyId, PartyProcessingSdkReadModel value)
        => Store(PartySdkReadModelAddresses.Processing(tenantId, partyId), value, orderAdmissions: true);

    /// <summary>Stores a processing value only while its read token is registered for that key.</summary>
    public bool StoreProcessingIfCurrent(
        string tenantId,
        string partyId,
        long generation,
        PartyProcessingSdkReadModel value)
        => StoreIfCurrent(PartySdkReadModelAddresses.Processing(tenantId, partyId), generation, value, orderAdmissions: true);

    /// <summary>
    /// Admits a verified processing value unless a later read already supplied fallback.
    /// Returns false only if the underlying read registration is invalid.
    /// </summary>
    internal bool AcceptProcessingRead(
        string tenantId,
        string partyId,
        long generation,
        PartyProcessingSdkReadModel value,
        string? etag)
        => StoreIfCurrent(PartySdkReadModelAddresses.Processing(tenantId, partyId), generation, value, etag, orderAdmissions: true, acceptSuperseded: true);

    /// <summary>Checks the eviction fence for a verified processing read that supplies no fallback value.</summary>
    internal bool IsProcessingReadCurrent(string tenantId, string partyId, long generation)
    {
        lock (_gate)
        {
            return _activeReads.TryGetValue(generation, out var read)
                && read.Key == PartySdkReadModelAddresses.Processing(tenantId, partyId);
        }
    }

    /// <summary>Retires earlier inline fallback after verified bucketed history or authoritative absence.</summary>
    internal bool RetireEarlierProcessingFallback(string tenantId, string partyId, long generation, bool invalidateEarlier = false)
    {
        string key = PartySdkReadModelAddresses.Processing(tenantId, partyId);
        lock (_gate)
        {
            if (!_activeReads.TryGetValue(generation, out var read) || read.Key != key)
            {
                return false;
            }

            if (!read.Superseded)
            {
                if (invalidateEarlier)
                {
                    InvalidateReads(key, generation);
                }
                else
                {
                    SupersedeEarlierReads(key, generation);
                }

                _ = _entries.Remove(key);
            }

            return true;
        }
    }

    /// <summary>Attempts to get a non-expired processing value.</summary>
    public bool TryGetProcessing(string tenantId, string partyId, out PartyProcessingSdkReadModel? value)
        => TryGet(PartySdkReadModelAddresses.Processing(tenantId, partyId), out value);

    /// <summary>Evicts a processing value and invalidates reads that began before the eviction of that key.</summary>
    public void EvictProcessing(string tenantId, string partyId)
        => Evict(PartySdkReadModelAddresses.Processing(tenantId, partyId));

    /// <summary>Removes a rejected processing revision without invalidating newer reads.</summary>
    internal void RejectProcessingRevision(string tenantId, string partyId, long generation,
        PartyProcessingSdkReadModel rejected, string? etag)
    {
        string key = PartySdkReadModelAddresses.Processing(tenantId, partyId);
        lock (_gate)
        {
            if (!_activeReads.TryGetValue(generation, out var read) || read.Key != key)
            {
                return;
            }

            InvalidateReads(key, generation);
            if (_entries.TryGetValue(key, out var entry)
                && (entry.ETag is null ? ReferenceEquals(entry.Value, rejected) : string.Equals(entry.ETag, etag, StringComparison.Ordinal)))
            {
                _ = _entries.Remove(key);
            }
        }
    }

    private void Evict(string key)
    {
        lock (_gate)
        {
            InvalidateReads(key, _lastReadToken);
            _ = _entries.Remove(key);
        }
    }

    private void Store<TValue>(string key, TValue value, bool orderAdmissions = false)
        where TValue : class
    {
        long generation = BeginRead(key);
        try
        {
            _ = StoreIfCurrent(key, generation, value, orderAdmissions: orderAdmissions);
        }
        finally
        {
            EndRead(generation);
        }
    }

    private bool StoreIfCurrent<TValue>(string key, long generation, TValue value, string? etag = null,
        bool orderAdmissions = false, bool acceptSuperseded = false)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            if (!_activeReads.TryGetValue(generation, out var read) || read.Key != key)
            {
                return false;
            }

            if (read.Superseded)
            {
                return acceptSuperseded;
            }

            if (orderAdmissions)
            {
                SupersedeEarlierReads(key, generation);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            RemoveExpired(now);
            while (_entries.Count >= _maximumEntries && !_entries.ContainsKey(key))
            {
                string oldestKey = _entries.MinBy(static pair => pair.Value.StoredAt).Key;
                _ = _entries.Remove(oldestKey);
            }

            _entries[key] = new PartySdkLastKnownReadModelCacheEntry(value, now, etag);
            return true;
        }
    }

    private void SupersedeEarlierReads(string key, long generation)
    {
        // An older overlapping read stays superseded even if the newer entry expires.
        long[] superseded = _activeReads.Where(pair => pair.Key < generation && pair.Value.Key == key)
            .Select(static pair => pair.Key).ToArray();
        foreach (long prior in superseded)
        {
            _activeReads[prior] = (key, true);
        }
    }

    private void InvalidateReads(string key, long throughGeneration)
    {
        long[] invalidated = _activeReads.Where(pair => pair.Key <= throughGeneration && pair.Value.Key == key)
            .Select(static pair => pair.Key).ToArray();
        foreach (long token in invalidated)
        {
            _ = _activeReads.Remove(token);
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
