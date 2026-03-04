using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

public sealed record UpdatePartyComposite
{
    public required string PartyId { get; init; }

    public PersonDetails? PersonDetails { get; init; }

    public OrganizationDetails? OrganizationDetails { get; init; }

    public IReadOnlyList<AddContactChannel> AddContactChannels { get; init; } = [];

    public IReadOnlyList<UpdateContactChannel> UpdateContactChannels { get; init; } = [];

    public IReadOnlyList<string> RemoveContactChannelIds { get; init; } = [];

    public IReadOnlyList<AddIdentifier> AddIdentifiers { get; init; } = [];

    public IReadOnlyList<string> RemoveIdentifierIds { get; init; } = [];
}
