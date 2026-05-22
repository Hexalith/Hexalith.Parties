namespace Hexalith.Parties.Contracts.Models;

/// <summary>
/// Describes why a party search result matched.
/// </summary>
/// <remarks>
/// In MVP, active match evidence is limited to <c>displayName</c>. Field names such as
/// <c>email</c>, <c>identifier</c>, <c>semantic</c>, <c>graph</c>, and <c>hybrid</c> are
/// reserved for future compatibility and are not available in MVP search results.
/// </remarks>
public sealed record MatchMetadata
{
    /// <summary>
    /// The matched field. MVP responses actively emit only <c>displayName</c>.
    /// </summary>
    public required string MatchedField { get; init; }

    /// <summary>
    /// The bounded match type for display-name matching, such as <c>exact</c>, <c>prefix</c>,
    /// <c>contains</c>, or display-name-only <c>fuzzy</c>.
    /// </summary>
    public required string MatchType { get; init; }

    /// <summary>
    /// Optional score metadata. MVP display-name search must not use this to imply semantic,
    /// graph, hybrid, memory, provider, or temporal provenance.
    /// </summary>
    public double? Score { get; init; }
}
