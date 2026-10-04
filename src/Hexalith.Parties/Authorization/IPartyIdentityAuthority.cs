using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Parties.Authorization;

/// <summary>Verifies identity invariants using SDK admission; generic tenant/RBAC remains gateway-owned.</summary>
public interface IPartyIdentityAuthority
{
    /// <summary>Verifies exact command scope before any state unprotection.</summary>
    PartyIdentityAdmissionResult Admit(CommandEnvelope command);

    /// <summary>Verifies exact query scope before protected source lookup.</summary>
    PartyIdentityAdmissionResult Admit(QueryEnvelope query);
}
