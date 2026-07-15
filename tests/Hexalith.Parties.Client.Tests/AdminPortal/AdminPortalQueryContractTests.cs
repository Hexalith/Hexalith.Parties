using Hexalith.FrontComposer.Contracts.Communication;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.Options;

using Shouldly;

using System.Xml.Linq;

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

        QueryRequest request = queryService.LastRequest.ShouldNotBeNull();
        request.Criteria.Take.ShouldBe(100);
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
        request.Criteria.SearchQuery.ShouldBe("anna@example.com");
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
            .Where(static path =>
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        // Strip comments before scanning: comments may legitimately reference retired
        // routes as historical context. String literals are preserved so any literal
        // mention of a retired route in production code still trips the test.
        string source = string.Join(
            Environment.NewLine,
            files.Select(static path => StripCommentsPreservingStringLiterals(File.ReadAllText(path))));

        source.ShouldNotContain("api/v1/parties");
        source.ShouldNotContain("api/v1/admin");
        source.ShouldNotContain("MapControllers");
        source.ShouldNotContain("MarkupString");
        source.ShouldNotContain("AddMarkupContent");
    }

    private static string StripCommentsPreservingStringLiterals(string source)
    {
        var output = new System.Text.StringBuilder(source.Length);
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];

            // Verbatim string literal @"..." — preserve verbatim, double-quote escapes "" stay inside.
            if (c == '@' && i + 1 < source.Length && source[i + 1] == '"')
            {
                output.Append(c);
                output.Append(source[i + 1]);
                i += 2;
                while (i < source.Length)
                {
                    if (source[i] == '"')
                    {
                        if (i + 1 < source.Length && source[i + 1] == '"')
                        {
                            output.Append('"');
                            output.Append('"');
                            i += 2;
                            continue;
                        }

                        output.Append('"');
                        i++;
                        break;
                    }

                    output.Append(source[i]);
                    i++;
                }

                continue;
            }

            // Regular string literal "..." — preserve, with backslash escapes.
            if (c == '"')
            {
                output.Append(c);
                i++;
                while (i < source.Length)
                {
                    char ch = source[i];
                    if (ch == '\\' && i + 1 < source.Length)
                    {
                        output.Append(ch);
                        output.Append(source[i + 1]);
                        i += 2;
                        continue;
                    }

                    output.Append(ch);
                    i++;
                    if (ch == '"' || ch == '\n')
                    {
                        break;
                    }
                }

                continue;
            }

            // Char literal '...' — preserve.
            if (c == '\'')
            {
                output.Append(c);
                i++;
                while (i < source.Length)
                {
                    char ch = source[i];
                    if (ch == '\\' && i + 1 < source.Length)
                    {
                        output.Append(ch);
                        output.Append(source[i + 1]);
                        i += 2;
                        continue;
                    }

                    output.Append(ch);
                    i++;
                    if (ch == '\'' || ch == '\n')
                    {
                        break;
                    }
                }

                continue;
            }

            // Block comment /* ... */
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                {
                    i++;
                }

                i = Math.Min(i + 2, source.Length);
                continue;
            }

            // Line comment // ...
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            // Razor comment @* ... *@
            if (c == '@' && i + 1 < source.Length && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '@'))
                {
                    i++;
                }

                i = Math.Min(i + 2, source.Length);
                continue;
            }

            output.Append(c);
            i++;
        }

        return output.ToString();
    }

    [Fact]
    public void AdminPortalProject_ReferencesFrontComposerShellAndPartiesClientBoundaries()
    {
        string root = LocateRepositoryRoot();
        string projectPath = Path.Combine(root, "src", "Hexalith.Parties.AdminPortal", "Hexalith.Parties.AdminPortal.csproj");
        XDocument project = XDocument.Load(projectPath);

        string[] projectReferences = project
            .Descendants()
            .Where(static element => element.Name.LocalName == "ProjectReference")
            .Select(static element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();

        projectReferences.ShouldContain(static include => include.EndsWith(
            @"Hexalith.FrontComposer.Shell\Hexalith.FrontComposer.Shell.csproj",
            StringComparison.Ordinal));
        projectReferences.ShouldContain(static include => include.EndsWith(
            @"Hexalith.Parties.Client\Hexalith.Parties.Client.csproj",
            StringComparison.Ordinal));
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
