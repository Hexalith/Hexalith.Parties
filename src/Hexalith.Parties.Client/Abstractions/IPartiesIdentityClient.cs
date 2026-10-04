using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Queries;

namespace Hexalith.Parties.Client.Abstractions;

/// <summary>Narrow gateway identity operations with immutable explicit per-call tenant scope.</summary>
public interface IPartiesIdentityClient
{
    /// <summary>Retries one immutable logical intent without replacing an uncertain identity.</summary>
    Task<AgentPartyProvisioningResult> ProvisionAgentPartyAsync(string tenantId, ProvisionAgentParty command, CancellationToken cancellationToken = default);

    /// <summary>Resolves current identity; unsupported or uncertain evidence remains unavailable.</summary>
    Task<PartyIdentityResult> ResolvePartyIdentityAsync(string tenantId, ResolvePartyIdentity query, CancellationToken cancellationToken = default);

    /// <summary>Resolves unique recorded action-time attribution without a current-binding fallback.</summary>
    Task<HumanActorBindingResult> ResolveHumanActorBindingAtAsync(string tenantId, ResolveHumanActorBindingAt query, CancellationToken cancellationToken = default);

    /// <summary>Submits an admitted establishment using the immutable logical identity.</summary>
    Task EstablishHumanActorBindingAsync(string tenantId, EstablishHumanActorBinding command, CancellationToken cancellationToken = default);

    /// <summary>Submits an admitted rebind using the immutable shared interval boundary.</summary>
    Task RebindHumanActorBindingAsync(string tenantId, RebindHumanActorBinding command, CancellationToken cancellationToken = default);

    /// <summary>Submits an admitted revocation; the recorded historical actor remains immutable.</summary>
    Task RevokeHumanActorBindingAsync(string tenantId, RevokeHumanActorBinding command, CancellationToken cancellationToken = default);
}
