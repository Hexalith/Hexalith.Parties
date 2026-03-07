namespace Hexalith.Parties.Contracts.Security;

public sealed record ErasureCertificate
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required IReadOnlyList<int> KeyVersionsDestroyed { get; init; }

    public required ErasureVerificationStatus VerificationStatus { get; init; }
}
