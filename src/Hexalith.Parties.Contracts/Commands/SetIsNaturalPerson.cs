namespace Hexalith.Parties.Contracts.Commands;

public sealed record SetIsNaturalPerson
{
    public required string PartyId { get; init; }

    public required bool IsNaturalPerson { get; init; }
}
