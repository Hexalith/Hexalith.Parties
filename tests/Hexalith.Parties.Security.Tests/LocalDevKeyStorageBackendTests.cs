using Hexalith.Parties.Security;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public class LocalDevKeyStorageBackendTests
{
    private readonly LocalDevKeyStorageBackend _backend = new();

    [Fact]
    public async Task CreateSecret_ThenReadSecret_ReturnsKeyMaterial()
    {
        byte[] key = new byte[32];
        Random.Shared.NextBytes(key);
        byte[] original = (byte[])key.Clone();

        await _backend.CreateSecretAsync("acme/parties/p1/v1", key);

        byte[] read = await _backend.ReadSecretAsync("acme/parties/p1/v1");
        read.ShouldBe(original);
    }

    [Fact]
    public async Task CreateSecret_DuplicatePath_Throws()
    {
        byte[] key = new byte[32];
        await _backend.CreateSecretAsync("acme/parties/p1/v1", key);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _backend.CreateSecretAsync("acme/parties/p1/v1", key));
    }

    [Fact]
    public async Task ReadSecret_NonExistentPath_ThrowsKeyNotFound()
    {
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _backend.ReadSecretAsync("acme/parties/p1/v1"));
    }

    [Fact]
    public async Task DeleteAllVersions_RemovesAllVersionsForParty()
    {
        byte[] key = new byte[32];
        await _backend.CreateSecretAsync("acme/parties/p1/v1", key);
        await _backend.CreateSecretAsync("acme/parties/p1/v2", key);
        await _backend.CreateSecretAsync("acme/parties/p2/v1", key); // Different party

        await _backend.DeleteAllVersionsAsync("acme", "p1");

        IReadOnlyList<int> versions = await _backend.ListKeyVersionsAsync("acme", "p1");
        versions.ShouldBeEmpty();
        // Other party's key should be unaffected
        IReadOnlyList<int> p2Versions = await _backend.ListKeyVersionsAsync("acme", "p2");
        p2Versions.ShouldBe([1]);
    }

    [Fact]
    public async Task ListKeyVersions_ReturnsOrderedVersions()
    {
        byte[] key = new byte[32];
        await _backend.CreateSecretAsync("acme/parties/p1/v3", key);
        await _backend.CreateSecretAsync("acme/parties/p1/v1", key);
        await _backend.CreateSecretAsync("acme/parties/p1/v2", key);

        IReadOnlyList<int> versions = await _backend.ListKeyVersionsAsync("acme", "p1");

        versions.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task CreateSecret_InvalidNamespace_Throws()
    {
        byte[] key = new byte[32];

        await Should.ThrowAsync<ArgumentException>(
            () => _backend.CreateSecretAsync("invalid-path", key));
    }

    [Fact]
    public async Task CreateSecret_OversizedKey_Throws()
    {
        byte[] oversized = new byte[8193];

        await Should.ThrowAsync<ArgumentException>(
            () => _backend.CreateSecretAsync("acme/parties/p1/v1", oversized));
    }

    [Fact]
    public async Task TenantIsolation_KeysForTenantA_InaccessibleToTenantB()
    {
        byte[] key = new byte[32];
        await _backend.CreateSecretAsync("tenant-a/parties/p1/v1", key);

        IReadOnlyList<int> tenantBVersions = await _backend.ListKeyVersionsAsync("tenant-b", "p1");
        tenantBVersions.ShouldBeEmpty();
    }
}
