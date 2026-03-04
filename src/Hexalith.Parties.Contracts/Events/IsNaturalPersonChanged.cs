using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record IsNaturalPersonChanged : IEventPayload
{
    public required bool IsNaturalPerson { get; init; }
}
