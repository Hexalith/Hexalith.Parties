namespace Hexalith.Parties.Contracts.Models;

public sealed record ProjectionFreshnessMetadata
{
    public const string WarningProjectionRebuilding = "projection-rebuilding";
    public const string WarningProjectionStateStoreUnavailable = "projection-state-store-unavailable";
    public const string WarningProjectionContextUnavailable = "projection-context-unavailable";
    public const string WarningProjectionStateUnavailable = "projection-state-unavailable";

    public required ProjectionFreshnessStatus Status { get; init; }

    public IReadOnlyList<string> WarningCodes { get; init; } = [];

    // Consolidated factory so the four projection/query/extension call sites share one
    // construction shape. Previously each site declared its own private `Freshness(...)`
    // helper with diverging signatures; the cycle-1 fix only deduplicated the string
    // literals. Keeping the factory here next to the constants prevents the next
    // freshness-status site from drifting back into a local helper.
    public static ProjectionFreshnessMetadata Create(
        ProjectionFreshnessStatus status,
        params string[] warningCodes)
        => new()
        {
            Status = status,
            WarningCodes = warningCodes,
        };
}
