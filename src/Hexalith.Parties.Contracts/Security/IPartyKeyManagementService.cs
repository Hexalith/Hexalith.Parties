namespace Hexalith.Parties.Contracts.Security;

public interface IPartyKeyManagementService
{
    Task<PartyKeyInfo> CreateKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);

    Task<byte[]> GetKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);

    Task<byte[]> GetKeyVersionAsync(string tenantId, string partyId, int version, CancellationToken cancellationToken = default);

    Task<PartyKeyInfo> RotateKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);

    Task<ErasureCertificate> DeleteKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);
}
