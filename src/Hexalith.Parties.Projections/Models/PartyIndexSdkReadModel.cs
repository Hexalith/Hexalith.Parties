using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Models;

/// <summary>The canonical shared tenant index value written by the SDK projection.</summary>
public sealed record PartyIndexSdkReadModel : IReadModelFreshness
{
    /// <summary>Gets the searchable entries keyed by aggregate identifier.</summary>
    public IReadOnlyDictionary<string, PartyIndexEntry> Entries { get; init; }
        = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal);

    /// <summary>Gets the highest incorporated aggregate sequence for each active identifier.</summary>
    public IReadOnlyDictionary<string, long> LastSequenceNumbers { get; init; }
        = new Dictionary<string, long>(StringComparer.Ordinal);

    /// <summary>
    /// Gets permanent erasure high-water marks keyed by aggregate identifier. Presence of a key
    /// prevents delayed or same-identifier events from restoring an erased entry.
    /// </summary>
    public IReadOnlyDictionary<string, long> ErasureSequenceNumbers { get; init; }
        = new Dictionary<string, long>(StringComparer.Ordinal);

    /// <summary>Gets the newest source or cleanup time incorporated by this projection.</summary>
    public DateTimeOffset? ProjectedAt { get; init; }

    /// <summary>Gets the source projection version, when one is known.</summary>
    public string? ProjectionVersion { get; init; }
}
