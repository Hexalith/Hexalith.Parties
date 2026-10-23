using System.Text.Json.Serialization;

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

    /// <summary>
    /// Gets at most one payload-free descriptor per entry, selected by the last nonnull canonical
    /// application. Historical rows without this property retain an empty dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, PartyIndexEventMetadata> LastIndexedEvents { get; init; }
        = new Dictionary<string, PartyIndexEventMetadata>(StringComparer.Ordinal);

    /// <summary>Gets optional digests binding complete transient search content to canonical checkpoints.</summary>
    /// <remarks>Historical rows lack this metadata. No contact or identifier values are retained here.</remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? SearchContentHashes { get; init; }

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

    /// <summary>Gets the diagnostic source-position hint, when one is known.</summary>
    /// <remarks>
    /// After delivery validation succeeds, the index fold prefers <c>global:N</c> for the greatest
    /// positive supplied or retained global position. Otherwise it uses
    /// <c>{aggregateId}:{sequence}</c> for the folded aggregate's
    /// checkpoint, or retains the prior value (possibly null) when no checkpoint is known.
    /// This value does not certify a complete tenant event prefix or every content change;
    /// aggregate fallback values cannot order different aggregates, and rebuilds start afresh.
    /// The accepted aggregate identifier <c>global</c> produces the same text as a global token,
    /// so the prefix alone does not establish that an event supplied that global position.
    /// Cleanup can retain the version while changing entries. Queries forward it as diagnostic
    /// metadata and classify freshness from <see cref="ProjectedAt"/> and degradation signals.
    /// Timestamp freshness also does not establish tenant-wide convergence.
    /// Use the read-model store's ETag for optimistic concurrency.
    /// </remarks>
    public string? ProjectionVersion { get; init; }
}
