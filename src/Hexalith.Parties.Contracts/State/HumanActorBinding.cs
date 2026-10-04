using Hexalith.Parties.Contracts.Models;
namespace Hexalith.Parties.Contracts.State;

/// <summary>Immutable interval and logical transition identity.</summary>
/// <param name="Evidence">The immutable interval evidence.</param>
/// <param name="LogicalId">The exact logical retry identity.</param>
/// <param name="IntentDigest">The immutable transition intent digest.</param>
public sealed record HumanActorBinding(
    HumanActorBindingEvidence Evidence,
    string LogicalId,
    string IntentDigest);
