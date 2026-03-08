namespace Hexalith.Parties.Contracts.Security;

public sealed record ErasureVerificationReport
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required IReadOnlyList<ErasureVerificationStoreResult> StoreResults { get; init; }

    public required ErasureVerificationOverallStatus OverallStatus { get; init; }
}
