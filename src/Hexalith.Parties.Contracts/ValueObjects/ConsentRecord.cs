using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Contracts.ValueObjects;

public sealed record ConsentRecord
{
    public required string ConsentId { get; init; }

    public required string ChannelId { get; init; }

    public required string Purpose { get; init; }

    public required LawfulBasis LawfulBasis { get; init; }

    public required DateTimeOffset GrantedAt { get; init; }

    public required string GrantedBy { get; init; }

    public string Source { get; init; } = "unspecified";

    public DateTimeOffset? RevokedAt { get; init; }

    public string? RevokedBy { get; init; }

    public string? RevocationReason { get; init; }

    public string? RevocationSource { get; init; }

    public bool IsActive => RevokedAt is null;
}
