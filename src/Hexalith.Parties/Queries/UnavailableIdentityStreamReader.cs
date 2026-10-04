using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Parties.Queries;

/// <summary>Fail-closed source when no authenticated SDK gateway reader was composed.</summary>
internal sealed class UnavailableIdentityStreamReader : IAuthoritativeEventStreamReader
{
    public Task<AuthoritativeStreamReadResult> ReadAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AuthoritativeStreamReadResult(null, "source-not-configured"));
    }
}
