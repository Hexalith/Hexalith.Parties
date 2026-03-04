using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

/// <summary>
/// v2 forward-compatibility placeholder (FR37) — additive fields only when activated.
/// </summary>
public sealed record PartyMerged : IEventPayload
{
    public required string SurvivorPartyId { get; init; }

    public required string MergedPartyId { get; init; }
}
