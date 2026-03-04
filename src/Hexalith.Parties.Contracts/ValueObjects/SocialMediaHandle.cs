namespace Hexalith.Parties.Contracts.ValueObjects;

public sealed record SocialMediaHandle
{
    public required string Platform { get; init; }

    [PersonalData]
    public required string Handle { get; init; }
}
