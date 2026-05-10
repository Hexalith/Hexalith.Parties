using Hexalith.FrontComposer.Contracts.Communication;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Client.Tests.AdminPortal;

public sealed class AdminPortalQueryContractTests
{
    [Fact]
    public async Task ListPartiesAsync_WhenTenantSwitchCancelsToken_PropagatesCancellationAsync()
    {
        using var cts = new CancellationTokenSource();
        var queryService = new BlockingQueryService();
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        Task<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>> pending = client.ListPartiesAsync(
            new AdminPortalListRequest(1, 20, null, null),
            cts.Token);

        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(pending);
    }

    [Fact]
    public async Task ListPartiesAsync_ClampsPageSizeInEventStoreQueryRequestAsync()
    {
        var queryService = new RecordingQueryService();
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        await client.ListPartiesAsync(
            new AdminPortalListRequest(1, 200, null, null),
            CancellationToken.None);

        queryService.LastRequest.ShouldNotBeNull().Take.ShouldBe(100);
    }

    [Fact]
    public async Task SearchPartiesAsync_UsesPostQueriesContractShapeThroughFrontComposerAsync()
    {
        var queryService = new RecordingQueryService();
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        await client.SearchPartiesAsync(
            new AdminPortalSearchRequest("anna@example.com", 1, 20, null, null),
            CancellationToken.None);

        QueryRequest request = queryService.LastRequest.ShouldNotBeNull();
        request.Domain.ShouldBe("party");
        request.QueryType.ShouldBe("SearchParties");
        request.SearchQuery.ShouldBe("anna@example.com");
        request.CacheDiscriminator.ShouldBe("parties-admin-search-v1");
    }

    [Fact]
    public async Task QueryFailures_SurfaceTypedBoundedOutcomesAsync()
    {
        var queryService = new RecordingQueryService
        {
            ExceptionToThrow = new QueryFailureException(
                QueryFailureKind.NotFound,
                ProblemDetailsPayload.Empty),
        };
        PartiesAdminPortalApiClient client = CreateClient(queryService);

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.GetPartyAsync("p-99", CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.NotFound);
        ex.Message.ShouldNotContain("p-99");
    }

    [Fact]
    public void AdminPortalSource_DoesNotContainRetiredPartiesRestReads()
    {
        string root = LocateRepositoryRoot();
        string sourceRoot = Path.Combine(root, "src", "Hexalith.Parties.AdminPortal");
        string[] files = Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        string source = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        source.ShouldNotContain("api/v1/parties");
        source.ShouldNotContain("api/v1/admin");
        source.ShouldNotContain("MapControllers");
        source.ShouldNotContain("MarkupString");
        source.ShouldNotContain("AddMarkupContent");
    }

    private static PartiesAdminPortalApiClient CreateClient(IQueryService queryService)
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
            }));

    private static string LocateRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Parties.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private sealed class RecordingQueryService : IQueryService
    {
        public QueryRequest? LastRequest { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public Task<QueryResult<T>> QueryAsync<T>(QueryRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(new QueryResult<T>([], 0, ETag: null));
        }
    }

    private sealed class BlockingQueryService : IQueryService
    {
        public async Task<QueryResult<T>> QueryAsync<T>(QueryRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
            return new QueryResult<T>([], 0, ETag: null);
        }
    }
}
