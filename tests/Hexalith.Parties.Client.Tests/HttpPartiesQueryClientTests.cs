using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

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
    public async Task GetPartyAsync_SendsGetToCorrectEndpoint_ReturnsPartyDetailAsync()
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
            JsonSerializer.Serialize(expectedDetail, _jsonOptions));

        PartyDetail result = await client.GetPartyAsync("p-1", CancellationToken.None);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("p-1");
        result.DisplayName.ShouldBe("John Doe");
        result.Type.ShouldBe(PartyType.Person);
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-1");
    }

    [Fact]
    public async Task ListPartiesAsync_SendsGetWithQueryParameters_ReturnsPagedResultAsync()
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
            JsonSerializer.Serialize(expectedResult, _jsonOptions));

        PagedResult<PartyIndexEntry> result = await client.ListPartiesAsync(
            1, 20, PartyType.Person, true, null, null, null, null, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.Items[0].Id.ShouldBe("p-1");

        string query = handler.LastRequest!.RequestUri!.Query;
        query.ShouldContain("page=1");
        query.ShouldContain("pageSize=20");
        query.ShouldContain("type=Person");
        query.ShouldContain("active=true");
    }

    [Fact]
    public async Task ListPartiesAsync_OmitsNullOptionalParametersAsync()
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
            JsonSerializer.Serialize(emptyResult, _jsonOptions));

        await client.ListPartiesAsync(1, 10, null, null, null, null, null, null, CancellationToken.None);

        string query = handler.LastRequest!.RequestUri!.Query;
        query.ShouldNotContain("type=");
        query.ShouldNotContain("active=");
        query.ShouldNotContain("createdAfter=");
    }

    [Fact]
    public async Task ListPartiesAsync_SerializesDateFiltersUsingIso8601Async()
    {
        var emptyResult = new PagedResult<PartyIndexEntry>
        {
            Items = [],
            Page = 2,
            PageSize = 5,
            TotalCount = 0,
            TotalPages = 1,
        };
        DateTimeOffset createdAfter = new(2026, 03, 01, 10, 11, 12, TimeSpan.Zero);
        DateTimeOffset modifiedBefore = new(2026, 03, 06, 8, 9, 10, TimeSpan.FromHours(1));

        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler handler) = CreateClient(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(emptyResult, _jsonOptions));

        await client.ListPartiesAsync(
            2,
            5,
            null,
            null,
            createdAfter,
            null,
            null,
            modifiedBefore,
            CancellationToken.None);

        string query = handler.LastRequest!.RequestUri!.Query;
        query.ShouldContain($"createdAfter={Uri.EscapeDataString(createdAfter.ToString("o"))}");
        query.ShouldContain($"modifiedBefore={Uri.EscapeDataString(modifiedBefore.ToString("o"))}");
    }

    [Fact]
    public async Task SearchPartiesAsync_SendsGetWithQueryStringAsync()
    {
        var expectedResult = new PagedResult<PartySearchResult>
        {
            Items = [],
            Page = 1,
            PageSize = 20,
            TotalCount = 0,
            TotalPages = 1,
        };

        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler handler) = CreateClient(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(expectedResult, _jsonOptions));

        await client.SearchPartiesAsync("john", 1, 20, CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldContain("/api/v1/parties/search");
        handler.LastRequest.RequestUri!.Query.ShouldContain("q=john");
        handler.LastRequest.RequestUri.Query.ShouldContain("page=1");
        handler.LastRequest.RequestUri.Query.ShouldContain("pageSize=20");
    }

    [Fact]
    public async Task SearchPartiesAsync_DeserializesTypedSearchResultsAsync()
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

        (HttpPartiesQueryClient client, _) = CreateClient(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(expectedResult, _jsonOptions));

        PagedResult<PartySearchResult> result = await client.SearchPartiesAsync("acme", 1, 20, CancellationToken.None);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Party.Id.ShouldBe("p-42");
        result.Items[0].Party.Type.ShouldBe(PartyType.Organization);
        result.Items[0].Matches.Count.ShouldBe(1);
        result.Items[0].Matches[0].MatchedField.ShouldBe("displayName");
        result.Items[0].Matches[0].MatchType.ShouldBe("prefix");
    }

    [Fact]
    public async Task GetPartyAsync_OnNotFound_ThrowsPartiesClientExceptionAsync()
    {
        string problemJson = JsonSerializer.Serialize(new
        {
            status = 404,
            title = "Party Not Found",
            type = "urn:hexalith:parties:error:PartyNotFound",
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

    private static (HttpPartiesQueryClient Client, HttpPartiesCommandClientTests.MockHandler Handler) CreateClient(
        HttpStatusCode statusCode,
        string responseBody,
        string contentType = "application/json")
    {
        var handler = new HttpPartiesCommandClientTests.MockHandler(statusCode, responseBody, contentType);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        return (new HttpPartiesQueryClient(httpClient), handler);
    }
}
