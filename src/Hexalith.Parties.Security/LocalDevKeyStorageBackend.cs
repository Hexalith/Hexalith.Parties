using System.Collections.Concurrent;
using System.Security.Cryptography;

using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class LocalDevKeyStorageBackend : IKeyStorageBackend
{
    private const int MaxSecretSizeBytes = 8192;
    private const int MaxSecretsPerParty = 100;
    private const int TenantKeySizeBytes = 32;

    private readonly ConcurrentDictionary<string, byte[]> _secrets = new();
    private readonly ConcurrentDictionary<string, TenantKeyMetadata> _tenantKeys = new();
    private readonly ConcurrentDictionary<string, byte[]> _tenantKeyMaterial = new();
    private readonly ConcurrentDictionary<string, PartyKeyWrappingMetadata> _wrappingMetadata = new();

    public Task CreateSecretAsync(string keyPath, byte[] keyMaterial, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentNullException.ThrowIfNull(keyMaterial);

        if (keyMaterial.Length > MaxSecretSizeBytes)
        {
            throw new ArgumentException($"Key material exceeds maximum size of {MaxSecretSizeBytes} bytes.", nameof(keyMaterial));
        }

        ValidateNamespace(keyPath);

        string partyPrefix = GetPartyPrefix(keyPath);
        int existingSecrets = _secrets.Keys.Count(k => k.StartsWith(partyPrefix, StringComparison.Ordinal));
        if (existingSecrets >= MaxSecretsPerParty)
        {
            throw new InvalidOperationException(
                $"Party key storage limit exceeded for '{partyPrefix}'. Maximum versions per party is {MaxSecretsPerParty}.");
        }

        byte[] stored = new byte[keyMaterial.Length];
        keyMaterial.CopyTo(stored, 0);

        if (!_secrets.TryAdd(keyPath, stored))
        {
            throw new InvalidOperationException($"Secret already exists at path '{keyPath}'.");
        }

        PartyKeyRecord record = ParsePartyKeyRecord(keyPath);
        TenantKeyMetadata tenantKey = EnsureInitialTenantKey(record.TenantId);
        _wrappingMetadata.TryAdd(
            BuildWrappingKey(record.TenantId, record.PartyId, record.Version),
            new PartyKeyWrappingMetadata
            {
                TenantId = record.TenantId,
                PartyId = record.PartyId,
                KeyVersion = record.Version,
                TenantKeyId = tenantKey.KeyId,
                TenantKeyVersion = tenantKey.Version,
                RotationId = tenantKey.OperationId,
                WrappedAt = DateTimeOffset.UtcNow,
            });

        return Task.CompletedTask;
    }

    public Task<byte[]> ReadSecretAsync(string keyPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);

        if (!_secrets.TryGetValue(keyPath, out byte[]? stored))
        {
            throw new KeyNotFoundException($"Secret not found at path '{keyPath}'.");
        }

        byte[] result = new byte[stored.Length];
        stored.CopyTo(result, 0);
        return Task.FromResult(result);
    }

    public Task DeleteSecretAsync(string keyPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);

        if (_secrets.TryRemove(keyPath, out byte[]? removed))
        {
            CryptographicOperations.ZeroMemory(removed);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAllVersionsAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        string prefix = $"{tenantId}/parties/{partyId}/v";

        foreach (string key in _secrets.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            if (_secrets.TryRemove(key, out byte[]? removed))
            {
                CryptographicOperations.ZeroMemory(removed);
            }

            PartyKeyRecord record = ParsePartyKeyRecord(key);
            _wrappingMetadata.TryRemove(BuildWrappingKey(record.TenantId, record.PartyId, record.Version), out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<int>> ListKeyVersionsAsync(string tenantId, string partyId, CancellationToken cancellationToken = default)
    {
        string prefix = $"{tenantId}/parties/{partyId}/v";

        List<int> versions = _secrets.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => int.Parse(k[prefix.Length..]))
            .OrderBy(v => v)
            .ToList();

        return Task.FromResult<IReadOnlyList<int>>(versions);
    }

    public Task<TenantKeyMetadata> GetOrCreateTenantKeyAsync(string tenantId, string operationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        return Task.FromResult(GetOrCreateTenantKeyCore(tenantId, operationId));
    }

    private TenantKeyMetadata GetOrCreateTenantKeyCore(string tenantId, string operationId)
    {
        TenantKeyMetadata? existingForOperation = _tenantKeys.Values
            .Where(k => string.Equals(k.TenantId, tenantId, StringComparison.Ordinal))
            .FirstOrDefault(k => string.Equals(k.OperationId, operationId, StringComparison.Ordinal));
        if (existingForOperation is not null)
        {
            return existingForOperation;
        }

        // Tight loop guarded by ConcurrentDictionary.TryAdd: if another caller wins the version
        // we just computed, recompute Max+1 and retry rather than overwriting a sibling's
        // freshly-generated random material with our own.
        while (true)
        {
            int nextVersion = _tenantKeys.Values
                .Where(k => string.Equals(k.TenantId, tenantId, StringComparison.Ordinal))
                .Select(k => k.Version)
                .DefaultIfEmpty(0)
                .Max() + 1;

            TenantKeyMetadata candidate = new()
            {
                TenantId = tenantId,
                KeyId = BuildTenantKeyId(tenantId, nextVersion),
                Version = nextVersion,
                ProviderName = "local-dev",
                CreatedAt = DateTimeOffset.UtcNow,
                OperationId = operationId,
            };

            byte[] material = RandomNumberGenerator.GetBytes(TenantKeySizeBytes);
            if (_tenantKeys.TryAdd(candidate.KeyId, candidate))
            {
                _tenantKeyMaterial[candidate.KeyId] = material;
                return candidate;
            }

            CryptographicOperations.ZeroMemory(material);

            if (_tenantKeys.TryGetValue(candidate.KeyId, out TenantKeyMetadata? winner)
                && string.Equals(winner.OperationId, operationId, StringComparison.Ordinal))
            {
                return winner;
            }
        }
    }

    public Task<IReadOnlyList<TenantKeyMetadata>> ListTenantKeysAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        IReadOnlyList<TenantKeyMetadata> keys = _tenantKeys.Values
            .Where(k => string.Equals(k.TenantId, tenantId, StringComparison.Ordinal))
            .OrderBy(k => k.Version)
            .ToList();

        return Task.FromResult(keys);
    }

    public Task<IReadOnlyList<PartyKeyRecord>> ListPartyKeyRecordsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        string prefix = $"{tenantId}/parties/";
        IReadOnlyList<PartyKeyRecord> records = _secrets.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(ParsePartyKeyRecord)
            .OrderBy(r => r.PartyId, StringComparer.Ordinal)
            .ThenBy(r => r.Version)
            .ToList();

        return Task.FromResult(records);
    }

    public Task<PartyKeyWrappingMetadata?> GetPartyKeyWrappingMetadataAsync(
        string tenantId,
        string partyId,
        int keyVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        if (!_secrets.ContainsKey($"{tenantId}/parties/{partyId}/v{keyVersion}"))
        {
            throw new PartyEncryptionKeyDestroyedException(tenantId, partyId);
        }

        _wrappingMetadata.TryGetValue(BuildWrappingKey(tenantId, partyId, keyVersion), out PartyKeyWrappingMetadata? metadata);
        return Task.FromResult(metadata);
    }

    public Task SetPartyKeyWrappingMetadataAsync(PartyKeyWrappingMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (!_tenantKeys.ContainsKey(metadata.TenantKeyId))
        {
            throw new KeyNotFoundException("Tenant key metadata was not found.");
        }

        if (!_secrets.ContainsKey($"{metadata.TenantId}/parties/{metadata.PartyId}/v{metadata.KeyVersion}"))
        {
            throw new PartyEncryptionKeyDestroyedException(metadata.TenantId, metadata.PartyId);
        }

        _wrappingMetadata[BuildWrappingKey(metadata.TenantId, metadata.PartyId, metadata.KeyVersion)] = metadata;
        return Task.CompletedTask;
    }

    private static void ValidateNamespace(string keyPath)
    {
        // Enforce format: {tenant}/parties/{partyId}/v{version}
        string[] parts = keyPath.Split('/');
        if (parts.Length != 4 || parts[1] != "parties" || !parts[3].StartsWith('v'))
        {
            throw new ArgumentException(
                $"Invalid key path format '{keyPath}'. Expected: '{{tenant}}/parties/{{partyId}}/v{{version}}'.",
                nameof(keyPath));
        }
    }

    private static string GetPartyPrefix(string keyPath)
    {
        string[] parts = keyPath.Split('/');
        return $"{parts[0]}/parties/{parts[2]}/";
    }

    private TenantKeyMetadata EnsureInitialTenantKey(string tenantId)
    {
        string keyId = BuildTenantKeyId(tenantId, 1);
        if (_tenantKeys.TryGetValue(keyId, out TenantKeyMetadata? existing))
        {
            return existing;
        }

        TenantKeyMetadata candidate = new()
        {
            TenantId = tenantId,
            KeyId = keyId,
            Version = 1,
            ProviderName = "local-dev",
            CreatedAt = DateTimeOffset.UtcNow,
            OperationId = "initial",
        };

        byte[] material = RandomNumberGenerator.GetBytes(TenantKeySizeBytes);
        if (_tenantKeys.TryAdd(keyId, candidate))
        {
            _tenantKeyMaterial[keyId] = material;
            return candidate;
        }

        // Lost the race — zero our unused material instead of leaking it through the loser path.
        CryptographicOperations.ZeroMemory(material);
        return _tenantKeys[keyId];
    }

    private static PartyKeyRecord ParsePartyKeyRecord(string keyPath)
    {
        ValidateNamespace(keyPath);
        string[] parts = keyPath.Split('/');
        return new PartyKeyRecord
        {
            TenantId = parts[0],
            PartyId = parts[2],
            Version = int.Parse(parts[3][1..]),
            KeyPath = keyPath,
        };
    }

    private static string BuildTenantKeyId(string tenantId, int version) => $"{tenantId}/tenant-keys/v{version}";

    private static string BuildWrappingKey(string tenantId, string partyId, int version) => $"{tenantId}:party-key-wrap:{partyId}:v{version}";
}
