using Hexalith.Parties.Contracts.Models;

using System.Net;

namespace Hexalith.Parties.Picker.Services;

public sealed record PartyPickerSearchResponse
{
    public required PartyPickerSearchState State { get; init; }

    public IReadOnlyList<PartySearchResult> Results { get; init; } = [];

    public PartyPickerSearchMetadata Metadata { get; init; } = new();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public string? SafeReason { get; init; }

    public HttpStatusCode? StatusCode { get; init; }

    public bool HasResults => Results.Count > 0;

    public int VisibleCount => Results.Count;

    public bool HasReliableTotalCount => TotalCount >= VisibleCount;
}
