using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Events;

public sealed record OrganizationDetailsUpdated : IEventPayload
{
    public required OrganizationDetails OrganizationDetails { get; init; }
}
