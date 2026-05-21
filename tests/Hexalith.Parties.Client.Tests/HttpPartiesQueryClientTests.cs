using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Client.Tests;

public sealed class HttpPartiesQueryClientTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task GetPartyAsync_SubmitsEventStoreQueryAndReturnsPartyDetailAsync()
    {
        var expectedDetail = new PartyDetail
        {
            Id = "p-1",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "John Doe",
            SortName = "Doe, John",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };

        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler handler) = CreateClient(
            HttpStatusCode.OK,
            QueryResponse(expectedDetail));

        PartyDetail result = await client.GetPartyAsync("p-1", CancellationToken.None);

        result.Id.ShouldBe("p-1");
        result.DisplayName.ShouldBe("John Doe");
        result.Type.ShouldBe(PartyType.Person);
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.ShouldBe("/api/v1/queries");

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        JsonElement root = body.RootElement;
        root.GetProperty("tenant").GetString().ShouldBe("tenant-a");
        root.GetProperty("domain").GetString().ShouldBe("party");
        root.GetProperty("aggregateId").GetString().ShouldBe("p-1");
        root.GetProperty("entityId").GetString().ShouldBe("p-1");
        root.GetProperty("queryType").GetString().ShouldBe("PartyDetail");
        root.GetProperty("projectionType").GetString().ShouldBe("party-detail");
        root.GetProperty("projectionActorType").GetString().ShouldBe("PartyDetailProjectionQueryActor");
    }

    [Fact]
    public async Task GetPartyAsync_DeserializesProjectionFreshnessMetadataFromEventStorePayloadAsync()
    {
        var expectedDetail = new PartyDetail
        {
            Id = "p-stale",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Stale Person",
            SortName = "Person, Stale",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
            Freshness = new ProjectionFreshnessMetadata
            {
                Status = ProjectionFreshnessStatus.Stale,
                WarningCodes = ["projection-state-store-unavailable"],
            },
        };

        (HttpPartiesQueryClient client, _) = CreateClient(
            HttpStatusCode.OK,
            QueryResponse(expectedDetail));

        PartyDetail result = await client.GetPartyAsync("p-stale", CancellationToken.None);

        result.Freshness.ShouldNotBeNull();
        result.Freshness.Status.ShouldBe(ProjectionFreshnessStatus.Stale);
        result.Freshness.WarningCodes.ShouldBe(["projection-state-store-unavailable"]);
    }

    [Fact]
    public async Task ListPartiesAsync_SubmitsEventStoreQueryWithTypedPayloadAsync()
    {
        var expectedResult = new PagedResult<PartyIndexEntry>
        {
            Items =
            [
                new PartyIndexEntry
                {
                    Id = "p-1",
                    Type = PartyType.Person,
                    IsActive = true,
                    DisplayName = "John Doe",
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastModifiedAt = DateTimeOffset.UtcNow,
                },
            ],
            Page = 1,
            PageSize = 20,
            TotalCount = 1,
            TotalPages = 1,
        };

        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler handler) = CreateClient(
            HttpStatusCode.OK,
            QueryResponse(expectedResult));

        PagedResult<PartyIndexEntry> result = await client.ListPartiesAsync(
            1,
            20,
            PartyType.Person,
            true,
            DateTimeOffset.Parse("2026-03-01T10:11:12Z"),
            null,
            null,
            DateTimeOffset.Parse("2026-03-06T08:09:10+01:00"),
            CancellationToken.None);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Id.ShouldBe("p-1");

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        JsonElement root = body.RootElement;
        root.GetProperty("queryType").GetString().ShouldBe("PartyIndex");
        root.GetProperty("projectionType").GetString().ShouldBe("party-index");
        root.GetProperty("projectionActorType").GetString().ShouldBe("PartyIndexProjectionQueryActor");

        JsonElement payload = root.GetProperty("payload");
        payload.GetProperty("page").GetInt32().ShouldBe(1);
        payload.GetProperty("pageSize").GetInt32().ShouldBe(20);
        payload.GetProperty("type").GetString().ShouldBe("Person");
        payload.GetProperty("active").GetBoolean().ShouldBeTrue();
        payload.GetProperty("createdAfter").GetString().ShouldBe("2026-03-01T10:11:12.0000000+00:00");
        payload.GetProperty("modifiedBefore").GetString().ShouldBe("2026-03-06T08:09:10.0000000+01:00");
    }

    [Fact]
    public async Task ListPartiesAsync_DeserializesProjectionFreshnessMetadataFromEventStorePayloadAsync()
    {
        var expectedResult = new PagedResult<PartyIndexEntry>
        {
            Items = [],
            Page = 1,
            PageSize = 20,
            TotalCount = 0,
            TotalPages = 1,
            Freshness = new ProjectionFreshnessMetadata
            {
                Status = ProjectionFreshnessStatus.Stale,
                WarningCodes = ["projection-state-store-unavailable"],
            },
        };

        (HttpPartiesQueryClient client, _) = CreateClient(
            HttpStatusCode.OK,
            QueryResponse(expectedResult));

        PagedResult<PartyIndexEntry> result = await client.ListPartiesAsync(
            1,
            20,
            null,
            null,
            null,
            null,
            null,
            null,
            CancellationToken.None);

        result.Freshness.ShouldNotBeNull();
        result.Freshness.Status.ShouldBe(ProjectionFreshnessStatus.Stale);
        result.Freshness.WarningCodes.ShouldBe(["projection-state-store-unavailable"]);
    }

    [Fact]
    public async Task SearchPartiesAsync_DeserializesCurrentFreshnessWithoutWarningsAsync()
    {
        var expectedResult = new PagedResult<PartySearchResult>
        {
            Items = [],
            Page = 1,
            PageSize = 20,
            TotalCount = 0,
            TotalPages = 1,
            Freshness = new ProjectionFreshnessMetadata { Status = ProjectionFreshnessStatus.Current },
        };

        (HttpPartiesQueryClient client, _) = CreateClient(
            HttpStatusCode.OK,
            QueryResponse(expectedResult));

        PagedResult<PartySearchResult> result = await client.SearchPartiesAsync("none", 1, 20, CancellationToken.None);

        result.Freshness.ShouldNotBeNull();
        result.Freshness.Status.ShouldBe(ProjectionFreshnessStatus.Current);
        result.Freshness.WarningCodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListPartiesAsync_OmitsNullOptionalPayloadParametersAsync()
    {
        var emptyResult = new PagedResult<PartyIndexEntry>
        {
            Items = [],
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 1,
        };

        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler handler) = CreateClient(
            HttpStatusCode.OK,
            QueryResponse(emptyResult));

        await client.ListPartiesAsync(1, 10, null, null, null, null, null, null, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        JsonElement payload = body.RootElement.GetProperty("payload");
        payload.TryGetProperty("type", out _).ShouldBeFalse();
        payload.TryGetProperty("active", out _).ShouldBeFalse();
        payload.TryGetProperty("createdAfter", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ListPartiesAsync_WhenCancelled_PropagatesCancellationAsync()
    {
        var httpClient = new HttpClient(new CancellationHandler()) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesQueryClient(httpClient, Options.Create(ClientOptions()));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => client.ListPartiesAsync(1, 20, null, null, null, null, null, null, cts.Token));
    }

    [Fact]
    public async Task SearchPartiesAsync_SubmitsEventStoreSearchQueryAsync()
    {
        var expectedResult = new PagedResult<PartySearchResult>
        {
            Items =
            [
                new PartySearchResult
                {
                    Party = new PartyIndexEntry
                    {
                        Id = "p-42",
                        Type = PartyType.Organization,
                        IsActive = true,
                        DisplayName = "Acme Corp",
                        CreatedAt = DateTimeOffset.UtcNow,
                        LastModifiedAt = DateTimeOffset.UtcNow,
                    },
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
            TotalPages = 1,
        };

        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler handler) = CreateClient(
            HttpStatusCode.OK,
            QueryResponse(expectedResult));

        PagedResult<PartySearchResult> result = await client.SearchPartiesAsync(
            "acme",
            1,
            20,
            CancellationToken.None,
            mode: "Lexical",
            caseId: "case-42",
            requestCustomizer: (request, _) =>
            {
                request.Headers.Authorization = new("Bearer", "host-token");
                return ValueTask.CompletedTask;
            });

        result.Items.Count.ShouldBe(1);
        result.Items[0].Party.Id.ShouldBe("p-42");
        result.Items[0].Party.Type.ShouldBe(PartyType.Organization);
        result.Items[0].Matches.Count.ShouldBe(1);
        result.Items[0].Matches[0].MatchedField.ShouldBe("displayName");
        result.Items[0].Matches[0].MatchType.ShouldBe("prefix");

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        JsonElement root = body.RootElement;
        root.GetProperty("queryType").GetString().ShouldBe("PartySearch");
        root.GetProperty("projectionType").GetString().ShouldBe("party-index");
        root.GetProperty("projectionActorType").GetString().ShouldBe("PartyIndexProjectionQueryActor");
        root.GetProperty("entityId").GetString().ShouldBe("parties");
        JsonElement payload = root.GetProperty("payload");
        payload.GetProperty("query").GetString().ShouldBe("acme");
        payload.GetProperty("page").GetInt32().ShouldBe(1);
        payload.GetProperty("pageSize").GetInt32().ShouldBe(20);
        payload.GetProperty("mode").GetString().ShouldBe("Lexical");
        payload.GetProperty("caseId").GetString().ShouldBe("case-42");
        handler.LastRequest!.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.ShouldBe("host-token");
    }

    [Fact]
    public async Task SearchPartiesAsync_OmitsNullOptionalPayloadParametersAsync()
    {
        var emptyResult = new PagedResult<PartySearchResult>
        {
            Items = [],
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 1,
        };

        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler handler) = CreateClient(
            HttpStatusCode.OK,
            QueryResponse(emptyResult));

        await client.SearchPartiesAsync("acme", 1, 10, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        JsonElement payload = body.RootElement.GetProperty("payload");
        payload.GetProperty("query").GetString().ShouldBe("acme");
        payload.GetProperty("page").GetInt32().ShouldBe(1);
        payload.GetProperty("pageSize").GetInt32().ShouldBe(10);
        payload.TryGetProperty("mode", out _).ShouldBeFalse();
        payload.TryGetProperty("caseId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task SearchPartiesAsync_WhenCancelled_PropagatesCancellationAsync()
    {
        var httpClient = new HttpClient(new CancellationHandler()) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesQueryClient(httpClient, Options.Create(ClientOptions()));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => client.SearchPartiesAsync("acme", 1, 20, cts.Token));
    }

    [Fact]
    public async Task GetPartyAsync_OnNotFound_ThrowsPartiesClientExceptionAsync()
    {
        string problemJson = JsonSerializer.Serialize(new
        {
            status = 404,
            title = "Party Not Found",
            type = "urn:hexalith:eventstore:error:not-found",
            detail = "No party found with ID 'p-missing'.",
            correlationId = "corr-q-err",
        });

        (HttpPartiesQueryClient client, _) = CreateClient(
            HttpStatusCode.NotFound,
            problemJson,
            "application/problem+json");

        PartiesClientException exception = await Should.ThrowAsync<PartiesClientException>(
            () => client.GetPartyAsync("p-missing", CancellationToken.None));

        exception.Status.ShouldBe(404);
        exception.Title.ShouldBe("Party Not Found");
        exception.CorrelationId.ShouldBe("corr-q-err");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, "urn:hexalith:eventstore:error:validation")]
    [InlineData(HttpStatusCode.Unauthorized, 401, "urn:hexalith:eventstore:error:unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, 403, "urn:hexalith:eventstore:error:forbidden")]
    [InlineData(HttpStatusCode.Conflict, 409, "urn:hexalith:eventstore:error:conflict")]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503, "urn:hexalith:eventstore:error:degraded")]
    public async Task QueryErrorResponses_MapToPartiesClientExceptionAsync(
        HttpStatusCode httpStatusCode,
        int expectedStatus,
        string expectedType)
    {
        string problemJson = JsonSerializer.Serialize(new
        {
            status = expectedStatus,
            title = "EventStore query failed",
            type = expectedType,
            detail = "Safe query failure detail.",
            correlationId = "corr-query-error",
        });

        (HttpPartiesQueryClient client, _) = CreateClient(
            httpStatusCode,
            problemJson,
            "application/problem+json");

        PartiesClientException exception = await Should.ThrowAsync<PartiesClientException>(
            () => client.GetPartyAsync("p-error", CancellationToken.None));

        exception.Status.ShouldBe(expectedStatus);
        exception.Type.ShouldBe(expectedType);
        exception.Detail.ShouldBe("Safe query failure detail.");
        exception.CorrelationId.ShouldBe("corr-query-error");
    }

    [Fact]
    public async Task QueryMalformedJsonResponse_ThrowsTypedClientExceptionAsync()
    {
        (HttpPartiesQueryClient client, _) = CreateClient(
            HttpStatusCode.OK,
            "{",
            "application/json");

        PartiesClientException exception = await Should.ThrowAsync<PartiesClientException>(
            () => client.GetPartyAsync("p-malformed", CancellationToken.None));

        exception.Status.ShouldBe(200);
        exception.Detail.ShouldBe("Response did not contain a valid query payload.");
    }

    [Fact]
    public async Task QuerySuccessWithoutPayload_ThrowsTypedClientExceptionAsync()
    {
        (HttpPartiesQueryClient client, _) = CreateClient(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new { correlationId = "corr-empty" }));

        PartiesClientException exception = await Should.ThrowAsync<PartiesClientException>(
            () => client.GetPartyAsync("p-empty", CancellationToken.None));

        exception.Status.ShouldBe(200);
        exception.Detail.ShouldBe("Response did not contain a valid query payload.");
        exception.CorrelationId.ShouldBe("corr-empty");
    }

    [Fact]
    public async Task GetPartyAsync_WhenCancelled_PropagatesCancellationAsync()
    {
        var httpClient = new HttpClient(new CancellationHandler()) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesQueryClient(httpClient, Options.Create(ClientOptions()));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => client.GetPartyAsync("p-cancel", cts.Token));
    }

    private static (HttpPartiesQueryClient Client, HttpPartiesCommandClientTests.MockHandler Handler) CreateClient(
        HttpStatusCode statusCode,
        string responseBody,
        string contentType = "application/json")
    {
        var handler = new HttpPartiesCommandClientTests.MockHandler(statusCode, responseBody, contentType);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        return (new HttpPartiesQueryClient(httpClient, Options.Create(ClientOptions())), handler);
    }

    private static string QueryResponse<T>(T payload)
        => JsonSerializer.Serialize(
            new
            {
                correlationId = "corr-query",
                payload,
            },
            _jsonOptions);

    private static PartiesClientOptions ClientOptions()
        => new()
        {
            BaseUrl = "https://localhost",
            Tenant = "tenant-a",
        };

    private sealed class CancellationHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
