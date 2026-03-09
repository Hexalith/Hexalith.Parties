using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record RestrictionLifted : IEventPayload
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset LiftedAt { get; init; }
}
