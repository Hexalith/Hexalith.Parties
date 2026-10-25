using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Client.Registration;
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
using Hexalith.Parties.Search;
using Hexalith.Parties.Testing;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
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

    [Theory]
    [InlineData("missing")]
    [InlineData("outage")]
    [InlineData("cache")]
    public async Task ProcessingFirstPageWithoutRevisionAuthorityReturnsReadModelUnavailableAsync(string scenario)
    {
        var token = TestContext.Current.CancellationToken;
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), token)
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel { Detail = Detail("party-1") }, "detail"));
        bool unavailable = scenario != "cache";
        var model = new PartyProcessingSdkReadModel { Records = [new ProcessingActivityRecord { TenantId = "tenant-a", PartyId = "party-1", SequenceNumber = 1, EventType = "PartyUpdated", Timestamp = s_now, Summary = "Retained fact." }] };
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), token).Returns(_ => !unavailable
            ? Task.FromResult(new ReadModelEntry<PartyProcessingSdkReadModel>(model, "revision"))
            : scenario == "missing" ? Task.FromResult(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null))
            : Task.FromException<ReadModelEntry<PartyProcessingSdkReadModel>>(new IOException("store unavailable")));
        var service = CreateService(store);
        var envelope = CreateDetailEnvelope("GetProcessingRecords");
        if (scenario == "cache") (await service.GetProcessingRecordsAsync(envelope, token)).Success.ShouldBeTrue();
        unavailable = true;
        var result = await service.GetProcessingRecordsAsync(envelope with { Paging = new QueryPagingOptions(PageSize: 10) }, token);
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
        result.PayloadBytes.ShouldBeNull();
        unavailable = false;
        var invalid = await service.GetProcessingRecordsAsync(envelope with { Paging = new QueryPagingOptions(PageSize: 10, Cursor: "invalid-cursor") }, token);
        invalid.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidPage);
    }

    [Theory]
    [InlineData("tenant", "page")]
    [InlineData("party", "page")]
    [InlineData("tenant", "complete")]
    [InlineData("party", "complete")]
    [InlineData("tenant", "export")]
    [InlineData("party", "export")]
    public async Task InlineProcessingReadsRefuseMisplacedForeignFactsWithoutPayloadAsync(string foreign, string mode)
    {
        var token = TestContext.Current.CancellationToken;
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), token)
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel { Detail = Detail("party-1") }, "detail"));
        var fact = new ProcessingActivityRecord { SequenceNumber = 1, TenantId = foreign == "tenant" ? "foreign" : "tenant-a",
            PartyId = foreign == "party" ? "foreign" : "party-1", EventType = "PartyUpdated", Timestamp = s_now, Summary = "foreign-private-fact" };
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), token)
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel { Records = [fact] }, "revision"));
        var service = CreateService(store);
        var query = CreateDetailEnvelope(mode == "export" ? "ExportPartyData" : "GetProcessingRecords");
        var result = mode == "export" ? await service.ExportPartyDataAsync(query, token)
            : await service.GetProcessingRecordsAsync(query with { Paging = mode == "page" ? new QueryPagingOptions(PageSize: 10) : null }, token);
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
        result.PayloadBytes.ShouldBeNull();
    }

    [Fact]
    public async Task InlineProcessingPagesTraverseProtectedFirstContinuationAndTerminalMetadataAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var records = Enumerable.Range(1, 32).Select(sequence => new ProcessingActivityRecord { SequenceNumber = sequence,
            PartyId = "party-1", TenantId = "tenant-a", EventType = "PartyDeactivated", Timestamp = s_now, Summary = "Party deactivated." }).ToArray();
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), token)
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel { Detail = Detail("party-1") }, "detail"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), token)
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel
                { Records = records, LastSequenceNumber = 32, ProjectionVersion = "32", ProjectedAt = s_now }, "inline-revision"));
        var service = CreateService(store);
        var envelope = CreateDetailEnvelope("GetProcessingRecords");
        string? cursor = null;
        var all = new List<ProcessingActivityRecord>();
        int pages = 0;
        do
        {
            var page = await service.GetProcessingRecordsAsync(envelope with { Paging = new QueryPagingOptions(PageSize: 11, Cursor: cursor) }, token);
            page.Success.ShouldBeTrue();
            var paging = page.Metadata!.Paging!;
            paging.TotalCount.ShouldBe(32);
            paging.PageSize.ShouldBe(11);
            var items = page.GetPayload().Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!;
            items.Length.ShouldBe(pages == 2 ? 10 : 11);
            all.AddRange(items);
            pages++;
            cursor = paging.NextCursor;
            paging.HasMore.ShouldBe(pages != 3);
            if (pages != 3) cursor.ShouldNotBeNullOrWhiteSpace();
            else cursor.ShouldBeNull();
        } while (cursor is not null);
        pages.ShouldBe(3);
        JsonSerializer.Serialize(all, PartiesJsonOptions.Default).ShouldBe(JsonSerializer.Serialize(records, PartiesJsonOptions.Default));
    }

    [Theory]
    [InlineData("continue")]
    [InlineData("long-user")]
    [InlineData("tenant")]
    [InlineData("party")]
    [InlineData("user")]
    [InlineData("page-size")]
    [InlineData("revision")]
    public async Task ProcessingBucketPagesProtectScopeAndCompatibilityReadsRemainCompleteAsync(string scenario)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var records = Enumerable.Range(1, 80).Select(sequence => new ProcessingActivityRecord { SequenceNumber = sequence,
            PartyId = "party-1", TenantId = "tenant-a", EventType = "PartyDeactivated", Timestamp = s_now, Summary = "Party deactivated." }).ToArray();
        var model = new PartyProcessingSdkReadModel { BucketGeneration = "generation", FirstBucket = 0, LastBucket = 2,
            FirstRecordSequenceNumber = 1, LastRecordSequenceNumber = 80, RecordCount = 80, LastSequenceNumber = 80, ProjectionVersion = "80", ProjectedAt = s_now };
        string revision = "revision-a";
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), token)
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel { Detail = Detail("party-1") }, "detail"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), token)
            .Returns(_ => new ReadModelEntry<PartyProcessingSdkReadModel>(model, revision));
        foreach (var group in records.GroupBy(record => PartyProcessingBucket.Number(record.SequenceNumber)))
            store.GetAsync<PartyProcessingBucket>("statestore", PartySdkReadModelAddresses.ProcessingBucket("tenant-a", "party-1", "generation", group.Key), token)
                .Returns(new ReadModelEntry<PartyProcessingBucket>(new PartyProcessingBucket { Records = group.ToArray() }, "bucket"));
        var service = CreateService(store);
        QueryEnvelope envelope = CreateDetailEnvelope("GetProcessingRecords");
        if (scenario == "long-user") envelope = envelope with { UserId = new string('u', 8192) };
        QueryResult complete = await service.GetProcessingRecordsAsync(envelope, token);
        complete.Success.ShouldBeTrue();
        complete.GetPayload().Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!.Length.ShouldBe(80);
        QueryResult exported = await service.ExportPartyDataAsync(envelope with { QueryType = "ExportPartyData" }, token);
        exported.Success.ShouldBeTrue();
        exported.GetPayload().Deserialize<PartyDataPortabilityPackage>(PartiesJsonOptions.Default)!.ProcessingRecords.Count.ShouldBe(80);
        QueryResult first = await service.GetProcessingRecordsAsync(envelope with { Paging = new QueryPagingOptions(PageSize: 17) }, token);
        first.Success.ShouldBeTrue();
        first.GetPayload().Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!.Length.ShouldBe(17);
        string cursor = first.Metadata!.Paging!.NextCursor.ShouldNotBeNull();
        var next = envelope with { Paging = new QueryPagingOptions(PageSize: 17, Cursor: cursor) };
        next = scenario switch
        {
            "tenant" => next with { TenantId = "tenant-b" },
            "party" => next with { AggregateId = "party-2", EntityId = "party-2" },
            "user" => next with { UserId = "different-user" },
            "page-size" => next with { Paging = new QueryPagingOptions(PageSize: 18, Cursor: cursor) },
            _ => next,
        };
        if (scenario == "revision") revision = "revision-b";
        QueryResult continued = await service.GetProcessingRecordsAsync(next, token);
        continued.Success.ShouldBe(scenario is "continue" or "long-user");
        if (continued.Success)
        {
            var all = first.GetPayload().Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!.ToList();
            QueryResult page = continued;
            while (true)
            {
                all.AddRange(page.GetPayload().Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!);
                if (page.Metadata!.Paging!.HasMore == false)
                {
                    page.Metadata.Paging.NextCursor.ShouldBeNull();
                    page.Metadata.Paging.TotalCount.ShouldBe(80);
                    break;
                }
                string continuation = page.Metadata.Paging.NextCursor.ShouldNotBeNull();
                continuation.Length.ShouldBeLessThanOrEqualTo(QueryPolicyLimits.MaxCursorLength);
                page = await service.GetProcessingRecordsAsync(envelope with { Paging = new QueryPagingOptions(PageSize: 17, Cursor: continuation) }, token);
                page.Success.ShouldBeTrue();
            }
            all.Select(record => record.SequenceNumber).ShouldBe(Enumerable.Range(1, 80).Select(number => (long)number));
        }
    }

    [Theory]
    [InlineData("missing-bounds", false)]
    [InlineData("reversed-bounds", false)]
    [InlineData("empty-bucket", false)]
    [InlineData("truncated-bucket", false)]
    [InlineData("missing-bucket", false)]
    [InlineData("revision-during-read", false)]
    [InlineData("revision-during-read", true)]
    public async Task ProcessingReadsRejectBrokenCoverageAndMidReadRevisionChangesAsync(string defect, bool export)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var records = Enumerable.Range(1, 40).Select(sequence => new ProcessingActivityRecord { SequenceNumber = sequence,
            PartyId = "party-1", TenantId = "tenant-a", EventType = "PartyDeactivated", Timestamp = s_now, Summary = "Party deactivated." }).ToArray();
        var model = new PartyProcessingSdkReadModel { BucketGeneration = "generation", FirstBucket = 0, LastBucket = 1,
            FirstRecordSequenceNumber = 1, LastRecordSequenceNumber = 40, RecordCount = 40, LastSequenceNumber = 40, ProjectedAt = s_now };
        if (defect == "missing-bounds") model = model with { FirstBucket = null, LastBucket = null };
        if (defect == "reversed-bounds") model = model with { FirstBucket = 2, LastBucket = 0 };
        string revision = "before";
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), token)
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel { Detail = Detail("party-1") }, "detail"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), token)
            .Returns(_ => new ReadModelEntry<PartyProcessingSdkReadModel>(model, revision));
        foreach (var group in records.GroupBy(record => PartyProcessingBucket.Number(record.SequenceNumber)))
        {
            var retained = defect == "empty-bucket" ? [] : defect == "truncated-bucket" ? group.Skip(1).ToArray() : group.ToArray();
            store.GetAsync<PartyProcessingBucket>("statestore", PartySdkReadModelAddresses.ProcessingBucket("tenant-a", "party-1", "generation", group.Key), token)
                .Returns(_ => { if (defect == "revision-during-read") revision = "after";
                    return new ReadModelEntry<PartyProcessingBucket>(defect == "missing-bucket" ? null : new PartyProcessingBucket { Records = retained }, "bucket"); });
        }
        var service = CreateService(store);
        QueryEnvelope envelope = CreateDetailEnvelope(export ? "ExportPartyData" : "GetProcessingRecords");
        QueryResult result = export ? await service.ExportPartyDataAsync(envelope, token)
            : await service.GetProcessingRecordsAsync(envelope with { Paging = new QueryPagingOptions(PageSize: 10) }, token);
        result.Success.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, "continue")]
    [InlineData(false, "tamper")]
    [InlineData(false, "user")]
    [InlineData(false, "tenant")]
    [InlineData(false, "query")]
    [InlineData(false, "type")]
    [InlineData(false, "active")]
    [InlineData(false, "mode")]
    [InlineData(false, "page-size")]
    [InlineData(true, "continue")]
    [InlineData(true, "tamper")]
    [InlineData(true, "user")]
    [InlineData(true, "tenant")]
    [InlineData(true, "query")]
    [InlineData(true, "type")]
    [InlineData(true, "active")]
    [InlineData(true, "mode")]
    [InlineData(true, "page-size")]
    public async Task SearchHandler_ProductionCursorRetainsScopeAcrossIndependentHandlersAndRejectsEachBindingChangeAsync(bool compact, string scenario)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        string query = compact ? string.Concat(Enumerable.Repeat("\U0001F600", 256)) : "Party";
        PartyIndexEntry[] entries = [IndexEntry("party-a") with { DisplayName = query }, IndexEntry("party-b") with { DisplayName = query }];
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel { Entries = entries.ToDictionary(entry => entry.Id) }, "etag"));
        var keys = new EphemeralDataProtectionProvider();
        IQueryCursorCodec firstCodec = CreateCursorCodec(keys);
        var firstHandler = new PartySearchQueryHandler(CreateService(store, new LocalFuzzyPartySearchProvider(), cursorCodec: firstCodec));
        var payload = new Dictionary<string, object?> { ["Query"] = query, ["Type"] = "Person", ["Active"] = true, ["Mode"] = "Lexical" };
        QueryEnvelope envelope = CreateIndexEnvelope("PartySearch") with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(payload),
            Paging = new QueryPagingOptions(PageSize: 1),
        };
        string scope = QueryCursorScope.Create().Add("tenant", "tenant-a").Add("user", "user-1")
            .Add("query", query).Add("type", "Person").Add("active", "True")
            .Add("mode", "Lexical").Add("case", (string?)null).Add("page-size", "1").Build();
        QueryResult first = await firstHandler.ExecuteAsync(envelope, TestContext.Current.CancellationToken);
        first.Success.ShouldBeTrue();
        string cursor = first.Metadata!.Paging!.NextCursor.ShouldNotBeNull();
        cursor.Length.ShouldBeLessThanOrEqualTo(QueryPolicyLimits.MaxCursorLength);
        // A pre-change reader can consume every still-bounded raw cursor during rolling upgrade.
        if (!compact)
        {
            firstCodec.TryDecode(cursor, "PartySearch", scope, out string? position, out _).ShouldBeTrue();
            position.ShouldBe("1");
        }

        if (scenario == "tamper")
        {
            int middle = cursor.Length / 2;
            cursor = cursor[..middle] + (cursor[middle] == 'a' ? "b" : "a") + cursor[(middle + 1)..];
        }

        if (scenario is "query" or "type" or "active" or "mode")
        {
            payload[scenario switch { "query" => "Query", "type" => "Type", "active" => "Active", _ => "Mode" }] = scenario switch
            {
                "query" => "other", "type" => "Organization", "active" => false, _ => "DisplayName",
            };
        }

        envelope = envelope with
        {
            Paging = new QueryPagingOptions(PageSize: scenario == "page-size" ? 2 : 1, Cursor: cursor),
            Payload = JsonSerializer.SerializeToUtf8Bytes(payload),
            UserId = scenario == "user" ? "other-user" : envelope.UserId,
            TenantId = scenario == "tenant" ? "other-tenant" : envelope.TenantId,
        };
        store.ClearReceivedCalls();
        var nextHandler = new PartySearchQueryHandler(CreateService(store, new LocalFuzzyPartySearchProvider(), cursorCodec: CreateCursorCodec(keys)));
        QueryResult result = await nextHandler.ExecuteAsync(envelope, TestContext.Current.CancellationToken);
        if (scenario == "continue")
        {
            result.Success.ShouldBeTrue();
            result.GetPayload().Deserialize<PagedResult<PartySearchResult>>(PartiesJsonOptions.Default)!
                .Items.ShouldHaveSingleItem().Party.Id.ShouldBe("party-b");
        }
        else
        {
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidCursor);
            store.ReceivedCalls().ShouldBeEmpty();
        }
    }

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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.GetPayload().Deserialize<PartyDetail>(PartiesJsonOptions.Default)!.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task DetailHandlers_ErasedPartyReturnSameSafeMissingResultAsync()
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        QueryResult partyDetail = await new PartyDetailQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.PartyDetailQueryType),
            TestContext.Current.CancellationToken);

        getParty.Success.ShouldBeFalse();
        partyDetail.Success.ShouldBeFalse();
        getParty.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
        partyDetail.ErrorMessage.ShouldBe(getParty.ErrorMessage);
        getParty.PayloadBytes.ShouldBeNull();
        partyDetail.PayloadBytes.ShouldBeNull();
        getParty.Metadata.ShouldBeNull();
        partyDetail.Metadata.ShouldBeNull();
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
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
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType),
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
        QueryEnvelope envelope = CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType) with
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        QueryResult export = await new ExportPartyDataQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.ExportPartyDataQueryType),
            TestContext.Current.CancellationToken);
        QueryResult index = await new PartyIndexQueryHandler(service).ExecuteAsync(
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType),
            TestContext.Current.CancellationToken);

        detail.Success.ShouldBeFalse();
        export.Success.ShouldBeFalse();
        index.Success.ShouldBeFalse();
        detail.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
        export.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
        index.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
    }

    [Theory]
    [InlineData(null, "Verified")]
    [InlineData(null, "Erased")]
    [InlineData("KeyDestroyed", "Erased")]
    [InlineData("Verified", "Active")]
    [InlineData("Erased", "KeyDestroyed")]
    public async Task ErasureStatusHandlerIgnoresLegacyStatusAuthorityAsync(string? committed, string legacy)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1"),
                LastSequenceNumber = 9,
                ErasureLifecycle = committed is null ? null : new PartyErasureStatusRecord
                {
                    PartyId = "party-1", TenantId = "tenant-a", Status = committed, UpdatedAt = s_now,
                },
                ProjectedAt = s_now,
            }, "committed-etag"));
        IPartyErasureRecordStore legacyStore = Substitute.For<IPartyErasureRecordStore>();
        legacyStore.GetStatusAsync("tenant-a", "party-1", Arg.Any<CancellationToken>())
            .Returns(new PartyErasureStatusRecord { PartyId = "party-1", TenantId = "tenant-a", Status = legacy, UpdatedAt = s_now.AddMinutes(1) });
        QueryResult result = await new GetErasureStatusQueryHandler(CreateService(store, recordStore: legacyStore))
            .ExecuteAsync(CreateDetailEnvelope(PartyDetailQueryTypes.GetErasureStatusQueryType), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        PartyErasureStatusRecord? status = result.GetPayload().Deserialize<PartyErasureStatusRecord>(PartiesJsonOptions.Default);
        status?.Status.ShouldBe(committed);
        await legacyStore.DidNotReceiveWithAnyArgs().GetStatusAsync(default!, default!, default);
    }

    [Fact]
    public async Task ErasureStatusHandler_LegacyPrivacyTombstoneCannotCertifyCommittedErasureAsync()
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetErasureStatusQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.GetPayload().Deserialize<PartyErasureStatusRecord>(PartiesJsonOptions.Default).ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_MismatchedDiscriminator_FailsClosedWithoutStoreReadAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new GetPartyQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.PartyDetailQueryType),
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
                CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
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
                CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
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
            CreateDetailEnvelope(PartyDetailQueryTypes.ExportPartyDataQueryType),
            TestContext.Current.CancellationToken);

        IReadModelStore cursorStore = Substitute.For<IReadModelStore>();
        IQueryCursorCodec codec = CreateCursorCodec();
        QueryResult rejected = await new PartyIndexQueryHandler(CreateService(cursorStore, cursorCodec: codec, logger: logger)).ExecuteAsync(
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType) with
            {
                Paging = new QueryPagingOptions(
                    PageSize: 20,
                    Cursor: codec.Encode(PartyIndexQueryTypes.PartyIndexQueryType, "different-scope", "20")),
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
        var handler = new PartyIndexQueryHandler(CreateService(store, cursorCodec: CreateCursorCodec()));

        QueryResult first = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType) with
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
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType) with
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
        IQueryCursorCodec codec = CreateCursorCodec();
        string cursor = codec.Encode(
            PartyIndexQueryTypes.PartyIndexQueryType,
            "different-scope",
            "2");
        var handler = new PartyIndexQueryHandler(CreateService(store, cursorCodec: codec));

        QueryResult result = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType) with
            {
                Paging = new QueryPagingOptions(PageSize: 2, Cursor: cursor),
            },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidCursor);
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyIndexSdkReadModel>(default!, default!, default);
    }

    [Fact]
    public async Task DetailHandler_StateStoreFailureRejectsAndEvictsPersonalLastKnownDataAsync()
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
        var cache = new PartySdkLastKnownReadModelCache();
        var handler = new GetPartyQueryHandler(CreateService(store, lastKnownCache: cache));

        QueryResult current = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        QueryResult degraded = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
            TestContext.Current.CancellationToken);

        current.Success.ShouldBeTrue();
        degraded.Success.ShouldBeFalse();
        degraded.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
        degraded.Metadata.ShouldBeNull();
        cache.TryGetDetail("tenant-a", "party-1", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task IndexHandler_StateStoreFailureRejectsAndEvictsNonemptyLastKnownIndexAsync()
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
        var cache = new PartySdkLastKnownReadModelCache();
        var handler = new PartyIndexQueryHandler(CreateService(store, lastKnownCache: cache));

        QueryResult current = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType),
            TestContext.Current.CancellationToken);
        QueryResult degraded = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType),
            TestContext.Current.CancellationToken);

        current.Success.ShouldBeTrue();
        degraded.Success.ShouldBeFalse();
        degraded.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
        degraded.Metadata.ShouldBeNull();
        cache.TryGetIndex("tenant-a", out _).ShouldBeFalse();
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
                ErasureLifecycle = new PartyErasureStatusRecord
                {
                    PartyId = "party-1", TenantId = "tenant-a", Status = "ErasurePending", UpdatedAt = s_now,
                },
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
            CreateDetailEnvelope(PartyDetailQueryTypes.ExportPartyDataQueryType),
            TestContext.Current.CancellationToken);
        QueryResult processingResult = await new GetProcessingRecordsQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetProcessingRecordsQueryType),
            TestContext.Current.CancellationToken);
        QueryResult statusResult = await new GetErasureStatusQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetErasureStatusQueryType),
            TestContext.Current.CancellationToken);
        QueryResult certificateResult = await new GetErasureCertificateQueryHandler(service).ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetErasureCertificateQueryType),
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
            CreateDetailEnvelope(PartyDetailQueryTypes.ExportPartyDataQueryType),
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetErasureCertificateQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.GetPayload().ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ErasureStatusHandler_StoreFailureReturnsBoundedFailureInsteadOfThrowingAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IPartyErasureRecordStore erasure = Substitute.For<IPartyErasureRecordStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", PartySdkReadModelAddresses.Detail("tenant-a", "party-1"), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReadModelEntry<PartyDetailSdkReadModel>>(new InvalidOperationException("store unavailable")));
        var handler = new GetErasureStatusQueryHandler(CreateService(store, recordStore: erasure));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetErasureStatusQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
        await store.Received(1).GetAsync<PartyDetailSdkReadModel>("statestore", PartySdkReadModelAddresses.Detail("tenant-a", "party-1"), Arg.Any<CancellationToken>());
        await erasure.DidNotReceiveWithAnyArgs().GetStatusAsync(default!, default!, default);
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetErasureCertificateQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
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
        long generation = cache.BeginRead(PartySdkReadModelAddresses.Detail("tenant-a", "party-1"));

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
    public void LastKnownReadModelCache_EvictionDoesNotInvalidateUnrelatedKeyStore()
    {
        var cache = new PartySdkLastKnownReadModelCache();
        long detailGeneration = cache.BeginRead(PartySdkReadModelAddresses.Detail("tenant-a", "party-1"));

        cache.EvictIndex("tenant-a");
        bool stored = cache.StoreDetailIfCurrent(
            "tenant-a",
            "party-1",
            detailGeneration,
            new PartyDetailSdkReadModel { Detail = Detail("party-1") });

        stored.ShouldBeTrue();
        cache.TryGetDetail("tenant-a", "party-1", out PartyDetailSdkReadModel? detail).ShouldBeTrue();
        detail!.Detail!.Id.ShouldBe("party-1");
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
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
        result.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        firstResult.Success.ShouldBeTrue();

        // Simulate the erasure cleanup step evicting the cache (PartiesServiceCollectionExtensions'
        // "projection-cache" erasure cleanup delegate), then a store outage before any post-erasure
        // read repopulates it.
        lastKnownCache.EvictDetail("tenant-a", "party-1");
        QueryResult degradedResult = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
            TestContext.Current.CancellationToken);

        // The eviction means ReadDetailModelAsync's exception filter no longer has a cached value
        // to fall back to, so the store failure propagates to ReadDetailAsync's outer catch as a
        // bounded ActorException failure rather than ever returning the stale pre-erasure PII.
        degradedResult.Success.ShouldBeFalse();
        degradedResult.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
    }

    [Fact]
    public async Task DetailHandler_ObservedCanonicalAbsenceInvalidatesLastKnownValueAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var cached = new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
        {
            Detail = Detail("party-1") with { DisplayName = "Stale personal value" },
            LastSequenceNumber = 1,
        }, "etag-old");
        int readCount = 0;
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => readCount++ switch
            {
                0 => Task.FromResult(cached),
                1 => Task.FromResult(new ReadModelEntry<PartyDetailSdkReadModel>(null, null)),
                _ => Task.FromException<ReadModelEntry<PartyDetailSdkReadModel>>(new InvalidOperationException("outage")),
            });
        var cache = new PartySdkLastKnownReadModelCache();
        var handler = new GetPartyQueryHandler(CreateService(store, lastKnownCache: cache));

        (await handler.ExecuteAsync(CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType), TestContext.Current.CancellationToken))
            .Success.ShouldBeTrue();
        QueryResult missing = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        QueryResult outage = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
            TestContext.Current.CancellationToken);

        missing.Success.ShouldBeFalse();
        missing.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
        outage.Success.ShouldBeFalse();
        outage.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
        cache.TryGetDetail("tenant-a", "party-1", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task IndexHandler_ObservedCanonicalAbsenceInvalidatesLastKnownValueAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var cached = new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
        {
            Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["party-1"] = IndexEntry("party-1") with { DisplayName = "Stale personal value" },
            },
        }, "etag-old");
        int readCount = 0;
        store.GetAsync<PartyIndexSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => readCount++ switch
            {
                0 => Task.FromResult(cached),
                1 => Task.FromResult(new ReadModelEntry<PartyIndexSdkReadModel>(null, null)),
                _ => Task.FromException<ReadModelEntry<PartyIndexSdkReadModel>>(new InvalidOperationException("outage")),
            });
        var cache = new PartySdkLastKnownReadModelCache();
        var handler = new PartyIndexQueryHandler(CreateService(store, lastKnownCache: cache));

        (await handler.ExecuteAsync(CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType), TestContext.Current.CancellationToken))
            .Success.ShouldBeTrue();
        QueryResult missing = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType),
            TestContext.Current.CancellationToken);
        QueryResult outage = await handler.ExecuteAsync(
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType),
            TestContext.Current.CancellationToken);

        missing.Success.ShouldBeFalse();
        missing.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
        outage.Success.ShouldBeFalse();
        outage.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
        cache.TryGetIndex("tenant-a", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessingHandler_ObservedCanonicalAbsenceInvalidatesLastKnownRecordsAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1"),
            }, "detail-etag"));
        var staleActivity = new ProcessingActivityRecord
        {
            SequenceNumber = 1,
            PartyId = "party-1",
            TenantId = "tenant-a",
            ActorId = "actor",
            CorrelationId = "correlation",
            OperationCategory = "PartyCommand",
            Outcome = "Succeeded",
            EventType = "PartyCreated",
            Timestamp = s_now,
            Summary = "Party record created.",
        };
        int readCount = 0;
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => readCount++ switch
            {
                0 => Task.FromResult(new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel
                {
                    Records = [staleActivity],
                }, "etag-old")),
                1 => Task.FromResult(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null)),
                _ => Task.FromException<ReadModelEntry<PartyProcessingSdkReadModel>>(new InvalidOperationException("outage")),
            });
        var cache = new PartySdkLastKnownReadModelCache();
        var handler = new GetProcessingRecordsQueryHandler(CreateService(store, lastKnownCache: cache));

        QueryResult first = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetProcessingRecordsQueryType),
            TestContext.Current.CancellationToken);
        QueryResult missing = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetProcessingRecordsQueryType),
            TestContext.Current.CancellationToken);
        QueryResult outage = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetProcessingRecordsQueryType),
            TestContext.Current.CancellationToken);

        first.GetPayload().Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!.ShouldHaveSingleItem();
        missing.GetPayload().Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!.ShouldBeEmpty();
        outage.GetPayload().Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!.ShouldBeEmpty();
        cache.TryGetProcessing("tenant-a", "party-1", out _).ShouldBeFalse();
    }

    [Fact]
    public void LastKnownReadModelCache_MissingKeyChurnDoesNotRetainHistoricalIdentifiers()
    {
        const int capacity = 4;
        var cache = new PartySdkLastKnownReadModelCache(TimeProvider.System, capacity, TimeSpan.FromMinutes(1));

        for (int index = 0; index < 512; index++)
        {
            cache.EvictDetail("tenant-a", $"missing-party-{index}");
        }

        int retainedEntries = typeof(PartySdkLastKnownReadModelCache)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Select(field => field.GetValue(cache))
            .OfType<System.Collections.IDictionary>()
            .Sum(dictionary => dictionary.Count);
        retainedEntries.ShouldBe(0);
    }

    [Fact]
    public void LastKnownReadModelCache_ObservedAbsenceGenerationRejectsLateStoresForEverySlot()
    {
        var cache = new PartySdkLastKnownReadModelCache();
        long detailGeneration = cache.BeginRead(PartySdkReadModelAddresses.Detail("tenant-a", "party-1"));
        long processingGeneration = cache.BeginRead(PartySdkReadModelAddresses.Processing("tenant-a", "party-1"));
        long indexGeneration = cache.BeginRead(PartySdkReadModelAddresses.Index("tenant-a"));

        cache.EvictDetail("tenant-a", "party-1");
        cache.EvictProcessing("tenant-a", "party-1");
        cache.EvictIndex("tenant-a");

        cache.StoreDetailIfCurrent("tenant-a", "party-1", detailGeneration, new PartyDetailSdkReadModel()).ShouldBeFalse();
        cache.StoreProcessingIfCurrent("tenant-a", "party-1", processingGeneration, new PartyProcessingSdkReadModel()).ShouldBeFalse();
        cache.StoreIndexIfCurrent("tenant-a", indexGeneration, new PartyIndexSdkReadModel()).ShouldBeFalse();
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("processing")]
    [InlineData("index")]
    public void LastKnownReadModelCache_ActiveTokensAreBoundedAndReleasedCapacityIsReusable(string slot)
    {
        var cache = new PartySdkLastKnownReadModelCache(TimeProvider.System, 2, TimeSpan.FromMinutes(1));
        string key = CacheKey(slot);
        long first = cache.BeginRead(key);
        long second = cache.BeginRead(key);
        long untracked = cache.BeginRead(key);

        first.ShouldBeGreaterThan(0);
        second.ShouldBeGreaterThan(first);
        untracked.ShouldBe(0);
        ActiveReadCount(cache).ShouldBe(2);
        StoreCacheSlotIfCurrent(cache, slot, untracked).ShouldBeFalse();

        cache.EndRead(first);
        long fresh = cache.BeginRead(key);
        fresh.ShouldBeGreaterThan(second);
        cache.EndRead(first);
        StoreCacheSlotIfCurrent(cache, slot, first).ShouldBeFalse();
        StoreCacheSlotIfCurrent(cache, slot, second).ShouldBeTrue();
        StoreCacheSlotIfCurrent(cache, slot, fresh).ShouldBeTrue();
        cache.EndRead(second);
        cache.EndRead(fresh);
        ActiveReadCount(cache).ShouldBe(0);
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("processing")]
    [InlineData("index")]
    public void LastKnownReadModelCache_EvictionAndChurnPreserveUnrelatedTokensWithoutRevivingOldReads(string slot)
    {
        var cache = new PartySdkLastKnownReadModelCache(TimeProvider.System, 3, TimeSpan.FromMinutes(1));
        long first = cache.BeginRead(CacheKey(slot));
        long second = cache.BeginRead(CacheKey(slot));
        long unrelated = cache.BeginRead(CacheKey(slot, "tenant-b"));

        EvictCacheSlot(cache, slot);
        for (int index = 0; index < 512; index++)
        {
            cache.EvictDetail("tenant-churn", $"missing-party-{index}");
        }

        ActiveReadCount(cache).ShouldBe(1);
        long fresh = cache.BeginRead(CacheKey(slot));
        fresh.ShouldBeGreaterThan(second);
        cache.EndRead(first);
        cache.EndRead(second);
        StoreCacheSlotIfCurrent(cache, slot, first).ShouldBeFalse();
        StoreCacheSlotIfCurrent(cache, slot, second).ShouldBeFalse();
        StoreCacheSlotIfCurrent(cache, slot, unrelated, "tenant-b").ShouldBeTrue();
        StoreCacheSlotIfCurrent(cache, slot, fresh).ShouldBeTrue();
        cache.EndRead(unrelated);
        cache.EndRead(fresh);
        ActiveReadCount(cache).ShouldBe(0);
    }

    [Fact]
    public void LastKnownReadModelCache_TokensAuthorizeOnlyTheirExactTenantPartyAndSlot()
    {
        var cache = new PartySdkLastKnownReadModelCache();
        long generation = cache.BeginRead(CacheKey("detail"));

        StoreCacheSlotIfCurrent(cache, "detail", generation, "tenant-b").ShouldBeFalse();
        cache.StoreDetailIfCurrent("tenant-a", "party-2", generation, new PartyDetailSdkReadModel()).ShouldBeFalse();
        StoreCacheSlotIfCurrent(cache, "processing", generation).ShouldBeFalse();
        StoreCacheSlotIfCurrent(cache, "index", generation).ShouldBeFalse();
        StoreCacheSlotIfCurrent(cache, "detail", generation).ShouldBeTrue();
        cache.EndRead(generation);
        StoreCacheSlotIfCurrent(cache, "detail", generation).ShouldBeFalse();
    }

    [Fact]
    public void LastKnownReadModelCache_ExhaustedTokenSpaceNeverWrapsOrReauthorizesAnOldRead()
    {
        var cache = new PartySdkLastKnownReadModelCache();
        typeof(PartySdkLastKnownReadModelCache)
            .GetField("_lastReadToken", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(cache, long.MaxValue - 1);

        long last = cache.BeginRead(CacheKey("detail"));
        last.ShouldBe(long.MaxValue);
        cache.BeginRead(CacheKey("detail")).ShouldBe(0);
        StoreCacheSlotIfCurrent(cache, "detail", last).ShouldBeTrue();
        cache.EndRead(last);
        cache.BeginRead(CacheKey("detail")).ShouldBe(0);
        StoreCacheSlotIfCurrent(cache, "detail", last).ShouldBeFalse();
        StoreCacheSlotIfCurrent(cache, "detail", 0).ShouldBeFalse();
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("processing")]
    [InlineData("index")]
    public void LastKnownReadModelCache_ConvenienceStoresReleaseTheirReadTokens(string slot)
    {
        var cache = new PartySdkLastKnownReadModelCache(TimeProvider.System, 1, TimeSpan.FromMinutes(1));
        for (int index = 0; index < 4; index++)
        {
            StoreCacheSlot(cache, slot);
            ActiveReadCount(cache).ShouldBe(0);
        }

        long generation = cache.BeginRead(CacheKey(slot));
        generation.ShouldBeGreaterThan(0);
        cache.EndRead(generation);
    }

    [Fact]
    public void LastKnownReadModelCache_FailedConvenienceStoresReleaseTheirReadTokens()
    {
        var cache = new PartySdkLastKnownReadModelCache(TimeProvider.System, 1, TimeSpan.FromMinutes(1));

        Should.Throw<ArgumentNullException>(() => cache.StoreDetail("tenant-a", "party-1", null!));
        Should.Throw<ArgumentNullException>(() => cache.StoreProcessing("tenant-a", "party-1", null!));
        Should.Throw<ArgumentNullException>(() => cache.StoreIndex("tenant-a", null!));

        ActiveReadCount(cache).ShouldBe(0);
        long generation = cache.BeginRead(CacheKey("detail"));
        generation.ShouldBeGreaterThan(0);
        cache.EndRead(generation);
    }

    [Fact]
    public void LastKnownReadModelCache_StoreFailureAfterTokenRegistrationReleasesCapacity()
    {
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_ => throw new InvalidOperationException("clock unavailable"));
        var cache = new PartySdkLastKnownReadModelCache(clock, 1, TimeSpan.FromMinutes(1));

        Should.Throw<InvalidOperationException>(() => cache.StoreDetail("tenant-a", "party-1", new PartyDetailSdkReadModel()));

        ActiveReadCount(cache).ShouldBe(0);
        long generation = cache.BeginRead(CacheKey("detail"));
        generation.ShouldBeGreaterThan(0);
        cache.EndRead(generation);
    }

    [Theory]
    [InlineData("detail", false)]
    [InlineData("detail", true)]
    [InlineData("processing", false)]
    [InlineData("processing", true)]
    [InlineData("index", false)]
    [InlineData("index", true)]
    public async Task SdkQueries_TrackingSaturationPreservesFallbackAndRecoversWhenCapacityIsReleasedAsync(string slot, bool seeded)
    {
        var cache = new PartySdkLastKnownReadModelCache(TimeProvider.System, 2, TimeSpan.FromMinutes(1));
        if (slot == "processing")
        {
            cache.StoreDetail("tenant-a", "party-1", new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1") with { IsErased = true, ErasedAt = s_now },
                ProjectionVersion = "cached",
            });
        }

        if (seeded)
        {
            if (slot == "detail")
            {
                cache.StoreDetail("tenant-a", "party-1", new PartyDetailSdkReadModel
                {
                    Detail = Detail("party-1") with { IsErased = true, ErasedAt = s_now },
                    ProjectionVersion = "cached",
                });
            }
            else
            {
                StoreCacheSlot(cache, slot, "cached");
            }
        }

        long firstBlocking = cache.BeginRead("first-unrelated-in-flight-read");
        long secondBlocking = cache.BeginRead("second-unrelated-in-flight-read");
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail("party-1"),
                ProjectionVersion = "canonical",
            }, "etag"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel { ProjectionVersion = "canonical" }, "etag"));
        store.GetAsync<PartyIndexSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel { ProjectionVersion = "canonical" }, "etag"));
        PartySdkQueryService service = CreateService(store, lastKnownCache: cache);

        QueryResult saturated = await ExecuteCacheQueryAsync(service, slot, TestContext.Current.CancellationToken);
        saturated.Success.ShouldBe((seeded && slot != "detail") || slot == "processing");
        if (saturated.Success)
        {
            saturated.Metadata!.IsDegraded.ShouldBe(true);
            saturated.Metadata.ProjectionVersion.ShouldBe(seeded ? "cached" : null);
        }
        else
        {
            saturated.ErrorMessage.ShouldBe(slot == "detail" && seeded ? PartySdkQueryFailureReason.ReadModelUnavailable : PartySdkQueryFailureReason.QueryExecutionFailure);
        }

        string? cachedVersion = slot switch
        {
            "detail" => cache.TryGetDetail("tenant-a", "party-1", out PartyDetailSdkReadModel? detail) ? detail!.ProjectionVersion : null,
            "processing" => cache.TryGetProcessing("tenant-a", "party-1", out PartyProcessingSdkReadModel? processing) ? processing!.ProjectionVersion : null,
            _ => cache.TryGetIndex("tenant-a", out PartyIndexSdkReadModel? index) ? index!.ProjectionVersion : null,
        };
        cachedVersion.ShouldBe(seeded ? "cached" : null);
        ActiveReadCount(cache).ShouldBe(2);

        cache.EndRead(firstBlocking);
        cache.EndRead(secondBlocking);
        QueryResult recovered = await ExecuteCacheQueryAsync(service, slot, TestContext.Current.CancellationToken);
        recovered.Success.ShouldBeTrue();
        recovered.Metadata!.ProjectionVersion.ShouldBe("canonical");
        ActiveReadCount(cache).ShouldBe(0);
    }

    [Fact]
    public async Task LastKnownReadModelCache_ConcurrentReadsRespectCapacityAndReleaseEveryRegistrationAsync()
    {
        const int capacity = 8;
        const int attempts = 32;
        var cache = new PartySdkLastKnownReadModelCache(TimeProvider.System, capacity, TimeSpan.FromMinutes(1));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new System.Collections.Concurrent.ConcurrentBag<long>();
        int count = 0;
        Task[] reads = Enumerable.Range(0, attempts).Select(index => Task.Run(async () =>
        {
            await start.Task.ConfigureAwait(true);
            string tenantId = $"tenant-{index}";
            long generation = cache.BeginRead(CacheKey("detail", tenantId));
            try
            {
                tokens.Add(generation);
                if (Interlocked.Increment(ref count) == attempts)
                {
                    registered.SetResult();
                }

                await release.Task.ConfigureAwait(true);
                StoreCacheSlotIfCurrent(cache, "detail", generation, tenantId).ShouldBe(generation > 0);
            }
            finally
            {
                cache.EndRead(generation);
            }
        }, TestContext.Current.CancellationToken)).ToArray();

        start.SetResult();
        try
        {
            await registered.Task.WaitAsync(TestContext.Current.CancellationToken);
            ActiveReadCount(cache).ShouldBe(capacity);
            tokens.Count(generation => generation > 0).ShouldBe(capacity);
            tokens.Where(generation => generation > 0).Distinct().Count().ShouldBe(capacity);
            tokens.Count(generation => generation == 0).ShouldBe(attempts - capacity);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(reads).ConfigureAwait(true);
        }

        ActiveReadCount(cache).ShouldBe(0);
        long fresh = cache.BeginRead(CacheKey("detail"));
        fresh.ShouldBeGreaterThan(0);
        cache.EndRead(fresh);
    }

    [Theory]
    [InlineData("detail", "success")]
    [InlineData("detail", "missing")]
    [InlineData("detail", "failure")]
    [InlineData("detail", "cancellation")]
    [InlineData("detail", "cached-cancellation")]
    [InlineData("detail", "degraded")]
    [InlineData("detail", "unsafe")]
    [InlineData("processing", "success")]
    [InlineData("processing", "missing")]
    [InlineData("processing", "failure")]
    [InlineData("processing", "cancellation")]
    [InlineData("processing", "cached-cancellation")]
    [InlineData("processing", "degraded")]
    [InlineData("index", "success")]
    [InlineData("index", "missing")]
    [InlineData("index", "failure")]
    [InlineData("index", "cancellation")]
    [InlineData("index", "cached-cancellation")]
    [InlineData("index", "degraded")]
    [InlineData("index", "unsafe")]
    public async Task SdkQueries_ReleaseReadTrackingForEveryOutcomeAsync(string slot, string outcome)
    {
        int capacity = slot == "processing" && outcome == "degraded" ? 2 : 1;
        var cache = new PartySdkLastKnownReadModelCache(TimeProvider.System, capacity, TimeSpan.FromMinutes(1));
        if (outcome is "degraded" or "unsafe" or "cached-cancellation")
        {
            if (outcome == "degraded" && slot == "detail")
            {
                cache.StoreDetail("tenant-a", "party-1", new PartyDetailSdkReadModel
                {
                    Detail = Detail("party-1") with { IsErased = true, ErasedAt = s_now },
                });
            }
            else if (outcome is "unsafe" or "cached-cancellation" && slot == "index")
            {
                cache.StoreIndex("tenant-a", new PartyIndexSdkReadModel
                {
                    Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal) { ["party-1"] = IndexEntry("party-1") },
                });
            }
            else
            {
                StoreCacheSlot(cache, slot);
            }
        }

        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ReadOutcome(new PartyDetailSdkReadModel { Detail = Detail("party-1") }, "detail"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ReadOutcome(new PartyProcessingSdkReadModel(), "processing"));
        store.GetAsync<PartyIndexSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ReadOutcome(new PartyIndexSdkReadModel(), "index"));
        PartySdkQueryService service = CreateService(store, lastKnownCache: cache);

        for (int index = 0; index < 4; index++)
        {
            if (outcome is "cancellation" or "cached-cancellation")
            {
                await Should.ThrowAsync<OperationCanceledException>(
                    () => ExecuteCacheQueryAsync(service, slot, TestContext.Current.CancellationToken));
            }
            else
            {
                QueryResult result = await ExecuteCacheQueryAsync(service, slot, TestContext.Current.CancellationToken);
                result.Success.ShouldBe(slot == "processing" || outcome == "success" || (outcome == "degraded" && slot != "detail"));
                if (outcome == "degraded" && slot != "detail")
                {
                    result.Metadata!.IsDegraded.ShouldBe(true);
                }
            }

            ActiveReadCount(cache).ShouldBe(0);
            long probe = cache.BeginRead("probe-key");
            probe.ShouldBeGreaterThan(0);
            cache.EndRead(probe);
        }

        Task<ReadModelEntry<TValue>> ReadOutcome<TValue>(TValue value, string currentSlot)
            where TValue : class
            => currentSlot != slot || outcome == "success"
                ? Task.FromResult(new ReadModelEntry<TValue>(value, "etag"))
                : outcome switch
                {
                    "missing" => Task.FromResult(new ReadModelEntry<TValue>(null, null)),
                    "cancellation" or "cached-cancellation" => Task.FromCanceled<ReadModelEntry<TValue>>(new CancellationToken(canceled: true)),
                    _ => Task.FromException<ReadModelEntry<TValue>>(new InvalidOperationException("store unavailable")),
                };
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("processing")]
    [InlineData("index")]
    public async Task SdkQueries_EvictionDuringReadStillRejectsLateValuesAfterMissingKeyChurnAsync(string slot)
    {
        var cache = new PartySdkLastKnownReadModelCache(TimeProvider.System, 2, TimeSpan.FromMinutes(1));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ReadDelayedAsync(new PartyDetailSdkReadModel { Detail = Detail("party-1") }, "detail"));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ReadDelayedAsync(new PartyProcessingSdkReadModel(), "processing"));
        store.GetAsync<PartyIndexSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ReadDelayedAsync(new PartyIndexSdkReadModel(), "index"));
        Task<QueryResult> pending = ExecuteCacheQueryAsync(CreateService(store, lastKnownCache: cache), slot, TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        EvictCacheSlot(cache, slot);
        for (int index = 0; index < 512; index++)
        {
            cache.EvictDetail("tenant-churn", $"missing-party-{index}");
        }

        release.SetResult();
        QueryResult result = await pending.ConfigureAwait(true);
        if (slot == "processing")
        {
            result.Success.ShouldBeTrue();
            result.Metadata!.IsDegraded.ShouldBe(true);
        }
        else
        {
            result.Success.ShouldBeFalse();
        }

        ActiveReadCount(cache).ShouldBe(0);
        bool cached = slot switch
        {
            "detail" => cache.TryGetDetail("tenant-a", "party-1", out _),
            "processing" => cache.TryGetProcessing("tenant-a", "party-1", out _),
            _ => cache.TryGetIndex("tenant-a", out _),
        };
        cached.ShouldBeFalse();

        async Task<ReadModelEntry<TValue>> ReadDelayedAsync<TValue>(TValue value, string currentSlot)
            where TValue : class
        {
            if (currentSlot == slot)
            {
                started.SetResult();
                await release.Task.ConfigureAwait(true);
            }

            return new ReadModelEntry<TValue>(value, "etag-before-erasure");
        }
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
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType) with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(JsonDocument.Parse(json).RootElement),
            },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyIndexSdkReadModel>(default!, default!, default);
    }

    [Theory]
    [InlineData("tenant:escape")]
    [InlineData("tenant-a\n")]
    public async Task DetailHandler_TenantKeySeparatorFailsClosedBeforeStoreReadAsync(string tenantId)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new GetPartyQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType) with { TenantId = tenantId },
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
            CreateIndexEnvelope(PartyIndexQueryTypes.PartySearchQueryType) with
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
            CreateIndexEnvelope(PartyIndexQueryTypes.PartySearchQueryType) with
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
            CreateDetailEnvelope(PartyDetailQueryTypes.PartyDetailQueryType),
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
        QueryEnvelope envelope = CreateIndexEnvelope(PartyIndexQueryTypes.PartySearchQueryType) with
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
        QueryEnvelope envelope = CreateIndexEnvelope(PartyIndexQueryTypes.PartySearchQueryType) with
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
            CreateDetailEnvelope(PartyDetailQueryTypes.ExportPartyDataQueryType),
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
            CreateDetailEnvelope(PartyDetailQueryTypes.ExportPartyDataQueryType),
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetProcessingRecordsQueryType),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetProcessingRecordsQueryType),
            TestContext.Current.CancellationToken);
        QueryResult degraded = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetProcessingRecordsQueryType),
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType),
            TestContext.Current.CancellationToken);
        QueryResult tenantB = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType) with { TenantId = "tenant-b" },
            TestContext.Current.CancellationToken);

        tenantA.Success.ShouldBeTrue();
        tenantB.Success.ShouldBeFalse();
        tenantB.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
        JsonSerializer.Serialize(tenantB, PartiesJsonOptions.Default).ShouldNotContain("Tenant A Secret");
    }

    [Theory]
    [InlineData(PartyDetailQueryTypes.GetPartyQueryType, "party-2")]
    [InlineData(PartyDetailQueryTypes.PartyDetailQueryType, "party-2")]
    [InlineData(PartyDetailQueryTypes.ExportPartyDataQueryType, "party-2")]
    [InlineData(PartyDetailQueryTypes.GetProcessingRecordsQueryType, "party-2")]
    [InlineData(PartyDetailQueryTypes.GetErasureStatusQueryType, "party-2")]
    [InlineData(PartyDetailQueryTypes.GetErasureCertificateQueryType, "party-2")]
    [InlineData(PartyDetailQueryTypes.GetPartyQueryType, "Party-1")]
    [InlineData(PartyDetailQueryTypes.PartyDetailQueryType, "Party-1")]
    [InlineData(PartyDetailQueryTypes.ExportPartyDataQueryType, "Party-1")]
    [InlineData(PartyDetailQueryTypes.GetProcessingRecordsQueryType, "Party-1")]
    [InlineData(PartyDetailQueryTypes.GetErasureStatusQueryType, "Party-1")]
    [InlineData(PartyDetailQueryTypes.GetErasureCertificateQueryType, "Party-1")]
    public async Task DetailHandler_AggregateAndEntityMismatchFailsClosedBeforeStoreReadAsync(string queryType, string entityId)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IPartyErasureRecordStore erasure = Substitute.For<IPartyErasureRecordStore>();
        PartySdkQueryHandler handler = CreateDetailHandler(queryType, CreateService(store, recordStore: erasure));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(queryType) with
            {
                AggregateId = "party-1",
                EntityId = entityId,
            },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        store.ReceivedCalls().ShouldBeEmpty();
        erasure.ReceivedCalls().ShouldBeEmpty();
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
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType) with { TenantId = "tenant-b" },
            TestContext.Current.CancellationToken);
        QueryResult absent = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType) with
            {
                TenantId = "tenant-b",
                AggregateId = "party-missing",
                EntityId = "party-missing",
            },
            TestContext.Current.CancellationToken);

        wrongTenant.Success.ShouldBeFalse();
        absent.Success.ShouldBeFalse();
        wrongTenant.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
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
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType),
            TestContext.Current.CancellationToken);
        QueryResult tenantBIndex = await new PartyIndexQueryHandler(service).ExecuteAsync(
            CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType) with { TenantId = "tenant-b" },
            TestContext.Current.CancellationToken);
        QueryResult tenantBSearch = await new PartySearchQueryHandler(service).ExecuteAsync(
            CreateIndexEnvelope(PartyIndexQueryTypes.PartySearchQueryType) with
            {
                TenantId = "tenant-b",
                Payload = JsonSerializer.SerializeToUtf8Bytes(new { query = "secret", page = 1, pageSize = 20 }),
            },
            TestContext.Current.CancellationToken);

        tenantA.Success.ShouldBeTrue();
        tenantBIndex.Success.ShouldBeFalse();
        tenantBSearch.Success.ShouldBeFalse();
        tenantBIndex.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
        tenantBSearch.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
        JsonSerializer.Serialize(new[] { tenantBIndex, tenantBSearch }, PartiesJsonOptions.Default)
            .ShouldNotContain("Tenant A Secret");
    }

    [Fact]
    public async Task DetailHandler_ReservedPartyIdCharactersFailClosedAsInvalidEnvelopeAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new GetPartyQueryHandler(CreateService(store));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType) with
            {
                AggregateId = "party|1",
                EntityId = "party|1",
            },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyDetailSdkReadModel>(default!, default!, default);
    }

    public static TheoryData<string, string, bool> InvalidDetailQueryIdentifiers
    {
        get
        {
            TheoryData<string, string, bool> cases = [];
            string[] identifiers =
            [
                string.Empty, " ", "party/1", "party\\1", "party:1", "party|1", "party\0x",
                "party\r1", "party\n1", "party\t1", "party\u00011", "party 1", " party-1", "party-1 ",
                "parté1", "party%2F1", "party?1", "party#1", "party\n", ".party", "party-", "_party",
                "{party-1}", new string('a', 129), new string('a', 4096),
                "{g23e4567-e89b-12d3-a456-426614174000}", "(123e4567_e89b-12d3-a456-426614174000)",
                "{123e4567-e89b-12d3-a456-426614174000)",
                "{0x123e4567,0xe89b,0x12d3,{0xa4,0x56,0x42,0x66,0x14,0x17,0x40,0x0g}}",
                "{0x123e4567;0xe89b,0x12d3,{0xa4,0x56,0x42,0x66,0x14,0x17,0x40,0x00}}",
            ];
            foreach (string queryType in DetailQueryTypes)
            {
                foreach (string partyId in identifiers)
                {
                    cases.Add(queryType, partyId, false);
                    cases.Add(queryType, partyId, true);
                }
            }

            return cases;
        }
    }

    public static TheoryData<string, string, string?> ValidDetailQueryIdentifiers
    {
        get
        {
            TheoryData<string, string, string?> cases = [];
            string[] identifiers =
            [
                "a", "Party-1.a_b", "01ARZ3NDEKTSV4RRFFQ69G5FAV", new string('a', 128),
                "123e4567-e89b-12d3-a456-426614174000", "123e4567e89b12d3a456426614174000",
                "{123e4567-e89b-12d3-a456-426614174000}", "(123e4567-e89b-12d3-a456-426614174000)",
                "{0x123e4567,0xe89b,0x12d3,{0xa4,0x56,0x42,0x66,0x14,0x17,0x40,0x00}}",
            ];
            foreach (string queryType in DetailQueryTypes)
            {
                foreach (string partyId in identifiers)
                {
                    cases.Add(queryType, partyId, partyId);
                    cases.Add(queryType, partyId, null);
                    cases.Add(queryType, partyId, string.Empty);
                    cases.Add(queryType, partyId, " \t");
                }
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(InvalidDetailQueryIdentifiers))]
    public async Task DetailHandlers_RejectUnsafeIdentifiersBeforeAnyStoreAccessAsync(string queryType, string partyId, bool omitEntityId)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IPartyErasureRecordStore erasure = Substitute.For<IPartyErasureRecordStore>();
        PartySdkQueryHandler handler = CreateDetailHandler(queryType, CreateService(store, recordStore: erasure));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(queryType) with { AggregateId = partyId, EntityId = omitEntityId ? null : partyId },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        store.ReceivedCalls().ShouldBeEmpty();
        erasure.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(ValidDetailQueryIdentifiers))]
    public async Task DetailHandlers_PreserveSupportedIdentifiersAndOptionalEntityFallbackAsync(
        string queryType,
        string partyId,
        string? entityId)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyDetailSdkReadModel>(
                "statestore", PartySdkReadModelAddresses.Detail("tenant-a", partyId), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(new PartyDetailSdkReadModel
            {
                Detail = Detail(partyId),
                ProjectedAt = s_now,
            }, "etag"));
        var activity = new ProcessingActivityRecord
        {
            SequenceNumber = 1,
            PartyId = partyId,
            TenantId = "tenant-a",
            ActorId = "user-1",
            CorrelationId = "correlation-1",
            OperationCategory = "Consent",
            Outcome = "Succeeded",
            EventType = "ConsentRecorded",
            Timestamp = s_now,
            Summary = "Consent preference changed.",
        };
        store.GetAsync<PartyProcessingSdkReadModel>(
                "statestore", PartySdkReadModelAddresses.Processing("tenant-a", partyId), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel
            {
                Records = [activity],
            }, "processing-etag"));
        IPartyErasureRecordStore erasure = Substitute.For<IPartyErasureRecordStore>();
        erasure.GetStatusAsync("tenant-a", partyId, Arg.Any<CancellationToken>()).Returns(new PartyErasureStatusRecord
        {
            TenantId = "tenant-a",
            PartyId = partyId,
            Status = ErasureStatus.ErasurePending.ToString(),
            UpdatedAt = s_now,
        });
        PartySdkQueryHandler handler = CreateDetailHandler(queryType, CreateService(store, recordStore: erasure));

        QueryResult result = await handler.ExecuteAsync(
            CreateDetailEnvelope(queryType) with { AggregateId = partyId, EntityId = entityId },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        if (queryType == PartyDetailQueryTypes.GetErasureCertificateQueryType)
        {
            await erasure.Received(1).GetCertificateAsync("tenant-a", partyId, Arg.Any<CancellationToken>());
        }
        else
        {
            await store.Received(1).GetAsync<PartyDetailSdkReadModel>(
                "statestore", PartySdkReadModelAddresses.Detail("tenant-a", partyId), Arg.Any<CancellationToken>());
        }

        if (queryType is PartyDetailQueryTypes.ExportPartyDataQueryType or PartyDetailQueryTypes.GetProcessingRecordsQueryType)
        {
            await store.Received(1).GetAsync<PartyProcessingSdkReadModel>(
                "statestore", PartySdkReadModelAddresses.Processing("tenant-a", partyId), Arg.Any<CancellationToken>());
            IReadOnlyList<ProcessingActivityRecord> records = queryType == PartyDetailQueryTypes.ExportPartyDataQueryType
                ? result.GetPayload().Deserialize<PartyDataPortabilityPackage>(PartiesJsonOptions.Default)!.ProcessingRecords
                : result.GetPayload().Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!;
            records.ShouldHaveSingleItem().ShouldBe(activity);
        }
    }

    private static string[] DetailQueryTypes =>
    [
        PartyDetailQueryTypes.GetPartyQueryType,
        PartyDetailQueryTypes.PartyDetailQueryType,
        PartyDetailQueryTypes.ExportPartyDataQueryType,
        PartyDetailQueryTypes.GetProcessingRecordsQueryType,
        PartyDetailQueryTypes.GetErasureStatusQueryType,
        PartyDetailQueryTypes.GetErasureCertificateQueryType,
    ];

    private static PartySdkQueryHandler CreateDetailHandler(string queryType, PartySdkQueryService service)
        => queryType switch
        {
            PartyDetailQueryTypes.GetPartyQueryType => new GetPartyQueryHandler(service),
            PartyDetailQueryTypes.PartyDetailQueryType => new PartyDetailQueryHandler(service),
            PartyDetailQueryTypes.ExportPartyDataQueryType => new ExportPartyDataQueryHandler(service),
            PartyDetailQueryTypes.GetProcessingRecordsQueryType => new GetProcessingRecordsQueryHandler(service),
            PartyDetailQueryTypes.GetErasureStatusQueryType => new GetErasureStatusQueryHandler(service),
            PartyDetailQueryTypes.GetErasureCertificateQueryType => new GetErasureCertificateQueryHandler(service),
            _ => throw new ArgumentOutOfRangeException(nameof(queryType)),
        };

    private static int ActiveReadCount(PartySdkLastKnownReadModelCache cache)
        => ((System.Collections.IDictionary)typeof(PartySdkLastKnownReadModelCache)
            .GetField("_activeReads", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(cache)!).Count;

    private static string CacheKey(string slot, string tenantId = "tenant-a")
        => slot switch
        {
            "detail" => PartySdkReadModelAddresses.Detail(tenantId, "party-1"),
            "processing" => PartySdkReadModelAddresses.Processing(tenantId, "party-1"),
            _ => PartySdkReadModelAddresses.Index(tenantId),
        };

    private static bool StoreCacheSlotIfCurrent(PartySdkLastKnownReadModelCache cache, string slot, long generation, string tenantId = "tenant-a")
        => slot switch
        {
            "detail" => cache.StoreDetailIfCurrent(tenantId, "party-1", generation, new PartyDetailSdkReadModel { Detail = Detail("party-1") }),
            "processing" => cache.StoreProcessingIfCurrent(tenantId, "party-1", generation, new PartyProcessingSdkReadModel()),
            _ => cache.StoreIndexIfCurrent(tenantId, generation, new PartyIndexSdkReadModel()),
        };

    private static void StoreCacheSlot(PartySdkLastKnownReadModelCache cache, string slot, string projectionVersion = "")
    {
        switch (slot)
        {
            case "detail":
                cache.StoreDetail("tenant-a", "party-1", new PartyDetailSdkReadModel { Detail = Detail("party-1"), ProjectionVersion = projectionVersion });
                break;
            case "processing":
                cache.StoreProcessing("tenant-a", "party-1", new PartyProcessingSdkReadModel { ProjectionVersion = projectionVersion });
                break;
            default:
                cache.StoreIndex("tenant-a", new PartyIndexSdkReadModel { ProjectionVersion = projectionVersion });
                break;
        }
    }

    private static void EvictCacheSlot(PartySdkLastKnownReadModelCache cache, string slot)
    {
        switch (slot)
        {
            case "detail":
                cache.EvictDetail("tenant-a", "party-1");
                break;
            case "processing":
                cache.EvictProcessing("tenant-a", "party-1");
                break;
            default:
                cache.EvictIndex("tenant-a");
                break;
        }
    }

    private static Task<QueryResult> ExecuteCacheQueryAsync(PartySdkQueryService service, string slot, CancellationToken cancellationToken)
        => slot switch
        {
            "detail" => service.GetPartyAsync(CreateDetailEnvelope(PartyDetailQueryTypes.GetPartyQueryType), cancellationToken),
            "processing" => service.GetProcessingRecordsAsync(CreateDetailEnvelope(PartyDetailQueryTypes.GetProcessingRecordsQueryType), cancellationToken),
            _ => service.GetPartyIndexAsync(CreateIndexEnvelope(PartyIndexQueryTypes.PartyIndexQueryType), cancellationToken),
        };

    [Theory]
    [InlineData("PartyIndex", null)]
    [InlineData("PartyIndex", "parties")]
    [InlineData("PartySearch", null)]
    [InlineData("PartySearch", "parties")]
    public async Task PublicIndexRoutes_RetainOptionalEntityFallbackAndSdkDispatchAsync(string queryType, string? entityId)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel { Entries = new Dictionary<string, PartyIndexEntry>() }, "etag"));
        PartySdkQueryService service = CreateService(store, new LocalFuzzyPartySearchProvider());
        PartySdkQueryHandler handler = queryType == "PartyIndex" ? new PartyIndexQueryHandler(service) : new PartySearchQueryHandler(service);
        QueryResult result = await handler.ExecuteAsync(CreateIndexEnvelope(queryType) with { EntityId = entityId }, TestContext.Current.CancellationToken);
        result.Success.ShouldBeTrue();
        await store.Received(1).GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("PartyIndex")]
    [InlineData("PartySearch")]
    public async Task PublicIndexRoutes_RejectMismatchedIdentityBeforeReadAsync(string queryType)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartySdkQueryService service = CreateService(store);
        PartySdkQueryHandler handler = queryType == "PartyIndex" ? new PartyIndexQueryHandler(service) : new PartySearchQueryHandler(service);
        QueryResult result = await handler.ExecuteAsync(CreateIndexEnvelope(queryType) with { EntityId = "other" }, TestContext.Current.CancellationToken);
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        result.PayloadBytes.ShouldBeNull();
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyIndexSdkReadModel>(default!, default!, default);
    }

    [Theory]
    [InlineData("PartyIndex", "{\"Type\":\"Person\",\"type\":\"Organization\"}")]
    [InlineData("PartySearch", "{\"Query\":\"Ada\",\"query\":\"Grace\"}")]
    [InlineData("PartyIndex", "{\"Page\":1,\"page\":2}")]
    [InlineData("PartySearch", "{\"CreatedAfter\":\"2026-01-01T00:00:00Z\"}")]
    [InlineData("PartyIndex", "{\"CreatedAfter\":\"not-a-date\"}")]
    [InlineData("PartyIndex", "{\"CreatedAfter\":\" \"}")]
    [InlineData("PartyIndex", "{\"CreatedAfter\":\"2026-01-01T00:00:00Z\\n\"}")]
    [InlineData("PartyIndex", "{\"ModifiedAfter\":\"2026-02-01T00:00:00Z\",\"ModifiedBefore\":\"2026-01-01T00:00:00Z\"}")]
    [InlineData("PartyIndex", "{\"CreatedAfter\":\"\"}")]
    [InlineData("PartyIndex", "{\"Type\":\" \"}")]
    [InlineData("PartyIndex", """{"\ud800":1}""")]
    [InlineData("PartyIndex", """{"Type":"\ud800"}""")]
    [InlineData("PartyIndex", """{"\udc00":1}""")]
    [InlineData("PartyIndex", """{"Type":"\udc00"}""")]
    [InlineData("PartySearch", """{"\ud800":1}""")]
    [InlineData("PartySearch", """{"Query":"\ud800"}""")]
    [InlineData("PartySearch", """{"\udc00":1}""")]
    [InlineData("PartySearch", """{"Query":"\udc00"}""")]
    public async Task PublicQueryPayloads_RejectAmbiguousAliasesAndInvalidFiltersBeforeReadAsync(string queryType, string payload)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartySdkQueryService service = CreateService(store);
        PartySdkQueryHandler handler = queryType == "PartyIndex" ? new PartyIndexQueryHandler(service) : new PartySearchQueryHandler(service);
        QueryResult result = await handler.ExecuteAsync(CreateIndexEnvelope(queryType) with
        { Payload = System.Text.Encoding.UTF8.GetBytes(payload), Paging = new QueryPagingOptions() }, TestContext.Current.CancellationToken);
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        result.PayloadBytes.ShouldBeNull();
        result.Metadata.ShouldBeNull();
        result.ProjectionType.ShouldBeNull();
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyIndexSdkReadModel>(default!, default!, default);
    }

    [Theory]
    [InlineData("PartyIndex")]
    [InlineData("PartySearch")]
    public async Task PublicQueries_FilterErasedAndMixedRowsBeforePagingAndReturnEffectiveTotalsAsync(string queryType)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexEntry[] entries = [
            IndexEntry("party-1") with { DisplayName = "Ada 1" },
            IndexEntry("party-2") with { DisplayName = "Ada 2", IsErased = true },
            IndexEntry("party-3") with { DisplayName = "Ada 3", Type = PartyType.Organization },
            IndexEntry("party-4") with { DisplayName = "Ada 4", IsActive = false },
            IndexEntry("party-5") with { DisplayName = "Ada 5" },
            IndexEntry("party-6") with { DisplayName = "Ada 6" },
        ];
        if (queryType == "PartyIndex")
        {
            entries = [.. entries,
                IndexEntry("created-too-early") with { CreatedAt = s_now.AddDays(-1).AddTicks(-1) },
                IndexEntry("created-too-late") with { CreatedAt = s_now.AddDays(-1).AddTicks(1) },
                IndexEntry("modified-too-early") with { LastModifiedAt = s_now.AddMinutes(-1).AddTicks(-1) },
                IndexEntry("modified-too-late") with { LastModifiedAt = s_now.AddMinutes(-1).AddTicks(1) },
            ];
        }
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
            { Entries = entries.ToDictionary(e => e.Id), ProjectedAt = s_now }, "etag"));
        PartySdkQueryService service = CreateService(store, new LocalFuzzyPartySearchProvider());
        PartySdkQueryHandler handler = queryType == "PartyIndex" ? new PartyIndexQueryHandler(service) : new PartySearchQueryHandler(service);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(queryType == "PartyIndex"
            ? new { Type = "Person", Active = true, Query = (string?)null }
            : new { Type = "Person", Active = true, Query = (string?)"  Ada  " });
        if (queryType == "PartyIndex")
        {
            payload = JsonSerializer.SerializeToUtf8Bytes(new { Type = "Person", Active = true,
                CreatedAfter = s_now.AddDays(-1), CreatedBefore = s_now.AddDays(-1),
                ModifiedAfter = s_now.AddMinutes(-1), ModifiedBefore = s_now.AddMinutes(-1) });
        }
        QueryEnvelope envelope = CreateIndexEnvelope(queryType) with { Payload = payload, Paging = new QueryPagingOptions(PageSize: 2) };
        QueryResult first = await handler.ExecuteAsync(envelope, TestContext.Current.CancellationToken);
        first.Success.ShouldBeTrue();
        JsonElement page = first.GetPayload();
        page.GetProperty("totalCount").GetInt32().ShouldBe(3);
        page.GetProperty("items").GetArrayLength().ShouldBe(2);
        string[] ids = page.GetProperty("items").EnumerateArray().Select(e => (queryType == "PartyIndex" ? e : e.GetProperty("party")).GetProperty("id").GetString()!).ToArray();
        ids.ShouldBe(["party-1", "party-5"]);
        first.Metadata!.Paging!.PageSize.ShouldBe(2);
        first.Metadata.Paging.Offset.ShouldBe(0);
        first.Metadata.Paging.TotalCount.ShouldBe(3);
        first.Metadata.Paging.HasMore.ShouldBe(true);
        QueryResult second = await handler.ExecuteAsync(envelope with { Paging = new QueryPagingOptions(PageSize: 2, Cursor: first.Metadata.Paging.NextCursor) }, TestContext.Current.CancellationToken);
        second.Success.ShouldBeTrue();
        second.Metadata!.Paging!.Offset.ShouldBe(2);
        second.Metadata.Paging.HasMore.ShouldBe(false);
        second.Metadata.Paging.NextCursor.ShouldBeNull();
        JsonElement last = second.GetPayload().GetProperty("items").EnumerateArray().Single();
        (queryType == "PartyIndex" ? last : last.GetProperty("party")).GetProperty("id").GetString().ShouldBe("party-6");
    }

    [Theory]
    [InlineData("PartyIndex", false)]
    [InlineData("PartyIndex", true)]
    [InlineData("PartySearch", false)]
    [InlineData("PartySearch", true)]
    public async Task EnvelopePagingAloneGovernsAndLegacyPayloadPagingRemainsAvailableAsync(string queryType, bool envelopePresent)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexEntry[] entries = Enumerable.Range(1, 30).Select(i => IndexEntry($"party-{i:00}") with { DisplayName = $"Ada {i:00}" }).ToArray();
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel { Entries = entries.ToDictionary(e => e.Id) }, "etag"));
        PartySdkQueryService service = CreateService(store, new LocalFuzzyPartySearchProvider());
        PartySdkQueryHandler handler = queryType == "PartyIndex" ? new PartyIndexQueryHandler(service) : new PartySearchQueryHandler(service);
        string payload = queryType == "PartyIndex" ? "{\"Page\":2,\"PageSize\":2}" : "{\"Query\":\"Ada\",\"Page\":2,\"PageSize\":2}";
        QueryEnvelope envelope = CreateIndexEnvelope(queryType) with { Payload = System.Text.Encoding.UTF8.GetBytes(payload), Paging = envelopePresent ? new QueryPagingOptions() : null };
        QueryResult result = await handler.ExecuteAsync(envelope, TestContext.Current.CancellationToken);
        result.Success.ShouldBeTrue();
        result.GetPayload().GetProperty("page").GetInt32().ShouldBe(envelopePresent ? 1 : 2);
        result.GetPayload().GetProperty("pageSize").GetInt32().ShouldBe(envelopePresent ? 20 : 2);
        result.GetPayload().GetProperty("items").GetArrayLength().ShouldBe(envelopePresent ? 20 : 2);
        if (envelopePresent)
        {
            result.Metadata!.Paging!.Offset.ShouldBe(0);
            result.Metadata.Paging.PageSize.ShouldBe(20);
            envelope = envelope with { Payload = System.Text.Encoding.UTF8.GetBytes(queryType == "PartyIndex" ? "{\"Page\":-1,\"PageSize\":\"ignored\"}" : "{\"Query\":\"Ada\",\"Page\":-1,\"PageSize\":\"ignored\"}") };
            (await handler.ExecuteAsync(envelope, TestContext.Current.CancellationToken)).Success.ShouldBeTrue();
        }
        else
        {
            result.Metadata!.Paging.ShouldBeNull();
        }
    }

    [Theory]
    [InlineData("PartyIndex", 0, 0, null)]
    [InlineData("PartyIndex", 101, 0, null)]
    [InlineData("PartyIndex", 2, -1, null)]
    [InlineData("PartyIndex", 2, 1, null)]
    [InlineData("PartyIndex", 2, 0, "invalid")]
    [InlineData("PartySearch", 101, 0, null)]
    [InlineData("PartySearch", 2, 1, null)]
    [InlineData("PartySearch", 2, null, "invalid")]
    public async Task PublicPaging_RejectsInvalidBoundsCursorAndOffsetBeforeReadAsync(string queryType, int pageSize, int? offset, string? cursor)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartySdkQueryService service = CreateService(store);
        PartySdkQueryHandler handler = queryType == "PartyIndex" ? new PartyIndexQueryHandler(service) : new PartySearchQueryHandler(service);
        QueryResult result = await handler.ExecuteAsync(CreateIndexEnvelope(queryType) with { Paging = new QueryPagingOptions(PageSize: pageSize, Cursor: cursor, Offset: offset) }, TestContext.Current.CancellationToken);
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(pageSize is < 1 or > 100
            ? QueryAdapterFailureReason.InvalidPage : QueryAdapterFailureReason.InvalidCursor);
        result.PayloadBytes.ShouldBeNull();
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyIndexSdkReadModel>(default!, default!, default);
    }

    [Fact]
    public async Task SearchCaseFilterIsRejectedBeforeReadAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartySearchQueryHandler(CreateService(store));
        QueryResult result = await handler.ExecuteAsync(CreateIndexEnvelope("PartySearch") with { Payload = JsonSerializer.SerializeToUtf8Bytes(new { Query = "Ada", CaseId = "unsupported" }) }, TestContext.Current.CancellationToken);
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.UnsupportedQueryType);
        result.PayloadBytes.ShouldBeNull();
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyIndexSdkReadModel>(default!, default!, default);
    }

    [Theory]
    [InlineData("PartyIndex", "tenant")]
    [InlineData("PartyIndex", "user")]
    [InlineData("PartyIndex", "type")]
    [InlineData("PartyIndex", "active")]
    [InlineData("PartyIndex", "date")]
    [InlineData("PartyIndex", "size")]
    [InlineData("PartySearch", "tenant")]
    [InlineData("PartySearch", "user")]
    [InlineData("PartySearch", "type")]
    [InlineData("PartySearch", "active")]
    [InlineData("PartySearch", "query")]
    [InlineData("PartySearch", "size")]
    public async Task PublicContinuationRejectsScopeChangesBeforeAnotherReadAsync(string queryType, string change)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexEntry[] entries = Enumerable.Range(1, 3).Select(i => IndexEntry($"party-{i}") with { DisplayName = $"Ada {i}" }).ToArray();
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel { Entries = entries.ToDictionary(e => e.Id) }, "etag"));
        PartySdkQueryService service = CreateService(store, new LocalFuzzyPartySearchProvider());
        PartySdkQueryHandler handler = queryType == "PartyIndex" ? new PartyIndexQueryHandler(service) : new PartySearchQueryHandler(service);
        byte[] payload = System.Text.Encoding.UTF8.GetBytes(queryType == "PartyIndex"
            ? "{\"Type\":\"Person\",\"Active\":true,\"CreatedAfter\":\"2026-01-01T00:00:00Z\"}"
            : "{\"Query\":\"Ada\",\"Type\":\"Person\",\"Active\":true}");
        QueryEnvelope envelope = CreateIndexEnvelope(queryType) with { Payload = payload, Paging = new QueryPagingOptions(PageSize: 2) };
        QueryResult first = await handler.ExecuteAsync(envelope, TestContext.Current.CancellationToken);
        first.Success.ShouldBeTrue();
        string cursor = first.Metadata!.Paging!.NextCursor.ShouldNotBeNull();
        QueryEnvelope continuation = envelope with { Paging = new QueryPagingOptions(PageSize: change == "size" ? 3 : 2, Cursor: cursor) };
        string changedPayload = System.Text.Encoding.UTF8.GetString(payload);
        continuation = change switch
        {
            "tenant" => continuation with { TenantId = "tenant-b" },
            "user" => continuation with { UserId = "other-user" },
            "type" => continuation with { Payload = System.Text.Encoding.UTF8.GetBytes(changedPayload.Replace("Person", "Organization", StringComparison.Ordinal)) },
            "active" => continuation with { Payload = System.Text.Encoding.UTF8.GetBytes(changedPayload.Replace("true", "false", StringComparison.Ordinal)) },
            "date" => continuation with { Payload = System.Text.Encoding.UTF8.GetBytes(changedPayload.Replace("2026-01-01", "2026-02-01", StringComparison.Ordinal)) },
            "query" => continuation with { Payload = System.Text.Encoding.UTF8.GetBytes(changedPayload.Replace("Ada", "Grace", StringComparison.Ordinal)) },
            _ => continuation,
        };
        store.ClearReceivedCalls();
        QueryResult rejected = await handler.ExecuteAsync(continuation, TestContext.Current.CancellationToken);
        rejected.Success.ShouldBeFalse();
        rejected.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidCursor);
        rejected.PayloadBytes.ShouldBeNull();
        await store.DidNotReceiveWithAnyArgs().GetAsync<PartyIndexSdkReadModel>(default!, default!, default);
    }

    [Fact]
    public async Task SearchProviderNeverReceivesErasedEntriesAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexEntry[] entries = [IndexEntry("visible"), IndexEntry("erased") with { IsErased = true }];
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel { Entries = entries.ToDictionary(e => e.Id) }, "etag"));
        IPartySearchProvider provider = Substitute.For<IPartySearchProvider>();
        string[]? receivedIds = null;
        provider.Search(Arg.Do<IEnumerable<PartyIndexEntry>>(items => receivedIds = items.Select(e => e.Id).ToArray()),
            Arg.Any<string>(), Arg.Any<PartyType?>(), Arg.Any<bool?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<PartySearchResult> { Items = [], Page = 1, PageSize = 20, TotalCount = 0, TotalPages = 1 });
        QueryResult result = await new PartySearchQueryHandler(CreateService(store, provider)).ExecuteAsync(CreateIndexEnvelope("PartySearch"), TestContext.Current.CancellationToken);
        result.Success.ShouldBeTrue();
        receivedIds.ShouldBe(["visible"]);
    }

    [Theory]
    [InlineData("Query", "text", false)]
    [InlineData("Query", "text", true)]
    [InlineData("Mode", "text", false)]
    [InlineData("Mode", "text", true)]
    [InlineData("CaseId", "text", false)]
    [InlineData("CaseId", "text", true)]
    [InlineData("Type", "text", false)]
    [InlineData("Type", "text", true)]
    [InlineData("Query", "whitespace", false)]
    [InlineData("Query", "whitespace", true)]
    [InlineData("Mode", "whitespace", false)]
    [InlineData("Mode", "whitespace", true)]
    [InlineData("Query", "supplementary", false)]
    [InlineData("Query", "supplementary", true)]
    public async Task SearchHandler_OversizedRawStringsRejectBeforeCursorStoreOrSearchAsync(
        string field, string content, bool envelopePaging)
    {
        string value = content switch
        {
            "whitespace" => new string(' ', 513),
            "supplementary" => string.Concat(Enumerable.Repeat("\U0001F600", 256)) + "x",
            _ => new string('x', 513),
        };
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IPartySearchProvider provider = Substitute.For<IPartySearchProvider>();
        IQueryCursorCodec codec = Substitute.For<IQueryCursorCodec>();
        var handler = new PartySearchQueryHandler(CreateService(store, provider, cursorCodec: codec));
        Dictionary<string, object?> payload = new(StringComparer.Ordinal)
        {
            ["qUeRy"] = "Ada",
            ["page"] = 1,
            ["pageSize"] = 20,
        };
        payload[string.Equals(field, "Query", StringComparison.Ordinal) ? "qUeRy" : field] = value;

        QueryResult result = await handler.ExecuteAsync(
            CreateIndexEnvelope("PartySearch") with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(payload),
                Paging = envelopePaging ? new QueryPagingOptions(PageSize: 20, Cursor: "must-not-decode") : null,
            },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        result.PayloadBytes.ShouldBeNull();
        result.Metadata.ShouldBeNull();
        result.ProjectionType.ShouldBeNull();
        store.ReceivedCalls().ShouldBeEmpty();
        provider.ReceivedCalls().ShouldBeEmpty();
        codec.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ascii", false)]
    [InlineData("ascii", true)]
    [InlineData("supplementary", false)]
    [InlineData("supplementary", true)]
    public async Task SearchHandler_MaximumQueryRetainsMatchingAndContinuationAsync(string content, bool envelopePaging)
    {
        string query = content == "ascii" ? new string('a', 512) : string.Concat(Enumerable.Repeat("\U0001F600", 256));
        query.Length.ShouldBe(512);
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexEntry[] entries = [IndexEntry("party-a") with { DisplayName = query }, IndexEntry("party-b") with { DisplayName = query }];
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel { Entries = entries.ToDictionary(e => e.Id) }, "etag"));
        var handler = new PartySearchQueryHandler(CreateService(store, new LocalFuzzyPartySearchProvider()));
        QueryEnvelope envelope = CreateIndexEnvelope("PartySearch") with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { Query = query, Type = "Person", Active = true, Page = 1, PageSize = 1 }),
            Paging = envelopePaging ? new QueryPagingOptions(PageSize: 1) : null,
        };

        QueryResult first = await handler.ExecuteAsync(envelope, TestContext.Current.CancellationToken);

        first.Success.ShouldBeTrue();
        if (envelopePaging)
        {
            first.Metadata!.Paging!.NextCursor!.Length.ShouldBeLessThanOrEqualTo(QueryPolicyLimits.MaxCursorLength);
        }

        PagedResult<PartySearchResult> firstPage = first.GetPayload().Deserialize<PagedResult<PartySearchResult>>(PartiesJsonOptions.Default)!;
        firstPage.TotalCount.ShouldBe(2);
        firstPage.Items.ShouldHaveSingleItem().Party.Id.ShouldBe("party-a");
        firstPage.Items[0].Matches.ShouldHaveSingleItem().MatchType.ShouldBe("exact");
        QueryEnvelope continuation = envelopePaging
            ? envelope with { Paging = new QueryPagingOptions(PageSize: 1, Cursor: first.Metadata!.Paging!.NextCursor.ShouldNotBeNull()) }
            : envelope with { Payload = JsonSerializer.SerializeToUtf8Bytes(new { Query = query, Type = "Person", Active = true, Page = 2, PageSize = 1 }) };

        QueryResult second = await handler.ExecuteAsync(continuation, TestContext.Current.CancellationToken);

        second.Success.ShouldBeTrue();
        PagedResult<PartySearchResult> secondPage = second.GetPayload().Deserialize<PagedResult<PartySearchResult>>(PartiesJsonOptions.Default)!;
        secondPage.Items.ShouldHaveSingleItem().Party.Id.ShouldBe("party-b");
        secondPage.Items[0].Matches.ShouldHaveSingleItem().MatchType.ShouldBe("exact");
        secondPage.Page.ShouldBe(2);
        secondPage.TotalCount.ShouldBe(2);
    }

    [Theory]
    [InlineData("Mode", false)]
    [InlineData("CaseId", false)]
    [InlineData("Mode", true)]
    [InlineData("CaseId", true)]
    public async Task SearchHandler_BoundedUnsupportedFiltersKeepExistingFailureAsync(string field, bool envelopePaging)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartySearchQueryHandler(CreateService(store));
        QueryResult result = await handler.ExecuteAsync(CreateIndexEnvelope("PartySearch") with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["Query"] = "Ada", [field] = new string('x', 512) }),
            Paging = envelopePaging ? new QueryPagingOptions(PageSize: 20) : null,
        }, TestContext.Current.CancellationToken);

        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.UnsupportedQueryType);
        result.PayloadBytes.ShouldBeNull();
        store.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Mode", false)]
    [InlineData("Mode", true)]
    [InlineData("CaseId", false)]
    [InlineData("CaseId", true)]
    public async Task SearchHandler_OversizedQueryPrecedesUnsupportedFiltersAsync(string field, bool envelopePaging)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IPartySearchProvider provider = Substitute.For<IPartySearchProvider>();
        IQueryCursorCodec codec = Substitute.For<IQueryCursorCodec>();
        var handler = new PartySearchQueryHandler(CreateService(store, provider, cursorCodec: codec));

        QueryResult result = await handler.ExecuteAsync(CreateIndexEnvelope("PartySearch") with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string>
            {
                ["Query"] = new string('x', 513),
                [field] = "unsupported",
            }),
            Paging = envelopePaging ? new QueryPagingOptions(PageSize: 20, Cursor: "must-not-decode") : null,
        }, TestContext.Current.CancellationToken);

        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        result.PayloadBytes.ShouldBeNull();
        result.Metadata.ShouldBeNull();
        store.ReceivedCalls().ShouldBeEmpty();
        provider.ReceivedCalls().ShouldBeEmpty();
        codec.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("omitted", false)]
    [InlineData("omitted", true)]
    [InlineData("null", false)]
    [InlineData("null", true)]
    [InlineData("empty", false)]
    [InlineData("empty", true)]
    [InlineData("whitespace", false)]
    [InlineData("whitespace", true)]
    public async Task SearchHandler_BoundedEmptyInputsKeepEmptyResultsAsync(string content, bool envelopePaging)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
            {
                Entries = new Dictionary<string, PartyIndexEntry> { ["party-a"] = IndexEntry("party-a") },
            }, "etag"));
        Dictionary<string, string?> payload = new(StringComparer.Ordinal);
        if (content != "omitted")
        {
            payload["Query"] = content switch { "null" => null, "empty" => string.Empty, _ => new string(' ', 512) };
            payload["Mode"] = payload["Query"];
        }

        var handler = new PartySearchQueryHandler(CreateService(store, new LocalFuzzyPartySearchProvider()));
        QueryResult result = await handler.ExecuteAsync(CreateIndexEnvelope("PartySearch") with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(payload),
            Paging = envelopePaging ? new QueryPagingOptions(PageSize: 20) : null,
        }, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        PagedResult<PartySearchResult> page = result.GetPayload().Deserialize<PagedResult<PartySearchResult>>(PartiesJsonOptions.Default)!;
        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(0);
    }

    /// <summary>
    /// Gets invalid page sizes combined with cursor, negative offset and conflicting offset inputs.
    /// </summary>
    public static TheoryData<string, int, string?, int?> InvalidPageCases
    {
        get
        {
            var cases = new TheoryData<string, int, string?, int?>();
            string[] queryTypes = [PartyIndexQueryTypes.PartyIndexQueryType, PartyIndexQueryTypes.PartySearchQueryType];
            int[] invalidPageSizes = [int.MinValue, -1, 0, 101, 200, int.MaxValue];
            foreach (string queryType in queryTypes)
            {
                foreach (int pageSize in invalidPageSizes)
                {
                    cases.Add(queryType, pageSize, null, null);
                    cases.Add(queryType, pageSize, "protected.cursor.payload", null);
                    cases.Add(queryType, pageSize, null, -1);
                    cases.Add(queryType, pageSize, "protected.cursor.payload", 0);
                }
            }

            return cases;
        }
    }

    /// <summary>
    /// Verifies invalid page sizes precede cursor and offset failures without dependency access.
    /// </summary>
    /// <param name="queryType">The list or search query discriminator.</param>
    /// <param name="pageSize">The invalid envelope page size.</param>
    /// <param name="cursor">The optional protected cursor.</param>
    /// <param name="offset">The optional invalid or conflicting offset.</param>
    /// <returns>A task that represents the verification.</returns>
    [Theory]
    [MemberData(nameof(InvalidPageCases))]
    public async Task Paging_InvalidEnvelopePageSize_FailsBeforeCodecStoreAndSearchAsync(string queryType, int pageSize, string? cursor, int? offset)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IQueryCursorCodec codec = Substitute.For<IQueryCursorCodec>();
        IPartySearchProvider search = Substitute.For<IPartySearchProvider>();
        PartySdkQueryService service = CreateService(store, searchProvider: search, cursorCodec: codec);
        QueryEnvelope envelope = CreatePagingEnvelope(queryType, new QueryPagingOptions(PageSize: pageSize, Cursor: cursor, Offset: offset));

        QueryResult result = await ExecutePagingHandlerAsync(service, envelope);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidPage);
        result.PayloadBytes.ShouldBeNull();
        store.ReceivedCalls().ShouldBeEmpty();
        codec.ReceivedCalls().ShouldBeEmpty();
        search.ReceivedCalls().ShouldBeEmpty();
        JsonSerializer.Serialize(result).ShouldNotContain("protected.cursor.payload");
    }

    /// <summary>
    /// Verifies existing cursor and offset errors retain the cursor category.
    /// </summary>
    /// <param name="queryType">The list or search query discriminator.</param>
    /// <returns>A task that represents the verification.</returns>
    [Theory]
    [InlineData(PartyIndexQueryTypes.PartyIndexQueryType)]
    [InlineData(PartyIndexQueryTypes.PartySearchQueryType)]
    public async Task Paging_CursorAndOffsetErrors_PreserveCursorCategoryAsync(string queryType)
    {
        foreach ((string? position, string? cursor, int? offset, bool decode) in new (string?, string?, int?, bool)[]
        {
            (null, "protected.cursor.payload", null, false),
            ("not-a-number", "protected.cursor.payload", null, true),
            ("-1", "protected.cursor.payload", null, true),
            ("2147483648", "protected.cursor.payload", null, true),
            ("1", "protected.cursor.payload", null, true),
            (null, null, -1, true),
            (null, null, 1, true),
            (null, "protected.cursor.payload", 0, true),
        })
        {
            IReadModelStore store = Substitute.For<IReadModelStore>();
            IPartySearchProvider search = Substitute.For<IPartySearchProvider>();
            IQueryCursorCodec codec = Substitute.For<IQueryCursorCodec>();
            codec.TryDecode(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<string?>(), out Arg.Any<string?>())
                .Returns(call =>
                {
                    call[3] = position;
                    call[4] = decode ? null : "malformed";
                    return decode;
                });
            QueryEnvelope envelope = CreatePagingEnvelope(queryType, new QueryPagingOptions(PageSize: 2, Cursor: cursor, Offset: offset));

            QueryResult result = await ExecutePagingHandlerAsync(CreateService(store, searchProvider: search, cursorCodec: codec), envelope);

            result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidCursor);
            store.ReceivedCalls().ShouldBeEmpty();
            search.ReceivedCalls().ShouldBeEmpty();
            JsonSerializer.Serialize(result).ShouldNotContain("protected.cursor.payload");
        }
    }

    /// <summary>
    /// Verifies strict envelope parsing and unsupported search mode precede paging checks.
    /// </summary>
    /// <param name="queryType">The list or search query discriminator.</param>
    /// <returns>A task that represents the verification.</returns>
    [Theory]
    [InlineData(PartyIndexQueryTypes.PartyIndexQueryType)]
    [InlineData(PartyIndexQueryTypes.PartySearchQueryType)]
    public async Task Paging_EnvelopeAndSearchModePrecedence_RemainsUnchangedAsync(string queryType)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IQueryCursorCodec codec = Substitute.For<IQueryCursorCodec>();
        IPartySearchProvider search = Substitute.For<IPartySearchProvider>();
        PartySdkQueryService service = CreateService(store, searchProvider: search, cursorCodec: codec);
        QueryEnvelope envelope = CreatePagingEnvelope(queryType, new QueryPagingOptions(PageSize: 0));
        foreach (QueryEnvelope invalid in new[]
        {
            envelope with
            {
                TenantId = "tenant:escape",
            },
            envelope with
            {
                Payload = "{\"unknown\":true}"u8.ToArray(),
            },
            envelope with
            {
                Payload = "{"u8.ToArray(),
            },
        })
        {
            (await ExecutePagingHandlerAsync(service, invalid)).ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        }

        if (queryType == PartyIndexQueryTypes.PartySearchQueryType)
        {
            QueryResult unsupported = await ExecutePagingHandlerAsync(service, envelope with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    query = "Ada",
                    mode = "unsupported",
                }),
            });
            unsupported.ErrorMessage.ShouldBe(QueryAdapterFailureReason.UnsupportedQueryType);
        }

        store.ReceivedCalls().ShouldBeEmpty();
        codec.ReceivedCalls().ShouldBeEmpty();
        search.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies accepted paging returns independently expected contents, counts and continuation metadata.
    /// </summary>
    /// <param name="queryType">The list or search query discriminator.</param>
    /// <returns>A task that represents the verification.</returns>
    [Theory]
    [InlineData(PartyIndexQueryTypes.PartyIndexQueryType)]
    [InlineData(PartyIndexQueryTypes.PartySearchQueryType)]
    public async Task Paging_AcceptedBoundariesDefaultsAndLegacyClamping_PreserveMetadataAsync(string queryType)
    {
        foreach ((QueryPagingOptions? paging, int payloadSize, int expectedSize, int expectedOffset, int expectedPage, int expectedTotalPages, string[] expectedIds, bool? expectedHasMore) in new (QueryPagingOptions?, int, int, int, int, int, string[], bool?)[]
        {
            (new QueryPagingOptions(PageSize: 1), 20, 1, 0, 1, 4, ["party-1"], true),
            (new QueryPagingOptions(PageSize: 100), 20, 100, 0, 1, 1, ["party-1", "party-2", "party-3", "party-4"], false),
            (new QueryPagingOptions(), 20, 20, 0, 1, 1, ["party-1", "party-2", "party-3", "party-4"], false),
            (new QueryPagingOptions(PageSize: 2, Offset: 2), 100, 2, 2, 2, 2, ["party-3", "party-4"], false),
            (null, 101, 100, 0, 1, 1, ["party-1", "party-2", "party-3", "party-4"], null),
            (null, int.MaxValue, 100, 0, 1, 1, ["party-1", "party-2", "party-3", "party-4"], null),
        })
        {
            IReadModelStore store = Substitute.For<IReadModelStore>();
            store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
                .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
                {
                    Entries = Enumerable.Range(1, 4).Select(index => IndexEntry($"party-{index}")).ToDictionary(entry => entry.Id, StringComparer.Ordinal),
                    ProjectedAt = s_now,
                }, "index-etag"));
            PartySdkQueryService service = CreateService(store, searchProvider: new BasicPartySearchProvider());
            QueryEnvelope envelope = CreatePagingEnvelope(queryType, paging, payloadSize);

            QueryResult result = await ExecutePagingHandlerAsync(service, envelope);

            result.Success.ShouldBeTrue();
            JsonElement payload = result.GetPayload();
            payload.GetProperty("pageSize").GetInt32().ShouldBe(expectedSize);
            payload.GetProperty("page").GetInt32().ShouldBe(expectedPage);
            payload.GetProperty("totalCount").GetInt32().ShouldBe(4);
            payload.GetProperty("totalPages").GetInt32().ShouldBe(expectedTotalPages);
            payload.GetProperty("items").GetArrayLength().ShouldBe(expectedIds.Length);
            PagingIds(result, queryType).ShouldBe(expectedIds);
            if (paging is null)
            {
                result.Metadata.ShouldNotBeNull().Paging.ShouldBeNull();
            }
            else
            {
                QueryPagingMetadata metadata = result.Metadata.ShouldNotBeNull().Paging.ShouldNotBeNull();
                metadata.PageSize.ShouldBe(expectedSize);
                metadata.Offset.ShouldBe(expectedOffset);
                metadata.TotalCount.ShouldBe(4);
                metadata.HasMore.ShouldBe(expectedHasMore);
                if (expectedHasMore == true)
                {
                    metadata.NextCursor.ShouldNotBeNullOrWhiteSpace();
                }
                else
                {
                    metadata.NextCursor.ShouldBeNull();
                }
            }
        }
    }

    /// <summary>
    /// Verifies both continuation pages match independently expected contents and metadata.
    /// </summary>
    /// <param name="queryType">The list or search query discriminator.</param>
    /// <returns>A task that represents the verification.</returns>
    [Theory]
    [InlineData(PartyIndexQueryTypes.PartyIndexQueryType)]
    [InlineData(PartyIndexQueryTypes.PartySearchQueryType)]
    public async Task Paging_ValidContinuation_ReturnsRemainingContentsAsync(string queryType)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(new PartyIndexSdkReadModel
            {
                Entries = Enumerable.Range(1, 4).Select(index => IndexEntry($"party-{index}")).ToDictionary(entry => entry.Id, StringComparer.Ordinal),
                ProjectedAt = s_now,
            }, "etag"));
        PartySdkQueryService service = CreateService(store, searchProvider: new BasicPartySearchProvider());
        QueryEnvelope envelope = CreatePagingEnvelope(queryType, new QueryPagingOptions(PageSize: 2));
        QueryResult first = await ExecutePagingHandlerAsync(service, envelope);
        first.Success.ShouldBeTrue();
        PagingIds(first, queryType).ShouldBe(["party-1", "party-2"]);
        JsonElement firstPayload = first.GetPayload();
        firstPayload.GetProperty("items").GetArrayLength().ShouldBe(2);
        firstPayload.GetProperty("page").GetInt32().ShouldBe(1);
        firstPayload.GetProperty("pageSize").GetInt32().ShouldBe(2);
        firstPayload.GetProperty("totalCount").GetInt32().ShouldBe(4);
        firstPayload.GetProperty("totalPages").GetInt32().ShouldBe(2);
        QueryPagingMetadata firstPaging = first.Metadata.ShouldNotBeNull().Paging.ShouldNotBeNull();
        firstPaging.PageSize.ShouldBe(2);
        firstPaging.Offset.ShouldBe(0);
        firstPaging.TotalCount.ShouldBe(4);
        firstPaging.HasMore.ShouldBe(true);
        string cursor = firstPaging.NextCursor.ShouldNotBeNull();
        cursor.ShouldNotBeNullOrWhiteSpace();

        QueryResult second = await ExecutePagingHandlerAsync(service, envelope with
        {
            Paging = new QueryPagingOptions(PageSize: 2, Cursor: cursor),
        });

        second.Success.ShouldBeTrue();
        PagingIds(second, queryType).ShouldBe(["party-3", "party-4"]);
        JsonElement secondPayload = second.GetPayload();
        secondPayload.GetProperty("items").GetArrayLength().ShouldBe(2);
        secondPayload.GetProperty("page").GetInt32().ShouldBe(2);
        secondPayload.GetProperty("pageSize").GetInt32().ShouldBe(2);
        secondPayload.GetProperty("totalCount").GetInt32().ShouldBe(4);
        secondPayload.GetProperty("totalPages").GetInt32().ShouldBe(2);
        QueryPagingMetadata secondPaging = second.Metadata.ShouldNotBeNull().Paging.ShouldNotBeNull();
        secondPaging.PageSize.ShouldBe(2);
        secondPaging.Offset.ShouldBe(2);
        secondPaging.TotalCount.ShouldBe(4);
        secondPaging.HasMore.ShouldBe(false);
        secondPaging.NextCursor.ShouldBeNull();
    }

    private static string[] PagingIds(QueryResult result, string queryType)
        => result.GetPayload().GetProperty("items").EnumerateArray()
            .Select(item => (queryType == PartyIndexQueryTypes.PartySearchQueryType
                ? item.GetProperty("party") : item).GetProperty("id").GetString()!).ToArray();

    private static QueryEnvelope CreatePagingEnvelope(string queryType, QueryPagingOptions? paging, int payloadPageSize = 20)
        => CreateIndexEnvelope(queryType) with
        {
            Paging = paging,
            Payload = queryType == PartyIndexQueryTypes.PartySearchQueryType
                ? JsonSerializer.SerializeToUtf8Bytes(new
                {
                    query = "Party",
                    page = 1,
                    pageSize = payloadPageSize,
                })
                : JsonSerializer.SerializeToUtf8Bytes(new
                {
                    page = 1,
                    pageSize = payloadPageSize,
                }),
        };

    private static Task<QueryResult> ExecutePagingHandlerAsync(PartySdkQueryService service, QueryEnvelope envelope)
        => envelope.QueryType == PartyIndexQueryTypes.PartySearchQueryType
            ? new PartySearchQueryHandler(service).ExecuteAsync(envelope, TestContext.Current.CancellationToken)
            : new PartyIndexQueryHandler(service).ExecuteAsync(envelope, TestContext.Current.CancellationToken);

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
            cursorCodec ?? CreateCursorCodec(),
            lastKnownCache ?? new PartySdkLastKnownReadModelCache(),
            logger ?? NullLogger<PartySdkQueryService>.Instance);

    private static QueryEnvelope CreateDetailEnvelope(string queryType)
        => new(
            tenantId: "tenant-a",
            domain: PartyDetailQueryTypes.PartyDomain,
            aggregateId: "party-1",
            queryType: queryType,
            payload: JsonSerializer.SerializeToUtf8Bytes(new { }),
            correlationId: "correlation-1",
            userId: "user-1",
            entityId: "party-1");

    private static QueryEnvelope CreateIndexEnvelope(string queryType)
        => new(
            tenantId: "tenant-a",
            domain: PartyDetailQueryTypes.PartyDomain,
            aggregateId: PartyIndexQueryTypes.ListAggregateId,
            queryType: queryType,
            payload: JsonSerializer.SerializeToUtf8Bytes(new { page = 1, pageSize = 20 }),
            correlationId: "correlation-1",
            userId: "user-1",
            entityId: PartyIndexQueryTypes.ListAggregateId);

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

    private static IQueryCursorCodec CreateCursorCodec(IDataProtectionProvider? keys = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<IDataProtectionProvider>(keys ?? new EphemeralDataProtectionProvider());
        services.AddEventStoreQueryCursorCodec("Hexalith.Parties.QueryCursor.v1");
        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IQueryCursorCodec>();
    }
}
