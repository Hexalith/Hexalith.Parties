using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Parties.Authorization;

/// <summary>Typed internal admission result; public denial never discloses existence.</summary>
/// <param name="Evidence">Verified exact-source evidence only on success.</param>
/// <param name="FailureReason">The value-free internal classification.</param>
public sealed record PartyIdentityAdmissionResult(IdentityAdmissionEvidence? Evidence, string? FailureReason);
