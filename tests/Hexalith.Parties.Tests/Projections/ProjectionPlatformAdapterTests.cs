using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Queries;
using Hexalith.Parties.Search;
using Hexalith.Parties.Security;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

public sealed class ProjectionPlatformAdapterTests
{
    [Fact]
    public void AddParties_UsesSdkReadModelsAndCursorCodecWithoutLocalProjectionMechanics()
    {
        IServiceCollection services = CreatePartiesServices();

        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(IReadModelStore));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(IQueryCursorCodec));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(TimeProvider));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(PartySdkQueryService));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(PartySdkLastKnownReadModelCache));
        services.ShouldNotContain(static descriptor => string.Equals(
            descriptor.ServiceType.FullName,
            "Hexalith.Parties.Projections.Services.IPartyProjectionPlatformAdapter",
            StringComparison.Ordinal));
        services.ShouldNotContain(static descriptor => string.Equals(
            descriptor.ServiceType.FullName,
            "Hexalith.Parties.Projections.Services.IProjectionRebuildService",
            StringComparison.Ordinal));
        services.ShouldNotContain(static descriptor => string.Equals(
            descriptor.ServiceType.FullName,
            "Hexalith.EventStore.Server.Projections.IProjectionUpdateOrchestrator",
            StringComparison.Ordinal));
    }

    [Fact]
    public void AddParties_ResolvesSdkQueryServiceCursorCodecAndInjectedLastKnownCacheClock()
    {
        IServiceCollection services = CreatePartiesServices();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<PartySdkQueryService>().ShouldNotBeNull();
        provider.GetRequiredService<IDataProtectionProvider>().ShouldNotBeNull();
        provider.GetRequiredService<IQueryCursorCodec>().ShouldNotBeNull();
        provider.GetRequiredService<PartySdkLastKnownReadModelCache>().ShouldNotBeNull();
        provider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }

    [Fact]
    public void AddParties_RejectsInvalidPartySdkReadModelOptionsOnAccess()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = "hexalith-test",
                ["Authentication:JwtBearer:Audience"] = "hexalith-parties",
                ["Authentication:JwtBearer:SigningKey"] = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!",
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                ["Tenants:PubSubName"] = "pubsub",
                ["Tenants:TopicName"] = "system.tenants.events",
                ["EventStore:Projections:ReadModelStateStoreName"] = " ",
                ["EventStore:Projections:FreshnessAgingSeconds"] = "30",
                ["EventStore:Projections:FreshnessStaleSeconds"] = "10",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddParties(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException ex = Should.Throw<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<PartySdkReadModelOptions>>().Value);
        ex.Message.ShouldContain("Party SDK");
    }

    [Fact]
    public void AddParties_RegistersEventStorePayloadProtectionAdapterWithDomainProvider()
    {
        IServiceCollection services = CreatePartiesServices();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IEventPayloadProtectionService>()
            .ShouldBeOfType<EventStorePartyPayloadProtectionAdapter>();
        provider.GetRequiredService<PartyPayloadProtectionService>()
            .ShouldNotBeNull();
    }

    [Fact]
    public async Task AddParties_ErasureCleanupDelegatesInvokeRealEraserAndCacheEvictionWithCorrectKeysAsync()
    {
        const string tenantId = "tenant-a";
        const string partyId = "party-1";
        IReadModelStore readModelStore = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        readModelStore.GetAsync<PartyDetailSdkReadModel>("statestore", PartySdkReadModelAddresses.Detail(tenantId, partyId), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        readModelStore.GetAsync<PartyProcessingSdkReadModel>("statestore", PartySdkReadModelAddresses.Processing(tenantId, partyId), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        readModelStore.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index(tenantId), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(null, null));
        batchStore.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>())
            .Returns(ReadModelBatchResult.Completed("fingerprint"));

        IServiceCollection services = CreatePartiesServices();
        IPartyMemoryUnitMappingStore mappingStore = new EmptyMappingStore();
        services.RemoveAll<IReadModelStore>();
        services.RemoveAll<IReadModelBatchStore>();
        services.RemoveAll<IPartyMemoryUnitMappingStore>();
        services.AddSingleton(readModelStore);
        services.AddSingleton(batchStore);
        services.AddSingleton(mappingStore);
        using ServiceProvider provider = services.BuildServiceProvider();

        PartySdkLastKnownReadModelCache cache = provider.GetRequiredService<PartySdkLastKnownReadModelCache>();
        cache.StoreDetail(tenantId, partyId, new PartyDetailSdkReadModel
        {
            LastSequenceNumber = 1,
            Detail = new PartyDetail
            {
                Id = partyId,
                Type = PartyType.Person,
                DisplayName = "Pre-erasure PII",
                SortName = "pre-erasure",
            },
        });
        cache.StoreProcessing(tenantId, partyId, new PartyProcessingSdkReadModel { LastSequenceNumber = 1 });
        cache.StoreIndex(tenantId, new PartyIndexSdkReadModel
        {
            Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                [partyId] = new()
                {
                    Id = partyId,
                    Type = PartyType.Person,
                    DisplayName = "Pre-erasure PII",
                },
            },
            LastSequenceNumbers = new Dictionary<string, long>(StringComparer.Ordinal) { [partyId] = 1 },
        });
        cache.TryGetDetail(tenantId, partyId, out _).ShouldBeTrue();
        cache.TryGetProcessing(tenantId, partyId, out _).ShouldBeTrue();
        cache.TryGetIndex(tenantId, out _).ShouldBeTrue();

        IReadOnlyList<ErasureStoreCleanupDelegate> cleanups =
            provider.GetRequiredService<IReadOnlyList<ErasureStoreCleanupDelegate>>();
        cleanups.Count.ShouldBe(5);

        Dictionary<string, ErasureStoreCleanupStatus> statuses = new(StringComparer.Ordinal);
        foreach (ErasureStoreCleanupDelegate cleanup in cleanups)
        {
            ErasureVerificationStoreResult result = await cleanup(tenantId, partyId, TestContext.Current.CancellationToken);
            statuses[result.StoreName] = result.Status;
        }

        statuses["sdk-read-models"].ShouldBe(ErasureStoreCleanupStatus.Cleaned);
        statuses["projection-cache"].ShouldBe(ErasureStoreCleanupStatus.Cleaned);
        statuses["aggregate-readable-state"].ShouldBe(ErasureStoreCleanupStatus.NotApplicable);
        statuses["snapshots"].ShouldBe(ErasureStoreCleanupStatus.NotApplicable);
        statuses["memories-search"].ShouldBe(ErasureStoreCleanupStatus.Cleaned);

        cache.TryGetDetail(tenantId, partyId, out _).ShouldBeFalse();
        cache.TryGetProcessing(tenantId, partyId, out _).ShouldBeFalse();
        cache.TryGetIndex(tenantId, out _).ShouldBeFalse();

        await readModelStore.Received(1).GetAsync<PartyDetailSdkReadModel>(
            "statestore", PartySdkReadModelAddresses.Detail(tenantId, partyId), Arg.Any<CancellationToken>());
        await readModelStore.Received(1).GetAsync<PartyProcessingSdkReadModel>(
            "statestore", PartySdkReadModelAddresses.Processing(tenantId, partyId), Arg.Any<CancellationToken>());
        await readModelStore.Received(1).GetAsync<PartyIndexSdkReadModel>(
            "statestore", PartySdkReadModelAddresses.Index(tenantId), Arg.Any<CancellationToken>());
        await batchStore.Received(1).ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddParties_FailingSdkBatchProducesNonCompleteVerificationInsteadOfD15CleanedAsync()
    {
        const string tenantId = "tenant-a";
        const string partyId = "party-1";
        IReadModelStore readModelStore = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        readModelStore.GetAsync<PartyDetailSdkReadModel>(
                "statestore", PartySdkReadModelAddresses.Detail(tenantId, partyId), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        readModelStore.GetAsync<PartyProcessingSdkReadModel>(
                "statestore", PartySdkReadModelAddresses.Processing(tenantId, partyId), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        readModelStore.GetAsync<PartyIndexSdkReadModel>(
                "statestore", PartySdkReadModelAddresses.Index(tenantId), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(null, null));
        batchStore.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>())
            .Returns(ReadModelBatchResult.Indeterminate("fingerprint", "transaction-dispatch"));
        IServiceCollection services = CreatePartiesServices();
        services.RemoveAll<IReadModelStore>();
        services.RemoveAll<IReadModelBatchStore>();
        services.AddSingleton(readModelStore);
        services.AddSingleton(batchStore);
        using ServiceProvider provider = services.BuildServiceProvider();
        IErasureVerificationService verification = provider.GetRequiredService<IErasureVerificationService>();

        ErasureVerificationReport report = await verification.VerifyErasureAsync(
            tenantId,
            partyId,
            new ErasureCertificate
            {
                PartyId = partyId,
                TenantId = tenantId,
                Timestamp = DateTimeOffset.UnixEpoch,
                KeyVersionsDestroyed = [1],
                VerificationStatus = ErasureVerificationStatus.Pending,
            },
            TestContext.Current.CancellationToken);

        report.OverallStatus.ShouldNotBe(ErasureVerificationOverallStatus.Complete);
        ErasureVerificationStoreResult sdk = report.StoreResults.Single(static result => result.StoreName == "sdk-read-models");
        sdk.Status.ShouldBe(ErasureStoreCleanupStatus.Failed);
        (sdk.ErrorMessage ?? string.Empty).ShouldNotContain("transaction-dispatch");
    }

    [Fact]
    public async Task AddParties_CanceledSdkReadModelCleanupRethrowsInsteadOfFailedAsync()
    {
        const string tenantId = "tenant-a";
        const string partyId = "party-1";
        IServiceCollection services = CreatePartiesServices();
        using ServiceProvider provider = services.BuildServiceProvider();
        IReadOnlyList<ErasureStoreCleanupDelegate> cleanups =
            provider.GetRequiredService<IReadOnlyList<ErasureStoreCleanupDelegate>>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        OperationCanceledException thrown = await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            foreach (ErasureStoreCleanupDelegate cleanup in cleanups)
            {
                ErasureVerificationStoreResult result = await cleanup(tenantId, partyId, cts.Token)
                    .ConfigureAwait(false);
                if (string.Equals(result.StoreName, "sdk-read-models", StringComparison.Ordinal))
                {
                    result.Status.ShouldNotBe(ErasureStoreCleanupStatus.Failed);
                }
            }
        });

        thrown.ShouldNotBeNull();
    }

    private static IServiceCollection CreatePartiesServices()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = "hexalith-test",
                ["Authentication:JwtBearer:Audience"] = "hexalith-parties",
                ["Authentication:JwtBearer:SigningKey"] = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!",
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                ["Tenants:PubSubName"] = "pubsub",
                ["Tenants:TopicName"] = "system.tenants.events",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddParties(configuration);
        return services;
    }

    private sealed class EmptyMappingStore : IPartyMemoryUnitMappingStore
    {
        public Task RecordMappingAsync(
            string tenantId,
            string partyId,
            string memoryUnitId,
            string sourceUri,
            string caseId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
            string tenantId,
            string partyId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PartyMemoryUnitMappingEntry>>([]);

        public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ReplaceMappingsAsync(
            string tenantId,
            string partyId,
            IReadOnlyList<PartyMemoryUnitMappingEntry> entries,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
