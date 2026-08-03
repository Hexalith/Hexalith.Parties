using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Parties.Tests.HealthChecks;

internal sealed class CancelingReadModelStore : IReadModelStore
{
    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
        string storeName,
        string key,
        CancellationToken cancellationToken = default)
        where TValue : class
        => Task.FromCanceled<ReadModelEntry<TValue>>(cancellationToken);

    public Task SaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        CancellationToken cancellationToken = default)
        where TValue : class
        => throw new NotSupportedException();

    public Task<bool> TrySaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        string etag,
        CancellationToken cancellationToken = default)
        where TValue : class
        => throw new NotSupportedException();
}
