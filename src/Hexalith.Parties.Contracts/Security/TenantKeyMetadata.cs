namespace Hexalith.Parties.Contracts.Security;

public sealed record TenantKeyMetadata
{
    public required string TenantId { get; init; }

    public required string KeyId { get; init; }

    public required int Version { get; init; }

    public required string ProviderName { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required string OperationId { get; init; }
}
