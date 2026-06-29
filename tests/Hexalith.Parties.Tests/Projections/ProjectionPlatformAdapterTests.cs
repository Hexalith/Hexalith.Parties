using System.Net;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

public sealed class ProjectionPlatformAdapterTests
{
    [Fact]
    public void AddParties_DefaultProjectionPlatformMode_UsesEventStoreAdapter()
    {
        using ServiceProvider provider = CreatePartiesServiceProvider(new Dictionary<string, string?>());

        provider.GetRequiredService<IPartyProjectionPlatformAdapter>()
            .ShouldBeOfType<EventStorePartyProjectionPlatformAdapter>();
    }

    [Fact]
    public void AddParties_LocalProjectionPlatformMode_UsesRollbackAdapter()
    {
        using ServiceProvider provider = CreatePartiesServiceProvider(new Dictionary<string, string?>
        {
            ["Parties:Projections:PlatformAdapterMode"] = nameof(PartyProjectionPlatformAdapterMode.Local),
        });

        provider.GetRequiredService<IPartyProjectionPlatformAdapter>()
            .ShouldBeOfType<LocalPartyProjectionPlatformAdapter>();
    }

    [Theory]
    [InlineData(ReadModelFreshnessState.Current, ProjectionFreshnessStatus.Current)]
    [InlineData(ReadModelFreshnessState.Aging, ProjectionFreshnessStatus.Current)]
    [InlineData(ReadModelFreshnessState.Stale, ProjectionFreshnessStatus.Stale)]
    [InlineData(ReadModelFreshnessState.Unknown, ProjectionFreshnessStatus.Unavailable)]
    public void MapFreshness_EventStoreState_PreservesPartiesStatusVocabulary(
        ReadModelFreshnessState state,
        ProjectionFreshnessStatus expected)
    {
        ProjectionFreshnessMetadata result = EventStorePartyProjectionPlatformAdapter.MapFreshness(state);

        result.Status.ShouldBe(expected);
    }

    [Fact]
    public void MapFreshness_ActiveRebuild_MapsToRebuildingWithExistingWarningCode()
    {
        ProjectionFreshnessMetadata result = EventStorePartyProjectionPlatformAdapter.MapFreshness(
            ReadModelFreshnessState.Current,
            isRebuilding: true);

        result.Status.ShouldBe(ProjectionFreshnessStatus.Rebuilding);
        result.WarningCodes.ShouldContain(ProjectionFreshnessMetadata.WarningProjectionRebuilding);
    }

    [Fact]
    public async Task EventStoreAdapter_SaveRebuildCheckpoint_MapsDetailScopeAndKeepsLocalCheckpointAsync()
    {
        ProjectionRebuildCheckpointScope? savedScope = null;
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        IProjectionRebuildCheckpointStore rebuildStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        rebuildStore
            .SaveAsync(
                Arg.Do<ProjectionRebuildCheckpointScope>(scope => savedScope = scope),
                7,
                ProjectionRebuildStatus.Running,
                null,
                Arg.Any<CancellationToken>(),
                null,
                false)
            .Returns(new ProjectionRebuildCheckpointSaveResult(
                true,
                null,
                new ProjectionRebuildCheckpoint(
                    "tenant-a",
                    "party",
                    "party-detail",
                    "party-1",
                    null,
                    7,
                    ProjectionRebuildStatus.Running,
                    DateTimeOffset.UtcNow,
                    null)));
        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            "/v1.0/actors/PartyIndexProjectionActor/tenant-a%3Aparty-index/state",
            null,
            HttpStatusCode.NoContent,
            HttpMethod.Put);
        var local = new LocalPartyProjectionPlatformAdapter(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3500") });
        var sut = new EventStorePartyProjectionPlatformAdapter(
            checkpointTracker,
            rebuildStore,
            local,
            Substitute.For<ILogger<EventStorePartyProjectionPlatformAdapter>>());

        await sut.SaveRebuildCheckpointAsync(
            PartyProjectionRebuildScope.Detail("tenant-a", "party-1"),
            new PartyProjectionRebuildCheckpoint("party-1", 7),
            CancellationToken.None);

        savedScope.ShouldNotBeNull();
        savedScope.Tenant.ShouldBe("tenant-a");
        savedScope.Domain.ShouldBe("party");
        savedScope.ProjectionName.ShouldBe("party-detail");
        savedScope.AggregateId.ShouldBe("party-1");
        handler.Requests.ShouldContain(request => request.Body?.Contains("tenant-a:rebuild-checkpoint:detail:party-1", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task EventStoreAdapter_DeleteRebuildCheckpoint_CompletionFailureSurfacesAfterLocalCleanupAsync()
    {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        IProjectionRebuildCheckpointStore rebuildStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        rebuildStore
            .SaveAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                0,
                ProjectionRebuildStatus.Succeeded,
                null,
                Arg.Any<CancellationToken>(),
                null,
                false)
            .Returns(new ProjectionRebuildCheckpointSaveResult(false, "write-conflict", null));
        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            "/v1.0/actors/PartyIndexProjectionActor/tenant-a%3Aparty-index/state",
            null,
            HttpStatusCode.NoContent,
            HttpMethod.Put);
        var local = new LocalPartyProjectionPlatformAdapter(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3500") });
        var sut = new EventStorePartyProjectionPlatformAdapter(
            checkpointTracker,
            rebuildStore,
            local,
            Substitute.For<ILogger<EventStorePartyProjectionPlatformAdapter>>());

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.DeleteRebuildCheckpointAsync(PartyProjectionRebuildScope.Index("tenant-a"), CancellationToken.None));

        exception.Message.ShouldBe("Projection rebuild checkpoint completion save failed.");
        handler.Requests.ShouldContain(request => request.Method == HttpMethod.Put
            && request.Body?.Contains("\"delete\"", StringComparison.Ordinal) == true
            && request.Body.Contains("tenant-a:rebuild-checkpoint:index", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LocalAdapter_ReadAndDeleteRebuildCheckpoint_UsesExistingLocalStateKeyAsync()
    {
        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            "/v1.0/actors/PartyIndexProjectionActor/tenant-a%3Aparty-index/state/tenant-a%3Arebuild-checkpoint%3Aindex",
            """{"partyId":"party-2","sequenceNumber":5}""");
        handler.AddResponse(
            "/v1.0/actors/PartyIndexProjectionActor/tenant-a%3Aparty-index/state",
            null,
            HttpStatusCode.NoContent,
            HttpMethod.Put);
        var sut = new LocalPartyProjectionPlatformAdapter(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3500") });

        PartyProjectionRebuildCheckpoint? checkpoint = await sut.ReadRebuildCheckpointAsync(
            PartyProjectionRebuildScope.Index("tenant-a"),
            CancellationToken.None);
        await sut.DeleteRebuildCheckpointAsync(PartyProjectionRebuildScope.Index("tenant-a"), CancellationToken.None);

        checkpoint.ShouldNotBeNull();
        checkpoint.PartyId.ShouldBe("party-2");
        checkpoint.SequenceNumber.ShouldBe(5);
        handler.Requests.ShouldContain(request => request.Method == HttpMethod.Put
            && request.Body?.Contains("\"delete\"", StringComparison.Ordinal) == true
            && request.Body.Contains("tenant-a:rebuild-checkpoint:index", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProjectionDelivery_OutOfOrderEvents_SavesPlatformCheckpointAfterBothActorsAcceptInSequenceAsync()
    {
        var context = CreateOrchestrator([
                CreateEnvelope(sequenceNumber: 2),
                CreateEnvelope(sequenceNumber: 1),
            ]);

        await context.Sut.DeliverProjectionAsync(new AggregateIdentity("tenant-a", "party", "party-1"), CancellationToken.None);

        long[] savedSequences = context.Adapter
            .ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IPartyProjectionPlatformAdapter.TrySaveDeliveredSequenceAsync))
            .Select(call => (long)call.GetArguments()[2]!)
            .ToArray();
        savedSequences.ShouldBe([1L, 2L]);
    }

    [Fact]
    public async Task ProjectionDelivery_IndexFailure_DoesNotSavePlatformCheckpointAfterDetailOnlyAsync()
    {
        var context = CreateOrchestrator([CreateEnvelope(sequenceNumber: 1)]);
        context.Index
            .HandleSerializedEventAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("index unavailable"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => context.Sut.DeliverProjectionAsync(new AggregateIdentity("tenant-a", "party", "party-1"), CancellationToken.None));

        await context.Adapter
            .DidNotReceive()
            .TrySaveDeliveredSequenceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    private static (
        PartyProjectionUpdateOrchestrator Sut,
        IPartyProjectionPlatformAdapter Adapter,
        IAggregateActor Aggregate,
        IPartyDetailProjectionActor Detail,
        IPartyIndexProjectionActor Index) CreateOrchestrator(EventEnvelope[] events)
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        IAggregateActor aggregate = Substitute.For<IAggregateActor>();
        IPartyDetailProjectionActor detail = Substitute.For<IPartyDetailProjectionActor>();
        IPartyIndexProjectionActor index = Substitute.For<IPartyIndexProjectionActor>();
        aggregate.GetEventsAsync(0).Returns(events);
        proxyFactory
            .CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), nameof(AggregateActor))
            .Returns(aggregate);
        proxyFactory
            .CreateActorProxy<IPartyDetailProjectionActor>(Arg.Any<ActorId>(), nameof(PartyDetailProjectionActor))
            .Returns(detail);
        proxyFactory
            .CreateActorProxy<IPartyIndexProjectionActor>(Arg.Any<ActorId>(), nameof(PartyIndexProjectionActor))
            .Returns(index);

        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        protection
            .UnprotectEventPayloadAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(new PayloadProtectionResult(
                (byte[])callInfo[2],
                (string)callInfo[3])));

        IPartyProjectionPlatformAdapter adapter = Substitute.For<IPartyProjectionPlatformAdapter>();
        adapter.ReadDeliveredSequenceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0L);
        adapter
            .TrySaveDeliveredSequenceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = new PartyProjectionUpdateOrchestrator(
            proxyFactory,
            protection,
            adapter,
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<PartyProjectionUpdateOrchestrator>>());
        return (sut, adapter, aggregate, detail, index);
    }

    private static EventEnvelope CreateEnvelope(long sequenceNumber)
        => new(
            MessageId: $"message-{sequenceNumber}",
            AggregateId: "party-1",
            AggregateType: "Party",
            TenantId: "tenant-a",
            Domain: "party",
            SequenceNumber: sequenceNumber,
            GlobalPosition: sequenceNumber,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "correlation",
            CausationId: "causation",
            UserId: "system",
            DomainServiceVersion: "test",
            EventTypeName: "PartyCreated",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: [],
            Extensions: null);

    private static ServiceProvider CreatePartiesServiceProvider(Dictionary<string, string?> projectionOverrides)
    {
        Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Authentication:JwtBearer:Issuer"] = "hexalith-test",
            ["Authentication:JwtBearer:Audience"] = "hexalith-parties",
            ["Authentication:JwtBearer:SigningKey"] = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["Tenants:PubSubName"] = "pubsub",
            ["Tenants:TopicName"] = "system.tenants.events",
        };

        foreach (KeyValuePair<string, string?> item in projectionOverrides)
        {
            values[item.Key] = item.Value;
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddParties(configuration);
        services.AddSingleton(Substitute.For<IProjectionCheckpointTracker>());
        services.AddSingleton(Substitute.For<IProjectionRebuildCheckpointStore>());
        return services.BuildServiceProvider();
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (string? Content, HttpStatusCode StatusCode)> _responses = new(StringComparer.OrdinalIgnoreCase);

        public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = [];

        public void AddResponse(string url, string? content, HttpStatusCode statusCode = HttpStatusCode.OK, HttpMethod? method = null)
        {
            _responses[$"{(method ?? HttpMethod.Get).Method}:{url}"] = (content, statusCode);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.PathAndQuery ?? string.Empty;
            string? body = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            Requests.Add((request.Method, path, body));

            if (_responses.TryGetValue($"{request.Method.Method}:{path}", out (string? Content, HttpStatusCode StatusCode) response))
            {
                HttpResponseMessage httpResponse = new(response.StatusCode);
                if (response.Content is not null)
                {
                    httpResponse.Content = new StringContent(response.Content, System.Text.Encoding.UTF8, "application/json");
                }

                return Task.FromResult(httpResponse);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
