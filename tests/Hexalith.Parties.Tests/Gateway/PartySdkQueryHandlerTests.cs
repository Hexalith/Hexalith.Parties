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

    private static PartySdkQueryService CreateService(
        IReadModelStore store,
        IPartySearchProvider? searchProvider = null,
        IPartyErasureRecordStore? recordStore = null,
        IQueryCursorCodec? cursorCodec = null,
        PartySdkLastKnownReadModelCache? lastKnownCache = null)
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
            lastKnownCache ?? new PartySdkLastKnownReadModelCache());

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
