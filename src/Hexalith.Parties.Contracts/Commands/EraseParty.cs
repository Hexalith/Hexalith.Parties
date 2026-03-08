namespace Hexalith.Parties.Contracts.Commands;

public sealed record EraseParty
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }
}
