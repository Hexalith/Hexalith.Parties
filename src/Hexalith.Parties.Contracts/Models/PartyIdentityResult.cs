using Hexalith.Parties.Contracts.ValueObjects;
namespace Hexalith.Parties.Contracts.Models;

/// <summary>Typed safe authoritative identity outcome.</summary>
/// <param name="Outcome">The classified outcome.</param>
/// <param name="Evidence">
/// Evidence only for an authorized complete source observation. Ineligible evidence may have Unknown
/// classification but never a human binding; Unknown classification cannot produce a Resolved outcome.
/// </param>
public sealed record PartyIdentityResult(
    PartyIdentityOutcome Outcome,
    PartyIdentityEvidence? Evidence);
