using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Security;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public class PartyKeyManagementServiceTests
{
    private readonly IKeyStorageBackend _backend = Substitute.For<IKeyStorageBackend>();
    private readonly IKeyOperationAuditService _auditService = Substitute.For<IKeyOperationAuditService>();

    private PartyKeyManagementService CreateService() => new(_backend, _auditService);

    [Fact]
    public async Task CreateKeyAsync_GeneratesValidAes256Key()
    {
        byte[]? capturedKey = null;
        _backend.CreateSecretAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedKey = (byte[])((byte[])ci[1]).Clone();
                return Task.CompletedTask;
            });

        PartyKeyInfo result = await CreateService().CreateKeyAsync("acme", "p1");

        capturedKey.ShouldNotBeNull();
        capturedKey.Length.ShouldBe(32); // AES-256 = 32 bytes
        result.TenantId.ShouldBe("acme");
        result.PartyId.ShouldBe("p1");
        result.Version.ShouldBe(1);
        result.Algorithm.ShouldBe(EncryptionAlgorithm.AES256GCM);
    }

    [Fact]
    public async Task CreateKeyAsync_StoresKeyWithCorrectPath()
    {
        await CreateService().CreateKeyAsync("acme", "p1");

        await _backend.Received(1).CreateSecretAsync(
            "acme/parties/p1/v1",
            Arg.Any<byte[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateKeyAsync_AuditsOperation()
    {
        await CreateService().CreateKeyAsync("acme", "p1");

        await _auditService.Received(1).RecordOperationAsync(
            Arg.Is<KeyOperationAuditEntry>(e =>
                e.OperationType == KeyOperationType.Create &&
                e.TenantId == "acme" &&
                e.PartyId == "p1" &&
                e.KeyVersion == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetKeyAsync_ReadsLatestVersionFromBackend()
    {
        byte[] expected = new byte[32];
        Random.Shared.NextBytes(expected);
        _backend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns([1, 2]);
        _backend.ReadSecretAsync("acme/parties/p1/v2", Arg.Any<CancellationToken>())
            .Returns(expected);

        byte[] result = await CreateService().GetKeyAsync("acme", "p1");

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task GetKeyVersionAsync_ReadsSpecificVersion()
    {
        byte[] expected = new byte[32];
        Random.Shared.NextBytes(expected);
        _backend.ReadSecretAsync("acme/parties/p1/v1", Arg.Any<CancellationToken>())
            .Returns(expected);

        byte[] result = await CreateService().GetKeyVersionAsync("acme", "p1", 1);

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task RotateKeyAsync_CreatesNewVersionAndRetainsOld()
    {
        _backend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns([1, 2]);

        PartyKeyInfo result = await CreateService().RotateKeyAsync("acme", "p1");

        result.Version.ShouldBe(3);
        await _backend.Received(1).CreateSecretAsync(
            "acme/parties/p1/v3",
            Arg.Any<byte[]>(),
            Arg.Any<CancellationToken>());
        // Old versions should NOT be deleted
        await _backend.DidNotReceive().DeleteSecretAsync(
            Arg.Is<string>(s => s.Contains("v1") || s.Contains("v2")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateKeyAsync_AuditsOperation()
    {
        _backend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns([1]);

        await CreateService().RotateKeyAsync("acme", "p1");

        await _auditService.Received(1).RecordOperationAsync(
            Arg.Is<KeyOperationAuditEntry>(e =>
                e.OperationType == KeyOperationType.Rotate &&
                e.TenantId == "acme" &&
                e.PartyId == "p1" &&
                e.KeyVersion == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteKeyAsync_DeletesAllVersions()
    {
        _backend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns([1, 2, 3]);

        ErasureCertificate cert = await CreateService().DeleteKeyAsync("acme", "p1");

        await _backend.Received(1).DeleteAllVersionsAsync("acme", "p1", Arg.Any<CancellationToken>());
        cert.PartyId.ShouldBe("p1");
        cert.TenantId.ShouldBe("acme");
        cert.KeyVersionsDestroyed.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task DeleteKeyAsync_VerifiesKeyDeletion()
    {
        _backend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns(
                [1],        // First call: enumerate versions before delete
                (IReadOnlyList<int>)[]  // Second call: verification after delete
            );

        ErasureCertificate cert = await CreateService().DeleteKeyAsync("acme", "p1");

        cert.VerificationStatus.ShouldBe(ErasureVerificationStatus.Verified);
    }

    [Fact]
    public async Task DeleteKeyAsync_ReportsFailedVerification_WhenKeysStillExist()
    {
        _backend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns(
                [1],  // Before delete
                [1]   // After delete — keys still there
            );

        ErasureCertificate cert = await CreateService().DeleteKeyAsync("acme", "p1");

        cert.VerificationStatus.ShouldBe(ErasureVerificationStatus.Failed);
    }

    [Fact]
    public async Task DeleteKeyAsync_AuditsOperation()
    {
        _backend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns(
                [1],
                (IReadOnlyList<int>)[]
            );

        await CreateService().DeleteKeyAsync("acme", "p1");

        await _auditService.Received(1).RecordOperationAsync(
            Arg.Is<KeyOperationAuditEntry>(e =>
                e.OperationType == KeyOperationType.Delete &&
                e.TenantId == "acme" &&
                e.PartyId == "p1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateKeyAsync_EnforcesTenantNamespace()
    {
        await CreateService().CreateKeyAsync("tenant-a", "p1");

        await _backend.Received(1).CreateSecretAsync(
            Arg.Is<string>(s => s.StartsWith("tenant-a/parties/")),
            Arg.Any<byte[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateKeyAsync_TwoCallsProduceDifferentKeys()
    {
        List<byte[]> capturedKeys = [];
        _backend.CreateSecretAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedKeys.Add((byte[])((byte[])ci[1]).Clone());
                return Task.CompletedTask;
            });

        await CreateService().CreateKeyAsync("acme", "p1");
        await CreateService().CreateKeyAsync("acme", "p2");

        capturedKeys.Count.ShouldBe(2);
        capturedKeys[0].ShouldNotBe(capturedKeys[1]);
    }

    [Fact]
    public async Task RotateKeyAsync_BackendFailure_DoesNotCorruptExistingKeys()
    {
        _backend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns([1]);
        _backend.CreateSecretAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Backend write failed"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => CreateService().RotateKeyAsync("acme", "p1"));

        // Old version should NOT have been deleted
        await _backend.DidNotReceive().DeleteSecretAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _backend.DidNotReceive().DeleteAllVersionsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("acme", "person-party-1")]
    [InlineData("acme", "org-party-1")]
    [InlineData("acme", "org-natural-person-1")]
    public async Task CreateKeyAsync_WorksForAllPartyTypes(string tenantId, string partyId)
    {
        PartyKeyInfo result = await CreateService().CreateKeyAsync(tenantId, partyId);

        result.TenantId.ShouldBe(tenantId);
        result.PartyId.ShouldBe(partyId);
        result.Version.ShouldBe(1);
        result.Algorithm.ShouldBe(EncryptionAlgorithm.AES256GCM);
    }

    [Fact]
    public async Task CreateKeyAsync_SecurelyDisposesKeyMaterial()
    {
        byte[]? capturedKey = null;
        _backend.CreateSecretAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedKey = (byte[])ci[1]; // Reference, NOT clone
                return Task.CompletedTask;
            });

        await CreateService().CreateKeyAsync("acme", "p1");

        // After the method returns, the key material should be zeroed
        capturedKey.ShouldNotBeNull();
        capturedKey.ShouldAllBe(b => b == 0);
    }
}
