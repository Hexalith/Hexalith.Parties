using Hexalith.Parties.Contracts.ValueObjects;
namespace Hexalith.Parties.Contracts.Models;

/// <summary>Safe action-time attribution; historical success never implies current eligibility.</summary>
/// <param name="Outcome">The explicit historical outcome.</param>
/// <param name="ContractVersion">The identity contract version.</param>
/// <param name="TenantId">The exact tenant.</param>
/// <param name="PartyId">The exact Party.</param>
/// <param name="ActionAt">The exact action instant.</param>
/// <param name="SourcePosition">The stable complete source checkpoint.</param>
/// <param name="ObservationId">The coherent source observation, only when available.</param>
/// <param name="Evidence">The unique matching recorded interval.</param>
public sealed record HumanActorBindingResult(
    HumanActorBindingOutcome Outcome,
    int ContractVersion,
    string TenantId,
    string PartyId,
    DateTimeOffset ActionAt,
    long SourcePosition,
    string? ObservationId,
    HumanActorBindingEvidence? Evidence);
