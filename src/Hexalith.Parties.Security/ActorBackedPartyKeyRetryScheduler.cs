using Dapr.Actors;
using Dapr.Actors.Client;

namespace Hexalith.Parties.Security;

public sealed class ActorBackedPartyKeyRetryScheduler(IActorProxyFactory actorProxyFactory) : IPartyKeyRetryScheduler
{
    public Task MarkPendingAsync(string tenantId, string partyId, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return CreateProxy(tenantId, partyId).ScheduleRetryAsync(
            new CryptoPendingRecord
            {
                TenantId = tenantId,
                PartyId = partyId,
                LastError = reason,
                FirstMarkedAt = DateTimeOffset.UtcNow,
                LastAttemptedAt = DateTimeOffset.UtcNow,
                AttemptCount = 0,
            });
    }

    public Task ClearPendingAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        return CreateProxy(tenantId, partyId).ClearRetryAsync();
    }

    public Task<bool> IsPendingAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        return CreateProxy(tenantId, partyId).IsPendingAsync();
    }

    private IPartyKeyRetryActor CreateProxy(string tenantId, string partyId)
        => actorProxyFactory.CreateActorProxy<IPartyKeyRetryActor>(
            new ActorId($"{tenantId}:{partyId}"),
            nameof(PartyKeyRetryActor));
}
