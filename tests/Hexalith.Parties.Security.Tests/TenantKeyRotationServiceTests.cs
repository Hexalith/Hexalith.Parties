#pragma warning disable CS8620 // NSubstitute + DaprClient nullable generics mismatch

using Dapr.Client;
using System.Text.Json;

using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Security;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public sealed class TenantKeyRotationServiceTests
{
    [Fact]
    public async Task RotateAsync_RewrapsActivePartyKeysAndKeepsReadsAvailable()
    {
        LocalDevKeyStorageBackend backend = new();
        byte[] key = [1, 2, 3, 4];
        await backend.CreateSecretAsync("tenant-a/parties/p1/v1", key);
        await backend.CreateSecretAsync("tenant-a/parties/p2/v1", key);
        DaprClient daprClient = ConfigureProgressStateStore();
        IKeyOperationAuditService auditService = Substitute.For<IKeyOperationAuditService>();
        CachedPartyKeyManagementService cache = new(Substitute.For<IPartyKeyManagementService>());
        TenantKeyRotationService service = new(backend, daprClient, auditService, new[] { cache }, new CorrelationContextAccessor());

        TenantKeyRotationStatus status = await service.RotateAsync(new TenantKeyRotationRequest
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            CorrelationId = "correlation-1",
        });

        status.Phase.ShouldBe(TenantKeyRotationPhase.Completed);
        status.ProcessedCount.ShouldBe(2);
        status.FailedCount.ShouldBe(0);
        (await backend.ReadSecretAsync("tenant-a/parties/p1/v1")).ShouldBe(key);
        PartyKeyWrappingMetadata metadata = (await backend.GetPartyKeyWrappingMetadataAsync("tenant-a", "p1", 1)).ShouldNotBeNull();
        metadata.RotationId.ShouldBe("rotation-1");
        metadata.TenantKeyVersion.ShouldBe(2);
    }

    [Fact]
    public async Task RotateAsync_SameOperationIsIdempotentAndDoesNotCreateConflictingTenantKeys()
    {
        LocalDevKeyStorageBackend backend = new();
        await backend.CreateSecretAsync("tenant-a/parties/p1/v1", [1, 2, 3, 4]);
        DaprClient daprClient = ConfigureProgressStateStore();
        IKeyOperationAuditService auditService = Substitute.For<IKeyOperationAuditService>();
        TenantKeyRotationService service = new(backend, daprClient, auditService, [], new CorrelationContextAccessor());

        TenantKeyRotationRequest request = new()
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            CorrelationId = "correlation-1",
        };
        TenantKeyRotationStatus first = await service.RotateAsync(request);
        TenantKeyRotationStatus second = await service.RotateAsync(request);

        first.Phase.ShouldBe(TenantKeyRotationPhase.Completed);
        second.Phase.ShouldBe(TenantKeyRotationPhase.Completed);
        IReadOnlyList<TenantKeyMetadata> keys = await backend.ListTenantKeysAsync("tenant-a");
        keys.Count.ShouldBe(2);
        keys.Count(k => k.OperationId == "rotation-1").ShouldBe(1);
    }

    [Fact]
    public async Task RotateAsync_PartialBackendFailureRecordsBoundedFailureCategoryAndRetriesFromSafeState()
    {
        LocalDevKeyStorageBackend realBackend = new();
        await realBackend.CreateSecretAsync("tenant-a/parties/p1/v1", [1, 2, 3, 4]);
        await realBackend.CreateSecretAsync("tenant-a/parties/p2/v1", [5, 6, 7, 8]);
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        WireBackendPassthrough(backend, realBackend);
        backend.SetPartyKeyWrappingMetadataAsync(
                Arg.Is<PartyKeyWrappingMetadata>(m => m.PartyId == "p2"),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("vault secret token unreachable"));
        DaprClient daprClient = ConfigureProgressStateStore();
        IKeyOperationAuditService auditService = Substitute.For<IKeyOperationAuditService>();
        TenantKeyRotationService service = new(backend, daprClient, auditService, [], new CorrelationContextAccessor());

        TenantKeyRotationRequest request = new()
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            CorrelationId = "correlation-1",
        };
        TenantKeyRotationStatus failed = await service.RotateAsync(request);
        backend.SetPartyKeyWrappingMetadataAsync(
                Arg.Is<PartyKeyWrappingMetadata>(m => m.PartyId == "p2"),
                Arg.Any<CancellationToken>())
            .Returns(call => realBackend.SetPartyKeyWrappingMetadataAsync(call.ArgAt<PartyKeyWrappingMetadata>(0)));
        TenantKeyRotationStatus retried = await service.RotateAsync(request);

        failed.Phase.ShouldBe(TenantKeyRotationPhase.Failed);
        failed.FailureCategories[TenantKeyRotationFailureCategory.BackendUnavailable].ShouldBe(1);
        JsonSerializer.Serialize(failed).ShouldNotContain("vault secret token unreachable");
        retried.Phase.ShouldBe(TenantKeyRotationPhase.Completed);
        retried.ProcessedCount.ShouldBe(2);
    }

    [Fact]
    public async Task RotateAsync_RetryAfterSkippedAndFailedRecordKeepsStatusCountsBounded()
    {
        LocalDevKeyStorageBackend realBackend = new();
        await realBackend.CreateSecretAsync("tenant-a/parties/p-active/v1", [5, 6, 7, 8]);
        TenantKeyMetadata targetTenantKey = await realBackend.GetOrCreateTenantKeyAsync("tenant-a", "rotation-1");
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.GetOrCreateTenantKeyAsync("tenant-a", "rotation-1", Arg.Any<CancellationToken>())
            .Returns(targetTenantKey);
        backend.ListPartyKeyRecordsAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns([
                new PartyKeyRecord
                {
                    TenantId = "tenant-a",
                    PartyId = "p-erased",
                    Version = 1,
                    KeyPath = "tenant-a/parties/p-erased/v1",
                },
                new PartyKeyRecord
                {
                    TenantId = "tenant-a",
                    PartyId = "p-active",
                    Version = 1,
                    KeyPath = "tenant-a/parties/p-active/v1",
                },
            ]);
        backend.GetPartyKeyWrappingMetadataAsync("tenant-a", "p-erased", 1, Arg.Any<CancellationToken>())
            .ThrowsAsync(new PartyEncryptionKeyDestroyedException("tenant-a", "p-erased"));
        backend.GetPartyKeyWrappingMetadataAsync("tenant-a", "p-active", 1, Arg.Any<CancellationToken>())
            .Returns(call => realBackend.GetPartyKeyWrappingMetadataAsync(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<int>(2)));
        bool failActivePartyRewrap = true;
        backend.SetPartyKeyWrappingMetadataAsync(
                Arg.Is<PartyKeyWrappingMetadata>(m => m.PartyId == "p-active"),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (failActivePartyRewrap)
                {
                    throw new InvalidOperationException("vault secret token unavailable");
                }

                return realBackend.SetPartyKeyWrappingMetadataAsync(call.ArgAt<PartyKeyWrappingMetadata>(0));
            });
        DaprClient daprClient = ConfigureProgressStateStore();
        IKeyOperationAuditService auditService = Substitute.For<IKeyOperationAuditService>();
        TenantKeyRotationService service = new(backend, daprClient, auditService, [], new CorrelationContextAccessor());
        TenantKeyRotationRequest request = new()
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            CorrelationId = "correlation-1",
        };

        TenantKeyRotationStatus failed = await service.RotateAsync(request);
        failActivePartyRewrap = false;
        TenantKeyRotationStatus retried = await service.RotateAsync(request);

        failed.Phase.ShouldBe(TenantKeyRotationPhase.Failed);
        failed.SkippedCount.ShouldBe(1);
        failed.ProcessedCount.ShouldBe(0);
        failed.FailedCount.ShouldBe(1);
        retried.Phase.ShouldBe(TenantKeyRotationPhase.Completed);
        retried.TotalCount.ShouldBe(2);
        retried.ProcessedCount.ShouldBe(1);
        retried.SkippedCount.ShouldBe(1);
        retried.FailedCount.ShouldBe(0);
    }

    [Fact]
    public async Task RotateAsync_MissingTenantKeyProviderReturnsRedactedFailureStatus()
    {
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.GetOrCreateTenantKeyAsync("tenant-a", "rotation-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("provider secret token missing"));
        DaprClient daprClient = ConfigureProgressStateStore();
        IKeyOperationAuditService auditService = Substitute.For<IKeyOperationAuditService>();
        TenantKeyRotationService service = new(backend, daprClient, auditService, [], new CorrelationContextAccessor());

        TenantKeyRotationStatus status = await service.RotateAsync(new TenantKeyRotationRequest
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            CorrelationId = "correlation-1",
        });

        status.Phase.ShouldBe(TenantKeyRotationPhase.Failed);
        status.FailureCategories[TenantKeyRotationFailureCategory.MissingKeyProvider].ShouldBe(1);
        JsonSerializer.Serialize(status).ShouldNotContain("provider secret token missing");
    }

    [Fact]
    public async Task RotateAsync_SkipsDestroyedPartyKeysWithoutRecreatingThem()
    {
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.GetOrCreateTenantKeyAsync("tenant-a", "rotation-1", Arg.Any<CancellationToken>())
            .Returns(new TenantKeyMetadata
            {
                TenantId = "tenant-a",
                KeyId = "tenant-a/tenant-keys/v2",
                Version = 2,
                ProviderName = "local-dev",
                CreatedAt = DateTimeOffset.Parse("2026-05-21T06:15:58.277Z"),
                OperationId = "rotation-1",
            });
        backend.ListPartyKeyRecordsAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns([
                new PartyKeyRecord
                {
                    TenantId = "tenant-a",
                    PartyId = "p-erased",
                    Version = 1,
                    KeyPath = "tenant-a/parties/p-erased/v1",
                },
            ]);
        backend.GetPartyKeyWrappingMetadataAsync("tenant-a", "p-erased", 1, Arg.Any<CancellationToken>())
            .ThrowsAsync(new PartyEncryptionKeyDestroyedException("tenant-a", "p-erased"));
        DaprClient daprClient = ConfigureProgressStateStore();
        IKeyOperationAuditService auditService = Substitute.For<IKeyOperationAuditService>();
        TenantKeyRotationService service = new(backend, daprClient, auditService, [], new CorrelationContextAccessor());

        TenantKeyRotationStatus status = await service.RotateAsync(new TenantKeyRotationRequest
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            CorrelationId = "correlation-1",
        });

        status.Phase.ShouldBe(TenantKeyRotationPhase.Completed);
        status.SkippedCount.ShouldBe(1);
        await backend.DidNotReceive().CreateSecretAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await backend.DidNotReceive().DeleteAllVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateAsync_InvalidatesCachedPartyKeysAfterRewrap()
    {
        LocalDevKeyStorageBackend backend = new();
        await backend.CreateSecretAsync("tenant-a/parties/p1/v1", [1, 2, 3, 4]);
        IPartyKeyManagementService inner = Substitute.For<IPartyKeyManagementService>();
        inner.GetKeyAsync("tenant-a", "p1", Arg.Any<CancellationToken>()).Returns([1, 2, 3, 4], [5, 6, 7, 8]);
        CachedPartyKeyManagementService cache = new(inner);
        _ = await cache.GetKeyAsync("tenant-a", "p1");
        TenantKeyRotationService service = new(backend, ConfigureProgressStateStore(), Substitute.For<IKeyOperationAuditService>(), new[] { cache }, new CorrelationContextAccessor());

        await service.RotateAsync(new TenantKeyRotationRequest
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            CorrelationId = "correlation-1",
        });
        _ = await cache.GetKeyAsync("tenant-a", "p1");

        await inner.Received(2).GetKeyAsync("tenant-a", "p1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateAsync_AllowsPartyKeyReadsWhileRewrapIsInProgress()
    {
        LocalDevKeyStorageBackend realBackend = new();
        byte[] p1Key = [1, 2, 3, 4];
        byte[] p2Key = [5, 6, 7, 8];
        await realBackend.CreateSecretAsync("tenant-a/parties/p1/v1", p1Key);
        await realBackend.CreateSecretAsync("tenant-a/parties/p2/v1", p2Key);
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        WireBackendPassthrough(backend, realBackend);
        WirePartyKeyReadPassthrough(backend, realBackend);
        TaskCompletionSource rewrapStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource allowRewrapToComplete = new(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetPartyKeyWrappingMetadataAsync(
                Arg.Is<PartyKeyWrappingMetadata>(m => m.PartyId == "p2"),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                rewrapStarted.TrySetResult();
                await allowRewrapToComplete.Task.ConfigureAwait(false);
                await realBackend.SetPartyKeyWrappingMetadataAsync(call.ArgAt<PartyKeyWrappingMetadata>(0)).ConfigureAwait(false);
            });
        DaprClient daprClient = ConfigureProgressStateStore();
        IKeyOperationAuditService auditService = Substitute.For<IKeyOperationAuditService>();
        TenantKeyRotationService rotationService = new(backend, daprClient, auditService, [], new CorrelationContextAccessor());
        PartyKeyManagementService keyService = new(backend, auditService, new CorrelationContextAccessor());

        Task<TenantKeyRotationStatus> rotation = rotationService.RotateAsync(new TenantKeyRotationRequest
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            CorrelationId = "correlation-1",
        });
        await rewrapStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        byte[] p1ReadDuringRotation = await keyService.GetKeyAsync("tenant-a", "p1");
        byte[] p2ReadDuringRotation = await keyService.GetKeyAsync("tenant-a", "p2");
        allowRewrapToComplete.SetResult();
        TenantKeyRotationStatus status = await rotation.ConfigureAwait(true);

        p1ReadDuringRotation.ShouldBe(p1Key);
        p2ReadDuringRotation.ShouldBe(p2Key);
        status.Phase.ShouldBe(TenantKeyRotationPhase.Completed);
    }

    [Fact]
    public async Task RotateAsync_DoesNotCrossTenantNamespaces()
    {
        LocalDevKeyStorageBackend backend = new();
        await backend.CreateSecretAsync("tenant-a/parties/p1/v1", [1, 2, 3, 4]);
        await backend.CreateSecretAsync("tenant-b/parties/p1/v1", [5, 6, 7, 8]);
        TenantKeyRotationService service = new(backend, ConfigureProgressStateStore(), Substitute.For<IKeyOperationAuditService>(), [], new CorrelationContextAccessor());

        await service.RotateAsync(new TenantKeyRotationRequest
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            CorrelationId = "correlation-1",
        });

        PartyKeyWrappingMetadata tenantA = (await backend.GetPartyKeyWrappingMetadataAsync("tenant-a", "p1", 1)).ShouldNotBeNull();
        PartyKeyWrappingMetadata tenantB = (await backend.GetPartyKeyWrappingMetadataAsync("tenant-b", "p1", 1)).ShouldNotBeNull();
        tenantA.TenantKeyVersion.ShouldBe(2);
        tenantB.TenantKeyVersion.ShouldBe(1);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsPersistedProgressWithoutSecretDetails()
    {
        LocalDevKeyStorageBackend backend = new();
        await backend.CreateSecretAsync("tenant-a/parties/p1/v1", [1, 2, 3, 4]);
        DaprClient daprClient = ConfigureProgressStateStore();
        IKeyOperationAuditService auditService = Substitute.For<IKeyOperationAuditService>();
        TenantKeyRotationService service = new(backend, daprClient, auditService, [], new CorrelationContextAccessor());

        TenantKeyRotationStatus rotated = await service.RotateAsync(new TenantKeyRotationRequest
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            CorrelationId = "correlation-1",
        });
        TenantKeyRotationStatus? queried = await service.GetStatusAsync("tenant-a", "rotation-1");
        string json = JsonSerializer.Serialize(queried);

        queried.ShouldNotBeNull();
        queried.ShouldBe(rotated);
        json.ShouldContain("tenant-a");
        json.ShouldContain("rotation-1");
        json.ShouldNotContain("secret", Case.Insensitive);
        json.ShouldNotContain("token", Case.Insensitive);
        json.ShouldNotContain("keyMaterial", Case.Sensitive);
        json.ShouldNotContain("wrappedPartyKeyBytes", Case.Sensitive);
    }

    private static DaprClient ConfigureProgressStateStore()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        TenantKeyRotationProgress? progress = null;
        string etag = string.Empty;

        daprClient.GetStateAndETagAsync<TenantKeyRotationProgress>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (progress, etag));
        daprClient.TrySaveStateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<TenantKeyRotationProgress>(),
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                progress = call.ArgAt<TenantKeyRotationProgress>(2);
                etag = "saved";
                return true;
            });

        return daprClient;
    }

    private static void WireBackendPassthrough(IKeyStorageBackend backend, LocalDevKeyStorageBackend realBackend)
    {
        backend.GetOrCreateTenantKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => realBackend.GetOrCreateTenantKeyAsync(call.ArgAt<string>(0), call.ArgAt<string>(1)));
        backend.ListPartyKeyRecordsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => realBackend.ListPartyKeyRecordsAsync(call.ArgAt<string>(0)));
        backend.GetPartyKeyWrappingMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => realBackend.GetPartyKeyWrappingMetadataAsync(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<int>(2)));
        backend.SetPartyKeyWrappingMetadataAsync(Arg.Any<PartyKeyWrappingMetadata>(), Arg.Any<CancellationToken>())
            .Returns(call => realBackend.SetPartyKeyWrappingMetadataAsync(call.ArgAt<PartyKeyWrappingMetadata>(0)));
    }

    private static void WirePartyKeyReadPassthrough(IKeyStorageBackend backend, LocalDevKeyStorageBackend realBackend)
    {
        backend.ListKeyVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => realBackend.ListKeyVersionsAsync(call.ArgAt<string>(0), call.ArgAt<string>(1)));
        backend.ReadSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => realBackend.ReadSecretAsync(call.ArgAt<string>(0)));
    }
}
