using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyDisplayNameDerived : IEventPayload
{
    [PersonalData]
    public required string DisplayName { get; init; }

    [PersonalData]
    public required string SortName { get; init; }
}
