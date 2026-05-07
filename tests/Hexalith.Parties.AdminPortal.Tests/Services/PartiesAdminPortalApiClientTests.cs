using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Services;

public sealed class PartiesAdminPortalApiClientTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public async Task ListPartiesAsync_BoundsPageSizeAndPreservesDegradedHeadersAsync()
    {
        string longReason = new('x', 256);
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(Page<PartyIndexEntry>(), _jsonOptions),
            new Dictionary<string, string>
            {
                ["X-Service-Degraded"] = "true",
                ["X-Stale-Data-Age"] = "PT12S",
                ["X-Parties-Search-Status"] = "local-only",
                ["X-Parties-Search-Degraded-Reason"] = longReason,
            });
        PartiesAdminPortalApiClient client = CreateClient(handler);

        AdminPortalQueryResult<PagedResult<PartyIndexEntry>> result = await client.ListPartiesAsync(
            new AdminPortalListRequest(Page: 1, PageSize: 250, Type: PartyType.Person, Active: true),
            CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldContain("/api/v1/parties?");
        handler.LastRequest.RequestUri.Query.ShouldContain("pageSize=100");
        handler.LastRequest.RequestUri.Query.ShouldContain("type=Person");
        handler.LastRequest.RequestUri.Query.ShouldContain("active=true");
        result.Metadata.ServiceDegraded.ShouldBe(true);
        result.Metadata.StaleDataAge.ShouldBe("PT12S");
        result.Metadata.SearchStatus.ShouldBe("local-only");
        result.Metadata.SearchDegradedReason!.Length.ShouldBe(128);
    }

    [Fact]
    public async Task SearchPartiesAsync_DeserializesPartySearchResponseShapeAndOmitsUnsupportedFiltersAsync()
    {
        // The backend /api/v1/parties/search endpoint returns PartySearchResponse {results, status, degradedReason, ...},
        // not a flat PagedResult<PartySearchResult>. Mirror that shape on the wire.
        var responseBody = new
        {
            results = Page(new PartySearchResult
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
            }),
            status = "localOnly",
            degradedReason = "rich-search-disabled",
        };
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(responseBody, _jsonOptions),
            new Dictionary<string, string>
            {
                ["X-Parties-Search-Status"] = "local-only",
            });
        PartiesAdminPortalApiClient client = CreateClient(handler);

        AdminPortalQueryResult<PagedResult<PartySearchResult>> result = await client.SearchPartiesAsync(
            new AdminPortalSearchRequest("ada@example.test", Page: 2, PageSize: 200, Type: PartyType.Person, Active: true),
            CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldStartWith("/api/v1/parties/search?");
        handler.LastRequest.RequestUri.Query.ShouldContain("q=ada%40example.test");
        handler.LastRequest.RequestUri.Query.ShouldContain("page=2");
        handler.LastRequest.RequestUri.Query.ShouldContain("pageSize=100");
        // Backend /search does not accept type or active — they must not be sent.
        handler.LastRequest.RequestUri.Query.ShouldNotContain("type=");
        handler.LastRequest.RequestUri.Query.ShouldNotContain("active=");
        result.Payload.Items.ShouldHaveSingleItem();
        result.Payload.Items[0].Party.DisplayName.ShouldBe("Ada Lovelace");
        result.Metadata.SearchStatus.ShouldBe("local-only");
    }

    [Fact]
    public async Task GetPartyAsync_OnForbidden_SurfacesBoundedFailureWithoutProblemDetailsLeakAsync()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.Forbidden,
            "{\"type\":\"about:blank\",\"title\":\"Forbidden\",\"status\":403,\"detail\":\"tenant=other party=p-99\"}");
        PartiesAdminPortalApiClient client = CreateClient(handler);

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.GetPartyAsync("tenant:other:p-99", CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.Forbidden);
        ex.Message.ShouldBe("Access is denied.");
        ex.Message.ShouldNotContain("tenant=other");
        ex.Message.ShouldNotContain("p-99");
    }

    [Fact]
    public async Task GetPartyAsync_NullOrEmptyId_ThrowsArgumentExceptionAsync()
    {
        PartiesAdminPortalApiClient client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "{}"));

        await Should.ThrowAsync<ArgumentException>(() => client.GetPartyAsync(null!, CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(() => client.GetPartyAsync(string.Empty, CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(() => client.GetPartyAsync("   ", CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, AdminPortalQueryFailureKind.Validation)]
    [InlineData(HttpStatusCode.UnprocessableEntity, AdminPortalQueryFailureKind.Validation)]
    [InlineData(HttpStatusCode.TooManyRequests, AdminPortalQueryFailureKind.TransientFailure)]
    [InlineData(HttpStatusCode.InternalServerError, AdminPortalQueryFailureKind.TransientFailure)]
    [InlineData(HttpStatusCode.BadGateway, AdminPortalQueryFailureKind.TransientFailure)]
    public async Task ListPartiesAsync_MapsHttpStatusCodesToBoundedFailureKindsAsync(HttpStatusCode status, AdminPortalQueryFailureKind expected)
    {
        var handler = new RecordingHttpMessageHandler(status, "{}");
        PartiesAdminPortalApiClient client = CreateClient(handler);

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.ListPartiesAsync(new AdminPortalListRequest(1, 20, null, null), CancellationToken.None));

        ex.Kind.ShouldBe(expected);
    }

    [Fact]
    public async Task ListPartiesAsync_OnNetworkFailure_SurfacesTransientFailureAsync()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("DNS failed"));
        PartiesAdminPortalApiClient client = CreateClient(handler);

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.ListPartiesAsync(new AdminPortalListRequest(1, 20, null, null), CancellationToken.None));

        ex.Kind.ShouldBe(AdminPortalQueryFailureKind.TransientFailure);
    }

    private static PartiesAdminPortalApiClient CreateClient(HttpMessageHandler handler)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("https://parties.test/") },
            Options.Create(new PartiesAdminPortalOptions()));

    private static PagedResult<T> Page<T>(params T[] items) => new()
    {
        Items = items,
        Page = items.Length == 0 ? 1 : 2,
        PageSize = 20,
        TotalCount = items.Length,
        TotalPages = items.Length == 0 ? 0 : 1,
    };

    private sealed class ThrowingHttpMessageHandler(Exception toThrow) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw toThrow;
    }
}
