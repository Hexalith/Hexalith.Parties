namespace Hexalith.Parties.Contracts.Queries;

/// <summary>Reads complete retained Article 30 history. An SDK paging envelope requests one bounded, protected-cursor page instead.</summary>
public sealed record GetProcessingRecords
{
    /// <summary>Gets the party identifier; the authorized tenant and caller come from the query envelope.</summary>
    public required string PartyId
    {
        get; init;
    }
}
