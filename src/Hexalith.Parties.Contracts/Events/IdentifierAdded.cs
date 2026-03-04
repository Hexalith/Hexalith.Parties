using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Events;

public sealed record IdentifierAdded : IEventPayload
{
    public required string IdentifierId { get; init; }

    public required IdentifierType Type { get; init; }

    [PersonalData]
    public required string Value { get; init; }
}
