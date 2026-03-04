using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Events;

public sealed record ContactChannelUpdated : IEventPayload
{
    public required string ContactChannelId { get; init; }

    public ContactChannelType? Type { get; init; }

    [PersonalData]
    public string? Value { get; init; }

    public bool? IsPreferred { get; init; }
}
