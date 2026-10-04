
namespace Hexalith.Parties.Contracts.Queries;

/// <summary>Requests unique recorded attribution at one immutable action time.</summary>
/// <param name="TenantId">The exact tenant.</param>
/// <param name="PartyId">The exact Party.</param>
/// <param name="ActionAt">The immutable action instant.</param>
/// <param name="ExpectedActorId">The expected stable actor.</param>
/// <param name="ExpectedBindingVersion">The expected immutable binding version.</param>
public sealed record ResolveHumanActorBindingAt(
    string TenantId,
    string PartyId,
    DateTimeOffset ActionAt,
    string ExpectedActorId,
    long ExpectedBindingVersion);
