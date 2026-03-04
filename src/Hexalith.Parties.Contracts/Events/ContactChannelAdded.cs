using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Events;

public sealed record ContactChannelAdded : IEventPayload
{
    public required string ContactChannelId { get; init; }

    public required ContactChannelType Type { get; init; }

    [PersonalData]
    public required string Value { get; init; }

    public bool IsPreferred { get; init; }
}
