using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyErasureCancelled : IEventPayload
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset CancelledAt { get; init; }

    public required string CancelledBy { get; init; }
}
