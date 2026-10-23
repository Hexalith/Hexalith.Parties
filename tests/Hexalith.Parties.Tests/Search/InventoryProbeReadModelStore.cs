using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Serialized SDK-store fault controls confined to the ingestion prefix.</summary>
internal sealed class InventoryProbeReadModelStore : IReadModelStore, IReadModelConditionalEraser
{
    internal PartyMemoryTestReadModelStore Inner { get; } = new();
    internal bool DenyInventoryWrites { get; set; }
    internal Exception? ReadFailure { get; set; }
    internal Exception? EraseFailure { get; set; }
    internal Action? AfterSave { get; set; }
    internal bool RejectErase { get; set; }
    /// <summary>Holds erasure until the supplied cleanup budget expires.</summary>
    internal bool WaitForEraseCancellation { get; set; }
    internal string? SentinelKey { get; private set; }
    internal int EraseAttempts { get; private set; }
    internal CancellationToken EraseToken { get; private set; }

    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default) where TValue : class
    {
        if (key == SentinelKey && ReadFailure is { } failure)
        {
            ReadFailure = null;
            return Task.FromException<ReadModelEntry<TValue>>(failure);
        }
        return Inner.GetAsync<TValue>(storeName, key, cancellationToken);
    }

    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default) where TValue : class
        => Inner.SaveAsync(storeName, key, value, cancellationToken);

    public async Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default) where TValue : class
    {
        if (key.StartsWith("parties:memory-ingestion:", StringComparison.Ordinal))
        {
            if (DenyInventoryWrites) throw new UnauthorizedAccessException("private backend ACL details");
            bool saved = await Inner.TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
            if (saved) { SentinelKey = key; AfterSave?.Invoke(); }
            return saved;
        }
        return await Inner.TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken cancellationToken = default)
    {
        EraseAttempts++;
        EraseToken = cancellationToken;
        if (EraseFailure is not null) throw EraseFailure;
        if (WaitForEraseCancellation)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        return !RejectErase && await Inner.TryEraseAsync(storeName, key, etag, cancellationToken).ConfigureAwait(false);
    }

    public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key, CancellationToken cancellationToken = default)
        => Inner.TryReadEtagAsync(storeName, key, cancellationToken);
}
