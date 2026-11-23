namespace Hexalith.Parties.Queries;

/// <summary>Stores a bounded last-known read-model value, its insertion time, and its known revision.</summary>
internal sealed record PartySdkLastKnownReadModelCacheEntry(object Value, DateTimeOffset StoredAt, string? ETag);
