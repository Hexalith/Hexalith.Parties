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
    public async Task VerifyErasureAsync_PendingStore_ReturnsPending()
    {
        ErasureStoreCleanupDelegate cleanStore = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "detail-projection",
                Status = ErasureStoreCleanupStatus.Cleaned,
                Timestamp = DateTimeOffset.UtcNow,
            });
        ErasureStoreCleanupDelegate pendingStore = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "index-projection",
                Status = ErasureStoreCleanupStatus.Pending,
                Timestamp = DateTimeOffset.UtcNow,
                ErrorMessage = "Projection is rebuilding for Ada Lovelace.",
            });

        var service = CreateService([cleanStore, pendingStore]);

        ErasureVerificationReport report = await service.VerifyErasureAsync(
            TenantId, PartyId, CreateCertificate(), CancellationToken.None);

        report.OverallStatus.ShouldBe(ErasureVerificationOverallStatus.Pending);
        report.StoreResults[1].Status.ShouldBe(ErasureStoreCleanupStatus.Pending);
        string pendingError = report.StoreResults[1].ErrorMessage ?? string.Empty;
        pendingError.ShouldNotContain("Ada Lovelace");
        pendingError.ShouldContain("index-projection");
    }

    [Fact]
    public async Task VerifyErasureAsync_NotApplicableStore_ReturnsComplete()
    {
        ErasureStoreCleanupDelegate searchStore = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "memories-search",
                Status = ErasureStoreCleanupStatus.NotApplicable,
                Timestamp = DateTimeOffset.UtcNow,
            });

        var service = CreateService([searchStore]);

        ErasureVerificationReport report = await service.VerifyErasureAsync(
            TenantId, PartyId, CreateCertificate(), CancellationToken.None);

        report.OverallStatus.ShouldBe(ErasureVerificationOverallStatus.Complete);
        report.StoreResults[0].Status.ShouldBe(ErasureStoreCleanupStatus.NotApplicable);
    }

    [Fact]
    public async Task VerifyErasureAsync_AllInternalStoreCategoriesClean_ReturnsBoundedReport()
    {
        string[] storeNames =
        [
            "aggregate-readable-state",
            "snapshots",
            "detail-projection",
            "index-projection",
            "projection-cache",
            "memories-search",
        ];
        ErasureStoreCleanupDelegate[] stores = storeNames
            .Select<string, ErasureStoreCleanupDelegate>(name => (_, _, _) => Task.FromResult(
                new ErasureVerificationStoreResult
                {
                    StoreName = name,
                    Status = name == "memories-search" ? ErasureStoreCleanupStatus.NotApplicable : ErasureStoreCleanupStatus.Cleaned,
                    Timestamp = DateTimeOffset.UtcNow,
                }))
            .ToArray();

        var service = CreateService(stores);

        ErasureVerificationReport report = await service.VerifyErasureAsync(
            TenantId, PartyId, CreateCertificate(), CancellationToken.None);

        report.OverallStatus.ShouldBe(ErasureVerificationOverallStatus.Complete);
        report.StoreResults.Select(r => r.StoreName).ShouldBe(storeNames);
        string serializedReport = System.Text.Json.JsonSerializer.Serialize(report);
        serializedReport.ShouldNotContain("Ada Lovelace");
        serializedReport.ShouldNotContain("ada@example.com");
        serializedReport.Length.ShouldBeLessThan(4096);
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
    public void DetermineOverallStatus_OnlyPending_ReturnsPending()
    {
        List<ErasureVerificationStoreResult> results =
        [
            new() { StoreName = "a", Status = ErasureStoreCleanupStatus.Pending, Timestamp = DateTimeOffset.UtcNow },
        ];

        ErasureVerificationOverallStatus status = ErasureVerificationService.DetermineOverallStatus(results);

        status.ShouldBe(ErasureVerificationOverallStatus.Pending);
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

    [Fact]
    public async Task VerifyErasureAsync_ActorCorrupted_TreatsAsClean()
    {
        // Arrange — delegate throws to simulate corrupted actor state (D15 pattern)
        ErasureStoreCleanupDelegate corruptedStore = (_, _, _)
            => throw new InvalidOperationException("Deserialization failed: corrupted state");
        ErasureStoreCleanupDelegate healthyStore = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "index-projection",
                Status = ErasureStoreCleanupStatus.Cleaned,
                Timestamp = DateTimeOffset.UtcNow,
            });

        var service = CreateService([corruptedStore, healthyStore]);
        ErasureCertificate certificate = CreateCertificate();

        // Act — should NOT throw; corrupted store treated as Cleaned per D15
        ErasureVerificationReport report = await service.VerifyErasureAsync(
            TenantId, PartyId, certificate, CancellationToken.None);

        // Assert
        report.OverallStatus.ShouldBe(ErasureVerificationOverallStatus.Complete);
        report.StoreResults.Count.ShouldBe(2);
        report.StoreResults[0].Status.ShouldBe(ErasureStoreCleanupStatus.Cleaned);
        string fallbackError = report.StoreResults[0].ErrorMessage ?? string.Empty;
        fallbackError.ShouldNotContain("Deserialization failed");
        fallbackError.ShouldNotContain("corrupted state");
    }

    [Fact]
    public async Task VerifyErasureAsync_ResumesFromCheckpoint_SkippedAndCleaned_ReturnsComplete()
    {
        // Arrange — simulates checkpoint resumption:
        // Store A was cleaned in previous run (returns Skipped on re-run)
        // Store B failed previously but succeeds this time (returns Cleaned)
        ErasureStoreCleanupDelegate alreadyCleanedStore = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "detail-projection",
                Status = ErasureStoreCleanupStatus.Skipped,
                Timestamp = DateTimeOffset.UtcNow,
            });
        ErasureStoreCleanupDelegate retriedStore = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "index-projection",
                Status = ErasureStoreCleanupStatus.Cleaned,
                Timestamp = DateTimeOffset.UtcNow,
            });

        var service = CreateService([alreadyCleanedStore, retriedStore]);
        ErasureCertificate certificate = CreateCertificate();

        // Act — checkpoint resumption: previously-cleaned stores return Skipped
        ErasureVerificationReport report = await service.VerifyErasureAsync(
            TenantId, PartyId, certificate, CancellationToken.None);

        // Assert — overall Complete because all stores are Cleaned or Skipped
        report.OverallStatus.ShouldBe(ErasureVerificationOverallStatus.Complete);
        report.StoreResults.Count.ShouldBe(2);
        report.StoreResults[0].Status.ShouldBe(ErasureStoreCleanupStatus.Skipped);
        report.StoreResults[1].Status.ShouldBe(ErasureStoreCleanupStatus.Cleaned);
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
