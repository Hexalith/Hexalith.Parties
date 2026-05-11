using System.Net;

using Hexalith.Parties.Client;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Picker.Services;

using Shouldly;

namespace Hexalith.Parties.Picker.Tests.Services;

public sealed class PartyPickerApiClientTests
{
    [Fact]
    public async Task SearchAsync_WithoutAuthProvider_ReturnsAuthenticationRequiredWithoutCallingClient()
    {
        var queryClient = new RecordingPartiesQueryClient();
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(new PartyPickerSearchRequest
        {
            Query = "ada",
        }, CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.AuthenticationRequired);
        queryClient.SearchCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_UsesTypedPartiesQueryClientWithNormalizedQueryAndBoundedPageSize()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result()));
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(new PartyPickerSearchRequest
        {
            Query = "  a\u0000da  ",
            Page = 2,
            PageSize = 250,
            Mode = PartyPickerSearchMode.Semantic,
            CaseId = "case-42",
            AccessTokenProvider = _ => ValueTask.FromResult<string?>("token-from-host"),
        }, CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Ready);
        response.Metadata.SearchStatus.ShouldBe("Unavailable");
        queryClient.SearchCalls.ShouldBe([new SearchCall("ada", 2, PartyPickerDefaults.MaxPageSize)]);
        queryClient.LastMode.ShouldBe("semantic");
        queryClient.LastCaseId.ShouldBe("case-42");
        queryClient.LastRequestCustomizer.ShouldNotBeNull();

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/v1/queries");
        await queryClient.LastRequestCustomizer(message, CancellationToken.None);
        message.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        message.Headers.Authorization.Parameter.ShouldBe("token-from-host");
    }

    [Fact]
    public async Task SearchAsync_ComposesHostRequestCustomizerAfterBearerToken()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result()));
        var client = new PartyPickerApiClient(queryClient);

        await client.SearchAsync(new PartyPickerSearchRequest
        {
            Query = "ada",
            AccessTokenProvider = _ => ValueTask.FromResult<string?>("token-from-host"),
            RequestCustomizer = (message, _) =>
            {
                message.Headers.Add("X-Host-Context", "tenant-a");
                return ValueTask.CompletedTask;
            },
        }, CancellationToken.None);

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/v1/queries");
        await queryClient.LastRequestCustomizer!(message, CancellationToken.None);

        message.Headers.Authorization!.Parameter.ShouldBe("token-from-host");
        message.Headers.GetValues("X-Host-Context").ShouldBe(["tenant-a"]);
    }

    [Fact]
    public async Task SearchAsync_WithInvisibleOnlyQuery_DoesNotCallClient()
    {
        var queryClient = new RecordingPartiesQueryClient();
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(Request("\u0000\u0001"), CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Idle);
        queryClient.SearchCalls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(401, PartyPickerSearchState.Unauthorized)]
    [InlineData(403, PartyPickerSearchState.Forbidden)]
    [InlineData(404, PartyPickerSearchState.NotFound)]
    [InlineData(410, PartyPickerSearchState.Gone)]
    [InlineData(503, PartyPickerSearchState.TransientFailure)]
    public async Task SearchAsync_MapsClientFailuresToNonLeakingStates(int statusCode, PartyPickerSearchState expectedState)
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.ThrowOnSearch = new PartiesClientException(
            statusCode,
            "Problem title",
            "problem-type",
            "token party Ada Lovelace backend detail",
            "correlation-1");
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(Request("ada"), CancellationToken.None);

        response.State.ShouldBe(expectedState);
        response.SafeReason.ShouldNotBeNullOrWhiteSpace();
        response.SafeReason.ShouldNotContain("token");
        response.SafeReason.ShouldNotContain("Ada");
    }

    [Fact]
    public async Task SearchAsync_EmptyTypedResult_ReturnsEmptyState()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage());
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(Request("ada"), CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Empty);
    }

    private static PartyPickerSearchRequest Request(string query)
        => new()
        {
            Query = query,
            AccessTokenProvider = _ => ValueTask.FromResult<string?>("host-token"),
        };

    private static PagedResult<PartySearchResult> SearchResultPage(params PartySearchResult[] results)
        => new()
        {
            Items = results,
            Page = 1,
            PageSize = 10,
            TotalCount = results.Length,
            TotalPages = results.Length == 0 ? 0 : 1,
        };

    internal sealed record SearchCall(string Query, int Page, int PageSize);

    internal sealed class RecordingPartiesQueryClient : IPartiesQueryClient
    {
        private readonly Queue<PagedResult<PartySearchResult>> _responses = [];

        public List<SearchCall> SearchCalls { get; } = [];

        public Exception? ThrowOnSearch { get; set; }

        public void Enqueue(PagedResult<PartySearchResult> response)
            => _responses.Enqueue(response);

        public Task<PartyDetail> GetPartyAsync(string partyId, CancellationToken ct)
            => throw new NotSupportedException();

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
            => throw new NotSupportedException();

        public Func<HttpRequestMessage, CancellationToken, ValueTask>? LastRequestCustomizer { get; private set; }

        public string? LastMode { get; private set; }

        public string? LastCaseId { get; private set; }

        public Task<PagedResult<PartySearchResult>> SearchPartiesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken ct,
            string? mode = null,
            string? caseId = null,
            Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null)
        {
            SearchCalls.Add(new SearchCall(query, page, pageSize));
            LastMode = mode;
            LastCaseId = caseId;
            LastRequestCustomizer = requestCustomizer;

            if (ThrowOnSearch is not null)
            {
                throw ThrowOnSearch;
            }

            return Task.FromResult(_responses.Count == 0 ? SearchResultPage() : _responses.Dequeue());
        }
    }
}
