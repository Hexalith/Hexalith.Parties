namespace Hexalith.Parties.Contracts.ValueObjects;

/// <summary>Safe identity contract classifications.</summary>
public enum PartyIdentityOutcome
{
    /// <summary>Unavailable evidence.</summary>
    Unavailable,
    /// <summary>Resolved evidence.</summary>
    Resolved,
    /// <summary>Ineligible evidence.</summary>
    Ineligible,
    /// <summary>Conflict evidence.</summary>
    Conflict,
}
