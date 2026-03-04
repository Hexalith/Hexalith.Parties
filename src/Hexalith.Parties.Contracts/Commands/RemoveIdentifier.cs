namespace Hexalith.Parties.Contracts.Commands;

public sealed record RemoveIdentifier
{
    public required string PartyId { get; init; }

    public required string IdentifierId { get; init; }
}
