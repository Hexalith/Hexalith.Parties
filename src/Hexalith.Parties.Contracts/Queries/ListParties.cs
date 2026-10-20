using System.ComponentModel;
using System.Text.Json.Serialization;

using Hexalith.McpCli.Abstractions;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Queries;

/// <summary>List visible parties in the envelope tenant, ordered by normalized display name then PartyId. Filters apply before paging and totals exclude erased parties. Use envelope paging: default size 20 and offset 0, maximum size 100, page-aligned offsets or a scoped continuation cursor. To continue, supply the returned paging.nextCursor without offset, keeping the same query, filters, envelope tenant, user and page size.</summary>
[HexalithQuery("List visible parties in the envelope tenant, ordered by normalized display name then PartyId. Filters apply before paging and totals exclude erased parties. Use envelope paging: default size 20 and offset 0, maximum size 100, page-aligned offsets or a scoped continuation cursor. To continue, supply the returned paging.nextCursor without offset, keeping the same query, filters, envelope tenant, user and page size.",
    Domain = "party", WireType = "PartyIndex", AggregateId = "parties",
    ProjectionType = "party-index", ProjectionActorType = "PartyIndexProjectionQueryActor",
    Example = """{"Type":"Person","Active":true,"CreatedAfter":"2026-01-01T00:00:00Z"}""")]
public sealed record ListParties
{
    /// <summary>Optional party kind: Person or Organization; Unknown and other values reject. Applied before paging.</summary>
    [Description("Optional party kind: Person or Organization; Unknown and other values reject. Applied before paging.")]
    [JsonConverter(typeof(JsonStringEnumConverter<PartyType>))]
    public PartyType? Type { get; init; }

    /// <summary>Optional active-status filter, applied before paging; omitted includes active and inactive parties.</summary>
    [Description("Optional active-status filter, applied before paging; omitted includes active and inactive parties.")]
    public bool? Active { get; init; }

    /// <summary>Inclusive earliest creation instant, with an explicit UTC or numeric offset; omitted leaves this bound open. Each lower bound must not exceed its upper bound.</summary>
    [Description("Inclusive earliest creation instant, with an explicit UTC or numeric offset; omitted leaves this bound open. Each lower bound must not exceed its upper bound.")]
    public DateTimeOffset? CreatedAfter { get; init; }

    /// <summary>Inclusive latest creation instant, with an explicit UTC or numeric offset; omitted leaves this bound open. Each lower bound must not exceed its upper bound.</summary>
    [Description("Inclusive latest creation instant, with an explicit UTC or numeric offset; omitted leaves this bound open. Each lower bound must not exceed its upper bound.")]
    public DateTimeOffset? CreatedBefore { get; init; }

    /// <summary>Inclusive earliest modification instant, with an explicit UTC or numeric offset; omitted leaves this bound open. Each lower bound must not exceed its upper bound.</summary>
    [Description("Inclusive earliest modification instant, with an explicit UTC or numeric offset; omitted leaves this bound open. Each lower bound must not exceed its upper bound.")]
    public DateTimeOffset? ModifiedAfter { get; init; }

    /// <summary>Inclusive latest modification instant, with an explicit UTC or numeric offset; omitted leaves this bound open. Each lower bound must not exceed its upper bound.</summary>
    [Description("Inclusive latest modification instant, with an explicit UTC or numeric offset; omitted leaves this bound open. Each lower bound must not exceed its upper bound.")]
    public DateTimeOffset? ModifiedBefore { get; init; }
}
