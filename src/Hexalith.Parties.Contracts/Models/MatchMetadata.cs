namespace Hexalith.Parties.Contracts.Models;

public sealed record MatchMetadata
{
    public required string MatchedField { get; init; }

    public required string MatchType { get; init; }

    public double? Score { get; init; }
}
