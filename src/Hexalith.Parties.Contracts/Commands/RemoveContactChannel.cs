namespace Hexalith.Parties.Contracts.Commands;

public sealed record RemoveContactChannel
{
    public required string PartyId { get; init; }

    public required string ContactChannelId { get; init; }
}
