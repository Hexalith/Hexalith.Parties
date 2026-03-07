namespace Hexalith.Parties.Contracts.Security;

public interface IKeyStorageBackend
{
    Task CreateSecretAsync(string keyPath, byte[] keyMaterial, CancellationToken cancellationToken = default);

    Task<byte[]> ReadSecretAsync(string keyPath, CancellationToken cancellationToken = default);

    Task DeleteSecretAsync(string keyPath, CancellationToken cancellationToken = default);

    Task DeleteAllVersionsAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> ListKeyVersionsAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);
}
