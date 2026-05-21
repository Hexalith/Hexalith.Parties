namespace Hexalith.Parties.Contracts.Models;

public sealed record PartyDataPortabilityPackage
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset ExportedAt { get; init; }

    public required string ExportedBy { get; init; }

    public required string CorrelationId { get; init; }

    public PartyDetail? Party { get; init; }

    public IReadOnlyList<ProcessingActivityRecord> ProcessingRecords { get; init; } = [];

    public ProjectionFreshnessMetadata? Freshness { get; init; }
}
