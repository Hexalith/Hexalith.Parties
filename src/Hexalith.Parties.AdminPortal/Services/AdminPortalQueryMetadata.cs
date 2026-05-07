namespace Hexalith.Parties.AdminPortal.Services;

public sealed record AdminPortalQueryMetadata(
    bool ServiceDegraded = false,
    string? StaleDataAge = null,
    string? SearchStatus = null,
    string? SearchDegradedReason = null)
{
    public static AdminPortalQueryMetadata Empty { get; } = new();

    public bool IsLocalOnlySearch
        => string.Equals(SearchStatus, "local-only", StringComparison.OrdinalIgnoreCase);

    public bool IsDegraded
        => ServiceDegraded
            || !string.IsNullOrWhiteSpace(StaleDataAge)
            || IsLocalOnlySearch
            || !string.IsNullOrWhiteSpace(SearchDegradedReason);
}
