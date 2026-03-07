using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Security;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public class CachedKeyManagementServiceTests
{
    private readonly IPartyKeyManagementService _inner = Substitute.For<IPartyKeyManagementService>();

    private CachedPartyKeyManagementService CreateService() => new(_inner);

    [Fact]
    public async Task GetKeyAsync_CacheHit_DoesNotCallInner()
    {
        byte[] key = new byte[32];
        Random.Shared.NextBytes(key);
        _inner.GetKeyAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns((byte[])key.Clone());

        CachedPartyKeyManagementService service = CreateService();
        await service.GetKeyAsync("acme", "p1"); // Cache miss — calls inner
        await service.GetKeyAsync("acme", "p1"); // Cache hit — should NOT call inner

        await _inner.Received(1).GetKeyAsync("acme", "p1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetKeyAsync_DifferentParties_CallsInnerForEach()
    {
        byte[] key = new byte[32];
        _inner.GetKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[])key.Clone());

        CachedPartyKeyManagementService service = CreateService();
        await service.GetKeyAsync("acme", "p1");
        await service.GetKeyAsync("acme", "p2");

        await _inner.Received(1).GetKeyAsync("acme", "p1", Arg.Any<CancellationToken>());
        await _inner.Received(1).GetKeyAsync("acme", "p2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateKeyAsync_DelegatesToInner()
    {
        var expected = new PartyKeyInfo
        {
            KeyId = "acme/parties/p1/v1",
            Version = 1,
            TenantId = "acme",
            PartyId = "p1",
            Algorithm = EncryptionAlgorithm.AES256GCM,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _inner.CreateKeyAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns(expected);

        PartyKeyInfo result = await CreateService().CreateKeyAsync("acme", "p1");

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task RotateKeyAsync_InvalidatesCacheForParty()
    {
        byte[] oldKey = new byte[32];
        byte[] newKey = new byte[32];
        Random.Shared.NextBytes(oldKey);
        Random.Shared.NextBytes(newKey);

        _inner.GetKeyAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns(oldKey, newKey);
        _inner.RotateKeyAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns(new PartyKeyInfo
            {
                KeyId = "acme/parties/p1/v2",
                Version = 2,
                TenantId = "acme",
                PartyId = "p1",
                Algorithm = EncryptionAlgorithm.AES256GCM,
                CreatedAt = DateTimeOffset.UtcNow,
            });

        CachedPartyKeyManagementService service = CreateService();
        await service.GetKeyAsync("acme", "p1"); // Cache the key
        await service.RotateKeyAsync("acme", "p1"); // Should invalidate cache
        await service.GetKeyAsync("acme", "p1"); // Should call inner again

        await _inner.Received(2).GetKeyAsync("acme", "p1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteKeyAsync_InvalidatesCacheForParty()
    {
        byte[] key = new byte[32];
        _inner.GetKeyAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns((byte[])key.Clone());
        _inner.DeleteKeyAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns(new ErasureCertificate
        {
            PartyId = "p1",
            TenantId = "acme",
            Timestamp = DateTimeOffset.UtcNow,
            KeyVersionsDestroyed = [1],
            VerificationStatus = ErasureVerificationStatus.Verified,
        });

        CachedPartyKeyManagementService service = CreateService();
        await service.GetKeyAsync("acme", "p1"); // Cache the key
        await service.DeleteKeyAsync("acme", "p1"); // Should invalidate cache

        // The cache entry should be evicted (key material zeroed)
        // Subsequent call would go to inner again
        await service.GetKeyAsync("acme", "p1");
        await _inner.Received(2).GetKeyAsync("acme", "p1", Arg.Any<CancellationToken>());
    }
}
