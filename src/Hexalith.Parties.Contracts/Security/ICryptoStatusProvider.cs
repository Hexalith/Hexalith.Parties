namespace Hexalith.Parties.Contracts.Security;

public interface ICryptoStatusProvider
{
    Task<bool> IsCryptoPendingAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);
}
