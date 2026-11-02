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
    /// <summary>An authoritative source contains no created Party.</summary>
    Missing,
    /// <summary>The requested actor differs from the authenticated target.</summary>
    Denied,
}
