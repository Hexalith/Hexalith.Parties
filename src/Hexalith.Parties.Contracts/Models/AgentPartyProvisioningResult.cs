using Hexalith.Parties.Contracts.ValueObjects;
namespace Hexalith.Parties.Contracts.Models;

/// <summary>The original immutable provisioning result; current liveness is evaluated separately.</summary>
/// <param name="Identity">The original immutable provisioning identity.</param>
/// <param name="ProvisioningRevision">The original marker source position.</param>
/// <param name="SourceId">The verified original provisioning source.</param>
public sealed record AgentPartyProvisioningResult(
    AgentPartyIdentity Identity,
    long ProvisioningRevision,
    string SourceId);
