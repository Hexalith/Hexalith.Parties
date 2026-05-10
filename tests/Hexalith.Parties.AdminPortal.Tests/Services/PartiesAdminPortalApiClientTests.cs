using System.Text.Json;

using Hexalith.FrontComposer.Contracts.Communication;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Services;

public sealed class PartiesAdminPortalApiClientTests
{
    [Fact]
    public async Task ListPartiesAsync_WhenPartiesQueryClientIsRegistered_PrefersTypedClientBoundaryAsync()
    {
        var partiesQueryClient = new RecordingPartiesQueryClient();
        partiesQueryClient.ListResult = new PagedResult<PartyIndexEntry>
        {
            Items = [],
            Page = 1,
            PageSize = 100,
            TotalCount = 0,
            TotalPages = 0,
        };
        var queryService = new RecordingQueryService();
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IPartiesQueryClient>(partiesQueryClient)
            .AddSingleton<IQueryService>(queryService)
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalQueryResult<PagedResult<PartyIndexEntry>> result = await client.ListPartiesAsync(
            new AdminPortalListRequest(Page: 1, PageSize: 250, Type: PartyType.Organization, Active: false),
            CancellationToken.None);

        result.Payload.PageSize.ShouldBe(100);
        partiesQueryClient.ListCallCount.ShouldBe(1);
        partiesQueryClient.LastListRequest.ShouldNotBeNull().PageSize.ShouldBe(100);
        partiesQueryClient.LastListRequest.ShouldNotBeNull().Type.ShouldBe(PartyType.Organization);
        partiesQueryClient.LastListRequest.ShouldNotBeNull().Active.ShouldBe(false);
        queryService.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListPartiesAsync_UsesFrontComposerQueryServiceWithPartyDomainAndBoundedPagingAsync()
    {
        var queryService = new RecordingQueryService();
        queryService.Enqueue([
            new PartyIndexEntry
            {
                Id = "p-1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Ada Lovelace",
                CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
                LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
            },
        ], totalCount: 101);
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        AdminPortalQueryResult<PagedResult<PartyIndexEntry>> result = await client.ListPartiesAsync(
            new AdminPortalListRequest(Page: 2, PageSize: 250, Type: PartyType.Person, Active: true),
            CancellationToken.None);

        QueryRequest request = queryService.LastRequest.ShouldNotBeNull();
        request.Domain.ShouldBe("party");
        request.ProjectionType.ShouldBe("PartyIndex");
        request.QueryType.ShouldBe("ListParties");
        request.Skip.ShouldBe(100);
        request.Take.ShouldBe(100);
        request.CacheDiscriminator.ShouldBe("parties-admin-list-v1");
        request.ColumnFilters.ShouldNotBeNull()["type"].ShouldBe("Person");
        request.ColumnFilters.ShouldNotBeNull()["active"].ShouldBe("true");
        result.Payload.Page.ShouldBe(2);
        result.Payload.PageSize.ShouldBe(100);
        result.Payload.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task ListPartiesAsync_PreservesEventStoreNotModifiedSignalInMetadataAsync()
    {
        var queryService = new RecordingQueryService();
        queryService.Enqueue(
            Array.Empty<PartyIndexEntry>(),
            totalCount: 0,
            isNotModified: true);
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        AdminPortalQueryResult<PagedResult<PartyIndexEntry>> result = await client.ListPartiesAsync(
            new AdminPortalListRequest(Page: 1, PageSize: 20, Type: null, Active: null),
            CancellationToken.None);

        result.Metadata.StaleDataAge.ShouldBe("not-modified");
        result.Payload.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchPartiesAsync_UsesSearchQueryWithoutForwardingUnsupportedFiltersAsync()
    {
        var queryService = new RecordingQueryService();
        queryService.Enqueue([
            new PartySearchResult
            {
                Party = new PartyIndexEntry
                {
                    Id = "p-1",
                    Type = PartyType.Person,
                    IsActive = true,
                    DisplayName = "Ada Lovelace",
                    CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
                    LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
                },
                Matches = [],
                RelevanceScore = 0.9,
            },
        ], totalCount: 1);
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        AdminPortalQueryResult<PagedResult<PartySearchResult>> result = await client.SearchPartiesAsync(
            new AdminPortalSearchRequest("ada@example.test", Page: 1, PageSize: 20, Type: PartyType.Person, Active: true),
            CancellationToken.None);

        QueryRequest request = queryService.LastRequest.ShouldNotBeNull();
        request.Domain.ShouldBe("party");
        request.ProjectionType.ShouldBe("PartySearch");
        request.QueryType.ShouldBe("SearchParties");
        request.SearchQuery.ShouldBe("ada@example.test");
        request.ColumnFilters.ShouldBeNull();
        request.CacheDiscriminator.ShouldBe("parties-admin-search-v1");
        result.Payload.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetPartyAsync_UsesDetailQueryAndReturnsFirstProjectionItemAsync()
    {
        var queryService = new RecordingQueryService();
        queryService.Enqueue([
            new PartyDetail
            {
                Id = "p-1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Ada Lovelace",
                SortName = "Lovelace, Ada",
                CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
                LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
            },
        ], totalCount: 1);
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        AdminPortalQueryResult<PartyDetail> result = await client.GetPartyAsync("p-1", CancellationToken.None);

        QueryRequest request = queryService.LastRequest.ShouldNotBeNull();
        request.Domain.ShouldBe("party");
        request.AggregateId.ShouldBe("p-1");
        request.EntityId.ShouldBe("p-1");
        request.ProjectionType.ShouldBe("PartyDetail");
        request.QueryType.ShouldBe("GetParty");
        request.ProjectionActorType.ShouldBe("PartyDetailProjectionActor");
        result.Payload.DisplayName.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task MissingEventStoreQueryContract_FailsClosedWithoutCallingQueryServiceAsync()
    {
        var queryService = new RecordingQueryService();
        PartiesAdminPortalApiClient client = new(
            queryService,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.ListPartiesAsync(new AdminPortalListRequest(1, 20, null, null), CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.ContractUnavailable);
        ex.Message.ShouldNotContain("api/v1/parties");
        queryService.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task QueryFailures_SurfaceTypedBoundedOutcomesWithoutProblemDetailsLeakAsync()
    {
        var queryService = new RecordingQueryService
        {
            ExceptionToThrow = new QueryFailureException(
                QueryFailureKind.Forbidden,
                new ProblemDetailsPayload(
                    "Forbidden",
                    "tenant=other party=p-99",
                    403,
                    null,
                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                    [])),
        };
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.GetPartyAsync("tenant:other:p-99", CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.TenantRequired);
        ex.Message.ShouldNotContain("tenant=other");
        ex.Message.ShouldNotContain("p-99");
    }

    [Fact]
    public async Task GdprMalformedQueryResponse_MapsToTransientFailureWithoutParserDetailAsync()
    {
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IAdminPortalGdprClient>(new MalformedGdprClient())
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.GetProcessingRecordsAsync("party-1", CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.TransientFailure);
        ex.Message.ShouldNotContain("<html>");
        ex.Message.ShouldNotContain("unexpected-token");
    }

    [Fact]
    public async Task GetRichSearchCapabilityAsync_RecordsBlockedContractAsLocalOnlyAsync()
    {
        var queryService = new RecordingQueryService();
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        AdminPortalRichSearchCapability capability = await client
            .GetRichSearchCapabilityAsync(CancellationToken.None)
            .ConfigureAwait(true);

        capability.IsAvailable.ShouldBeFalse();
        capability.IsDegraded.ShouldBeFalse();
        capability.Reason.ShouldNotBeNull().ShouldContain("Story 12.4/12.5");
        queryService.CallCount.ShouldBe(0);
    }

    private static PartiesAdminPortalApiClient CreateClient(RecordingQueryService queryService)
        => new(
            queryService,
            Options.Create(new PartiesAdminPortalOptions
            {
                ListProjectionType = "PartyIndex",
                ListQueryType = "ListParties",
                SearchProjectionType = "PartySearch",
                SearchQueryType = "SearchParties",
                DetailProjectionType = "PartyDetail",
                DetailQueryType = "GetParty",
                DetailProjectionActorType = "PartyDetailProjectionActor",
            }));

    private sealed class RecordingQueryService : IQueryService
    {
        private object? _items;
        private int _totalCount;

        public QueryRequest? LastRequest { get; private set; }

        public int CallCount { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        private bool _isNotModified;

        public void Enqueue<T>(IReadOnlyList<T> items, int totalCount, bool isNotModified = false)
        {
            _items = items;
            _totalCount = totalCount;
            _isNotModified = isNotModified;
        }

        public Task<QueryResult<T>> QueryAsync<T>(QueryRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            IReadOnlyList<T> items = _items as IReadOnlyList<T> ?? [];
            return Task.FromResult(new QueryResult<T>(items, _totalCount, ETag: null, _isNotModified));
        }
    }

    private sealed class RecordingPartiesQueryClient : IPartiesQueryClient
    {
        public PagedResult<PartyIndexEntry>? ListResult { get; set; }

        public ListRequestSnapshot? LastListRequest { get; private set; }

        public int ListCallCount { get; private set; }

        public Task<PartyDetail> GetPartyAsync(string partyId, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<PagedResult<PartyIndexEntry>> ListPartiesAsync(
            int page,
            int pageSize,
            PartyType? type,
            bool? active,
            DateTimeOffset? createdAfter,
            DateTimeOffset? createdBefore,
            DateTimeOffset? modifiedAfter,
            DateTimeOffset? modifiedBefore,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ListCallCount++;
            LastListRequest = new(page, pageSize, type, active, createdAfter, createdBefore, modifiedAfter, modifiedBefore);
            return Task.FromResult(ListResult ?? new PagedResult<PartyIndexEntry> { Items = [] });
        }

        public Task<PagedResult<PartySearchResult>> SearchPartiesAsync(string query, int page, int pageSize, CancellationToken ct)
            => throw new NotImplementedException();
    }

    private sealed record ListRequestSnapshot(
        int Page,
        int PageSize,
        PartyType? Type,
        bool? Active,
        DateTimeOffset? CreatedAfter,
        DateTimeOffset? CreatedBefore,
        DateTimeOffset? ModifiedAfter,
        DateTimeOffset? ModifiedBefore);

    private sealed class MalformedGdprClient : IAdminPortalGdprClient
    {
        public Task<AdminPortalGdprCommandResult> RequestErasureAsync(string partyId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<PartyErasureStatusRecord?> GetErasureStatusAsync(string partyId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<ErasureCertificate?> GetErasureCertificateAsync(string partyId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<AdminPortalGdprCommandResult> RetryErasureVerificationAsync(string partyId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<AdminPortalGdprCommandResult> RestrictProcessingAsync(string partyId, string? reason, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<AdminPortalGdprCommandResult> LiftRestrictionAsync(string partyId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<AdminPortalGdprCommandResult> AddConsentAsync(
            string partyId,
            string channelId,
            string purpose,
            LawfulBasis lawfulBasis,
            CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<AdminPortalGdprCommandResult> RevokeConsentAsync(string partyId, string consentId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ConsentRecord>> GetConsentAsync(string partyId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<AdminPortalExportDownload> ExportPartyDataAsync(string partyId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(string partyId, CancellationToken cancellationToken)
            => throw new JsonException("unexpected-token <html>");
    }
}
