using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Injects per-instance read outages and delayed responses over a shared SDK store.</summary>
/// <param name="inner">The shared canonical store.</param>
internal sealed class PartySdkFaultingReadModelStore(IReadModelStore inner) : IReadModelStore
{
    /// <summary>Gets the canonical keys whose reads fail for this instance.</summary>
    public HashSet<string> UnavailableKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets a hook after the canonical value is captured and before it is returned.</summary>
    public Func<string, CancellationToken, Task>? AfterReadAsync { get; set; }

    /// <inheritdoc/>
    public async Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default)
        where TValue : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (UnavailableKeys.Contains(key))
        {
            throw new InvalidOperationException("canonical-read-unavailable");
        }

        ReadModelEntry<TValue> read = await inner.GetAsync<TValue>(storeName, key, cancellationToken).ConfigureAwait(false);
        if (AfterReadAsync is { } afterRead)
        {
            await afterRead(key, cancellationToken).ConfigureAwait(false);
        }

        return read;
    }

    /// <inheritdoc/>
    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default)
        where TValue : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default)
        where TValue : class
        => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
}
