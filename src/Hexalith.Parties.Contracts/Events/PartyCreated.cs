using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyCreated : IEventPayload
{
    public required PartyType Type { get; init; }

    public PersonDetails? PersonDetails { get; init; }

    public OrganizationDetails? OrganizationDetails { get; init; }
}
