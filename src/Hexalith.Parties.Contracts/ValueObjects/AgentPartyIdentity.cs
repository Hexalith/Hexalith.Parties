
namespace Hexalith.Parties.Contracts.ValueObjects;

/// <summary>Immutable versioned provisioning intent; contains no profile data.</summary>
/// <param name="TenantId">The exact canonical tenant.</param>
/// <param name="AgentId">The immutable Agent ULID.</param>
/// <param name="PartyId">The mapped immutable Party ULID.</param>
/// <param name="ContractVersion">The mapping and identity contract version.</param>
/// <param name="LogicalId">The stable logical retry identity.</param>
/// <param name="CreationFingerprint">The immutable creation intent digest.</param>
/// <param name="CreatedAt">The immutable creation effective instant.</param>
public sealed record AgentPartyIdentity(
    string TenantId,
    string AgentId,
    string PartyId,
    int ContractVersion,
    string LogicalId,
    string CreationFingerprint,
    DateTimeOffset CreatedAt);
