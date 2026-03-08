using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record ErasePartyRequested : IEventPayload
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }

    public required string RequestedBy { get; init; }
}
