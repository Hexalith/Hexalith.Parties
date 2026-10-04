using Hexalith.Parties.Contracts.ValueObjects;
namespace Hexalith.Parties.Contracts.Models;

/// <summary>Typed safe authoritative identity outcome.</summary>
/// <param name="Outcome">The classified outcome.</param>
/// <param name="Evidence">Evidence only for an authorized complete source observation.</param>
public sealed record PartyIdentityResult(
    PartyIdentityOutcome Outcome,
    PartyIdentityEvidence? Evidence);
