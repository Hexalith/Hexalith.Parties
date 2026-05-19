using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Queries;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

public sealed class PartyIndexProjectionQueryActorTests
{
    [Fact]
    public async Task QueryAsync_PartyIndex_ReadsTenantScopedIndexAndFiltersBeforePagingAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-alpha"] = Entry("p-alpha", "Alpha Person", PartyType.Person, active: true, "2026-05-03T00:00:00Z", "2026-05-05T00:00:00Z"),
                ["p-beta"] = Entry("p-beta", "Beta Person", PartyType.Person, active: true, "2026-05-04T00:00:00Z", "2026-05-06T00:00:00Z"),
                ["p-inactive"] = Entry("p-inactive", "Inactive Person", PartyType.Person, active: false, "2026-05-04T00:00:00Z", "2026-05-06T00:00:00Z"),
                ["p-org"] = Entry("p-org", "Org", PartyType.Organization, active: true, "2026-05-04T00:00:00Z", "2026-05-06T00:00:00Z"),
                ["p-outside"] = Entry("p-outside", "Outside Person", PartyType.Person, active: true, "2026-04-30T00:00:00Z", "2026-05-06T00:00:00Z"),
                ["p-erased"] = Entry("p-erased", "Erased Person", PartyType.Person, active: true, "2026-05-04T00:00:00Z", "2026-05-06T00:00:00Z") with { IsErased = true },
            }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new
            {
                page = 1,
                pageSize = 1,
                type = "Person",
                active = true,
                createdAfter = "2026-05-01T00:00:00.0000000+00:00",
                createdBefore = "2026-05-31T00:00:00.0000000+00:00",
                modifiedAfter = "2026-05-01T00:00:00.0000000+00:00",
                modifiedBefore = "2026-05-31T00:00:00.0000000+00:00",
            })));

        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe(PartyIndexProjectionQueryActor.ProjectionType);
        PagedResult<PartyIndexEntry> page = DeserializePage(result);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(1);
        page.TotalCount.ShouldBe(2);
        page.TotalPages.ShouldBe(2);
        page.Items.Select(static i => i.Id).ShouldBe(["p-alpha"]);

        actorProxyFactory.Received(1).CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId() == "tenant-a:party-index"),
            nameof(PartyIndexProjectionActor),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_ActiveFalse_ReturnsInactiveEntriesWithoutHidingThemAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-active"] = Entry("p-active", "Active Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
                ["p-inactive"] = Entry("p-inactive", "Inactive Person", PartyType.Person, active: false, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
            }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { page = 1, pageSize = 10, active = false })));

        result.Success.ShouldBeTrue();
        PagedResult<PartyIndexEntry> page = DeserializePage(result);
        page.Items.Select(static i => i.Id).ShouldBe(["p-inactive"]);
        page.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task QueryAsync_InvalidDateRange_FailsBeforeProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new
            {
                page = 1,
                pageSize = 20,
                createdAfter = "2026-05-31T00:00:00.0000000+00:00",
                createdBefore = "2026-05-01T00:00:00.0000000+00:00",
            })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_InvalidPartyType_FailsBeforeProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { page = 1, pageSize = 20, type = "999" })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_CrossTenantActorRoute_FailsBeforeProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-b",
            payload: Payload(new { page = 1, pageSize = 20 })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_PayloadTenantCannotInfluenceIndexActorKeyAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-tenant-b"] = Entry("p-tenant-b", "Tenant B Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
            }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-b:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-b",
            payload: Payload(new { page = 1, pageSize = 20, tenantId = "tenant-a", partitionKey = "tenant-a:party-index" })));

        result.Success.ShouldBeTrue();
        DeserializePage(result).Items.Select(static i => i.Id).ShouldBe(["p-tenant-b"]);
        actorProxyFactory.Received(1).CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId() == "tenant-b:party-index"),
            nameof(PartyIndexProjectionActor),
            Arg.Any<ActorProxyOptions?>());
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId().Contains("tenant-a", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_OperationCanceledExceptionFromProjectionRead_PropagatesCancellationAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Throws(new OperationCanceledException());
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        await Should.ThrowAsync<OperationCanceledException>(
            () => actor.QueryAsync(CreateEnvelope("tenant-a", Payload(new { page = 1, pageSize = 20 }))));
    }

    [Fact]
    public async Task QueryAsync_LogMessages_ContainOnlyBoundedMetadataOnReadFailureAsync()
    {
        var recordingLogger = new RecordingLogger<PartyIndexProjectionQueryActor>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Throws(new InvalidOperationException("state store failed"));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActorWithLogger(
            "party-index:tenant-a:parties",
            actorProxyFactory,
            recordingLogger);

        QueryResult result = await actor.QueryAsync(CreateEnvelope("tenant-a", Payload(new { page = 1, pageSize = 20 })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
        recordingLogger.Records.ShouldNotBeEmpty();
        foreach (string message in recordingLogger.Records.Select(static r => r.Message))
        {
            message.ShouldNotContain("Ada", Case.Insensitive);
            message.ShouldNotContain("ada@example.test", Case.Insensitive);
            message.ShouldNotContain("displayName", Case.Insensitive);
            message.ShouldNotContain("contactChannels", Case.Insensitive);
            message.ShouldNotContain("tenant-a:party-index:all", Case.Insensitive);
        }
    }

    private static PartyIndexProjectionQueryActor CreateActor(string actorId, IActorProxyFactory actorProxyFactory)
        => CreateActorWithLogger(actorId, actorProxyFactory, NullLogger<PartyIndexProjectionQueryActor>.Instance);

    private static PartyIndexProjectionQueryActor CreateActorWithLogger(
        string actorId,
        IActorProxyFactory actorProxyFactory,
        ILogger<PartyIndexProjectionQueryActor> logger)
    {
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyIndexProjectionQueryActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        return new PartyIndexProjectionQueryActor(host, actorProxyFactory, logger);
    }

    private static QueryEnvelope CreateEnvelope(string tenant, byte[] payload)
        => new(
            tenantId: tenant,
            domain: PartyIndexProjectionQueryActor.PartyDomain,
            aggregateId: PartyIndexProjectionQueryActor.ListAggregateId,
            queryType: PartyIndexProjectionQueryActor.PartyIndexQueryType,
            payload: payload,
            correlationId: "corr-list",
            userId: "user-1",
            entityId: PartyIndexProjectionQueryActor.ListAggregateId);

    private static PagedResult<PartyIndexEntry> DeserializePage(QueryResult result)
        => result.GetPayload().Deserialize<PagedResult<PartyIndexEntry>>(JsonOptions)
            ?? throw new InvalidOperationException("Expected paged PartyIndex payload.");

    private static byte[] Payload<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);

    private static PartyIndexEntry Entry(
        string id,
        string displayName,
        PartyType type,
        bool active,
        string createdAt,
        string modifiedAt)
        => new()
        {
            Id = id,
            Type = type,
            IsActive = active,
            DisplayName = displayName,
            SortName = displayName,
            CreatedAt = DateTimeOffset.Parse(createdAt, System.Globalization.CultureInfo.InvariantCulture),
            LastModifiedAt = DateTimeOffset.Parse(modifiedAt, System.Globalization.CultureInfo.InvariantCulture),
        };

    private static JsonSerializerOptions JsonOptions => new(JsonSerializerDefaults.Web);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Records.Add((logLevel, formatter(state, exception)));
        }
    }
}
