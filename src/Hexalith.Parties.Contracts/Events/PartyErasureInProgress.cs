using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyErasureInProgress : IRejectionEvent
{
    public string? PartyId { get; init; }

    public string? TenantId { get; init; }

    public string Status { get; init; } = "ErasureInProgress";

    public string? Message { get; init; }
}
