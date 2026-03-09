namespace Hexalith.Parties.Contracts.Security;

public sealed record PartyErasureStatusRecord {
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? ErasedAt { get; init; }

    public string? ErrorMessage { get; init; }
}
