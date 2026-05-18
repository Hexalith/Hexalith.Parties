namespace Hexalith.Parties.Contracts.ValueObjects;

public sealed record NameHistoryEntry
{
    [PersonalData]
    public required string DisplayName { get; init; }

    [PersonalData]
    public required string SortName { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    public string? TriggeredBy { get; init; }
}
