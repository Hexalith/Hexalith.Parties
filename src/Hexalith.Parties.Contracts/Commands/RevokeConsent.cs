namespace Hexalith.Parties.Contracts.Commands;

public sealed record RevokeConsent {
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required string ConsentId { get; init; }

    public string? ActorUserId { get; init; }
}
