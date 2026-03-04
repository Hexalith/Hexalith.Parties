namespace Hexalith.Parties.Contracts.ValueObjects;

public sealed record PostalAddress
{
    [PersonalData]
    public string? Street { get; init; }

    [PersonalData]
    public string? City { get; init; }

    [PersonalData]
    public string? Region { get; init; }

    [PersonalData]
    public string? PostalCode { get; init; }

    [PersonalData]
    public string? Country { get; init; }
}
