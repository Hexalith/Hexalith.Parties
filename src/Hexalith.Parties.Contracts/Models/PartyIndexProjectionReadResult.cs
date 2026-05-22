namespace Hexalith.Parties.Contracts.Models;

public sealed record PartyIndexProjectionReadResult
{
    public required IReadOnlyDictionary<string, PartyIndexEntry> Entries { get; init; }

    public required ProjectionFreshnessMetadata Freshness { get; init; }
}
