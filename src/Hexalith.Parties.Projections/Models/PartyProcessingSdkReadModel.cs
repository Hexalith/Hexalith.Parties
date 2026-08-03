using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Models;

/// <summary>PII-free processing activity records projected with the aggregate detail.</summary>
public sealed record PartyProcessingSdkReadModel : IReadModelFreshness
{
    /// <summary>Gets the retained Article 30 processing activity records.</summary>
    public IReadOnlyList<ProcessingActivityRecord> Records { get; init; } = [];

    /// <summary>Gets the highest aggregate sequence durably incorporated by this projection.</summary>
    public long LastSequenceNumber { get; init; } = long.MinValue;

    /// <summary>
    /// Gets the retained erasure high-water mark. A non-null value blocks later events for the
    /// erased identifier because the domain currently supplies no incarnation token.
    /// </summary>
    public long? ErasureSequenceNumber { get; init; }

    /// <summary>Gets the time at which the retained erasure tombstone was established.</summary>
    public DateTimeOffset? ErasedAt { get; init; }

    /// <summary>Gets the newest source or cleanup time incorporated by this projection.</summary>
    public DateTimeOffset? ProjectedAt { get; init; }

    /// <summary>Gets the source projection version, when one is known.</summary>
    public string? ProjectionVersion { get; init; }
}
