using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PreferredContactChannelChanged : IEventPayload
{
    public required string ContactChannelId { get; init; }
}
