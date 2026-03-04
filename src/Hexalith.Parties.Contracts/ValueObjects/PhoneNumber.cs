namespace Hexalith.Parties.Contracts.ValueObjects;

public sealed record PhoneNumber
{
    [PersonalData]
    public required string Number { get; init; }

    public string? CountryCode { get; init; }
}
