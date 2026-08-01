using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
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

using Microsoft.Extensions.Options;

using NSubstitute;

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

    private static PartySdkQueryService CreateService(
        IReadModelStore store,
        IPartySearchProvider? searchProvider = null,
        IProjectionRebuildService? rebuildService = null,
        IPartyErasureRecordStore? recordStore = null)
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
            rebuildService ?? Substitute.For<IProjectionRebuildService>(),
            recordStore ?? Substitute.For<IPartyErasureRecordStore>());

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
        public override DateTimeOffset GetUtcNow() => now;
    }
}
