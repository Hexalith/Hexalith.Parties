using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Models;

/// <summary>
/// Reserved future temporal-name result shape.
/// </summary>
/// <remarks>
/// This type is reserved for future compatibility and is not available through MVP query,
/// REST, MCP, client, admin, or picker surfaces.
/// </remarks>
public sealed record TemporalNameResult
{
    /// <summary>
    /// Identifier of the party the resolved name belongs to. Reserved for future temporal
    /// queries; MVP exposes no public surface that returns this type.
    /// </summary>
    public required string PartyId { get; init; }

    /// <summary>
    /// Logical instant the name was resolved at. Reserved for future temporal queries; MVP
    /// does not expose an as-of query path that returns this type.
    /// </summary>
    public required DateTimeOffset AsOf { get; init; }

    /// <summary>
    /// Display name as of <see cref="AsOf"/>. Personal data. Reserved for future temporal
    /// queries; MVP must not return this through query, REST, MCP, client, or UI surfaces.
    /// </summary>
    [PersonalData]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Sort-name collation as of <see cref="AsOf"/>. Personal data. Reserved for future
    /// temporal queries; not available in MVP.
    /// </summary>
    [PersonalData]
    public required string SortName { get; init; }
}
