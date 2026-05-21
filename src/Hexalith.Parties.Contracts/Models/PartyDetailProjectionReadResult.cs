namespace Hexalith.Parties.Contracts.Models;

public sealed record PartyDetailProjectionReadResult
{
    public PartyDetail? Detail { get; init; }

    public required ProjectionFreshnessMetadata Freshness { get; init; }
}
