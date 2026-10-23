using System.Reflection;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Serialization;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Search;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Handlers;

public sealed class PartySdkProjectionHandlerTests
{
    private static readonly JsonSerializerOptions s_canonicalJsonOptions = new(PartiesJsonOptions.Default)
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly JsonSerializerOptions s_explicitNullJsonOptions = new(s_canonicalJsonOptions)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };
    private static readonly IOptions<PartySdkReadModelOptions> s_options =
        Options.Create(new PartySdkReadModelOptions { ReadModelStateStoreName = "statestore" });

    [Theory]
    [InlineData("ContactChannelAdded", false)]
    [InlineData("ContactChannelAdded", true)]
    [InlineData("ContactChannelUpdated", false)]
    [InlineData("ContactChannelUpdated", true)]
    [InlineData("ContactChannelRemoved", false)]
    [InlineData("ContactChannelRemoved", true)]
    [InlineData("IdentifierAdded", false)]
    [InlineData("IdentifierAdded", true)]
    [InlineData("IdentifierRemoved", false)]
    [InlineData("IdentifierRemoved", true)]
    public async Task IncompleteIgnoredContentMutationRetainsCheckpointAndWitnessUntilCompleteReplayAsync(string mutation, bool erasureFirst)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelBatchStore();
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        List<PartyIndexEntry> indexed = [];
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), token).Returns(call =>
            {
                indexed.Add(call.Arg<PartyIndexEntry>());
                return true;
            });
        search.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), token).Returns(true);
        ProjectionRequest seed = CreateRequest() with
        {
            Events =
            [
                Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch),
                Event(new ContactChannelAdded { ContactChannelId = "email", Type = ContactChannelType.Email, Value = "seed@example.invalid" }, 2, DateTimeOffset.UnixEpoch.AddSeconds(1)),
                Event(new IdentifierAdded { IdentifierId = "tax", Type = IdentifierType.Other, Value = "seed-tax" }, 3, DateTimeOffset.UnixEpoch.AddSeconds(2)),
            ],
        };
        PartyIndexSdkProjectionHandler Handler() => new(store, s_options, search);
        (await Handler().ProjectAsync(seed, "serialized-seed", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        indexed.Clear();
        string key = PartySdkReadModelAddresses.Index(seed.TenantId);
        ReadModelEntry<PartyIndexSdkReadModel> before = await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, token);
        before.Value.ShouldNotBeNull().Entries[seed.AggregateId].SearchableContactChannels.ShouldBeEmpty();
        before.Value.Entries[seed.AggregateId].SearchableIdentifiers.ShouldBeEmpty();
        string originalHash = before.Value.SearchContentHashes.ShouldNotBeNull()[seed.AggregateId];
        string originalJson = JsonSerializer.Serialize(before.Value, s_canonicalJsonOptions);
        object change = mutation switch
        {
            nameof(ContactChannelAdded) => new ContactChannelAdded { ContactChannelId = "second", Type = ContactChannelType.Email, Value = "second@example.invalid" },
            nameof(ContactChannelUpdated) => new ContactChannelUpdated { ContactChannelId = "email", Value = "updated@example.invalid" },
            nameof(ContactChannelRemoved) => new ContactChannelRemoved { ContactChannelId = "email" },
            nameof(IdentifierAdded) => new IdentifierAdded { IdentifierId = "second", Type = IdentifierType.Other, Value = "second-tax" },
            _ => new IdentifierRemoved { IdentifierId = "tax" },
        };
        ProjectionEventDto tail = Event(change, 4, DateTimeOffset.UnixEpoch.AddSeconds(3));
        ProjectionRequest complete = seed with { Events = [.. seed.Events, tail] };
        object changedSeed = mutation.StartsWith("Contact", StringComparison.Ordinal)
            ? new IdentifierAdded { IdentifierId = "tax", Type = IdentifierType.Other, Value = "changed-tax" }
            : new ContactChannelAdded { ContactChannelId = "email", Type = ContactChannelType.Email, Value = "changed@example.invalid" };
        int changedIndex = mutation.StartsWith("Contact", StringComparison.Ordinal) ? 2 : 1;
        ProjectionEventDto[] changedEvents = [.. complete.Events];
        changedEvents[changedIndex] = Event(changedSeed, seed.Events[changedIndex].SequenceNumber, seed.Events[changedIndex].Timestamp);
        ProjectionRequest changed = complete with { Events = changedEvents };
        foreach (ProjectionRequest rejected in new[] { seed with { Events = [tail] }, changed })
        {
            DomainProjectionHandlerResult result = await Handler().ProjectAsync(rejected, "rejected-tail-or-source", token);
            result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
            result.ReasonCode.ShouldBe("search-reconciliation-required");
            ReadModelEntry<PartyIndexSdkReadModel> retained = await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, token);
            retained.ETag.ShouldBe(before.ETag);
            JsonSerializer.Serialize(retained.Value, s_canonicalJsonOptions).ShouldBe(originalJson);
            retained.Value!.LastSequenceNumbers[seed.AggregateId].ShouldBe(3);
            retained.Value.SearchContentHashes![seed.AggregateId].ShouldBe(originalHash);
            indexed.ShouldBeEmpty();
        }

        (await Handler().ProjectAsync(complete, "complete-current", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        PartyIndexEntry published = indexed.ShouldHaveSingleItem();
        published.SearchableContactChannels.Count.ShouldBe(mutation == nameof(ContactChannelAdded) ? 2 : mutation == nameof(ContactChannelRemoved) ? 0 : 1);
        published.SearchableIdentifiers.Count.ShouldBe(mutation == nameof(IdentifierAdded) ? 2 : mutation == nameof(IdentifierRemoved) ? 0 : 1);
        if (mutation == nameof(ContactChannelUpdated))
        {
            published.SearchableContactChannels[0].Value.ShouldBe("updated@example.invalid");
        }
        ReadModelEntry<PartyIndexSdkReadModel> committed = await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, token);
        committed.Value!.LastSequenceNumbers[seed.AggregateId].ShouldBe(4);
        committed.Value.SearchContentHashes![seed.AggregateId].ShouldNotBe(originalHash);
        (await Handler().ProjectAsync(changed, "changed-after-restart", token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        (await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, token)).ETag.ShouldBe(committed.ETag);
        indexed.Count.ShouldBe(1);

        long erasureSequence = erasureFirst ? 5 : 6;
        DateTimeOffset erasedAt = DateTimeOffset.UnixEpoch.AddSeconds(erasureSequence - 1);
        ProjectionEventDto erasure = Event(new PartyErased
        { PartyId = seed.AggregateId, TenantId = seed.TenantId, ErasedAt = erasedAt }, erasureSequence, erasedAt);
        ProjectionEventDto mutationTail = tail with
        {
            SequenceNumber = erasureFirst ? 6 : 5,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(erasureFirst ? 5 : 4),
        };
        ProjectionRequest erasedTail = seed with
        {
            Events = erasureFirst ? [erasure, mutationTail] : [mutationTail, erasure],
        };
        (await Handler().ProjectAsync(erasedTail, "erase-and-suppress-tail", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        PartyIndexSdkReadModel erased = store.Deserialize<PartyIndexSdkReadModel>(key);
        erased.Entries.ShouldNotContainKey(seed.AggregateId);
        erased.SearchContentHashes.ShouldBeNull();
        erased.LastSequenceNumbers[seed.AggregateId].ShouldBe(6);
        erased.ErasureSequenceNumbers[seed.AggregateId].ShouldBe(erasureSequence);
        indexed.Count.ShouldBe(1);
        await search.Received(1).NotifyRemovedAsync(seed.TenantId, seed.AggregateId, token);
    }

    [Fact]
    public async Task CompleteHistoricalReplayPersistsSearchWitnessBeforeRestartAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelBatchStore();
        ProjectionRequest source = CreateRequest() with
        {
            Events = [CreateRequest().Events[0], Event(new ContactChannelAdded
            { ContactChannelId = "email", Type = ContactChannelType.Email, Value = "original@example.invalid" }, 2, DateTimeOffset.UnixEpoch.AddSeconds(1))],
        };
        string key = PartySdkReadModelAddresses.Index(source.TenantId);
        PartyIndexSdkReadModel legacy = PartyIndexSdkProjectionHandler.Fold(source, null) with { SearchContentHashes = null };
        await store.SaveAsync("statestore", key, legacy, token);
        string? oldEtag = (await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, token)).ETag;
        (await new PartyIndexSdkProjectionHandler(store, s_options).ProjectAsync(source, "upgrade-witness", token))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        ReadModelEntry<PartyIndexSdkReadModel> upgraded = await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, token);
        upgraded.ETag.ShouldNotBe(oldEtag);
        upgraded.Value.ShouldNotBeNull().LastSequenceNumbers[source.AggregateId].ShouldBe(2);
        upgraded.Value.SearchContentHashes.ShouldNotBeNull().ContainsKey(source.AggregateId).ShouldBeTrue();
        ProjectionRequest changed = source with
        {
            Events = [source.Events[0], Event(new ContactChannelAdded
            { ContactChannelId = "email", Type = ContactChannelType.Email, Value = "changed@example.invalid" }, 2, source.Events[1].Timestamp)],
        };
        (await new PartyIndexSdkProjectionHandler(store, s_options).ProjectAsync(changed, "changed-after-restart", token))
            .Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        (await new PartyIndexSdkProjectionHandler(store, s_options).ProjectAsync(source, "matching-after-restart", token))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        ReadModelEntry<PartyIndexSdkReadModel> retained = await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, token);
        retained.ETag.ShouldBe(upgraded.ETag);
        JsonSerializer.Serialize(retained.Value, s_canonicalJsonOptions).ShouldBe(JsonSerializer.Serialize(upgraded.Value, s_canonicalJsonOptions));
    }

    [Fact]
    public async Task SearchRetryRestoresAllIgnoredContentAndRejectsChangedOrPartialReplayAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelBatchStore();
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(false, true);
        ProjectionRequest complete = CreateRequest() with
        {
            Events =
            [
                Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch),
                Event(new ContactChannelAdded { ContactChannelId = "email", Type = ContactChannelType.Email, Value = "old@example.invalid" }, 2, DateTimeOffset.UnixEpoch.AddSeconds(1)),
                Event(new IdentifierAdded { IdentifierId = "tax", Type = IdentifierType.Other, Value = "synthetic-tax" }, 3, DateTimeOffset.UnixEpoch.AddSeconds(2)),
                Event(new ContactChannelUpdated { ContactChannelId = "email", Value = "current@example.invalid" }, 4, DateTimeOffset.UnixEpoch.AddSeconds(3)),
            ],
        };
        PartyIndexSdkProjectionHandler Projection() => new(store, s_options, search);
        (await Projection().ProjectAsync(complete, "failed-first", token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        string key = PartySdkReadModelAddresses.Index(complete.TenantId);
        PartyIndexSdkReadModel persisted = store.Deserialize<PartyIndexSdkReadModel>(key);
        persisted.Entries[complete.AggregateId].SearchableContactChannels.ShouldBeEmpty();
        persisted.Entries[complete.AggregateId].SearchableIdentifiers.ShouldBeEmpty();
        persisted.SearchContentHashes.ShouldNotBeNull().ContainsKey(complete.AggregateId).ShouldBeTrue();
        var changed = complete with { Events = [.. complete.Events[..3], Event(new ContactChannelUpdated { ContactChannelId = "email", Value = "changed@example.invalid" }, 4, DateTimeOffset.UnixEpoch.AddSeconds(3))] };
        foreach (ProjectionRequest invalid in new[]
        {
            complete with { Events = complete.Events[..3] },
            complete with { Events = complete.Events[1..] },
            changed,
        })
        {
            (await Projection().ProjectAsync(invalid, "incomplete-or-changed", token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        }

        (await Projection().ProjectAsync(complete, "complete-restart", token)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        await search.Received(2).NotifyIndexedAsync(complete.TenantId,
            Arg.Is<PartyIndexEntry>(entry => entry.SearchableContactChannels.Count == 1
                && entry.SearchableContactChannels[0].Value == "current@example.invalid"
                && entry.SearchableIdentifiers.Count == 1 && entry.SearchableIdentifiers[0].Value == "synthetic-tax"),
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), 4, token);
        ProjectionRequest next = complete with { Events = [.. complete.Events, Event(new IdentifierRemoved { IdentifierId = "tax" }, 5, DateTimeOffset.UnixEpoch.AddSeconds(4))] };
        (await Projection().ProjectAsync(next, "next-complete", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await search.Received(1).NotifyIndexedAsync(complete.TenantId,
            Arg.Is<PartyIndexEntry>(entry => entry.SearchableContactChannels.Count == 1 && entry.SearchableIdentifiers.Count == 0),
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), 5, token);
        ProjectionRequest erasure = complete with { Events = [Event(new PartyErased { PartyId = complete.AggregateId, TenantId = complete.TenantId, ErasedAt = DateTimeOffset.UnixEpoch.AddSeconds(5) }, 6, DateTimeOffset.UnixEpoch.AddSeconds(5))] };
        search.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), token).Returns(true);
        (await Projection().ProjectAsync(erasure, "erased", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Deserialize<PartyIndexSdkReadModel>(key).SearchContentHashes.ShouldBeNull();
        (await Projection().ProjectAsync(complete, "old-erased-replay", token)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
    }

    [Fact]
    public async Task CleanupTombstonePreservesCommittedLifecycleAndReplayBackfillsHistoricalRowsAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelBatchStore();
        ProjectionRequest source = CreateRequest() with
        {
            Events = [CreateRequest().Events[0], Event(new PartyEncryptionKeyDeleted
            { PartyId = "party-1", TenantId = "tenant-a", DeletedAt = DateTimeOffset.UnixEpoch.AddSeconds(1) }, 2, DateTimeOffset.UnixEpoch.AddSeconds(1))],
        };
        var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        (await handler.ProjectAsync(source, "key-destroyed", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string key = PartySdkReadModelAddresses.Detail(source.TenantId, source.AggregateId);
        PartyDetailSdkReadModel committed = store.Deserialize<PartyDetailSdkReadModel>(key);
        committed.ErasureLifecycle.ShouldBe(new PartyErasureStatusRecord
        {
            PartyId = source.AggregateId, TenantId = source.TenantId, Status = "KeyDestroyed", UpdatedAt = DateTimeOffset.UnixEpoch.AddSeconds(1),
        });
        PartyDetailSdkReadModel cleanup = PartySdkReadModelEraser.RedactDetail(committed, source.AggregateId);
        cleanup.ErasureLifecycle.ShouldBe(committed.ErasureLifecycle);
        cleanup.Detail.ShouldNotBeNull().IsErased.ShouldBeTrue();
        // A legacy cleanup row has no lifecycle proof. Only a complete committed prefix backfills it.
        await store.SaveAsync("statestore", key, cleanup with { ErasureLifecycle = null }, token);
        (await handler.ProjectAsync(source with { Events = [source.Events[1]] }, "legacy-tail", token)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        store.Deserialize<PartyDetailSdkReadModel>(key).ErasureLifecycle.ShouldBeNull();
        (await handler.ProjectAsync(source, "legacy-complete", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Deserialize<PartyDetailSdkReadModel>(key).ErasureLifecycle.ShouldBe(committed.ErasureLifecycle);
        ProjectionRequest terminal = source with { Events = [Event(new PartyErased
        { PartyId = source.AggregateId, TenantId = source.TenantId, ErasedAt = DateTimeOffset.UnixEpoch.AddSeconds(2) }, 3, DateTimeOffset.UnixEpoch.AddSeconds(2))] };
        (await handler.ProjectAsync(terminal, "terminal-committed", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var expectedTerminal = new PartyErasureStatusRecord
        {
            PartyId = source.AggregateId, TenantId = source.TenantId, Status = "Erased",
            UpdatedAt = DateTimeOffset.UnixEpoch.AddSeconds(2), ErasedAt = DateTimeOffset.UnixEpoch.AddSeconds(2),
        };
        store.Deserialize<PartyDetailSdkReadModel>(key).ErasureLifecycle.ShouldBe(expectedTerminal);
        (await new PartyDetailSdkProjectionHandler(store, store, s_options).ProjectAsync(terminal, "terminal-duplicate", token))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        store.Deserialize<PartyDetailSdkReadModel>(key).ErasureLifecycle.ShouldBe(expectedTerminal);
    }

    [Fact]
    public async Task CommittedErasureLifecycleSurvivesRestartAndDuplicateDeliveryAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelBatchStore();
        IEventPayload[] events =
        [
            new PartyCreated { Type = PartyType.Person },
            new ErasePartyRequested { PartyId = "party-1", TenantId = "tenant-a", RequestedAt = DateTimeOffset.UnixEpoch.AddSeconds(1), RequestedBy = "operator" },
            new PartyErasureCancelled { PartyId = "party-1", TenantId = "tenant-a", CancelledAt = DateTimeOffset.UnixEpoch.AddSeconds(2), CancelledBy = "operator" },
            new ErasePartyRequested { PartyId = "party-1", TenantId = "tenant-a", RequestedAt = DateTimeOffset.UnixEpoch.AddSeconds(3), RequestedBy = "operator" },
            new PartyEncryptionKeyDeleted { PartyId = "party-1", TenantId = "tenant-a", DeletedAt = DateTimeOffset.UnixEpoch.AddSeconds(4) },
            new ErasureVerified { PartyId = "party-1", TenantId = "tenant-a", VerifiedAt = DateTimeOffset.UnixEpoch.AddSeconds(5), VerificationReportId = "report" },
            new PartyErased { PartyId = "party-1", TenantId = "tenant-a", ErasedAt = DateTimeOffset.UnixEpoch.AddSeconds(6) },
            new PartyErasureCancelled { PartyId = "party-1", TenantId = "tenant-a", CancelledAt = DateTimeOffset.UnixEpoch.AddSeconds(7), CancelledBy = "late" },
        ];
        string?[] statuses = [null, "ErasurePending", "Active", "ErasurePending", "KeyDestroyed", "Verified", "Erased", "Erased"];
        string key = PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
        for (int index = 0; index < events.Length; index++)
        {
            ProjectionRequest delivery = CreateRequest() with
            {
                Events = [Event(events[index], index + 1, DateTimeOffset.UnixEpoch.AddSeconds(index))],
            };
            var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
            (await handler.ProjectAsync(delivery, $"lifecycle-{index}", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            PartyErasureStatusRecord? expected = index == 0 ? null : new PartyErasureStatusRecord
            {
                PartyId = "party-1", TenantId = "tenant-a", Status = statuses[index]!,
                UpdatedAt = DateTimeOffset.UnixEpoch.AddSeconds(Math.Min(index, 6)),
                ErasedAt = index >= 6 ? DateTimeOffset.UnixEpoch.AddSeconds(6) : null,
            };
            store.Deserialize<PartyDetailSdkReadModel>(key).ErasureLifecycle.ShouldBe(expected);
            (await new PartyDetailSdkProjectionHandler(store, store, s_options).ProjectAsync(delivery, $"retry-{index}", token))
                .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
            store.Deserialize<PartyDetailSdkReadModel>(key).ErasureLifecycle.ShouldBe(expected);
        }

        PartyDetailSdkReadModel persisted = store.Deserialize<PartyDetailSdkReadModel>(key);
        persisted.ErasureLifecycle.ShouldNotBeNull().UpdatedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(6));
        persisted.ErasureLifecycle.ErasedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(6));
        persisted.ErasureSequenceNumber.ShouldBe(7);
        persisted.Detail.ShouldNotBeNull().IsErased.ShouldBeTrue();
        persisted.LastSequenceNumber.ShouldBe(8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdvancingLiveDeliveryWaitsForEachReadAndPersistsBothCoordinatedSlotsAsync(bool processingFirst)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        IReadModelStore reads = Substitute.For<IReadModelStore>();
        var writes = new InMemoryReadModelBatchStore();
        var detail = new TaskCompletionSource<ReadModelEntry<PartyDetailSdkReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processing = new TaskCompletionSource<ReadModelEntry<PartyProcessingSdkReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        reads.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), token).Returns(detail.Task);
        reads.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), token).Returns(processing.Task);
        var handler = new PartyDetailSdkProjectionHandler(reads, writes, s_options);
        ProjectionRequest request = CreateRequest();
        string detailKey = PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId);
        string processingKey = PartySdkReadModelAddresses.Processing(request.TenantId, request.AggregateId);
        Task<DomainProjectionHandlerResult> projection = handler.ProjectAsync(request, "advancing-paired-reads", token);
        await reads.Received(1).GetAsync<PartyDetailSdkReadModel>("statestore", detailKey, token);
        await reads.Received(1).GetAsync<PartyProcessingSdkReadModel>("statestore", processingKey, token);
        try
        {
            if (processingFirst)
            {
                processing.SetResult(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
            }
            else
            {
                detail.SetResult(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
            }

            projection.IsCompleted.ShouldBeFalse();
            (await writes.GetAsync<PartyDetailSdkReadModel>("statestore", detailKey, token)).Value.ShouldBeNull();
            (await writes.GetAsync<PartyProcessingSdkReadModel>("statestore", processingKey, token)).Value.ShouldBeNull();
            detail.TrySetResult(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
            processing.TrySetResult(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
            (await projection.ConfigureAwait(true)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            PartyDetailSdkReadModel persisted = writes.Deserialize<PartyDetailSdkReadModel>(detailKey);
            PartyProcessingSdkReadModel audit = writes.Deserialize<PartyProcessingSdkReadModel>(processingKey);
            persisted.LastSequenceNumber.ShouldBe(2);
            persisted.Detail.ShouldNotBeNull().Type.ShouldBe(PartyType.Person);
            persisted.Detail.IsActive.ShouldBeFalse();
            audit.LastSequenceNumber.ShouldBe(2);
            audit.Records.ShouldNotBeEmpty();
        }
        finally
        {
            detail.TrySetResult(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
            processing.TrySetResult(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        }
    }

    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "success")]
    [InlineData(false, "fault")]
    [InlineData(true, "fault")]
    [InlineData(false, "processing-fault")]
    [InlineData(true, "processing-fault")]
    [InlineData(false, "cancel")]
    [InlineData(true, "cancel")]
    public async Task PairedLiveReadsStartTogetherAndDoNotWriteUntilBothSucceedAsync(bool reload, string outcome)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        CancellationToken token = cancellation.Token;
        IReadModelStore reads = Substitute.For<IReadModelStore>();
        var writes = new InMemoryReadModelBatchStore();
        var detail = new TaskCompletionSource<ReadModelEntry<PartyDetailSdkReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processing = new TaskCompletionSource<ReadModelEntry<PartyProcessingSdkReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int detailReads = 0;
        int processingReads = 0;
        reads.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), token).Returns(_ =>
            reload && ++detailReads == 1
                ? Task.FromResult(new ReadModelEntry<PartyDetailSdkReadModel>(null, null))
                : detail.Task);
        reads.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), token).Returns(_ =>
        {
            if (reload && ++processingReads == 1)
            {
                return Task.FromResult(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
            }

            processingStarted.TrySetResult();
            return processing.Task;
        });
        var request = new ProjectionRequest("tenant-a", "party", "party-1",
            [new ProjectionEventDto("Future.UnknownEvent", "{}"u8.ToArray(), "json", 1, DateTimeOffset.UnixEpoch, "correlation-1")]);
        var handler = new PartyDetailSdkProjectionHandler(reads, writes, Options.Create(new PartySdkReadModelOptions()));
        Task<DomainProjectionHandlerResult> projection = handler.ProjectAsync(request, "paired-reads", token);

        try
        {
            await processingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            projection.IsCompleted.ShouldBeFalse();
            detail.Task.IsCompleted.ShouldBeFalse();
            processing.Task.IsCompleted.ShouldBeFalse();
            await reads.Received(reload ? 2 : 1).GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), token);

            // Only the existing empty initialization may have persisted at the reload boundary.
            string key = PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId);
            if (reload)
            {
                writes.Deserialize<PartyDetailSdkReadModel>(key).Detail.ShouldBeNull();
            }

            var detailValue = new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel { LastSequenceNumber = 1 }, "detail-etag");
            var processingValue = new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel { LastSequenceNumber = 1 }, "processing-etag");
            if (outcome == "fault")
            {
                detail.SetException(new InvalidOperationException("read failed"));
                processing.SetResult(processingValue);
                await Should.ThrowAsync<InvalidOperationException>(() => projection);
            }
            else if (outcome == "processing-fault")
            {
                detail.SetResult(detailValue);
                processing.SetException(new InvalidOperationException("processing read failed"));
                await Should.ThrowAsync<InvalidOperationException>(() => projection);
            }
            else if (outcome == "cancel")
            {
                using CancellationTokenRegistration registration = token.Register(() =>
                {
                    detail.TrySetCanceled(token);
                    processing.TrySetCanceled(token);
                });
                cancellation.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => projection);
            }
            else
            {
                detail.SetResult(detailValue);
                processing.SetResult(processingValue);
                (await projection.ConfigureAwait(true)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
            }

            ReadModelEntry<PartyDetailSdkReadModel> persisted = await writes.GetAsync<PartyDetailSdkReadModel>("statestore", key, TestContext.Current.CancellationToken);
            ReadModelEntry<PartyProcessingSdkReadModel> persistedProcessing = await writes.GetAsync<PartyProcessingSdkReadModel>(
                "statestore", PartySdkReadModelAddresses.Processing(request.TenantId, request.AggregateId), TestContext.Current.CancellationToken);
            if (reload)
            {
                persisted.Value.ShouldNotBeNull().LastSequenceNumber.ShouldBe(long.MinValue);
                persisted.Value.Detail.ShouldBeNull();
                persistedProcessing.Value.ShouldNotBeNull().LastSequenceNumber.ShouldBe(long.MinValue);
                persistedProcessing.Value.Records.ShouldBeEmpty();
            }
            else
            {
                persisted.Value.ShouldBeNull();
                persistedProcessing.Value.ShouldBeNull();
            }
        }
        finally
        {
            detail.TrySetResult(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
            processing.TrySetResult(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad:aggregate")]
    [InlineData("bad|aggregate")]
    [InlineData("bad\0aggregate")]
    [InlineData("bad\raggregate")]
    [InlineData("bad\naggregate")]
    public async Task IndexHandlerValidatesAggregateSegmentsWithoutBuildingADetailAddressAsync(string aggregateId)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        await Should.ThrowAsync<ArgumentException>(() => handler.ProjectAsync(
            CreateRequest() with { AggregateId = aggregateId }, "invalid-segment", TestContext.Current.CancellationToken));
        store.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task LargeRebuildCountsOnceAcrossSerializedHandlersAndMatchesPersistedLiveIndexAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        var liveStore = new InMemoryReadModelBatchStore();
        ILogger<PartyIndexSdkProjectionHandler> logger = CreateLogger();
        DomainSharedProjectionRebuildIdentity identity = Identity();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, logger: logger);
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        foreach (string party in new[] { "private-party-one", "private-party-two" })
        {
            ProjectionRequest history = History(party, corrupt: 1000, redacted: 200, whole: 300);
            handler = new PartyIndexSdkProjectionHandler(store, s_options, logger: logger);
            candidate = await handler.AccumulateAsync(identity, candidate, history, TestContext.Current.CancellationToken);
            // Retrying checkpointed history, including repeated sequence numbers, adds no counts.
            candidate = await handler.AccumulateAsync(identity, candidate, history, TestContext.Current.CancellationToken);
            var live = new PartyIndexSdkProjectionHandler(liveStore, s_options);
            (await live.ProjectAsync(history, "live", TestContext.Current.CancellationToken)).Status
                .ShouldBe(ProjectionDispatchStatus.Completed);
        }

        candidate = await handler.AccumulateAsync(identity, candidate, History("empty") with { Events = [] }, TestContext.Current.CancellationToken);
        logger.ReceivedCalls().ShouldBeEmpty();
        DomainProjectionRebuildPlan plan = await CommitAsync(store, handler, identity, candidate);
        logger.ReceivedCalls().ShouldBeEmpty();
        string key = PartySdkReadModelAddresses.Index(identity.TenantId);
        JsonSerializer.Serialize(store.Deserialize<PartyIndexSdkReadModel>(key), PartiesJsonOptions.Default)
            .ShouldBe(JsonSerializer.Serialize(liveStore.Deserialize<PartyIndexSdkReadModel>(key), PartiesJsonOptions.Default));
        string canonical = System.Text.Encoding.UTF8.GetString(plan.Operations.Single().CanonicalValue.Span);
        canonical.ShouldNotContain("diagnostics");
        canonical.ShouldNotContain("corruptPayload");

        handler = new PartyIndexSdkProjectionHandler(store, s_options, logger: logger);
        (await handler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        Dictionary<string, object?> summary = Summary(logger, LogLevel.Warning, completed: true);
        summary["CorruptPayload"].ShouldBe(2000L);
        summary["RedactedPayload"].ShouldBe(400L);
        summary["WholePayloadRedacted"].ShouldBe(600L);
        summary["NonJson"].ShouldBe(0L);
        summary["UnknownType"].ShouldBe(0L);
        summary["AmbiguousType"].ShouldBe(0L);
        summary["NullPayload"].ShouldBe(0L);
    }

    [Theory]
    [InlineData("non-json", "NonJson")]
    [InlineData("unknown", "UnknownType")]
    [InlineData("null", "NullPayload")]
    [InlineData("ambiguous", "AmbiguousType")]
    public async Task FailedAccumulationEmitsOneValueFreeSummaryAndPreservesCandidateAsync(string failure, string counter)
    {
        var store = new InMemoryReadModelBatchStore();
        ILogger<PartyIndexSdkProjectionHandler> logger = CreateLogger();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, logger: logger);
        DomainSharedProjectionRebuildIdentity identity = Identity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, History("earlier", corrupt: 1), TestContext.Current.CancellationToken);
        byte[] before = candidate.State.ToArray();
        ProjectionRequest failed = History("private-failed-party", corrupt: 100);
        string eventTypeName = failure switch
        {
            "unknown" => "private-unknown-type",
            "ambiguous" => "private-ambiguous-" + Guid.NewGuid().ToString("N"),
            _ => nameof(PartyCreated),
        };
        if (failure == "ambiguous")
        {
            SeedEventTypeResolverCacheAsAmbiguous(eventTypeName);
        }

        ProjectionEventDto last = failed.Events[^1] with
        {
            SequenceNumber = 102,
            EventTypeName = eventTypeName,
            SerializationFormat = failure == "non-json" ? "private-format" : "json",
            Payload = failure == "null" ? "null"u8.ToArray() : "{}"u8.ToArray(),
        };
        failed = failed with { Events = [.. failed.Events, last] };

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.AccumulateAsync(identity, candidate, failed, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe(PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason);
        candidate.State.ToArray().ShouldBe(before);
        (await store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index(identity.TenantId), TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        Dictionary<string, object?> summary = Summary(logger, LogLevel.Warning, completed: false);
        summary[counter].ShouldBe(1L);
        summary["CorruptPayload"].ShouldBe(101L);
    }

    [Theory]
    [InlineData("retry")]
    [InlineData("throw")]
    [InlineData("ignore-cancel")]
    public async Task SearchRetryOrCancellationDefersSummaryUntilSuccessfulCompletionAsync(string outcome)
    {
        var store = new InMemoryReadModelBatchStore();
        ILogger<PartyIndexSdkProjectionHandler> logger = CreateLogger();
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search, logger);
        DomainSharedProjectionRebuildIdentity identity = Identity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, History("party", whole: 2), TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan plan = await CommitAsync(store, handler, identity, candidate);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (outcome != "retry")
                {
                    cancellation.Cancel();
                    return outcome == "throw"
                        ? Task.FromCanceled<bool>(cancellation.Token)
                        : Task.FromResult(true);
                }

                return Task.FromResult(false);
            });
        if (outcome != "retry")
        {
            _ = await Should.ThrowAsync<OperationCanceledException>(() =>
                handler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, cancellation.Token));
        }
        else
        {
            (await handler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, cancellation.Token))
                .Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        }

        logger.ReceivedCalls().ShouldBeEmpty();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        handler = new PartyIndexSdkProjectionHandler(store, s_options, search, logger);
        (await handler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        Summary(logger, LogLevel.Information, completed: true)["WholePayloadRedacted"].ShouldBe(2L);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanAndEmptyRebuildsHaveNoDropSummaryAsync(bool empty)
    {
        var store = new InMemoryReadModelBatchStore();
        ILogger<PartyIndexSdkProjectionHandler> logger = CreateLogger();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, logger: logger);
        DomainSharedProjectionRebuildIdentity identity = Identity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        if (!empty)
        {
            candidate = await handler.AccumulateAsync(identity, candidate, History("party"), TestContext.Current.CancellationToken);
        }

        DomainProjectionRebuildPlan plan = await CommitAsync(store, handler, identity, candidate);
        (await handler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        logger.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task LegacyRawCandidateResumesAndOperationsKeepIndependentCountsAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        ILogger<PartyIndexSdkProjectionHandler> firstLogger = CreateLogger();
        ILogger<PartyIndexSdkProjectionHandler> secondLogger = firstLogger;
        var first = new PartyIndexSdkProjectionHandler(store, s_options, logger: firstLogger);
        PartyIndexSdkProjectionHandler second = first;
        DomainSharedProjectionRebuildIdentity firstIdentity = Identity();
        DomainSharedProjectionRebuildIdentity secondIdentity = Identity("second-operation");
        PartyIndexSdkReadModel oldModel = PartyIndexSdkProjectionHandler.Fold(History("legacy", corrupt: 5), null);
        var legacy = new DomainSharedProjectionRebuildCandidate(JsonSerializer.SerializeToUtf8Bytes(oldModel, PartiesJsonOptions.Default));
        DomainSharedProjectionRebuildCandidate other = await second.CreateEmptyCandidateAsync(secondIdentity, TestContext.Current.CancellationToken);
        legacy = await first.AccumulateAsync(firstIdentity, legacy, History("new", whole: 3), TestContext.Current.CancellationToken);
        other = await second.AccumulateAsync(secondIdentity, other, History("other", corrupt: 2), TestContext.Current.CancellationToken);

        DomainProjectionRebuildPlan plan = await CommitAsync(store, first, firstIdentity, legacy);
        (await first.CompleteRebuildAsync(firstIdentity, legacy, plan.CompletionState, TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        Dictionary<string, object?> firstSummary = Summary(firstLogger, LogLevel.Information, completed: true);
        firstSummary["WholePayloadRedacted"].ShouldBe(3L);
        firstSummary["CorruptPayload"].ShouldBe(0L);
        firstLogger.ClearReceivedCalls();
        plan = await CommitAsync(store, second, secondIdentity, other);
        (await second.CompleteRebuildAsync(secondIdentity, other, plan.CompletionState, TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        Dictionary<string, object?> secondSummary = Summary(secondLogger, LogLevel.Warning, completed: true);
        secondSummary["CorruptPayload"].ShouldBe(2L);
        secondSummary["WholePayloadRedacted"].ShouldBe(0L);
        store.Deserialize<PartyIndexSdkReadModel>(PartySdkReadModelAddresses.Index(firstIdentity.TenantId))
            .Entries.Keys.ShouldBe(["other"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SequenceValidationFailureStillProducesOneBoundedWarningAsync(bool conflict)
    {
        var store = new InMemoryReadModelBatchStore();
        ILogger<PartyIndexSdkProjectionHandler> logger = CreateLogger();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, logger: logger);
        DomainSharedProjectionRebuildIdentity identity = Identity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        ProjectionRequest failed = History("private-failure");
        failed = failed with
        {
            Events = conflict
                ? [failed.Events[0], failed.Events[0] with { Payload = "private-conflict"u8.ToArray() }]
                : [failed.Events[0] with { SequenceNumber = 2 }],
        };
        byte[] before = candidate.State.ToArray();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.AccumulateAsync(identity, candidate, failed, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe(conflict ? PartySdkProjectionFold.ConflictingDuplicateEventReason : PartySdkProjectionFold.DeliverySequenceGapReason);
        candidate.State.ToArray().ShouldBe(before);
        Dictionary<string, object?> summary = Summary(logger, LogLevel.Warning, completed: false);
        summary.Values.OfType<long>().ShouldAllBe(static value => value == 0);
        (await store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index(identity.TenantId), TestContext.Current.CancellationToken))
            .Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SerializedEnvelopeCasingAndSaturatedCountsPreserveIndexAsync(bool pascalCase)
    {
        var store = new InMemoryReadModelBatchStore();
        ILogger<PartyIndexSdkProjectionHandler> logger = CreateLogger();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, logger: logger);
        DomainSharedProjectionRebuildIdentity identity = Identity();
        var state = new PartyIndexRebuildCandidate(
            PartyIndexSdkProjectionHandler.Fold(History("original"), null),
            new PartyProjectionRebuildDiagnostics { CorruptPayload = long.MaxValue });
        JsonSerializerOptions serialization = new(PartiesJsonOptions.Default)
        {
            PropertyNamingPolicy = pascalCase ? null : JsonNamingPolicy.CamelCase,
        };
        var candidate = new DomainSharedProjectionRebuildCandidate(JsonSerializer.SerializeToUtf8Bytes(state, serialization));

        candidate = await handler.AccumulateAsync(identity, candidate, History("new", corrupt: 1), TestContext.Current.CancellationToken);

        PartyIndexRebuildCandidate resumed = JsonSerializer.Deserialize<PartyIndexRebuildCandidate>(candidate.State.Span, PartiesJsonOptions.Default)!;
        resumed.Diagnostics.CorruptPayload.ShouldBe(long.MaxValue);
        DomainProjectionRebuildPlan plan = await CommitAsync(store, handler, identity, candidate);
        (await handler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        Summary(logger, LogLevel.Warning, completed: true)["CorruptPayload"].ShouldBe(long.MaxValue);
        store.Deserialize<PartyIndexSdkReadModel>(PartySdkReadModelAddresses.Index(identity.TenantId))
            .Entries.Keys.Order(StringComparer.Ordinal).ShouldBe(["new", "original"]);
    }

    [Theory]
    [InlineData("{\"model\":null}")]
    [InlineData("{\"model\":null,\"diagnostics\":null}")]
    [InlineData("{\"Model\":null,\"Diagnostics\":null}")]
    [InlineData("{\"model\":{},\"diagnostics\":null}")]
    public async Task MalformedEnvelopeCannotBecomeAnEmptyReplacementAsync(string json)
    {
        var store = new InMemoryReadModelBatchStore();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        DomainSharedProjectionRebuildIdentity identity = Identity();
        var candidate = new DomainSharedProjectionRebuildCandidate(System.Text.Encoding.UTF8.GetBytes(json));

        InvalidOperationException accumulation = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.AccumulateAsync(identity, candidate, History("party"), TestContext.Current.CancellationToken));
        InvalidOperationException finalization = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken));

        accumulation.Message.ShouldBe("The shared Party index rebuild candidate is empty or malformed.");
        finalization.Message.ShouldBe(accumulation.Message);
        System.Text.Encoding.UTF8.GetString(candidate.State.Span).ShouldBe(json);
        (await store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index(identity.TenantId), TestContext.Current.CancellationToken))
            .Value.ShouldBeNull();
    }

    /// <summary>Rejects malformed canonical rows without writes or external notifications.</summary>
    /// <param name="dictionary">The explicitly null dictionary.</param>
    [Theory]
    [InlineData("Entries")]
    [InlineData("LastIndexedEvents")]
    [InlineData("LastSequenceNumbers")]
    [InlineData("ErasureSequenceNumbers")]
    public async Task NullIndexDictionaryRequiresRebuildWithoutChangingPersistedStateAsync(string dictionary)
    {
        var store = new InMemoryReadModelBatchStore(writeOptions: s_explicitNullJsonOptions);
        string key = PartySdkReadModelAddresses.Index("tenant-a");
        PartyIndexSdkReadModel corrupt = NullIndexDictionary(CreateIndex("private-unrelated"), dictionary);
        await store.SaveAsync("statestore", key, corrupt, TestContext.Current.CancellationToken);
        ReadModelEntry<PartyIndexSdkReadModel> before = await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, TestContext.Current.CancellationToken);
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(CreateRequest(), "dispatch", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("projection-rebuild-required");
        ReadModelEntry<PartyIndexSdkReadModel> after = await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, TestContext.Current.CancellationToken);
        after.ETag.ShouldBe(before.ETag);
        JsonSerializer.Serialize(after.Value, PartiesJsonOptions.Default).ShouldBe(JsonSerializer.Serialize(corrupt, PartiesJsonOptions.Default));
        search.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Revalidates a concurrent snapshot before another optimistic write.</summary>
    /// <param name="dictionary">The explicitly null dictionary.</param>
    [Theory]
    [InlineData("Entries")]
    [InlineData("LastIndexedEvents")]
    [InlineData("LastSequenceNumbers")]
    [InlineData("ErasureSequenceNumbers")]
    public async Task NullIndexDictionaryOnOptimisticRetryCannotProduceAnotherWriteAsync(string dictionary)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexSdkReadModel valid = CreateIndex("private-unrelated");
        string key = PartySdkReadModelAddresses.Index("tenant-a");
        store.GetAsync<PartyIndexSdkReadModel>("statestore", key, Arg.Any<CancellationToken>()).Returns(
            new ReadModelEntry<PartyIndexSdkReadModel>(valid, "before"),
            new ReadModelEntry<PartyIndexSdkReadModel>(valid, "before"),
            new ReadModelEntry<PartyIndexSdkReadModel>(NullIndexDictionary(valid, dictionary), "corrupt"));
        store.TrySaveAsync("statestore", key, Arg.Any<PartyIndexSdkReadModel>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();

        DomainProjectionHandlerResult result = await new PartyIndexSdkProjectionHandler(store, s_options, search)
            .ProjectAsync(CreateRequest(), "dispatch", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("projection-rebuild-required");
        await store.Received(1).TrySaveAsync("statestore", key, Arg.Any<PartyIndexSdkReadModel>(), "before", Arg.Any<CancellationToken>());
        search.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Rejects explicit null dictionaries in legacy and wrapped rebuild candidates.</summary>
    /// <param name="dictionary">The explicitly null dictionary.</param>
    [Theory]
    [InlineData("Entries")]
    [InlineData("LastIndexedEvents")]
    [InlineData("LastSequenceNumbers")]
    [InlineData("ErasureSequenceNumbers")]
    public async Task NullIndexDictionaryCannotBeAcceptedAsAStagedCandidateAsync(string dictionary)
    {
        var store = new InMemoryReadModelBatchStore();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        PartyIndexSdkReadModel corrupt = NullIndexDictionary(CreateIndex("private-unrelated"), dictionary);
        foreach (bool wrapped in new[] { false, true })
        {
            byte[] bytes = wrapped
                ? JsonSerializer.SerializeToUtf8Bytes(new PartyIndexRebuildCandidate(corrupt, new PartyProjectionRebuildDiagnostics()), s_explicitNullJsonOptions)
                : JsonSerializer.SerializeToUtf8Bytes(corrupt, s_explicitNullJsonOptions);
            var candidate = new DomainSharedProjectionRebuildCandidate(bytes);
            foreach (bool empty in new[] { false, true })
            {
                InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => handler.AccumulateAsync(
                    identity, candidate, empty ? CreateRequest() with { Events = [] } : CreateRequest(), TestContext.Current.CancellationToken));
                failure.Message.ShouldBe("The shared Party index rebuild candidate is empty or malformed.");
            }

            InvalidOperationException finalize = await Should.ThrowAsync<InvalidOperationException>(() =>
                handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken));
            finalize.Message.ShouldBe("The shared Party index rebuild candidate is empty or malformed.");
            candidate.State.ToArray().ShouldBe(bytes);
        }

        (await store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), TestContext.Current.CancellationToken)).Value.ShouldBeNull();
    }

    /// <summary>Repairs corrupted canonical state from full history while preserving erasure fences.</summary>
    /// <param name="dictionary">The explicitly null dictionary.</param>
    [Theory]
    [InlineData("Entries")]
    [InlineData("LastIndexedEvents")]
    [InlineData("LastSequenceNumbers")]
    public async Task ValidFullRebuildReplacesCorruptIndexAndRetainsErasureFencesAsync(string dictionary)
    {
        var store = new InMemoryReadModelBatchStore(writeOptions: s_explicitNullJsonOptions);
        string key = PartySdkReadModelAddresses.Index("tenant-a");
        await store.SaveAsync("statestore", key, NullIndexDictionary(CreateIndex("private-obsolete"), dictionary), TestContext.Current.CancellationToken);
        string? etag = (await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, TestContext.Current.CancellationToken)).ETag;
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(true);
        search.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        ProjectionRequest history = CreateRequest();
        history = history with { Events = [history.Events[0], CreateErasureRequest().Events[0]] };
        candidate = await handler.AccumulateAsync(identity, candidate, history, TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, CreateRequest() with { AggregateId = "party-2" }, TestContext.Current.CancellationToken);

        DomainProjectionRebuildPlan plan = await CommitAsync(store, handler, identity, candidate);

        plan.Operations.Single().Concurrency.ShouldBe(ReadModelBatchConcurrency.Match(etag!));
        PartyIndexSdkReadModel persisted = store.Deserialize<PartyIndexSdkReadModel>(key);
        persisted.Entries.Keys.ShouldBe(["party-2"]);
        persisted.LastIndexedEvents.Keys.ShouldBe(["party-2"]);
        persisted.SearchContentHashes.ShouldNotBeNull().Keys.ShouldBe(["party-2"]);
        persisted.LastSequenceNumbers["party-1"].ShouldBe(2);
        persisted.ErasureSequenceNumbers["party-1"].ShouldBe(2);
        (await handler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await search.Received(1).NotifyIndexedAsync("tenant-a", Arg.Is<PartyIndexEntry>(entry => entry.Id == "party-2"), "PartyProjectionRebuilt", Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>());
        await search.DidNotReceive().NotifyRemovedAsync("tenant-a", "party-1", Arg.Any<CancellationToken>());
        await search.Received(dictionary == "Entries" ? 0 : 1).NotifyRemovedAsync("tenant-a", "private-obsolete", Arg.Any<CancellationToken>());
        (await handler.ProjectAsync(CreateRequest(), "delayed", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        store.Deserialize<PartyIndexSdkReadModel>(key).Entries.Keys.ShouldBe(["party-2"]);
    }

    /// <summary>Preserves every coordinated slot when the shared index is malformed.</summary>
    /// <param name="dictionary">The explicitly null dictionary.</param>
    [Theory]
    [InlineData("Entries")]
    [InlineData("LastIndexedEvents")]
    [InlineData("LastSequenceNumbers")]
    [InlineData("ErasureSequenceNumbers")]
    public async Task EraserRejectsNullIndexDictionaryBeforeAnyCoordinatedWriteAsync(string dictionary)
    {
        var store = new InMemoryReadModelBatchStore(writeOptions: s_explicitNullJsonOptions);
        string detailKey = PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
        string indexKey = PartySdkReadModelAddresses.Index("tenant-a");
        PartyDetailSdkReadModel detail = PartyDetailSdkProjectionHandler.Fold(CreateRequest(), null);
        await store.SaveAsync("statestore", detailKey, detail, TestContext.Current.CancellationToken);
        string processingKey = PartySdkReadModelAddresses.Processing("tenant-a", "party-1");
        PartyProcessingSdkReadModel processing = PartyProcessingActivityFold.Fold(CreateRequest(), null);
        await store.SaveAsync("statestore", processingKey, processing, TestContext.Current.CancellationToken);
        PartyIndexSdkReadModel corrupt = NullIndexDictionary(CreateIndex("party-1", "private-unrelated"), dictionary);
        await store.SaveAsync("statestore", indexKey, corrupt, TestContext.Current.CancellationToken);
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            new PartySdkReadModelEraser(store, store, s_options, search).EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken));

        failure.Message.ShouldBe("projection-rebuild-required");
        JsonSerializer.Serialize(store.Deserialize<PartyDetailSdkReadModel>(detailKey), PartiesJsonOptions.Default)
            .ShouldBe(JsonSerializer.Serialize(detail, PartiesJsonOptions.Default));
        JsonSerializer.Serialize(store.Deserialize<PartyIndexSdkReadModel>(indexKey), PartiesJsonOptions.Default)
            .ShouldBe(JsonSerializer.Serialize(corrupt, PartiesJsonOptions.Default));
        JsonSerializer.Serialize(store.Deserialize<PartyProcessingSdkReadModel>(processingKey), PartiesJsonOptions.Default)
            .ShouldBe(JsonSerializer.Serialize(processing, PartiesJsonOptions.Default));
        search.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Preserves initialization defaults for historical rows with omitted dictionaries.</summary>
    [Fact]
    public async Task OmittedLegacyIndexDictionariesRetainDefaultsAndAcceptLiveDeliveryAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        PartyIndexSdkReadModel legacy = JsonSerializer.Deserialize<PartyIndexSdkReadModel>("{}", PartiesJsonOptions.Default)!;
        await store.SaveAsync("statestore", PartySdkReadModelAddresses.Index("tenant-a"), legacy, TestContext.Current.CancellationToken);

        (await new PartyIndexSdkProjectionHandler(store, s_options).ProjectAsync(CreateRequest(), "legacy", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        PartyIndexSdkReadModel persisted = store.Deserialize<PartyIndexSdkReadModel>(PartySdkReadModelAddresses.Index("tenant-a"));
        persisted.Entries.Keys.ShouldBe(["party-1"]);
        persisted.LastIndexedEvents["party-1"].SequenceNumber.ShouldBe(2);
        persisted.LastSequenceNumbers["party-1"].ShouldBe(2);
        persisted.ErasureSequenceNumbers.ShouldBeEmpty();
    }

    /// <summary>Preserves cleanup-created fences even without a PartyErased source event.</summary>
    /// <param name="dictionary">The corrupt dictionary, or none for an intact row.</param>
    [Theory]
    [InlineData("Entries")]
    [InlineData("LastIndexedEvents")]
    [InlineData("LastSequenceNumbers")]
    [InlineData("none")]
    public async Task FullRebuildRetainsCleanupOnlyErasureEvidenceAsync(string dictionary)
    {
        var store = new InMemoryReadModelBatchStore(writeOptions: s_explicitNullJsonOptions);
        DateTimeOffset cleanupAt = DateTimeOffset.UnixEpoch.AddDays(1);
        PartyIndexSdkReadModel cleaned = PartySdkReadModelEraser.RemoveParty(CreateIndex("party-1", "party-2"), "party-1", cleanupAt);
        cleaned = PartySdkReadModelEraser.RemoveParty(cleaned, "cleanup-only", cleanupAt);
        await store.SaveAsync("statestore", PartySdkReadModelAddresses.Index("tenant-a"),
            dictionary == "none" ? cleaned : NullIndexDictionary(cleaned, dictionary), TestContext.Current.CancellationToken);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, CreateRequest(), TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, CreateRequest() with { AggregateId = "party-2" }, TestContext.Current.CancellationToken);

        _ = await CommitAsync(store, handler, identity, candidate);

        PartyIndexSdkReadModel persisted = store.Deserialize<PartyIndexSdkReadModel>(PartySdkReadModelAddresses.Index("tenant-a"));
        persisted.Entries.Keys.ShouldBe(["party-2"]);
        persisted.LastIndexedEvents.Keys.ShouldBe(["party-2"]);
        persisted.SearchContentHashes.ShouldNotBeNull().Keys.ShouldBe(["party-2"]);
        persisted.ErasureSequenceNumbers["party-1"].ShouldBe(1);
        persisted.ErasureSequenceNumbers["cleanup-only"].ShouldBe(long.MinValue);
        persisted.LastSequenceNumbers["party-1"].ShouldBe(2);
        persisted.ProjectedAt.ShouldBe(cleanupAt);
        (await handler.ProjectAsync(CreateRequest(), "delayed", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        store.Deserialize<PartyIndexSdkReadModel>(PartySdkReadModelAddresses.Index("tenant-a")).Entries.Keys.ShouldBe(["party-2"]);
    }

    /// <summary>Does not infer eventless cleanup fences when the canonical fence dictionary was lost.</summary>
    [Fact]
    public async Task FullRebuildRefusesUnknownCanonicalErasureEvidenceAsync()
    {
        var store = new InMemoryReadModelBatchStore(writeOptions: s_explicitNullJsonOptions);
        string key = PartySdkReadModelAddresses.Index("tenant-a");
        PartyIndexSdkReadModel corrupt = CreateIndex("party-1") with { ErasureSequenceNumbers = null! };
        await store.SaveAsync("statestore", key, corrupt, TestContext.Current.CancellationToken);
        string? etag = (await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, TestContext.Current.CancellationToken)).ETag;
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, CreateRequest(), TestContext.Current.CancellationToken);

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken));

        failure.Message.ShouldBe("projection-rebuild-required");
        (await store.GetAsync<PartyIndexSdkReadModel>("statestore", key, TestContext.Current.CancellationToken)).ETag.ShouldBe(etag);
        store.Deserialize<PartyIndexSdkReadModel>(key).ErasureSequenceNumbers.ShouldBeNull();
    }

    /// <summary>Stops erasure before a second batch when a conflicting writer exposes corruption.</summary>
    /// <param name="dictionary">The dictionary corrupted by the concurrent writer.</param>
    [Theory]
    [InlineData("Entries")]
    [InlineData("LastIndexedEvents")]
    [InlineData("LastSequenceNumbers")]
    [InlineData("ErasureSequenceNumbers")]
    public async Task EraserRejectsCorruptReloadAfterOptimisticConflictAsync(string dictionary)
    {
        var store = new InMemoryReadModelBatchStore(writeOptions: s_explicitNullJsonOptions);
        string detailKey = PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
        string processingKey = PartySdkReadModelAddresses.Processing("tenant-a", "party-1");
        string indexKey = PartySdkReadModelAddresses.Index("tenant-a");
        PartyDetailSdkReadModel detail = PartyDetailSdkProjectionHandler.Fold(CreateRequest(), null);
        PartyProcessingSdkReadModel processing = PartyProcessingActivityFold.Fold(CreateRequest(), null);
        PartyIndexSdkReadModel index = PartySdkReadModelEraser.RemoveParty(CreateIndex("party-1", "private-unrelated"), "already-erased");
        await store.SaveAsync("statestore", detailKey, detail, TestContext.Current.CancellationToken);
        await store.SaveAsync("statestore", processingKey, processing, TestContext.Current.CancellationToken);
        await store.SaveAsync("statestore", indexKey, index, TestContext.Current.CancellationToken);
        PartyIndexSdkReadModel corrupt = NullIndexDictionary(index, dictionary);
        IReadModelBatchStore batches = Substitute.For<IReadModelBatchStore>();
        batches.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await store.SaveAsync("statestore", indexKey, corrupt, TestContext.Current.CancellationToken).ConfigureAwait(false);
            return ReadModelBatchResult.OptimisticConflict("conflict", "transaction-precondition");
        });

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            new PartySdkReadModelEraser(store, batches, s_options).EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken));

        failure.Message.ShouldBe("projection-rebuild-required");
        await batches.Received(1).ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
        JsonSerializer.Serialize(store.Deserialize<PartyDetailSdkReadModel>(detailKey), PartiesJsonOptions.Default).ShouldBe(JsonSerializer.Serialize(detail, PartiesJsonOptions.Default));
        JsonSerializer.Serialize(store.Deserialize<PartyProcessingSdkReadModel>(processingKey), PartiesJsonOptions.Default).ShouldBe(JsonSerializer.Serialize(processing, PartiesJsonOptions.Default));
        JsonSerializer.Serialize(store.Deserialize<PartyIndexSdkReadModel>(indexKey), PartiesJsonOptions.Default).ShouldBe(JsonSerializer.Serialize(corrupt, PartiesJsonOptions.Default));
    }

    private static PartyIndexSdkReadModel NullIndexDictionary(PartyIndexSdkReadModel model, string dictionary)
        => dictionary switch
        {
            "Entries" => model with { Entries = null! },
            "LastIndexedEvents" => model with { LastIndexedEvents = null! },
            "LastSequenceNumbers" => model with { LastSequenceNumbers = null! },
            "ErasureSequenceNumbers" => model with { ErasureSequenceNumbers = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(dictionary)),
        };

    private static ILogger<PartyIndexSdkProjectionHandler> CreateLogger()
    {
        ILogger<PartyIndexSdkProjectionHandler> logger = Substitute.For<ILogger<PartyIndexSdkProjectionHandler>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        logger.ClearReceivedCalls();
        return logger;
    }

    private static Dictionary<string, object?> Summary(ILogger<PartyIndexSdkProjectionHandler> logger, LogLevel level, bool completed)
    {
        object?[] args = logger.ReceivedCalls().Where(static call => call.GetMethodInfo().Name == "Log")
            .ShouldHaveSingleItem().GetArguments();
        args[0].ShouldBe(level);
        ((EventId)args[1]!).Id.ShouldBe(4801);
        args[3].ShouldBeNull();
        Dictionary<string, object?> state = ((IReadOnlyList<KeyValuePair<string, object?>>)args[2]!)
            .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
        state.Keys.Order(StringComparer.Ordinal).ShouldBe(new[]
        {
            "Completed", "NonJson", "UnknownType", "AmbiguousType", "CorruptPayload", "RedactedPayload",
            "WholePayloadRedacted", "NullPayload", "{OriginalFormat}",
        }.Order(StringComparer.Ordinal));
        state["Completed"].ShouldBe(completed);
        foreach (object? value in state.Values)
        {
            if (value is string text)
            {
                text.ShouldNotContain("private-");
                text.ShouldNotContain("tenant-secret");
            }
            else
            {
                (value is bool or long).ShouldBeTrue();
            }
        }

        return state;
    }

    private static DomainSharedProjectionRebuildIdentity Identity(string operation = "private-operation")
        => new("tenant-secret", "party", PartyProjectionNames.Index, operation, "private-catalog");

    private static ProjectionRequest History(string party, int corrupt = 0, int redacted = 0, int whole = 0)
    {
        var events = new List<ProjectionEventDto>
        {
            new(nameof(PartyCreated), JsonSerializer.SerializeToUtf8Bytes(new PartyCreated { Type = PartyType.Person }, PartiesJsonOptions.Default),
                "json", 1, DateTimeOffset.UnixEpoch, "private-correlation"),
        };
        foreach ((int count, string format, string type, byte[] payload) in new[]
        {
            (corrupt, "json", nameof(PartyCreated), "{private-payload"u8.ToArray()),
            (redacted, "json-redacted", nameof(PartyCreated), "{private-payload"u8.ToArray()),
            (whole, "json-redacted", "private-whole-event-type", "{}"u8.ToArray()),
        })
        {
            for (int index = 0; index < count; index++)
            {
                events.Add(new ProjectionEventDto(type, payload, format, events.Count + 1,
                    DateTimeOffset.UnixEpoch.AddSeconds(events.Count), "private-correlation"));
            }
        }

        // Equivalent duplicates within a history must also be counted only once.
        return new ProjectionRequest("tenant-secret", "party", party, [.. events, events[^1]]);
    }

    private static async Task<DomainProjectionRebuildPlan> CommitAsync(
        InMemoryReadModelBatchStore store,
        PartyIndexSdkProjectionHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate)
    {
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var scope = new ReadModelBatchScope(plan.StoreName, identity.TenantId, "party", PartySdkReadModelAddresses.SharedIndexAggregateId,
            PartyProjectionNames.Index, identity.OperationId);
        (await store.ExecuteAsync(new ReadModelBatch(scope, plan.Operations), TestContext.Current.CancellationToken).ConfigureAwait(false))
            .Status.ShouldBe(ReadModelBatchStatus.Completed);
        return plan;
    }

    [Fact]
    public async Task DetailHandler_WritesCanonicalBatchAndMatchesRetainedFoldAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        ReadModelBatch? captured = null;
        batchStore.ExecuteAsync(Arg.Do<ReadModelBatch>(value => captured = value), Arg.Any<CancellationToken>())
            .Returns(ReadModelBatchResult.Completed("fingerprint"));
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
        ProjectionRequest request = CreateRequest();

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request,
            "dispatch-1",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        captured.ShouldNotBeNull();
        captured.Scope.ShouldBe(new ReadModelBatchScope(
            "statestore", "tenant-a", "party", "party-1", PartyProjectionNames.Detail, "dispatch-1"));
        captured.RetainManifestForRecovery.ShouldBeFalse();
        captured.Operations.Count.ShouldBe(2);
        ReadModelBatchOperation detailOperation = captured.Operations.Single(static operation =>
            operation.Key.EndsWith(":detail", StringComparison.Ordinal));
        detailOperation.Key.ShouldBe("readmodel:tenant-a:party:party-detail:party-1:detail");
        PartyDetailSdkReadModel persisted = JsonSerializer.Deserialize<PartyDetailSdkReadModel>(
            detailOperation.CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        PartyDetail? retained = PartyDetailProjectionHandler.Apply(
            "party-1",
            new PartyCreated { Type = PartyType.Person },
            null);
        retained = PartyDetailProjectionHandler.Apply("party-1", new PartyDeactivated(), retained) ?? retained;
        persisted.Detail.ShouldNotBeNull();
        persisted.Detail.Id.ShouldBe(retained!.Id);
        persisted.Detail.Type.ShouldBe(retained.Type);
        persisted.Detail.IsActive.ShouldBe(retained.IsActive);
        persisted.Detail.CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        persisted.Detail.LastModifiedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(1));
        persisted.Detail.NameHistory.ShouldHaveSingleItem().ChangedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        persisted.LastSequenceNumber.ShouldBe(2);
        persisted.ProjectedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(1));
        ReadModelBatchOperation processingOperation = captured.Operations.Single(static operation =>
            operation.Key.EndsWith(":processing-records", StringComparison.Ordinal));
        processingOperation.Key.ShouldBe("readmodel:tenant-a:party:party-detail:party-1:processing-records");
        PartyProcessingSdkReadModel processing = JsonSerializer.Deserialize<PartyProcessingSdkReadModel>(
            processingOperation.CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        processing.Records.Select(static record => record.SequenceNumber).ShouldBe([1, 2]);
    }

    [Fact]
    public async Task DetailRebuildPlan_MatchesNormalReplayAfterTimestampNormalizationAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
        ProjectionRequest request = CreateRequest();

        DomainProjectionRebuildPlan rebuild = await handler.PrepareRebuildAsync(
            request,
            "rebuild-1",
            TestContext.Current.CancellationToken);
        PartyDetailSdkReadModel candidate = JsonSerializer.Deserialize<PartyDetailSdkReadModel>(
            rebuild.Operations.Single(static operation => operation.Key.EndsWith(":detail", StringComparison.Ordinal)).CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        PartyDetailSdkReadModel replay = PartyDetailSdkProjectionHandler.Fold(request, current: null);

        Normalize(candidate).ShouldBe(Normalize(replay));
        rebuild.StoreName.ShouldBe("statestore");
        rebuild.Operations.Count.ShouldBe(2);
        PartyProcessingSdkReadModel processingCandidate = JsonSerializer.Deserialize<PartyProcessingSdkReadModel>(
            rebuild.Operations.Single(static operation => operation.Key.EndsWith(":processing-records", StringComparison.Ordinal)).CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        processingCandidate.Records.Select(static record => record.SequenceNumber).ShouldBe([1, 2]);
        processingCandidate.LastSequenceNumber.ShouldBe(2);
        rebuild.Operations.ShouldAllBe(static operation =>
            operation.Concurrency == ReadModelBatchConcurrency.CreateOnly);
        handler.RebuildSemantics.ShouldBe(DomainProjectionRebuildSemantics.FullReplay);
    }

    [Fact]
    public async Task DetailRebuildPlan_NormalizesLegacyProcessingCategoryAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        PartyProcessingSdkReadModel legacy = CreateLegacyProcessingReadModel();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(legacy, "legacy-etag"));
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                Event(new PartyNotFound(), 1, DateTimeOffset.UnixEpoch) with
                {
                    UserId = "actor-1",
                },
            ],
        };
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);

        DomainProjectionRebuildPlan plan = await handler.PrepareRebuildAsync(
            request,
            "rebuild-legacy-category",
            TestContext.Current.CancellationToken);

        ReadModelBatchOperation operation = plan.Operations.Single(static value =>
            value.Key.EndsWith(":processing-records", StringComparison.Ordinal));
        PartyProcessingSdkReadModel candidate = JsonSerializer.Deserialize<PartyProcessingSdkReadModel>(
            operation.CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        candidate.Records.ShouldHaveSingleItem().ShouldBe(legacy.Records[0] with { OperationCategory = "Other" });
        candidate.LastSequenceNumber.ShouldBe(1);
        operation.Concurrency.ShouldBe(ReadModelBatchConcurrency.Match("legacy-etag"));
        legacy.Records[0].OperationCategory.ShouldBe(nameof(PartyNotFound));
    }

    [Fact]
    public async Task DetailRebuildPlan_ExistingSlotsRequireSnapshotEtagsAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel(), "detail-etag"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel(), "processing-etag"));
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);

        DomainProjectionRebuildPlan rebuild = await handler.PrepareRebuildAsync(
            CreateRequest(),
            "rebuild-etags",
            TestContext.Current.CancellationToken);

        rebuild.Operations.Single(static operation => operation.Key.EndsWith(":detail", StringComparison.Ordinal))
            .Concurrency.ShouldBe(ReadModelBatchConcurrency.Match("detail-etag"));
        rebuild.Operations.Single(static operation => operation.Key.EndsWith(":processing-records", StringComparison.Ordinal))
            .Concurrency.ShouldBe(ReadModelBatchConcurrency.Match("processing-etag"));
    }

    [Fact]
    public async Task DetailRebuildPlan_UnresolvedEventFailsWithoutProducingCandidateAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
        ProjectionRequest unresolved = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.PrepareRebuildAsync(
                unresolved,
                "rebuild-unresolved",
                TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("unresolved-or-unsupported-event");
    }

    [Fact]
    public async Task IndexHandler_ErasureRemovesOnlyTargetAndPreservesUnrelatedEntryAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexSdkReadModel existing = CreateIndex("party-1", "party-2");
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(existing, "etag-1"));
        PartyIndexSdkReadModel? persisted = null;
        store.TrySaveAsync(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Do<PartyIndexSdkReadModel>(value => persisted = value),
                "etag-1",
                Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateErasureRequest(),
            "dispatch-erase",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        persisted.ShouldNotBeNull();
        persisted.Entries.ContainsKey("party-1").ShouldBeFalse();
        persisted.Entries["party-2"].DisplayName.ShouldBe("Party party-2");

        persisted.LastSequenceNumbers["party-1"].ShouldBe(2);
        persisted.ErasureSequenceNumbers["party-1"].ShouldBe(2);
        persisted.LastSequenceNumbers["party-2"].ShouldBe(1);
    }

    [Fact]
    public async Task IndexHandler_LaterSameIdCreateAfterErasureCannotRestoreEntryAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexSdkReadModel erased = PartyIndexSdkProjectionHandler.Fold(CreateErasureRequest(), current: CreateIndex("party-1"));
        erased.LastSequenceNumbers["party-1"].ShouldBe(2);
        erased.ErasureSequenceNumbers["party-1"].ShouldBe(2);

        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(erased, "etag-2"));
        PartyIndexSdkReadModel? persisted = null;
        store.TrySaveAsync(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Do<PartyIndexSdkReadModel>(value => persisted = value),
                "etag-2",
                Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);

        ProjectionRequest recreateRequest = new(
            "tenant-a",
            "party",
            "party-1",
            [Event(new PartyCreated { Type = PartyType.Person }, 3, DateTimeOffset.UnixEpoch.AddMinutes(10))]);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            recreateRequest,
            "dispatch-recreate",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        persisted.ShouldNotBeNull();
        persisted.Entries.ContainsKey("party-1").ShouldBeFalse();
        persisted.LastSequenceNumbers["party-1"].ShouldBe(3);
        persisted.ErasureSequenceNumbers["party-1"].ShouldBe(2);
    }

    [Fact]
    public async Task IndexHandler_NotifiesSearchIndexerWithLatestEntryAndEventAfterSuccessfulWriteAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        searchIndexer.NotifyIndexedAsync(
                Arg.Any<string>(),
                Arg.Any<PartyIndexEntry>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<long?>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateRequest(),
            "dispatch-1",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await searchIndexer.Received(1).NotifyIndexedAsync(
            "tenant-a",
            Arg.Is<PartyIndexEntry>(entry => entry != null && entry.Id == "party-1"),
            "PartyDeactivated",
            DateTimeOffset.UnixEpoch.AddSeconds(1),
            2L,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_ErasureNotifiesSearchIndexerRemovedAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        await store.SaveAsync("statestore", PartySdkReadModelAddresses.Index("tenant-a"), CreateIndex("party-1"), TestContext.Current.CancellationToken);
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        searchIndexer.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateErasureRequest(),
            "dispatch-erase",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await searchIndexer.DidNotReceive().NotifyIndexedAsync(
            Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>());
        await searchIndexer.Received(1).NotifyRemovedAsync(
            "tenant-a",
            "party-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_ErasureNotifiesRemovalWhenCanonicalEntryWasAlreadyAbsentAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        await store.SaveAsync("statestore", PartySdkReadModelAddresses.Index("tenant-a"), new PartyIndexSdkReadModel
        {
            LastSequenceNumbers = new Dictionary<string, long>(StringComparer.Ordinal) { ["party-1"] = 1 },
        }, TestContext.Current.CancellationToken);
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        searchIndexer.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);
        ProjectionRequest request = CreateErasureRequest();

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request,
            "dispatch-erase-absent",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await searchIndexer.Received(1).NotifyRemovedAsync("tenant-a", "party-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_SearchIndexerThrowReturnsRetryableAfterCanonicalWriteAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        searchIndexer
            .NotifyIndexedAsync(
                Arg.Any<string>(),
                Arg.Any<PartyIndexEntry>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<long?>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("memories down"));
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateRequest(),
            "dispatch-1",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("search-reconciliation-required");
    }

    [Fact]
    public async Task IndexHandler_RetryAfterSearchFailureReconcilesIdempotentCanonicalWriteAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        string indexKey = PartySdkReadModelAddresses.Index("tenant-a");
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        searchIndexer.NotifyIndexedAsync(
                Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(false, true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);
        ProjectionRequest request = CreateRequest() with
        {
            Events = [.. CreateRequest().Events, Event(new PartyNotFound(), 3, DateTimeOffset.UnixEpoch.AddSeconds(2))],
        };

        DomainProjectionHandlerResult first = await handler.ProjectAsync(
            request,
            "dispatch-search-retry",
            TestContext.Current.CancellationToken);
        first.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        ReadModelEntry<PartyIndexSdkReadModel> persisted = await store.GetAsync<PartyIndexSdkReadModel>(
            "statestore", indexKey, TestContext.Current.CancellationToken);
        persisted.Value.ShouldNotBeNull();
        persisted.Value.LastSequenceNumbers[request.AggregateId].ShouldBe(3);
        handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);

        DomainProjectionHandlerResult retry = await handler.ProjectAsync(
            request,
            "dispatch-search-retry",
            TestContext.Current.CancellationToken);

        retry.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, TestContext.Current.CancellationToken))
            .ETag.ShouldBe(persisted.ETag);
        await searchIndexer.Received(2).NotifyIndexedAsync(
            "tenant-a",
            Arg.Is<PartyIndexEntry>(entry => entry != null && entry.Id == "party-1"),
            "PartyDeactivated",
            DateTimeOffset.UnixEpoch.AddSeconds(1),
            2L,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_EmptyEventsCompletesWithoutWriteAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateRequest() with { Events = [] },
            "dispatch-empty",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await store.DidNotReceive().GetAsync<PartyIndexSdkReadModel>(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("rejection", false)]
    [InlineData("rejection", true)]
    [InlineData("deactivation", false)]
    [InlineData("deactivation", true)]
    [InlineData("missing-channel", false)]
    [InlineData("missing-channel", true)]
    [InlineData("missing-identifier", false)]
    [InlineData("missing-identifier", true)]
    [InlineData("display-name", false)]
    [InlineData("display-name", true)]
    [InlineData("same-reference", false)]
    [InlineData("same-reference", true)]
    public async Task IndexHandler_SerializedRetryRetainsCanonicalSelectionAcrossNoOpsAsync(string scenario, bool incremental)
    {
        var store = new InMemoryReadModelBatchStore();
        ProjectionRequest history = CreateRequest();
        if (incremental)
        {
            var seedHandler = new PartyIndexSdkProjectionHandler(store, s_options);
            (await seedHandler.ProjectAsync(history with { Events = [history.Events[0]] },
                "seed", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }

        object tail = scenario switch
        {
            "rejection" => new PartyNotFound(),
            "deactivation" => new PartyDeactivated(),
            "missing-channel" => new ContactChannelUpdated { ContactChannelId = "absent" },
            "missing-identifier" => new IdentifierRemoved { IdentifierId = "absent" },
            "display-name" => new PartyDisplayNameDerived { DisplayName = string.Empty },
            "same-reference" => new PartyCreated { Type = PartyType.Organization },
            _ => throw new ArgumentException("Unknown test scenario.", nameof(scenario)),
        };
        // No-op events share the mutation's timestamp; the same-reference application uses a
        // later timestamp while retaining the entry's LastModifiedAt.
        ProjectionEventDto mutation = Event(new PartyDeactivated(), 2, DateTimeOffset.UnixEpoch);
        ProjectionEventDto finalEvent = Event(tail, 3,
            scenario == "same-reference" ? DateTimeOffset.UnixEpoch.AddMinutes(1) : DateTimeOffset.UnixEpoch);
        bool completeDelivery = !incremental || scenario is "missing-channel" or "missing-identifier";
        ProjectionRequest request = history with
        {
            // Even a no-op collection mutation needs the complete prior collections.
            Events = completeDelivery ? [history.Events[0], mutation, finalEvent] : [mutation, finalEvent],
        };
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(false, true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);

        (await handler.ProjectAsync(request, "first", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        string indexKey = PartySdkReadModelAddresses.Index(request.TenantId);
        ReadModelEntry<PartyIndexSdkReadModel> persisted = await store.GetAsync<PartyIndexSdkReadModel>(
            "statestore", indexKey, TestContext.Current.CancellationToken);
        persisted.Value.ShouldNotBeNull();
        bool sameReference = scenario == "same-reference";
        string expectedType = sameReference ? nameof(PartyCreated) : nameof(PartyDeactivated);
        DateTimeOffset expectedTimestamp = sameReference ? finalEvent.Timestamp : mutation.Timestamp;
        persisted.Value.LastIndexedEvents.ShouldHaveSingleItem().Value.ShouldBe(new PartyIndexEventMetadata(
            sameReference ? 3 : 2, expectedType, expectedTimestamp));
        persisted.Value.LastSequenceNumbers[request.AggregateId].ShouldBe(3);
        persisted.Value.Entries[request.AggregateId].IsActive.ShouldBeFalse();
        persisted.Value.Entries[request.AggregateId].Type.ShouldBe(PartyType.Person);
        persisted.Value.Entries[request.AggregateId].LastModifiedAt.ShouldBe(DateTimeOffset.UnixEpoch);

        var recreatedHandler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        if (incremental && !completeDelivery)
        {
            (await recreatedHandler.ProjectAsync(request with { Events = [history.Events[0], mutation, finalEvent] }, "complete-first", TestContext.Current.CancellationToken))
                .Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        }

        (await recreatedHandler.ProjectAsync(request with { Events = [history.Events[0], mutation, finalEvent] }, "retry", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);

        (await store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, TestContext.Current.CancellationToken))
            .ETag.ShouldBe(persisted.ETag);
        await search.Received(2).NotifyIndexedAsync("tenant-a",
            Arg.Is<PartyIndexEntry>(entry => entry.Id == request.AggregateId && !entry.IsActive),
            expectedType, expectedTimestamp, Arg.Any<long?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_CheckpointOnlyAndOlderRetryWaitForCompleteCanonicalReplayAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        ProjectionRequest original = CreateRequest();
        var seedHandler = new PartyIndexSdkProjectionHandler(store, s_options);
        (await seedHandler.ProjectAsync(original, "seed", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        ProjectionEventDto reactivation = Event(new PartyReactivated(), 3,
            DateTimeOffset.UnixEpoch.AddSeconds(5).ToOffset(TimeSpan.FromHours(2)));
        (await seedHandler.ProjectAsync(original with { Events = [reactivation] }, "newer",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);

        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(false, true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        ProjectionRequest checkpointOnly = original with
        {
            Events =
            [
                Event(new PartyNotFound(), 4, DateTimeOffset.UnixEpoch.AddSeconds(6)),
                Event(new PartyReactivated(), 5, DateTimeOffset.UnixEpoch.AddSeconds(7)),
            ],
        };
        (await handler.ProjectAsync(checkpointOnly, "checkpoint", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        string indexKey = PartySdkReadModelAddresses.Index(original.TenantId);
        ReadModelEntry<PartyIndexSdkReadModel> persisted = await store.GetAsync<PartyIndexSdkReadModel>(
            "statestore", indexKey, TestContext.Current.CancellationToken);
        persisted.Value.ShouldNotBeNull();
        persisted.Value.LastIndexedEvents[original.AggregateId].ShouldBe(
            new PartyIndexEventMetadata(3, nameof(PartyReactivated), reactivation.Timestamp));
        persisted.Value.LastIndexedEvents[original.AggregateId].Timestamp.Offset.ShouldBe(TimeSpan.FromHours(2));
        persisted.Value.LastSequenceNumbers[original.AggregateId].ShouldBe(5);

        handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        (await handler.ProjectAsync(original, "older", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        (await store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, TestContext.Current.CancellationToken))
            .ETag.ShouldBe(persisted.ETag);
        await search.DidNotReceiveWithAnyArgs().NotifyIndexedAsync(default!, default!, default!, default, default, default);
        ProjectionRequest complete = original with { Events = [.. original.Events, reactivation, .. checkpointOnly.Events] };
        (await handler.ProjectAsync(complete, "complete-first", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        (await handler.ProjectAsync(complete, "complete-retry", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        await search.Received(2).NotifyIndexedAsync("tenant-a",
            Arg.Is<PartyIndexEntry>(entry => entry.IsActive), nameof(PartyReactivated), reactivation.Timestamp,
            Arg.Any<long?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_HistoricalSerializedRowUsesSyntheticProvenanceWithoutRewriteAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        ProjectionRequest request = CreateRequest();
        string indexKey = PartySdkReadModelAddresses.Index(request.TenantId);
        PartyIndexSdkReadModel legacy = PartyIndexSdkProjectionHandler.Fold(request, null);
        // Write the historical JSON shape, with no descriptor property at all.
        await store.SaveAsync("statestore", indexKey, new
        {
            legacy.Entries,
            legacy.LastSequenceNumbers,
            legacy.ErasureSequenceNumbers,
            legacy.ProjectedAt,
            legacy.ProjectionVersion,
        }, TestContext.Current.CancellationToken);
        ReadModelEntry<PartyIndexSdkReadModel> before = await store.GetAsync<PartyIndexSdkReadModel>(
            "statestore", indexKey, TestContext.Current.CancellationToken);
        before.Value.ShouldNotBeNull();
        before.Value.LastIndexedEvents.ShouldBeEmpty();
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(false, true);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
            (await handler.ProjectAsync(request, "historical", TestContext.Current.CancellationToken)).Status
                .ShouldBe(attempt == 0 ? ProjectionDispatchStatus.Retryable : ProjectionDispatchStatus.AlreadyCompleted);
        }

        ReadModelEntry<PartyIndexSdkReadModel> after = await store.GetAsync<PartyIndexSdkReadModel>(
            "statestore", indexKey, TestContext.Current.CancellationToken);
        after.ETag.ShouldNotBe(before.ETag);
        after.Value.ShouldNotBeNull().SearchContentHashes.ShouldNotBeNull().ContainsKey(request.AggregateId).ShouldBeTrue();
        after.Value.LastSequenceNumbers.ShouldBe(before.Value.LastSequenceNumbers);
        after.Value.LastIndexedEvents.ShouldBeEmpty();
        await search.Received(2).NotifyIndexedAsync("tenant-a", Arg.Any<PartyIndexEntry>(),
            "PartyProjectionReconciled", legacy.Entries[request.AggregateId].LastModifiedAt, Arg.Any<long?>(), Arg.Any<CancellationToken>());

        var checkpointHandler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        ProjectionRequest checkpointOnly = request with
        {
            Events = [.. request.Events, Event(new PartyNotFound(), 3, DateTimeOffset.UnixEpoch.AddSeconds(2))],
        };
        (await checkpointHandler.ProjectAsync(checkpointOnly, "historical-checkpoint", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        PartyIndexSdkReadModel checkpointed = store.Deserialize<PartyIndexSdkReadModel>(indexKey);
        checkpointed.LastSequenceNumbers[request.AggregateId].ShouldBe(3);
        checkpointed.LastIndexedEvents.ShouldBeEmpty();
        await search.Received(3).NotifyIndexedAsync("tenant-a", Arg.Any<PartyIndexEntry>(),
            "PartyProjectionReconciled", legacy.Entries[request.AggregateId].LastModifiedAt, Arg.Any<long?>(), Arg.Any<CancellationToken>());

        ProjectionEventDto canonicalEvent = Event(new PartyReactivated(), 4, DateTimeOffset.UnixEpoch.AddSeconds(3));
        var mutationHandler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        (await mutationHandler.ProjectAsync(request with { Events = [.. checkpointOnly.Events, canonicalEvent] }, "historical-mutation",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        ReadModelEntry<PartyIndexSdkReadModel> installed = await store.GetAsync<PartyIndexSdkReadModel>(
            "statestore", indexKey, TestContext.Current.CancellationToken);
        installed.Value!.LastIndexedEvents[request.AggregateId].ShouldBe(
            new PartyIndexEventMetadata(4, nameof(PartyReactivated), canonicalEvent.Timestamp));

        var olderRetry = new PartyIndexSdkProjectionHandler(store, s_options, search);
        (await olderRetry.ProjectAsync(checkpointOnly, "historical-older", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        (await store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, TestContext.Current.CancellationToken))
            .ETag.ShouldBe(installed.ETag);
        await search.Received(1).NotifyIndexedAsync("tenant-a",
            Arg.Is<PartyIndexEntry>(entry => entry.IsActive), nameof(PartyReactivated), canonicalEvent.Timestamp,
            Arg.Any<long?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("json-redacted", "{}")]
    [InlineData("json-redacted", "null")]
    [InlineData("json", "{ not valid json")]
    public async Task IndexHandler_DecoderCheckpointOnlyRetainsSerializedProvenanceOnRetryAsync(string format, string payload)
    {
        var store = new InMemoryReadModelBatchStore();
        ProjectionRequest request = CreateRequest();
        var seedHandler = new PartyIndexSdkProjectionHandler(store, s_options);
        _ = await seedHandler.ProjectAsync(request, "seed", TestContext.Current.CancellationToken);
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(false, true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        ProjectionRequest checkpointOnly = request with
        {
            Events = [new ProjectionEventDto(nameof(PartyReactivated), System.Text.Encoding.UTF8.GetBytes(payload),
                format, 3, DateTimeOffset.UnixEpoch.AddMinutes(1), "correlation-1")],
        };

        (await handler.ProjectAsync(checkpointOnly, "checkpoint", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        string indexKey = PartySdkReadModelAddresses.Index(request.TenantId);
        ReadModelEntry<PartyIndexSdkReadModel> persisted = await store.GetAsync<PartyIndexSdkReadModel>(
            "statestore", indexKey, TestContext.Current.CancellationToken);
        persisted.Value!.LastIndexedEvents[request.AggregateId].ShouldBe(
            new PartyIndexEventMetadata(2, nameof(PartyDeactivated), request.Events[1].Timestamp));
        persisted.Value.LastSequenceNumbers[request.AggregateId].ShouldBe(3);
        persisted.Value.Entries[request.AggregateId].IsActive.ShouldBeFalse();
        handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        (await handler.ProjectAsync(checkpointOnly, "checkpoint-retry", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        (await store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, TestContext.Current.CancellationToken))
            .ETag.ShouldBe(persisted.ETag);
        await search.DidNotReceiveWithAnyArgs().NotifyIndexedAsync(default!, default!, default!, default, default, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndexHandler_ConfiguredDaprSerializerPreservesDescriptorsAndHistoricalRowsAsync(bool historical)
    {
        // Match the host's AddDaprClient registration and inspect its configured options without
        // making any sidecar request. Complete rows use the live store's serializer in both directions.
        var services = new ServiceCollection();
        services.AddDaprClient();
        using ServiceProvider provider = services.BuildServiceProvider();
        JsonSerializerOptions daprOptions = provider.GetRequiredService<DaprClient>().JsonSerializerOptions;
        var store = new InMemoryReadModelBatchStore(daprOptions, daprOptions);
        DateTimeOffset timestamp = new DateTimeOffset(2026, 8, 4, 12, 34, 56, TimeSpan.FromMinutes(330)).AddTicks(1234567);
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                CreateRequest().Events[0],
                Event(new PartyDeactivated(), 2, timestamp) with { EventTypeName = typeof(PartyDeactivated).FullName! },
            ],
        };
        string indexKey = PartySdkReadModelAddresses.Index(request.TenantId);
        if (historical)
        {
            PartyIndexSdkReadModel legacy = PartyIndexSdkProjectionHandler.Fold(request, null);
            await store.SaveAsync("statestore", indexKey, new
            {
                legacy.Entries,
                legacy.LastSequenceNumbers,
                legacy.ErasureSequenceNumbers,
                legacy.ProjectedAt,
                legacy.ProjectionVersion,
            }, TestContext.Current.CancellationToken);
        }
        else
        {
            var seedHandler = new PartyIndexSdkProjectionHandler(store, s_options);
            _ = await seedHandler.ProjectAsync(request, "seed", TestContext.Current.CancellationToken);
        }

        ReadModelEntry<PartyIndexSdkReadModel> before = await store.GetAsync<PartyIndexSdkReadModel>(
            "statestore", indexKey, TestContext.Current.CancellationToken);
        before.Value.ShouldNotBeNull();
        if (historical)
        {
            before.Value.LastIndexedEvents.ShouldBeEmpty();
        }
        else
        {
            PartyIndexEventMetadata metadata = before.Value.LastIndexedEvents.ShouldHaveSingleItem().Value;
            metadata.SequenceNumber.ShouldBe(2);
            metadata.EventTypeName.ShouldBe(typeof(PartyDeactivated).FullName);
            metadata.Timestamp.ToString("O").ShouldBe("2026-08-04T12:34:56.1234567+05:30");
            // Descriptor dictionaries also cross the candidate/fixture serializer and the
            // configured Dapr serializer in both directions, independently of entry enum policy.
            foreach ((JsonSerializerOptions writer, JsonSerializerOptions reader) in new[]
            {
                (s_canonicalJsonOptions, daprOptions),
                (daprOptions, s_canonicalJsonOptions),
            })
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(before.Value.LastIndexedEvents, writer);
                PartyIndexEventMetadata roundTripped = JsonSerializer
                    .Deserialize<Dictionary<string, PartyIndexEventMetadata>>(bytes, reader)![request.AggregateId];
                roundTripped.SequenceNumber.ShouldBe(metadata.SequenceNumber);
                roundTripped.EventTypeName.ShouldBe(metadata.EventTypeName);
                roundTripped.Timestamp.ToString("O").ShouldBe("2026-08-04T12:34:56.1234567+05:30");
            }
        }

        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        (await handler.ProjectAsync(request, "retry", TestContext.Current.CancellationToken))
            .Status.ShouldBe(historical ? ProjectionDispatchStatus.Completed : ProjectionDispatchStatus.AlreadyCompleted);
        ReadModelEntry<PartyIndexSdkReadModel> after = await store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, TestContext.Current.CancellationToken);
        if (historical)
        {
            after.ETag.ShouldNotBe(before.ETag);
        }
        else
        {
            after.ETag.ShouldBe(before.ETag);
        }
        after.Value.ShouldNotBeNull().SearchContentHashes.ShouldNotBeNull().ContainsKey(request.AggregateId).ShouldBeTrue();
        string expectedTimestamp = historical ? timestamp.ToUniversalTime().ToString("O") : timestamp.ToString("O");
        await search.Received(1).NotifyIndexedAsync("tenant-a", Arg.Any<PartyIndexEntry>(),
            historical ? "PartyProjectionReconciled" : typeof(PartyDeactivated).FullName!,
            Arg.Is<DateTimeOffset>(value => value.ToString("O") == expectedTimestamp), Arg.Any<long?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndexHandler_SearchCancellationPropagatesForWriteAndReconciliationAsync(bool reconciliation)
    {
        var store = new InMemoryReadModelBatchStore();
        ProjectionRequest request = CreateRequest();
        if (reconciliation)
        {
            var seedHandler = new PartyIndexSdkProjectionHandler(store, s_options);
            _ = await seedHandler.ProjectAsync(request, "seed", TestContext.Current.CancellationToken);
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<bool>(cancellation.Token);
            });
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);

        await Should.ThrowAsync<OperationCanceledException>(() => handler.ProjectAsync(
            request, "cancel", cancellation.Token));
        store.Deserialize<PartyIndexSdkReadModel>(PartySdkReadModelAddresses.Index(request.TenantId))
            .LastIndexedEvents[request.AggregateId].EventTypeName.ShouldBe(nameof(PartyDeactivated));
    }

    [Fact]
    public async Task IndexHandler_DuplicateDeliveryReturnsAlreadyCompletedAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexSdkReadModel existing = PartyIndexSdkProjectionHandler.Fold(CreateRequest(), current: null);
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(existing, "etag-1"));
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateRequest(),
            "dispatch-dup",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        await store.DidNotReceive().TrySaveAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<PartyIndexSdkReadModel>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_DuplicateTerminalErasureReturnsAlreadyCompletedAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexSdkReadModel erased = PartyIndexSdkProjectionHandler.Fold(
            CreateErasureRequest(),
            CreateIndex("party-1", "party-2"));
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(erased, "etag-erased"));
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateErasureRequest(),
            "dispatch-erase-duplicate",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        await store.DidNotReceive().TrySaveAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<PartyIndexSdkReadModel>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_ConcurrencyRetryRefoldsAndPreservesUnrelatedEntryAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexSdkReadModel concurrent = CreateIndex("party-2");
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(
                new ReadModelEntry<PartyIndexSdkReadModel>(null, null),
                new ReadModelEntry<PartyIndexSdkReadModel>(null, null),
                new ReadModelEntry<PartyIndexSdkReadModel>(concurrent, "etag-concurrent"));
        var attempts = new List<PartyIndexSdkReadModel>();
        store.TrySaveAsync(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Do<PartyIndexSdkReadModel>(attempts.Add),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(false, true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateRequest(),
            "dispatch-concurrent",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        attempts.Count.ShouldBe(2);
        attempts[0].Entries.Keys.ShouldBe(["party-1"]);
        attempts[1].Entries.Keys.Order(StringComparer.Ordinal).ShouldBe(["party-1", "party-2"]);
        attempts[1].LastSequenceNumbers["party-2"].ShouldBe(1);
    }

    [Fact]
    public async Task IndexHandler_OptimisticRetryWaitsForNewerCanonicalReplayAsync()
    {
        var durable = new InMemoryReadModelBatchStore();
        string indexKey = PartySdkReadModelAddresses.Index("tenant-a");
        ProjectionRequest request = CreateRequest();
        var seedHandler = new PartyIndexSdkProjectionHandler(durable, s_options);
        _ = await seedHandler.ProjectAsync(request with { Events = [request.Events[0]] },
            "seed", TestContext.Current.CancellationToken);
        ProjectionRequest newer = request with
        {
            Events = [.. request.Events, Event(new PartyReactivated(), 3, DateTimeOffset.UnixEpoch.AddSeconds(2))],
        };
        ProjectionRequest unrelated = request with { AggregateId = "party-2", Events = [request.Events[0]] };
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, Arg.Any<CancellationToken>())
            .Returns(_ => durable.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, TestContext.Current.CancellationToken));
        var attempts = new List<PartyIndexSdkReadModel>();
        store.TrySaveAsync("statestore", indexKey, Arg.Any<PartyIndexSdkReadModel>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(async call =>
            {
                attempts.Add(call.Arg<PartyIndexSdkReadModel>());
                if (attempts.Count == 1)
                {
                    // Another writer commits a newer entry for this aggregate and an unrelated
                    // aggregate before this handler's first ETag write can succeed.
                    _ = await seedHandler.ProjectAsync(newer, "concurrent-target", TestContext.Current.CancellationToken).ConfigureAwait(false);
                    _ = await seedHandler.ProjectAsync(unrelated, "concurrent-other", TestContext.Current.CancellationToken).ConfigureAwait(false);
                }

                return await durable.TrySaveAsync("statestore", indexKey, call.Arg<PartyIndexSdkReadModel>(),
                    call.ArgAt<string>(3), TestContext.Current.CancellationToken).ConfigureAwait(false);
            });
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);

        (await handler.ProjectAsync(request, "contended", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Retryable);

        attempts.Count.ShouldBe(2);
        attempts[0].LastIndexedEvents[request.AggregateId].SequenceNumber.ShouldBe(2);
        PartyIndexSdkReadModel persisted = durable.Deserialize<PartyIndexSdkReadModel>(indexKey);
        persisted.LastIndexedEvents.ShouldBe(attempts[1].LastIndexedEvents);
        persisted.LastIndexedEvents[request.AggregateId].ShouldBe(
            new PartyIndexEventMetadata(3, nameof(PartyReactivated), newer.Events[2].Timestamp));
        persisted.LastIndexedEvents[unrelated.AggregateId].ShouldBe(
            new PartyIndexEventMetadata(1, nameof(PartyCreated), unrelated.Events[0].Timestamp));
        persisted.Entries[request.AggregateId].IsActive.ShouldBeTrue();
        await search.DidNotReceiveWithAnyArgs().NotifyIndexedAsync(default!, default!, default!, default, default, default);
        (await handler.ProjectAsync(newer, "current-replay", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        await search.Received(1).NotifyIndexedAsync("tenant-a",
            Arg.Is<PartyIndexEntry>(entry => entry.Id == request.AggregateId && entry.IsActive),
            nameof(PartyReactivated), newer.Events[2].Timestamp, Arg.Any<long?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndexHandler_ErasureRemovesOnlyTargetProvenanceAndRetainsFenceAsync(bool sdkCleanup)
    {
        var store = new InMemoryReadModelBatchStore();
        var seedHandler = new PartyIndexSdkProjectionHandler(store, s_options);
        ProjectionRequest request = CreateRequest();
        ProjectionRequest unrelated = request with { AggregateId = "party-2", Events = [request.Events[0]] };
        _ = await seedHandler.ProjectAsync(request, "seed-target", TestContext.Current.CancellationToken);
        _ = await seedHandler.ProjectAsync(unrelated, "seed-other", TestContext.Current.CancellationToken);
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        if (sdkCleanup)
        {
            var eraser = new PartySdkReadModelEraser(store, store, s_options, search);
            await eraser.EraseAsync(request.TenantId, request.AggregateId, TestContext.Current.CancellationToken);
        }
        else
        {
            ProjectionRequest erasure = CreateErasureRequest() with
            {
                Events = [CreateErasureRequest().Events[0] with { SequenceNumber = 3 }],
            };
            (await handler.ProjectAsync(erasure, "erase", TestContext.Current.CancellationToken))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }

        string indexKey = PartySdkReadModelAddresses.Index(request.TenantId);
        PartyIndexSdkReadModel erased = store.Deserialize<PartyIndexSdkReadModel>(indexKey);
        erased.Entries.Keys.ShouldBe([unrelated.AggregateId]);
        erased.SearchContentHashes.ShouldNotBeNull().Keys.ShouldBe([unrelated.AggregateId]);
        erased.LastIndexedEvents.ShouldHaveSingleItem().ShouldBe(
            new KeyValuePair<string, PartyIndexEventMetadata>(unrelated.AggregateId,
                new PartyIndexEventMetadata(1, nameof(PartyCreated), unrelated.Events[0].Timestamp)));
        erased.ErasureSequenceNumbers.ContainsKey(request.AggregateId).ShouldBeTrue();

        handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        ProjectionRequest recreate = request with
        {
            Events = [Event(new PartyCreated { Type = PartyType.Person },
                erased.LastSequenceNumbers[request.AggregateId] + 1, DateTimeOffset.UnixEpoch.AddHours(1))],
        };
        (await handler.ProjectAsync(recreate, "blocked-recreate", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await handler.ProjectAsync(request, "old-retry", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        PartyIndexSdkReadModel after = store.Deserialize<PartyIndexSdkReadModel>(indexKey);
        after.Entries.Keys.ShouldBe([unrelated.AggregateId]);
        after.LastIndexedEvents.ShouldBe(erased.LastIndexedEvents);
        after.ErasureSequenceNumbers.ShouldBe(erased.ErasureSequenceNumbers);
        await search.Received(3).NotifyRemovedAsync(request.TenantId, request.AggregateId, Arg.Any<CancellationToken>());
        await search.DidNotReceiveWithAnyArgs().NotifyIndexedAsync(default!, default!, default!, default, Arg.Any<long?>(), default);
    }

    [Fact]
    public async Task IndexHandler_SeparateDeliveryGapIsRetryableWithoutPersistenceAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(CreateIndex("party-1"), "etag-1"));
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        ProjectionRequest gap = CreateRequest() with
        {
            Events = [Event(new PartyDeactivated(), 3, DateTimeOffset.UnixEpoch.AddSeconds(3))],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            gap,
            "dispatch-gap",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("delivery-sequence-gap");
        await store.DidNotReceiveWithAnyArgs().TrySaveAsync(
            default!, default!, default(PartyIndexSdkReadModel)!, default!, default);
    }

    [Fact]
    public async Task IndexHandler_RetrySnapshotGapStopsBeforeSecondPersistenceAttemptAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexSdkReadModel sequenceOne = CreateIndex("party-1");
        PartyIndexSdkReadModel regressed = sequenceOne with
        {
            LastSequenceNumbers = new Dictionary<string, long>(StringComparer.Ordinal),
        };
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(
                new ReadModelEntry<PartyIndexSdkReadModel>(sequenceOne, "etag-1"),
                new ReadModelEntry<PartyIndexSdkReadModel>(sequenceOne, "etag-1"),
                new ReadModelEntry<PartyIndexSdkReadModel>(regressed, "etag-regressed"));
        store.TrySaveAsync(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<PartyIndexSdkReadModel>(),
                "etag-1",
                Arg.Any<CancellationToken>())
            .Returns(false);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        ProjectionRequest sequenceTwo = CreateRequest() with
        {
            Events = [Event(new PartyDeactivated(), 2, DateTimeOffset.UnixEpoch.AddSeconds(2))],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            sequenceTwo,
            "dispatch-retry-gap",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("delivery-sequence-gap");
        await store.Received(1).TrySaveAsync(
            "statestore",
            PartySdkReadModelAddresses.Index("tenant-a"),
            Arg.Any<PartyIndexSdkReadModel>(),
            "etag-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_MixedKnownAndUnresolvedDeliveryFailsWithoutWriteAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(null, null));
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch),
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    2,
                    DateTimeOffset.UnixEpoch.AddSeconds(1),
                    "correlation-1"),
            ],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request,
            "dispatch-unresolved-mixed",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("unresolved-or-unsupported-event");
        await store.DidNotReceive().TrySaveAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<PartyIndexSdkReadModel>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_MixedKnownAndUnresolvedDeliveryLogsUnresolvedDiagnosticAsync()
    {
        // FoldCore's failure branch returns before its own logged DeserializeNew loop is ever
        // reached, so without a dedicated fix the shared Party index projection would silently
        // never emit NonJsonEventDropped/UnknownEventTypeDropped/AmbiguousEventTypeDropped —
        // exactly when a delivery is stuck Retryable and operators most need to see it.
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(null, null));
        var logger = new RecordingLogger<PartyIndexSdkProjectionHandler>();
        ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger("Hexalith.Parties.Projections.Handlers.PartySdkProjectionFold").Returns(logger);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer: null, loggerFactory: loggerFactory);
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch),
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    2,
                    DateTimeOffset.UnixEpoch.AddSeconds(1),
                    "correlation-1"),
            ],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request,
            "dispatch-unresolved-mixed-logged",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("unresolved-or-unsupported-event");
        (LogLevel Level, string Message, Exception? Exception) record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain("could not resolve event type");
    }

    [Fact]
    public async Task IndexHandler_FirstPersistenceSkipAndAdvanceLogsDiagnosticOnceAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(null, null));
        store.TrySaveAsync(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<PartyIndexSdkReadModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        var logger = new RecordingLogger<PartyIndexSdkProjectionHandler>();
        ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger("Hexalith.Parties.Projections.Handlers.PartySdkProjectionFold").Returns(logger);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer: null, loggerFactory: loggerFactory);
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    nameof(PartyCreated),
                    "{ not valid json"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request,
            "dispatch-corrupt-first-write",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (LogLevel level, string message, Exception? loggedException) = logger.Records.ShouldHaveSingleItem();
        level.ShouldBe(LogLevel.Warning);
        message.ShouldContain("failed to deserialize live event");
        loggedException.ShouldBeNull();
        await store.Received(1).TrySaveAsync(
            "statestore",
            PartySdkReadModelAddresses.Index("tenant-a"),
            Arg.Any<PartyIndexSdkReadModel>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetailHandler_DuplicateDeliveryReturnsAlreadyCompletedAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        PartyDetailSdkReadModel detail = PartyDetailSdkProjectionHandler.Fold(CreateRequest(), current: null);
        PartyProcessingSdkReadModel processing = PartyProcessingActivityFold.Fold(CreateRequest(), current: null);
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(detail, "etag-d"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(processing, "etag-p"));
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateRequest(),
            "dispatch-dup",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        await batchStore.DidNotReceive().ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DetailHandler_ExactlyOneMissingCoordinatedSlotRequiresRebuildAsync(bool detailIsMissing)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(
                detailIsMissing ? null : new PartyDetailSdkReadModel { LastSequenceNumber = 2 },
                detailIsMissing ? null : "detail-etag"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(
                detailIsMissing ? new PartyProcessingSdkReadModel { LastSequenceNumber = 2 } : null,
                detailIsMissing ? "processing-etag" : null));
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
        ProjectionRequest request = CreateRequest() with
        {
            Events = [Event(new PartyReactivated(), 3, DateTimeOffset.UnixEpoch.AddSeconds(2))],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request,
            "dispatch-missing-slot",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("projection-rebuild-required");
        await batchStore.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Fact]
    public async Task DetailHandler_ConflictingDuplicateSequenceIsRetryableWithoutWriteAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
        ProjectionEventDto first = Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch);
        ProjectionRequest request = CreateRequest() with
        {
            Events = [first, first with { Payload = Event(new PartyCreated { Type = PartyType.Organization }, 1, DateTimeOffset.UnixEpoch).Payload }],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request,
            "dispatch-conflicting-duplicate",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("conflicting-duplicate-event");
        await batchStore.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Fact]
    public async Task DetailHandler_IdenticalDuplicateSequenceAppliesOnceAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        ReadModelBatch? captured = null;
        batchStore.ExecuteAsync(Arg.Do<ReadModelBatch>(batch => captured = batch), Arg.Any<CancellationToken>())
            .Returns(ReadModelBatchResult.Completed("fingerprint"));
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
        ProjectionEventDto first = Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch);
        ProjectionRequest request = CreateRequest() with { Events = [first, first with { Payload = [.. first.Payload] }] };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request,
            "dispatch-identical-duplicate",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        PartyProcessingSdkReadModel processing = DeserializeOperation<PartyProcessingSdkReadModel>(
            captured!,
            PartySdkReadModelAddresses.Processing("tenant-a", "party-1"));
        processing.Records.ShouldHaveSingleItem().SequenceNumber.ShouldBe(1);
        processing.LastSequenceNumber.ShouldBe(1);
    }

    [Fact]
    public async Task DetailHandler_UnresolvedOnlyDeliveryIsRetryableAndPersistsFailedAuditAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        PartyDetailSdkReadModel detail = PartyDetailSdkProjectionHandler.Fold(CreateRequest(), current: null);
        PartyProcessingSdkReadModel processing = PartyProcessingActivityFold.Fold(CreateRequest(), current: null);
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(detail, "etag-d"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(processing, "etag-p"));
        PartyProcessingSdkReadModel? persistedAudit = null;
        store.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Do<PartyProcessingSdkReadModel>(value => persistedAudit = value),
                "etag-p",
                Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
        ProjectionRequest unresolved = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    3,
                    DateTimeOffset.UnixEpoch.AddSeconds(3),
                    "correlation-1"),
            ],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            unresolved,
            "dispatch-unresolved",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("unresolved-or-unsupported-event");
        persistedAudit.ShouldNotBeNull();
        persistedAudit.Records.Last().Outcome.ShouldBe("Failed");
        persistedAudit.LastSequenceNumber.ShouldBe(2);
        await batchStore.DidNotReceive().ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetailHandler_UnresolvedAuditWriteExhaustsRetryBudgetWithoutCrashingDispatchAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        PartyDetailSdkReadModel detail = PartyDetailSdkProjectionHandler.Fold(CreateRequest(), current: null);
        PartyProcessingSdkReadModel processing = PartyProcessingActivityFold.Fold(CreateRequest(), current: null);
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(detail, "etag-d"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(processing, "etag-p"));
        // Every optimistic-concurrency attempt loses the race, exhausting
        // ReadModelWritePolicy.UpdateAsync's retry budget and making the audit write itself throw
        // InvalidOperationException — this must not crash dispatch of the still-retryable delivery.
        store.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<PartyProcessingSdkReadModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(false);
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
        ProjectionRequest unresolved = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    3,
                    DateTimeOffset.UnixEpoch.AddSeconds(3),
                    "correlation-1"),
            ],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            unresolved,
            "dispatch-unresolved-exhausted",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("unresolved-or-unsupported-event");
        await batchStore.DidNotReceive().ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetailHandler_MixedKnownAndUnresolvedDeliveryIsRetryableAndRecordsAuditAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch),
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    2,
                    DateTimeOffset.UnixEpoch.AddSeconds(1),
                    "correlation-1"),
            ],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request,
            "dispatch-unresolved-mixed",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("unresolved-or-unsupported-event");
        PartyDetailSdkReadModel detail = store.Deserialize<PartyDetailSdkReadModel>(PartySdkReadModelAddresses.Detail("tenant-a", "party-1"));
        detail.Detail.ShouldBeNull();
        detail.LastSequenceNumber.ShouldBe(long.MinValue);
        PartyProcessingSdkReadModel persistedAudit = store.Deserialize<PartyProcessingSdkReadModel>(PartySdkReadModelAddresses.Processing("tenant-a", "party-1"));
        persistedAudit.Records.Select(static record => record.Outcome).ShouldBe(["Succeeded", "Failed"]);
        persistedAudit.LastSequenceNumber.ShouldBe(1);
    }

    [Fact]
    public async Task DetailHandler_UnresolvedThenResolvableInSameBatchDoesNotAdvancePastUnresolvedAsync()
    {
        var store = new InMemoryReadModelBatchStore();
        var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
                Event(new PartyCreated { Type = PartyType.Person }, 2, DateTimeOffset.UnixEpoch.AddSeconds(1)),
            ],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request,
            "dispatch-unresolved-then-resolved",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("unresolved-or-unsupported-event");
        PartyDetailSdkReadModel detail = store.Deserialize<PartyDetailSdkReadModel>(PartySdkReadModelAddresses.Detail("tenant-a", "party-1"));
        detail.Detail.ShouldBeNull();
        detail.LastSequenceNumber.ShouldBe(long.MinValue);
        PartyProcessingSdkReadModel persistedAudit = store.Deserialize<PartyProcessingSdkReadModel>(PartySdkReadModelAddresses.Processing("tenant-a", "party-1"));
        persistedAudit.Records.Select(static record => record.Outcome).ShouldBe(["Failed", "Succeeded"]);
        // The checkpoint must stay behind the still-unresolved sequence 1 event even though
        // sequence 2 resolved successfully in the same batch — otherwise a later redelivery
        // (e.g. after a consumer upgrade) would never revisit sequence 1 again.
        persistedAudit.LastSequenceNumber.ShouldBe(long.MinValue);
    }

    [Fact]
    public async Task DetailHandler_SeparateDeliveryGapIsRetryableWithoutCoordinatedWriteAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel { LastSequenceNumber = 1 }, "detail-etag"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel { LastSequenceNumber = 1 }, "processing-etag"));
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
        ProjectionRequest gap = CreateRequest() with
        {
            Events = [Event(new PartyDeactivated(), 3, DateTimeOffset.UnixEpoch.AddSeconds(3))],
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            gap,
            "dispatch-gap",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("delivery-sequence-gap");
        await batchStore.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Fact]
    public void DetailFold_PartyCreatedAfterErasurePreservesTombstone()
    {
        ProjectionRequest eraseThenCreate = new(
            "tenant-a",
            "party",
            "party-1",
            [
                Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch),
                Event(new PartyErased
                {
                    PartyId = "party-1",
                    TenantId = "tenant-a",
                    ErasedAt = DateTimeOffset.UnixEpoch.AddMinutes(5),
                }, 2, DateTimeOffset.UnixEpoch.AddMinutes(5)),
                Event(new PartyCreated { Type = PartyType.Organization }, 3, DateTimeOffset.UnixEpoch.AddMinutes(6)),
            ]);

        PartyDetailSdkReadModel result = PartyDetailSdkProjectionHandler.Fold(eraseThenCreate, current: null);

        result.Detail.ShouldNotBeNull();
        result.Detail.IsErased.ShouldBeTrue();
        result.Detail.Type.ShouldBe(PartyType.Person);
        result.LastSequenceNumber.ShouldBe(3);
        result.ErasureSequenceNumber.ShouldBe(2);
    }

    [Fact]
    public void IndexFold_NoOpAfterErasureRetainsTombstoneAndAdvancesSafeCheckpoint()
    {
        ProjectionRequest eraseThenNoOp = new(
            "tenant-a",
            "party",
            "party-1",
            [
                Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch),
                Event(new PartyErased
                {
                    PartyId = "party-1",
                    TenantId = "tenant-a",
                    ErasedAt = DateTimeOffset.UnixEpoch.AddMinutes(5),
                }, 2, DateTimeOffset.UnixEpoch.AddMinutes(5)),
                Event(new PersonDetailsUpdated
                {
                    PersonDetails = new PersonDetails { FirstName = "x", LastName = "y" },
                }, 3, DateTimeOffset.UnixEpoch.AddMinutes(6)),
            ]);

        PartyIndexSdkReadModel result = PartyIndexSdkProjectionHandler.Fold(eraseThenNoOp, current: null);

        result.Entries.ContainsKey("party-1").ShouldBeFalse();
        result.LastSequenceNumbers["party-1"].ShouldBe(3);
        result.ErasureSequenceNumbers["party-1"].ShouldBe(2);
    }

    [Theory]
    [InlineData("TotallyUnknownEventType", "TotallyUnknownEventType")]
    [InlineData("Future.Events.NewProcessingEvent", "NewProcessingEvent")]
    [InlineData("AnotherUnknownEvent", "AnotherUnknownEvent")]
    public void ProcessingActivityFold_UnresolvedEventIsRecordedAsFailedWithoutAdvancing(
        string eventTypeName,
        string expectedEventType)
    {
        ProjectionRequest request = new(
            "tenant-a",
            "party",
            "party-1",
            [
                new ProjectionEventDto(
                    eventTypeName,
                    "{}"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ]);

        PartyProcessingSdkReadModel result = PartyProcessingActivityFold.Fold(request, current: null);

        ProcessingActivityRecord record = result.Records.ShouldHaveSingleItem();
        record.OperationCategory.ShouldBe("Other");
        record.EventType.ShouldBe(expectedEventType);
        record.Outcome.ShouldBe("Failed");
        result.LastSequenceNumber.ShouldBe(long.MinValue);
    }

    [Fact]
    public void ProcessingActivityFold_UncategorizedResolvedEventRetainsEventTypeAndOutcome()
    {
        ProjectionRequest request = CreateRequest() with
        {
            Events = [Event(new PartyNotFound(), 1, DateTimeOffset.UnixEpoch)],
        };

        PartyProcessingSdkReadModel result = PartyProcessingActivityFold.Fold(request, current: null);

        ProcessingActivityRecord record = result.Records.ShouldHaveSingleItem();
        record.OperationCategory.ShouldBe("Other");
        record.EventType.ShouldBe(nameof(PartyNotFound));
        record.Outcome.ShouldBe("Succeeded");
        record.Summary.ShouldBe("PartyNotFound recorded.");
        result.LastSequenceNumber.ShouldBe(1);
    }

    [Fact]
    public void ProcessingActivityFold_PreservesEstablishedOperationCategories()
    {
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch),
                Event(new ConsentRecorded
                {
                    PartyId = "party-1",
                    TenantId = "tenant-a",
                    ConsentId = "consent-1",
                    ChannelId = "email-1",
                    Purpose = "Updates",
                    LawfulBasis = LawfulBasis.Consent,
                    GrantedAt = DateTimeOffset.UnixEpoch,
                    GrantedBy = "actor-1",
                }, 2, DateTimeOffset.UnixEpoch.AddSeconds(1)),
                Event(new ProcessingRestricted
                {
                    PartyId = "party-1",
                    TenantId = "tenant-a",
                    RestrictedAt = DateTimeOffset.UnixEpoch,
                }, 3, DateTimeOffset.UnixEpoch.AddSeconds(2)),
                Event(new ErasePartyRequested
                {
                    PartyId = "party-1",
                    TenantId = "tenant-a",
                    RequestedAt = DateTimeOffset.UnixEpoch,
                    RequestedBy = "actor-1",
                }, 4, DateTimeOffset.UnixEpoch.AddSeconds(3)),
            ],
        };

        PartyProcessingSdkReadModel result = PartyProcessingActivityFold.Fold(request, current: null);

        result.Records.Select(static record => record.OperationCategory)
            .ShouldBe(["PartyCommand", "Consent", "Restriction", "Erasure"]);
        result.Records.Select(static record => record.EventType)
            .ShouldBe([nameof(PartyCreated), nameof(ConsentRecorded), nameof(ProcessingRestricted), nameof(ErasePartyRequested)]);
        result.Records.ShouldAllBe(static record => record.Outcome == "Succeeded");
        result.LastSequenceNumber.ShouldBe(4);
    }

    [Fact]
    public void ProcessingActivityFold_DecodableRedactedEventKeepsMappedCategory()
    {
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(nameof(PersonDetailsUpdated), "{\"personDetails\":null}"u8.ToArray(), "json-redacted",
                    1, DateTimeOffset.UnixEpoch, "correlation-1"),
            ],
        };

        PartyProcessingSdkReadModel result = PartyProcessingActivityFold.Fold(request, current: null);

        ProcessingActivityRecord record = result.Records.ShouldHaveSingleItem();
        record.OperationCategory.ShouldBe("PartyCommand");
        record.EventType.ShouldBe(nameof(PersonDetailsUpdated));
        record.Outcome.ShouldBe("Succeeded");
        result.LastSequenceNumber.ShouldBe(1);
    }

    [Fact]
    public void ProcessingActivityFold_AppendPreservesLegacyCategory()
    {
        PartyProcessingSdkReadModel legacy = CreateLegacyProcessingReadModel();
        ProjectionRequest request = CreateRequest() with
        {
            Events = [Event(new PartyNotFound(), 2, DateTimeOffset.UnixEpoch.AddSeconds(1))],
        };

        PartyProcessingSdkReadModel result = PartyProcessingActivityFold.Fold(request, legacy);

        result.Records.Count.ShouldBe(2);
        result.Records[0].ShouldBe(legacy.Records[0]);
        result.Records[1].OperationCategory.ShouldBe("Other");
        result.Records[1].EventType.ShouldBe(nameof(PartyNotFound));
        result.LastSequenceNumber.ShouldBe(2);
        legacy.Records.ShouldHaveSingleItem();
    }

    [Fact]
    public void ProcessingActivityFold_AppendsAcrossDeliveries()
    {
        ProjectionRequest first = CreateRequest() with
        {
            Events = [Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch)],
        };
        PartyProcessingSdkReadModel afterFirst = PartyProcessingActivityFold.Fold(first, current: null);
        ProjectionRequest second = CreateRequest() with
        {
            Events = [Event(new PartyDeactivated(), 2, DateTimeOffset.UnixEpoch.AddSeconds(1))],
        };

        PartyProcessingSdkReadModel afterSecond = PartyProcessingActivityFold.Fold(second, afterFirst);

        afterSecond.Records.Count.ShouldBe(2);
        afterSecond.Records[0].SequenceNumber.ShouldBe(1);
        afterSecond.Records[1].SequenceNumber.ShouldBe(2);
        afterSecond.LastSequenceNumber.ShouldBe(2);
    }

    [Fact]
    public void ProcessingActivityFold_ResolvedRedeliveryReplacesFailedRecordWithoutDuplication()
    {
        var failed = new PartyProcessingSdkReadModel
        {
            Records =
            [
                new ProcessingActivityRecord
                {
                    SequenceNumber = 1,
                    PartyId = "party-1",
                    TenantId = "tenant-a",
                    ActorId = "old-actor",
                    CorrelationId = "old-correlation",
                    OperationCategory = "TotallyUnknownEventType",
                    Outcome = "Failed",
                    EventType = "TotallyUnknownEventType",
                    Timestamp = DateTimeOffset.UnixEpoch,
                    Summary = "TotallyUnknownEventType recorded.",
                },
            ],
            LastSequenceNumber = long.MinValue,
        };
        ProjectionEventDto resolved = Event(
            new PartyCreated { Type = PartyType.Person },
            1,
            DateTimeOffset.UnixEpoch.AddMinutes(1)) with
        {
            CorrelationId = "resolved-correlation",
            UserId = "resolved-actor",
        };
        ProjectionRequest request = CreateRequest() with { Events = [resolved] };

        PartyProcessingSdkReadModel result = PartyProcessingActivityFold.Fold(request, failed);

        ProcessingActivityRecord record = result.Records.ShouldHaveSingleItem();
        record.Outcome.ShouldBe("Succeeded");
        record.EventType.ShouldBe(nameof(PartyCreated));
        record.OperationCategory.ShouldBe("PartyCommand");
        record.ActorId.ShouldBe("resolved-actor");
        record.CorrelationId.ShouldBe("resolved-correlation");
        record.Timestamp.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(1));
        result.LastSequenceNumber.ShouldBe(1);
    }

    [Fact]
    public void SharedIndex_IsDeclaredSharedAndAdvertisesOnlyDomainSharedRebuild()
    {
        ProjectionReadModelSlotDeclaration slot = PartyIndexSdkProjectionHandler.ProjectionReadModelSlots.Single();

        slot.Domain.ShouldBe("party");
        slot.Kind.ShouldBe(ProjectionReadModelSlotKind.Shared);
        slot.DeclaresCanonicalWriter.ShouldBeTrue();
        typeof(IAsyncDomainProjectionRebuildHandler).IsAssignableFrom(typeof(PartyIndexSdkProjectionHandler)).ShouldBeFalse();
        typeof(IAsyncDomainSharedProjectionRebuildHandler).IsAssignableFrom(typeof(PartyIndexSdkProjectionHandler)).ShouldBeTrue();
    }

    [Fact]
    public void DetailHandler_DeclaresCanonicalDetailAndProcessingSlots()
    {
        PartyDetailSdkProjectionHandler.ProjectionReadModelSlots.ShouldBe(
        [
            new ProjectionReadModelSlotDeclaration(
                "party",
                PartyProjectionNames.Detail,
                PartySdkReadModelAddresses.DetailSlot,
                ProjectionReadModelSlotKind.AggregateOwned,
                declaresCanonicalWriter: true),
            new ProjectionReadModelSlotDeclaration(
                "party",
                PartyProjectionNames.Detail,
                PartySdkReadModelAddresses.ProcessingSlot,
                ProjectionReadModelSlotKind.AggregateOwned,
                declaresCanonicalWriter: true),
        ]);
    }

    [Fact]
    public async Task SharedIndexRebuild_AccumulatesCompleteHistoriesIntoOneReplacementAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        ProjectionRequest first = CreateRequest();
        ProjectionRequest second = CreateRequest() with
        {
            AggregateId = "party-2",
            Events =
            [
                Event(new PartyCreated { Type = PartyType.Organization }, 1, DateTimeOffset.UnixEpoch.AddSeconds(2)),
            ],
        };

        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, first, TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, second, TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(
            identity,
            candidate,
            TestContext.Current.CancellationToken);

        plan.StoreName.ShouldBe("statestore");
        plan.Operations.Count.ShouldBe(1);
        plan.Operations[0].Key.ShouldBe(PartySdkReadModelAddresses.Index("tenant-a"));
        PartyIndexSdkReadModel rebuilt = JsonSerializer.Deserialize<PartyIndexSdkReadModel>(
            plan.Operations[0].CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        rebuilt.Entries.Keys.Order(StringComparer.Ordinal).ShouldBe(["party-1", "party-2"]);
        rebuilt.Entries["party-1"].Type.ShouldBe(PartyType.Person);
        rebuilt.Entries["party-1"].IsActive.ShouldBeFalse();
        rebuilt.Entries["party-1"].CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        rebuilt.Entries["party-1"].LastModifiedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(1));
        rebuilt.Entries["party-2"].Type.ShouldBe(PartyType.Organization);
        rebuilt.Entries["party-2"].IsActive.ShouldBeTrue();
        rebuilt.Entries["party-2"].CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(2));
        rebuilt.LastSequenceNumbers.ShouldBe(
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["party-1"] = 2,
                ["party-2"] = 1,
            });
        rebuilt.ErasureSequenceNumbers.ShouldBeEmpty();
        rebuilt.ProjectedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(2));
        plan.Operations[0].Concurrency.ShouldBe(ReadModelBatchConcurrency.CreateOnly);
    }

    [Fact]
    public async Task SharedIndexRebuild_SerializedCandidatePreservesLiveCanonicalProvenanceAsync()
    {
        ProjectionRequest first = CreateRequest() with
        {
            Events =
            [
                .. CreateRequest().Events,
                Event(new PartyNotFound(), 3, DateTimeOffset.UnixEpoch.AddSeconds(2)),
                Event(new PartyDeactivated(), 4, DateTimeOffset.UnixEpoch.AddSeconds(3)),
                Event(new PartyCreated { Type = PartyType.Organization }, 5, DateTimeOffset.UnixEpoch.AddSeconds(4)),
            ],
        };
        ProjectionRequest second = CreateRequest() with { AggregateId = "party-2", Events = [first.Events[0]] };
        ProjectionRequest erased = CreateRequest() with
        {
            AggregateId = "party-3",
            Events = [first.Events[0], CreateErasureRequest().Events[0]],
        };
        var liveStore = new InMemoryReadModelBatchStore();
        var liveHandler = new PartyIndexSdkProjectionHandler(liveStore, s_options);
        foreach (ProjectionEventDto[] delivery in new[] { first.Events[..1], first.Events[1..3], first.Events[3..] })
        {
            (await liveHandler.ProjectAsync(first with { Events = delivery }, "live", TestContext.Current.CancellationToken))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }

        _ = await liveHandler.ProjectAsync(second, "other", TestContext.Current.CancellationToken);
        _ = await liveHandler.ProjectAsync(erased, "erased", TestContext.Current.CancellationToken);
        var rebuiltStore = new InMemoryReadModelBatchStore();
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(true);
        var rebuildHandler = new PartyIndexSdkProjectionHandler(rebuiltStore, s_options, search);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await rebuildHandler.CreateEmptyCandidateAsync(
            identity, TestContext.Current.CancellationToken);
        foreach (ProjectionRequest history in new[] { first, second, erased })
        {
            // Each call reads a serialized candidate and a new handler has no in-memory history.
            rebuildHandler = new PartyIndexSdkProjectionHandler(rebuiltStore, s_options, search);
            candidate = await rebuildHandler.AccumulateAsync(identity, candidate, history, TestContext.Current.CancellationToken);
        }

        DomainProjectionRebuildPlan plan = await rebuildHandler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken);
        var batch = new ReadModelBatch(new ReadModelBatchScope(plan.StoreName, identity.TenantId, "party",
            PartySdkReadModelAddresses.SharedIndexAggregateId, PartyProjectionNames.Index, "rebuilt"), plan.Operations);
        (await rebuiltStore.ExecuteAsync(batch, TestContext.Current.CancellationToken)).Status.ShouldBe(ReadModelBatchStatus.Completed);
        string indexKey = PartySdkReadModelAddresses.Index(identity.TenantId);
        PartyIndexSdkReadModel live = liveStore.Deserialize<PartyIndexSdkReadModel>(indexKey);
        PartyIndexSdkReadModel rebuilt = rebuiltStore.Deserialize<PartyIndexSdkReadModel>(indexKey);
        JsonSerializer.Serialize(rebuilt, s_canonicalJsonOptions).ShouldBe(JsonSerializer.Serialize(live, s_canonicalJsonOptions));
        rebuilt.LastIndexedEvents.Keys.Order(StringComparer.Ordinal).ShouldBe(["party-1", "party-2"]);
        rebuilt.LastIndexedEvents[first.AggregateId].ShouldBe(new PartyIndexEventMetadata(5, nameof(PartyCreated), first.Events[4].Timestamp));
        rebuilt.Entries[first.AggregateId].LastModifiedAt.ShouldBe(first.Events[1].Timestamp);
        rebuilt.ErasureSequenceNumbers.ContainsKey(erased.AggregateId).ShouldBeTrue();

        (await rebuildHandler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await search.Received(1).NotifyIndexedAsync(identity.TenantId,
            Arg.Is<PartyIndexEntry>(entry => entry.Id == first.AggregateId), "PartyProjectionRebuilt",
            first.Events[1].Timestamp, Arg.Any<long?>(), Arg.Any<CancellationToken>());
        await search.Received(1).NotifyIndexedAsync(identity.TenantId,
            Arg.Is<PartyIndexEntry>(entry => entry.Id == second.AggregateId), "PartyProjectionRebuilt",
            second.Events[0].Timestamp, Arg.Any<long?>(), Arg.Any<CancellationToken>());

        var retryHandler = new PartyIndexSdkProjectionHandler(rebuiltStore, s_options, search);
        ReadModelEntry<PartyIndexSdkReadModel> before = await rebuiltStore.GetAsync<PartyIndexSdkReadModel>(
            "statestore", indexKey, TestContext.Current.CancellationToken);
        (await retryHandler.ProjectAsync(first, "post-rebuild-retry", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await rebuiltStore.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, TestContext.Current.CancellationToken))
            .ETag.ShouldBe(before.ETag);
        await search.Received(1).NotifyIndexedAsync(identity.TenantId,
            Arg.Is<PartyIndexEntry>(entry => entry.Id == first.AggregateId), nameof(PartyCreated),
            first.Events[4].Timestamp, Arg.Any<long?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SharedIndexRebuild_ExistingIndexRequiresSnapshotEtagAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(CreateIndex("party-live"), "index-etag"));
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);

        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(
            identity,
            candidate,
            TestContext.Current.CancellationToken);

        plan.Operations.ShouldHaveSingleItem().Concurrency.ShouldBe(ReadModelBatchConcurrency.Match("index-etag"));
    }

    [Fact]
    public async Task SharedIndexRebuild_FinalizeOnlyReturnsPlanWithoutExternalSearchWritesAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(
            identity,
            candidate,
            CreateRequest(),
            TestContext.Current.CancellationToken);

        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(
            identity,
            candidate,
            TestContext.Current.CancellationToken);

        plan.Operations.ShouldHaveSingleItem();
        await searchIndexer.DidNotReceiveWithAnyArgs().NotifyIndexedAsync(default!, default!, default!, default, Arg.Any<long?>(), default);
        await searchIndexer.DidNotReceiveWithAnyArgs().NotifyRemovedAsync(default!, default!, default);
    }

    [Fact]
    public async Task SharedIndexRebuild_CompletionReindexesCandidateAndRemovesStaleEntriesAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        // FinalizeAsync reads the pre-rebuild index (still has "stale-party"); by the time
        // CompleteRebuildAsync runs, the rebuild plan's write has already applied and the
        // canonical store no longer has it — CompleteRebuildAsync re-reads fresh to confirm.
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(
                new ReadModelEntry<PartyIndexSdkReadModel>(CreateIndex("stale-party"), "etag-1"),
                new ReadModelEntry<PartyIndexSdkReadModel>(PartyIndexSdkProjectionHandler.Fold(CreateRequest(), current: null), "etag-2"));
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        searchIndexer.NotifyIndexedAsync(
                Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        searchIndexer.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, CreateRequest(), TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken);

        DomainProjectionHandlerResult result = await handler.CompleteRebuildAsync(
            identity,
            candidate,
            plan.CompletionState,
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await searchIndexer.Received(1).NotifyRemovedAsync("tenant-a", "stale-party", Arg.Any<CancellationToken>());
        await searchIndexer.Received(1).NotifyIndexedAsync(
            "tenant-a",
            Arg.Is<PartyIndexEntry>(entry => entry != null && entry.Id == "party-1"),
            "PartyProjectionRebuilt",
            DateTimeOffset.UnixEpoch.AddSeconds(1),
            Arg.Any<long?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SharedIndexRebuild_CompletionDoesNotNotifyRemovalForAPartyStillPresentAsync()
    {
        // A live write can (re)add a party during the rebuild-accumulation window, after
        // FinalizeAsync already snapshotted it as absent from the rebuilt candidate. By the time
        // CompleteRebuildAsync runs, the canonical store reflects that live write again, so the
        // still-live party must not be reported as removed to the search indexer.
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(
                new ReadModelEntry<PartyIndexSdkReadModel>(CreateIndex("stale-party"), "etag-1"),
                new ReadModelEntry<PartyIndexSdkReadModel>(CreateIndex("stale-party"), "etag-2"));
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        searchIndexer.NotifyIndexedAsync(
                Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        searchIndexer.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, CreateRequest(), TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken);

        DomainProjectionHandlerResult result = await handler.CompleteRebuildAsync(
            identity,
            candidate,
            plan.CompletionState,
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await searchIndexer.DidNotReceive().NotifyRemovedAsync("tenant-a", "stale-party", Arg.Any<CancellationToken>());
        await searchIndexer.Received(1).NotifyIndexedAsync("tenant-a", Arg.Is<PartyIndexEntry>(entry => entry.Id == "stale-party"),
            "PartyProjectionRebuilt", Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SharedIndexRebuild_CompletionPublishesLatestCanonicalEntryAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexSdkReadModel latest = CreateIndex("party-1");
        PartyIndexEntry latestEntry = latest.Entries["party-1"] with
        {
            DisplayName = "Updated after rebuild commit",
            SortName = "updated-after-rebuild",
            LastModifiedAt = DateTimeOffset.UnixEpoch.AddHours(2),
        };
        latest = latest with
        {
            Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["party-1"] = latestEntry,
            },
        };
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(
                new ReadModelEntry<PartyIndexSdkReadModel>(null, null),
                new ReadModelEntry<PartyIndexSdkReadModel>(latest, "etag-live"));
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        searchIndexer.NotifyIndexedAsync(
                Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, CreateRequest(), TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken);

        DomainProjectionHandlerResult result = await handler.CompleteRebuildAsync(
            identity,
            candidate,
            plan.CompletionState,
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await searchIndexer.Received(1).NotifyIndexedAsync(
            "tenant-a",
            Arg.Is<PartyIndexEntry>(entry => entry != null && entry.DisplayName == "Updated after rebuild commit"),
            "PartyProjectionRebuilt",
            DateTimeOffset.UnixEpoch.AddHours(2),
            Arg.Any<long?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SharedIndexRebuild_CompletionSkipsManifestEntryErasedAfterCommitAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(
                new ReadModelEntry<PartyIndexSdkReadModel>(null, null),
                new ReadModelEntry<PartyIndexSdkReadModel>(CreateIndex(), "etag-erased"));
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        searchIndexer.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, CreateRequest(), TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken);

        DomainProjectionHandlerResult result = await handler.CompleteRebuildAsync(
            identity,
            candidate,
            plan.CompletionState,
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await searchIndexer.DidNotReceiveWithAnyArgs().NotifyIndexedAsync(default!, default!, default!, default, Arg.Any<long?>(), default);
    }

    [Fact]
    public async Task SharedIndexRebuild_EmptyInventoryProducesEmptyReplacementThatPrunesStaleEntriesAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);

        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(
            identity,
            candidate,
            TestContext.Current.CancellationToken);

        PartyIndexSdkReadModel rebuilt = JsonSerializer.Deserialize<PartyIndexSdkReadModel>(
            plan.Operations.Single().CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        rebuilt.Entries.ShouldBeEmpty();
        rebuilt.LastSequenceNumbers.ShouldBeEmpty();
    }

    [Fact]
    public async Task SharedIndexRebuild_AccumulateWithEmptyAggregateHistoryDoesNotThrowAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        ProjectionRequest emptyHistory = CreateRequest() with { Events = [] };

        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);

        candidate = await handler.AccumulateAsync(identity, candidate, emptyHistory, TestContext.Current.CancellationToken);

        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken);
        PartyIndexSdkReadModel rebuilt = JsonSerializer.Deserialize<PartyIndexSdkReadModel>(
            plan.Operations.Single().CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        rebuilt.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task SharedIndexRebuild_UnresolvedEventFailsWithoutUpdatingCandidateAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        ProjectionRequest unresolved = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.AccumulateAsync(
                identity,
                candidate,
                unresolved,
                TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("unresolved-or-unsupported-event");
    }

    [Fact]
    public async Task SharedIndexRebuild_UnresolvedEventLogsUnresolvedDiagnosticAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var logger = new RecordingLogger<PartyIndexSdkProjectionHandler>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer: null, logger: logger);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        ProjectionRequest unresolved = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.AccumulateAsync(
                identity,
                candidate,
                unresolved,
                TestContext.Current.CancellationToken));

        (LogLevel Level, string Message, Exception? Exception) record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain("Completed=False");
        record.Message.ShouldContain("UnknownType=1");
        record.Message.ShouldNotContain("TotallyUnknownEventType");
        record.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task Eraser_RedactsAllCanonicalModelsInOneCoordinatedBatchAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        string detailKey = PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
        string processingKey = PartySdkReadModelAddresses.Processing("tenant-a", "party-1");
        string indexKey = PartySdkReadModelAddresses.Index("tenant-a");

        PartyDetail existingDetail = PartyDetailSdkProjectionHandler.Fold(CreateRequest(), current: null).Detail!;
        var existingDetailModel = new PartyDetailSdkReadModel { Detail = existingDetail, LastSequenceNumber = 2 };
        store.GetAsync<PartyDetailSdkReadModel>("statestore", detailKey, Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(existingDetailModel, "detail-etag"));

        var existingProcessing = new PartyProcessingSdkReadModel
        {
            Records = [new ProcessingActivityRecord
            {
                SequenceNumber = 1,
                PartyId = "party-1",
                TenantId = "tenant-a",
                ActorId = "system",
                CorrelationId = "unspecified",
                OperationCategory = "PartyCommand",
                Outcome = "Succeeded",
                EventType = "PartyCreated",
                Timestamp = DateTimeOffset.UnixEpoch,
                Summary = "Party record created.",
            }],
            LastSequenceNumber = 2,
        };
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", processingKey, Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(existingProcessing, "processing-etag"));

        store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(CreateIndex("party-1", "party-2"), "index-etag"));
        ReadModelBatch? persistedBatch = null;
        batchStore.ExecuteAsync(Arg.Do<ReadModelBatch>(value => persistedBatch = value), Arg.Any<CancellationToken>())
            .Returns(ReadModelBatchResult.Completed("fingerprint"));
        var eraser = new PartySdkReadModelEraser(store, batchStore, s_options);

        await eraser.EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);

        persistedBatch.ShouldNotBeNull();
        persistedBatch.Operations.Count.ShouldBe(3);
        PartyDetailSdkReadModel persistedDetail = DeserializeOperation<PartyDetailSdkReadModel>(persistedBatch, detailKey);
        persistedDetail.Detail.ShouldNotBeNull();
        persistedDetail.Detail.IsErased.ShouldBeTrue();
        persistedDetail.Detail.DisplayName.ShouldBeEmpty();
        persistedDetail.LastSequenceNumber.ShouldBe(2);
        persistedDetail.ErasureSequenceNumber.ShouldBe(2);

        PartyProcessingSdkReadModel persistedProcessing = DeserializeOperation<PartyProcessingSdkReadModel>(persistedBatch, processingKey);
        persistedProcessing.Records.ShouldHaveSingleItem();
        persistedProcessing.LastSequenceNumber.ShouldBe(2);
        persistedProcessing.ErasureSequenceNumber.ShouldBe(2);

        PartyIndexSdkReadModel persistedIndex = DeserializeOperation<PartyIndexSdkReadModel>(persistedBatch, indexKey);
        persistedIndex.Entries.Keys.ShouldBe(["party-2"]);
        persistedIndex.LastSequenceNumbers["party-1"].ShouldBe(1);
        persistedIndex.LastSequenceNumbers["party-2"].ShouldBe(1);
        persistedIndex.ErasureSequenceNumbers["party-1"].ShouldBe(1);
    }

    [Fact]
    public async Task Eraser_SearchIndexerNonConvergenceDoesNotFailAnAlreadyCommittedBatchAsync()
    {
        // The canonical batch is the source of truth for "sdk-read-models" erasure certification.
        // A non-converging (optional, best-effort) Memories search notify must not turn a
        // successfully-committed batch into a reported cleanup failure.
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        ConfigureEmptyErasureReads(store);
        batchStore.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>())
            .Returns(ReadModelBatchResult.Completed("fingerprint"));
        searchIndexer.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var eraser = new PartySdkReadModelEraser(store, batchStore, s_options, searchIndexer);

        await eraser.EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);

        await batchStore.Received(1).ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
        await searchIndexer.Received(1).NotifyRemovedAsync("tenant-a", "party-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Eraser_SearchIndexerExceptionDoesNotFailAnAlreadyCommittedBatchAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        ConfigureEmptyErasureReads(store);
        batchStore.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>())
            .Returns(ReadModelBatchResult.Completed("fingerprint"));
        searchIndexer.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("index failure with personal data")));
        var eraser = new PartySdkReadModelEraser(store, batchStore, s_options, searchIndexer);

        await eraser.EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);

        await batchStore.Received(1).ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Eraser_SearchIndexerCancellationPropagatesAfterCommittedBatchAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        ConfigureEmptyErasureReads(store);
        batchStore.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>())
            .Returns(ReadModelBatchResult.Completed("fingerprint"));
        searchIndexer.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new OperationCanceledException()));
        var eraser = new PartySdkReadModelEraser(store, batchStore, s_options, searchIndexer);

        await Should.ThrowAsync<OperationCanceledException>(() => eraser.EraseAsync(
            "tenant-a",
            "party-1",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Eraser_ResetProcessingCheckpoint_NoExistingRowReturnsEmptyModel()
    {
        PartyProcessingSdkReadModel result = PartySdkReadModelEraser.ResetProcessingCheckpoint(current: null);

        result.Records.ShouldBeEmpty();
        result.LastSequenceNumber.ShouldBe(long.MinValue);
        result.ErasureSequenceNumber.ShouldBe(long.MinValue);
    }

    [Fact]
    public void Eraser_RedactDetail_NoExistingRowReturnsEmptyModel()
    {
        // Intentional empty-row semantics: do not invent an IsErased tombstone when no detail
        // was ever projected. Authoritative erasure status remains IPartyErasureRecordStore.
        PartyDetailSdkReadModel result = PartySdkReadModelEraser.RedactDetail(current: null, partyId: "party-1");

        result.Detail.ShouldBeNull();
        result.LastSequenceNumber.ShouldBe(long.MinValue);
        result.ErasureSequenceNumber.ShouldBe(long.MinValue);
    }

    [Fact]
    public void Eraser_RedactDetail_ExistingRowRetainsSequenceWatermark()
    {
        var current = new PartyDetailSdkReadModel
        {
            Detail = new PartyDetail
            {
                Id = "party-1",
                Type = PartyType.Person,
                DisplayName = "Ada",
                SortName = "Ada",
            },
            LastSequenceNumber = 7,
            ProjectionVersion = "7",
        };

        PartyDetailSdkReadModel result = PartySdkReadModelEraser.RedactDetail(current, partyId: "party-1");

        result.Detail.ShouldNotBeNull();
        result.Detail.IsErased.ShouldBeTrue();
        result.LastSequenceNumber.ShouldBe(7);
        result.ErasureSequenceNumber.ShouldBe(7);
        result.ProjectionVersion.ShouldBe("7");
    }

    [Fact]
    public async Task Eraser_IndeterminateBatchFailsWithoutIndependentWritesOrSearchSuccessAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        ConfigureEmptyErasureReads(store);
        batchStore.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>())
            .Returns(ReadModelBatchResult.Indeterminate("fingerprint", "transaction-dispatch"));
        var eraser = new PartySdkReadModelEraser(store, batchStore, s_options, searchIndexer);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => eraser.EraseAsync(
            "tenant-a",
            "party-1",
            TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("sdk-read-model-cleanup-failed");
        await batchStore.Received(1).ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
        await store.DidNotReceiveWithAnyArgs().TrySaveAsync(
            default!, default!, default(PartyDetailSdkReadModel)!, default!, default);
        await searchIndexer.DidNotReceiveWithAnyArgs().NotifyRemovedAsync(default!, default!, default);
    }

    [Fact]
    public async Task Eraser_OptimisticConflictReloadsAllRowsWithFrozenTimestampAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        ConfigureEmptyErasureReads(store, "etag-1", "etag-2");
        PartyIndexSdkReadModel initial = PartyIndexSdkProjectionHandler.Fold(CreateRequest(), CreateIndex("party-2"));
        initial = PartyIndexSdkProjectionHandler.Fold(CreateRequest() with
        {
            AggregateId = "party-2",
            Events = [Event(new PartyDeactivated(), 2, DateTimeOffset.UnixEpoch.AddSeconds(2))],
        }, initial);
        PartyIndexSdkReadModel concurrent = PartyIndexSdkProjectionHandler.Fold(CreateRequest() with
        {
            AggregateId = "party-2",
            Events = [Event(new PartyReactivated(), 3, DateTimeOffset.UnixEpoch.AddSeconds(3))],
        }, initial);
        string indexKey = PartySdkReadModelAddresses.Index("tenant-a");
        store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(initial, "etag-1"),
                new ReadModelEntry<PartyIndexSdkReadModel>(concurrent, "etag-2"));
        var batches = new List<ReadModelBatch>();
        batchStore.ExecuteAsync(
                Arg.Do<ReadModelBatch>(batches.Add),
                Arg.Any<CancellationToken>())
            .Returns(
                ReadModelBatchResult.OptimisticConflict("first", "transaction-precondition"),
                ReadModelBatchResult.Completed("second"));
        var eraser = new PartySdkReadModelEraser(store, batchStore, s_options);

        await eraser.EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);

        batches.Count.ShouldBe(2);
        batches[0].Scope.BatchId.ShouldNotBe(batches[1].Scope.BatchId);
        PartyDetailSdkReadModel first = DeserializeOperation<PartyDetailSdkReadModel>(
            batches[0], PartySdkReadModelAddresses.Detail("tenant-a", "party-1"));
        PartyDetailSdkReadModel second = DeserializeOperation<PartyDetailSdkReadModel>(
            batches[1], PartySdkReadModelAddresses.Detail("tenant-a", "party-1"));
        second.ErasedAt.ShouldBe(first.ErasedAt);
        second.ProjectedAt.ShouldBe(first.ProjectedAt);
        PartyIndexSdkReadModel firstIndex = DeserializeOperation<PartyIndexSdkReadModel>(batches[0], indexKey);
        PartyIndexSdkReadModel secondIndex = DeserializeOperation<PartyIndexSdkReadModel>(batches[1], indexKey);
        firstIndex.LastIndexedEvents.Keys.ShouldBe(["party-2"]);
        firstIndex.LastIndexedEvents["party-2"].ShouldBe(initial.LastIndexedEvents["party-2"]);
        secondIndex.Entries.Keys.ShouldBe(["party-2"]);
        secondIndex.Entries["party-2"].IsActive.ShouldBeTrue();
        secondIndex.LastIndexedEvents.Keys.ShouldBe(["party-2"]);
        secondIndex.LastIndexedEvents["party-2"].ShouldBe(concurrent.LastIndexedEvents["party-2"]);
        secondIndex.LastIndexedEvents["party-2"].SequenceNumber.ShouldBe(3);
        secondIndex.ErasureSequenceNumbers["party-1"].ShouldBe(2);
        batches[1].Operations.Single(operation => operation.Key == indexKey).Concurrency
            .ShouldBe(ReadModelBatchConcurrency.Match("etag-2"));
    }

    [Fact]
    public async Task Eraser_IncompleteBatchResumesWithExactSameAttemptIdentityAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        ConfigureEmptyErasureReads(store);
        var batches = new List<ReadModelBatch>();
        batchStore.ExecuteAsync(Arg.Do<ReadModelBatch>(batches.Add), Arg.Any<CancellationToken>())
            .Returns(
                ReadModelBatchResult.Incomplete("fingerprint", "transaction-marker-unverified"),
                ReadModelBatchResult.Completed("fingerprint"));
        var eraser = new PartySdkReadModelEraser(store, batchStore, s_options);

        await eraser.EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);

        batches.Count.ShouldBe(2);
        batches[1].ShouldBeSameAs(batches[0]);
        batches[1].Scope.BatchId.ShouldBe(batches[0].Scope.BatchId);
    }

    [Fact]
    public void Eraser_TransformsAreIdempotentAcrossCleanupRetries()
    {
        DateTimeOffset firstCleanup = DateTimeOffset.UnixEpoch.AddHours(1);
        DateTimeOffset laterRetry = firstCleanup.AddHours(1);
        PartyDetailSdkReadModel detail = PartySdkReadModelEraser.RedactDetail(
            new PartyDetailSdkReadModel
            {
                Detail = new PartyDetail { Id = "party-1", Type = PartyType.Person, DisplayName = "Ada", SortName = "Ada" },
                LastSequenceNumber = 7,
                ProjectedAt = DateTimeOffset.UnixEpoch,
                ProjectionVersion = "7",
            },
            "party-1",
            firstCleanup);
        PartyProcessingSdkReadModel processing = PartySdkReadModelEraser.ResetProcessingCheckpoint(
            new PartyProcessingSdkReadModel { LastSequenceNumber = 7, ProjectedAt = DateTimeOffset.UnixEpoch, ProjectionVersion = "7" },
            firstCleanup);
        PartyIndexSdkReadModel index = PartySdkReadModelEraser.RemoveParty(CreateIndex("party-1"), "party-1", firstCleanup);

        JsonSerializer.Serialize(PartySdkReadModelEraser.RedactDetail(detail, "party-1", laterRetry), s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(detail, s_canonicalJsonOptions));
        JsonSerializer.Serialize(PartySdkReadModelEraser.ResetProcessingCheckpoint(processing, laterRetry), s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(processing, s_canonicalJsonOptions));
        JsonSerializer.Serialize(PartySdkReadModelEraser.RemoveParty(index, "party-1", laterRetry), s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(index, s_canonicalJsonOptions));
    }

    [Fact]
    public void CleanupThenDelayedOldEventsCannotRestoreAnyCanonicalReadModel()
    {
        DateTimeOffset cleanupAt = DateTimeOffset.UnixEpoch.AddMinutes(5);
        PartyDetailSdkReadModel originalDetail = PartyDetailSdkProjectionHandler.Fold(CreateRequest(), current: null);
        PartyProcessingSdkReadModel originalProcessing = PartyProcessingActivityFold.Fold(CreateRequest(), current: null);
        PartyIndexSdkReadModel originalIndex = PartyIndexSdkProjectionHandler.Fold(CreateRequest(), current: null);
        PartyDetailSdkReadModel cleanedDetail = PartySdkReadModelEraser.RedactDetail(originalDetail, "party-1", cleanupAt);
        PartyProcessingSdkReadModel cleanedProcessing = PartySdkReadModelEraser.ResetProcessingCheckpoint(originalProcessing, cleanupAt);
        PartyIndexSdkReadModel cleanedIndex = PartySdkReadModelEraser.RemoveParty(originalIndex, "party-1", cleanupAt);
        ProjectionRequest delayedAndReused = CreateRequest() with
        {
            Events =
            [
                Event(new PartyCreated { Type = PartyType.Organization }, 1, cleanupAt.AddMinutes(1)),
                Event(new PartyCreated { Type = PartyType.Organization }, 3, cleanupAt.AddMinutes(2)),
            ],
        };

        PartyDetailSdkReadModel detail = PartyDetailSdkProjectionHandler.Fold(delayedAndReused, cleanedDetail);
        PartyProcessingSdkReadModel processing = PartyProcessingActivityFold.Fold(delayedAndReused, cleanedProcessing);
        PartyIndexSdkReadModel index = PartyIndexSdkProjectionHandler.Fold(delayedAndReused, cleanedIndex);

        detail.Detail.ShouldNotBeNull();
        detail.Detail.IsErased.ShouldBeTrue();
        detail.Detail.CreatedAt.ShouldBe(originalDetail.Detail!.CreatedAt);
        detail.Detail.DisplayName.ShouldBeEmpty();
        processing.Records.Count.ShouldBe(originalProcessing.Records.Count);
        processing.Records.ShouldNotContain(static record => record.SequenceNumber == 3);
        index.Entries.ContainsKey("party-1").ShouldBeFalse();
        index.ErasureSequenceNumbers.ContainsKey("party-1").ShouldBeTrue();
    }

    /// <summary>Persists payload erasure instants independently of audit and freshness envelope timestamps.</summary>
    /// <param name="rebuild">Whether to rebuild existing mismatched tombstones instead of delivering incrementally.</param>
    /// <param name="offsetHours">The payload timestamp's UTC offset.</param>
    /// <param name="envelopeDelayMinutes">The envelope instant's difference from the payload instant.</param>
    [Theory]
    [InlineData(false, 0, 15)]
    [InlineData(false, 2, -15)]
    [InlineData(false, -7, 15)]
    [InlineData(true, 0, 15)]
    [InlineData(true, 2, -15)]
    [InlineData(true, -7, 15)]
    public async Task DetailAndProcessingTombstonesUsePayloadErasureInstantAsync(
        bool rebuild,
        int offsetHours,
        int envelopeDelayMinutes)
    {
        DateTimeOffset erasedAt = DateTimeOffset.UnixEpoch.AddDays(1).ToOffset(TimeSpan.FromHours(offsetHours));
        DateTimeOffset envelopeAt = erasedAt.AddMinutes(envelopeDelayMinutes).ToOffset(TimeSpan.FromHours(3));
        ProjectionRequest creation = CreateRequest();
        ProjectionEventDto erased = Event(new PartyErased
        {
            PartyId = creation.AggregateId,
            TenantId = creation.TenantId,
            ErasedAt = erasedAt,
        }, 3, envelopeAt);
        ProjectionRequest history = creation with { Events = [.. creation.Events, erased] };
        string detailKey = PartySdkReadModelAddresses.Detail(creation.TenantId, creation.AggregateId);
        string processingKey = PartySdkReadModelAddresses.Processing(creation.TenantId, creation.AggregateId);
        var store = new InMemoryReadModelBatchStore();
        var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        (await handler.ProjectAsync(creation, "create-before-erasure", cancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        if (rebuild)
        {
            // Previously checkpointed processing tombstones used the envelope time.
            await store.SaveAsync("statestore", processingKey,
                PartyProcessingActivityFold.Fold(history, current: null) with { ErasedAt = envelopeAt.ToUniversalTime() },
                cancellationToken);
            await store.SaveAsync("statestore", detailKey,
                PartyDetailSdkProjectionHandler.Fold(history, current: null), cancellationToken);
            string legacyDetail = JsonSerializer.Serialize(store.Deserialize<PartyDetailSdkReadModel>(detailKey), s_canonicalJsonOptions);
            string legacyProcessing = JsonSerializer.Serialize(store.Deserialize<PartyProcessingSdkReadModel>(processingKey), s_canonicalJsonOptions);
            (await handler.ProjectAsync(history, "legacy-erasure-redelivery", cancellationToken))
                .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
            JsonSerializer.Serialize(store.Deserialize<PartyDetailSdkReadModel>(detailKey), s_canonicalJsonOptions).ShouldBe(legacyDetail);
            JsonSerializer.Serialize(store.Deserialize<PartyProcessingSdkReadModel>(processingKey), s_canonicalJsonOptions).ShouldBe(legacyProcessing);

            DomainProjectionRebuildPlan plan = await handler.PrepareRebuildAsync(history, "rebuild-erasure", cancellationToken);
            var scope = new ReadModelBatchScope(plan.StoreName, history.TenantId, history.Domain,
                history.AggregateId, PartyProjectionNames.Detail, "rebuild-erasure");
            (await store.ExecuteAsync(new ReadModelBatch(scope, plan.Operations), cancellationToken))
                .Status.ShouldBe(ReadModelBatchStatus.Completed);
        }
        else
        {
            (await handler.ProjectAsync(history with { Events = [erased] }, "erase-incrementally", cancellationToken))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }

        PartyDetailSdkReadModel detail = store.Deserialize<PartyDetailSdkReadModel>(detailKey);
        PartyProcessingSdkReadModel processing = store.Deserialize<PartyProcessingSdkReadModel>(processingKey);
        detail.ErasedAt.ShouldBe(erasedAt);
        processing.ErasedAt.ShouldBe(erasedAt);
        // Preserve the authoritative payload representation, including a non-UTC offset.
        detail.ErasedAt.GetValueOrDefault().Offset.ShouldBe(erasedAt.Offset);
        processing.ErasedAt.GetValueOrDefault().Offset.ShouldBe(erasedAt.Offset);
        detail.ErasureSequenceNumber.ShouldBe(3);
        processing.ErasureSequenceNumber.ShouldBe(3);
        detail.LastSequenceNumber.ShouldBe(3);
        processing.LastSequenceNumber.ShouldBe(3);
        detail.ProjectedAt.ShouldBe(envelopeAt.ToUniversalTime());
        processing.ProjectedAt.ShouldBe(envelopeAt.ToUniversalTime());
        detail.Detail.ShouldNotBeNull().IsErased.ShouldBeTrue();
        detail.Detail.DisplayName.ShouldBeEmpty();
        detail.Detail.PersonDetails.ShouldBeNull();
        detail.Detail.ErasedAt.GetValueOrDefault().EqualsExact(erasedAt).ShouldBeTrue();
        detail.Detail.LastModifiedAt.EqualsExact(erasedAt).ShouldBeTrue();
        processing.Records.Select(static record => record.SequenceNumber).ShouldBe([1L, 2L, 3L]);
        ProcessingActivityRecord erasureRecord = processing.Records.Single(static record => record.SequenceNumber == 3);
        erasureRecord.Timestamp.ShouldBe(envelopeAt.ToUniversalTime());
        erasureRecord.Timestamp.Offset.ShouldBe(TimeSpan.Zero);
        erasureRecord.OperationCategory.ShouldBe("Erasure");
        erasureRecord.Outcome.ShouldBe("Succeeded");

        string beforeDetail = JsonSerializer.Serialize(detail, s_canonicalJsonOptions);
        string beforeProcessing = JsonSerializer.Serialize(processing, s_canonicalJsonOptions);
        (await handler.ProjectAsync(history, "erasure-redelivery", cancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        JsonSerializer.Serialize(store.Deserialize<PartyDetailSdkReadModel>(detailKey), s_canonicalJsonOptions).ShouldBe(beforeDetail);
        JsonSerializer.Serialize(store.Deserialize<PartyProcessingSdkReadModel>(processingKey), s_canonicalJsonOptions).ShouldBe(beforeProcessing);

        ProjectionEventDto tail = Event(new PartyReactivated(), 4, envelopeAt.AddHours(1));
        (await handler.ProjectAsync(history with { Events = [tail] }, "after-erasure", cancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        PartyDetailSdkReadModel advancedDetail = store.Deserialize<PartyDetailSdkReadModel>(detailKey);
        PartyProcessingSdkReadModel advancedProcessing = store.Deserialize<PartyProcessingSdkReadModel>(processingKey);
        advancedDetail.ErasedAt.GetValueOrDefault().EqualsExact(erasedAt).ShouldBeTrue();
        advancedProcessing.ErasedAt.GetValueOrDefault().EqualsExact(erasedAt).ShouldBeTrue();
        advancedDetail.ErasureSequenceNumber.ShouldBe(3);
        advancedProcessing.ErasureSequenceNumber.ShouldBe(3);
        advancedDetail.LastSequenceNumber.ShouldBe(4);
        advancedProcessing.LastSequenceNumber.ShouldBe(4);
        advancedDetail.ProjectedAt.ShouldBe(tail.Timestamp.ToUniversalTime());
        advancedProcessing.ProjectedAt.ShouldBe(tail.Timestamp.ToUniversalTime());
        advancedDetail.Detail.ShouldNotBeNull().IsErased.ShouldBeTrue();
        advancedDetail.Detail.ErasedAt.GetValueOrDefault().EqualsExact(erasedAt).ShouldBeTrue();
        advancedDetail.Detail.LastModifiedAt.EqualsExact(erasedAt).ShouldBeTrue();
        advancedProcessing.Records.ShouldBe(processing.Records);

        var eraser = new PartySdkReadModelEraser(store, store, s_options);
        await eraser.EraseAsync(history.TenantId, history.AggregateId, cancellationToken);
        PartyDetailSdkReadModel cleanedDetail = store.Deserialize<PartyDetailSdkReadModel>(detailKey);
        PartyProcessingSdkReadModel cleanedProcessing = store.Deserialize<PartyProcessingSdkReadModel>(processingKey);
        cleanedDetail.ErasedAt.GetValueOrDefault().EqualsExact(erasedAt).ShouldBeTrue();
        cleanedProcessing.ErasedAt.GetValueOrDefault().EqualsExact(erasedAt).ShouldBeTrue();
        cleanedDetail.Detail.ShouldNotBeNull().IsErased.ShouldBeTrue();
        cleanedDetail.Detail.ErasedAt.GetValueOrDefault().EqualsExact(erasedAt).ShouldBeTrue();
        cleanedDetail.Detail.LastModifiedAt.EqualsExact(erasedAt).ShouldBeTrue();
        cleanedDetail.ErasureSequenceNumber.ShouldBe(4);
        cleanedProcessing.ErasureSequenceNumber.ShouldBe(4);
        cleanedDetail.LastSequenceNumber.ShouldBe(4);
        cleanedProcessing.LastSequenceNumber.ShouldBe(4);
        cleanedDetail.ProjectedAt.ShouldBe(advancedDetail.ProjectedAt);
        cleanedProcessing.ProjectedAt.ShouldBe(advancedProcessing.ProjectedAt);
        cleanedProcessing.Records.ShouldBe(processing.Records);
    }

    [Fact]
    public async Task DetailHandler_PartyErasedRedactsPersonalDataUsingEventTimestampAsync()
    {
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                .. CreateRequest().Events,
                Event(new PartyErased
                {
                    PartyId = "party-1",
                    TenantId = "tenant-a",
                    ErasedAt = DateTimeOffset.UnixEpoch.AddMinutes(5),
                }, 3, DateTimeOffset.UnixEpoch.AddMinutes(5)),
            ],
        };

        PartyDetailSdkReadModel result = PartyDetailSdkProjectionHandler.Fold(request, current: null);

        result.Detail.ShouldNotBeNull();
        result.Detail.IsErased.ShouldBeTrue();
        result.Detail.DisplayName.ShouldBeEmpty();
        result.Detail.ErasedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(5));
        result.Detail.LastModifiedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(5));
    }

    [Fact]
    public void DetailFold_DuplicateAndOutOfOrderBatchMatchesOrderedReplayWithoutFreshnessDrift()
    {
        ProjectionRequest ordered = CreateRequest();
        ProjectionRequest duplicateAndOutOfOrder = ordered with
        {
            Events =
            [
                ordered.Events[1],
                ordered.Events[0],
                ordered.Events[0] with { Payload = [.. ordered.Events[0].Payload] },
            ],
        };

        PartyDetailSdkReadModel expected = PartyDetailSdkProjectionHandler.Fold(ordered, current: null);
        PartyDetailSdkReadModel actual = PartyDetailSdkProjectionHandler.Fold(duplicateAndOutOfOrder, current: null);

        JsonSerializer.Serialize(actual, s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(expected, s_canonicalJsonOptions));
        actual.ProjectedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(1));
    }

    [Fact]
    public void IndexFold_DuplicateAndOutOfOrderBatchMatchesOrderedReplayWithoutFreshnessDrift()
    {
        ProjectionRequest ordered = CreateRequest();
        ProjectionRequest duplicateAndOutOfOrder = ordered with
        {
            Events =
            [
                ordered.Events[1],
                ordered.Events[0],
                ordered.Events[0] with { Payload = [.. ordered.Events[0].Payload] },
            ],
        };

        PartyIndexSdkReadModel expected = PartyIndexSdkProjectionHandler.Fold(ordered, current: null);
        PartyIndexSdkReadModel actual = PartyIndexSdkProjectionHandler.Fold(duplicateAndOutOfOrder, current: null);

        JsonSerializer.Serialize(actual, s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(expected, s_canonicalJsonOptions));
        actual.ProjectedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(1));
    }

    [Fact]
    public void ProcessingActivityFold_ProjectsBoundedMetadataWithoutPersonalPayloadText()
    {
        const string firstName = "PersonalFirstName-should-not-leak";
        const string lastName = "PersonalLastName-should-not-leak";
        ProjectionRequest request = new(
            "tenant-a",
            "party",
            "party-1",
            [
                Event(new PersonDetailsUpdated
                {
                    PersonDetails = new PersonDetails
                    {
                        FirstName = firstName,
                        LastName = lastName,
                    },
                }, 1, DateTimeOffset.UnixEpoch),
            ]);

        PartyProcessingSdkReadModel result = PartyProcessingActivityFold.Fold(request, current: null);

        ProcessingActivityRecord record = result.Records.ShouldHaveSingleItem();
        record.SequenceNumber.ShouldBe(1);
        record.OperationCategory.ShouldBe("PartyCommand");
        record.EventType.ShouldBe(nameof(PersonDetailsUpdated));
        record.Summary.ShouldBe("Person details updated.");
        string json = JsonSerializer.Serialize(result, s_canonicalJsonOptions);
        json.ShouldNotContain(firstName);
        json.ShouldNotContain(lastName);
    }

    [Fact]
    public void ProcessingActivityFold_CorruptLiveEventIsRecordedAsFailedNotSucceeded()
    {
        ProjectionRequest request = new(
            "tenant-a",
            "party",
            "party-1",
            [new ProjectionEventDto(
                nameof(PartyCreated),
                "{ not valid json"u8.ToArray(),
                "json",
                1,
                DateTimeOffset.UnixEpoch,
                "correlation-1")]);

        PartyProcessingSdkReadModel result = PartyProcessingActivityFold.Fold(request, current: null);

        ProcessingActivityRecord record = result.Records.ShouldHaveSingleItem();
        record.OperationCategory.ShouldBe("Other");
        record.EventType.ShouldBe(nameof(PartyCreated));
        record.Outcome.ShouldBe("Failed");
        result.LastSequenceNumber.ShouldBe(1);
    }

    [Fact]
    public void ProcessingActivityFold_WholePayloadRedactedEventIsRecordedAsRedactedNotSucceeded()
    {
        ProjectionRequest request = new(
            "tenant-a",
            "party",
            "party-1",
            [new ProjectionEventDto(
                nameof(PartyCreated),
                "null"u8.ToArray(),
                "json-redacted",
                1,
                DateTimeOffset.UnixEpoch,
                "correlation-1")]);

        PartyProcessingSdkReadModel result = PartyProcessingActivityFold.Fold(request, current: null);

        ProcessingActivityRecord record = result.Records.ShouldHaveSingleItem();
        record.OperationCategory.ShouldBe("Other");
        record.EventType.ShouldBe(nameof(PartyCreated));
        record.Outcome.ShouldBe("Redacted");
        result.LastSequenceNumber.ShouldBe(1);
    }

    [Fact]
    public void DeserializeNew_UnknownEventType_DoesNotAdvanceCheckpointAndYieldsNoPayload()
    {
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        var results = PartySdkProjectionFold.DeserializeNew(request.Events, long.MinValue).ToList();

        (ProjectionEventDto Event, IEventPayload? Payload, bool AdvanceCheckpoint) single = results.ShouldHaveSingleItem();
        single.Payload.ShouldBeNull();
        single.AdvanceCheckpoint.ShouldBeFalse();
    }

    [Fact]
    public void DeserializeNew_NonJsonSerializationFormat_DoesNotAdvanceCheckpointAndYieldsNoPayload()
    {
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    nameof(PartyCreated),
                    "not-used"u8.ToArray(),
                    "protobuf",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        var results = PartySdkProjectionFold.DeserializeNew(request.Events, long.MinValue).ToList();

        (ProjectionEventDto Event, IEventPayload? Payload, bool AdvanceCheckpoint) single = results.ShouldHaveSingleItem();
        single.Payload.ShouldBeNull();
        single.AdvanceCheckpoint.ShouldBeFalse();
    }

    [Fact]
    public void DeserializeNew_UnknownEventType_LogsUnknownEventTypeDroppedWarning()
    {
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        _ = PartySdkProjectionFold.DeserializeNew(request.Events, long.MinValue, logger).ToList();

        (LogLevel Level, string Message, Exception? Exception) record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain("could not resolve event type");
        record.Message.ShouldNotContain("tenant-a");
        record.Message.ShouldNotContain("party-1");
    }

    [Fact]
    public void DeserializeNew_NonJsonFormat_LogsNonJsonEventDroppedWarning()
    {
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    nameof(PartyCreated),
                    "not-used"u8.ToArray(),
                    "protobuf",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        _ = PartySdkProjectionFold.DeserializeNew(request.Events, long.MinValue, logger).ToList();

        (LogLevel Level, string Message, Exception? Exception) record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain("non-JSON serialization format");
    }

    [Fact]
    public void DeserializeNew_AmbiguousShortEventTypeName_LogsAmbiguousEventTypeDroppedWarning()
    {
        // PartyEventTypeResolver.IsAmbiguousShortName has no true-positive case reachable through
        // the real Hexalith.Parties.Contracts assembly today (verified directly: 0 short-name
        // collisions among its 44 current event-payload types — PartyEventTypeResolverTests
        // already documents this as an unreachable branch). To prove DeserializeNew's
        // branch-selection logic actually calls AmbiguousEventTypeDropped (not
        // UnknownEventTypeDropped) when the resolver reports ambiguous, this test seeds the
        // resolver's private resolution cache with a synthetic ambiguous outcome for a unique
        // sentinel event-type name that can never collide with a real one — the same "GetOrAdd
        // returns the cached entry without invoking the factory" contract the resolver documents
        // for itself. No production type is added to the Contracts assembly.
        string sentinelEventTypeName = $"Test.Sentinel.Ambiguous.{Guid.NewGuid():N}";
        SeedEventTypeResolverCacheAsAmbiguous(sentinelEventTypeName);
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    sentinelEventTypeName,
                    "{}"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        var results = PartySdkProjectionFold.DeserializeNew(request.Events, long.MinValue, logger).ToList();

        (ProjectionEventDto Event, IEventPayload? Payload, bool AdvanceCheckpoint) single = results.ShouldHaveSingleItem();
        single.Payload.ShouldBeNull();
        single.AdvanceCheckpoint.ShouldBeFalse();
        (LogLevel Level, string Message, Exception? Exception) record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain("ambiguous short event-type name");
        record.Message.ShouldNotContain("could not resolve event type");
    }

    [Fact]
    public void DeserializeNew_CorruptLiveEvent_LogsPayloadDeserializationFailedWarningDistinctFromRedacted()
    {
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    nameof(PartyCreated),
                    "{ not valid json"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        var results = PartySdkProjectionFold.DeserializeNew(request.Events, long.MinValue, logger).ToList();

        (ProjectionEventDto Event, IEventPayload? Payload, bool AdvanceCheckpoint) single = results.ShouldHaveSingleItem();
        single.Payload.ShouldBeNull();
        single.AdvanceCheckpoint.ShouldBeTrue();
        (LogLevel Level, string Message, Exception? Exception) record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain("failed to deserialize live event");
        record.Message.ShouldContain("JsonException");
        record.Message.ShouldNotContain("redacted");
        record.Message.ShouldNotContain("tenant-a");
        record.Message.ShouldNotContain("party-1");
        // The raw exception object is never passed to ILogger: a syntax-level JsonException can
        // embed a fragment of the offending raw payload bytes in its own .Message, and most
        // logging sinks render exception.ToString() into the emitted log text. Only the exception
        // type name travels as a structured field (asserted above).
        record.Exception.ShouldBeNull();
    }

    [Fact]
    public void DeserializeNew_RedactedTailDecodeFailure_LogsRedactedEventDroppedInformationDistinctFromCorrupt()
    {
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    nameof(PartyCreated),
                    "{ not valid json"u8.ToArray(),
                    PartySdkProjectionFold.RedactedFormat,
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        var results = PartySdkProjectionFold.DeserializeNew(request.Events, long.MinValue, logger).ToList();

        (ProjectionEventDto Event, IEventPayload? Payload, bool AdvanceCheckpoint) single = results.ShouldHaveSingleItem();
        single.Payload.ShouldBeNull();
        single.AdvanceCheckpoint.ShouldBeTrue();
        (LogLevel Level, string Message, Exception? Exception) record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Information);
        record.Message.ShouldContain("dropped redacted event");
        record.Message.ShouldContain("JsonException");
        record.Message.ShouldNotContain("failed to deserialize live event");
        record.Message.ShouldNotContain("tenant-a");
        record.Message.ShouldNotContain("party-1");
        // Same no-raw-exception rule as the corrupt-live-event case above.
        record.Exception.ShouldBeNull();
    }

    [Fact]
    public void DeserializeNew_ResolvedTypeDecodesToNullWithoutRedaction_LogsNullPayloadEventDroppedWarning()
    {
        // A resolved, non-redacted event type can deserialize without throwing but still yield a
        // null instance (e.g. a literal JSON "null" body under serialization format "json", not
        // "json-redacted"). The checkpoint must not advance — the same non-advancing category as
        // an unresolved/non-JSON drop — and this previously logged nothing at all, contradicting
        // the method's own "every drop/skip emits a distinct diagnostic" doc comment.
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    nameof(PartyCreated),
                    "null"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        var results = PartySdkProjectionFold.DeserializeNew(request.Events, long.MinValue, logger).ToList();

        (ProjectionEventDto Event, IEventPayload? Payload, bool AdvanceCheckpoint) single = results.ShouldHaveSingleItem();
        single.Payload.ShouldBeNull();
        single.AdvanceCheckpoint.ShouldBeFalse();
        (LogLevel Level, string Message, Exception? Exception) record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain("produced no payload");
        record.Message.ShouldNotContain("tenant-a");
        record.Message.ShouldNotContain("party-1");
    }

    [Fact]
    public void DeserializeNew_WholePayloadRedacted_LogsWholePayloadRedactedEventDroppedInformation()
    {
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(
                    nameof(PartyCreated),
                    "null"u8.ToArray(),
                    PartySdkProjectionFold.RedactedFormat,
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ],
        };

        var results = PartySdkProjectionFold.DeserializeNew(request.Events, long.MinValue, logger).ToList();

        (ProjectionEventDto Event, IEventPayload? Payload, bool AdvanceCheckpoint) single = results.ShouldHaveSingleItem();
        single.Payload.ShouldBeNull();
        single.AdvanceCheckpoint.ShouldBeTrue();
        (LogLevel Level, string Message, Exception? Exception) record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Information);
        record.Message.ShouldContain("whole-payload-redacted");
        record.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task DetailHandler_IncrementalDispatchAcrossTwoDeliveriesMatchesSingleShotRebuildAsync()
    {
        // The pre-existing rebuild-vs-replay test compares PrepareRebuildAsync's output to a
        // second, separate call of the exact same static Fold function with the same full event
        // list — both sides run identical code once, so it cannot detect a real divergence
        // between genuine multi-dispatch incremental persistence and a full rebuild. This test
        // exercises two independent code paths: (1) two separate ProjectAsync calls against a
        // stateful store, simulating live incremental delivery, and (2) one PrepareRebuildAsync
        // call over the complete history, simulating a full rebuild — then asserts convergence.
        var inMemoryStore = new InMemoryReadModelBatchStore();
        var incrementalHandler = new PartyDetailSdkProjectionHandler(inMemoryStore, inMemoryStore, s_options);
        ProjectionRequest full = CreateRequest();
        ProjectionRequest firstDelivery = full with { Events = [full.Events[0]] };
        ProjectionRequest secondDelivery = full with { Events = [full.Events[1]] };

        DomainProjectionHandlerResult firstResult = await incrementalHandler.ProjectAsync(
            firstDelivery,
            "dispatch-1",
            TestContext.Current.CancellationToken);
        DomainProjectionHandlerResult secondResult = await incrementalHandler.ProjectAsync(
            secondDelivery,
            "dispatch-2",
            TestContext.Current.CancellationToken);

        firstResult.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        secondResult.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string detailKey = PartySdkReadModelAddresses.Detail(full.TenantId, full.AggregateId);
        string processingKey = PartySdkReadModelAddresses.Processing(full.TenantId, full.AggregateId);
        PartyDetailSdkReadModel incrementalDetail = inMemoryStore.Deserialize<PartyDetailSdkReadModel>(detailKey);
        PartyProcessingSdkReadModel incrementalProcessing = inMemoryStore.Deserialize<PartyProcessingSdkReadModel>(processingKey);

        var rebuildHandler = new PartyDetailSdkProjectionHandler(
            new InMemoryReadModelBatchStore(),
            new InMemoryReadModelBatchStore(),
            s_options);
        DomainProjectionRebuildPlan plan = await rebuildHandler.PrepareRebuildAsync(
            full,
            "rebuild-1",
            TestContext.Current.CancellationToken);
        PartyDetailSdkReadModel rebuiltDetail = JsonSerializer.Deserialize<PartyDetailSdkReadModel>(
            plan.Operations.Single(static operation => operation.Key.EndsWith(":detail", StringComparison.Ordinal)).CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        PartyProcessingSdkReadModel rebuiltProcessing = JsonSerializer.Deserialize<PartyProcessingSdkReadModel>(
            plan.Operations.Single(static operation => operation.Key.EndsWith(":processing-records", StringComparison.Ordinal)).CanonicalValue.Span,
            s_canonicalJsonOptions)!;

        Normalize(incrementalDetail).ShouldBe(Normalize(rebuiltDetail));
        incrementalDetail.LastSequenceNumber.ShouldBe(rebuiltDetail.LastSequenceNumber);
        incrementalProcessing.Records.Select(static record => record.SequenceNumber)
            .ShouldBe(rebuiltProcessing.Records.Select(static record => record.SequenceNumber));
        incrementalProcessing.LastSequenceNumber.ShouldBe(rebuiltProcessing.LastSequenceNumber);
    }

    /// <summary>
    /// Seeds <see cref="PartyEventTypeResolver"/>'s private resolution cache with a synthetic
    /// "ambiguous" outcome for <paramref name="eventTypeName"/> via reflection, so a test can
    /// exercise the true-ambiguous branch without a real short-name collision existing in the
    /// Contracts assembly. <c>ConcurrentDictionary.GetOrAdd</c> returns an existing entry without
    /// invoking its factory, so once seeded, <see cref="PartyEventTypeResolver.Resolve"/> and
    /// <see cref="PartyEventTypeResolver.IsAmbiguousShortName"/> both report this key as ambiguous
    /// for the lifetime of the test process — safe because the sentinel key is process-unique
    /// (caller-supplied GUID) and can never collide with a real event type name.
    /// </summary>
    private static void SeedEventTypeResolverCacheAsAmbiguous(string eventTypeName)
    {
        Type resolverType = typeof(PartyEventTypeResolver);
        FieldInfo cacheField = resolverType.GetField("s_resolvedCache", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("PartyEventTypeResolver.s_resolvedCache field not found.");
        object cache = cacheField.GetValue(null)
            ?? throw new InvalidOperationException("PartyEventTypeResolver.s_resolvedCache is null.");
        Type outcomeType = typeof(ResolveOutcome);
        PropertyInfo ambiguousProperty = outcomeType.GetProperty("Ambiguous", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("PartyEventTypeResolver.ResolveOutcome.Ambiguous property not found.");
        object ambiguousOutcome = ambiguousProperty.GetValue(null)
            ?? throw new InvalidOperationException("PartyEventTypeResolver.ResolveOutcome.Ambiguous returned null.");
        MethodInfo tryAdd = cache.GetType().GetMethod("TryAdd")
            ?? throw new InvalidOperationException("ConcurrentDictionary<string, ResolveOutcome>.TryAdd not found.");
        bool added = (bool)tryAdd.Invoke(cache, [eventTypeName, ambiguousOutcome])!;
        added.ShouldBeTrue("the sentinel event-type name must be unique and unseeded before this call.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsentRenewal_IncrementalPersistenceReplayAndRebuildConvergeAsync(bool revokeRenewal)
    {
        ConsentRecorded first = new()
        {
            PartyId = "party-1", TenantId = "tenant-a", ConsentId = "channel:marketing",
            ChannelId = "channel", Purpose = "marketing", LawfulBasis = LawfulBasis.Consent,
            GrantedAt = DateTimeOffset.UnixEpoch, GrantedBy = "first-user", Source = "first-source",
        };
        ConsentRevoked firstRevoke = new()
        {
            PartyId = "party-1", TenantId = "tenant-a", ConsentId = first.ConsentId,
            RevokedAt = DateTimeOffset.UnixEpoch.AddSeconds(1), RevokedBy = "first-revoker",
            Reason = "first-reason", Source = "first-revocation-source",
        };
        ConsentRecorded renewal = first with
        {
            GrantedAt = DateTimeOffset.UnixEpoch.AddSeconds(2), GrantedBy = "renewing-user", Source = "renewal-source",
        };
        List<IEventPayload> payloads =
        [
            new PartyCreated { Type = PartyType.Person }, first,
            first with { GrantedAt = first.GrantedAt.AddMilliseconds(100), Source = "active-retry" },
            firstRevoke,
            firstRevoke with { RevokedAt = firstRevoke.RevokedAt.AddMilliseconds(100), Reason = "already-revoked-retry" },
            first, // repeated old grant must not reopen a revoked lifecycle
            renewal, firstRevoke, // repeated old withdrawal must not close the renewal
            renewal, // exact duplicate grant
        ];
        if (revokeRenewal)
        {
            ConsentRevoked final = firstRevoke with
            {
                RevokedAt = DateTimeOffset.UnixEpoch.AddSeconds(3), RevokedBy = "final-revoker",
                Reason = "final-reason", Source = "final-revocation-source",
            };
            payloads.Add(final);
            payloads.Add(final with { RevokedAt = final.RevokedAt.AddSeconds(1), Reason = "retry" });
        }

        ProjectionRequest full = CreateRequest() with
        {
            Events = payloads.Select((payload, index) => Event(payload, index + 1,
                DateTimeOffset.UnixEpoch.AddSeconds(index))).ToArray(),
        };
        var store = new InMemoryReadModelBatchStore();
        var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        string key = PartySdkReadModelAddresses.Detail(full.TenantId, full.AggregateId);
        PartyState aggregateState = new();
        for (int index = 0; index < full.Events.Length; index++)
        {
            switch (payloads[index])
            {
                case PartyCreated created: aggregateState.Apply(created); break;
                case ConsentRecorded recorded: aggregateState.Apply(recorded); break;
                case ConsentRevoked revoked: aggregateState.Apply(revoked); break;
            }

            DomainProjectionHandlerResult result = await handler.ProjectAsync(
                full with { Events = [full.Events[index]] }, $"consent-dispatch-{index}",
                TestContext.Current.CancellationToken);
            result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
            PartyDetailSdkReadModel persistedStep = store.Deserialize<PartyDetailSdkReadModel>(key);
            persistedStep.Detail.ShouldNotBeNull().ConsentRecords.ShouldBe(aggregateState.ConsentRecords);
            persistedStep.LastSequenceNumber.ShouldBe(index + 1);
        }

        PartyDetailSdkReadModel persisted = store.Deserialize<PartyDetailSdkReadModel>(key);
        ConsentRecord[] records = persisted.Detail.ShouldNotBeNull().ConsentRecords.ToArray();
        records.Length.ShouldBe(2);
        records[0].GrantedBy.ShouldBe("first-user");
        records[0].Source.ShouldBe("first-source");
        records[0].RevokedAt.ShouldBe(firstRevoke.RevokedAt);
        records[0].RevokedBy.ShouldBe(firstRevoke.RevokedBy);
        records[0].RevocationReason.ShouldBe(firstRevoke.Reason);
        records[0].RevocationSource.ShouldBe(firstRevoke.Source);
        records[1].GrantedBy.ShouldBe("renewing-user");
        records[1].Source.ShouldBe("renewal-source");
        records[1].IsActive.ShouldBe(!revokeRenewal);
        records[1].RevocationReason.ShouldBe(revokeRenewal ? "final-reason" : null);
        records[1].RevokedBy.ShouldBe(revokeRenewal ? "final-revoker" : null);
        records[1].RevocationSource.ShouldBe(revokeRenewal ? "final-revocation-source" : null);

        string beforeDuplicateDelivery = JsonSerializer.Serialize(persisted, s_canonicalJsonOptions);
        await handler.ProjectAsync(full, "consent-duplicate-delivery", TestContext.Current.CancellationToken);
        JsonSerializer.Serialize(store.Deserialize<PartyDetailSdkReadModel>(key), s_canonicalJsonOptions)
            .ShouldBe(beforeDuplicateDelivery);
        // Pre-fix projection: the checkpoint advanced past renewal but its row was lost.
        ConsentRecord legacyFirst = revokeRenewal
            ? records[0] with
            {
                RevokedAt = records[1].RevokedAt, RevokedBy = records[1].RevokedBy,
                RevocationReason = records[1].RevocationReason, RevocationSource = records[1].RevocationSource,
            }
            : records[0]; // old projection overwrote the first lifecycle on the final withdrawal
        PartyDetailSdkReadModel legacy = persisted with
        {
            Detail = persisted.Detail! with { ConsentRecords = [legacyFirst] },
        };
        await store.SaveAsync("statestore", key, legacy, TestContext.Current.CancellationToken);
        await handler.ProjectAsync(full, "legacy-redelivery", TestContext.Current.CancellationToken);
        store.Deserialize<PartyDetailSdkReadModel>(key).Detail.ShouldNotBeNull()
            .ConsentRecords.ShouldHaveSingleItem().ShouldBe(legacyFirst);
        DomainProjectionRebuildPlan rebuild = await handler.PrepareRebuildAsync(
            full, "consent-rebuild", TestContext.Current.CancellationToken);
        PartyDetailSdkReadModel rebuilt = JsonSerializer.Deserialize<PartyDetailSdkReadModel>(
            rebuild.Operations.Single(operation => operation.Key == key).CanonicalValue.Span, s_canonicalJsonOptions)!;
        JsonSerializer.Serialize(rebuilt, s_canonicalJsonOptions).ShouldBe(beforeDuplicateDelivery);
        await store.ExecuteAsync(new ReadModelBatch(
            new ReadModelBatchScope(rebuild.StoreName, full.TenantId, "party", full.AggregateId,
                PartyProjectionNames.Detail, "consent-rebuild"), rebuild.Operations),
            TestContext.Current.CancellationToken);
        JsonSerializer.Serialize(store.Deserialize<PartyDetailSdkReadModel>(key), s_canonicalJsonOptions)
            .ShouldBe(beforeDuplicateDelivery);
        PartyProcessingSdkReadModel processing = store.Deserialize<PartyProcessingSdkReadModel>(
            PartySdkReadModelAddresses.Processing(full.TenantId, full.AggregateId));
        processing.LastSequenceNumber.ShouldBe(full.Events.Length);
        processing.Records.Count.ShouldBe(payloads.Count); // original events remain audit evidence
        JsonSerializer.Serialize(PartyDetailSdkProjectionHandler.Fold(full, current: null), s_canonicalJsonOptions)
            .ShouldBe(beforeDuplicateDelivery);
    }

    [Theory]
    [InlineData(nameof(FormatException), false)]
    [InlineData(nameof(OverflowException), false)]
    [InlineData(nameof(FormatException), true)]
    [InlineData(nameof(OverflowException), true)]
    public async Task DetailHandler_ConverterFailurePersistsBothCheckpointsAndContinuesAsync(string exceptionType, bool includeValidTail)
    {
        using var failures = new PartySdkProjectionConverterFailureTests();
        ProjectionEventDto bad = failures.FailingEvent(exceptionType, "json");
        ProjectionRequest request = CreateRequest() with
        {
            Events = includeValidTail
                ? [bad, Event(new PartyCreated { Type = PartyType.Person }, 2, DateTimeOffset.UnixEpoch.AddSeconds(1))]
                : [bad],
        };
        var store = new InMemoryReadModelBatchStore();
        var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request, "converter-failure-dispatch", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        PartyDetailSdkReadModel detail = store.Deserialize<PartyDetailSdkReadModel>(
            PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId));
        PartyProcessingSdkReadModel processing = store.Deserialize<PartyProcessingSdkReadModel>(
            PartySdkReadModelAddresses.Processing(request.TenantId, request.AggregateId));
        detail.LastSequenceNumber.ShouldBe(request.Events.Length);
        (detail.Detail is not null).ShouldBe(includeValidTail);
        processing.LastSequenceNumber.ShouldBe(request.Events.Length);
        processing.Records.Select(static record => record.Outcome)
            .ShouldBe(includeValidTail ? ["Failed", "Succeeded"] : ["Failed"]);
    }

    /// <summary>Proves redacted root payloads advance persisted checkpoints without creating domain state.</summary>
    [Theory]
    [InlineData(nameof(PartyDeactivated), "{}")]
    [InlineData(nameof(PartyCreated), "{}")]
    [InlineData("Future.Events.UnknownEvent", " \r\n{ \t }\n")]
    [InlineData("Future.Events.UnknownEvent", "null")]
    public async Task WholePayloadRedactionOnly_PersistsEmptyDetailAndIndexWithRedactedProcessingAsync(
        string eventTypeName, string payload)
    {
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                new ProjectionEventDto(eventTypeName, System.Text.Encoding.UTF8.GetBytes(payload), "JSON-REDACTED",
                    1, DateTimeOffset.UnixEpoch.AddSeconds(1), "correlation-1"),
            ],
        };
        var store = new InMemoryReadModelBatchStore();
        var detailHandler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        var indexHandler = new PartyIndexSdkProjectionHandler(store, s_options);

        (await detailHandler.ProjectAsync(request, "redacted-only-detail", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await indexHandler.ProjectAsync(request, "redacted-only-index", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        PartyDetailSdkReadModel detail = store.Deserialize<PartyDetailSdkReadModel>(
            PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId));
        PartyProcessingSdkReadModel processing = store.Deserialize<PartyProcessingSdkReadModel>(
            PartySdkReadModelAddresses.Processing(request.TenantId, request.AggregateId));
        PartyIndexSdkReadModel index = store.Deserialize<PartyIndexSdkReadModel>(
            PartySdkReadModelAddresses.Index(request.TenantId));
        detail.Detail.ShouldBeNull();
        detail.LastSequenceNumber.ShouldBe(1);
        detail.ProjectedAt.ShouldBe(request.Events[0].Timestamp);
        processing.Records.ShouldHaveSingleItem().Outcome.ShouldBe("Redacted");
        processing.LastSequenceNumber.ShouldBe(1);
        index.Entries.ShouldBeEmpty();
        index.LastSequenceNumbers[request.AggregateId].ShouldBe(1);
        index.ProjectedAt.ShouldBe(request.Events[0].Timestamp);
    }

    /// <summary>Checks persisted incremental projection state against both rebuild protocols after a redacted tail.</summary>
    [Theory]
    [InlineData(nameof(PartyDeactivated), "{}")]
    [InlineData("Future.Events.UnknownEvent", " \r\n{ \t }\n")]
    [InlineData("Future.Events.UnknownEvent", "null")]
    public async Task WholePayloadRedaction_IncrementalPersistenceAndRebuildsAgreeAsync(string eventTypeName, string payload)
    {
        ProjectionRequest history = CreateRequest() with
        {
            Events =
            [
                Event(new PartyCreated
                {
                    Type = PartyType.Person,
                    PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
                }, 1, DateTimeOffset.UnixEpoch),
                new ProjectionEventDto(eventTypeName, System.Text.Encoding.UTF8.GetBytes(payload), "json-redacted",
                    2, DateTimeOffset.UnixEpoch.AddSeconds(1), "correlation-1"),
                Event(new PartyDisplayNameDerived
                {
                    DisplayName = "Augusta Ada Lovelace",
                    SortName = "Lovelace, Augusta Ada",
                }, 3, DateTimeOffset.UnixEpoch.AddSeconds(2)),
            ],
        };
        ProjectionRequest unrelated = CreateRequest() with
        {
            AggregateId = "party-unrelated",
            Events = [Event(new PartyCreated { Type = PartyType.Organization }, 1, DateTimeOffset.UnixEpoch)],
        };
        var store = new InMemoryReadModelBatchStore();
        var detailHandler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        var indexHandler = new PartyIndexSdkProjectionHandler(store, s_options);
        (await indexHandler.ProjectAsync(unrelated, "unrelated-index", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        for (int position = 0; position < history.Events.Length; position++)
        {
            ProjectionRequest delivery = history with { Events = [history.Events[position]] };
            (await detailHandler.ProjectAsync(delivery, $"redacted-detail-{position}", TestContext.Current.CancellationToken))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
            (await indexHandler.ProjectAsync(delivery, $"redacted-index-{position}", TestContext.Current.CancellationToken))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }

        string detailKey = PartySdkReadModelAddresses.Detail(history.TenantId, history.AggregateId);
        string processingKey = PartySdkReadModelAddresses.Processing(history.TenantId, history.AggregateId);
        string indexKey = PartySdkReadModelAddresses.Index(history.TenantId);
        PartyDetailSdkReadModel detail = store.Deserialize<PartyDetailSdkReadModel>(detailKey);
        PartyProcessingSdkReadModel processing = store.Deserialize<PartyProcessingSdkReadModel>(processingKey);
        PartyIndexSdkReadModel index = store.Deserialize<PartyIndexSdkReadModel>(indexKey);
        detail.Detail.ShouldNotBeNull().IsActive.ShouldBeTrue();
        detail.Detail.PersonDetails.ShouldNotBeNull().FirstName.ShouldBe("Ada");
        detail.Detail.DisplayName.ShouldBe("Augusta Ada Lovelace");
        detail.Detail.SortName.ShouldBe("Lovelace, Augusta Ada");
        detail.Detail.LastModifiedAt.ShouldBe(history.Events[2].Timestamp);
        detail.Detail.NameHistory.Count.ShouldBe(2);
        detail.Detail.NameHistory[1].TriggeredBy.ShouldBe(nameof(PartyDisplayNameDerived));
        detail.Detail.NameHistory[1].ChangedAt.ShouldBe(history.Events[2].Timestamp);
        detail.LastSequenceNumber.ShouldBe(3);
        detail.ProjectedAt.ShouldBe(history.Events[2].Timestamp);
        processing.Records.Select(static record => record.Outcome).ShouldBe(["Succeeded", "Redacted", "Succeeded"]);
        processing.Records[1].OperationCategory.ShouldBe("Other");
        processing.LastSequenceNumber.ShouldBe(3);
        index.Entries[history.AggregateId].IsActive.ShouldBeTrue();
        index.Entries[history.AggregateId].DisplayName.ShouldBe("Augusta Ada Lovelace");
        index.Entries[history.AggregateId].SortName.ShouldBe("Lovelace, Augusta Ada");
        index.Entries[history.AggregateId].LastModifiedAt.ShouldBe(history.Events[2].Timestamp);
        index.Entries.ContainsKey(unrelated.AggregateId).ShouldBeTrue();
        index.LastSequenceNumbers[history.AggregateId].ShouldBe(3);
        index.ProjectedAt.ShouldBe(history.Events[2].Timestamp);

        (await detailHandler.ProjectAsync(history, "redacted-detail-duplicate", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await indexHandler.ProjectAsync(history, "redacted-index-duplicate", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        JsonSerializer.Serialize(store.Deserialize<PartyProcessingSdkReadModel>(processingKey), s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(processing, s_canonicalJsonOptions));

        DomainProjectionRebuildPlan detailPlan = await detailHandler.PrepareRebuildAsync(
            history, "redacted-detail-rebuild", TestContext.Current.CancellationToken);
        PartyDetailSdkReadModel rebuiltDetail = JsonSerializer.Deserialize<PartyDetailSdkReadModel>(
            detailPlan.Operations.Single(operation => operation.Key == detailKey).CanonicalValue.Span, s_canonicalJsonOptions)!;
        JsonSerializer.Serialize(rebuiltDetail, s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(detail, s_canonicalJsonOptions));
        PartyProcessingSdkReadModel rebuiltProcessing = JsonSerializer.Deserialize<PartyProcessingSdkReadModel>(
            detailPlan.Operations.Single(operation => operation.Key == processingKey).CanonicalValue.Span, s_canonicalJsonOptions)!;
        JsonSerializer.Serialize(rebuiltProcessing, s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(processing, s_canonicalJsonOptions));
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await indexHandler.CreateEmptyCandidateAsync(
            identity, TestContext.Current.CancellationToken);
        candidate = await indexHandler.AccumulateAsync(identity, candidate, unrelated, TestContext.Current.CancellationToken);
        candidate = await indexHandler.AccumulateAsync(identity, candidate, history, TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan indexPlan = await indexHandler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken);
        PartyIndexSdkReadModel rebuiltIndex = JsonSerializer.Deserialize<PartyIndexSdkReadModel>(
            indexPlan.Operations.Single(operation => operation.Key == indexKey).CanonicalValue.Span, s_canonicalJsonOptions)!;
        rebuiltIndex.Entries.Keys.Order(StringComparer.Ordinal).ShouldBe(index.Entries.Keys.Order(StringComparer.Ordinal));
        foreach (string partyId in index.Entries.Keys)
        {
            JsonSerializer.Serialize(rebuiltIndex.Entries[partyId], s_canonicalJsonOptions)
                .ShouldBe(JsonSerializer.Serialize(index.Entries[partyId], s_canonicalJsonOptions));
        }

        rebuiltIndex.LastSequenceNumbers.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ShouldBe(index.LastSequenceNumbers.OrderBy(static pair => pair.Key, StringComparer.Ordinal));
        rebuiltIndex.ErasureSequenceNumbers.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ShouldBe(index.ErasureSequenceNumbers.OrderBy(static pair => pair.Key, StringComparer.Ordinal));
        rebuiltIndex.ProjectedAt.ShouldBe(index.ProjectedAt);
        rebuiltIndex.ProjectionVersion.ShouldBe(index.ProjectionVersion);
    }

    /// <summary>Replaces an uncheckpointed failed audit record when a later delivery supplies whole-payload redaction.</summary>
    [Fact]
    public async Task WholePayloadRedaction_ReplacesPersistedFailedProcessingRecordAsync()
    {
        ProjectionRequest creation = CreateRequest() with { Events = [CreateRequest().Events[0]] };
        ProjectionRequest unresolved = creation with
        {
            Events =
            [
                new ProjectionEventDto("Future.Events.UnknownEvent", "{ not valid json"u8.ToArray(), "json-redacted",
                    2, DateTimeOffset.UnixEpoch.AddSeconds(1), "correlation-1"),
            ],
        };
        var store = new InMemoryReadModelBatchStore();
        var detailHandler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        var indexHandler = new PartyIndexSdkProjectionHandler(store, s_options);
        (await detailHandler.ProjectAsync(creation, "audit-create-detail", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await indexHandler.ProjectAsync(creation, "audit-create-index", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        DomainProjectionHandlerResult detailFailure = await detailHandler.ProjectAsync(
            unresolved, "audit-failed-detail", TestContext.Current.CancellationToken);
        DomainProjectionHandlerResult indexFailure = await indexHandler.ProjectAsync(
            unresolved, "audit-failed-index", TestContext.Current.CancellationToken);

        detailFailure.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        detailFailure.ReasonCode.ShouldBe(PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason);
        indexFailure.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        indexFailure.ReasonCode.ShouldBe(PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason);
        string detailKey = PartySdkReadModelAddresses.Detail(creation.TenantId, creation.AggregateId);
        string processingKey = PartySdkReadModelAddresses.Processing(creation.TenantId, creation.AggregateId);
        string indexKey = PartySdkReadModelAddresses.Index(creation.TenantId);
        PartyProcessingSdkReadModel failed = store.Deserialize<PartyProcessingSdkReadModel>(processingKey);
        failed.Records.Select(static record => record.Outcome).ShouldBe(["Succeeded", "Failed"]);
        failed.LastSequenceNumber.ShouldBe(1);
        store.Deserialize<PartyDetailSdkReadModel>(detailKey).LastSequenceNumber.ShouldBe(1);
        store.Deserialize<PartyIndexSdkReadModel>(indexKey).LastSequenceNumbers[creation.AggregateId].ShouldBe(1);

        ProjectionRequest redelivered = unresolved with
        {
            Events = [unresolved.Events[0] with { Payload = "{}"u8.ToArray() }],
        };
        (await detailHandler.ProjectAsync(redelivered, "audit-redacted-detail", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await indexHandler.ProjectAsync(redelivered, "audit-redacted-index", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        PartyProcessingSdkReadModel repaired = store.Deserialize<PartyProcessingSdkReadModel>(processingKey);
        repaired.Records.Select(static record => record.SequenceNumber).ShouldBe([1L, 2L]);
        repaired.Records.Select(static record => record.Outcome).ShouldBe(["Succeeded", "Redacted"]);
        repaired.Records[0].ShouldBe(failed.Records[0]);
        repaired.Records[1].OperationCategory.ShouldBe("Other");
        repaired.LastSequenceNumber.ShouldBe(2);
        repaired.ProjectedAt.ShouldBe(redelivered.Events[0].Timestamp);
        store.Deserialize<PartyDetailSdkReadModel>(detailKey).LastSequenceNumber.ShouldBe(2);
        PartyIndexSdkReadModel index = store.Deserialize<PartyIndexSdkReadModel>(indexKey);
        index.LastSequenceNumbers[creation.AggregateId].ShouldBe(2);
        index.Entries[creation.AggregateId].IsActive.ShouldBeTrue();
    }

    /// <summary>Retains persisted erasure tombstones and audit history when a later root-redacted event advances checkpoints.</summary>
    [Theory]
    [InlineData(nameof(PartyReactivated))]
    [InlineData("Future.Events.UnknownEvent")]
    public async Task WholePayloadRedaction_AfterErasurePreservesPersistedTombstonesAsync(string eventTypeName)
    {
        DateTimeOffset erasedAt = DateTimeOffset.UnixEpoch.AddMinutes(5);
        ProjectionRequest erasedHistory = CreateRequest() with
        {
            Events =
            [
                Event(new PartyCreated
                {
                    Type = PartyType.Person,
                    PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
                }, 1, DateTimeOffset.UnixEpoch),
                Event(new PartyErased { PartyId = "party-1", TenantId = "tenant-a", ErasedAt = erasedAt }, 2, erasedAt),
            ],
        };
        var store = new InMemoryReadModelBatchStore();
        var detailHandler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        var indexHandler = new PartyIndexSdkProjectionHandler(store, s_options);
        (await detailHandler.ProjectAsync(erasedHistory, "erased-detail", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await indexHandler.ProjectAsync(erasedHistory, "erased-index", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string detailKey = PartySdkReadModelAddresses.Detail(erasedHistory.TenantId, erasedHistory.AggregateId);
        string processingKey = PartySdkReadModelAddresses.Processing(erasedHistory.TenantId, erasedHistory.AggregateId);
        string indexKey = PartySdkReadModelAddresses.Index(erasedHistory.TenantId);
        PartyProcessingSdkReadModel priorProcessing = store.Deserialize<PartyProcessingSdkReadModel>(processingKey);
        ProjectionRequest redactedTail = erasedHistory with
        {
            Events = [new ProjectionEventDto(eventTypeName, "{}"u8.ToArray(), "json-redacted",
                3, erasedAt.AddSeconds(1), "correlation-1")],
        };

        (await detailHandler.ProjectAsync(redactedTail, "erased-redacted-detail", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await indexHandler.ProjectAsync(redactedTail, "erased-redacted-index", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        PartyDetailSdkReadModel detail = store.Deserialize<PartyDetailSdkReadModel>(detailKey);
        detail.Detail.ShouldNotBeNull().IsErased.ShouldBeTrue();
        detail.Detail.DisplayName.ShouldBeEmpty();
        detail.Detail.PersonDetails.ShouldBeNull();
        detail.Detail.LastModifiedAt.ShouldBe(erasedAt);
        detail.ErasureSequenceNumber.ShouldBe(2);
        detail.ErasedAt.ShouldBe(erasedAt);
        detail.LastSequenceNumber.ShouldBe(3);
        detail.ProjectedAt.ShouldBe(redactedTail.Events[0].Timestamp);
        PartyProcessingSdkReadModel processing = store.Deserialize<PartyProcessingSdkReadModel>(processingKey);
        processing.Records.ShouldBe(priorProcessing.Records);
        processing.Records.Select(static record => record.SequenceNumber).ShouldBe([1L, 2L]);
        processing.Records[1].EventType.ShouldBe(nameof(PartyErased));
        processing.Records[1].OperationCategory.ShouldBe("Erasure");
        processing.Records[1].Outcome.ShouldBe("Succeeded");
        processing.ErasureSequenceNumber.ShouldBe(2);
        processing.ErasedAt.ShouldBe(erasedAt);
        processing.LastSequenceNumber.ShouldBe(3);
        processing.ProjectedAt.ShouldBe(redactedTail.Events[0].Timestamp);
        PartyIndexSdkReadModel index = store.Deserialize<PartyIndexSdkReadModel>(indexKey);
        index.Entries.ContainsKey(erasedHistory.AggregateId).ShouldBeFalse();
        index.ErasureSequenceNumbers[erasedHistory.AggregateId].ShouldBe(2);
        index.LastSequenceNumbers[erasedHistory.AggregateId].ShouldBe(3);
        index.ProjectedAt.ShouldBe(redactedTail.Events[0].Timestamp);
    }

    /// <summary>Persists surviving nonpersonal fields from a partly redacted event in both canonical domain views.</summary>
    [Fact]
    public async Task FieldRedaction_PersistsUsefulSurvivingFieldsInDetailAndIndexAsync()
    {
        ProjectionRequest request = CreateRequest() with
        {
            Events = [new ProjectionEventDto(nameof(PartyCreated),
                "{\"type\":\"Organization\",\"organizationDetails\":null}"u8.ToArray(), "JsOn-ReDaCtEd",
                1, DateTimeOffset.UnixEpoch.AddMinutes(1), "correlation-1")],
        };
        var store = new InMemoryReadModelBatchStore();
        var detailHandler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        var indexHandler = new PartyIndexSdkProjectionHandler(store, s_options);

        (await detailHandler.ProjectAsync(request, "field-redacted-detail", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await indexHandler.ProjectAsync(request, "field-redacted-index", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        PartyDetailSdkReadModel detail = store.Deserialize<PartyDetailSdkReadModel>(
            PartySdkReadModelAddresses.Detail(request.TenantId, request.AggregateId));
        detail.Detail.ShouldNotBeNull().Type.ShouldBe(PartyType.Organization);
        detail.Detail.IsActive.ShouldBeTrue();
        detail.Detail.OrganizationDetails.ShouldBeNull();
        detail.Detail.CreatedAt.ShouldBe(request.Events[0].Timestamp);
        detail.LastSequenceNumber.ShouldBe(1);
        PartyIndexSdkReadModel index = store.Deserialize<PartyIndexSdkReadModel>(
            PartySdkReadModelAddresses.Index(request.TenantId));
        index.Entries[request.AggregateId].Type.ShouldBe(PartyType.Organization);
        index.Entries[request.AggregateId].IsActive.ShouldBeTrue();
        index.Entries[request.AggregateId].CreatedAt.ShouldBe(request.Events[0].Timestamp);
        index.LastSequenceNumbers[request.AggregateId].ShouldBe(1);
        PartyProcessingSdkReadModel processing = store.Deserialize<PartyProcessingSdkReadModel>(
            PartySdkReadModelAddresses.Processing(request.TenantId, request.AggregateId));
        processing.Records.ShouldHaveSingleItem().Outcome.ShouldBe("Succeeded");
        processing.Records[0].OperationCategory.ShouldBe("PartyCommand");
        processing.LastSequenceNumber.ShouldBe(1);
    }

    /// <summary>Repairs historical default-event effects by committing full rebuild plans and reading the corrected state back.</summary>
    [Fact]
    public async Task WholePayloadRedaction_RebuildRepairsHistoricalInactiveReadModelsAsync()
    {
        ProjectionRequest history = CreateRequest() with
        {
            Events =
            [
                CreateRequest().Events[0],
                CreateRequest().Events[1] with { SerializationFormat = "json-redacted" },
            ],
        };
        // Before DW-35 the redacted empty object was applied exactly like ordinary JSON.
        // Seed that historical persisted outcome through real handlers; the authoritative
        // rebuild history retains the redaction marker and must undo its mistaken effects.
        ProjectionRequest historicalEquivalent = history with
        {
            Events = [history.Events[0], history.Events[1] with { SerializationFormat = "json" }],
        };
        var store = new InMemoryReadModelBatchStore();
        var detailHandler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        var indexHandler = new PartyIndexSdkProjectionHandler(store, s_options);
        (await detailHandler.ProjectAsync(historicalEquivalent, "legacy-inactive-detail", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await indexHandler.ProjectAsync(historicalEquivalent, "legacy-inactive-index", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string detailKey = PartySdkReadModelAddresses.Detail(history.TenantId, history.AggregateId);
        string processingKey = PartySdkReadModelAddresses.Processing(history.TenantId, history.AggregateId);
        string indexKey = PartySdkReadModelAddresses.Index(history.TenantId);
        store.Deserialize<PartyDetailSdkReadModel>(detailKey).Detail.ShouldNotBeNull().IsActive.ShouldBeFalse();
        store.Deserialize<PartyIndexSdkReadModel>(indexKey).Entries[history.AggregateId].IsActive.ShouldBeFalse();
        store.Deserialize<PartyProcessingSdkReadModel>(processingKey).Records[1].Outcome.ShouldBe("Succeeded");
        (await detailHandler.ProjectAsync(history, "legacy-redelivery-detail", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await indexHandler.ProjectAsync(history, "legacy-redelivery-index", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);

        DomainProjectionRebuildPlan detailPlan = await detailHandler.PrepareRebuildAsync(
            history, "repair-inactive-detail", TestContext.Current.CancellationToken);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await indexHandler.CreateEmptyCandidateAsync(
            identity, TestContext.Current.CancellationToken);
        candidate = await indexHandler.AccumulateAsync(identity, candidate, history, TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan indexPlan = await indexHandler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken);

        (await store.ExecuteAsync(new ReadModelBatch(new ReadModelBatchScope(detailPlan.StoreName, history.TenantId,
            "party", history.AggregateId, PartyProjectionNames.Detail, "repair-detail-commit"), detailPlan.Operations),
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await store.ExecuteAsync(new ReadModelBatch(new ReadModelBatchScope(indexPlan.StoreName, history.TenantId,
            "party", PartySdkReadModelAddresses.SharedIndexAggregateId, PartyProjectionNames.Index, "repair-index-commit"), indexPlan.Operations),
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        PartyDetailSdkReadModel repairedDetail = store.Deserialize<PartyDetailSdkReadModel>(detailKey);
        repairedDetail.Detail.ShouldNotBeNull().IsActive.ShouldBeTrue();
        repairedDetail.Detail.LastModifiedAt.ShouldBe(history.Events[0].Timestamp);
        repairedDetail.LastSequenceNumber.ShouldBe(2);
        PartyProcessingSdkReadModel repairedProcessing = store.Deserialize<PartyProcessingSdkReadModel>(processingKey);
        repairedProcessing.Records.Select(static record => record.Outcome).ShouldBe(["Succeeded", "Redacted"]);
        repairedProcessing.LastSequenceNumber.ShouldBe(2);
        PartyIndexSdkReadModel repairedIndex = store.Deserialize<PartyIndexSdkReadModel>(indexKey);
        repairedIndex.Entries[history.AggregateId].IsActive.ShouldBeTrue();
        repairedIndex.Entries[history.AggregateId].LastModifiedAt.ShouldBe(history.Events[0].Timestamp);
        repairedIndex.LastSequenceNumbers[history.AggregateId].ShouldBe(2);
    }

    private static PartyProcessingSdkReadModel CreateLegacyProcessingReadModel()
        => new()
        {
            Records =
            [
                new ProcessingActivityRecord
                {
                    SequenceNumber = 1,
                    PartyId = "party-1",
                    TenantId = "tenant-a",
                    ActorId = "actor-1",
                    CorrelationId = "correlation-1",
                    OperationCategory = nameof(PartyNotFound),
                    Outcome = "Succeeded",
                    EventType = nameof(PartyNotFound),
                    Timestamp = DateTimeOffset.UnixEpoch,
                    Summary = "PartyNotFound recorded.",
                },
            ],
            LastSequenceNumber = 1,
            ProjectedAt = DateTimeOffset.UnixEpoch,
            ProjectionVersion = "1",
        };

    private static ProjectionRequest CreateRequest()
        => new(
            "tenant-a",
            "party",
            "party-1",
            [
                Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch),
                Event(new PartyDeactivated(), 2, DateTimeOffset.UnixEpoch.AddSeconds(1)),
            ]);

    private static ProjectionRequest CreateErasureRequest()
        => new(
            "tenant-a",
            "party",
            "party-1",
            [
                Event(new PartyErased
                {
                    PartyId = "party-1",
                    TenantId = "tenant-a",
                    ErasedAt = DateTimeOffset.UnixEpoch.AddMinutes(5),
                }, 2, DateTimeOffset.UnixEpoch.AddMinutes(5)),
            ]);

    private static DomainSharedProjectionRebuildIdentity CreateSharedRebuildIdentity()
        => new("tenant-a", "party", PartyProjectionNames.Index, "rebuild-1", "catalog-fingerprint");

    private static ProjectionEventDto Event(object payload, long sequence, DateTimeOffset timestamp)
        => new(
            payload.GetType().Name,
            JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), PartiesJsonOptions.Default),
            "json",
            sequence,
            timestamp,
            "correlation-1");

    private static TValue DeserializeOperation<TValue>(ReadModelBatch batch, string key)
        where TValue : class
        => JsonSerializer.Deserialize<TValue>(
            batch.Operations.Single(operation => string.Equals(operation.Key, key, StringComparison.Ordinal)).CanonicalValue.Span,
            s_canonicalJsonOptions)!;

    private static void ConfigureEmptyErasureReads(
        IReadModelStore store,
        string? firstEtag = null,
        string? secondEtag = null)
    {
        ReadModelEntry<PartyDetailSdkReadModel> firstDetail = new(null, firstEtag);
        ReadModelEntry<PartyProcessingSdkReadModel> firstProcessing = new(null, firstEtag);
        ReadModelEntry<PartyIndexSdkReadModel> firstIndex = new(null, firstEtag);
        if (secondEtag is null)
        {
            store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(firstDetail);
            store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(firstProcessing);
            store.GetAsync<PartyIndexSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(firstIndex);
            return;
        }

        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(firstDetail, new ReadModelEntry<PartyDetailSdkReadModel>(null, secondEtag));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(firstProcessing, new ReadModelEntry<PartyProcessingSdkReadModel>(null, secondEtag));
        store.GetAsync<PartyIndexSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(firstIndex, new ReadModelEntry<PartyIndexSdkReadModel>(null, secondEtag));
    }

    private static PartyIndexSdkReadModel CreateIndex(params string[] partyIds)
        => new()
        {
            Entries = partyIds.ToDictionary(
                static id => id,
                static id => new PartyIndexEntry
                {
                    Id = id,
                    Type = PartyType.Person,
                    IsActive = true,
                    DisplayName = $"Party {id}",
                    SortName = id,
                },
                StringComparer.Ordinal),
            LastSequenceNumbers = partyIds.ToDictionary(static id => id, static _ => 1L, StringComparer.Ordinal),
            ProjectedAt = DateTimeOffset.UnixEpoch,
            ProjectionVersion = "1",
        };

    private static object Normalize(PartyDetailSdkReadModel model)
        => new
        {
            model.Detail?.Id,
            model.Detail?.Type,
            model.Detail?.IsActive,
            model.Detail?.DisplayName,
            model.Detail?.SortName,
            model.LastSequenceNumber,
            model.ProjectedAt,
            model.ProjectionVersion,
        };

    /// <summary>
    /// A real (non-mocked) stateful <see cref="IReadModelStore"/>/<see cref="IReadModelBatchStore"/>
    /// double that actually persists across calls, so a sequence of independent handler dispatches
    /// observes the effect of earlier ones — unlike an NSubstitute stub returning a fixed value.
    /// Used to prove genuine convergence between incremental multi-dispatch persistence and a
    /// single-shot rebuild, which two calls to the same static Fold function cannot distinguish.
    /// </summary>
    private sealed class InMemoryReadModelBatchStore(
        JsonSerializerOptions? writeOptions = null,
        JsonSerializerOptions? readOptions = null) : IReadModelStore, IReadModelBatchStore
    {
        private readonly Dictionary<string, (ReadOnlyMemory<byte> Bytes, string ETag)> _entries = new(StringComparer.Ordinal);
        private readonly JsonSerializerOptions _writeOptions = writeOptions ?? s_canonicalJsonOptions;
        private readonly JsonSerializerOptions _readOptions = readOptions ?? s_canonicalJsonOptions;
        private int _etagSequence;

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.FromResult(_entries.TryGetValue(key, out (ReadOnlyMemory<byte> Bytes, string ETag) entry)
                ? new ReadModelEntry<TValue>(
                    JsonSerializer.Deserialize<TValue>(entry.Bytes.Span, _readOptions),
                    entry.ETag)
                : new ReadModelEntry<TValue>(null, null));

        public Task SaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            _entries[key] = (JsonSerializer.SerializeToUtf8Bytes(value, _writeOptions), NextETag());
            return Task.CompletedTask;
        }

        public Task<bool> TrySaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            bool exists = _entries.TryGetValue(key, out (ReadOnlyMemory<byte> Bytes, string ETag) current);
            bool matches = etag.Length == 0 ? !exists : exists && string.Equals(current.ETag, etag, StringComparison.Ordinal);
            if (!matches)
            {
                return Task.FromResult(false);
            }

            _entries[key] = (JsonSerializer.SerializeToUtf8Bytes(value, _writeOptions), NextETag());
            return Task.FromResult(true);
        }

        public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        {
            foreach (ReadModelBatchOperation operation in batch.Operations)
            {
                if (operation.Kind == ReadModelBatchOperationKind.Delete)
                {
                    _ = _entries.Remove(operation.Key);
                    continue;
                }

                _entries[operation.Key] = (operation.CanonicalValue, NextETag());
            }

            return Task.FromResult(ReadModelBatchResult.Completed("test-fingerprint"));
        }

        public TValue Deserialize<TValue>(string key)
            where TValue : class
            => JsonSerializer.Deserialize<TValue>(_entries[key].Bytes.Span, _readOptions)!;

        private string NextETag() => $"etag-{++_etagSequence}";
    }
}
