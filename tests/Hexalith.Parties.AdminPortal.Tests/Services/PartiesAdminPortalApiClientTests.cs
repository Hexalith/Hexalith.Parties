using System.Text.Json;

using Hexalith.FrontComposer.Contracts.Communication;
using Hexalith.Parties.Client;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Contracts.Commands;
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
    public async Task SearchPartiesAsync_WhenPartiesQueryClientIsRegistered_PrefersTypedClientBoundaryAsync()
    {
        var partiesQueryClient = new RecordingPartiesQueryClient();
        partiesQueryClient.SearchResult = new PagedResult<PartySearchResult>
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

        await client.SearchPartiesAsync(
            new AdminPortalSearchRequest("ada@example.com", Page: 2, PageSize: 250, Type: PartyType.Person, Active: true),
            CancellationToken.None);

        partiesQueryClient.SearchCallCount.ShouldBe(1);
        partiesQueryClient.LastSearchRequest.ShouldNotBeNull().Query.ShouldBe("ada@example.com");
        partiesQueryClient.LastSearchRequest.ShouldNotBeNull().Page.ShouldBe(2);
        partiesQueryClient.LastSearchRequest.ShouldNotBeNull().PageSize.ShouldBe(100);
        partiesQueryClient.LastSearchRequest.ShouldNotBeNull().Mode.ShouldBe("Lexical");
        partiesQueryClient.LastSearchRequest.ShouldNotBeNull().Type.ShouldBe(PartyType.Person);
        partiesQueryClient.LastSearchRequest.ShouldNotBeNull().Active.ShouldBe(true);
        queryService.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetPartyAsync_WhenPartiesQueryClientIsRegistered_PrefersTypedClientBoundaryAsync()
    {
        var partiesQueryClient = new RecordingPartiesQueryClient();
        partiesQueryClient.DetailResult = new PartyDetail
        {
            Id = "p-1",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        };
        var queryService = new RecordingQueryService();
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IPartiesQueryClient>(partiesQueryClient)
            .AddSingleton<IQueryService>(queryService)
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalQueryResult<PartyDetail> result = await client.GetPartyAsync("p-1", CancellationToken.None);

        result.Payload.DisplayName.ShouldBe("Ada Lovelace");
        partiesQueryClient.DetailCallCount.ShouldBe(1);
        partiesQueryClient.LastDetailPartyId.ShouldBe("p-1");
        queryService.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetPartyAsync_TypedClient_NormalizesNullCollectionsAsync()
    {
        var partiesQueryClient = new RecordingPartiesQueryClient();
        partiesQueryClient.DetailResult = new PartyDetail
        {
            Id = "p-1",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
            ContactChannels = null!,
            Identifiers = null!,
            ConsentRecords = null!,
            NameHistory = null!,
        };
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IPartiesQueryClient>(partiesQueryClient)
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalQueryResult<PartyDetail> result = await client.GetPartyAsync("p-1", CancellationToken.None);

        result.Payload.ContactChannels.ShouldNotBeNull().ShouldBeEmpty();
        result.Payload.Identifiers.ShouldNotBeNull().ShouldBeEmpty();
        result.Payload.ConsentRecords.ShouldNotBeNull().ShouldBeEmpty();
        result.Payload.NameHistory.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task CreatePartyCompositeAsync_UsesTypedCommandClientAndNormalizesPayloadAsync()
    {
        var commandClient = new RecordingPartiesCommandClient
        {
            Result = new PartiesCommandResult<PartyDetail>("corr-create", new PartyDetail
            {
                Id = "p-1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Ada Lovelace",
                SortName = "Lovelace, Ada",
                ContactChannels = null!,
                Identifiers = null!,
                ConsentRecords = null!,
                NameHistory = null!,
            }),
        };
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IPartiesCommandClient>(commandClient)
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));
        var command = new CreatePartyComposite
        {
            PartyId = Guid.NewGuid().ToString("D"),
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        };

        AdminPortalCommandResult result = await client.CreatePartyCompositeAsync(command, CancellationToken.None);

        commandClient.CreateComposite.ShouldBe(command);
        result.Outcome.ShouldBe(AdminPortalCommandOutcome.Accepted);
        result.CorrelationId.ShouldBe("corr-create");
        result.Detail.ShouldNotBeNull().ContactChannels.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdatePartyCompositeAsync_UsesRoutePartyIdAsTypedClientArgumentAsync()
    {
        var commandClient = new RecordingPartiesCommandClient
        {
            Result = new PartiesCommandResult<PartyDetail>("corr-update", null),
        };
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IPartiesCommandClient>(commandClient)
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));
        var command = new UpdatePartyComposite
        {
            PartyId = "payload-id",
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Byron" },
        };

        AdminPortalCommandResult result = await client.UpdatePartyCompositeAsync("route-id", command, CancellationToken.None);

        commandClient.UpdatePartyId.ShouldBe("route-id");
        commandClient.UpdateComposite.ShouldBe(command);
        result.Outcome.ShouldBe(AdminPortalCommandOutcome.Accepted);
    }

    [Fact]
    public async Task CreatePartyCompositeAsync_WhenTypedCommandClientIsMissing_ReturnsContractUnavailableOutcomeAsync()
    {
        using ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));
        var command = new CreatePartyComposite
        {
            PartyId = Guid.NewGuid().ToString("D"),
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        };

        AdminPortalCommandResult result = await client.CreatePartyCompositeAsync(command, CancellationToken.None);

        result.Outcome.ShouldBe(AdminPortalCommandOutcome.ContractUnavailable);
        result.CorrelationId.ShouldBeNull();
        result.ValidationFailures.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task CommandValidationFailure_MapsToSafeValidationOutcomeAsync()
    {
        var commandClient = new RecordingPartiesCommandClient
        {
            ExceptionToThrow = new PartiesClientException(422, "Validation failed", null, "raw backend detail", "corr-validation"),
        };
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IPartiesCommandClient>(commandClient)
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));
        var command = new CreatePartyComposite
        {
            PartyId = Guid.NewGuid().ToString("D"),
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails { LegalName = "Acme" },
        };

        AdminPortalCommandResult result = await client.CreatePartyCompositeAsync(command, CancellationToken.None);

        result.Outcome.ShouldBe(AdminPortalCommandOutcome.ValidationRejected);
        result.CorrelationId.ShouldBe("corr-validation");
        result.Detail.ShouldBeNull();
    }

    [Fact]
    public async Task TypedClient_PartiesClientException_MapsByStatusCodeAsync()
    {
        foreach ((int status, AdminPortalQueryFailureKind expected) in new[]
        {
            (401, AdminPortalQueryFailureKind.AuthenticationRequired),
            (404, AdminPortalQueryFailureKind.NotFound),
            (410, AdminPortalQueryFailureKind.Gone),
            (422, AdminPortalQueryFailureKind.Validation),
            (429, AdminPortalQueryFailureKind.TransientFailure),
            (503, AdminPortalQueryFailureKind.TransientFailure),
        })
        {
            var partiesQueryClient = new RecordingPartiesQueryClient
            {
                ExceptionToThrow = new PartiesClientException(status, "title", null, "detail", correlationId: null),
            };
            using ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton<IPartiesQueryClient>(partiesQueryClient)
                .BuildServiceProvider();
            PartiesAdminPortalApiClient client = new(
                serviceProvider,
                Options.Create(new PartiesAdminPortalOptions()));

            AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
                () => client.GetPartyAsync("p-1", CancellationToken.None));

            ex.Kind.ShouldBe(expected, $"status {status} should map to {expected}");
            ex.StatusCode.ShouldBe(status);
        }
    }

    [Fact]
    public async Task TypedClient_HttpRequestException_MapsToTransientFailureAsync()
    {
        var partiesQueryClient = new RecordingPartiesQueryClient
        {
            ExceptionToThrow = new HttpRequestException("transport failure"),
        };
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IPartiesQueryClient>(partiesQueryClient)
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.GetPartyAsync("p-1", CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.TransientFailure);
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
    public async Task ListPartiesAsync_RejectsPageBeyondAddressableRangeAsync()
    {
        var queryService = new RecordingQueryService();
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.ListPartiesAsync(
                new AdminPortalListRequest(Page: int.MaxValue, PageSize: 100, Type: null, Active: null),
                CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.Validation);
        queryService.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SearchPartiesAsync_UsesSearchQueryWithFilteredDisplayNameSearchAsync()
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
        request.ColumnFilters.ShouldNotBeNull()["type"].ShouldBe("Person");
        request.ColumnFilters.ShouldNotBeNull()["active"].ShouldBe("true");
        request.ColumnFilters.ShouldNotBeNull()["mode"].ShouldBe("Lexical");
        request.CacheDiscriminator.ShouldBe("parties-admin-search-v1");
        result.Payload.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SearchPartiesAsync_AppliesPagingForPageBeyondFirstAsync()
    {
        var queryService = new RecordingQueryService();
        queryService.Enqueue(Array.Empty<PartySearchResult>(), totalCount: 250);
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        await client.SearchPartiesAsync(
            new AdminPortalSearchRequest("ada", Page: 3, PageSize: 25, Type: null, Active: null),
            CancellationToken.None);

        QueryRequest request = queryService.LastRequest.ShouldNotBeNull();
        request.Skip.ShouldBe(50);
        request.Take.ShouldBe(25);
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
    public async Task GetPartyAsync_DetailProjectionReturnsMultipleItems_ThrowsUnknownAsync()
    {
        var queryService = new RecordingQueryService();
        queryService.Enqueue([
            new PartyDetail { Id = "p-1", Type = PartyType.Person, IsActive = true, DisplayName = "Ada", SortName = "Ada", CreatedAt = DateTimeOffset.UtcNow, LastModifiedAt = DateTimeOffset.UtcNow },
            new PartyDetail { Id = "p-1", Type = PartyType.Person, IsActive = true, DisplayName = "Other", SortName = "Other", CreatedAt = DateTimeOffset.UtcNow, LastModifiedAt = DateTimeOffset.UtcNow },
        ], totalCount: 2);
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.GetPartyAsync("p-1", CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.Unknown);
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
        ex.Message.ShouldNotContain("Story 12.4");
        queryService.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task MissingEventStoreQueryContract_FailsClosedForSearchAsync()
    {
        var queryService = new RecordingQueryService();
        PartiesAdminPortalApiClient client = new(
            queryService,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.SearchPartiesAsync(new AdminPortalSearchRequest("ada", 1, 20, null, null), CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.ContractUnavailable);
        ex.Message.ShouldNotContain("Story 12.4");
        queryService.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task MissingEventStoreQueryContract_FailsClosedForDetailAsync()
    {
        var queryService = new RecordingQueryService();
        PartiesAdminPortalApiClient client = new(
            queryService,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.GetPartyAsync("p-1", CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.ContractUnavailable);
        ex.Message.ShouldNotContain("Story 12.4");
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
    public async Task GdprCommand_TransportFailure_MapsToTransientFailureOutcomeAsync()
    {
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IAdminPortalGdprClient>(new TransportFailingGdprClient())
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalGdprCommandResult result = await client.RequestErasureAsync("party-1", CancellationToken.None);

        result.Outcome.ShouldBe(AdminPortalGdprOutcome.TransientFailure);
    }

    [Fact]
    public async Task GdprQuery_TransportFailure_MapsToTransientFailureExceptionAsync()
    {
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IAdminPortalGdprClient>(new TransportFailingGdprClient())
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.GetErasureStatusAsync("party-1", CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.TransientFailure);
    }

    [Fact]
    public async Task GdprQuery_ContractUnavailable_MapsToContractUnavailableExceptionAsync()
    {
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IAdminPortalGdprClient>(new ContractUnavailableGdprClient())
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.ExportPartyDataAsync("party-1", CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.ContractUnavailable);
    }

    [Fact]
    public async Task GdprCommand_ContractUnavailable_MapsToContractUnavailableOutcomeAsync()
    {
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IAdminPortalGdprClient>(new ContractUnavailableGdprClient())
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalGdprCommandResult result = await client.RetryErasureVerificationAsync("party-1", CancellationToken.None);

        result.Outcome.ShouldBe(AdminPortalGdprOutcome.ContractUnavailable);
    }

    [Fact]
    public async Task GdprMethods_RejectNullOrWhitespaceArgumentsAsync()
    {
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IAdminPortalGdprClient>(new MalformedGdprClient())
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        await Should.ThrowAsync<ArgumentException>(
            () => client.RequestErasureAsync(string.Empty, CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(
            () => client.AddConsentAsync("p-1", string.Empty, "purpose", LawfulBasis.Consent, CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(
            () => client.AddConsentAsync("p-1", "channel", "  ", LawfulBasis.Consent, CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(
            () => client.RevokeConsentAsync("p-1", "  ", CancellationToken.None));
    }

    [Fact]
    public async Task GetGdprCapabilityAsync_WithoutGdprClient_FailsClosedToUnavailableAsync()
    {
        var queryService = new RecordingQueryService();
        PartiesAdminPortalApiClient client = new(
            queryService,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalGdprCapability capability = await client.GetGdprCapabilityAsync(CancellationToken.None);

        capability.CanRequestErasure.ShouldBeFalse();
        capability.CanReadErasureStatus.ShouldBeFalse();
        capability.CanReadErasureCertificate.ShouldBeFalse();
        capability.CanRetryVerification.ShouldBeFalse();
        capability.CanRestrictProcessing.ShouldBeFalse();
        capability.CanLiftRestriction.ShouldBeFalse();
        capability.CanManageConsent.ShouldBeFalse();
        capability.CanExportData.ShouldBeFalse();
        capability.CanReadProcessingRecords.ShouldBeFalse();
        capability.HasAnySupport.ShouldBeFalse();
        capability.Reason.ShouldBe(AdminPortalGdprCapability.ContractUnavailableReason);
    }

    [Fact]
    public async Task GetGdprCapabilityAsync_WithRealGdprClient_ReportsCertificateAndRetryAvailableAsync()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost/") };
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IAdminPortalGdprClient>(new HttpAdminPortalGdprClient(httpClient))
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalGdprCapability capability = await client.GetGdprCapabilityAsync(CancellationToken.None);

        capability.CanRequestErasure.ShouldBeTrue();
        capability.CanReadErasureStatus.ShouldBeTrue();
        capability.CanRestrictProcessing.ShouldBeTrue();
        capability.CanLiftRestriction.ShouldBeTrue();
        capability.CanManageConsent.ShouldBeTrue();
        capability.CanExportData.ShouldBeTrue();
        capability.CanReadProcessingRecords.ShouldBeTrue();
        capability.CanReadErasureCertificate.ShouldBeTrue();
        capability.CanRetryVerification.ShouldBeTrue();
        capability.HasAnySupport.ShouldBeTrue();
        capability.Reason.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task GetGdprCapabilityAsync_WithProvisionalGdprClient_KeepsCertificateAndRetryDisabledAsync()
    {
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IAdminPortalGdprClient>(new ContractUnavailableGdprClient())
            .BuildServiceProvider();
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(new PartiesAdminPortalOptions()));

        AdminPortalGdprCapability capability = await client.GetGdprCapabilityAsync(CancellationToken.None);

        capability.CanRequestErasure.ShouldBeTrue();
        capability.CanReadErasureStatus.ShouldBeTrue();
        capability.CanRestrictProcessing.ShouldBeTrue();
        capability.CanLiftRestriction.ShouldBeTrue();
        capability.CanManageConsent.ShouldBeTrue();
        capability.CanExportData.ShouldBeTrue();
        capability.CanReadProcessingRecords.ShouldBeTrue();
        capability.CanReadErasureCertificate.ShouldBeFalse();
        capability.CanRetryVerification.ShouldBeFalse();
        capability.HasAnySupport.ShouldBeTrue();
        capability.Reason.ShouldBe(AdminPortalGdprCapability.ContractUnavailableReason);
    }

    [Fact]
    public void GdprCapability_WithOnlyCertificateSupport_CountsAsSupported()
    {
        AdminPortalGdprCapability capability = AdminPortalGdprCapability.Partial(
            canReadErasureCertificate: true);

        capability.CanReadErasureCertificate.ShouldBeTrue();
        capability.HasAnySupport.ShouldBeTrue();
    }

    [Fact]
    public async Task GetRichSearchCapabilityAsync_WithoutProbeWiring_ReturnsLocalOnlyAsync()
    {
        var queryService = new RecordingQueryService();
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        AdminPortalRichSearchCapability capability = await client
            .GetRichSearchCapabilityAsync(CancellationToken.None)
            .ConfigureAwait(true);

        capability.IsAvailable.ShouldBeFalse();
        capability.IsDegraded.ShouldBeFalse();
        capability.Reason.ShouldNotBeNull().ShouldNotContain("Story 12.4");
        queryService.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetRichSearchCapabilityAsync_HealthyHealthPayload_ReportsAvailableAsync()
    {
        AdminPortalRichSearchCapability capability = await ProbeWithHealthPayloadAsync(
            """
            {
              "results": {
                "memories-search": {
                  "status": "Healthy",
                  "data": { "enabled": true, "searchReachable": true, "degradedReportedByMemories": false }
                }
              }
            }
            """);

        capability.IsAvailable.ShouldBeTrue();
        capability.IsDegraded.ShouldBeFalse();
    }

    [Fact]
    public async Task GetRichSearchCapabilityAsync_MissingSearchReachable_FailsClosedToLocalOnlyAsync()
    {
        AdminPortalRichSearchCapability capability = await ProbeWithHealthPayloadAsync(
            """
            {
              "results": {
                "memories-search": {
                  "status": "Healthy",
                  "data": { "enabled": true }
                }
              }
            }
            """);

        capability.IsAvailable.ShouldBeFalse();
    }

    private static async Task<AdminPortalRichSearchCapability> ProbeWithHealthPayloadAsync(string healthJson)
    {
        var probeHandler = new StaticHttpMessageHandler(healthJson);
        var factory = new SingleClientHttpClientFactory(probeHandler);
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IHttpClientFactory>(factory)
            .BuildServiceProvider();
        var options = new PartiesAdminPortalOptions
        {
            RichSearchProbeBaseAddress = new Uri("https://localhost/"),
        };
        PartiesAdminPortalApiClient client = new(
            serviceProvider,
            Options.Create(options));

        return await client.GetRichSearchCapabilityAsync(CancellationToken.None).ConfigureAwait(true);
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

        public PagedResult<PartySearchResult>? SearchResult { get; set; }

        public PartyDetail? DetailResult { get; set; }

        public Exception? ExceptionToThrow { get; init; }

        public ListRequestSnapshot? LastListRequest { get; private set; }

        public SearchRequestSnapshot? LastSearchRequest { get; private set; }

        public string? LastDetailPartyId { get; private set; }

        public int ListCallCount { get; private set; }

        public int SearchCallCount { get; private set; }

        public int DetailCallCount { get; private set; }

        public Task<PartyDetail> GetPartyAsync(
            string partyId,
            CancellationToken ct,
            Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null)
        {
            ct.ThrowIfCancellationRequested();
            DetailCallCount++;
            LastDetailPartyId = partyId;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(DetailResult ?? throw new InvalidOperationException("DetailResult not set."));
        }

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
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ListResult ?? new PagedResult<PartyIndexEntry> { Items = [] });
        }

        public Task<PagedResult<PartySearchResult>> SearchPartiesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken ct,
            string? mode = null,
            string? caseId = null,
            Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null,
            PartyType? type = null,
            bool? active = null)
        {
            ct.ThrowIfCancellationRequested();
            SearchCallCount++;
            LastSearchRequest = new(query, page, pageSize, mode, type, active);
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(SearchResult ?? new PagedResult<PartySearchResult> { Items = [] });
        }
    }

    private sealed class RecordingPartiesCommandClient : IPartiesCommandClient
    {
        public PartiesCommandResult<PartyDetail>? Result { get; init; }

        public Exception? ExceptionToThrow { get; init; }

        public CreatePartyComposite? CreateComposite { get; private set; }

        public string? UpdatePartyId { get; private set; }

        public UpdatePartyComposite? UpdateComposite { get; private set; }

        public Task<PartiesCommandResult<PartyDetail>> CreatePartyCompositeWithResultAsync(CreatePartyComposite command, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            CreateComposite = command;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result ?? new PartiesCommandResult<PartyDetail>("corr", null));
        }

        public Task<PartiesCommandResult<PartyDetail>> UpdatePartyCompositeWithResultAsync(string partyId, UpdatePartyComposite command, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            UpdatePartyId = partyId;
            UpdateComposite = command;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result ?? new PartiesCommandResult<PartyDetail>("corr", null));
        }

        public Task<string> CreatePartyAsync(CreateParty command, CancellationToken ct) => throw new NotImplementedException();

        public Task<PartiesCommandResult<PartyDetail>> CreatePartyWithResultAsync(CreateParty command, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> UpdatePersonDetailsAsync(string partyId, UpdatePersonDetails command, CancellationToken ct) => throw new NotImplementedException();

        public Task<PartiesCommandResult<PartyDetail>> UpdatePersonDetailsWithResultAsync(string partyId, UpdatePersonDetails command, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> UpdateOrganizationDetailsAsync(string partyId, UpdateOrganizationDetails command, CancellationToken ct) => throw new NotImplementedException();

        public Task<PartiesCommandResult<PartyDetail>> UpdateOrganizationDetailsWithResultAsync(string partyId, UpdateOrganizationDetails command, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> AddContactChannelAsync(string partyId, AddContactChannel command, CancellationToken ct) => throw new NotImplementedException();

        public Task<PartiesCommandResult<PartyDetail>> AddContactChannelWithResultAsync(string partyId, AddContactChannel command, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> UpdateContactChannelAsync(string partyId, UpdateContactChannel command, CancellationToken ct) => throw new NotImplementedException();

        public Task<PartiesCommandResult<PartyDetail>> UpdateContactChannelWithResultAsync(string partyId, UpdateContactChannel command, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> RemoveContactChannelAsync(string partyId, RemoveContactChannel command, CancellationToken ct) => throw new NotImplementedException();

        public Task<PartiesCommandResult<PartyDetail>> RemoveContactChannelWithResultAsync(string partyId, RemoveContactChannel command, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> AddIdentifierAsync(string partyId, AddIdentifier command, CancellationToken ct) => throw new NotImplementedException();

        public Task<PartiesCommandResult<PartyDetail>> AddIdentifierWithResultAsync(string partyId, AddIdentifier command, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> RemoveIdentifierAsync(string partyId, RemoveIdentifier command, CancellationToken ct) => throw new NotImplementedException();

        public Task<PartiesCommandResult<PartyDetail>> RemoveIdentifierWithResultAsync(string partyId, RemoveIdentifier command, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> DeactivatePartyAsync(string partyId, CancellationToken ct) => throw new NotImplementedException();

        public Task<PartiesCommandResult<PartyDetail>> DeactivatePartyWithResultAsync(string partyId, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> ReactivatePartyAsync(string partyId, CancellationToken ct) => throw new NotImplementedException();

        public Task<PartiesCommandResult<PartyDetail>> ReactivatePartyWithResultAsync(string partyId, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> CreatePartyCompositeAsync(CreatePartyComposite command, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> UpdatePartyCompositeAsync(string partyId, UpdatePartyComposite command, CancellationToken ct) => throw new NotImplementedException();

        public Task<string> SetIsNaturalPersonAsync(string partyId, SetIsNaturalPerson command, CancellationToken ct) => throw new NotImplementedException();

        public Task<PartiesCommandResult<PartyDetail>> SetIsNaturalPersonWithResultAsync(string partyId, SetIsNaturalPerson command, CancellationToken ct) => throw new NotImplementedException();
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

    private sealed record SearchRequestSnapshot(
        string Query,
        int Page,
        int PageSize,
        string? Mode,
        PartyType? Type,
        bool? Active);

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

    private sealed class TransportFailingGdprClient : IAdminPortalGdprClient
    {
        public Task<AdminPortalGdprCommandResult> RequestErasureAsync(string partyId, CancellationToken cancellationToken)
            => throw new HttpRequestException("transport down");

        public Task<PartyErasureStatusRecord?> GetErasureStatusAsync(string partyId, CancellationToken cancellationToken)
            => throw new HttpRequestException("transport down");

        public Task<ErasureCertificate?> GetErasureCertificateAsync(string partyId, CancellationToken cancellationToken)
            => throw new TimeoutException("timed out");

        public Task<AdminPortalGdprCommandResult> RetryErasureVerificationAsync(string partyId, CancellationToken cancellationToken)
            => throw new HttpRequestException("transport down");

        public Task<AdminPortalGdprCommandResult> RestrictProcessingAsync(string partyId, string? reason, CancellationToken cancellationToken)
            => throw new HttpRequestException("transport down");

        public Task<AdminPortalGdprCommandResult> LiftRestrictionAsync(string partyId, CancellationToken cancellationToken)
            => throw new HttpRequestException("transport down");

        public Task<AdminPortalGdprCommandResult> AddConsentAsync(
            string partyId,
            string channelId,
            string purpose,
            LawfulBasis lawfulBasis,
            CancellationToken cancellationToken)
            => throw new HttpRequestException("transport down");

        public Task<AdminPortalGdprCommandResult> RevokeConsentAsync(string partyId, string consentId, CancellationToken cancellationToken)
            => throw new HttpRequestException("transport down");

        public Task<IReadOnlyList<ConsentRecord>> GetConsentAsync(string partyId, CancellationToken cancellationToken)
            => throw new HttpRequestException("transport down");

        public Task<AdminPortalExportDownload> ExportPartyDataAsync(string partyId, CancellationToken cancellationToken)
            => throw new HttpRequestException("transport down");

        public Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(string partyId, CancellationToken cancellationToken)
            => throw new HttpRequestException("transport down");
    }

    private sealed class ContractUnavailableGdprClient : IAdminPortalGdprClient
    {
        private static PartiesClientException Exception()
            => new(501, AdminPortalGdprOutcome.ContractUnavailable.ToString(), null, null, null);

        public Task<AdminPortalGdprCommandResult> RequestErasureAsync(string partyId, CancellationToken cancellationToken)
            => throw Exception();

        public Task<PartyErasureStatusRecord?> GetErasureStatusAsync(string partyId, CancellationToken cancellationToken)
            => throw Exception();

        public Task<ErasureCertificate?> GetErasureCertificateAsync(string partyId, CancellationToken cancellationToken)
            => throw Exception();

        public Task<AdminPortalGdprCommandResult> RetryErasureVerificationAsync(string partyId, CancellationToken cancellationToken)
            => throw Exception();

        public Task<AdminPortalGdprCommandResult> RestrictProcessingAsync(string partyId, string? reason, CancellationToken cancellationToken)
            => throw Exception();

        public Task<AdminPortalGdprCommandResult> LiftRestrictionAsync(string partyId, CancellationToken cancellationToken)
            => throw Exception();

        public Task<AdminPortalGdprCommandResult> AddConsentAsync(
            string partyId,
            string channelId,
            string purpose,
            LawfulBasis lawfulBasis,
            CancellationToken cancellationToken)
            => throw Exception();

        public Task<AdminPortalGdprCommandResult> RevokeConsentAsync(string partyId, string consentId, CancellationToken cancellationToken)
            => throw Exception();

        public Task<IReadOnlyList<ConsentRecord>> GetConsentAsync(string partyId, CancellationToken cancellationToken)
            => throw Exception();

        public Task<AdminPortalExportDownload> ExportPartyDataAsync(string partyId, CancellationToken cancellationToken)
            => throw Exception();

        public Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(string partyId, CancellationToken cancellationToken)
            => throw Exception();
    }

    private sealed class StaticHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class SingleClientHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
