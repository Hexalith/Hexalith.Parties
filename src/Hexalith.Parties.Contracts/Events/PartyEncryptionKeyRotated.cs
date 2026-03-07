using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyEncryptionKeyRotated : IEventPayload
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required int NewKeyVersion { get; init; }

    public required int PreviousKeyVersion { get; init; }

    public required DateTimeOffset RotatedAt { get; init; }
}
