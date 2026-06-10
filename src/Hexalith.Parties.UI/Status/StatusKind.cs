namespace Hexalith.Parties.UI.Status;

/// <summary>
/// The canonical set of UI states a Parties client outcome (a <c>PartiesClientException</c> HTTP status
/// or a projection-freshness value) maps to. This is the architecture's Communication-Patterns table
/// (architecture.md:458-484) expressed verbatim — exactly nine states, no more, no less.
/// </summary>
/// <remarks>
/// <para>
/// This is the <b>single source of truth</b> for "how an outcome looks and sounds". Every screen consumes
/// this vocabulary via <see cref="StatusPresentation"/>; no screen re-implements the status→state table.
/// </para>
/// <para>
/// Deliberately <b>not</b> the legacy AdminPortal/Picker supersets
/// (<c>Loading</c>/<c>Loaded</c>/<c>Conflict</c>/<c>NoData</c>/…). Those are pre-existing, richer,
/// screen-specific taxonomies in RCLs that cannot reference this host; the canonical set intentionally
/// collapses them. Do not add states here to match those.
/// </para>
/// </remarks>
public enum StatusKind
{
    /// <summary><c>200</c>/<c>202</c> — optimistic write accepted, reconcile in flight.</summary>
    AcceptedProcessing,

    /// <summary><c>400</c>/<c>422</c> — validation rejected; surface inline and preserve input.</summary>
    Validation,

    /// <summary><c>401</c> — sign-in required; route to sign-in with a return URL (no in-place announcement).</summary>
    SignInRequired,

    /// <summary><c>403</c> tenant-related ("warming up") — render the last-known view, retry shortly.</summary>
    TenantUnavailable,

    /// <summary><c>403</c> hard denial — the user's role does not permit this.</summary>
    Forbidden,

    /// <summary><c>404</c>/<c>410</c> — tombstone; show a no-PII gone state.</summary>
    Gone,

    /// <summary><c>408</c>/timeout/<c>429</c> — transient; retry with backoff.</summary>
    TransientFailure,

    /// <summary><c>&gt;=500</c> and any unmapped/unknown status — load failed; retry + support (never a raw 500).</summary>
    LoadFailure,

    /// <summary>Projection freshness is not <c>Current</c> — render last-known data with a degraded indicator.</summary>
    Degraded,
}
