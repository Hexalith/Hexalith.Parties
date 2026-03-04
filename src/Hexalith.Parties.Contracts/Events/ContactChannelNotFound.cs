using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record ContactChannelNotFound : IRejectionEvent
{
    public string? Message { get; init; }
}
