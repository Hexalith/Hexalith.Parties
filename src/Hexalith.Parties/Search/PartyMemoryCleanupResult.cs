namespace Hexalith.Parties.Search;

/// <summary>Reports a scoped memory-unit cleanup attempt and any remaining block.</summary>
/// <param name="PartyId">The party being cleaned up.</param>
/// <param name="MemoryUnitId">The affected memory-unit identifier, when applicable.</param>
/// <param name="SourceUri">The party source URI.</param>
/// <param name="Cleaned">Whether the scoped cleanup requests were accepted.</param>
/// <param name="BlockedReason">The cleanup block or aggregate progress, if any.</param>
internal sealed record PartyMemoryCleanupResult(
    string PartyId,
    string MemoryUnitId,
    string SourceUri,
    bool Cleaned,
    string? BlockedReason);
