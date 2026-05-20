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

public sealed class PartyDetailProjectionQueryActorTests
{
    [Fact]
    public async Task QueryAsync_PartyDetail_ReadsTenantScopedDetailProjectionAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyDetailProjectionActor detailActor = Substitute.For<IPartyDetailProjectionActor>();
        detailActor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(CreateDetail("p-1")));
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(detailActor);
        PartyDetailProjectionQueryActor actor = CreateActor("party-detail:tenant-a:p-1", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope("tenant-a", "p-1", "PartyDetail"));

        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe("party-detail");
        result.GetPayload().GetProperty("id").GetString().ShouldBe("p-1");
        result.GetPayload().GetProperty("displayName").GetString().ShouldBe("Ada Lovelace");
        actorProxyFactory.Received(1).CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId() == "tenant-a:party-detail:p-1"),
            nameof(PartyDetailProjectionActor),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_GetPartyAlias_UsesSameTenantScopedProjectionActorAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyDetailProjectionActor detailActor = Substitute.For<IPartyDetailProjectionActor>();
        detailActor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(CreateDetail("p-2")));
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(detailActor);
        PartyDetailProjectionQueryActor actor = CreateActor("party-detail:tenant-a:p-2", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope("tenant-a", "p-2", "GetParty"));

        result.Success.ShouldBeTrue();
        actorProxyFactory.Received(1).CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId() == "tenant-a:party-detail:p-2"),
            nameof(PartyDetailProjectionActor),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_CrossTenantEnvelopeMismatch_FailsBeforeProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyDetailProjectionQueryActor actor = CreateActor("party-detail:tenant-a:p-1", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope("tenant-b", "p-1", "PartyDetail"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_CrossTenantPartyId_DerivesOnlyCallerTenantActorKeyAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyDetailProjectionActor detailActor = Substitute.For<IPartyDetailProjectionActor>();
        detailActor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(detailActor);
        PartyDetailProjectionQueryActor actor = CreateActor("party-detail:tenant-b:p-tenant-a", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope("tenant-b", "p-tenant-a", "PartyDetail"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
        actorProxyFactory.Received(1).CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId() == "tenant-b:party-detail:p-tenant-a"),
            nameof(PartyDetailProjectionActor),
            Arg.Any<ActorProxyOptions?>());
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId().Contains("tenant-a:party-detail", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_InactiveParty_ReturnsInspectableInactiveDetailAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyDetailProjectionActor detailActor = Substitute.For<IPartyDetailProjectionActor>();
        detailActor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(CreateDetail("p-inactive") with { IsActive = false }));
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(detailActor);
        PartyDetailProjectionQueryActor actor = CreateActor("party-detail:tenant-a:p-inactive", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope("tenant-a", "p-inactive", "PartyDetail"));

        result.Success.ShouldBeTrue();
        result.GetPayload().GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task QueryAsync_ErasedParty_ReturnsOnlyRedactedProjectionStateAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyDetailProjectionActor detailActor = Substitute.For<IPartyDetailProjectionActor>();
        PartyDetail erased = CreateDetail("p-erased") with
        {
            DisplayName = string.Empty,
            SortName = string.Empty,
            PersonDetails = null,
            ContactChannels = [],
            Identifiers = [],
            NameHistory = [],
            IsErased = true,
            ErasedAt = DateTimeOffset.UtcNow,
        };
        detailActor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(erased));
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(detailActor);
        PartyDetailProjectionQueryActor actor = CreateActor("party-detail:tenant-a:p-erased", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope("tenant-a", "p-erased", "PartyDetail"));

        result.Success.ShouldBeTrue();
        JsonElement payload = result.GetPayload();
        payload.GetProperty("isErased").GetBoolean().ShouldBeTrue();

        // Enumerate every PII-bearing field on PartyDetail and assert each is empty/absent on the erased
        // projection. Substring checks against synthetic test names are insufficient because they would
        // silently miss new PII surfaces (phones, addresses, identifier values, name-history entries).
        payload.GetProperty("displayName").GetString().ShouldBeNullOrEmpty();
        payload.GetProperty("sortName").GetString().ShouldBeNullOrEmpty();
        payload.GetProperty("personDetails").ValueKind.ShouldBe(JsonValueKind.Null);
        payload.GetProperty("contactChannels").GetArrayLength().ShouldBe(0);
        payload.GetProperty("identifiers").GetArrayLength().ShouldBe(0);
        payload.GetProperty("nameHistory").GetArrayLength().ShouldBe(0);

        // Defense in depth: scan raw text for the synthetic seed values to catch any future PII field
        // we forgot to enumerate above.
        string rawText = payload.GetRawText();
        rawText.ShouldNotContain("Ada", Case.Insensitive);
        rawText.ShouldNotContain("Lovelace", Case.Insensitive);
        rawText.ShouldNotContain("ada@example.test", Case.Insensitive);
    }

    [Fact]
    public async Task QueryAsync_UnsupportedQueryType_FailsWithoutProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyDetailProjectionQueryActor actor = CreateActor("party-detail:tenant-a:p-1", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope("tenant-a", "p-1", "PartySearch"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.UnsupportedQueryType);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_OperationCanceledExceptionFromProxy_PropagatesCancellationAsync()
    {
        // Required Test Matrix row "Cancellation during actor read": the adapter must let OperationCanceledException
        // bubble up so the EventStore query router (and HTTP layer) can honor terminal cancellation, rather than
        // masking the cancel signal as a generic ActorException failure.
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyDetailProjectionActor detailActor = Substitute.For<IPartyDetailProjectionActor>();
        detailActor.GetDetailAsync().Throws(new OperationCanceledException());
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(detailActor);
        PartyDetailProjectionQueryActor actor = CreateActor("party-detail:tenant-a:p-cancel", actorProxyFactory);

        await Should.ThrowAsync<OperationCanceledException>(
            () => actor.QueryAsync(CreateEnvelope("tenant-a", "p-cancel", "PartyDetail")));
    }

    [Fact]
    public async Task QueryAsync_LogMessages_ContainOnlyBoundedMetadataAcrossAllOutcomesAsync()
    {
        // Task 6 requires privacy-safe assertions for response/error/log text touched by this story.
        // Exercise routing-success, projection-not-found, and read-failed log paths from a single actor
        // and assert no record contains synthetic PII seed values, raw payload fragments, or storage internals.
        var recordingLogger = new RecordingLogger<PartyDetailProjectionQueryActor>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();

        // 1) Success path — exercises PartyDetailQueryRouting (Debug)
        IPartyDetailProjectionActor successProxy = Substitute.For<IPartyDetailProjectionActor>();
        successProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(CreateDetail("p-log-success")));
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Is<ActorId>(id => id.GetId() == "tenant-a:party-detail:p-log-success"),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(successProxy);

        // 2) Not-found path — exercises PartyDetailProjectionNotFound (Warning)
        IPartyDetailProjectionActor notFoundProxy = Substitute.For<IPartyDetailProjectionActor>();
        notFoundProxy.GetDetailAsync().Throws(new InvalidOperationException("did not find address for actor"));
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Is<ActorId>(id => id.GetId() == "tenant-a:party-detail:p-log-missing"),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(notFoundProxy);

        // 3) Read-failed path — exercises PartyDetailProjectionReadFailed (Warning, with exception)
        IPartyDetailProjectionActor failedProxy = Substitute.For<IPartyDetailProjectionActor>();
        failedProxy.GetDetailAsync().Throws(new InvalidOperationException("transient infrastructure failure"));
        actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Is<ActorId>(id => id.GetId() == "tenant-a:party-detail:p-log-fail"),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(failedProxy);

        PartyDetailProjectionQueryActor actorSuccess = CreateActorWithLogger("party-detail:tenant-a:p-log-success", actorProxyFactory, recordingLogger);
        await actorSuccess.QueryAsync(CreateEnvelope("tenant-a", "p-log-success", "PartyDetail"));

        PartyDetailProjectionQueryActor actorMissing = CreateActorWithLogger("party-detail:tenant-a:p-log-missing", actorProxyFactory, recordingLogger);
        await actorMissing.QueryAsync(CreateEnvelope("tenant-a", "p-log-missing", "PartyDetail"));

        PartyDetailProjectionQueryActor actorFailed = CreateActorWithLogger("party-detail:tenant-a:p-log-fail", actorProxyFactory, recordingLogger);
        await actorFailed.QueryAsync(CreateEnvelope("tenant-a", "p-log-fail", "PartyDetail"));

        recordingLogger.Records.Count.ShouldBeGreaterThan(0);
        foreach ((LogLevel _, string message, Exception? exception) in recordingLogger.Records)
        {
            // No PII seed values from CreateDetail
            message.ShouldNotContain("Ada", Case.Insensitive);
            message.ShouldNotContain("Lovelace", Case.Insensitive);
            message.ShouldNotContain("ada@example.test", Case.Insensitive);

            // No raw projection payload fragments, JSON envelopes, or stack-trace excerpts
            message.ShouldNotContain("displayName", Case.Insensitive);
            message.ShouldNotContain("personDetails", Case.Insensitive);
            message.ShouldNotContain("contactChannels", Case.Insensitive);
            message.ShouldNotContain("\"id\":", Case.Sensitive);
            message.ShouldNotContain("tenant-a:party-detail", Case.Insensitive);

            // Exception arg (if any) is allowed for diagnostics, but its message must not leak our synthetic PII
            if (exception is not null)
            {
                exception.Message.ShouldNotContain("Ada", Case.Insensitive);
                exception.Message.ShouldNotContain("ada@example.test", Case.Insensitive);
            }
        }
    }

    private static PartyDetailProjectionQueryActor CreateActor(string actorId, IActorProxyFactory actorProxyFactory)
        => CreateActorWithLogger(actorId, actorProxyFactory, NullLogger<PartyDetailProjectionQueryActor>.Instance);

    private static PartyDetailProjectionQueryActor CreateActorWithLogger(
        string actorId,
        IActorProxyFactory actorProxyFactory,
        ILogger<PartyDetailProjectionQueryActor> logger)
    {
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyDetailProjectionQueryActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        return new PartyDetailProjectionQueryActor(host, actorProxyFactory, logger);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Records { get; } = [];

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
            Records.Add((logLevel, formatter(state, exception), exception));
        }
    }

    private static QueryEnvelope CreateEnvelope(string tenant, string partyId, string queryType)
        => new(
            tenantId: tenant,
            domain: "party",
            aggregateId: partyId,
            queryType: queryType,
            payload: [],
            correlationId: $"corr-{partyId}",
            userId: "user-1",
            entityId: partyId);

    private static PartyDetail CreateDetail(string id)
        => new()
        {
            Id = id,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            PersonDetails = new PersonDetails
            {
                FirstName = "Ada",
                LastName = "Lovelace",
            },
            ContactChannels =
            [
                new ContactChannel
                {
                    Id = "email-1",
                    Type = ContactChannelType.Email,
                    Value = "ada@example.test",
                },
            ],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        };
}
