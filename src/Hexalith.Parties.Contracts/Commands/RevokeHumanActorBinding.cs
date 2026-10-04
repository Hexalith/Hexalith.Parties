using System.Text.Json.Serialization;
using Hexalith.Parties.Contracts.Models;
namespace Hexalith.Parties.Contracts.Commands;

/// <summary>Requests a trusted-service human attribution transition.</summary>
/// <param name="TenantId">The exact canonical tenant.</param>
/// <param name="PartyId">The exact Party target.</param>
/// <param name="ActorId">The verified target actor ULID.</param>
/// <param name="ActorRevision">The expected registry capability revision.</param>
/// <param name="ExpectedPartyRevision">The expected complete source position.</param>
/// <param name="ExpectedBindingVersion">The expected predecessor binding version.</param>
/// <param name="EffectiveAt">The immutable effective boundary.</param>
/// <param name="LogicalId">The stable logical intent identity.</param>
/// <param name="PolicyId">The explicitly approved finite policy reference.</param>
public sealed record RevokeHumanActorBinding(
    string TenantId,
    string PartyId,
    string ActorId,
    long ActorRevision,
    long ExpectedPartyRevision,
    long ExpectedBindingVersion,
    DateTimeOffset EffectiveAt,
    string LogicalId,
    string PolicyId)
{
    /// <summary>Gets the independently issued opaque operator intent proof; never a JWT or login identifier.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperatorProof { get; init; }

    /// <summary>Gets processor-owned verified authorization; excluded from public JSON.</summary>
    [JsonIgnore]
    public IdentityCommandAuthorization? Authorization { get; init; }
}
