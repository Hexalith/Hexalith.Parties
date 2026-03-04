namespace Hexalith.Parties.Contracts.ValueObjects;

public sealed record ContactChannel
{
    public required string Id { get; init; }

    public required ContactChannelType Type { get; init; }

    [PersonalData]
    public required string Value { get; init; }

    public bool IsPreferred { get; init; }
}
