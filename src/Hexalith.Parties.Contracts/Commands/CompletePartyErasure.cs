namespace Hexalith.Parties.Contracts.Commands;

public sealed record CompletePartyErasure {
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset ErasedAt { get; init; }
}
