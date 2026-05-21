using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

public sealed record ConsentRevoked : IEventPayload
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required string ConsentId { get; init; }

    public required DateTimeOffset RevokedAt { get; init; }

    public required string RevokedBy { get; init; }

    public string? Reason { get; init; }

    public string Source { get; init; } = "unspecified";
}
