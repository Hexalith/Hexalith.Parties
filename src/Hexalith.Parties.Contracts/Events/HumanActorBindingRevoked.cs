using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
namespace Hexalith.Parties.Contracts.Events;

/// <summary>Immutable revocation closes the recorded predecessor interval.</summary>
/// <param name="LogicalId">The immutable logical revocation identity.</param>
/// <param name="IntentDigest">The original transition intent.</param>
/// <param name="ExpectedBindingVersion">The exact predecessor version.</param>
/// <param name="Custody">The accepted independent history lifecycle.</param>
/// <param name="EffectiveAt">The exclusive predecessor boundary.</param>
public sealed record HumanActorBindingRevoked(
    string LogicalId,
    string IntentDigest,
    long ExpectedBindingVersion,
    DateTimeOffset EffectiveAt,
    IdentityHistoryCustodyEvidence Custody) : IIdentityHistoryEvent;
