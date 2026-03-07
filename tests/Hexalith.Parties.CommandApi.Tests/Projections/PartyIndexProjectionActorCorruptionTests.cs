using System.Reflection;
using System.Runtime.Serialization;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Projections.Strategies;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Projections;

public sealed class PartyIndexProjectionActorCorruptionTests
{
    private const string ActorId = "test-tenant:party-index";

    [Fact]
    public async Task OnActivateAsync_DeserializationFailure_SetsRebuildingFlagAsync()
    {
        (PartyIndexProjectionActor actor, IActorStateManager stateManager) = CreateActor();

        stateManager.TryGetStateAsync<Dictionary<string, PartyIndexEntry>>(
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SerializationException("Corrupted state"));

        await InvokeOnActivateAsync(actor);

        (await actor.IsRebuildingAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task OnActivateAsync_NormalState_DoesNotSetRebuildingFlagAsync()
    {
        (PartyIndexProjectionActor actor, IActorStateManager stateManager) = CreateActor();

        stateManager.TryGetStateAsync<Dictionary<string, PartyIndexEntry>>(
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<Dictionary<string, PartyIndexEntry>>(true, []));

        await InvokeOnActivateAsync(actor);

        (await actor.IsRebuildingAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task GetEntriesAsync_WhenRebuilding_ReturnsEmptyAsync()
    {
        (PartyIndexProjectionActor actor, _) = CreateActor();
        actor.SetRebuilding(true);

        IReadOnlyDictionary<string, PartyIndexEntry> result = await actor.GetEntriesAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task OnActivateAsync_FormatException_SetsRebuildingFlagAsync()
    {
        (PartyIndexProjectionActor actor, IActorStateManager stateManager) = CreateActor();

        stateManager.TryGetStateAsync<Dictionary<string, PartyIndexEntry>>(
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FormatException("Bad format"));

        await InvokeOnActivateAsync(actor);

        (await actor.IsRebuildingAsync()).ShouldBeTrue();
    }

    private static (PartyIndexProjectionActor Actor, IActorStateManager StateManager) CreateActor()
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IIndexPartitionStrategy partitionStrategy = new SingleKeyPartitionStrategy();
        IOptions<ProjectionOptions> options = Options.Create(new ProjectionOptions());
        IProjectionRebuildService rebuildService = Substitute.For<IProjectionRebuildService>();
        ILogger<PartyIndexProjectionActor> logger = Substitute.For<ILogger<PartyIndexProjectionActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyIndexProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(ActorId), TimerManager = timerManager });
        var actor = new PartyIndexProjectionActor(host, partitionStrategy, options, rebuildService, logger);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        return (actor, stateManager);
    }

    private static async Task InvokeOnActivateAsync(PartyIndexProjectionActor actor)
    {
        MethodInfo? method = typeof(PartyIndexProjectionActor).GetMethod(
            "OnActivateAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Task? task = method?.Invoke(actor, null) as Task;
        if (task is not null)
        {
            await task.ConfigureAwait(false);
        }
    }
}
