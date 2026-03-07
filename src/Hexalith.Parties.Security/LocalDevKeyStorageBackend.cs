using System.Collections.Concurrent;
using System.Security.Cryptography;

using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class LocalDevKeyStorageBackend : IKeyStorageBackend
{
    private const int MaxSecretSizeBytes = 8192;
    private const int MaxSecretsPerParty = 100;

    private readonly ConcurrentDictionary<string, byte[]> _secrets = new();

    public Task CreateSecretAsync(string keyPath, byte[] keyMaterial, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentNullException.ThrowIfNull(keyMaterial);

        if (keyMaterial.Length > MaxSecretSizeBytes)
        {
            throw new ArgumentException($"Key material exceeds maximum size of {MaxSecretSizeBytes} bytes.", nameof(keyMaterial));
        }

        ValidateNamespace(keyPath);

        byte[] stored = new byte[keyMaterial.Length];
        keyMaterial.CopyTo(stored, 0);

        if (!_secrets.TryAdd(keyPath, stored))
        {
            throw new InvalidOperationException($"Secret already exists at path '{keyPath}'.");
        }

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
}
