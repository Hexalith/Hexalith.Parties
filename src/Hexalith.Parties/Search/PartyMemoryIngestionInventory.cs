namespace Hexalith.Parties.Search;

/// <summary>Durable admission generation, unresolved obligations and terminal cleanup receipts.</summary>
/// <param name="CleanupRequested">Whether this generation is closed to publication.</param>
/// <param name="Intents">Retained ingestion obligations; entries are never evicted.</param>
/// <param name="Generation">Generation used to distinguish ingestion after a transient removal.</param>
/// <param name="PermanentCleanup">Whether an erasure permanently closed admission.</param>
/// <param name="CleanupReceipts">Canonical provenance retained for writers that can resume after cleanup.</param>
/// <param name="SourceClosureAuthority">Hashed normalized producer endpoint for the closure; missing legacy authority never qualifies a cache hit.</param>
/// <param name="SourceClosureVersion">Complete terminal remote inventory observed under the permanent source fence, if any.</param>
internal sealed record PartyMemoryIngestionInventory(
    bool CleanupRequested,
    List<PartyMemoryIngestionIntent> Intents,
    long Generation = 0,
    bool PermanentCleanup = false,
    List<PartyMemoryIngestionIntent>? CleanupReceipts = null,
    string? SourceClosureVersion = null,
    string? SourceClosureAuthority = null);
