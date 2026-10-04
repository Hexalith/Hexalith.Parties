
namespace Hexalith.Parties.Contracts.Queries;

/// <summary>Requests current identity from complete authoritative source and actor evidence.</summary>
/// <param name="TenantId">The explicit immutable tenant scope.</param>
/// <param name="PartyId">The exact target.</param>
/// <param name="ExpectedActorId">The expected verified stable actor for human eligibility.</param>
public sealed record ResolvePartyIdentity(
    string TenantId,
    string PartyId,
    string? ExpectedActorId = null);
