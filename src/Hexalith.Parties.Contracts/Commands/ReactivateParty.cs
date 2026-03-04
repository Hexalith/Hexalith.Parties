namespace Hexalith.Parties.Contracts.Commands;

public sealed record ReactivateParty
{
    public required string PartyId { get; init; }
}
