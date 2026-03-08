namespace Hexalith.Parties.Security;

public sealed record CryptoPendingRecord
{
    public required string TenantId { get; init; }

    public required string PartyId { get; init; }

    public required string LastError { get; init; }

    public required DateTimeOffset FirstMarkedAt { get; init; }

    public required DateTimeOffset LastAttemptedAt { get; init; }

    public required int AttemptCount { get; init; }
}
