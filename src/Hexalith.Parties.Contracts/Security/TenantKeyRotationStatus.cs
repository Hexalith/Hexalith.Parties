namespace Hexalith.Parties.Contracts.Security;

public sealed record TenantKeyRotationStatus
{
    public required string TenantId { get; init; }

    public required string OperationId { get; init; }

    public required TenantKeyRotationPhase Phase { get; init; }

    public required int TotalCount { get; init; }

    public required int ProcessedCount { get; init; }

    public required int SkippedCount { get; init; }

    public required int FailedCount { get; init; }

    public required IReadOnlyDictionary<TenantKeyRotationFailureCategory, int> FailureCategories { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? CorrelationId { get; init; }
}
