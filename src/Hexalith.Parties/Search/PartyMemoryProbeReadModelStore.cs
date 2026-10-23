using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Parties.Search;

/// <summary>Routes only one reserved probe's ordinary registration CAS through the SDK expiry capability.</summary>
internal sealed class PartyMemoryProbeReadModelStore(
    IReadModelStore store,
    IReadModelExpiringStore expiring,
    string probeStore,
    string probeKey) : IReadModelStore
{
    /// <summary>The maximum lifetime of an uncollected probe; normal ingestion does not use this adapter.</summary>
    internal static TimeSpan Retention => TimeSpan.FromMinutes(1);

    /// <inheritdoc/>
    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default) where TValue : class
        => store.GetAsync<TValue>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default) where TValue : class
        => throw new NotSupportedException("Probe registration requires conditional writes.");

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default) where TValue : class
    {
        if (storeName != probeStore || key != probeKey)
        {
            throw new InvalidOperationException("Probe expiry cannot be applied to another inventory.");
        }
        return expiring.TrySaveWithTimeToLiveAsync(storeName, key, value, etag, Retention, cancellationToken);
    }
}
