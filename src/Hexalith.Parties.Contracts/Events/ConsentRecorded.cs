using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Contracts.Events;

public sealed record ConsentRecorded : IEventPayload
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required string ConsentId { get; init; }

    public required string ChannelId { get; init; }

    public required string Purpose { get; init; }

    public required LawfulBasis LawfulBasis { get; init; }

    public required DateTimeOffset GrantedAt { get; init; }

    public required string GrantedBy { get; init; }

    public string Source { get; init; } = "unspecified";
}
