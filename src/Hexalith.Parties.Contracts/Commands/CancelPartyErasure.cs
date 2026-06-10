namespace Hexalith.Parties.Contracts.Commands;

public sealed record CancelPartyErasure
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }
}
