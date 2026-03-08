namespace Hexalith.Parties.Contracts.Commands;

public sealed record RotatePartyKey
{
    public required string PartyId { get; init; }

    public required int NewKeyVersion { get; init; }

    public required int PreviousKeyVersion { get; init; }
}
