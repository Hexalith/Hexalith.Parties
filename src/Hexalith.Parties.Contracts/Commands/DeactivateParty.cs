namespace Hexalith.Parties.Contracts.Commands;

public sealed record DeactivateParty
{
    public required string PartyId { get; init; }
}
