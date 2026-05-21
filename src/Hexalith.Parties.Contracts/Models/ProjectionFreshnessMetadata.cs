namespace Hexalith.Parties.Contracts.Models;

public sealed record ProjectionFreshnessMetadata
{
    public const string WarningProjectionRebuilding = "projection-rebuilding";
    public const string WarningProjectionStateStoreUnavailable = "projection-state-store-unavailable";
    public const string WarningProjectionContextUnavailable = "projection-context-unavailable";
    public const string WarningProjectionStateUnavailable = "projection-state-unavailable";

    public required ProjectionFreshnessStatus Status { get; init; }

    public IReadOnlyList<string> WarningCodes { get; init; } = [];
}
