using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Parties.IntegrationTests.Identity;

/// <summary>
/// Test-only custody double. It is registered by the persisted verifier, never by production DI.
/// Exclusive expiry denies reads. Destruction removes the decryption key for every copy.
/// An unknown acknowledgement stays pending and is never reported as success.
/// </summary>
internal sealed class TestOnlyIdentityHistoryCustody : IIdentityHistoryCustody
{
    private static readonly string[] CopyNames = ["source", "snapshot", "cache", "replica", "backup"];
    private readonly PersistedPartyIdentityDirectory _directory;
    private readonly MutableUtcClock _clock;

    /// <summary>Creates a double whose units live in <paramref name="directory"/>.</summary>
    /// <param name="directory">The disposable durable directory.</param>
    /// <param name="clock">The clock that decides exclusive expiry.</param>
    public TestOnlyIdentityHistoryCustody(PersistedPartyIdentityDirectory directory, MutableUtcClock clock)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(clock);
        _directory = directory;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task<IdentityHistoryCustodyEvidence?> AdmitAsync(AggregateIdentity identity, IdentityHistoryPolicy policy, DateTimeOffset effectiveAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(policy);
        if (!policy.IsValid || policy.DeriveExpiry(effectiveAt) is not { } expiresAt)
        {
            return Task.FromResult<IdentityHistoryCustodyEvidence?>(null);
        }

        string evidenceId = EvidenceIdFor(identity, effectiveAt);
        string unit = UnitPath(evidenceId);
        Directory.CreateDirectory(unit);
        string keyPath = Path.Combine(unit, "key.bin");
        if (!File.Exists(keyPath))
        {
            byte[] key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(keyPath, key);
            CryptographicOperations.ZeroMemory(key);
        }

        var evidence = new IdentityHistoryCustodyEvidence(policy.PolicyId, "party-actor-history-v1", expiresAt, 1, evidenceId, true, true, true);
        WriteStatus(evidenceId, new UnitStatus(evidence, false, false, File.Exists(AckPath(evidenceId))));
        return Task.FromResult<IdentityHistoryCustodyEvidence?>(evidence);
    }

    /// <inheritdoc />
    public Task<bool> CanReadAsync(AggregateIdentity identity, IdentityHistoryCustodyEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(evidence);
        UnitStatus? status = ReadStatus(evidence.EvidenceId);
        bool readable = status is { Pending: false, Destroyed: false }
            && File.Exists(KeyPath(evidence.EvidenceId))
            && _clock.UtcNow < evidence.ExpiresAt
            && status.Evidence.ExpiresAt == evidence.ExpiresAt;
        return Task.FromResult(readable);
    }

    /// <inheritdoc />
    public Task<bool> DestroyExpiredAsync(AggregateIdentity identity, IdentityHistoryCustodyEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(evidence);
        if (_clock.UtcNow < evidence.ExpiresAt)
        {
            return Task.FromResult(false);
        }

        WipeKeyAndCopies(evidence.EvidenceId);
        bool acknowledged = File.Exists(AckPath(evidence.EvidenceId)) && !File.Exists(HoldPath(evidence.EvidenceId));
        UnitStatus? existing = ReadStatus(evidence.EvidenceId);
        IdentityHistoryCustodyEvidence recorded = existing?.Evidence ?? evidence;
        WriteStatus(evidence.EvidenceId, new UnitStatus(recorded, !acknowledged, acknowledged, acknowledged));
        return Task.FromResult(acknowledged);
    }

    /// <summary>Keeps the next destruction acknowledgement unknown across restarts.</summary>
    /// <param name="evidenceId">The custody unit.</param>
    public void HoldAcknowledgement(string evidenceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);
        Directory.CreateDirectory(UnitPath(evidenceId));
        File.WriteAllText(HoldPath(evidenceId), "pending");
        if (File.Exists(AckPath(evidenceId)))
        {
            File.Delete(AckPath(evidenceId));
        }
    }

    /// <summary>Supplies the missing destruction acknowledgement. A later destroy can then succeed.</summary>
    /// <param name="evidenceId">The custody unit.</param>
    public void Acknowledge(string evidenceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);
        Directory.CreateDirectory(UnitPath(evidenceId));
        File.WriteAllText(AckPath(evidenceId), "acknowledged");
        if (File.Exists(HoldPath(evidenceId)))
        {
            File.Delete(HoldPath(evidenceId));
        }
    }

    /// <summary>Reports whether destruction is still waiting for an acknowledgement.</summary>
    /// <param name="evidenceId">The custody unit.</param>
    /// <returns><see langword="true"/> when the unit is pending.</returns>
    public bool IsPending(string evidenceId)
        => ReadStatus(evidenceId) is { Pending: true, Destroyed: false };

    /// <summary>Reports whether every copy's decryption key is gone.</summary>
    /// <param name="evidenceId">The custody unit.</param>
    /// <returns><see langword="true"/> when the key and every copy are unreadable.</returns>
    public bool EveryCopyIsUnreadable(string evidenceId)
    {
        if (File.Exists(KeyPath(evidenceId)))
        {
            return false;
        }

        foreach (string copy in CopyNames)
        {
            string path = CopyPath(evidenceId, copy);
            if (!File.Exists(path))
            {
                return false;
            }

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) == "HXIH1")
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public Task<PayloadProtectionResult> ProtectEventAsync(AggregateIdentity identity, string eventType, byte[] payload, string format, IdentityHistoryCustodyEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(evidence);
        byte[] envelope = Seal(evidence.EvidenceId, payload);
        foreach (string copy in CopyNames)
        {
            string path = CopyPath(evidence.EvidenceId, copy);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, envelope);
        }

        return Task.FromResult(new PayloadProtectionResult(envelope, "json+identity-history-v1", EventStorePayloadProtectionMetadata.Unprotected(PayloadProtectionState.Protected) with { Scheme = "party-actor-history-v1" }));
    }

    /// <inheritdoc />
    public Task<PayloadProtectionResult> UnprotectEventAsync(AggregateIdentity identity, string eventType, byte[] payload, string format, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] plain = Open(payload);
        return Task.FromResult(new PayloadProtectionResult(plain, "json"));
    }

    /// <inheritdoc />
    public Task<object> ProtectSnapshotAsync(AggregateIdentity identity, object snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(snapshot);
    }

    /// <inheritdoc />
    public Task<object?> UnprotectSnapshotAsync(AggregateIdentity identity, object snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot is byte[] bytes)
        {
            return Task.FromResult<object?>(Open(bytes));
        }

        return Task.FromResult<object?>(snapshot);
    }

    private byte[] Seal(string evidenceId, byte[] plaintext)
    {
        byte[] key = File.ReadAllBytes(KeyPath(evidenceId));
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] cipher = new byte[plaintext.Length];
        byte[] tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, cipher, tag);
        CryptographicOperations.ZeroMemory(key);
        byte[] id = Encoding.UTF8.GetBytes(evidenceId);
        byte[] envelope = new byte[5 + 1 + id.Length + nonce.Length + tag.Length + cipher.Length];
        Encoding.ASCII.GetBytes("HXIH1").CopyTo(envelope, 0);
        envelope[5] = checked((byte)id.Length);
        id.CopyTo(envelope, 6);
        nonce.CopyTo(envelope, 6 + id.Length);
        tag.CopyTo(envelope, 6 + id.Length + nonce.Length);
        cipher.CopyTo(envelope, 6 + id.Length + nonce.Length + tag.Length);
        return envelope;
    }

    private byte[] Open(byte[] envelope)
    {
        if (envelope is not { Length: > 6 } || Encoding.ASCII.GetString(envelope, 0, 5) != "HXIH1")
        {
            throw new CryptographicException("History ciphertext is not readable.");
        }

        int idLength = envelope[5];
        if (envelope.Length < 6 + idLength + 12 + 16)
        {
            throw new CryptographicException("History ciphertext is not readable.");
        }

        string evidenceId = Encoding.UTF8.GetString(envelope, 6, idLength);
        string keyPath = KeyPath(evidenceId);
        if (!File.Exists(keyPath))
        {
            throw new CryptographicException("History decryption capability is destroyed.");
        }

        byte[] key = File.ReadAllBytes(keyPath);
        int offset = 6 + idLength;
        byte[] nonce = envelope[offset..(offset + 12)];
        byte[] tag = envelope[(offset + 12)..(offset + 28)];
        byte[] cipher = envelope[(offset + 28)..];
        byte[] plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return plain;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private void WipeKeyAndCopies(string evidenceId)
    {
        string keyPath = KeyPath(evidenceId);
        if (File.Exists(keyPath))
        {
            byte[] key = File.ReadAllBytes(keyPath);
            CryptographicOperations.ZeroMemory(key);
            File.Delete(keyPath);
        }

        byte[] destroyed = "destroyed"u8.ToArray();
        foreach (string copy in CopyNames)
        {
            string path = CopyPath(evidenceId, copy);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, destroyed);
        }
    }

    private static string EvidenceIdFor(AggregateIdentity identity, DateTimeOffset effectiveAt)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(identity.TenantId + "\0" + identity.AggregateId + "\0" + effectiveAt.UtcTicks));
        return "custody-" + Convert.ToHexString(digest)[..32];
    }

    private string UnitPath(string evidenceId) => Path.Combine(_directory.Root, "custody", evidenceId);

    private string KeyPath(string evidenceId) => Path.Combine(UnitPath(evidenceId), "key.bin");

    private string AckPath(string evidenceId) => Path.Combine(UnitPath(evidenceId), "ack.txt");

    private string HoldPath(string evidenceId) => Path.Combine(UnitPath(evidenceId), "hold.txt");

    private string CopyPath(string evidenceId, string copy) => Path.Combine(UnitPath(evidenceId), "copies", copy + ".bin");

    private string StatusPath(string evidenceId) => Path.Combine(UnitPath(evidenceId), "status.json");

    private void WriteStatus(string evidenceId, UnitStatus status)
    {
        Directory.CreateDirectory(UnitPath(evidenceId));
        File.WriteAllBytes(StatusPath(evidenceId), JsonSerializer.SerializeToUtf8Bytes(status));
    }

    private UnitStatus? ReadStatus(string evidenceId)
    {
        string path = StatusPath(evidenceId);
        return File.Exists(path) ? JsonSerializer.Deserialize<UnitStatus>(File.ReadAllBytes(path)) : null;
    }

    private sealed record UnitStatus(IdentityHistoryCustodyEvidence Evidence, bool Pending, bool Destroyed, bool Acknowledged);
}
