namespace Hexalith.Parties.Contracts.Security;

public sealed record TenantKeyRotationRequest
{
    public required string TenantId { get; init; }

    public required string OperationId { get; init; }

    public string? CorrelationId { get; init; }
}
