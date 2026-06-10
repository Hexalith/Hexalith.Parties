using Hexalith.Parties.Client;                 // PartiesClientException
using Hexalith.Parties.Contracts.Models;       // ProjectionFreshnessStatus / ProjectionFreshnessMetadata

namespace Hexalith.Parties.UI.Status;

/// <summary>
/// The single, pure mapper from a Parties client outcome (a <see cref="PartiesClientException"/>, a bare
/// HTTP status, a transport exception, or a projection-freshness value) to the canonical
/// <see cref="StatusKind"/> vocabulary, and from a <see cref="StatusKind"/> to its
/// <see cref="LiveRegionPoliteness"/> and the concrete <c>(role, aria-live)</c> ARIA attributes.
/// </summary>
/// <remarks>
/// <para>
/// This type is the <b>one and only</b> implementation of the architecture's Communication-Patterns table
/// (architecture.md:458-484). No screen re-implements this mapping; screens call it. It is pure — no DI, no
/// I/O, no <c>Program.cs</c> registration — so consumers (Story 1.7's reconcile effect, Story 1.8's domain
/// components, every Epic 2/4/5 screen) reuse it verbatim.
/// </para>
/// <para>
/// <b>PII hygiene:</b> the mapper accepts only a status int, a freshness enum, or exception metadata. The
/// <c>403</c> tenant heuristic inspects <c>Title</c>/<c>Detail</c> but returns only a <see cref="StatusKind"/>;
/// it never logs or echoes <c>Detail</c>/party/tenant values.
/// </para>
/// </remarks>
public static class StatusPresentation
{
    /// <summary>
    /// Maps a bare HTTP status code to its canonical <see cref="StatusKind"/>. The <c>default</c> arm is a
    /// fail-safe: an unmapped/unknown status collapses to <see cref="StatusKind.LoadFailure"/> so a raw or
    /// unexpected status is never surfaced.
    /// </summary>
    public static StatusKind FromHttpStatus(int statusCode)
        => statusCode switch
        {
            200 or 202 => StatusKind.AcceptedProcessing,
            400 or 422 => StatusKind.Validation,
            401 => StatusKind.SignInRequired,
            403 => StatusKind.Forbidden,
            404 or 410 => StatusKind.Gone,
            408 or 429 => StatusKind.TransientFailure,
            >= 500 => StatusKind.LoadFailure,
            _ => StatusKind.LoadFailure,         // fail-safe: never surface a raw/unknown status
        };

    /// <summary>
    /// Maps a <see cref="PartiesClientException"/> to its canonical <see cref="StatusKind"/>. A <c>403</c>
    /// that is tenant-related ("warming up") becomes <see cref="StatusKind.TenantUnavailable"/>; otherwise the
    /// status is delegated to <see cref="FromHttpStatus"/> (so a non-tenant <c>403</c> is
    /// <see cref="StatusKind.Forbidden"/>).
    /// </summary>
    public static StatusKind FromClientException(PartiesClientException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.Status == 403 && IsTenantProblem(exception)
            ? StatusKind.TenantUnavailable
            : FromHttpStatus(exception.Status);
    }

    /// <summary>
    /// Maps a projection-freshness status to a <see cref="StatusKind"/>. <see cref="ProjectionFreshnessStatus.Current"/>
    /// is fresh and returns <c>null</c> (no degraded treatment); every other value
    /// (<c>Stale</c>/<c>Rebuilding</c>/<c>Degraded</c>/<c>Unavailable</c>/<c>LocalOnly</c>) returns
    /// <see cref="StatusKind.Degraded"/>.
    /// </summary>
    public static StatusKind? FromFreshness(ProjectionFreshnessStatus status)
        => status == ProjectionFreshnessStatus.Current ? null : StatusKind.Degraded;

    /// <summary>
    /// Convenience overload reading <see cref="ProjectionFreshnessMetadata.Status"/>; see
    /// <see cref="FromFreshness(ProjectionFreshnessStatus)"/>.
    /// </summary>
    public static StatusKind? FromFreshness(ProjectionFreshnessMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return FromFreshness(metadata.Status);
    }

    /// <summary>
    /// Convenience for call sites that catch broadly (AC4). A <see cref="PartiesClientException"/> delegates to
    /// <see cref="FromClientException"/>; a <see cref="TimeoutException"/> or an
    /// <see cref="OperationCanceledException"/> (including <see cref="System.Threading.Tasks.TaskCanceledException"/>,
    /// which is how an HttpClient timeout surfaces) becomes <see cref="StatusKind.TransientFailure"/>; anything
    /// else becomes <see cref="StatusKind.LoadFailure"/>.
    /// </summary>
    /// <remarks>
    /// This mapper is pure and <b>does not receive the caller's cancellation token</b>, so it cannot tell a
    /// transport timeout from a user-initiated cancellation. Filtering a user-initiated cancellation (the user
    /// navigated away / the component disposed) is the <b>call site's</b> responsibility — wrap the call in
    /// <c>catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* abandoned — drop silently */ }</c>
    /// so only an un-requested cancellation (a real timeout) ever reaches this method. (Story 1.7 wires that
    /// effect; Story 1.6 ships only the mapper and this contract.)
    /// </remarks>
    public static StatusKind FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            PartiesClientException pce => FromClientException(pce),
            TimeoutException => StatusKind.TransientFailure,
            OperationCanceledException => StatusKind.TransientFailure,
            _ => StatusKind.LoadFailure,
        };
    }

    /// <summary>
    /// Maps a <see cref="StatusKind"/> to its <see cref="LiveRegionPoliteness"/> (AC2). The switch is
    /// exhaustive with a throwing <c>default</c> arm: Roslyn never proves an enum switch exhaustive, so the
    /// arm is required (CS8509 → error under <c>TreatWarningsAsErrors</c>); throwing — rather than a silent
    /// <c>Polite</c> default — guarantees a future, unmapped state can never default to blanket-polite (the
    /// AC6 test that drives <see cref="System.Enum.GetValues{T}()"/> catches such a state loudly).
    /// </summary>
    public static LiveRegionPoliteness PolitenessFor(StatusKind kind)
        => kind switch
        {
            StatusKind.AcceptedProcessing or StatusKind.TenantUnavailable
                or StatusKind.Gone or StatusKind.Degraded => LiveRegionPoliteness.Polite,
            StatusKind.Validation or StatusKind.Forbidden
                or StatusKind.TransientFailure or StatusKind.LoadFailure => LiveRegionPoliteness.Assertive,
            StatusKind.SignInRequired => LiveRegionPoliteness.None,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null), // never blanket-polite
        };

    /// <summary>
    /// The single source for the literal ARIA strings: <c>Polite → ("status", "polite")</c>,
    /// <c>Assertive → ("alert", "assertive")</c>, <c>None → (null, null)</c>. The component and any future
    /// caller bind these, never hard-coding <c>"polite"</c>/<c>"alert"</c>.
    /// </summary>
    public static (string? Role, string? AriaLive) LiveRegionAttributes(LiveRegionPoliteness politeness)
        => politeness switch
        {
            LiveRegionPoliteness.Polite => ("status", "polite"),
            LiveRegionPoliteness.Assertive => ("alert", "assertive"),
            LiveRegionPoliteness.None => (null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(politeness), politeness, null),
        };

    private static bool IsTenantProblem(PartiesClientException exception)
        => Contains(exception.Title) || Contains(exception.Detail);

    private static bool Contains(string? value)
        => value?.Contains("tenant", StringComparison.OrdinalIgnoreCase) == true;
}
