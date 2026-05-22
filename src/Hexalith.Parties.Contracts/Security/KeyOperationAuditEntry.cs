namespace Hexalith.Parties.Contracts.Security;

public sealed record KeyOperationAuditEntry
{
    public required KeyOperationType OperationType { get; init; }

    public required string TenantId { get; init; }

    public required string PartyId { get; init; }

    public required int KeyVersion { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string CorrelationId { get; init; }

    public string? OperationId { get; init; }

    public string? Outcome { get; init; }

    public int? ProcessedCount { get; init; }

    public int? SkippedCount { get; init; }

    public int? FailedCount { get; init; }

    public TenantKeyRotationFailureCategory? FailureCategory { get; init; }
}
