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
        root.GetProperty("projectionType").GetString().ShouldBe("PartyDetail");
        root.GetProperty("queryType").GetString().ShouldBe("GetParty");
        root.GetProperty("projectionActorType").GetString().ShouldBe("PartyDetailProjectionActor");
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
        root.GetProperty("projectionType").GetString().ShouldBe("PartyIndex");
        root.GetProperty("queryType").GetString().ShouldBe("ListParties");
        root.TryGetProperty("projectionActorType", out _).ShouldBeFalse();

        JsonElement payload = root.GetProperty("payload");
        payload.GetProperty("page").GetInt32().ShouldBe(1);
        payload.GetProperty("pageSize").GetInt32().ShouldBe(20);
        payload.GetProperty("type").GetString().ShouldBe("Person");
        payload.GetProperty("active").GetBoolean().ShouldBeTrue();
        payload.GetProperty("createdAfter").GetString().ShouldBe("2026-03-01T10:11:12.0000000+00:00");
        payload.GetProperty("modifiedBefore").GetString().ShouldBe("2026-03-06T08:09:10.0000000+01:00");
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

        PagedResult<PartySearchResult> result = await client.SearchPartiesAsync("acme", 1, 20, CancellationToken.None);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Party.Id.ShouldBe("p-42");
        result.Items[0].Matches[0].MatchedField.ShouldBe("displayName");

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        JsonElement root = body.RootElement;
        root.GetProperty("projectionType").GetString().ShouldBe("PartySearch");
        root.GetProperty("queryType").GetString().ShouldBe("SearchParties");
        root.GetProperty("payload").GetProperty("query").GetString().ShouldBe("acme");
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
}
