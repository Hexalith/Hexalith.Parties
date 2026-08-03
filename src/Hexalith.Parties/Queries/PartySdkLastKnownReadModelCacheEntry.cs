namespace Hexalith.Parties.Queries;

/// <summary>Stores one bounded last-known read-model value and its insertion time.</summary>
internal sealed record PartySdkLastKnownReadModelCacheEntry(object Value, DateTimeOffset StoredAt);
