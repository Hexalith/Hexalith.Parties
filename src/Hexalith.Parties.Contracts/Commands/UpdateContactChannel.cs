using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

public sealed record UpdateContactChannel
{
    public required string PartyId { get; init; }

    public required string ContactChannelId { get; init; }

    public ContactChannelType? Type { get; init; }

    [PersonalData]
    public string? Value { get; init; }

    public bool? IsPreferred { get; init; }
}
