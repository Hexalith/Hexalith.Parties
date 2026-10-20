#pragma warning disable CS8620 // DaprClient nullable generic arguments in substitutes.

using Dapr.Client;
using Hexalith.Parties.Contracts.Security;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public sealed class SecurityComponentBindingTests
{
    [Theory]
    [InlineData("statestore")]
    [InlineData("parties-security-custom")]
    public async Task ErasureRecordsRoundTripThroughConfiguredComponentWithExistingKeys(string component)
    {
        DaprClient client = Substitute.For<DaprClient>();
        PartyErasureStatusRecord? saved = null;
        client.SaveStateAsync(component, "tenant:erasure-status:party", Arg.Any<PartyErasureStatusRecord>(),
            cancellationToken: Arg.Any<CancellationToken>()).Returns(call =>
            {
                saved = call.ArgAt<PartyErasureStatusRecord>(2);
                return Task.CompletedTask;
            });
        client.GetStateAsync<PartyErasureStatusRecord>(component, "tenant:erasure-status:party",
            cancellationToken: Arg.Any<CancellationToken>()).Returns(_ => saved);
        ServiceCollection services = new();
        services.AddSingleton(client);
        services.AddSingleton<IOptions<PartiesSecurityStoreOptions>>(Options.Create(new PartiesSecurityStoreOptions { StateStoreName = component }));
        services.AddSingleton<IPartyErasureRecordStore, PartyErasureRecordStore>();
        using ServiceProvider provider = services.BuildServiceProvider();
        IPartyErasureRecordStore store = provider.GetRequiredService<IPartyErasureRecordStore>();
        PartyErasureStatusRecord status = new() { TenantId = "tenant", PartyId = "party", Status = "erased", UpdatedAt = DateTimeOffset.UtcNow };
        await store.SaveStatusAsync(status);
        (await store.GetStatusAsync("tenant", "party")).ShouldBe(status);
        saved.ShouldBe(status);

        ErasureCertificate certificate = new() { TenantId = "tenant", PartyId = "party", Timestamp = DateTimeOffset.UtcNow, KeyVersionsDestroyed = [1], VerificationStatus = default };
        await store.SaveCertificateAsync(certificate);
        await client.Received(1).SaveStateAsync(component, "tenant:erasure:party", certificate, cancellationToken: Arg.Any<CancellationToken>());
        ErasureVerificationReport report = new() { TenantId = "tenant", PartyId = "party", Timestamp = DateTimeOffset.UtcNow, StoreResults = [], OverallStatus = default };
        await store.SaveVerificationReportAsync(report);
        await client.Received(1).SaveStateAsync(component, "tenant:erasure-report:party", report, cancellationToken: Arg.Any<CancellationToken>());
        await store.GetCertificateAsync("tenant", "party");
        await store.GetVerificationReportAsync("tenant", "party");
        await client.Received(1).GetStateAsync<ErasureCertificate>(component, "tenant:erasure:party", cancellationToken: Arg.Any<CancellationToken>());
        await client.Received(1).GetStateAsync<ErasureVerificationReport>(component, "tenant:erasure-report:party", cancellationToken: Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("statestore")]
    [InlineData("parties-security-custom")]
    public async Task AuditAndRotationPersistToTheConfiguredComponent(string component)
    {
        DaprClient client = Substitute.For<DaprClient>();
        List<KeyOperationAuditEntry>? audit = null;
        client.GetStateAndETagAsync<List<KeyOperationAuditEntry>>(component, "tenant:party-key-audit:tenant-key-rotation",
            cancellationToken: Arg.Any<CancellationToken>()).Returns(_ => (audit, "etag"));
        client.TrySaveStateAsync(component, "tenant:party-key-audit:tenant-key-rotation", Arg.Any<List<KeyOperationAuditEntry>>(), Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>()).Returns(call => { audit = call.ArgAt<List<KeyOperationAuditEntry>>(2); return true; });
        client.GetStateAsync<List<KeyOperationAuditEntry>>(component, "tenant:party-key-audit:tenant-key-rotation",
            cancellationToken: Arg.Any<CancellationToken>()).Returns(_ => audit);
        TenantKeyRotationProgress? progress = null;
        client.GetStateAndETagAsync<TenantKeyRotationProgress>(component, "tenant:tenant-key-rotation:rotation",
            cancellationToken: Arg.Any<CancellationToken>()).Returns(_ => (progress, "etag"));
        client.TrySaveStateAsync(component, "tenant:tenant-key-rotation:rotation", Arg.Any<TenantKeyRotationProgress>(), Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>()).Returns(call => { progress = call.ArgAt<TenantKeyRotationProgress>(2); return true; });
        IOptions<PartiesSecurityStoreOptions> options = Options.Create(new PartiesSecurityStoreOptions { StateStoreName = component });
        ServiceCollection services = new();
        services.AddSingleton(client);
        services.AddSingleton(options);
        services.AddSingleton<IKeyStorageBackend, LocalDevKeyStorageBackend>();
        services.AddSingleton<ICorrelationContextAccessor, CorrelationContextAccessor>();
        services.AddSingleton<IKeyOperationAuditService, KeyOperationAuditService>();
        services.AddSingleton<ITenantKeyRotationService, TenantKeyRotationService>();
        using ServiceProvider provider = services.BuildServiceProvider();
        IKeyOperationAuditService service = provider.GetRequiredService<IKeyOperationAuditService>();
        ITenantKeyRotationService rotation = provider.GetRequiredService<ITenantKeyRotationService>();
        TenantKeyRotationStatus status = await rotation.RotateAsync(new TenantKeyRotationRequest { TenantId = "tenant", OperationId = "rotation" });
        status.Phase.ShouldBe(TenantKeyRotationPhase.Completed);
        progress.ShouldNotBeNull().Status.ShouldBe(status);
        (await rotation.GetStatusAsync("tenant", "rotation")).ShouldBe(status);
        (await service.GetAuditTrailAsync("tenant", "tenant-key-rotation")).Single().OperationId.ShouldBe("rotation");
        audit.ShouldNotBeNull().Single().OperationType.ShouldBe(KeyOperationType.TenantRotate);
    }

    [Fact]
    public void ExistingConstructorSignaturesRemainAvailable()
    {
        typeof(PartyErasureRecordStore).GetConstructor([typeof(DaprClient)]).ShouldNotBeNull();
        typeof(KeyOperationAuditService).GetConstructor([typeof(DaprClient)]).ShouldNotBeNull();
        typeof(TenantKeyRotationService).GetConstructor([
            typeof(IKeyStorageBackend), typeof(DaprClient), typeof(IKeyOperationAuditService),
            typeof(IEnumerable<ITenantKeyRotationCacheInvalidator>), typeof(ICorrelationContextAccessor)])
            .ShouldNotBeNull();
    }

    [Fact]
    public void SecurityStoreDefaultRetainsCompatibility() => new PartiesSecurityStoreOptions().StateStoreName.ShouldBe("statestore");
}
