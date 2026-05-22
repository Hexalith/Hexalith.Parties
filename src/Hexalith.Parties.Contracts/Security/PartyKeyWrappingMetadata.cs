namespace Hexalith.Parties.Contracts.Security;

public sealed record PartyKeyWrappingMetadata
{
    public required string TenantId { get; init; }

    public required string PartyId { get; init; }

    public required int KeyVersion { get; init; }

    public required string TenantKeyId { get; init; }

    public required int TenantKeyVersion { get; init; }

    public required string RotationId { get; init; }

    public required DateTimeOffset WrappedAt { get; init; }
}
