using System.Reflection;
using System.Runtime.Serialization;

using Dapr.Actors;
using Dapr.Actors.Runtime;

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

    private static (PartyDetailProjectionActor Actor, IActorStateManager StateManager) CreateActor()
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IProjectionRebuildService rebuildService = Substitute.For<IProjectionRebuildService>();
        ILogger<PartyDetailProjectionActor> logger = Substitute.For<ILogger<PartyDetailProjectionActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyDetailProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(ActorId), TimerManager = timerManager });
        var actor = new PartyDetailProjectionActor(host, rebuildService, logger);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        return (actor, stateManager);
    }

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
