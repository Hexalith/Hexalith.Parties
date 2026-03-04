using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyDisplayNameDerived : IEventPayload
{
    public required string DisplayName { get; init; }

    public required string SortName { get; init; }
}
