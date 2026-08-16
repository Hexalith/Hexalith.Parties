using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Search;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Testing;

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
        searchIndexer.NotifyIndexedAsync(
                Arg.Any<string>(),
                Arg.Any<PartyIndexEntry>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
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
        searchIndexer.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
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
    public async Task IndexHandler_ErasureNotifiesRemovalWhenCanonicalEntryWasAlreadyAbsentAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var existing = new PartyIndexSdkReadModel
        {
            LastSequenceNumbers = new Dictionary<string, long>(StringComparer.Ordinal) { ["party-1"] = 1 },
        };
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
        IReadModelStore store = Substitute.For<IReadModelStore>();
        string indexKey = PartySdkReadModelAddresses.Index("tenant-a");
        store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(null, null));
        PartyIndexSdkReadModel? persisted = null;
        store.TrySaveAsync(
                "statestore",
                indexKey,
                Arg.Do<PartyIndexSdkReadModel>(value => persisted = value),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        IPartyIndexSearchIndexer searchIndexer = Substitute.For<IPartyIndexSearchIndexer>();
        searchIndexer.NotifyIndexedAsync(
                Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false, true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer);
        ProjectionRequest request = CreateRequest();

        DomainProjectionHandlerResult first = await handler.ProjectAsync(
            request,
            "dispatch-search-retry",
            TestContext.Current.CancellationToken);
        first.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        persisted.ShouldNotBeNull();
        store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(persisted, "etag-2"));

        DomainProjectionHandlerResult retry = await handler.ProjectAsync(
            request,
            "dispatch-search-retry",
            TestContext.Current.CancellationToken);

        retry.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        await searchIndexer.Received(2).NotifyIndexedAsync(
            "tenant-a",
            Arg.Is<PartyIndexEntry>(entry => entry != null && entry.Id == "party-1"),
            "PartyDeactivated",
            DateTimeOffset.UnixEpoch.AddSeconds(1),
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
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, searchIndexer: null, logger: logger);
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
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        PartyProcessingSdkReadModel? persistedAudit = null;
        store.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Do<PartyProcessingSdkReadModel>(value => persistedAudit = value),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
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
        persistedAudit.ShouldNotBeNull();
        persistedAudit.Records.Select(static record => record.Outcome).ShouldBe(["Succeeded", "Failed"]);
        persistedAudit.LastSequenceNumber.ShouldBe(1);
        await batchStore.DidNotReceive().ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetailHandler_UnresolvedThenResolvableInSameBatchDoesNotAdvancePastUnresolvedAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        PartyProcessingSdkReadModel? persistedAudit = null;
        store.TrySaveAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Do<PartyProcessingSdkReadModel>(value => persistedAudit = value),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
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
        persistedAudit.ShouldNotBeNull();
        persistedAudit.Records.Select(static record => record.Outcome).ShouldBe(["Failed", "Succeeded"]);
        // The checkpoint must stay behind the still-unresolved sequence 1 event even though
        // sequence 2 resolved successfully in the same batch — otherwise a later redelivery
        // (e.g. after a consumer upgrade) would never revisit sequence 1 again.
        persistedAudit.LastSequenceNumber.ShouldBe(long.MinValue);
        await batchStore.DidNotReceive().ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>());
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
        await searchIndexer.DidNotReceiveWithAnyArgs().NotifyIndexedAsync(default!, default!, default!, default, default);
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
                Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
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
                Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
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
        await searchIndexer.DidNotReceiveWithAnyArgs().NotifyRemovedAsync(default!, default!, default);
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
                Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
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
        await searchIndexer.DidNotReceiveWithAnyArgs().NotifyIndexedAsync(default!, default!, default!, default, default);
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
        record.Message.ShouldContain("could not resolve event type");
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
        Type outcomeType = resolverType.GetNestedType("ResolveOutcome", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PartyEventTypeResolver.ResolveOutcome type not found.");
        PropertyInfo ambiguousProperty = outcomeType.GetProperty("Ambiguous", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("PartyEventTypeResolver.ResolveOutcome.Ambiguous property not found.");
        object ambiguousOutcome = ambiguousProperty.GetValue(null)
            ?? throw new InvalidOperationException("PartyEventTypeResolver.ResolveOutcome.Ambiguous returned null.");
        MethodInfo tryAdd = cache.GetType().GetMethod("TryAdd")
            ?? throw new InvalidOperationException("ConcurrentDictionary<string, ResolveOutcome>.TryAdd not found.");
        bool added = (bool)tryAdd.Invoke(cache, [eventTypeName, ambiguousOutcome])!;
        added.ShouldBeTrue("the sentinel event-type name must be unique and unseeded before this call.");
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
    private sealed class InMemoryReadModelBatchStore : IReadModelStore, IReadModelBatchStore
    {
        private readonly Dictionary<string, (ReadOnlyMemory<byte> Bytes, string ETag)> _entries = new(StringComparer.Ordinal);
        private int _etagSequence;

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.FromResult(_entries.TryGetValue(key, out (ReadOnlyMemory<byte> Bytes, string ETag) entry)
                ? new ReadModelEntry<TValue>(
                    JsonSerializer.Deserialize<TValue>(entry.Bytes.Span, s_canonicalJsonOptions),
                    entry.ETag)
                : new ReadModelEntry<TValue>(null, null));

        public Task SaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            _entries[key] = (JsonSerializer.SerializeToUtf8Bytes(value, s_canonicalJsonOptions), NextETag());
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

            _entries[key] = (JsonSerializer.SerializeToUtf8Bytes(value, s_canonicalJsonOptions), NextETag());
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
            => JsonSerializer.Deserialize<TValue>(_entries[key].Bytes.Span, s_canonicalJsonOptions)!;

        private string NextETag() => $"etag-{++_etagSequence}";
    }
}
