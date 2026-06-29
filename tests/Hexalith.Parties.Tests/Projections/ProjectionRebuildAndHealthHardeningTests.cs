using System.Net;
using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.HealthChecks;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Projections.Strategies;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

public sealed class ProjectionRebuildAndHealthHardeningTests
{
    [Fact]
    public async Task ProjectionActorsHealthCheck_HealthyActors_DescriptionContainsOnlyBoundedCategoriesAsync()
    {
        ProjectionActorsHealthCheck check = CreateHealthCheck(healthy: true);

        HealthCheckResult result = await check.CheckHealthAsync(CreateHealthCheckContext(), CancellationToken.None);

        result.Status.ShouldBe(HealthStatus.Healthy);
        string description = result.Description ?? string.Empty;
        description.ShouldBe("Projection actors are responsive.");
        AssertNoSensitiveProjectionDiagnostics(description);
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task ProjectionActorsHealthCheck_RoutingFailure_ReportsDegradedWithoutTenantOrKeyLeakageAsync()
    {
        ProjectionActorsHealthCheck check = CreateHealthCheck(healthy: false);

        HealthCheckResult result = await check.CheckHealthAsync(CreateHealthCheckContext(), CancellationToken.None);

        result.Status.ShouldBe(HealthStatus.Degraded);
        string description = result.Description ?? string.Empty;
        description.ShouldBe("Projection actor health check failed.");
        AssertNoSensitiveProjectionDiagnostics(description);
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public void RebuildDetailProjection_RejectionEventsInStream_DoNotMutateSuccessfulState()
    {
        PartyDetail? detail = null;
        detail = PartyDetailProjectionHandler.Apply(
            "party-001",
            new PartyCreated
            {
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            },
            detail);

        PartyDetail? rejectionResult = PartyDetailProjectionHandler.Apply(
            "party-001",
            new PartyCannotAddDuplicateChannel(),
            detail);
        PartyDetail? afterRejection = rejectionResult ?? detail;
        PartyDetail? afterUpdate = PartyDetailProjectionHandler.Apply(
            "party-001",
            new PersonDetailsUpdated { PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Byron" } },
            afterRejection);

        rejectionResult.ShouldBeNull();
        afterRejection.ShouldBeSameAs(detail);
        afterUpdate.ShouldNotBeNull();
        afterUpdate.PersonDetails.ShouldNotBeNull();
        afterUpdate.PersonDetails.LastName.ShouldBe("Byron");
    }

    [Fact]
    public async Task RebuildDetailProjection_StateStoreWriteFailure_FailsClosedAsync()
    {
        MockHttpMessageHandler handler = new();
        AddDetailRebuildResponses(handler, "party-write-failure");
        handler.AddResponse(
            BuildActorStateRootUrl("PartyDetailProjectionActor", "test-tenant:party-detail:party-write-failure"),
            null,
            HttpStatusCode.InternalServerError,
            HttpMethod.Put);

        ProjectionRebuildService sut = CreateService(handler);

        await Should.ThrowAsync<HttpRequestException>(
            () => sut.RebuildDetailProjectionAsync("test-tenant", "party-write-failure", CancellationToken.None));

        handler.Requests.ShouldContain(request => request.Method == HttpMethod.Put
            && request.Path == BuildActorStateRootUrl("PartyDetailProjectionActor", "test-tenant:party-detail:party-write-failure"));
        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Put
            && request.Path == BuildActorStateRootUrl("PartyIndexProjectionActor", "test-tenant:party-index"));
    }

    [Fact]
    public async Task RebuildDetailProjection_CancellationMidFlight_StopsBeforeStateWriteAsync()
    {
        using var cts = new CancellationTokenSource();
        CancelingHttpMessageHandler handler = new(cts);
        AddDetailRebuildResponses(handler, "party-cancel");

        ProjectionRebuildService sut = CreateService(handler);

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.RebuildDetailProjectionAsync("test-tenant", "party-cancel", cts.Token));

        handler.Requests.ShouldContain(request => request.Method == HttpMethod.Get
            && request.Path == BuildActorStateUrl(
                "AggregateActor",
                "test-tenant:party:party-cancel",
                "test-tenant:party:party-cancel:events:1"));
        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task RebuildDetailProjection_CheckpointDeleteFailure_LeavesProjectionDegradedAsync()
    {
        MockHttpMessageHandler handler = new();
        AddDetailRebuildResponses(handler, "party-delete-failure");
        handler.AddResponse(
            BuildActorStateRootUrl("PartyDetailProjectionActor", "test-tenant:party-detail:party-delete-failure"),
            null,
            HttpStatusCode.NoContent,
            HttpMethod.Put);
        handler.AddResponse(
            BuildActorStateRootUrl("PartyIndexProjectionActor", "test-tenant:party-index"),
            null,
            HttpStatusCode.NoContent,
            HttpMethod.Put);
        handler.AddResponse(
            BuildActorStateRootUrl("PartyIndexProjectionActor", "test-tenant:party-index"),
            null,
            HttpStatusCode.InternalServerError,
            HttpMethod.Put);

        ProjectionRebuildService sut = CreateService(handler);

        await Should.ThrowAsync<HttpRequestException>(
            () => sut.RebuildDetailProjectionAsync("test-tenant", "party-delete-failure", CancellationToken.None));

        handler.Requests.Count(request => request.Method == HttpMethod.Put
            && request.Path == BuildActorStateRootUrl("PartyIndexProjectionActor", "test-tenant:party-index"))
            .ShouldBe(2);
        handler.Requests.Last(request => request.Method == HttpMethod.Put).Body.ShouldNotBeNull().ShouldContain("\"delete\"");
    }

    [Fact]
    public async Task RebuildingProjection_CrossTenantProbe_NoCacheRebuildOrPositionLeakageAsync()
    {
        (PartyIndexProjectionActor tenantAActor, IActorStateManager tenantAStateManager, _) =
            CreateIndexActor("tenant-a:party-index", new ProjectionOptions { BatchSize = 1 });
        SetupEmptyIndexState(tenantAStateManager);
        await tenantAActor.HandleEventAsync(
            "party-shared",
            new PartyCreated
            {
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            });

        (PartyIndexProjectionActor tenantBActor, _, _) = CreateIndexActor("tenant-b:party-index");
        tenantBActor.SetRebuilding(true);

        PartyIndexProjectionReadResult result = await tenantBActor.GetEntriesReadAsync();

        result.Entries.ShouldBeEmpty();
        result.Freshness.Status.ShouldBe(ProjectionFreshnessStatus.Unavailable);
        string serialized = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        serialized.ShouldNotContain("Ada", Case.Insensitive);
        serialized.ShouldNotContain("tenant-a", Case.Insensitive);
        AssertNoSensitiveProjectionDiagnostics(serialized);
    }

    [Fact]
    public async Task DetailRead_UsesProjectionPlatformFreshnessMapperAsync()
    {
        IPartyProjectionPlatformAdapter adapter = Substitute.For<IPartyProjectionPlatformAdapter>();
        adapter
            .MapFreshness(PartyProjectionPlatformFreshness.Current, false, false, false)
            .Returns(ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current));
        (PartyDetailProjectionActor actor, IActorStateManager stateManager, _) =
            CreateDetailActor(projectionPlatformAdapter: adapter);
        stateManager.TryGetStateAsync<PartyDetail>(
                "test-tenant:party-detail:party-001",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(true, CreateDetail("party-001")));

        PartyDetailProjectionReadResult result = await actor.GetDetailReadAsync();

        result.Freshness.Status.ShouldBe(ProjectionFreshnessStatus.Current);
        adapter.Received(1).MapFreshness(PartyProjectionPlatformFreshness.Current, false, false, false);
    }

    [Fact]
    public async Task IndexRead_UsesProjectionPlatformFreshnessMapperAsync()
    {
        IPartyProjectionPlatformAdapter adapter = Substitute.For<IPartyProjectionPlatformAdapter>();
        adapter
            .MapFreshness(PartyProjectionPlatformFreshness.Current, false, false, false)
            .Returns(ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current));
        (PartyIndexProjectionActor actor, IActorStateManager stateManager, _) =
            CreateIndexActor("test-tenant:party-index", projectionPlatformAdapter: adapter);
        SetupEmptyIndexState(stateManager);

        PartyIndexProjectionReadResult result = await actor.GetEntriesReadAsync();

        result.Freshness.Status.ShouldBe(ProjectionFreshnessStatus.Current);
        adapter.Received(1).MapFreshness(PartyProjectionPlatformFreshness.Current, false, false, false);
    }

    [Fact]
    public async Task RebuildDetailProjection_DuplicateReminder_KeepsRebuildingStateAuthoritativeAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager, IProjectionRebuildService rebuildService) =
            CreateDetailActor();
        actor.SetRebuilding(true);

        int activeRebuilds = 0;
        int observedMaxConcurrency = 0;
        rebuildService
            .RebuildDetailProjectionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                int active = Interlocked.Increment(ref activeRebuilds);
                observedMaxConcurrency = Math.Max(observedMaxConcurrency, active);
                await Task.Yield();
                Interlocked.Decrement(ref activeRebuilds);
            });
        stateManager.TryGetStateAsync<PartyDetail>(
                "test-tenant:party-detail:party-001",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(true, CreateDetail("party-001")));

        // Duplicate reminder dispatches: the actor's turn-based contract must serialize them,
        // so RebuildDetailProjectionAsync should run twice but never concurrently.
        Task first = actor.ReceiveReminderAsync("auto-rebuild", [], TimeSpan.Zero, TimeSpan.Zero);
        Task second = actor.ReceiveReminderAsync("auto-rebuild", [], TimeSpan.Zero, TimeSpan.Zero);
        await Task.WhenAll(first, second);

        await rebuildService.Received(2).RebuildDetailProjectionAsync(
            "test-tenant",
            "party-001",
            Arg.Any<CancellationToken>());
        observedMaxConcurrency.ShouldBe(1);
        (await actor.IsRebuildingAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task RebuildDetailProjection_SuccessfulCompletion_ClearsRebuildingStateAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager, IProjectionRebuildService rebuildService) =
            CreateDetailActor();
        actor.SetRebuilding(true);
        stateManager.TryGetStateAsync<PartyDetail>(
                "test-tenant:party-detail:party-001",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(true, CreateDetail("party-001")));

        await actor.ReceiveReminderAsync("auto-rebuild", [], TimeSpan.Zero, TimeSpan.Zero);

        await rebuildService.Received(1).RebuildDetailProjectionAsync("test-tenant", "party-001", Arg.Any<CancellationToken>());
        (await actor.IsRebuildingAsync()).ShouldBeFalse();
        (await actor.GetDetailAsync()).ShouldNotBeNull();
    }

    private static ProjectionActorsHealthCheck CreateHealthCheck(bool healthy)
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        IPartyDetailProjectionActor detailActor = Substitute.For<IPartyDetailProjectionActor>();

        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>())
            .Returns(detailActor);

        if (healthy)
        {
            indexActor.PingAsync().Returns(Task.FromResult(true));
            indexActor.IsRebuildingAsync().Returns(Task.FromResult(false));
            detailActor.PingAsync().Returns(Task.FromResult(true));
            detailActor.IsRebuildingAsync().Returns(Task.FromResult(false));
        }
        else
        {
            indexActor.PingAsync().ThrowsAsync(new HttpRequestException("tenant-a:party-index state key stream position leaked"));
        }

        return new ProjectionActorsHealthCheck(actorProxyFactory, NullLogger<ProjectionActorsHealthCheck>.Instance);
    }

    private static HealthCheckContext CreateHealthCheckContext()
        => new()
        {
            Registration = new HealthCheckRegistration(
                "projection-actors",
                Substitute.For<IHealthCheck>(),
                HealthStatus.Degraded,
                tags: null),
        };

    private static void AddDetailRebuildResponses(MockHttpMessageHandler handler, string partyId)
    {
        var metadata = new { currentSequence = 1L, lastModified = DateTimeOffset.UtcNow };
        PartyCreated evt = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        };

        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", $"test-tenant:rebuild-checkpoint:detail:{partyId}"),
            null,
            HttpStatusCode.NotFound);
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", $"test-tenant:party:{partyId}", $"test-tenant:party:{partyId}:metadata"),
            JsonSerializer.Serialize(metadata));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", $"test-tenant:party:{partyId}", $"test-tenant:party:{partyId}:events:1"),
            JsonSerializer.Serialize(CreateEnvelope(partyId, 1, evt)));
    }

    private static object CreateEnvelope(
        string aggregateId,
        long seq,
        IEventPayload payload,
        string serializationFormat = "json")
    {
        byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(
            payload,
            payload.GetType(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return new
        {
            aggregateId,
            tenantId = "test-tenant",
            domain = "party",
            sequenceNumber = seq,
            eventTypeName = payload.GetType().FullName,
            serializationFormat,
            payload = payloadBytes,
        };
    }

    private static string BuildActorStateUrl(string actorType, string actorId, string stateKey)
        => $"/v1.0/actors/{actorType}/{Uri.EscapeDataString(actorId)}/state/{Uri.EscapeDataString(stateKey)}";

    private static string BuildActorStateRootUrl(string actorType, string actorId)
        => $"/v1.0/actors/{actorType}/{Uri.EscapeDataString(actorId)}/state";

    private static ProjectionRebuildService CreateService(MockHttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:3500") };
        var adapter = new LocalPartyProjectionPlatformAdapter(httpClient);
        return new ProjectionRebuildService(
            httpClient,
            new NoOpPayloadProtectionService(),
            adapter,
            Substitute.For<ILogger<ProjectionRebuildService>>());
    }

    private static (PartyDetailProjectionActor Actor, IActorStateManager StateManager, IProjectionRebuildService RebuildService)
        CreateDetailActor(
            string actorId = "test-tenant:party-detail:party-001",
            IPartyProjectionPlatformAdapter? projectionPlatformAdapter = null)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IProjectionRebuildService rebuildService = Substitute.For<IProjectionRebuildService>();
        ILogger<PartyDetailProjectionActor> logger = Substitute.For<ILogger<PartyDetailProjectionActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyDetailProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        var actor = new PartyDetailProjectionActor(host, rebuildService, logger, projectionPlatformAdapter);
        InjectStateManager(actor, stateManager);
        return (actor, stateManager, rebuildService);
    }

    private static (PartyIndexProjectionActor Actor, IActorStateManager StateManager, IProjectionRebuildService RebuildService)
        CreateIndexActor(
            string actorId,
            ProjectionOptions? options = null,
            IPartyProjectionPlatformAdapter? projectionPlatformAdapter = null)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IProjectionRebuildService rebuildService = Substitute.For<IProjectionRebuildService>();
        ILogger<PartyIndexProjectionActor> logger = Substitute.For<ILogger<PartyIndexProjectionActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyIndexProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        var actor = new PartyIndexProjectionActor(
            host,
            new SingleKeyPartitionStrategy(),
            Options.Create(options ?? new ProjectionOptions()),
            rebuildService,
            logger,
            projectionPlatformAdapter);
        InjectStateManager(actor, stateManager);
        return (actor, stateManager, rebuildService);
    }

    private static void SetupEmptyIndexState(IActorStateManager stateManager)
    {
        stateManager.TryGetStateAsync<Dictionary<string, PartyIndexEntry>>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<Dictionary<string, PartyIndexEntry>>(true, []));
    }

    private static PartyDetail CreateDetail(string partyId)
        => new()
        {
            Id = partyId,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };

    private static void InjectStateManager(Actor actor, IActorStateManager stateManager)
    {
        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);
    }

    private static void AssertNoSensitiveProjectionDiagnostics(string text)
    {
        text.ShouldNotContain("tenant-", Case.Insensitive);
        text.ShouldNotContain("party-detail:", Case.Insensitive);
        text.ShouldNotContain("party-index:", Case.Insensitive);
        text.ShouldNotContain("state key", Case.Insensitive);
        text.ShouldNotContain("stateKey", Case.Insensitive);
        text.ShouldNotContain("stream", Case.Insensitive);
        text.ShouldNotContain("sequence", Case.Insensitive);
        text.ShouldNotContain("localhost", Case.Insensitive);
        text.ShouldNotContain("http://", Case.Insensitive);
        text.ShouldNotContain("3500", Case.Sensitive);
    }

    private sealed class NoOpPayloadProtectionService : IEventPayloadProtectionService
    {
        public Task<PayloadProtectionResult> ProtectEventPayloadAsync(
            AggregateIdentity identity,
            IEventPayload eventPayload,
            string eventTypeName,
            byte[] payloadBytes,
            string serializationFormat,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat));

        public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
            AggregateIdentity identity,
            string eventTypeName,
            byte[] payloadBytes,
            string serializationFormat,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat));

        public Task<object> ProtectSnapshotStateAsync(
            AggregateIdentity identity,
            object state,
            CancellationToken cancellationToken = default)
            => Task.FromResult(state);

        public Task<object> UnprotectSnapshotStateAsync(
            AggregateIdentity identity,
            object state,
            CancellationToken cancellationToken = default)
            => Task.FromResult(state);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Queue<(string? Content, HttpStatusCode StatusCode)>> _responses = new(StringComparer.OrdinalIgnoreCase);

        public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = [];

        public void AddResponse(string url, string? content, HttpStatusCode statusCode = HttpStatusCode.OK, HttpMethod? method = null)
        {
            string key = $"{(method ?? HttpMethod.Get).Method}:{url}";
            if (!_responses.TryGetValue(key, out Queue<(string? Content, HttpStatusCode StatusCode)>? responses))
            {
                responses = [];
                _responses[key] = responses;
            }

            responses.Enqueue((content, statusCode));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.PathAndQuery ?? string.Empty;
            string? body = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            Requests.Add((request.Method, path, body));

            if (_responses.TryGetValue($"{request.Method.Method}:{path}", out Queue<(string? Content, HttpStatusCode StatusCode)>? responses)
                && responses.TryDequeue(out (string? Content, HttpStatusCode StatusCode) response))
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

    private sealed class CancelingHttpMessageHandler(CancellationTokenSource cancellationTokenSource) : MockHttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Task<HttpResponseMessage> response = base.SendAsync(request, cancellationToken);
            string path = Uri.UnescapeDataString(request.RequestUri?.PathAndQuery ?? string.Empty);
            if (request.Method == HttpMethod.Get && path.Contains(":events:1", StringComparison.OrdinalIgnoreCase))
            {
                cancellationTokenSource.Cancel();
            }

            return response;
        }
    }
}
