using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record ProcessingRestricted : IEventPayload
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset RestrictedAt { get; init; }

    public string? Reason { get; init; }
}
