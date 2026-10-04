using Hexalith.EventStore.Contracts.Security;
namespace Hexalith.Parties.Contracts.Models;

/// <summary>Minimal immutable opaque human attribution for one half-open interval.</summary>
/// <param name="TenantId">The exact tenant.</param>
/// <param name="PartyId">The exact Party.</param>
/// <param name="ActorId">The verified opaque actor ULID.</param>
/// <param name="BindingVersion">The immutable binding version.</param>
/// <param name="ActorRevision">The admitted actor capability revision.</param>
/// <param name="ValidFrom">The inclusive effective boundary.</param>
/// <param name="ValidUntil">The exclusive boundary, when closed.</param>
/// <param name="SourceId">The verified trusted source reference.</param>
/// <param name="ProvenanceId">The opaque verified operator provenance.</param>
/// <param name="Custody">The independently enforced finite history lifecycle.</param>
public sealed record HumanActorBindingEvidence(
    string TenantId,
    string PartyId,
    string ActorId,
    long BindingVersion,
    long ActorRevision,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil,
    string SourceId,
    string ProvenanceId,
    IdentityHistoryCustodyEvidence Custody);
