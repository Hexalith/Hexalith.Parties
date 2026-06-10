namespace Hexalith.Parties.UI.Status;

/// <summary>
/// The aria-live politeness vocabulary for the canonical status→announcement split (AC2). A
/// <see cref="StatusKind"/> resolves to exactly one of these via <see cref="StatusPresentation.PolitenessFor"/>,
/// which is then turned into concrete <c>(role, aria-live)</c> attributes by
/// <see cref="StatusPresentation.LiveRegionAttributes"/>.
/// </summary>
/// <remarks>
/// The split is deliberate and must never be blanket-polite: a validation error or hard failure announced
/// <c>polite</c> is the named accessibility anti-pattern (architecture.md:531-542).
/// </remarks>
public enum LiveRegionPoliteness
{
    /// <summary>No live region at all — nothing is announced in place (used by <see cref="StatusKind.SignInRequired"/>).</summary>
    None,

    /// <summary><c>role="status" aria-live="polite"</c> — status / freshness / accepted-processing.</summary>
    Polite,

    /// <summary><c>role="alert" aria-live="assertive"</c> — validation-rejected / transient-failure / load-failure / hard denial.</summary>
    Assertive,
}
