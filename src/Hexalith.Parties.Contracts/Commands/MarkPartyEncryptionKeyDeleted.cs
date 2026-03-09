namespace Hexalith.Parties.Contracts.Commands;

public sealed record MarkPartyEncryptionKeyDeleted {
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}
