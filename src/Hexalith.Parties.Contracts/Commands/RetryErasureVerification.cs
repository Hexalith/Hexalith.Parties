namespace Hexalith.Parties.Contracts.Commands;

public sealed record RetryErasureVerification
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    /// <summary>Gets an explicit opt-in to replace ambiguous legacy evidence after fresh authoritative key destruction.</summary>
    public bool RecoverKeyDestructionEvidence { get; init; }
}
