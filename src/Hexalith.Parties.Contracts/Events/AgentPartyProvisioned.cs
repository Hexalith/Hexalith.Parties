using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
namespace Hexalith.Parties.Contracts.Events;

/// <summary>Immutable provenance marker committed atomically with Party creation.</summary>
/// <param name="Result">The original safe provisioning identity and result.</param>
public sealed record AgentPartyProvisioned(
    [property: PersonalData] AgentPartyProvisioningResult Result) : IEventPayload;
