namespace Hexalith.Parties.Contracts.Security;

public sealed record PartyKeyInfo
{
    public required string KeyId { get; init; }

    public required int Version { get; init; }

    public required string TenantId { get; init; }

    public required string PartyId { get; init; }

    public required EncryptionAlgorithm Algorithm { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
