using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Contracts.State;

/// <summary>Original immutable result for a logical attribution transition.</summary>
/// <param name="LogicalId">The stable logical identity.</param>
/// <param name="IntentDigest">The exact transition intent digest.</param>
/// <param name="Version">The original transition revision.</param>
/// <param name="OriginalEvidence">The original interval result, absent for revocation.</param>
public sealed record HumanActorBindingTransition(string LogicalId, string IntentDigest,
    long Version, HumanActorBindingEvidence? OriginalEvidence);
