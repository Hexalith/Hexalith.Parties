using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

public sealed record AddContactChannel
{
    public required string PartyId { get; init; }

    public required string ContactChannelId { get; init; }

    public required ContactChannelType Type { get; init; }

    [PersonalData]
    public required string Value { get; init; }

    public bool IsPreferred { get; init; }
}
