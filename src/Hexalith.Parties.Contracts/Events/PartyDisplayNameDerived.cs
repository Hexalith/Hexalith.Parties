using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyDisplayNameDerived : IEventPayload
{
    [PersonalData]
    public required string DisplayName { get; init; }

    // Additive in story 2.2 — defaults to empty so historical events without a
    // SortName property still deserialize. Projection handler preserves the
    // prior SortName when an event carries an empty value.
    [PersonalData]
    public string SortName { get; init; } = string.Empty;
}
