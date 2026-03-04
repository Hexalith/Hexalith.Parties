using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record ContactChannelRemoved : IEventPayload
{
    public required string ContactChannelId { get; init; }
}
