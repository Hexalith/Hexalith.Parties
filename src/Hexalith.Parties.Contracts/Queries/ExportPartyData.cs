namespace Hexalith.Parties.Contracts.Queries;

/// <summary>Existing ExportPartyData wire payload; authorization comes from the query envelope.</summary>
public sealed record ExportPartyData
{
    /// <summary>Gets the party identifier.</summary>
    public required string PartyId { get; init; }
}
