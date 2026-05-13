namespace Hexalith.Parties.Picker.Services;

public sealed record PartyPickerSearchMetadata
{
    public string? SearchStatus { get; init; }

    [Obsolete("Not populated by the EventStore-fronted typed query client. Retained for binary compatibility; will be removed after consumers migrate.")]
    public string? DegradedReason { get; init; }

    [Obsolete("Not populated by the EventStore-fronted typed query client. Retained for binary compatibility; will be removed after consumers migrate.")]
    public string? ServiceDegraded { get; init; }

    [Obsolete("Not populated by the EventStore-fronted typed query client. Retained for binary compatibility; will be removed after consumers migrate.")]
    public string? StaleDataAge { get; init; }

    public bool IsLocalOnly
        => string.Equals(SearchStatus, "LocalOnly", StringComparison.OrdinalIgnoreCase);

#pragma warning disable CS0618
    public bool IsDegraded
        => string.Equals(SearchStatus, "Degraded", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(ServiceDegraded);
#pragma warning restore CS0618
}
