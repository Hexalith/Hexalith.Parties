namespace Hexalith.Parties.Contracts.Queries;

/// <summary>Existing GetErasureStatus wire payload.</summary>
public sealed record GetErasureStatus
{
    /// <summary>Gets the party identifier.</summary>
    public required string PartyId { get; init; }
}
