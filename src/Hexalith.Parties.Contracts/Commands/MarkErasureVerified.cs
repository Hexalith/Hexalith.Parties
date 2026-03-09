namespace Hexalith.Parties.Contracts.Commands;

public sealed record MarkErasureVerified {
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset VerifiedAt { get; init; }

    public required string VerificationReportId { get; init; }
}
