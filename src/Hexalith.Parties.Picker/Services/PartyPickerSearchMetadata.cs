namespace Hexalith.Parties.Picker.Services;

public sealed record PartyPickerSearchMetadata
{
    public string? SearchStatus { get; init; }

    public string? DegradedReason { get; init; }

    public string? ServiceDegraded { get; init; }

    public string? StaleDataAge { get; init; }

    public bool IsLocalOnly
        => string.Equals(SearchStatus, "LocalOnly", StringComparison.OrdinalIgnoreCase);

    public bool IsDegraded
        => string.Equals(SearchStatus, "Degraded", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(ServiceDegraded);
}
