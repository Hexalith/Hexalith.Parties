using Hexalith.Parties.Contracts.Security;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public class ErasureVerificationServiceTests
{
    private const string TenantId = "test-tenant";
    private const string PartyId = "party-1";

    [Fact]
    public async Task VerifyErasureAsync_AllStoresClean_ReturnsComplete()
    {
        // Arrange
        ErasureStoreCleanupDelegate store1 = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "detail-projection",
                Status = ErasureStoreCleanupStatus.Cleaned,
                Timestamp = DateTimeOffset.UtcNow,
            });
        ErasureStoreCleanupDelegate store2 = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "index-projection",
                Status = ErasureStoreCleanupStatus.Cleaned,
                Timestamp = DateTimeOffset.UtcNow,
            });

        var service = CreateService([store1, store2]);
        ErasureCertificate certificate = CreateCertificate();

        // Act
        ErasureVerificationReport report = await service.VerifyErasureAsync(
            TenantId, PartyId, certificate, CancellationToken.None);

        // Assert
        report.OverallStatus.ShouldBe(ErasureVerificationOverallStatus.Complete);
        report.StoreResults.Count.ShouldBe(2);
        report.PartyId.ShouldBe(PartyId);
        report.TenantId.ShouldBe(TenantId);
    }

    [Fact]
    public async Task VerifyErasureAsync_PartialFailure_ReturnsPartial()
    {
        // Arrange
        ErasureStoreCleanupDelegate cleanStore = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "detail-projection",
                Status = ErasureStoreCleanupStatus.Cleaned,
                Timestamp = DateTimeOffset.UtcNow,
            });
        ErasureStoreCleanupDelegate failedStore = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "index-projection",
                Status = ErasureStoreCleanupStatus.Failed,
                Timestamp = DateTimeOffset.UtcNow,
                ErrorMessage = "Store unavailable",
            });

        var service = CreateService([cleanStore, failedStore]);
        ErasureCertificate certificate = CreateCertificate();

        // Act
        ErasureVerificationReport report = await service.VerifyErasureAsync(
            TenantId, PartyId, certificate, CancellationToken.None);

        // Assert
        report.OverallStatus.ShouldBe(ErasureVerificationOverallStatus.Partial);
        report.StoreResults.Count.ShouldBe(2);
    }

    [Fact]
    public async Task VerifyErasureAsync_AllStoresFail_ReturnsFailed()
    {
        // Arrange
        ErasureStoreCleanupDelegate failedStore1 = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "detail-projection",
                Status = ErasureStoreCleanupStatus.Failed,
                Timestamp = DateTimeOffset.UtcNow,
                ErrorMessage = "Timeout",
            });
        ErasureStoreCleanupDelegate failedStore2 = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "index-projection",
                Status = ErasureStoreCleanupStatus.Failed,
                Timestamp = DateTimeOffset.UtcNow,
                ErrorMessage = "Connection refused",
            });

        var service = CreateService([failedStore1, failedStore2]);
        ErasureCertificate certificate = CreateCertificate();

        // Act
        ErasureVerificationReport report = await service.VerifyErasureAsync(
            TenantId, PartyId, certificate, CancellationToken.None);

        // Assert
        report.OverallStatus.ShouldBe(ErasureVerificationOverallStatus.Failed);
        report.StoreResults.Count.ShouldBe(2);
    }

    [Fact]
    public async Task VerifyErasureAsync_NoStores_ReturnsComplete()
    {
        // Arrange
        var service = CreateService([]);
        ErasureCertificate certificate = CreateCertificate();

        // Act
        ErasureVerificationReport report = await service.VerifyErasureAsync(
            TenantId, PartyId, certificate, CancellationToken.None);

        // Assert
        report.OverallStatus.ShouldBe(ErasureVerificationOverallStatus.Complete);
        report.StoreResults.ShouldBeEmpty();
    }

    [Fact]
    public async Task VerifyErasureAsync_SkippedStores_ReturnsComplete()
    {
        // Arrange
        ErasureStoreCleanupDelegate skippedStore = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "optional-projection",
                Status = ErasureStoreCleanupStatus.Skipped,
                Timestamp = DateTimeOffset.UtcNow,
            });

        var service = CreateService([skippedStore]);
        ErasureCertificate certificate = CreateCertificate();

        // Act
        ErasureVerificationReport report = await service.VerifyErasureAsync(
            TenantId, PartyId, certificate, CancellationToken.None);

        // Assert
        report.OverallStatus.ShouldBe(ErasureVerificationOverallStatus.Complete);
    }

    [Fact]
    public void DetermineOverallStatus_MixedCleanedAndFailed_ReturnsPartial()
    {
        List<ErasureVerificationStoreResult> results =
        [
            new() { StoreName = "a", Status = ErasureStoreCleanupStatus.Cleaned, Timestamp = DateTimeOffset.UtcNow },
            new() { StoreName = "b", Status = ErasureStoreCleanupStatus.Failed, Timestamp = DateTimeOffset.UtcNow },
        ];

        ErasureVerificationOverallStatus status = ErasureVerificationService.DetermineOverallStatus(results);

        status.ShouldBe(ErasureVerificationOverallStatus.Partial);
    }

    [Fact]
    public void DetermineOverallStatus_OnlyFailed_ReturnsFailed()
    {
        List<ErasureVerificationStoreResult> results =
        [
            new() { StoreName = "a", Status = ErasureStoreCleanupStatus.Failed, Timestamp = DateTimeOffset.UtcNow },
        ];

        ErasureVerificationOverallStatus status = ErasureVerificationService.DetermineOverallStatus(results);

        status.ShouldBe(ErasureVerificationOverallStatus.Failed);
    }

    [Fact]
    public void DetermineOverallStatus_OnlyCleaned_ReturnsComplete()
    {
        List<ErasureVerificationStoreResult> results =
        [
            new() { StoreName = "a", Status = ErasureStoreCleanupStatus.Cleaned, Timestamp = DateTimeOffset.UtcNow },
            new() { StoreName = "b", Status = ErasureStoreCleanupStatus.Cleaned, Timestamp = DateTimeOffset.UtcNow },
        ];

        ErasureVerificationOverallStatus status = ErasureVerificationService.DetermineOverallStatus(results);

        status.ShouldBe(ErasureVerificationOverallStatus.Complete);
    }

    [Fact]
    public void DetermineOverallStatus_Empty_ReturnsComplete()
    {
        ErasureVerificationOverallStatus status = ErasureVerificationService.DetermineOverallStatus([]);

        status.ShouldBe(ErasureVerificationOverallStatus.Complete);
    }

    private static ErasureVerificationService CreateService(IReadOnlyList<ErasureStoreCleanupDelegate> storeCleanups)
        => new(storeCleanups, NullLogger<ErasureVerificationService>.Instance);

    private static ErasureCertificate CreateCertificate() => new()
    {
        PartyId = PartyId,
        TenantId = TenantId,
        Timestamp = DateTimeOffset.UtcNow,
        KeyVersionsDestroyed = [1],
        VerificationStatus = ErasureVerificationStatus.Pending,
    };
}
