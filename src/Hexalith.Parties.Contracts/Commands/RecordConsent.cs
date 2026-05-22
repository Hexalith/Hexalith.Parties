using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Contracts.Commands;

public sealed record RecordConsent {
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required string ChannelId { get; init; }

    public required string Purpose { get; init; }

    public required LawfulBasis LawfulBasis { get; init; }

    public string? ActorUserId { get; init; }

    public string? Source { get; init; }
}
