using Hexalith.EventStore.Contracts.Events;
namespace Hexalith.Parties.Contracts.Events.Rejections;

/// <summary>Value-free identity rejection; never includes foreign existence or profile data.</summary>
/// <param name="ReasonCode">The bounded internal classification.</param>
public sealed record AgentPartyProvisioningRejected(
    string ReasonCode) : IRejectionEvent;
