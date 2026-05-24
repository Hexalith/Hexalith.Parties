using System.Net;

using Hexalith.Parties.Client;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Picker.Services;
using Hexalith.Parties.Picker.Tests.Fakes;

using Shouldly;

namespace Hexalith.Parties.Picker.Tests.Services;

public sealed class PartyPickerApiClientTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0000\u0001")]
    public async Task ResolveSelectedPartyAsync_WithBlankOrControlOnlyPartyId_ReturnsNotFoundWithoutCallingClient(string partyId)
    {
        var queryClient = new RecordingPartiesQueryClient();
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSelection response = await client.ResolveSelectedPartyAsync(new PartyPickerSelectedPartyRequest
        {
            PartyId = partyId,
            AccessTokenProvider = _ => ValueTask.FromResult<string?>("host-token"),
        }, CancellationToken.None);

        response.State.ShouldBe(PartyPickerSelectionState.NotFound);
        response.SafeReason.ShouldNotBeNullOrWhiteSpace();
        queryClient.GetCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveSelectedPartyAsync_WithoutAuthProvider_ReturnsAuthenticationRequiredWithoutCallingClient()
    {
        var queryClient = new RecordingPartiesQueryClient();
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSelection response = await client.ResolveSelectedPartyAsync(new PartyPickerSelectedPartyRequest
        {
            PartyId = "party-1",
        }, CancellationToken.None);

        response.PartyId.ShouldBe("party-1");
        response.State.ShouldBe(PartyPickerSelectionState.AuthenticationRequired);
        queryClient.GetCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveSelectedPartyAsync_UsesTypedPartiesQueryClientAndHostAuth()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.EnqueueDetail(PartyDetail(id: "party-1", name: "Ada Lovelace"));
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSelection response = await client.ResolveSelectedPartyAsync(new PartyPickerSelectedPartyRequest
        {
            PartyId = "party-1",
            AccessTokenProvider = _ => ValueTask.FromResult<string?>("token-from-host"),
            RequestCustomizer = (message, _) =>
            {
                message.Headers.Add("X-Host-Context", "tenant-a");
                return ValueTask.CompletedTask;
            },
        }, CancellationToken.None);

        response.PartyId.ShouldBe("party-1");
        response.DisplayName.ShouldBe("Ada Lovelace");
        response.State.ShouldBe(PartyPickerSelectionState.Available);
        queryClient.GetCalls.ShouldBe(["party-1"]);
        queryClient.LastRequestCustomizer.ShouldNotBeNull();

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/v1/queries");
        await queryClient.LastRequestCustomizer(message, CancellationToken.None);
        message.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        message.Headers.Authorization.Parameter.ShouldBe("token-from-host");
        message.Headers.GetValues("X-Host-Context").ShouldBe(["tenant-a"]);
    }

    [Fact]
    public async Task ResolveSelectedPartyAsync_WithRequestCustomizerOnly_CallsTypedClient()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.EnqueueDetail(PartyDetail(id: "party-1", name: "Ada Lovelace"));
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSelection response = await client.ResolveSelectedPartyAsync(new PartyPickerSelectedPartyRequest
        {
            PartyId = "party-1",
            RequestCustomizer = (_, _) => ValueTask.CompletedTask,
        }, CancellationToken.None);

        response.PartyId.ShouldBe("party-1");
        response.DisplayName.ShouldBe("Ada Lovelace");
        response.State.ShouldBe(PartyPickerSelectionState.Available);
        queryClient.GetCalls.ShouldBe(["party-1"]);
    }

    [Theory]
    [InlineData(401, PartyPickerSelectionState.Unauthorized)]
    [InlineData(403, PartyPickerSelectionState.Forbidden)]
    [InlineData(404, PartyPickerSelectionState.NotFound)]
    [InlineData(410, PartyPickerSelectionState.Gone)]
    [InlineData(503, PartyPickerSelectionState.TransientFailure)]
    public async Task ResolveSelectedPartyAsync_MapsClientFailuresToNonLeakingStates(
        int statusCode,
        PartyPickerSelectionState expectedState)
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnGet = new PartiesClientException(
                statusCode,
                "Problem title",
                "problem-type",
                "token party Ada Lovelace backend detail",
                "correlation-1"),
        };
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSelection response = await client.ResolveSelectedPartyAsync(SelectedRequest("party-1"), CancellationToken.None);

        response.PartyId.ShouldBe("party-1");
        response.State.ShouldBe(expectedState);
        response.SafeReason.ShouldNotBeNullOrWhiteSpace();
        response.SafeReason.ShouldNotContain("token");
        response.SafeReason.ShouldNotContain("Ada");
    }

    [Fact]
    public async Task ResolveSelectedPartyAsync_MalformedClientFailureReturnsBoundedUnavailableState()
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnGet = new PartiesClientException("raw token Ada Lovelace backend detail"),
        };
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSelection response = await client.ResolveSelectedPartyAsync(SelectedRequest("party-1"), CancellationToken.None);

        response.State.ShouldBe(PartyPickerSelectionState.Unavailable);
        response.SafeReason.ShouldNotBeNull();
        response.SafeReason!.ShouldNotContain("token");
        response.SafeReason.ShouldNotContain("Ada");
    }

    [Fact]
    public async Task ResolveSelectedPartyAsync_TransportFailureReturnsTransientState()
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnGet = new HttpRequestException("raw token Ada Lovelace transport detail"),
        };
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSelection response = await client.ResolveSelectedPartyAsync(SelectedRequest("party-1"), CancellationToken.None);

        response.State.ShouldBe(PartyPickerSelectionState.TransientFailure);
        response.SafeReason.ShouldNotBeNull();
        response.SafeReason!.ShouldNotContain("token");
        response.SafeReason.ShouldNotContain("Ada");
    }

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
    public async Task SearchAsync_WithHostRequestCustomizerOnly_CallsTypedClient()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result()));
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(new PartyPickerSearchRequest
        {
            Query = "ada",
            RequestCustomizer = (_, _) => ValueTask.CompletedTask,
        }, CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Ready);
        queryClient.SearchCalls.ShouldBe([new SearchCall("ada", 1, PartyPickerDefaults.PageSize)]);
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

    [Fact]
    public async Task SearchAsync_NegativePage_IsClampedToOneInAllResponseBranches()
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnSearch = new PartiesClientException(401, "Unauthorized", null, "raw", "correlation"),
        };
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(new PartyPickerSearchRequest
        {
            Query = "ada",
            Page = -5,
            AccessTokenProvider = _ => ValueTask.FromResult<string?>("host-token"),
        }, CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Unauthorized);
        response.Page.ShouldBe(1);
    }

    [Fact]
    public async Task SearchAsync_NullItemsInPayload_ReturnsEmptyStateWithoutThrowing()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(new PagedResult<PartySearchResult>
        {
            Items = null!,
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 0,
        });
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(Request("ada"), CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Empty);
    }

    [Fact]
    public async Task SearchAsync_ReturnsOnlyBoundedVisibleResultsWhenClientOverReturns()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(
            3,
            PartyPickerTestData.Result(id: "party-1", name: "Ada One"),
            PartyPickerTestData.Result(id: "party-2", name: "Ada Two"),
            PartyPickerTestData.Result(id: "party-3", name: "Ada Three")));
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(new PartyPickerSearchRequest
        {
            Query = "ada",
            PageSize = 2,
            AccessTokenProvider = _ => ValueTask.FromResult<string?>("host-token"),
        }, CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Ready);
        response.Results.Select(r => r.Party.DisplayName).ShouldBe(["Ada One", "Ada Two"]);
        response.PageSize.ShouldBe(2);
        response.TotalCount.ShouldBe(3);
        response.HasReliableTotalCount.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ProjectionFreshnessStatus.LocalOnly, PartyPickerSearchState.LocalOnly, "LocalOnly")]
    [InlineData(ProjectionFreshnessStatus.Degraded, PartyPickerSearchState.Degraded, "Degraded")]
    [InlineData(ProjectionFreshnessStatus.Stale, PartyPickerSearchState.Degraded, "Degraded")]
    [InlineData(ProjectionFreshnessStatus.Rebuilding, PartyPickerSearchState.Degraded, "Degraded")]
    public async Task SearchAsync_MapsFreshnessToBoundedSearchState(
        ProjectionFreshnessStatus freshnessStatus,
        PartyPickerSearchState expectedState,
        string expectedSearchStatus)
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(
            1,
            ProjectionFreshnessMetadata.Create(freshnessStatus, "raw tenant token backend detail"),
            PartyPickerTestData.Result()));
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(Request("ada"), CancellationToken.None);

        response.State.ShouldBe(expectedState);
        response.Metadata.SearchStatus.ShouldBe(expectedSearchStatus);
        response.Results.Count.ShouldBe(1);
        response.SafeReason.ShouldNotBeNullOrWhiteSpace();
        response.SafeReason!.ShouldNotContain("raw");
        response.SafeReason.ShouldNotContain("tenant");
        response.SafeReason.ShouldNotContain("token");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task SearchAsync_InconsistentTotalCount_IsNotMarkedReliable(int totalCount)
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(
            totalCount,
            PartyPickerTestData.Result(id: "party-1", name: "Ada One"),
            PartyPickerTestData.Result(id: "party-2", name: "Ada Two")));
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(Request("ada"), CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Ready);
        response.Results.Count.ShouldBe(2);
        response.VisibleCount.ShouldBe(2);
        response.HasReliableTotalCount.ShouldBeFalse();
    }

    [Fact]
    public async Task SearchAsync_UnexpectedException_MapsToBoundedErrorState()
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnSearch = new InvalidOperationException("raw token Ada Lovelace internal detail"),
        };
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(Request("ada"), CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Error);
        response.SafeReason.ShouldNotBeNullOrWhiteSpace();
        response.SafeReason.ShouldNotContain("token");
        response.SafeReason.ShouldNotContain("Ada");
    }

    [Fact]
    public async Task SearchAsync_TokenProviderThrows_ReturnsAuthenticationRequired()
    {
        var queryClient = new RecordingPartiesQueryClient();
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(new PartyPickerSearchRequest
        {
            Query = "ada",
            AccessTokenProvider = _ => throw new InvalidOperationException("token cache corrupted"),
        }, CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.AuthenticationRequired);
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
    public async Task SearchAsync_MapsMalformedClientFailureToBoundedErrorState()
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnSearch = new PartiesClientException("raw token Ada Lovelace backend detail"),
        };
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(Request("ada"), CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Error);
        response.SafeReason.ShouldNotBeNull();
        response.SafeReason.ShouldBe("The Parties client returned an invalid response.");
        response.SafeReason.ShouldNotContain("token");
        response.SafeReason.ShouldNotContain("Ada");
    }

    [Fact]
    public async Task SearchAsync_MapsTransportFailureToTransientState()
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnSearch = new HttpRequestException("raw token Ada Lovelace transport detail"),
        };
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(Request("ada"), CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.TransientFailure);
        response.SafeReason.ShouldNotBeNull();
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

    [Fact]
    public async Task SearchAsync_FiltersNullPartyItems()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(new PagedResult<PartySearchResult>
        {
            Items =
            [
                new PartySearchResult { Party = null!, Matches = [] },
                PartyPickerTestData.Result(),
            ],
            Page = 1,
            PageSize = 10,
            TotalCount = 2,
            TotalPages = 1,
        });
        var client = new PartyPickerApiClient(queryClient);

        PartyPickerSearchResponse response = await client.SearchAsync(Request("ada"), CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Ready);
        response.Results.Count.ShouldBe(1);
    }

    private static PartyPickerSearchRequest Request(string query)
        => new()
        {
            Query = query,
            AccessTokenProvider = _ => ValueTask.FromResult<string?>("host-token"),
        };

    private static PartyPickerSelectedPartyRequest SelectedRequest(string partyId)
        => new()
        {
            PartyId = partyId,
            AccessTokenProvider = _ => ValueTask.FromResult<string?>("host-token"),
        };

    private static PartyDetail PartyDetail(
        string id = "party-1",
        string name = "Ada Lovelace",
        bool active = true,
        bool erased = false)
        => new()
        {
            Id = id,
            Type = Hexalith.Parties.Contracts.ValueObjects.PartyType.Person,
            IsActive = active,
            DisplayName = name,
            SortName = name,
            CreatedAt = DateTimeOffset.Parse("2026-05-05T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-05T00:00:00Z"),
            IsErased = erased,
        };

    private static PagedResult<PartySearchResult> SearchResultPage(params PartySearchResult[] results)
        => SearchResultPage(results.Length, results);

    private static PagedResult<PartySearchResult> SearchResultPage(int totalCount, params PartySearchResult[] results)
        => SearchResultPage(totalCount, null, results);

    private static PagedResult<PartySearchResult> SearchResultPage(
        int totalCount,
        ProjectionFreshnessMetadata? freshness,
        params PartySearchResult[] results)
        => new()
        {
            Items = results,
            Page = 1,
            PageSize = Math.Max(1, results.Length),
            TotalCount = totalCount,
            TotalPages = results.Length == 0 ? 0 : 1,
            Freshness = freshness,
        };
}
