using System.Net;
using System.Net.Http.Json;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Picker.Tests.Services;

internal static class PartyPickerTestData
{
    public static HttpResponseMessage SearchResponse(
        params PartySearchResult[] results)
        => SearchResponse(null, null, results);

    public static HttpResponseMessage SearchResponse(
        string? searchStatus,
        string? degradedReason,
        params PartySearchResult[] results)
    {
        var message = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PagedResult<PartySearchResult>
            {
                Items = results,
                Page = 1,
                PageSize = 10,
                TotalCount = results.Length,
                TotalPages = results.Length == 0 ? 0 : 1,
            }),
        };

        if (searchStatus is not null)
        {
            message.Headers.Add("X-Parties-Search-Status", searchStatus);
        }

        if (degradedReason is not null)
        {
            message.Headers.Add("X-Parties-Search-Degraded-Reason", degradedReason);
        }

        return message;
    }

    public static HttpResponseMessage Failure(HttpStatusCode statusCode)
        => new(statusCode);

    public static PartySearchResult Result(
        string id = "party-1",
        string name = "Ada Lovelace",
        PartyType type = PartyType.Person,
        bool active = true,
        bool erased = false)
        => new()
        {
            Party = new PartyIndexEntry
            {
                Id = id,
                DisplayName = name,
                Type = type,
                IsActive = active,
                IsErased = erased,
                CreatedAt = DateTimeOffset.Parse("2026-05-05T00:00:00Z"),
                LastModifiedAt = DateTimeOffset.Parse("2026-05-05T00:00:00Z"),
            },
            Matches = [],
            RelevanceScore = 0.95,
        };
}
