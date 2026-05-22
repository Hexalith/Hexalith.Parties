using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyErased : IEventPayload
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset ErasedAt { get; init; }

    public string ErasureStatus { get; init; } = "Erased";

    public string VerificationStatus { get; init; } = "Complete";
}
