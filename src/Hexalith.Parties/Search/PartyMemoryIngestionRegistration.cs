namespace Hexalith.Parties.Search;

/// <summary>Reports whether this caller durably reserved a new ingestion attempt.</summary>
/// <param name="Intent">The persisted obligation.</param>
/// <param name="Created">Whether this call alone may submit the attempt.</param>
internal sealed record PartyMemoryIngestionRegistration(PartyMemoryIngestionIntent Intent, bool Created);
