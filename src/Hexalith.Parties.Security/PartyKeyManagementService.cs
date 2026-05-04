using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;

using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class PartyKeyManagementService(
    IKeyStorageBackend backend,
    IKeyOperationAuditService auditService,
    ICorrelationContextAccessor correlationContextAccessor) : IPartyKeyManagementService
{
    private const int KeySizeBytes = 32; // AES-256

    internal static readonly Meter s_meter = new("Hexalith.Parties.Security", "1.0.0");
    internal static readonly Counter<long> s_keysCreated = s_meter.CreateCounter<long>("parties.keys.created", description: "Number of encryption keys created");
    internal static readonly Counter<long> s_keysRotated = s_meter.CreateCounter<long>("parties.keys.rotated", description: "Number of encryption keys rotated");
    internal static readonly Counter<long> s_keysDeleted = s_meter.CreateCounter<long>("parties.keys.deleted", description: "Number of encryption keys deleted");
    internal static readonly Counter<long> s_failedOperations = s_meter.CreateCounter<long>("parties.keys.failed_operations", description: "Number of failed key operations");
    internal static readonly Histogram<double> s_backendLatency = s_meter.CreateHistogram<double>("parties.keys.backend_latency_ms", "ms", "Backend operation latency");

    public async Task<PartyKeyInfo> CreateKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        long startTicks = Stopwatch.GetTimestamp();
        try
        {
            IReadOnlyList<int> existingVersions = await backend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
            if (existingVersions.Count > 0)
            {
                int latestVersion = existingVersions.Max();
                return CreateKeyInfo(tenantId, partyId, latestVersion);
            }

            byte[] keyMaterial = RandomNumberGenerator.GetBytes(KeySizeBytes);
            try
            {
                int version = 1;
                string keyPath = BuildKeyPath(tenantId, partyId, version);

                await backend.CreateSecretAsync(keyPath, keyMaterial, cancellationToken).ConfigureAwait(false);

                PartyKeyInfo keyInfo = CreateKeyInfo(tenantId, partyId, version);
                await AuditOperationAsync(KeyOperationType.Create, tenantId, partyId, version, cancellationToken).ConfigureAwait(false);

                s_keysCreated.Add(1, new KeyValuePair<string, object?>("tenant", tenantId));
                return keyInfo;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(keyMaterial);
            }
        }
        catch
        {
            s_failedOperations.Add(1, new KeyValuePair<string, object?>("operation", "create"), new KeyValuePair<string, object?>("tenant", tenantId));
            throw;
        }
        finally
        {
            s_backendLatency.Record(Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds, new KeyValuePair<string, object?>("operation", "create"));
        }
    }

    public async Task<byte[]> GetKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        long startTicks = Stopwatch.GetTimestamp();
        try
        {
            IReadOnlyList<int> versions = await backend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
            if (versions.Count == 0)
            {
                throw new PartyEncryptionKeyDestroyedException(tenantId, partyId);
            }

            int latestVersion = versions.Max();
            return await GetKeyVersionAsync(tenantId, partyId, latestVersion, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            s_backendLatency.Record(Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds, new KeyValuePair<string, object?>("operation", "read"));
        }
    }

    public async Task<byte[]> GetKeyVersionAsync(string tenantId, string partyId, int version, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        string keyPath = BuildKeyPath(tenantId, partyId, version);
        byte[] key;
        try
        {
            key = await backend.ReadSecretAsync(keyPath, cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException ex) when (ex.GetType() != typeof(PartyEncryptionKeyDestroyedException))
        {
            // Backends that surface a raw KeyNotFoundException (e.g., Vault "Secret not found")
            // must be normalized to the typed exception so catch sites recognize the post-erasure
            // condition via PartyEncryptionKeyDestroyedException.IsMatch(...).
            throw new PartyEncryptionKeyDestroyedException(tenantId, partyId, ex);
        }

        await AuditOperationAsync(KeyOperationType.Read, tenantId, partyId, version, cancellationToken).ConfigureAwait(false);
        return key;
    }

    public async Task<PartyKeyInfo> RotateKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        long startTicks = Stopwatch.GetTimestamp();
        try
        {
            IReadOnlyList<int> versions = await backend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
            if (versions.Count == 0)
            {
                throw new PartyEncryptionKeyDestroyedException(tenantId, partyId);
            }

            int newVersion = versions.Max() + 1;

            byte[] keyMaterial = RandomNumberGenerator.GetBytes(KeySizeBytes);
            try
            {
                string keyPath = BuildKeyPath(tenantId, partyId, newVersion);
                await backend.CreateSecretAsync(keyPath, keyMaterial, cancellationToken).ConfigureAwait(false);

                try
                {
                    await AuditOperationAsync(KeyOperationType.Rotate, tenantId, partyId, newVersion, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception auditEx)
                {
                    // Rollback: remove the orphaned secret version if audit recording fails.
                    // Wrap rollback in its own try/catch so the original exception is not lost.
                    try
                    {
                        await backend.DeleteSecretAsync(keyPath, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception rollbackEx)
                    {
                        throw new AggregateException("Key rotation audit failed and rollback also failed. Orphaned key version may exist.", auditEx, rollbackEx);
                    }

                    throw;
                }

                PartyKeyInfo keyInfo = CreateKeyInfo(tenantId, partyId, newVersion);
                s_keysRotated.Add(1, new KeyValuePair<string, object?>("tenant", tenantId));
                return keyInfo;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(keyMaterial);
            }
        }
        catch
        {
            s_failedOperations.Add(1, new KeyValuePair<string, object?>("operation", "rotate"), new KeyValuePair<string, object?>("tenant", tenantId));
            throw;
        }
        finally
        {
            s_backendLatency.Record(Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds, new KeyValuePair<string, object?>("operation", "rotate"));
        }
    }

    public async Task<ErasureCertificate> DeleteKeyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        long startTicks = Stopwatch.GetTimestamp();
        try
        {
            IReadOnlyList<int> versions = await backend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);

            await backend.DeleteAllVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);

            // Verification: read-back to confirm deletion
            IReadOnlyList<int> remaining = await backend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
            ErasureVerificationStatus verificationStatus = remaining.Count == 0
                ? ErasureVerificationStatus.Verified
                : ErasureVerificationStatus.Failed;

            await AuditOperationAsync(KeyOperationType.Delete, tenantId, partyId, 0, cancellationToken).ConfigureAwait(false);

            s_keysDeleted.Add(1, new KeyValuePair<string, object?>("tenant", tenantId));
            return new ErasureCertificate
            {
                PartyId = partyId,
                TenantId = tenantId,
                Timestamp = DateTimeOffset.UtcNow,
                KeyVersionsDestroyed = versions.ToList(),
                VerificationStatus = verificationStatus,
            };
        }
        catch
        {
            s_failedOperations.Add(1, new KeyValuePair<string, object?>("operation", "delete"), new KeyValuePair<string, object?>("tenant", tenantId));
            throw;
        }
        finally
        {
            s_backendLatency.Record(Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds, new KeyValuePair<string, object?>("operation", "delete"));
        }
    }

    private async Task AuditOperationAsync(
        KeyOperationType operationType,
        string tenantId,
        string partyId,
        int keyVersion,
        CancellationToken cancellationToken)
    {
        string correlationId = correlationContextAccessor.CorrelationId ?? Guid.NewGuid().ToString();

        await auditService.RecordOperationAsync(
            new KeyOperationAuditEntry
            {
                OperationType = operationType,
                TenantId = tenantId,
                PartyId = partyId,
                KeyVersion = keyVersion,
                Timestamp = DateTimeOffset.UtcNow,
                CorrelationId = correlationId,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static PartyKeyInfo CreateKeyInfo(string tenantId, string partyId, int version)
        => new()
        {
            KeyId = BuildKeyPath(tenantId, partyId, version),
            Version = version,
            TenantId = tenantId,
            PartyId = partyId,
            Algorithm = EncryptionAlgorithm.AES256GCM,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    internal static string BuildKeyPath(string tenantId, string partyId, int version)
        => $"{tenantId}/parties/{partyId}/v{version}";
}
