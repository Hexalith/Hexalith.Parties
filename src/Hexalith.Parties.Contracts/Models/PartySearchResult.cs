namespace Hexalith.Parties.Contracts.Models;

public sealed record PartySearchResult
{
    public required PartyIndexEntry Party { get; init; }

    public required IReadOnlyList<MatchMetadata> Matches { get; init; }

    public double RelevanceScore { get; init; }
}
