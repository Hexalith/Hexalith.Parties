using Hexalith.Parties.UI.Status;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// Per-circuit holder of the last-seen transport-level degraded signal captured from gateway GET responses
/// (Story 1.7, AC2/AC4 — NFR8). This is the <strong>secondary</strong> degraded signal; the primary signal
/// is <c>ProjectionFreshnessMetadata</c> carried end-to-end on <c>PartyDetail.Freshness</c>.
/// </summary>
public interface IDegradedStateAccessor
{
    /// <summary>Gets whether the last GET response reported <c>X-Service-Degraded: true</c>.</summary>
    bool IsDegraded { get; }

    /// <summary>Gets the last-seen <c>X-Stale-Data-Age</c> value in seconds (when degraded), else <see langword="null"/>.</summary>
    long? StaleDataAgeSeconds { get; }

    /// <summary>
    /// Convenience mapping into the canonical Story 1.6 vocabulary: <see cref="StatusKind.Degraded"/> when
    /// degraded, else <see langword="null"/>. Reuses the canonical state — it does <strong>not</strong>
    /// invent a new one.
    /// </summary>
    StatusKind? StatusKind { get; }

    /// <summary>Records the degraded state captured from a gateway GET response.</summary>
    /// <param name="isDegraded">Whether the response carried <c>X-Service-Degraded: true</c>.</param>
    /// <param name="staleDataAgeSeconds">The <c>X-Stale-Data-Age</c> value when present.</param>
    void Set(bool isDegraded, long? staleDataAgeSeconds);
}

/// <summary>
/// The Scoped (per-circuit — ADR-030) implementation of <see cref="IDegradedStateAccessor"/>.
/// </summary>
internal sealed class DegradedStateAccessor : IDegradedStateAccessor
{
    public bool IsDegraded { get; private set; }

    public long? StaleDataAgeSeconds { get; private set; }

    public StatusKind? StatusKind
        => IsDegraded ? Hexalith.Parties.UI.Status.StatusKind.Degraded : null;

    public void Set(bool isDegraded, long? staleDataAgeSeconds)
    {
        IsDegraded = isDegraded;
        StaleDataAgeSeconds = isDegraded ? staleDataAgeSeconds : null;
    }
}
