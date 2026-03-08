namespace Hexalith.Parties.Security;

public interface IPartyKeyRetryScheduler
{
    Task MarkPendingAsync(string tenantId, string partyId, string reason, CancellationToken cancellationToken = default);

    Task ClearPendingAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);

    Task<bool> IsPendingAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);
}
