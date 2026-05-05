using System.Net;

using Hexalith.Parties.Picker.Services;
using Hexalith.Parties.Picker.Tests.Services;

using Shouldly;

namespace Hexalith.Parties.Picker.Tests.Services;

public sealed class PartyPickerApiClientTests
{
    [Fact]
    public async Task SearchAsync_WithoutAuthProvider_ReturnsAuthenticationRequiredWithoutCallingApi()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = new PartyPickerApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://parties.test/") });

        PartyPickerSearchResponse response = await client.SearchAsync(new PartyPickerSearchRequest
        {
            Query = "ada",
        }, CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.AuthenticationRequired);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_BuildsApprovedSearchRequestWithBoundedPageSizeAndHostAuthorization()
    {
        var handler = new RecordingHttpMessageHandler();
        handler.Enqueue(PartyPickerTestData.SearchResponse(PartyPickerTestData.Result()));
        var client = new PartyPickerApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://parties.test/") });

        PartyPickerSearchResponse response = await client.SearchAsync(new PartyPickerSearchRequest
        {
            Query = "  ada  ",
            Page = 2,
            PageSize = 250,
            Mode = PartyPickerSearchMode.Semantic,
            CaseId = "case-42",
            AccessTokenProvider = _ => ValueTask.FromResult<string?>("token-from-host"),
        }, CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Ready);
        handler.Requests.Count.ShouldBe(1);
        HttpRequestMessage request = handler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.ToString().ShouldBe("https://parties.test/api/v1/parties/search?q=ada&page=2&pageSize=100&mode=semantic&caseId=case-42");
        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("token-from-host");
    }

    [Theory]
    [InlineData("LocalOnly", PartyPickerSearchState.LocalOnly)]
    [InlineData("Degraded", PartyPickerSearchState.Degraded)]
    public async Task SearchAsync_MapsSearchStatusHeadersToBoundedStates(string header, PartyPickerSearchState expectedState)
    {
        var handler = new RecordingHttpMessageHandler();
        handler.Enqueue(PartyPickerTestData.SearchResponse(header, "backend reason", PartyPickerTestData.Result()));
        var client = new PartyPickerApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://parties.test/") });

        PartyPickerSearchResponse response = await client.SearchAsync(Request("ada"), CancellationToken.None);

        response.State.ShouldBe(expectedState);
        response.Metadata.SearchStatus.ShouldBe(header);
        response.Metadata.DegradedReason.ShouldBe("backend reason");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, PartyPickerSearchState.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, PartyPickerSearchState.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, PartyPickerSearchState.NotFound)]
    [InlineData(HttpStatusCode.Gone, PartyPickerSearchState.Gone)]
    [InlineData(HttpStatusCode.ServiceUnavailable, PartyPickerSearchState.TransientFailure)]
    public async Task SearchAsync_MapsApiFailuresToNonLeakingStates(HttpStatusCode statusCode, PartyPickerSearchState expectedState)
    {
        var handler = new RecordingHttpMessageHandler();
        handler.Enqueue(PartyPickerTestData.Failure(statusCode));
        var client = new PartyPickerApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://parties.test/") });

        PartyPickerSearchResponse response = await client.SearchAsync(Request("ada"), CancellationToken.None);

        response.State.ShouldBe(expectedState);
        response.SafeReason.ShouldNotBeNullOrWhiteSpace();
        response.SafeReason.ShouldNotContain("token");
    }

    [Fact]
    public async Task SearchAsync_AllowsHostRequestCustomizerInsteadOfTokenProvider()
    {
        var handler = new RecordingHttpMessageHandler();
        handler.Enqueue(PartyPickerTestData.SearchResponse(PartyPickerTestData.Result()));
        var client = new PartyPickerApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://parties.test/") });

        PartyPickerSearchResponse response = await client.SearchAsync(new PartyPickerSearchRequest
        {
            Query = "ada",
            RequestCustomizer = (message, _) =>
            {
                message.Headers.Add("X-Host-Auth", "present");
                return ValueTask.CompletedTask;
            },
        }, CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Ready);
        handler.Requests.Single().Headers.GetValues("X-Host-Auth").Single().ShouldBe("present");
    }

    [Fact]
    public async Task SearchAsync_WithInvisibleOnlyQuery_DoesNotCallApi()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = new PartyPickerApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://parties.test/") });

        PartyPickerSearchResponse response = await client.SearchAsync(Request("\u0000\u0001"), CancellationToken.None);

        response.State.ShouldBe(PartyPickerSearchState.Idle);
        handler.Requests.ShouldBeEmpty();
    }

    private static PartyPickerSearchRequest Request(string query)
        => new()
        {
            Query = query,
            AccessTokenProvider = _ => ValueTask.FromResult<string?>("host-token"),
        };
}
