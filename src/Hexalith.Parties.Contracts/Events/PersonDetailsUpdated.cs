using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PersonDetailsUpdated : IEventPayload
{
    public required PersonDetails PersonDetails { get; init; }
}
