namespace Hexalith.Parties.Contracts.Queries;

/// <summary>Existing GetErasureCertificate wire payload.</summary>
public sealed record GetErasureCertificate
{
    /// <summary>Gets the party identifier.</summary>
    public required string PartyId { get; init; }
}
