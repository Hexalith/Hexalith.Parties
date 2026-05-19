using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
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

namespace Hexalith.Parties.Tests.Projections;

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
        (PartyIndexProjectionActor actor, _) = CreateActor(actorId: "empty-tenant:party-index");
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

    [Fact]
    public async Task HandleEventAsync_UsesSingleKeyPartitionStateAndManifestKeysAsync()
    {
        (PartyIndexProjectionActor actor, IActorStateManager stateManager) =
            CreateActor(options: new ProjectionOptions { BatchSize = 1 });
        SetupEmptyIndexState(stateManager);

        await actor.HandleEventAsync(
            "party-1",
            new PartyCreated
            {
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            });

        await stateManager.Received(1).SetStateAsync(
            "test-tenant:party-index:default",
            Arg.Is<Dictionary<string, PartyIndexEntry>>(state =>
                state.ContainsKey("party-1")
                && state["party-1"].DisplayName == "Ada Lovelace"),
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SetStateAsync(
            "test-tenant:party-index:manifest",
            Arg.Is<List<string>>(ids => ids.SequenceEqual(new[] { "party-1" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSerializedEventAsync_RejectionReplayAdvancesCheckpointWithoutIndexWriteAsync()
    {
        (PartyIndexProjectionActor actor, IActorStateManager stateManager) = CreateActor();
        SetupEmptyIndexState(stateManager);
        SetupMissingSequenceCheckpoint(stateManager);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            new PartyCannotAddDuplicateChannel(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await actor.HandleSerializedEventAsync(
            "party-1",
            nameof(PartyCannotAddDuplicateChannel),
            payload,
            "json",
            7,
            CancellationToken.None);

        await stateManager.Received(1).SetStateAsync(
            "test-tenant:party-index:party-1:last-sequence",
            7L,
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SetStateAsync(
            "test-tenant:party-index:default",
            Arg.Any<Dictionary<string, PartyIndexEntry>>(),
            Arg.Any<CancellationToken>());
        // Rejection replay advances the per-party checkpoint but must not touch the
        // tenant manifest — a regression that drops the _pendingChanges guard or
        // moves the manifest write outside PersistStateAsync would slip through
        // without this assertion.
        await stateManager.DidNotReceive().SetStateAsync(
            "test-tenant:party-index:manifest",
            Arg.Any<List<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEntriesAsync_WithMalformedActorId_ReturnsEmptyAsync()
    {
        (PartyIndexProjectionActor actor, _) = CreateActor(actorId: "malformed-party-index");

        IReadOnlyDictionary<string, PartyIndexEntry> result = await actor.GetEntriesAsync();

        result.ShouldBeEmpty();
    }

    private static (PartyIndexProjectionActor Actor, IActorStateManager StateManager) CreateActor(
        string actorId = ActorId,
        ProjectionOptions? options = null)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IIndexPartitionStrategy partitionStrategy = new SingleKeyPartitionStrategy();
        IOptions<ProjectionOptions> projectionOptions = Options.Create(options ?? new ProjectionOptions());
        IProjectionRebuildService rebuildService = Substitute.For<IProjectionRebuildService>();
        ILogger<PartyIndexProjectionActor> logger = Substitute.For<ILogger<PartyIndexProjectionActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyIndexProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        var actor = new PartyIndexProjectionActor(host, partitionStrategy, projectionOptions, rebuildService, logger);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        return (actor, stateManager);
    }

    private static void SetupEmptyIndexState(IActorStateManager stateManager)
    {
        stateManager.TryGetStateAsync<Dictionary<string, PartyIndexEntry>>(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<Dictionary<string, PartyIndexEntry>>(true, []));
    }

    private static void SetupMissingSequenceCheckpoint(IActorStateManager stateManager)
    {
        stateManager.TryGetStateAsync<long>(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<long>(false, default));
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
