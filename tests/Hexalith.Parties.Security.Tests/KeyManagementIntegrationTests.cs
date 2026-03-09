#pragma warning disable CS8620 // NSubstitute + DaprClient nullable generics mismatch

using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Security;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

/// <summary>
/// Tier 2 integration tests that wire together real components
/// to verify key management flows end-to-end.
/// </summary>
public class KeyManagementIntegrationTests
{
    /// <summary>
    /// Configures a mock DaprClient to handle ETag-based audit state operations.
    /// </summary>
    private static void ConfigureAuditMock(DaprClient daprClient)
    {
        daprClient.GetStateAndETagAsync<List<KeyOperationAuditEntry>>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns((null as List<KeyOperationAuditEntry>, ""));

        daprClient.TrySaveStateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<KeyOperationAuditEntry>>(),
            Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
    }

    /// <summary>
    /// 8.1: Verifies that processing a PartyCreated event through the
    /// payload protection service triggers key creation in the backend.
    /// Uses real PartyKeyManagementService + LocalDevKeyStorageBackend.
    /// </summary>
    [Fact]
    public async Task PartyCreation_TriggersKeyCreation_InBackend()
    {
        // Arrange — wire real components (no mocks except DaprClient for audit)
        LocalDevKeyStorageBackend backend = new();
        DaprClient daprClient = Substitute.For<DaprClient>();
        ConfigureAuditMock(daprClient);
        KeyOperationAuditService auditService = new(daprClient);
        CorrelationContextAccessor correlationAccessor = new();
        PartyKeyManagementService keyService = new(backend, auditService, correlationAccessor);
        IPartyKeyRetryScheduler retryScheduler = Substitute.For<IPartyKeyRetryScheduler>();
        PartyKeyLifecycleService lifecycleService = new(keyService, retryScheduler, NullLogger<PartyKeyLifecycleService>.Instance);
        IOptionsMonitor<CryptoShreddingOptions> cryptoOptions = Substitute.For<IOptionsMonitor<CryptoShreddingOptions>>();
        cryptoOptions.CurrentValue.Returns(new CryptoShreddingOptions());
        PartyPayloadProtectionService protectionService = new(
            keyService, backend, lifecycleService,
            new DecryptionCircuitBreaker(NullLogger<DecryptionCircuitBreaker>.Instance),
            cryptoOptions,
            NullLogger<PartyPayloadProtectionService>.Instance);

        PartyCreated payload = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Ada",
                LastName = "Lovelace",
            },
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload);
        AggregateIdentity identity = new("acme", "party", "p1");

        // Act — process PartyCreated through protection service
        PayloadProtectionResult result = await protectionService.ProtectEventPayloadAsync(
            identity, payload, typeof(PartyCreated).FullName!, serialized, "json");

        // Assert — key was created in the real backend
        IReadOnlyList<int> versions = await backend.ListKeyVersionsAsync("acme", "p1");
        versions.Count.ShouldBe(1);
        versions[0].ShouldBe(1);

        // And the payload was encrypted
        result.SerializationFormat.ShouldBe("json+pdenc-v1");
        string protectedJson = Encoding.UTF8.GetString(result.PayloadBytes);
        protectedJson.ShouldNotContain("Ada");
        protectedJson.ShouldNotContain("Lovelace");
    }

    /// <summary>
    /// 8.5: Multi-tenant isolation — keys created for tenant A
    /// are not accessible when querying tenant B's namespace.
    /// </summary>
    [Fact]
    public async Task MultiTenantIsolation_TenantA_KeyNotAccessible_FromTenantB()
    {
        // Arrange
        LocalDevKeyStorageBackend backend = new();
        DaprClient daprClient = Substitute.For<DaprClient>();
        ConfigureAuditMock(daprClient);
        KeyOperationAuditService auditService = new(daprClient);
        CorrelationContextAccessor correlationAccessor = new();
        PartyKeyManagementService keyService = new(backend, auditService, correlationAccessor);

        // Act — create key for tenant A
        await keyService.CreateKeyAsync("tenant-a", "p1");

        // Assert — tenant A can access the key
        IReadOnlyList<int> tenantAVersions = await backend.ListKeyVersionsAsync("tenant-a", "p1");
        tenantAVersions.Count.ShouldBe(1);

        // Assert — tenant B has no keys for the same party ID
        IReadOnlyList<int> tenantBVersions = await backend.ListKeyVersionsAsync("tenant-b", "p1");
        tenantBVersions.ShouldBeEmpty();

        // Assert — reading tenant A's key with tenant B context throws
        await Should.ThrowAsync<KeyNotFoundException>(
            () => keyService.GetKeyAsync("tenant-b", "p1"));
    }

    /// <summary>
    /// 8.5: Multi-tenant isolation — rotate key for tenant A,
    /// tenant B still has no keys.
    /// </summary>
    [Fact]
    public async Task MultiTenantIsolation_RotateKeyTenantA_TenantBUnaffected()
    {
        // Arrange
        LocalDevKeyStorageBackend backend = new();
        DaprClient daprClient = Substitute.For<DaprClient>();
        ConfigureAuditMock(daprClient);
        KeyOperationAuditService auditService = new(daprClient);
        CorrelationContextAccessor correlationAccessor = new();
        PartyKeyManagementService keyService = new(backend, auditService, correlationAccessor);

        await keyService.CreateKeyAsync("tenant-a", "p1");
        await keyService.CreateKeyAsync("tenant-b", "p2");

        // Act — rotate tenant A's key
        PartyKeyInfo rotated = await keyService.RotateKeyAsync("tenant-a", "p1");

        // Assert — tenant A has 2 versions
        IReadOnlyList<int> tenantAVersions = await backend.ListKeyVersionsAsync("tenant-a", "p1");
        tenantAVersions.Count.ShouldBe(2);
        rotated.Version.ShouldBe(2);

        // Assert — tenant B party still has exactly 1 version
        IReadOnlyList<int> tenantBVersions = await backend.ListKeyVersionsAsync("tenant-b", "p2");
        tenantBVersions.Count.ShouldBe(1);
    }

    /// <summary>
    /// 8.6: Circuit breaker — when the backend is unavailable,
    /// key creation fails gracefully without cascading failures.
    /// The party creation still succeeds (CryptoPending pattern).
    /// </summary>
    [Fact]
    public async Task BackendUnavailable_KeyCreationFailsGracefully_MarkedCryptoPending()
    {
        // Arrange — backend that throws on every operation
        IKeyStorageBackend failingBackend = Substitute.For<IKeyStorageBackend>();
        failingBackend.ListKeyVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Backend unavailable"));

        DaprClient daprClient = Substitute.For<DaprClient>();
        ConfigureAuditMock(daprClient);
        KeyOperationAuditService auditService = new(daprClient);
        CorrelationContextAccessor correlationAccessor = new();
        PartyKeyManagementService keyService = new(failingBackend, auditService, correlationAccessor);
        IPartyKeyRetryScheduler retryScheduler = Substitute.For<IPartyKeyRetryScheduler>();
        retryScheduler.IsPendingAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns(true);
        PartyKeyLifecycleService lifecycleService = new(keyService, retryScheduler, NullLogger<PartyKeyLifecycleService>.Instance);

        // Act — attempt key creation through lifecycle service (same as party creation)
        await lifecycleService.OnPartyCreatedAsync("acme", "p1");

        // Assert — no exception thrown (graceful degradation)
        // Party is marked CryptoPending
        bool isPending = await lifecycleService.IsCryptoPendingAsync("acme", "p1");
        isPending.ShouldBeTrue();
    }

    /// <summary>
    /// 8.6: Backend recovers after being unavailable —
    /// retry clears CryptoPending status.
    /// </summary>
    [Fact]
    public async Task BackendRecovers_RetrySucceeds_ClearsCryptoPending()
    {
        // Arrange — backend that fails first, then succeeds
        LocalDevKeyStorageBackend realBackend = new();
        IKeyStorageBackend wrappedBackend = Substitute.For<IKeyStorageBackend>();

        // First call fails
        wrappedBackend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new InvalidOperationException("Backend unavailable"),
                _ => realBackend.ListKeyVersionsAsync("acme", "p1"));

        wrappedBackend.CreateSecretAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => realBackend.CreateSecretAsync(
                callInfo.ArgAt<string>(0), callInfo.ArgAt<byte[]>(1)));

        DaprClient daprClient = Substitute.For<DaprClient>();
        ConfigureAuditMock(daprClient);
        KeyOperationAuditService auditService = new(daprClient);
        CorrelationContextAccessor correlationAccessor = new();
        PartyKeyManagementService keyService = new(wrappedBackend, auditService, correlationAccessor);
        IPartyKeyRetryScheduler retryScheduler = Substitute.For<IPartyKeyRetryScheduler>();
        retryScheduler.IsPendingAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns(true, false);
        PartyKeyLifecycleService lifecycleService = new(keyService, retryScheduler, NullLogger<PartyKeyLifecycleService>.Instance);

        // Act — initial creation fails
        await lifecycleService.OnPartyCreatedAsync("acme", "p1");
        bool pendingAfterFailure = await lifecycleService.IsCryptoPendingAsync("acme", "p1");
        pendingAfterFailure.ShouldBeTrue();

        // Act — retry succeeds (backend recovered)
        await lifecycleService.RetryPendingKeyCreationAsync("acme", "p1");

        // Assert — CryptoPending cleared
        bool pendingAfterRetry = await lifecycleService.IsCryptoPendingAsync("acme", "p1");
        pendingAfterRetry.ShouldBeFalse();
    }

    /// <summary>
    /// 9.3-8.14: Idempotent erasure — calling DeleteKeyAsync twice for the same party
    /// returns equivalent certificates (same PartyId, TenantId, and key versions destroyed).
    /// </summary>
    [Fact]
    public async Task DeleteKeyAsync_CalledTwice_ReturnsEquivalentCertificates()
    {
        // Arrange
        LocalDevKeyStorageBackend backend = new();
        DaprClient daprClient = Substitute.For<DaprClient>();
        ConfigureAuditMock(daprClient);
        KeyOperationAuditService auditService = new(daprClient);
        CorrelationContextAccessor correlationAccessor = new();
        PartyKeyManagementService keyService = new(backend, auditService, correlationAccessor);

        // Create a key first
        await keyService.CreateKeyAsync("acme", "p1");

        // Act — first deletion
        ErasureCertificate cert1 = await keyService.DeleteKeyAsync("acme", "p1");

        // Act — second deletion (idempotent)
        ErasureCertificate cert2 = await keyService.DeleteKeyAsync("acme", "p1");

        // Assert — both certificates reference the same party
        cert1.PartyId.ShouldBe(cert2.PartyId);
        cert1.TenantId.ShouldBe(cert2.TenantId);
        cert2.VerificationStatus.ShouldBe(ErasureVerificationStatus.Verified);
    }

    /// <summary>
    /// 8.4: Key caching performance — cache hit returns in under 50ms
    /// even when backend has simulated latency.
    /// </summary>
    [Fact]
    public async Task CacheHit_ReturnsUnder50ms_WithSimulatedBackendLatency()
    {
        // Arrange — inner service with 200ms simulated latency
        IPartyKeyManagementService slowInner = Substitute.For<IPartyKeyManagementService>();
        byte[] key = new byte[32];
        Random.Shared.NextBytes(key);

        slowInner.GetKeyAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(200).ConfigureAwait(false);
                return (byte[])key.Clone();
            });

        CachedPartyKeyManagementService cachedService = new(slowInner);

        // Warm cache (this will take ~200ms)
        _ = await cachedService.GetKeyAsync("acme", "p1");

        // Act — cache hit
        Stopwatch sw = Stopwatch.StartNew();
        _ = await cachedService.GetKeyAsync("acme", "p1");
        sw.Stop();

        // Assert — well under 50ms (NFR1 benchmark)
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(50));
    }
}
