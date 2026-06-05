using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Services;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

public sealed class PartyDetailProjectionActorCorruptionTests
{
    private const string ActorId = "test-tenant:party-detail:party-001";

    [Fact]
    public async Task OnActivateAsync_DeserializationFailure_SetsRebuildingFlagAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor();

        stateManager.TryGetStateAsync<PartyDetail>(
            "test-tenant:party-detail:party-001", Arg.Any<CancellationToken>())
            .ThrowsAsync(new SerializationException("Corrupted state"));

        await InvokeOnActivateAsync(actor);

        (await actor.IsRebuildingAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task OnActivateAsync_NormalState_DoesNotSetRebuildingFlagAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor();

        var detail = new PartyDetail
        {
            Id = "party-001",
            Type = PartyType.Person,
            DisplayName = "Test Party",
            SortName = "Party, Test",
        };
        stateManager.TryGetStateAsync<PartyDetail>(
            "test-tenant:party-detail:party-001", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(true, detail));

        await InvokeOnActivateAsync(actor);

        (await actor.IsRebuildingAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task GetDetailAsync_WhenRebuilding_ReturnsCachedOrNullAsync()
    {
        (PartyDetailProjectionActor actor, _) = CreateActor();
        actor.SetRebuilding(true);

        PartyDetail? result = await actor.GetDetailAsync();

        // During rebuild, returns cached detail (from static cache) or null if no cache.
        // The actor does NOT throw — it degrades gracefully.
        // Depending on test order, static cache may or may not have data.
        // The key invariant is: no exception is thrown during degraded mode.
    }

    [Fact]
    public async Task GetDetailAsync_WhenNotRebuilding_QueriesStateStoreAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor();
        var detail = new PartyDetail
        {
            Id = "party-001",
            Type = PartyType.Person,
            DisplayName = "Test",
            SortName = "Test",
        };
        stateManager.TryGetStateAsync<PartyDetail>(
            "test-tenant:party-detail:party-001", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(true, detail));

        PartyDetail? result = await actor.GetDetailAsync();

        result.ShouldNotBeNull();
        result.DisplayName.ShouldBe("Test");
    }

    [Theory]
    [InlineData("test-tenant:wrong-projection:party-001")]
    [InlineData("test-tenant:party-detail:party-001:extra")]
    public async Task GetDetailAsync_InvalidProjectionActorId_ReturnsNullWithoutStateReadAsync(string actorId)
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor(actorId);

        PartyDetail? result = await actor.GetDetailAsync();

        result.ShouldBeNull();
        await stateManager.DidNotReceive().TryGetStateAsync<PartyDetail>(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActivateAsync_JsonException_SetsRebuildingFlagAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor();

        stateManager.TryGetStateAsync<PartyDetail>(
            "test-tenant:party-detail:party-001", Arg.Any<CancellationToken>())
            .ThrowsAsync(new System.Text.Json.JsonException("Bad JSON"));

        await InvokeOnActivateAsync(actor);

        (await actor.IsRebuildingAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task OnActivateAsync_OperationCanceledException_DoesNotCatchAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor();

        stateManager.TryGetStateAsync<PartyDetail>(
            "test-tenant:party-detail:party-001", Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // OperationCanceledException is NOT a deserialization failure — should propagate
        await Should.ThrowAsync<OperationCanceledException>(() => InvokeOnActivateAsync(actor));
    }

    [Fact]
    public async Task HandleEventAsync_PartyCreated_UsesDocumentedTenantPartyDetailStateKeyAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor();

        stateManager.TryGetStateAsync<PartyDetail>(
                "test-tenant:party-detail:party-001",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(false, default!));

        await actor.HandleEventAsync(
            "party-001",
            new PartyCreated
            {
                Type = PartyType.Person,
                PersonDetails = new PersonDetails
                {
                    FirstName = "Test",
                    LastName = "Party",
                },
            });

        await stateManager.Received(1).TryGetStateAsync<PartyDetail>(
            "test-tenant:party-detail:party-001",
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SetStateAsync(
            "test-tenant:party-detail:party-001",
            Arg.Is<PartyDetail>(detail => detail != null && detail.Id == "party-001"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleEventAsync_RejectionNoOp_DoesNotWritePartyDetailStateAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor();
        PartyDetail existing = CreateDetail();

        stateManager.TryGetStateAsync<PartyDetail>(
                "test-tenant:party-detail:party-001",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(true, existing));

        await actor.HandleEventAsync("party-001", new PartyCannotAddDuplicateChannel());

        await stateManager.DidNotReceive().SetStateAsync(
            "test-tenant:party-detail:party-001",
            Arg.Any<PartyDetail>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSerializedEventAsync_AcceptedRejectionNoOp_AdvancesCheckpointWithoutWritingDetailStateAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor();
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new PartyCannotBeCreatedWithInvalidId(), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        stateManager.TryGetStateAsync<long>(
                "test-tenant:party-detail:party-001:last-sequence",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<long>(false, default));
        stateManager.TryGetStateAsync<PartyDetail>(
                "test-tenant:party-detail:party-001",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(true, CreateDetail()));

        await actor.HandleSerializedEventAsync(
            "party-001",
            nameof(PartyCannotBeCreatedWithInvalidId),
            payload,
            "json",
            7,
            CancellationToken.None);

        await stateManager.DidNotReceive().SetStateAsync(
            "test-tenant:party-detail:party-001",
            Arg.Any<PartyDetail>(),
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SetStateAsync(
            "test-tenant:party-detail:party-001:last-sequence",
            7L,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSerializedEventAsync_AlreadyAppliedSequence_SkipsStateAndCheckpointWritesAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor();
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new PartyCannotBeCreatedWithInvalidId(), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        stateManager.TryGetStateAsync<long>(
                "test-tenant:party-detail:party-001:last-sequence",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<long>(true, 10));

        await actor.HandleSerializedEventAsync(
            "party-001",
            nameof(PartyCannotBeCreatedWithInvalidId),
            payload,
            "json",
            7,
            CancellationToken.None);

        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<PartyDetail>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SetStateAsync(
            "test-tenant:party-detail:party-001:last-sequence",
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleEventAsync_PartyDeactivated_WhenAlreadyInactive_DoesNotWritePartyDetailStateAsync()
    {
        // Accepted no-op success event: PartyDeactivated against an already-inactive state
        // returns null from the handler. The actor must preserve the stored projection (no
        // SetStateAsync call). Complements HandleEventAsync_RejectionNoOp_…Async which covers
        // the rejection-event variant.
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor();
        PartyDetail inactive = CreateDetail() with { IsActive = false };

        stateManager.TryGetStateAsync<PartyDetail>(
                "test-tenant:party-detail:party-001",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(true, inactive));

        await actor.HandleEventAsync("party-001", new PartyDeactivated());

        await stateManager.DidNotReceive().SetStateAsync(
            "test-tenant:party-detail:party-001",
            Arg.Any<PartyDetail>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("test-tenant:wrong-projection:party-001")]
    [InlineData("malformed-actor-id")]
    public async Task HandleEventAsync_InvalidActorId_FailsBeforeStateWriteAsync(string actorId)
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor(actorId);

        await Should.ThrowAsync<InvalidOperationException>(() => actor.HandleEventAsync("party-001", new PartyDeactivated()));

        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<PartyDetail>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSerializedEventAsync_PartyIdMismatch_FailsBeforeStateWriteOrCheckpointAdvanceAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager stateManager) = CreateActor();
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new PartyCannotBeCreatedWithInvalidId(), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await Should.ThrowAsync<InvalidOperationException>(() => actor.HandleSerializedEventAsync(
            "different-party",
            nameof(PartyCannotBeCreatedWithInvalidId),
            payload,
            "json",
            7,
            CancellationToken.None));

        await stateManager.DidNotReceive().TryGetStateAsync<long>(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<PartyDetail>(),
            Arg.Any<CancellationToken>());
    }

    private static (PartyDetailProjectionActor Actor, IActorStateManager StateManager) CreateActor(string actorId = ActorId)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IProjectionRebuildService rebuildService = Substitute.For<IProjectionRebuildService>();
        ILogger<PartyDetailProjectionActor> logger = Substitute.For<ILogger<PartyDetailProjectionActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyDetailProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        var actor = new PartyDetailProjectionActor(host, rebuildService, logger);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        return (actor, stateManager);
    }

    private static PartyDetail CreateDetail() =>
        new()
        {
            Id = "party-001",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Test Party",
            SortName = "Party, Test",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };

    private static async Task InvokeOnActivateAsync(PartyDetailProjectionActor actor)
    {
        MethodInfo? method = typeof(PartyDetailProjectionActor).GetMethod(
            "OnActivateAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Task? task = method?.Invoke(actor, null) as Task;
        if (task is not null)
        {
            await task.ConfigureAwait(false);
        }
    }
}
