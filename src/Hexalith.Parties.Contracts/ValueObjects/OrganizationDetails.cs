namespace Hexalith.Parties.Contracts.ValueObjects;

public sealed record OrganizationDetails
{
    public required string LegalName { get; init; }

    public string? TradingName { get; init; }

    public string? LegalForm { get; init; }

    public string? RegistrationNumber { get; init; }

    public bool IsNaturalPerson { get; init; }
}
