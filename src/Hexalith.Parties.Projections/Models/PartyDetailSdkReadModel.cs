using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Models;

/// <summary>The canonical aggregate-owned detail value written by the SDK projection.</summary>
public sealed record PartyDetailSdkReadModel : IReadModelFreshness
{
    /// <summary>Gets the projected party detail, or <see langword="null"/> when no detail is available.</summary>
    public PartyDetail? Detail { get; init; }

    /// <summary>Gets the highest aggregate sequence durably incorporated by this projection.</summary>
    public long LastSequenceNumber { get; init; } = long.MinValue;

    /// <summary>
    /// Gets the retained erasure high-water mark. A non-null value permanently prevents events for
    /// the erased aggregate identifier from restoring personal data without a domain generation token.
    /// </summary>
    public long? ErasureSequenceNumber { get; init; }

    /// <summary>Gets the time at which the retained erasure tombstone was established.</summary>
    public DateTimeOffset? ErasedAt { get; init; }

    /// <summary>Gets the newest source or cleanup time incorporated by this projection.</summary>
    public DateTimeOffset? ProjectedAt { get; init; }

    /// <summary>Gets the source projection version, when one is known.</summary>
    public string? ProjectionVersion { get; init; }
}
