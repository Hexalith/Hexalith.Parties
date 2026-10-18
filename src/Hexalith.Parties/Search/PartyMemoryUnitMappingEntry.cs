namespace Hexalith.Parties.Search;

/// <summary>
/// Identifies a mapped Memories unit in its captured ingestion case. Cleanup compares
/// the entire record so it cannot remove a changed source or another case's mapping.
/// </summary>
/// <param name="MemoryUnitId">The recorded Memories unit identifier.</param>
/// <param name="SourceUri">The indexed Parties source URI.</param>
/// <param name="CaseId">The ingestion case, or null for legacy mappings.</param>
internal sealed record PartyMemoryUnitMappingEntry(string MemoryUnitId, string SourceUri, string? CaseId = null);
