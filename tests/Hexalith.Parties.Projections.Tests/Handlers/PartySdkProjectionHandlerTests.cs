using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Search;
using Hexalith.Parties.Projections.Services;

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
    private static readonly IOptions<PartySdkReadModelOptions> s_options =
        Options.Create(new PartySdkReadModelOptions { ReadModelStateStoreName = "statestore" });

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
        persisted.LastSequenceNumber.ShouldBe(2);
        persisted.ProjectedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(1));
    }

    [Fact]
    public async Task DetailRebuildPlan_MatchesNormalReplayAfterTimestampNormalizationAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
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
        handler.RebuildSemantics.ShouldBe(DomainProjectionRebuildSemantics.FullReplay);
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

        // Erasure was the terminal event for party-1: the sequence checkpoint is dropped, not
        // retained, so a party recreated with the same id is not skipped by the stale watermark.
        persisted.LastSequenceNumbers.ContainsKey("party-1").ShouldBeFalse();
        persisted.LastSequenceNumbers["party-2"].ShouldBe(1);
    }

    [Fact]
    public async Task IndexHandler_RecreatedPartyAfterErasureIsNotSkippedByStaleCheckpointAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexSdkReadModel erased = PartyIndexSdkProjectionHandler.Fold(CreateErasureRequest(), current: CreateIndex("party-1"));
        erased.LastSequenceNumbers.ContainsKey("party-1").ShouldBeFalse();

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
            [Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch.AddMinutes(10))]);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            recreateRequest,
            "dispatch-recreate",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        persisted.ShouldNotBeNull();
        persisted.Entries.ContainsKey("party-1").ShouldBeTrue();
        persisted.LastSequenceNumbers["party-1"].ShouldBe(1);
    }

    [Fact]
    public async Task IndexHandler_NotifiesSearchIndexerWithLatestEntryAndEventAfterSuccessfulWriteAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(null, null));
        store.TrySaveAsync(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<PartyIndexSdkReadModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
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
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_ErasureNotifiesSearchIndexerRemovedAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexSdkReadModel existing = CreateIndex("party-1");
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(existing, "etag-1"));
        store.TrySaveAsync(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<PartyIndexSdkReadModel>(),
                "etag-1",
                Arg.Any<CancellationToken>())
            .Returns(true);
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateErasureRequest(),
            "dispatch-erase",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await searchIndexer.DidNotReceive().NotifyIndexedAsync(
            Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await searchIndexer.Received(1).NotifyRemovedAsync(
            "tenant-a",
            "party-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexHandler_SearchIndexerThrowDoesNotFailProjectionAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(null, null));
        store.TrySaveAsync(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<PartyIndexSdkReadModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        searchIndexer
            .NotifyIndexedAsync(
                Arg.Any<string>(),
                Arg.Any<PartyIndexEntry>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("memories down"));
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateRequest(),
            "dispatch-1",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
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

    [Fact]
    public async Task DetailHandler_UnresolvedOnlyDeliveryReturnsFailedNotAlreadyCompletedAsync()
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

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe("unresolved-or-unsupported-event");
        await batchStore.DidNotReceive().ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DetailFold_PartyCreatedAfterErasureReplacesTombstone()
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
        result.Detail.IsErased.ShouldBeFalse();
        result.Detail.Type.ShouldBe(PartyType.Organization);
        result.LastSequenceNumber.ShouldBe(3);
    }

    [Fact]
    public void IndexFold_NoOpAfterErasureStillDropsTerminalCheckpoint()
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
        result.LastSequenceNumbers.ContainsKey("party-1").ShouldBeFalse();
    }

    [Fact]
    public void ProcessingActivityFold_UnresolvedEventIsRecordedAsFailedWithoutAdvancing()
    {
        ProjectionRequest request = new(
            "tenant-a",
            "party",
            "party-1",
            [
                new ProjectionEventDto(
                    "TotallyUnknownEventType",
                    "{}"u8.ToArray(),
                    "json",
                    1,
                    DateTimeOffset.UnixEpoch,
                    "correlation-1"),
            ]);

        PartyProcessingSdkReadModel result = PartyProcessingActivityFold.Fold(request, current: null);

        ProcessingActivityRecord record = result.Records.ShouldHaveSingleItem();
        record.Outcome.ShouldBe("Failed");
        result.LastSequenceNumber.ShouldBe(long.MinValue);
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
        PartyIndexSdkReadModel replayed = PartyIndexSdkProjectionHandler.Fold(
            second,
            PartyIndexSdkProjectionHandler.Fold(first, current: null));
        JsonSerializer.Serialize(rebuilt, s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(replayed, s_canonicalJsonOptions));
        rebuilt.Entries.Keys.Order(StringComparer.Ordinal).ShouldBe(["party-1", "party-2"]);
        rebuilt.ProjectedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(2));
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
    public async Task Eraser_RedactsDetailInPlaceResetsProcessingCheckpointAndNeverErasesWholeSharedIndexAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        string detailKey = PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
        string processingKey = PartySdkReadModelAddresses.Processing("tenant-a", "party-1");
        string indexKey = PartySdkReadModelAddresses.Index("tenant-a");

        PartyDetail existingDetail = PartyDetailSdkProjectionHandler.Fold(CreateRequest(), current: null).Detail!;
        var existingDetailModel = new PartyDetailSdkReadModel { Detail = existingDetail, LastSequenceNumber = 2 };
        store.GetAsync<PartyDetailSdkReadModel>("statestore", detailKey, Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(existingDetailModel, "detail-etag"));
        PartyDetailSdkReadModel? persistedDetail = null;
        store.TrySaveAsync(
                "statestore", detailKey, Arg.Do<PartyDetailSdkReadModel>(value => persistedDetail = value), "detail-etag", Arg.Any<CancellationToken>())
            .Returns(true);

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
        PartyProcessingSdkReadModel? persistedProcessing = null;
        store.TrySaveAsync(
                "statestore", processingKey, Arg.Do<PartyProcessingSdkReadModel>(value => persistedProcessing = value), "processing-etag", Arg.Any<CancellationToken>())
            .Returns(true);

        store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(CreateIndex("party-1", "party-2"), "index-etag"));
        PartyIndexSdkReadModel? persistedIndex = null;
        store.TrySaveAsync(
                "statestore", indexKey, Arg.Do<PartyIndexSdkReadModel>(value => persistedIndex = value), "index-etag", Arg.Any<CancellationToken>())
            .Returns(true);
        var eraser = new PartySdkReadModelEraser(store, s_options);

        await eraser.EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);

        persistedDetail.ShouldNotBeNull();
        persistedDetail.Detail.ShouldNotBeNull();
        persistedDetail.Detail.IsErased.ShouldBeTrue();
        persistedDetail.Detail.DisplayName.ShouldBeEmpty();
        persistedDetail.LastSequenceNumber.ShouldBe(long.MinValue);
        persistedDetail.ProjectionVersion.ShouldBeNull();

        persistedProcessing.ShouldNotBeNull();
        persistedProcessing.Records.ShouldHaveSingleItem();
        persistedProcessing.LastSequenceNumber.ShouldBe(long.MinValue);

        persistedIndex.ShouldNotBeNull();
        persistedIndex.Entries.Keys.ShouldBe(["party-2"]);
        persistedIndex.LastSequenceNumbers.ContainsKey("party-1").ShouldBeFalse();
        persistedIndex.LastSequenceNumbers["party-2"].ShouldBe(1);
    }

    [Fact]
    public void Eraser_ResetProcessingCheckpoint_NoExistingRowReturnsEmptyModel()
    {
        PartyProcessingSdkReadModel result = PartySdkReadModelEraser.ResetProcessingCheckpoint(current: null);

        result.Records.ShouldBeEmpty();
        result.LastSequenceNumber.ShouldBe(long.MinValue);
    }

    [Fact]
    public void Eraser_RedactDetail_NoExistingRowReturnsEmptyModel()
    {
        // Intentional empty-row semantics: do not invent an IsErased tombstone when no detail
        // was ever projected. Authoritative erasure status remains IPartyErasureRecordStore.
        PartyDetailSdkReadModel result = PartySdkReadModelEraser.RedactDetail(current: null, partyId: "party-1");

        result.Detail.ShouldBeNull();
        result.LastSequenceNumber.ShouldBe(long.MinValue);
    }

    [Fact]
    public void Eraser_RedactDetail_ExistingRowResetsSequenceWatermark()
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
        result.LastSequenceNumber.ShouldBe(long.MinValue);
        result.ProjectionVersion.ShouldBeNull();
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
                ordered.Events[0] with { Timestamp = DateTimeOffset.UnixEpoch.AddHours(1) },
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
                ordered.Events[0] with { Timestamp = DateTimeOffset.UnixEpoch.AddHours(1) },
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
        record.Outcome.ShouldBe("Redacted");
        result.LastSequenceNumber.ShouldBe(1);
    }

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
                }, 3, DateTimeOffset.UnixEpoch.AddMinutes(5)),
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
}
