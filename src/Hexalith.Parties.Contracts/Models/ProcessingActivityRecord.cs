namespace Hexalith.Parties.Contracts.Models;

public sealed record ProcessingActivityRecord {
    public required long SequenceNumber { get; init; }

    public required string EventType { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string Summary { get; init; }
}
