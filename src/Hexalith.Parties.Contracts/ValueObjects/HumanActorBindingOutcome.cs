namespace Hexalith.Parties.Contracts.ValueObjects;

/// <summary>Safe identity contract classifications.</summary>
public enum HumanActorBindingOutcome
{
    /// <summary>Unavailable evidence.</summary>
    Unavailable,
    /// <summary>Resolved evidence.</summary>
    Resolved,
    /// <summary>Gap evidence.</summary>
    Gap,
    /// <summary>Ambiguous evidence.</summary>
    Ambiguous,
    /// <summary>Mismatch evidence.</summary>
    Mismatch,
    /// <summary>Revoked evidence.</summary>
    Revoked,
    /// <summary>Expired evidence.</summary>
    Expired,
}
