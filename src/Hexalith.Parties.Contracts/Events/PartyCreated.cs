using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyCreated : IEventPayload
{
    /// <summary>Gets the recorded creation instant; legacy events use a deterministic epoch.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UnixEpoch;

    public required PartyType Type { get; init; }

    public PersonDetails? PersonDetails { get; init; }

    public OrganizationDetails? OrganizationDetails { get; init; }
}
