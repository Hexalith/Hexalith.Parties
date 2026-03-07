using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Security;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public class PartyKeyLifecycleServiceTests
{
    private readonly IPartyKeyManagementService _keyService = Substitute.For<IPartyKeyManagementService>();

    private PartyKeyLifecycleService CreateService()
        => new(_keyService, NullLogger<PartyKeyLifecycleService>.Instance);

    [Fact]
    public async Task OnPartyCreatedAsync_CreatesKey_WhenBackendAvailable()
    {
        _keyService.CreateKeyAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns(new PartyKeyInfo
            {
                KeyId = "acme/parties/p1/v1",
                Version = 1,
                TenantId = "acme",
                PartyId = "p1",
                Algorithm = EncryptionAlgorithm.AES256GCM,
                CreatedAt = DateTimeOffset.UtcNow,
            });

        var service = CreateService();
        await service.OnPartyCreatedAsync("acme", "p1");

        bool pending = await service.IsCryptoPendingAsync("acme", "p1");
        pending.ShouldBeFalse();
    }

    [Fact]
    public async Task OnPartyCreatedAsync_MarksCryptoPending_WhenKeyCreationFails()
    {
        _keyService.CreateKeyAsync("acme", "p1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Backend unavailable"));

        var service = CreateService();
        await service.OnPartyCreatedAsync("acme", "p1");

        bool pending = await service.IsCryptoPendingAsync("acme", "p1");
        pending.ShouldBeTrue();
    }

    [Fact]
    public async Task OnPartyCreatedAsync_DoesNotThrow_WhenKeyCreationFails()
    {
        _keyService.CreateKeyAsync("acme", "p1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Backend unavailable"));

        var service = CreateService();

        await Should.NotThrowAsync(() => service.OnPartyCreatedAsync("acme", "p1"));
    }

    [Fact]
    public async Task RetryPendingKeyCreationAsync_ClearsPending_WhenRetrySucceeds()
    {
        _keyService.CreateKeyAsync("acme", "p1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Backend unavailable"));

        var service = CreateService();
        await service.OnPartyCreatedAsync("acme", "p1");

        // Now make the backend available
        _keyService.CreateKeyAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns(new PartyKeyInfo
            {
                KeyId = "acme/parties/p1/v1",
                Version = 1,
                TenantId = "acme",
                PartyId = "p1",
                Algorithm = EncryptionAlgorithm.AES256GCM,
                CreatedAt = DateTimeOffset.UtcNow,
            });

        await service.RetryPendingKeyCreationAsync("acme", "p1");

        bool pending = await service.IsCryptoPendingAsync("acme", "p1");
        pending.ShouldBeFalse();
    }

    [Fact]
    public async Task RetryPendingKeyCreationAsync_NoOp_WhenNotPending()
    {
        var service = CreateService();
        await service.RetryPendingKeyCreationAsync("acme", "p1");

        await _keyService.DidNotReceive().CreateKeyAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsCryptoPendingAsync_ReturnsFalse_WhenPartyNotTracked()
    {
        var service = CreateService();
        bool pending = await service.IsCryptoPendingAsync("acme", "p1");
        pending.ShouldBeFalse();
    }

    [Theory]
    [InlineData("tenant-a", "p1")]
    [InlineData("tenant-b", "p1")]
    public async Task CryptoPending_IsTenantScoped(string tenantId, string partyId)
    {
        _keyService.CreateKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Backend unavailable"));

        var service = CreateService();
        await service.OnPartyCreatedAsync(tenantId, partyId);

        // Same party, different tenant should not be pending
        string otherTenant = tenantId == "tenant-a" ? "tenant-b" : "tenant-a";
        bool pending = await service.IsCryptoPendingAsync(otherTenant, partyId);
        pending.ShouldBeFalse();

        bool thisPending = await service.IsCryptoPendingAsync(tenantId, partyId);
        thisPending.ShouldBeTrue();
    }
}
