namespace Hexalith.Parties.Tests.FitnessTests;

/// <summary>
/// Represents an accepted Epic 8 closure deferral parsed from the deferred-work ledger.
/// </summary>
/// <param name="Id">The stable deferral identifier.</param>
/// <param name="Status">Optional packed status token when present (legacy fixtures still use <c>accepted</c>); membership in ExpectedDeferrals is the accepted-wait set.</param>
/// <param name="Owner">The accountable owner.</param>
/// <param name="ExitProof">The proof required to close the deferral.</param>
/// <param name="Rollback">The retained rollback path.</param>
/// <param name="Evidence">The current evidence anchor.</param>
internal sealed record ClosureDeferral(
    string Id,
    string Status,
    string Owner,
    string ExitProof,
    string Rollback,
    string Evidence);
