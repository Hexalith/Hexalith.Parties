using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Queries;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

public sealed class PartySdkQueryHandlerTests
{
    private static readonly DateTimeOffset s_now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DetailHandler_ReadsCanonicalStoreAndSurfacesPersistedFreshnessAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1"),
                LastSequenceNumber = 7,
                ProjectedAt = s_now.AddSeconds(-10),
                ProjectionVersion = "7",
            }, "detail-etag"));
        PartySdkQueryService service = CreateService(store);
        var handler = new GetPartyQueryHandler(service);

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe("party-detail");
        result.Metadata.ShouldNotBeNull();
        result.Metadata.ETag.ShouldBe("detail-etag");
        result.Metadata.IsStale.ShouldBe(false);
        result.Metadata.ProjectionVersion.ShouldBe("7");
        result.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        PartyDetail payload = result.GetPayload().Deserialize<PartyDetail>(PartiesJsonOptions.Default)!;
        payload.Id.ShouldBe("party-1");
        payload.Freshness!.Status.ShouldBe(ProjectionFreshnessStatus.Current);
    }

    [Fact]
    public async Task DetailHandler_InactivePartyRemainsInspectableAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1") with { IsActive = false },
                ProjectedAt = s_now,
                ProjectionVersion = "2",
            }, "etag"));
        var handler = new GetPartyQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.GetPayload().Deserialize<PartyDetail>(PartiesJsonOptions.Default)!.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task DetailHandlers_ErasedPartyReturnOnlyRedactedStateAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyDetail erased = Detail("party-1") with
        {
            IsErased = true,
            DisplayName = string.Empty,
            SortName = string.Empty,
            ErasedAt = s_now,
        };
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = erased,
                ProjectedAt = s_now,
                ProjectionVersion = "3",
            }, "etag"));
        PartySdkQueryService service = CreateService(store);

        QueryResult getParty = await new GetPartyQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        QueryResult partyDetail = await new PartyDetailQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.PartyDetailQueryType),
            TestContext.Current.CancellationToken);

        getParty.Success.ShouldBeTrue();
        partyDetail.Success.ShouldBeTrue();
        PartyDetail getPartyPayload = getParty.GetPayload().Deserialize<PartyDetail>(PartiesJsonOptions.Default)!;
        PartyDetail partyDetailPayload = partyDetail.GetPayload().Deserialize<PartyDetail>(PartiesJsonOptions.Default)!;
        getPartyPayload.IsErased.ShouldBeTrue();
        partyDetailPayload.IsErased.ShouldBeTrue();
        string serialized = JsonSerializer.Serialize(new[] { getPartyPayload, partyDetailPayload }, PartiesJsonOptions.Default);
        serialized.ShouldNotContain("Ada Lovelace");
        serialized.ShouldNotContain("Lovelace, Ada");
    }

    [Fact]
    public async Task DetailHandler_UnknownProjectionTimestampReportsUnavailableFreshnessAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1"),
                ProjectedAt = null,
                ProjectionVersion = null,
            }, "etag"));
        var handler = new GetPartyQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        PartyDetail detail = result.GetPayload().Deserialize<PartyDetail>(PartiesJsonOptions.Default)!;
        detail.Freshness!.Status.ShouldBe(ProjectionFreshnessStatus.Unavailable);
        detail.Freshness.WarningCodes.ShouldContain(ProjectionFreshnessMetadata.WarningProjectionStateUnavailable);
    }

    [Fact]
    public async Task IndexHandler_PreservesPagingAndReportsStaleCanonicalModelAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
            {
                Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
                {
                    ["party-1"] = IndexEntry("party-1"),
                    ["party-2"] = IndexEntry("party-2"),
                },
                ProjectedAt = s_now.AddMinutes(-10),
                ProjectionVersion = "global:42",
            }, "index-etag"));
        PartySdkQueryService service = CreateService(store);
        var handler = new PartyIndexQueryHandler(service);

        QueryResult result = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Metadata!.IsStale.ShouldBe(true);
        result.Metadata.ProjectionVersion.ShouldBe("global:42");
        PagedResult<PartyIndexEntry> page = result.GetPayload().Deserialize<PagedResult<PartyIndexEntry>>(PartiesJsonOptions.Default)!;
        page.TotalCount.ShouldBe(2);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(20);
        page.Freshness!.Status.ShouldBe(ProjectionFreshnessStatus.Stale);
    }

    [Fact]
    public async Task IndexHandler_AppliesAcceptedTypeActiveAndOffsetDateFiltersAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexEntry match = IndexEntry("party-match") with { IsActive = false };
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
            {
                Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
                {
                    [match.Id] = match,
                    ["party-active"] = IndexEntry("party-active"),
                    ["party-organization"] = IndexEntry("party-organization") with
                    {
                        Type = PartyType.Organization,
                        IsActive = false,
                    },
                    ["party-outside-range"] = IndexEntry("party-outside-range") with
                    {
                        IsActive = false,
                        CreatedAt = s_now.AddDays(-10),
                    },
                },
                ProjectedAt = s_now,
                ProjectionVersion = "global:4",
            }, "index-etag"));
        var handler = new PartyIndexQueryHandler(CreateService(store));
        QueryEnvelope envelope = CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType) with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                page = 1,
                pageSize = 20,
                type = "Person",
                active = false,
                createdAfter = "2026-07-31T10:00:00+02:00",
                createdBefore = "2026-08-01T14:00:00+02:00",
                modifiedAfter = "2026-08-01T10:00:00+02:00",
                modifiedBefore = "2026-08-01T14:00:00+02:00",
            }),
        };

        QueryResult result = await handler.ExecuteAsync(envelope, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        PagedResult<PartyIndexEntry> page = result.GetPayload()
            .Deserialize<PagedResult<PartyIndexEntry>>(PartiesJsonOptions.Default)!;
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(match.Id);
    }

    [Fact]
    public async Task MissingDetailExportAndIndexModelsFailClosedAsActorNotFoundAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(null, null));
        PartySdkQueryService service = CreateService(store);

        QueryResult detail = await new GetPartyQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        QueryResult export = await new ExportPartyDataQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.ExportPartyDataQueryType),
            TestContext.Current.CancellationToken);
        QueryResult index = await new PartyIndexQueryHandler(service).ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType),
            TestContext.Current.CancellationToken);

        detail.Success.ShouldBeFalse();
        export.Success.ShouldBeFalse();
        index.Success.ShouldBeFalse();
        detail.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
        export.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
        index.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
    }

    [Fact]
    public async Task ErasureStatusHandler_UsesDirectRedactedDetailFallbackAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1") with
                {
                    IsErased = true,
                    ErasedAt = s_now.AddMinutes(-1),
                    DisplayName = string.Empty,
                    SortName = string.Empty,
                },
                LastSequenceNumber = 9,
                ProjectedAt = s_now.AddMinutes(-1),
                ProjectionVersion = "9",
            }, "etag"));
        IPartyErasureRecordStore recordStore = Substitute.For<IPartyErasureRecordStore>();
        recordStore.GetStatusAsync("tenant-a", "party-1", Arg.Any<CancellationToken>())
            .Returns((PartyErasureStatusRecord?)null);
        PartySdkQueryService service = CreateService(store, recordStore: recordStore);
        var handler = new GetErasureStatusQueryHandler(service);

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetErasureStatusQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        PartyErasureStatusRecord status = result.GetPayload().Deserialize<PartyErasureStatusRecord>(PartiesJsonOptions.Default)!;
        status.Status.ShouldBe("Erased");
        status.PartyId.ShouldBe("party-1");
    }

    [Fact]
    public async Task ExecuteAsync_MismatchedDiscriminator_FailsClosedWithoutStoreReadAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new GetPartyQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.PartyDetailQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.UnsupportedQueryType);
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyDetailSdkReadModel>(default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_PreCanceledRequest_DoesNotReadStoreAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new GetPartyQueryHandler(CreateService(store));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(
                CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
                cancellation.Token));

        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyDetailSdkReadModel>(default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_StoreThrowsOperationCanceledMidReadPropagatesInsteadOfBoundedFailureAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .ThrowsAsync<OperationCanceledException>();
        var handler = new GetPartyQueryHandler(CreateService(store));

        await Should.ThrowAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(
                CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Diagnostics_ContainOnlyBoundedMetadataAndNeverRetainExceptionsAsync()
    {
        var logger = new RecordingLogger<PartySdkQueryService>();
        IReadModelStore failingStore = Substitute.For<IReadModelStore>();
        failingStore.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReadModelEntry<PartyDetailSdkReadModel>>(
                new InvalidOperationException(
                    "Ada Lovelace <ada@example.test> leaked from readmodel:tenant-a:party:party-detail:party-1:detail")));
        QueryResult failed = await new GetPartyQueryHandler(CreateService(failingStore, logger: logger)).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);

        IReadModelStore processingStore = Substitute.For<IReadModelStore>();
        processingStore.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1"),
                ProjectedAt = s_now,
                ProjectionVersion = "1",
            }, "etag"));
        processingStore.GetAsync<PartyProcessingSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Processing("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReadModelEntry<PartyProcessingSdkReadModel>>(
                new InvalidOperationException("Ada Lovelace processing payload unavailable")));
        QueryResult degraded = await new ExportPartyDataQueryHandler(CreateService(processingStore, logger: logger)).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.ExportPartyDataQueryType),
            TestContext.Current.CancellationToken);

        IReadModelStore cursorStore = Substitute.For<IReadModelStore>();
        var codec = new TestCursorCodec();
        QueryResult rejected = await new PartyIndexQueryHandler(CreateService(cursorStore, cursorCodec: codec, logger: logger)).ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType) with
            {
                Paging = new QueryPagingOptions(
                    PageSize: 20,
                    Cursor: codec.Encode(PartyIndexProjectionQueryActor.PartyIndexQueryType, "different-scope", "20")),
            },
            TestContext.Current.CancellationToken);

        failed.Success.ShouldBeFalse();
        degraded.Success.ShouldBeTrue();
        degraded.Metadata!.IsDegraded.ShouldBe(true);
        rejected.Success.ShouldBeFalse();
        logger.Records.Count.ShouldBe(3);
        logger.Records.Any(record => record.Message.Contains("InvalidOperationException", StringComparison.Ordinal)).ShouldBeTrue();
        logger.Records.Any(record => record.Message.Contains("processing read model unavailable", StringComparison.Ordinal)).ShouldBeTrue();
        logger.Records.Any(record => record.Message.Contains("cursor rejected", StringComparison.Ordinal)).ShouldBeTrue();
        logger.Records.All(static record => record.Exception is null).ShouldBeTrue();

        string logText = string.Join('\n', logger.Records.Select(static record => record.Message));
        logText.ShouldNotContain("Ada", Case.Insensitive);
        logText.ShouldNotContain("ada@example.test", Case.Insensitive);
        logText.ShouldNotContain("tenant-a", Case.Insensitive);
        logText.ShouldNotContain("party-1", Case.Insensitive);
        logText.ShouldNotContain("correlation-1", Case.Insensitive);
        logText.ShouldNotContain("readmodel:", Case.Insensitive);
    }

    [Fact]
    public async Task IndexHandler_ProtectedCursorContinuesWithoutSkippingOrRepeatingAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
            {
                Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
                {
                    ["party-1"] = IndexEntry("party-1"),
                    ["party-2"] = IndexEntry("party-2"),
                    ["party-3"] = IndexEntry("party-3"),
                },
                ProjectedAt = s_now,
                ProjectionVersion = "global:3",
            }, "etag"));
        var handler = new PartyIndexQueryHandler(CreateService(store, cursorCodec: new TestCursorCodec()));

        QueryResult first = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType) with
            {
                Paging = new QueryPagingOptions(PageSize: 2),
            },
            TestContext.Current.CancellationToken);
        PagedResult<PartyIndexEntry> firstPage = first.GetPayload()
            .Deserialize<PagedResult<PartyIndexEntry>>(PartiesJsonOptions.Default)!;

        first.Success.ShouldBeTrue();
        firstPage.Items.Select(static item => item.Id).ShouldBe(["party-1", "party-2"]);
        first.Metadata!.Paging.ShouldNotBeNull();
        first.Metadata.Paging.HasMore.ShouldBe(true);
        first.Metadata.Paging.NextCursor.ShouldNotBeNullOrWhiteSpace();

        QueryResult second = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType) with
            {
                Paging = new QueryPagingOptions(PageSize: 2, Cursor: first.Metadata.Paging.NextCursor),
            },
            TestContext.Current.CancellationToken);
        PagedResult<PartyIndexEntry> secondPage = second.GetPayload()
            .Deserialize<PagedResult<PartyIndexEntry>>(PartiesJsonOptions.Default)!;

        second.Success.ShouldBeTrue();
        secondPage.Items.Select(static item => item.Id).ShouldBe(["party-3"]);
        second.Metadata!.Paging!.HasMore.ShouldBe(false);
        second.Metadata.Paging.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task IndexHandler_CursorBoundToDifferentCallerFailsClosedBeforeStoreReadAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var codec = new TestCursorCodec();
        string cursor = codec.Encode(
            PartyIndexProjectionQueryActor.PartyIndexQueryType,
            "different-scope",
            "2");
        var handler = new PartyIndexQueryHandler(CreateService(store, cursorCodec: codec));

        QueryResult result = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType) with
            {
                Paging = new QueryPagingOptions(PageSize: 2, Cursor: cursor),
            },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidCursor);
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyIndexSdkReadModel>(default!, default!, default);
    }

    [Fact]
    public async Task DetailHandler_StateStoreFailureReturnsTenantScopedLastKnownDataAsStaleAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var read = new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
        {
            Detail = Detail("party-1"),
            LastSequenceNumber = 7,
            ProjectedAt = s_now,
            ProjectionVersion = "7",
        }, "etag");
        int readCount = 0;
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(_ => readCount++ == 0
                ? Task.FromResult(read)
                : Task.FromException<ReadModelEntry<PartyDetailSdkReadModel>>(
                    new InvalidOperationException("state store unavailable for Ada Lovelace")));
        var handler = new GetPartyQueryHandler(CreateService(store));

        QueryResult current = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        QueryResult degraded = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);

        current.Success.ShouldBeTrue();
        degraded.Success.ShouldBeTrue();
        degraded.Metadata!.IsDegraded.ShouldBe(true);
        degraded.Metadata.IsStale.ShouldBe(true);
        degraded.Metadata.WarningCodes!.ShouldContain(ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable);
        PartyDetail payload = degraded.GetPayload().Deserialize<PartyDetail>(PartiesJsonOptions.Default)!;
        payload.Id.ShouldBe("party-1");
        payload.Freshness!.Status.ShouldBe(ProjectionFreshnessStatus.Stale);
        payload.Freshness.WarningCodes.ShouldContain(ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable);
        degraded.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task IndexHandler_StateStoreFailureReturnsTenantScopedLastKnownDataAsStaleAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var read = new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
        {
            Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["party-1"] = IndexEntry("party-1"),
            },
            ProjectedAt = s_now,
            ProjectionVersion = "global:1",
        }, "etag");
        int readCount = 0;
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(_ => readCount++ == 0
                ? Task.FromResult(read)
                : Task.FromException<ReadModelEntry<PartyIndexSdkReadModel>>(
                    new InvalidOperationException("state store unavailable for Ada Lovelace")));
        var handler = new PartyIndexQueryHandler(CreateService(store));

        QueryResult current = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType),
            TestContext.Current.CancellationToken);
        QueryResult degraded = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType),
            TestContext.Current.CancellationToken);

        current.Success.ShouldBeTrue();
        degraded.Success.ShouldBeTrue();
        degraded.Metadata!.IsDegraded.ShouldBe(true);
        degraded.Metadata.IsStale.ShouldBe(true);
        degraded.Metadata.WarningCodes!.ShouldContain(ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable);
        PagedResult<PartyIndexEntry> payload = degraded.GetPayload()
            .Deserialize<PagedResult<PartyIndexEntry>>(PartiesJsonOptions.Default)!;
        payload.Items.ShouldHaveSingleItem().Id.ShouldBe("party-1");
        payload.Freshness!.Status.ShouldBe(ProjectionFreshnessStatus.Stale);
        payload.Freshness.WarningCodes.ShouldContain(ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable);
        degraded.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task GdprHandlers_PreserveExportProcessingStatusAndCertificateSemanticsAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1"),
                LastSequenceNumber = 3,
                ProjectedAt = s_now,
                ProjectionVersion = "3",
            }, "etag"));
        var activity = new ProcessingActivityRecord
        {
            SequenceNumber = 3,
            PartyId = "party-1",
            TenantId = "tenant-a",
            ActorId = "user-1",
            CorrelationId = "correlation-1",
            OperationCategory = "Consent",
            Outcome = "Succeeded",
            EventType = "ConsentGranted",
            Timestamp = s_now,
            Summary = "Consent preference changed.",
        };
        store.GetAsync<PartyProcessingSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Processing("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel
            {
                Records = [activity],
                LastSequenceNumber = 3,
                ProjectedAt = s_now,
                ProjectionVersion = "3",
            }, "processing-etag"));
        IPartyErasureRecordStore erasure = Substitute.For<IPartyErasureRecordStore>();
        var status = new PartyErasureStatusRecord
        {
            PartyId = "party-1",
            TenantId = "tenant-a",
            Status = ErasureStatus.ErasurePending.ToString(),
            UpdatedAt = s_now,
        };
        var certificate = new ErasureCertificate
        {
            PartyId = "party-1",
            TenantId = "tenant-a",
            Timestamp = s_now,
            KeyVersionsDestroyed = [1],
            VerificationStatus = ErasureVerificationStatus.Verified,
        };
        erasure.GetStatusAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns(status);
        erasure.GetCertificateAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns(certificate);
        PartySdkQueryService service = CreateService(store, recordStore: erasure);

        QueryResult exportResult = await new ExportPartyDataQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.ExportPartyDataQueryType),
            TestContext.Current.CancellationToken);
        QueryResult processingResult = await new GetProcessingRecordsQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetProcessingRecordsQueryType),
            TestContext.Current.CancellationToken);
        QueryResult statusResult = await new GetErasureStatusQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetErasureStatusQueryType),
            TestContext.Current.CancellationToken);
        QueryResult certificateResult = await new GetErasureCertificateQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetErasureCertificateQueryType),
            TestContext.Current.CancellationToken);

        exportResult.GetPayload().Deserialize<PartyDataPortabilityPackage>(PartiesJsonOptions.Default)!
            .ProcessingRecords.ShouldHaveSingleItem().ShouldBe(activity);
        processingResult.GetPayload().Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!
            .ShouldHaveSingleItem().ShouldBe(activity);
        statusResult.GetPayload().Deserialize<PartyErasureStatusRecord>(PartiesJsonOptions.Default).ShouldBe(status);
        ErasureCertificate returnedCertificate = certificateResult.GetPayload()
            .Deserialize<ErasureCertificate>(PartiesJsonOptions.Default)!;
        returnedCertificate.PartyId.ShouldBe(certificate.PartyId);
        returnedCertificate.TenantId.ShouldBe(certificate.TenantId);
        returnedCertificate.Timestamp.ShouldBe(certificate.Timestamp);
        returnedCertificate.KeyVersionsDestroyed.ShouldBe(certificate.KeyVersionsDestroyed);
        returnedCertificate.VerificationStatus.ShouldBe(certificate.VerificationStatus);
    }

    [Fact]
    public async Task ExportHandler_UnavailablePersonalDataReturnsNoPartialPartyPayloadAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyDetail unavailable = Detail("party-1") with
        {
            DisplayName = string.Empty,
            SortName = string.Empty,
        };
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = unavailable,
                ProjectedAt = s_now,
                ProjectionVersion = "1",
            }, "etag"));
        store.GetAsync<PartyProcessingSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Processing("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel
            {
                ProjectedAt = s_now,
                ProjectionVersion = "1",
            }, "processing-etag"));
        var handler = new ExportPartyDataQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.ExportPartyDataQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        PartyDataPortabilityPackage package = result.GetPayload()
            .Deserialize<PartyDataPortabilityPackage>(PartiesJsonOptions.Default)!;
        package.Status.ShouldBe("PersonalDataUnavailable");
        package.Party.ShouldBeNull();
        JsonSerializer.Serialize(package, PartiesJsonOptions.Default).ShouldNotContain("Ada Lovelace");
    }

    [Fact]
    public async Task ErasureCertificateHandler_NullCertificateReturnsSuccessfulJsonNullAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IPartyErasureRecordStore erasure = Substitute.For<IPartyErasureRecordStore>();
        erasure.GetCertificateAsync("tenant-a", "party-1", Arg.Any<CancellationToken>())
            .Returns((ErasureCertificate?)null);
        var handler = new GetErasureCertificateQueryHandler(CreateService(store, recordStore: erasure));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetErasureCertificateQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.GetPayload().ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ErasureStatusHandler_StoreFailureReturnsBoundedFailureInsteadOfThrowingAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IPartyErasureRecordStore erasure = Substitute.For<IPartyErasureRecordStore>();
        erasure.GetStatusAsync("tenant-a", "party-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PartyErasureStatusRecord?>(new InvalidOperationException("store unavailable")));
        var handler = new GetErasureStatusQueryHandler(CreateService(store, recordStore: erasure));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetErasureStatusQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task ErasureCertificateHandler_StoreFailureReturnsBoundedFailureInsteadOfThrowingAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IPartyErasureRecordStore erasure = Substitute.For<IPartyErasureRecordStore>();
        erasure.GetCertificateAsync("tenant-a", "party-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ErasureCertificate?>(new InvalidOperationException("store unavailable")));
        var handler = new GetErasureCertificateQueryHandler(CreateService(store, recordStore: erasure));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetErasureCertificateQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public void LastKnownReadModelCache_EvictDetailRemovesOnlyThatPartysCachedEntry()
    {
        var cache = new PartySdkLastKnownReadModelCache();
        var detail = new PartyDetailSdkReadModel { Detail = Detail("party-1") };
        cache.StoreDetail("tenant-a", "party-1", detail);
        cache.StoreDetail("tenant-a", "party-2", detail with { Detail = Detail("party-2") });

        cache.EvictDetail("tenant-a", "party-1");

        cache.TryGetDetail("tenant-a", "party-1", out _).ShouldBeFalse();
        cache.TryGetDetail("tenant-a", "party-2", out _).ShouldBeTrue();
    }

    [Fact]
    public void LastKnownReadModelCache_EvictProcessingRemovesOnlyThatPartysCachedEntry()
    {
        var cache = new PartySdkLastKnownReadModelCache();
        var processing = new PartyProcessingSdkReadModel();
        cache.StoreProcessing("tenant-a", "party-1", processing);
        cache.StoreProcessing("tenant-a", "party-2", processing);

        cache.EvictProcessing("tenant-a", "party-1");

        cache.TryGetProcessing("tenant-a", "party-1", out _).ShouldBeFalse();
        cache.TryGetProcessing("tenant-a", "party-2", out _).ShouldBeTrue();
    }

    [Fact]
    public void LastKnownReadModelCache_EvictIndexRemovesOnlyThatTenantsCachedEntry()
    {
        var cache = new PartySdkLastKnownReadModelCache();
        var index = new PartyIndexSdkReadModel();
        cache.StoreIndex("tenant-a", index);
        cache.StoreIndex("tenant-b", index);

        cache.EvictIndex("tenant-a");

        cache.TryGetIndex("tenant-a", out _).ShouldBeFalse();
        cache.TryGetIndex("tenant-b", out _).ShouldBeTrue();
    }

    [Fact]
    public void LastKnownReadModelCache_EvictionGenerationRejectsLateReadStore()
    {
        var clock = new FixedTimeProvider(s_now);
        var cache = new PartySdkLastKnownReadModelCache(clock, 8, TimeSpan.FromMinutes(1));
        long generation = cache.BeginRead();

        cache.EvictDetail("tenant-a", "party-1");
        bool stored = cache.StoreDetailIfCurrent(
            "tenant-a",
            "party-1",
            generation,
            new PartyDetailSdkReadModel { Detail = Detail("party-1") });

        stored.ShouldBeFalse();
        cache.TryGetDetail("tenant-a", "party-1", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task DetailHandler_EvictionDuringCanonicalReadRejectsLatePreErasureValueAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var completion = new TaskCompletionSource<ReadModelEntry<PartyDetailSdkReadModel>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(completion.Task);
        var cache = new PartySdkLastKnownReadModelCache();
        var handler = new GetPartyQueryHandler(CreateService(store, lastKnownCache: cache));

        Task<QueryResult> pending = handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        cache.EvictDetail("tenant-a", "party-1");
        completion.SetResult(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
        {
            Detail = Detail("party-1") with { DisplayName = "Pre-erasure PII" },
            LastSequenceNumber = 2,
            ProjectedAt = s_now,
        }, "etag-old"));

        QueryResult result = await pending.ConfigureAwait(true);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
        cache.TryGetDetail("tenant-a", "party-1", out _).ShouldBeFalse();
    }

    [Fact]
    public void LastKnownReadModelCache_CapacityAndRetentionAreBounded()
    {
        var clock = new FixedTimeProvider(s_now);
        var cache = new PartySdkLastKnownReadModelCache(clock, 1, TimeSpan.FromMinutes(1));
        cache.StoreDetail("tenant-a", "party-1", new PartyDetailSdkReadModel { Detail = Detail("party-1") });
        clock.Advance(TimeSpan.FromSeconds(1));
        cache.StoreDetail("tenant-a", "party-2", new PartyDetailSdkReadModel { Detail = Detail("party-2") });

        cache.TryGetDetail("tenant-a", "party-1", out _).ShouldBeFalse();
        cache.TryGetDetail("tenant-a", "party-2", out _).ShouldBeTrue();

        clock.Advance(TimeSpan.FromMinutes(1));
        cache.TryGetDetail("tenant-a", "party-2", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task DetailHandler_DegradedReadAfterEvictionFailsBoundedInsteadOfReturningStalePreErasurePiiAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var lastKnownCache = new PartySdkLastKnownReadModelCache();
        var preErasureDetail = new PartyDetailSdkReadModel { Detail = Detail("party-1"), LastSequenceNumber = 2 };
        var read = new ReadModelEntry<PartyDetailSdkReadModel>(preErasureDetail, "etag-1");
        int readCount = 0;
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(_ => readCount++ == 0
                ? Task.FromResult(read)
                : Task.FromException<ReadModelEntry<PartyDetailSdkReadModel>>(
                    new InvalidOperationException("state store unavailable")));
        var handler = new GetPartyQueryHandler(CreateService(store, lastKnownCache: lastKnownCache));

        // First read succeeds and populates the last-known cache with pre-erasure PII.
        QueryResult firstResult = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        firstResult.Success.ShouldBeTrue();

        // Simulate the erasure cleanup step evicting the cache (PartiesServiceCollectionExtensions'
        // "projection-cache" erasure cleanup delegate), then a store outage before any post-erasure
        // read repopulates it.
        lastKnownCache.EvictDetail("tenant-a", "party-1");
        QueryResult degradedResult = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);

        // The eviction means ReadDetailModelAsync's exception filter no longer has a cached value
        // to fall back to, so the store failure propagates to ReadDetailAsync's outer catch as a
        // bounded ActorException failure rather than ever returning the stale pre-erasure PII.
        degradedResult.Success.ShouldBeFalse();
        degradedResult.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Theory]
    [InlineData("{\"page\":0,\"pageSize\":20}")]
    [InlineData("{\"page\":1,\"pageSize\":20,\"type\":\"0\"}")]
    [InlineData("{\"page\":1,\"pageSize\":20,\"createdAfter\":\"2026-08-01T12:00:00\"}")]
    [InlineData("{\"page\":1,\"pageSize\":20,\"createdAfter\":\"2026-08-02T12:00:00Z\",\"createdBefore\":\"2026-08-01T12:00:00Z\"}")]
    [InlineData("{\"page\":1,\"pageSize\":20,\"unexpected\":true}")]
    public async Task IndexHandler_StrictPayloadValidationFailsClosedBeforeStoreReadAsync(string json)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartyIndexQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType) with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(JsonDocument.Parse(json).RootElement),
            },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyIndexSdkReadModel>(default!, default!, default);
    }

    [Fact]
    public async Task DetailHandler_TenantKeySeparatorFailsClosedBeforeStoreReadAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new GetPartyQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType) with { TenantId = "tenant:escape" },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyDetailSdkReadModel>(default!, default!, default);
    }

    [Fact]
    public async Task SearchHandler_UnsupportedModeFailsClosedBeforeStoreReadAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartySearchQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartySearchQueryType) with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    query = "Ada",
                    page = 1,
                    pageSize = 20,
                    mode = "Semantic",
                }),
            },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.UnsupportedQueryType);
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyIndexSdkReadModel>(default!, default!, default);
    }

    [Fact]
    public async Task SearchHandler_UnknownPayloadFieldFailsClosedBeforeStoreReadAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartySearchQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartySearchQueryType) with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    query = "Ada",
                    page = 1,
                    pageSize = 20,
                    unexpected = true,
                }),
            },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyIndexSdkReadModel>(default!, default!, default);
    }

    [Fact]
    public async Task PartyDetailQueryHandler_ReadsCanonicalStoreForPartyDetailDiscriminatorAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1"),
                LastSequenceNumber = 7,
                ProjectedAt = s_now.AddSeconds(-10),
                ProjectionVersion = "7",
            }, "detail-etag"));
        var handler = new PartyDetailQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.PartyDetailQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.GetPayload().Deserialize<PartyDetail>(PartiesJsonOptions.Default)!.Id.ShouldBe("party-1");
    }

    [Fact]
    public async Task SearchHandler_HappyPathReturnsProviderResultsOverCanonicalIndexAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
            {
                Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
                {
                    ["party-1"] = IndexEntry("party-1"),
                },
                ProjectedAt = s_now,
                ProjectionVersion = "global:1",
            }, "index-etag"));
        IPartySearchProvider searchProvider = Substitute.For<IPartySearchProvider>();
        searchProvider.Search(
                Arg.Any<IEnumerable<PartyIndexEntry>>(),
                "ada",
                null,
                null,
                1,
                20,
                Arg.Any<CancellationToken>())
            .Returns(ci => new PagedResult<PartySearchResult>
            {
                Items =
                [
                    new PartySearchResult
                    {
                        Party = IndexEntry("party-1"),
                        RelevanceScore = 1,
                        Matches =
                        [
                            new MatchMetadata
                            {
                                MatchedField = "displayName",
                                MatchType = "prefix",
                            },
                        ],
                    },
                ],
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
            });
        var handler = new PartySearchQueryHandler(CreateService(store, searchProvider: searchProvider));
        QueryEnvelope envelope = CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartySearchQueryType) with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { query = "ada", page = 1, pageSize = 20 }),
        };

        QueryResult result = await handler.ExecuteAsync(envelope, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        PagedResult<PartySearchResult> page = result.GetPayload()
            .Deserialize<PagedResult<PartySearchResult>>(PartiesJsonOptions.Default)!;
        page.Items.ShouldHaveSingleItem().Party.Id.ShouldBe("party-1");
        searchProvider.Received(1).Search(
            Arg.Any<IEnumerable<PartyIndexEntry>>(),
            "ada",
            null,
            null,
            1,
            20,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Lexical")]
    [InlineData("DisplayName")]
    public async Task SearchHandler_SupportedModesForwardTypeAndActiveFiltersAsync(string mode)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
            {
                Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
                {
                    ["party-organization"] = IndexEntry("party-organization") with
                    {
                        Type = PartyType.Organization,
                        IsActive = false,
                    },
                },
                ProjectedAt = s_now,
                ProjectionVersion = "global:1",
            }, "index-etag"));
        IPartySearchProvider searchProvider = Substitute.For<IPartySearchProvider>();
        searchProvider.Search(
                Arg.Any<IEnumerable<PartyIndexEntry>>(),
                "ada",
                PartyType.Organization,
                false,
                1,
                20,
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<PartySearchResult>
            {
                Items = [],
                Page = 1,
                PageSize = 20,
                TotalCount = 0,
            });
        var handler = new PartySearchQueryHandler(CreateService(store, searchProvider: searchProvider));
        QueryEnvelope envelope = CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartySearchQueryType) with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                query = "ada",
                page = 1,
                pageSize = 20,
                type = "Organization",
                active = false,
                mode,
            }),
        };

        QueryResult result = await handler.ExecuteAsync(envelope, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        searchProvider.Received(1).Search(
            Arg.Any<IEnumerable<PartyIndexEntry>>(),
            "ada",
            PartyType.Organization,
            false,
            1,
            20,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportHandler_ErasedPartyReturnsErasedStatusAndNullPartyAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyDetail erased = Detail("party-1") with
        {
            IsErased = true,
            DisplayName = string.Empty,
            SortName = string.Empty,
            ErasedAt = s_now,
        };
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = erased,
                LastSequenceNumber = long.MinValue,
                ProjectedAt = s_now,
            }, "etag"));
        store.GetAsync<PartyProcessingSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Processing("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel(), null));
        var handler = new ExportPartyDataQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.ExportPartyDataQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        PartyDataPortabilityPackage package = result.GetPayload()
            .Deserialize<PartyDataPortabilityPackage>(PartiesJsonOptions.Default)!;
        package.Status.ShouldBe("Erased");
        package.Party.ShouldBeNull();
        JsonSerializer.Serialize(package, PartiesJsonOptions.Default).ShouldNotContain("Ada Lovelace");
    }

    [Fact]
    public async Task ExportHandler_ProcessingStoreFailureWithoutCacheReturnsEmptyRecordsDegradedAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1"),
                LastSequenceNumber = 3,
                ProjectedAt = s_now,
                ProjectionVersion = "3",
            }, "etag"));
        store.GetAsync<PartyProcessingSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Processing("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReadModelEntry<PartyProcessingSdkReadModel>>(
                new InvalidOperationException("processing store unavailable")));
        var handler = new ExportPartyDataQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.ExportPartyDataQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Metadata!.IsDegraded.ShouldBe(true);
        PartyDataPortabilityPackage package = result.GetPayload()
            .Deserialize<PartyDataPortabilityPackage>(PartiesJsonOptions.Default)!;
        package.Party.ShouldNotBeNull();
        package.ProcessingRecords.ShouldBeEmpty();
        package.Freshness!.Status.ShouldBe(ProjectionFreshnessStatus.Stale);
        package.Party.Freshness!.Status.ShouldBe(ProjectionFreshnessStatus.Stale);
        result.Metadata!.Lifecycle.ShouldBe(ProjectionLifecycleState.Stale);
    }

    [Fact]
    public async Task ProcessingHandler_NonexistentPartyReturnsBoundedNotFoundWithoutEmptyHistoryAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        var handler = new GetProcessingRecordsQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetProcessingRecordsQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
        await store.DidNotReceive().GetAsync<PartyProcessingSdkReadModel>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessingHandler_StateStoreFailureReturnsTenantScopedLastKnownDataAsDegradedAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1"),
                LastSequenceNumber = 3,
                ProjectedAt = s_now,
                ProjectionVersion = "3",
            }, "detail-etag"));
        var activity = new ProcessingActivityRecord
        {
            SequenceNumber = 3,
            PartyId = "party-1",
            TenantId = "tenant-a",
            ActorId = "user-1",
            CorrelationId = "correlation-1",
            OperationCategory = "Consent",
            Outcome = "Succeeded",
            EventType = "ConsentGranted",
            Timestamp = s_now,
            Summary = "Consent preference changed.",
        };
        var read = new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel
        {
            Records = [activity],
            LastSequenceNumber = 3,
            ProjectedAt = s_now,
            ProjectionVersion = "3",
        }, "processing-etag");
        int readCount = 0;
        store.GetAsync<PartyProcessingSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Processing("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(_ => readCount++ == 0
                ? Task.FromResult(read)
                : Task.FromException<ReadModelEntry<PartyProcessingSdkReadModel>>(
                    new InvalidOperationException("state store unavailable")));
        var handler = new GetProcessingRecordsQueryHandler(CreateService(store));

        QueryResult current = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetProcessingRecordsQueryType),
            TestContext.Current.CancellationToken);
        QueryResult degraded = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetProcessingRecordsQueryType),
            TestContext.Current.CancellationToken);

        current.Success.ShouldBeTrue();
        degraded.Success.ShouldBeTrue();
        degraded.Metadata!.IsDegraded.ShouldBe(true);
        degraded.GetPayload().Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!
            .ShouldHaveSingleItem().ShouldBe(activity);
    }

    [Fact]
    public async Task DetailHandler_DegradedReadDoesNotLeakCrossTenantLastKnownDataAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1") with { DisplayName = "Tenant A Secret" },
                LastSequenceNumber = 7,
                ProjectedAt = s_now,
                ProjectionVersion = "7",
            }, "etag-a"));
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-b", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReadModelEntry<PartyDetailSdkReadModel>>(
                new InvalidOperationException("state store unavailable")));
        PartySdkQueryService service = CreateService(store);
        var handler = new GetPartyQueryHandler(service);

        QueryResult tenantA = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        QueryResult tenantB = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType) with { TenantId = "tenant-b" },
            TestContext.Current.CancellationToken);

        tenantA.Success.ShouldBeTrue();
        tenantB.Success.ShouldBeFalse();
        tenantB.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
        JsonSerializer.Serialize(tenantB, PartiesJsonOptions.Default).ShouldNotContain("Tenant A Secret");
    }

    [Fact]
    public async Task DetailHandler_AggregateAndEntityMismatchFailsClosedBeforeStoreReadAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new GetPartyQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType) with
            {
                AggregateId = "party-1",
                EntityId = "party-2",
            },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyDetailSdkReadModel>(default!, default!, default);
    }

    [Fact]
    public async Task DetailHandler_WrongTenantAndAbsentPartyHaveSameBoundedOutcomeAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1"),
                ProjectedAt = s_now,
            }, "etag"));
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-b", "party-1"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Detail("tenant-b", "party-missing"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        var handler = new GetPartyQueryHandler(CreateService(store));

        QueryResult wrongTenant = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType) with { TenantId = "tenant-b" },
            TestContext.Current.CancellationToken);
        QueryResult absent = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType) with
            {
                TenantId = "tenant-b",
                AggregateId = "party-missing",
                EntityId = "party-missing",
            },
            TestContext.Current.CancellationToken);

        wrongTenant.Success.ShouldBeFalse();
        absent.Success.ShouldBeFalse();
        wrongTenant.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
        absent.ErrorMessage.ShouldBe(wrongTenant.ErrorMessage);
        wrongTenant.Metadata.ShouldBeNull();
        absent.Metadata.ShouldBeNull();
        await store.DidNotReceive().GetAsync<PartyDetailSdkReadModel>(
            "statestore",
            PartySdkReadModelAddresses.Detail("tenant-a", "party-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexAndSearchDegradedReadsDoNotLeakCrossTenantLastKnownDataAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
            {
                Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
                {
                    ["party-secret"] = IndexEntry("party-secret") with { DisplayName = "Tenant A Secret" },
                },
                ProjectedAt = s_now,
                ProjectionVersion = "global:1",
            }, "etag-a"));
        store.GetAsync<PartyIndexSdkReadModel>(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-b"),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReadModelEntry<PartyIndexSdkReadModel>>(
                new InvalidOperationException("state store unavailable")));
        PartySdkQueryService service = CreateService(store);

        QueryResult tenantA = await new PartyIndexQueryHandler(service).ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType),
            TestContext.Current.CancellationToken);
        QueryResult tenantBIndex = await new PartyIndexQueryHandler(service).ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartyIndexQueryType) with { TenantId = "tenant-b" },
            TestContext.Current.CancellationToken);
        QueryResult tenantBSearch = await new PartySearchQueryHandler(service).ExecuteAsync(
            CreateIndexEnvelope(PartyIndexProjectionQueryActor.PartySearchQueryType) with
            {
                TenantId = "tenant-b",
                Payload = JsonSerializer.SerializeToUtf8Bytes(new { query = "secret", page = 1, pageSize = 20 }),
            },
            TestContext.Current.CancellationToken);

        tenantA.Success.ShouldBeTrue();
        tenantBIndex.Success.ShouldBeFalse();
        tenantBSearch.Success.ShouldBeFalse();
        tenantBIndex.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
        tenantBSearch.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
        JsonSerializer.Serialize(new[] { tenantBIndex, tenantBSearch }, PartiesJsonOptions.Default)
            .ShouldNotContain("Tenant A Secret");
    }

    [Fact]
    public async Task DetailHandler_ReservedPartyIdCharactersFailClosedAsInvalidEnvelopeAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new GetPartyQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailProjectionQueryActor.GetPartyQueryType) with
            {
                AggregateId = "party|1",
                EntityId = "party|1",
            },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyDetailSdkReadModel>(default!, default!, default);
    }

    private static PartySdkQueryService CreateService(
        IReadModelStore store,
        IPartySearchProvider? searchProvider = null,
        IPartyErasureRecordStore? recordStore = null,
        IQueryCursorCodec? cursorCodec = null,
        PartySdkLastKnownReadModelCache? lastKnownCache = null,
        ILogger<PartySdkQueryService>? logger = null)
        => new(
            store,
            Options.Create(new PartySdkReadModelOptions
            {
                ReadModelStateStoreName = "statestore",
                FreshnessAgingSeconds = 30,
                FreshnessStaleSeconds = 300,
            }),
            new FixedTimeProvider(s_now),
            searchProvider ?? Substitute.For<IPartySearchProvider>(),
            recordStore ?? Substitute.For<IPartyErasureRecordStore>(),
            cursorCodec ?? new TestCursorCodec(),
            lastKnownCache ?? new PartySdkLastKnownReadModelCache(),
            logger ?? NullLogger<PartySdkQueryService>.Instance);

    private static QueryEnvelope CreateDetailEnvelope(string queryType)
        => new(
            tenantId: "tenant-a",
            domain: PartyDetailProjectionQueryActor.PartyDomain,
            aggregateId: "party-1",
            queryType: queryType,
            payload: JsonSerializer.SerializeToUtf8Bytes(new { }),
            correlationId: "correlation-1",
            userId: "user-1",
            entityId: "party-1");

    private static QueryEnvelope CreateIndexEnvelope(string queryType)
        => new(
            tenantId: "tenant-a",
            domain: PartyDetailProjectionQueryActor.PartyDomain,
            aggregateId: PartyIndexProjectionQueryActor.ListAggregateId,
            queryType: queryType,
            payload: JsonSerializer.SerializeToUtf8Bytes(new { page = 1, pageSize = 20 }),
            correlationId: "correlation-1",
            userId: "user-1",
            entityId: PartyIndexProjectionQueryActor.ListAggregateId);

    private static PartyDetail Detail(string id)
        => new()
        {
            Id = id,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            CreatedAt = s_now.AddDays(-1),
            LastModifiedAt = s_now.AddMinutes(-1),
        };

    private static PartyIndexEntry IndexEntry(string id)
        => new()
        {
            Id = id,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = $"Party {id}",
            SortName = id,
            CreatedAt = s_now.AddDays(-1),
            LastModifiedAt = s_now.AddMinutes(-1),
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class TestCursorCodec : IQueryCursorCodec
    {
        public string Encode(string queryType, string scope, string position)
            => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new CursorValue(queryType, scope, position)));

        public bool TryDecode(
            string? cursor,
            string queryType,
            string scope,
            out string? position,
            out string? failureReason)
        {
            position = null;
            failureReason = null;
            if (string.IsNullOrWhiteSpace(cursor))
            {
                return true;
            }

            try
            {
                CursorValue? value = JsonSerializer.Deserialize<CursorValue>(Convert.FromBase64String(cursor));
                if (value is null
                    || !string.Equals(value.QueryType, queryType, StringComparison.Ordinal)
                    || !string.Equals(value.Scope, scope, StringComparison.Ordinal))
                {
                    failureReason = "wrong-scope";
                    return false;
                }

                position = value.Position;
                return true;
            }
            catch (Exception exception) when (exception is FormatException or JsonException)
            {
                failureReason = "malformed";
                return false;
            }
        }

        private sealed record CursorValue(string QueryType, string Scope, string Position);
    }
}
