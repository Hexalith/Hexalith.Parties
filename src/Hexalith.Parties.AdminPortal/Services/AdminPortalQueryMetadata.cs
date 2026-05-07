namespace Hexalith.Parties.AdminPortal.Services;

public sealed record AdminPortalQueryMetadata
{
    private readonly string? _staleDataAge;
    private readonly string? _searchStatus;
    private readonly string? _searchDegradedReason;

    public static AdminPortalQueryMetadata Empty { get; } = new();

    public AdminPortalQueryMetadata(
        bool ServiceDegraded = false,
        string? StaleDataAge = null,
        string? SearchStatus = null,
        string? SearchDegradedReason = null)
    {
        this.ServiceDegraded = ServiceDegraded;
        this.StaleDataAge = BoundHeaderValue(StaleDataAge);
        this.SearchStatus = BoundHeaderValue(SearchStatus);
        this.SearchDegradedReason = BoundHeaderValue(SearchDegradedReason);
    }

    public bool ServiceDegraded { get; init; }

    public string? StaleDataAge
    {
        get => _staleDataAge;
        init => _staleDataAge = BoundHeaderValue(value);
    }

    public string? SearchStatus
    {
        get => _searchStatus;
        init => _searchStatus = BoundHeaderValue(value);
    }

    public string? SearchDegradedReason
    {
        get => _searchDegradedReason;
        init => _searchDegradedReason = BoundHeaderValue(value);
    }

    public bool IsLocalOnlySearch
        => string.Equals(SearchStatus, "local-only", StringComparison.OrdinalIgnoreCase);

    public bool IsDegraded
        => ServiceDegraded
            || !string.IsNullOrWhiteSpace(StaleDataAge)
            || IsLocalOnlySearch
            || !string.IsNullOrWhiteSpace(SearchDegradedReason);

    private static string? BoundHeaderValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string bounded = new(value
            .Trim()
            .Where(static c => !char.IsControl(c))
            .Take(128)
            .ToArray());

        return string.IsNullOrWhiteSpace(bounded) ? null : bounded;
    }
}
