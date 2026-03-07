using System.Security.Cryptography;

using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class PartyKeyManagementService(
    IKeyStorageBackend backend,
    IKeyOperationAuditService auditService) : IPartyKeyManagementService
{
    private const int KeySizeBytes = 32; // AES-256

    public async Task<PartyKeyInfo> CreateKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        byte[] keyMaterial = RandomNumberGenerator.GetBytes(KeySizeBytes);
        try
        {
            int version = 1;
            string keyPath = BuildKeyPath(tenantId, partyId, version);

            await backend.CreateSecretAsync(keyPath, keyMaterial, cancellationToken).ConfigureAwait(false);

            var keyInfo = new PartyKeyInfo
            {
                KeyId = keyPath,
                Version = version,
                TenantId = tenantId,
                PartyId = partyId,
                Algorithm = EncryptionAlgorithm.AES256GCM,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await auditService.RecordOperationAsync(
                new KeyOperationAuditEntry
                {
                    OperationType = KeyOperationType.Create,
                    TenantId = tenantId,
                    PartyId = partyId,
                    KeyVersion = version,
                    Timestamp = DateTimeOffset.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString(),
                },
                cancellationToken).ConfigureAwait(false);

            return keyInfo;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyMaterial);
        }
    }

    public async Task<byte[]> GetKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<int> versions = await backend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        int latestVersion = versions.Max();
        return await GetKeyVersionAsync(tenantId, partyId, latestVersion, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> GetKeyVersionAsync(string tenantId, string partyId, int version, CancellationToken cancellationToken = default)
    {
        string keyPath = BuildKeyPath(tenantId, partyId, version);
        return await backend.ReadSecretAsync(keyPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PartyKeyInfo> RotateKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<int> versions = await backend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        int newVersion = versions.Max() + 1;

        byte[] keyMaterial = RandomNumberGenerator.GetBytes(KeySizeBytes);
        try
        {
            string keyPath = BuildKeyPath(tenantId, partyId, newVersion);
            await backend.CreateSecretAsync(keyPath, keyMaterial, cancellationToken).ConfigureAwait(false);

            var keyInfo = new PartyKeyInfo
            {
                KeyId = keyPath,
                Version = newVersion,
                TenantId = tenantId,
                PartyId = partyId,
                Algorithm = EncryptionAlgorithm.AES256GCM,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await auditService.RecordOperationAsync(
                new KeyOperationAuditEntry
                {
                    OperationType = KeyOperationType.Rotate,
                    TenantId = tenantId,
                    PartyId = partyId,
                    KeyVersion = newVersion,
                    Timestamp = DateTimeOffset.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString(),
                },
                cancellationToken).ConfigureAwait(false);

            return keyInfo;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyMaterial);
        }
    }

    public async Task<ErasureCertificate> DeleteKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<int> versions = await backend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);

        await backend.DeleteAllVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);

        // Verification: read-back to confirm deletion
        IReadOnlyList<int> remaining = await backend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        ErasureVerificationStatus verificationStatus = remaining.Count == 0
            ? ErasureVerificationStatus.Verified
            : ErasureVerificationStatus.Failed;

        await auditService.RecordOperationAsync(
            new KeyOperationAuditEntry
            {
                OperationType = KeyOperationType.Delete,
                TenantId = tenantId,
                PartyId = partyId,
                KeyVersion = 0,
                Timestamp = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid().ToString(),
            },
            cancellationToken).ConfigureAwait(false);

        return new ErasureCertificate
        {
            PartyId = partyId,
            TenantId = tenantId,
            Timestamp = DateTimeOffset.UtcNow,
            KeyVersionsDestroyed = versions.ToList(),
            VerificationStatus = verificationStatus,
        };
    }

    internal static string BuildKeyPath(string tenantId, string partyId, int version)
        => $"{tenantId}/parties/{partyId}/v{version}";
}
