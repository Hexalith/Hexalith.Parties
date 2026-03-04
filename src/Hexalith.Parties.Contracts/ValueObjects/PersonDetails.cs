namespace Hexalith.Parties.Contracts.ValueObjects;

public sealed record PersonDetails
{
    [PersonalData]
    public required string FirstName { get; init; }

    [PersonalData]
    public required string LastName { get; init; }

    [PersonalData]
    public DateTimeOffset? DateOfBirth { get; init; }

    [PersonalData]
    public string? Prefix { get; init; }

    [PersonalData]
    public string? Suffix { get; init; }
}
