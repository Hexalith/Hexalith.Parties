using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyProcessingRestricted : IRejectionEvent {
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public string? Message { get; init; }
}
