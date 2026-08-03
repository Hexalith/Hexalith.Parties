using System.Collections.Concurrent;

using Hexalith.Parties.Security;

namespace Hexalith.Parties.IntegrationTests.Security;

/// <summary>
/// Real in-memory retry scheduler used to isolate encryption integration tests from the retired
/// actor-proxy dependency while preserving pending-state transitions.
/// </summary>
internal sealed class InMemoryPartyKeyRetryScheduler : IPartyKeyRetryScheduler
{
    private readonly ConcurrentDictionary<string, string> _pending = new(StringComparer.Ordinal);

    public Task MarkPendingAsync(string tenantId, string partyId, string reason, CancellationToken cancellationToken = default)
    {
        _pending[Key(tenantId, partyId)] = reason;
        return Task.CompletedTask;
    }

    public Task ClearPendingAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        _ = _pending.TryRemove(Key(tenantId, partyId), out _);
        return Task.CompletedTask;
    }

    public Task<bool> IsPendingAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
        => Task.FromResult(_pending.ContainsKey(Key(tenantId, partyId)));

    private static string Key(string tenantId, string partyId) => $"{tenantId}:{partyId}";
}
