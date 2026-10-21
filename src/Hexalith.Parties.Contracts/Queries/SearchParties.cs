using System.ComponentModel;
using System.Text.Json.Serialization;

using Hexalith.McpCli.Abstractions;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Queries;

/// <summary>Search visible display names in the envelope tenant using trimmed, case-insensitive, diacritic-normalized exact, prefix, contains and fuzzy matching. Type and Active apply before paging; totals exclude erased parties. Empty query returns no matches. Date filters and advanced search modes reject. Use envelope paging: default size 20 and offset 0, maximum size 100, page-aligned offsets or a scoped continuation cursor. To continue, supply the returned paging.nextCursor without offset, keeping the same query, filters, envelope tenant, user and page size.</summary>
[HexalithQuery("Search visible display names in the envelope tenant using trimmed, case-insensitive, diacritic-normalized exact, prefix, contains and fuzzy matching. Type and Active apply before paging; totals exclude erased parties. Empty query returns no matches. Date filters and advanced search modes reject. Use envelope paging: default size 20 and offset 0, maximum size 100, page-aligned offsets or a scoped continuation cursor. To continue, supply the returned paging.nextCursor without offset, keeping the same query, filters, envelope tenant, user and page size.",
    Domain = "party", WireType = "PartySearch", AggregateId = "parties",
    ProjectionType = "party-index", ProjectionActorType = "PartyIndexProjectionQueryActor",
    Example = """{"Query":"Ada","Type":"Person","Active":true}""")]
public sealed record SearchParties
{
    /// <summary>Display-name query, at most 512 UTF-16 units including whitespace before trimming. Oversized input rejects; empty or whitespace returns no matches. Contact channels and business identifiers are not searched.</summary>
    [Description("Display-name query, at most 512 UTF-16 units including whitespace before trimming. Oversized input rejects; empty or whitespace returns no matches. Contact channels and business identifiers are not searched.")]
    public required string Query { get; init; }

    /// <summary>Optional party kind: Person or Organization; Unknown and other values reject. Applied before paging.</summary>
    [Description("Optional party kind: Person or Organization; Unknown and other values reject. Applied before paging.")]
    [JsonConverter(typeof(JsonStringEnumConverter<PartyType>))]
    public PartyType? Type { get; init; }

    /// <summary>Optional active-status filter, applied before paging; omitted includes active and inactive parties.</summary>
    [Description("Optional active-status filter, applied before paging; omitted includes active and inactive parties.")]
    public bool? Active { get; init; }
}
