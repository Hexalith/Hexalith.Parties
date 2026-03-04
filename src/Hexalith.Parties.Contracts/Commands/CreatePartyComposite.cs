using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

public sealed record CreatePartyComposite
{
    public required string PartyId { get; init; }

    public required PartyType Type { get; init; }

    public PersonDetails? PersonDetails { get; init; }

    public OrganizationDetails? OrganizationDetails { get; init; }

    public IReadOnlyList<AddContactChannel> ContactChannels { get; init; } = [];

    public IReadOnlyList<AddIdentifier> Identifiers { get; init; } = [];
}
