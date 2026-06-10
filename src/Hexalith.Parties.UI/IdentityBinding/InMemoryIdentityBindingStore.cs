using System.Collections.Concurrent;

namespace Hexalith.Parties.UI.IdentityBinding;

public sealed class InMemoryIdentityBindingStore : IIdentityBindingStore
{
    private readonly ConcurrentDictionary<IdentityBindingKey, IdentityBindingRecord> _bindings = new();

    public Task<IdentityBindingRecord?> GetAsync(
        IdentityBindingKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        _bindings.TryGetValue(key, out IdentityBindingRecord? binding);
        return Task.FromResult(binding);
    }

    public Task<IdentityBindingRecord> CreateAsync(
        IdentityBindingRecord binding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_bindings.TryAdd(binding.Key, binding))
        {
            throw new IdentityBindingStoreConflictException("BindingAlreadyExists");
        }

        return Task.FromResult(binding);
    }

    public Task<IdentityBindingRecord> ReplaceAsync(
        IdentityBindingRecord binding,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        cancellationToken.ThrowIfCancellationRequested();

        while (true)
        {
            if (!_bindings.TryGetValue(binding.Key, out IdentityBindingRecord? current))
            {
                throw new IdentityBindingStoreConflictException("BindingNotFound");
            }

            if (current.Version != expectedVersion)
            {
                throw new IdentityBindingStoreConflictException("VersionConflict");
            }

            if (_bindings.TryUpdate(binding.Key, binding, current))
            {
                return Task.FromResult(binding);
            }
        }
    }

    public Task DeleteAsync(
        IdentityBindingKey key,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        while (true)
        {
            if (!_bindings.TryGetValue(key, out IdentityBindingRecord? current))
            {
                throw new IdentityBindingStoreConflictException("BindingNotFound");
            }

            if (current.Version != expectedVersion)
            {
                throw new IdentityBindingStoreConflictException("VersionConflict");
            }

            if (_bindings.TryRemove(new KeyValuePair<IdentityBindingKey, IdentityBindingRecord>(key, current)))
            {
                return Task.CompletedTask;
            }
        }
    }
}
