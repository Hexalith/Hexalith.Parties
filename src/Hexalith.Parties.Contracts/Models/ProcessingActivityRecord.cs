namespace Hexalith.Parties.Contracts.Models;

public sealed record ProcessingActivityRecord {
    public required long SequenceNumber { get; init; }

    public string PartyId { get; init; } = string.Empty;

    public string TenantId { get; init; } = string.Empty;

    public string ActorId { get; init; } = "unknown";

    public string CorrelationId { get; init; } = "unspecified";

    public string OperationCategory { get; init; } = "DomainEvent";

    public string Outcome { get; init; } = "Succeeded";

    public required string EventType { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string Summary { get; init; }
}
