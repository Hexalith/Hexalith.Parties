namespace Hexalith.Parties.Contracts.Security;

public sealed record ErasureVerificationStoreResult
{
    public required string StoreName { get; init; }

    public required ErasureStoreCleanupStatus Status { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public string? ErrorMessage { get; init; }
}
