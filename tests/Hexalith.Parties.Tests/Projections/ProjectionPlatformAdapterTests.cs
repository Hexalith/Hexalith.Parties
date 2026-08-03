using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Queries;
using Hexalith.Parties.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        readModelStore.GetAsync<PartyDetailSdkReadModel>("statestore", PartySdkReadModelAddresses.Detail(tenantId, partyId), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        readModelStore.GetAsync<PartyProcessingSdkReadModel>("statestore", PartySdkReadModelAddresses.Processing(tenantId, partyId), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        readModelStore.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index(tenantId), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(null, null));
        readModelStore.TrySaveAsync(
                "statestore", Arg.Any<string>(), Arg.Any<PartyDetailSdkReadModel>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        readModelStore.TrySaveAsync(
                "statestore", Arg.Any<string>(), Arg.Any<PartyProcessingSdkReadModel>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        readModelStore.TrySaveAsync(
                "statestore", Arg.Any<string>(), Arg.Any<PartyIndexSdkReadModel>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        IServiceCollection services = CreatePartiesServices();
        services.RemoveAll<IReadModelStore>();
        services.AddSingleton(readModelStore);
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
        statuses["memories-search"].ShouldBe(ErasureStoreCleanupStatus.NotApplicable);

        cache.TryGetDetail(tenantId, partyId, out _).ShouldBeFalse();
        cache.TryGetProcessing(tenantId, partyId, out _).ShouldBeFalse();
        cache.TryGetIndex(tenantId, out _).ShouldBeFalse();

        await readModelStore.Received(1).GetAsync<PartyDetailSdkReadModel>(
            "statestore", PartySdkReadModelAddresses.Detail(tenantId, partyId), Arg.Any<CancellationToken>());
        await readModelStore.Received(1).GetAsync<PartyProcessingSdkReadModel>(
            "statestore", PartySdkReadModelAddresses.Processing(tenantId, partyId), Arg.Any<CancellationToken>());
        await readModelStore.Received(1).GetAsync<PartyIndexSdkReadModel>(
            "statestore", PartySdkReadModelAddresses.Index(tenantId), Arg.Any<CancellationToken>());
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
}
