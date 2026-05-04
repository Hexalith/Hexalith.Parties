using System.Text.Json;

using Dapr;

using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Parties.CommandApi.Extensions;
using Hexalith.Tenants.Client.Configuration;
using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Client.Registration;
using Hexalith.Tenants.Client.Subscription;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Events;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Routing;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Tenants;

public class TenantEventInfrastructureTests {
    [Fact]
    public void AddPartiesRegistersTenantsProjectionPipelineAndAccessService() {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:PubSubName"] = "pubsub",
                ["Tenants:TopicName"] = "system.tenants.events",
                ["Tenants:CommandApiAppId"] = "commandapi",
            })
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(configuration);

        services.AddParties(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<ITenantProjectionStore>().ShouldNotBeNull();
        provider.GetRequiredService<TenantEventProcessor>().ShouldNotBeNull();
        provider.GetRequiredService<ITenantAccessService>().ShouldNotBeNull();
        HexalithTenantsOptions options = provider.GetRequiredService<IOptions<HexalithTenantsOptions>>().Value;
        options.PubSubName.ShouldBe("pubsub");
        options.TopicName.ShouldBe("system.tenants.events");
        options.CommandApiAppId.ShouldBe("commandapi");
    }

    [Fact]
    public async Task TenantEventProcessorAppliesSupportedEventsAndDeduplicatesByMessageId() {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHexalithTenants();
        using ServiceProvider provider = services.BuildServiceProvider();
        TenantEventProcessor processor = provider.GetRequiredService<TenantEventProcessor>();

        (await processor.ProcessAsync(Envelope("message-1", new TenantCreated("tenant-1", "Tenant One", null, DateTimeOffset.UtcNow))))
            .ShouldBe(TenantEventProcessingResult.Processed);
        (await processor.ProcessAsync(Envelope("message-2", new UserAddedToTenant("tenant-1", "user-1", TenantRole.TenantContributor))))
            .ShouldBe(TenantEventProcessingResult.Processed);
        (await processor.ProcessAsync(Envelope("message-2", new UserAddedToTenant("tenant-1", "user-1", TenantRole.TenantReader))))
            .ShouldBe(TenantEventProcessingResult.Duplicate);
        (await processor.ProcessAsync(Envelope("message-3", new TenantDisabled("tenant-1", DateTimeOffset.UtcNow))))
            .ShouldBe(TenantEventProcessingResult.Processed);

        TenantLocalState state = (await provider.GetRequiredService<ITenantProjectionStore>().GetAsync("tenant-1"))!;
        state.Status.ShouldBe(TenantStatus.Disabled);
        state.Members["user-1"].ShouldBe(TenantRole.TenantContributor);
    }

    [Fact]
    public async Task TenantEventProcessorRemovesUsersAndFailsInvalidPayloadWithoutPoisoningMessageId() {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHexalithTenants();
        using ServiceProvider provider = services.BuildServiceProvider();
        TenantEventProcessor processor = provider.GetRequiredService<TenantEventProcessor>();

        _ = await processor.ProcessAsync(Envelope("message-1", new TenantCreated("tenant-1", "Tenant One", null, DateTimeOffset.UtcNow)));
        _ = await processor.ProcessAsync(Envelope("message-2", new UserAddedToTenant("tenant-1", "user-1", TenantRole.TenantOwner)));
        (await processor.ProcessAsync(Envelope("message-3", new UserRemovedFromTenant("tenant-1", "user-1"))))
            .ShouldBe(TenantEventProcessingResult.Processed);
        (await processor.ProcessAsync(new TenantEventEnvelope(
            "bad-message",
            "tenant-1",
            "system",
            typeof(TenantCreated).FullName!,
            4,
            DateTimeOffset.UtcNow,
            "correlation-1",
            "json",
            "{ not-json"u8.ToArray())))
            .ShouldBe(TenantEventProcessingResult.FailedInvalidPayload);
        (await processor.ProcessAsync(Envelope("unknown-message", "Hexalith.Tenants.Contracts.Events.DoesNotExist", "{}"u8.ToArray())))
            .ShouldBe(TenantEventProcessingResult.SkippedUnknownEventType);

        TenantLocalState state = (await provider.GetRequiredService<ITenantProjectionStore>().GetAsync("tenant-1"))!;
        state.Members.ContainsKey("user-1").ShouldBeFalse();
    }

    [Fact]
    public async Task MapTenantEventSubscriptionMapsConfiguredRouteAndTopicMetadata() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services.AddHexalithTenants(options =>
        {
            options.PubSubName = "pubsub";
            options.TopicName = "system.tenants.events";
        });

        await using WebApplication app = builder.Build();
        app.MapSubscribeHandler();
        app.MapTenantEventSubscription();

        RouteEndpoint endpoint = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/tenants/events");

        ITopicMetadata topic = endpoint.Metadata.GetMetadata<ITopicMetadata>()!;
        topic.ShouldNotBeNull();
        topic.PubsubName.ShouldBe("pubsub");
        topic.Name.ShouldBe("system.tenants.events");
    }

    private static TenantEventEnvelope Envelope<TEvent>(string messageId, TEvent @event)
        => Envelope(messageId, typeof(TEvent).FullName!, JsonSerializer.SerializeToUtf8Bytes(@event));

    private static TenantEventEnvelope Envelope(string messageId, string eventTypeName, byte[] payload)
        => new(
            messageId,
            "tenant-1",
            "system",
            eventTypeName,
            1,
            DateTimeOffset.UtcNow,
            "correlation-1",
            "json",
            payload);
}
