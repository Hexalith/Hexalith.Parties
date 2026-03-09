namespace Hexalith.Parties.Contracts.Security;

public interface IPartyKeyLifecycleService : ICryptoStatusProvider
{
    Task OnPartyCreatedAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);

    Task RetryPendingKeyCreationAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);

    Task MarkCryptoPendingAsync(string tenantId, string partyId, string reason, CancellationToken cancellationToken = default);

    Task ClearCryptoPendingAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);
}
