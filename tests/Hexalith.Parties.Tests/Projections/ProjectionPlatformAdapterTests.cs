using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
