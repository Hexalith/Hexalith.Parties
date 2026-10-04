using System.Text.Json.Serialization;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

/// <summary>Conditionally provisions the one immutable tenant/Agent mapped Organization Party.</summary>
/// <param name="Identity">The immutable logical provisioning intent.</param>
public sealed record ProvisionAgentParty(AgentPartyIdentity Identity)
{
    /// <summary>Gets the immutable logical retry identity.</summary>
    public string LogicalId => Identity.LogicalId;

    /// <summary>Gets processor-owned verified authorization; public payloads cannot supply it.</summary>
    [JsonIgnore]
    public IdentityCommandAuthorization? Authorization { get; init; }
}
