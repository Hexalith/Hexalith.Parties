using System.Collections.Concurrent;

namespace Hexalith.Parties.UI.IdentityBinding;

public sealed class InMemoryIdentityProviderPartyAttributeClient : IIdentityProviderPartyAttributeClient
{
    private readonly ConcurrentDictionary<IdentityBindingKey, string[]> _partyIds = new();

    public Task SetPartyIdAsync(
        IdentityBindingKey key,
        string partyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        cancellationToken.ThrowIfCancellationRequested();

        _partyIds[key] = [partyId];
        return Task.CompletedTask;
    }

    public Task ClearPartyIdAsync(
        IdentityBindingKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        _partyIds.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetPartyIdsAsync(
        IdentityBindingKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> partyIds = _partyIds.TryGetValue(key, out string[]? values)
            ? values
            : [];
        return Task.FromResult(partyIds);
    }
}
