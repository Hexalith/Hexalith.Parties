using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record PartyEncryptionKeyCreated : IEventPayload
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required int KeyVersion { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
