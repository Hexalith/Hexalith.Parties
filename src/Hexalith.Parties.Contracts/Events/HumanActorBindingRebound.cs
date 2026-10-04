using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.State;
namespace Hexalith.Parties.Contracts.Events;

/// <summary>Immutable finite human attribution transition.</summary>
/// <param name="Binding">The successor interval and exact transition identity.</param>
/// <param name="EffectiveAt">The shared predecessor-close and successor-open instant.</param>
/// <param name="ExpectedBindingVersion">The exact predecessor binding version.</param>
public sealed record HumanActorBindingRebound(
    HumanActorBinding Binding,
    DateTimeOffset EffectiveAt,
    long ExpectedBindingVersion) : IIdentityHistoryEvent
{
    /// <inheritdoc/>
    public IdentityHistoryCustodyEvidence Custody => Binding.Evidence.Custody;
}

