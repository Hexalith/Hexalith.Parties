using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyNotFound : IRejectionEvent
{
    public string? Message { get; init; }
}
