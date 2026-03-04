namespace Hexalith.Parties.Contracts.ValueObjects;

public sealed record PartyIdentifier
{
    public required string Id { get; init; }

    public required IdentifierType Type { get; init; }

    [PersonalData]
    public required string Value { get; init; }

    public string? Jurisdiction { get; init; }
}
