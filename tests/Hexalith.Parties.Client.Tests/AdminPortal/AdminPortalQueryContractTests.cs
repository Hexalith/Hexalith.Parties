using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Client.Tests.AdminPortal;

public sealed class AdminPortalQueryContractTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public async Task ListPartiesAsync_WhenTenantSwitchCancelsToken_PropagatesCancellationAsync()
    {
        using var cts = new CancellationTokenSource();
        var handler = new AdminPortalHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1), ct).ConfigureAwait(false);
            return JsonResponse(HttpStatusCode.OK, EmptyIndexPage());
        });
        PartiesAdminPortalApiClient client = CreateClient(handler);

        Task<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>> pending = client.ListPartiesAsync(
            new AdminPortalListRequest(1, 20, null, null),
            cts.Token);

        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(pending);
    }

    [Fact]
    public async Task ListPartiesAsync_PageSizeAboveServerCap_ClientClampsTo100Async()
    {
        var handler = new AdminPortalHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, EmptyIndexPage())));
        PartiesAdminPortalApiClient client = CreateClient(handler);

        await client.ListPartiesAsync(
            new AdminPortalListRequest(1, 200, null, null),
            CancellationToken.None);

        string query = handler.LastRequest!.RequestUri!.Query;
        query.ShouldContain("pageSize=100");
        query.ShouldNotContain("pageSize=200");
    }

    [Fact]
    public async Task ListPartiesAsync_WhenResponseHasDegradedHeaders_SurfacesBoundedMetadataToCallerAsync()
    {
        var handler = new AdminPortalHandler((_, _) =>
        {
            HttpResponseMessage response = JsonResponse(HttpStatusCode.OK, EmptyIndexPage());
            response.Headers.Add("X-Service-Degraded", "true");
            response.Headers.Add("X-Stale-Data-Age", "PT12S");
            // Backend emits PascalCase for the search-status header (Enum.ToString) — verify
            // the client preserves it verbatim and treats it as local-only.
            response.Headers.Add("X-Parties-Search-Status", "LocalOnly");
            response.Headers.Add("X-Parties-Search-Degraded-Reason", new string('x', 256));
            return Task.FromResult(response);
        });
        PartiesAdminPortalApiClient client = CreateClient(handler);

        AdminPortalQueryResult<PagedResult<PartyIndexEntry>> result = await client.ListPartiesAsync(
            new AdminPortalListRequest(1, 20, null, null),
            CancellationToken.None);

        result.Metadata.ServiceDegraded.ShouldBeTrue();
        result.Metadata.StaleDataAge.ShouldBe("PT12S");
        result.Metadata.SearchStatus.ShouldBe("LocalOnly");
        result.Metadata.IsLocalOnlySearch.ShouldBeTrue();
        result.Metadata.SearchDegradedReason!.Length.ShouldBe(128);
    }

    [Fact]
    public async Task SearchPartiesAsync_EmptyQuery_SendsApprovedSearchRequestAsync()
    {
        var responseBody = new
        {
            results = new PagedResult<PartySearchResult>
            {
                Items = [],
                Page = 1,
                PageSize = 20,
                TotalCount = 0,
                TotalPages = 0,
            },
            status = "LocalOnly",
        };
        var handler = new AdminPortalHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, responseBody)));
        PartiesAdminPortalApiClient client = CreateClient(handler);

        AdminPortalQueryResult<PagedResult<PartySearchResult>> result = await client.SearchPartiesAsync(
            new AdminPortalSearchRequest(string.Empty, 1, 20, null, null),
            CancellationToken.None);

        result.Payload.Items.ShouldBeEmpty();
        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldStartWith("/api/v1/parties/search?");
        handler.LastRequest.RequestUri.Query.ShouldContain("q=");
    }

    [Fact]
    public async Task SearchPartiesAsync_RichSearchUnavailable_KeepsDisplayNameOnlyMetadataAsync()
    {
        var responseBody = new
        {
            results = new PagedResult<PartySearchResult>
            {
                Items =
                [
                    new PartySearchResult
                    {
                        Party = IndexEntry("p-1", "Anna Smith", PartyType.Person, true),
                        Matches = [],
                        RelevanceScore = 0.0,
                    },
                ],
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
                TotalPages = 1,
            },
            status = "LocalOnly",
            degradedReason = "rich-search-disabled",
        };
        var handler = new AdminPortalHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, responseBody)));
        PartiesAdminPortalApiClient client = CreateClient(handler);

        AdminPortalQueryResult<PagedResult<PartySearchResult>> result = await client.SearchPartiesAsync(
            new AdminPortalSearchRequest("anna@example.com", 1, 20, null, null),
            CancellationToken.None);

        result.Payload.Items.Count.ShouldBe(1);
        result.Metadata.IsLocalOnlySearch.ShouldBeTrue();
        result.Metadata.SearchDegradedReason.ShouldBe("rich-search-disabled");
        handler.LastRequest!.RequestUri!.Query.ShouldNotContain("type=");
        handler.LastRequest.RequestUri.Query.ShouldNotContain("active=");
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone, AdminPortalQueryFailureKind.Gone)]
    [InlineData(HttpStatusCode.Forbidden, AdminPortalQueryFailureKind.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized, AdminPortalQueryFailureKind.AuthenticationRequired)]
    public async Task QueryFailures_SurfaceTypedBoundedOutcomesAsync(HttpStatusCode status, AdminPortalQueryFailureKind expected)
    {
        var handler = new AdminPortalHandler((_, _) => Task.FromResult(JsonResponse(status, new
        {
            title = "Forbidden",
            detail = "tenant=other party=p-99",
        })));
        PartiesAdminPortalApiClient client = CreateClient(handler);

        AdminPortalQueryException ex = await Should.ThrowAsync<AdminPortalQueryException>(
            () => client.GetPartyAsync("tenant:other:p-99", CancellationToken.None));

        ex.Kind.ShouldBe(expected);
        ex.Message.ShouldNotContain("tenant=other");
        ex.Message.ShouldNotContain("p-99");
    }

    private static PartiesAdminPortalApiClient CreateClient(HttpMessageHandler handler)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") },
            Options.Create(new PartiesAdminPortalOptions()));

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body)
        => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, _jsonOptions), System.Text.Encoding.UTF8, "application/json"),
        };

    private static PagedResult<PartyIndexEntry> EmptyIndexPage() => new()
    {
        Items = [],
        Page = 1,
        PageSize = 20,
        TotalCount = 0,
        TotalPages = 0,
    };

    private static PartyIndexEntry IndexEntry(string id, string name, PartyType type, bool active) => new()
    {
        Id = id,
        Type = type,
        IsActive = active,
        DisplayName = name,
        CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
        LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
    };

    private sealed class AdminPortalHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            cancellationToken.ThrowIfCancellationRequested();
            return handler(request, cancellationToken);
        }
    }
}
