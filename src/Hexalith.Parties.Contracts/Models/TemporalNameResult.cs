namespace Hexalith.Parties.Contracts.Models;

public sealed record TemporalNameResult
{
    public required string PartyId { get; init; }

    public required DateTimeOffset AsOf { get; init; }

    public required string DisplayName { get; init; }

    public required string SortName { get; init; }
}
