namespace Hexalith.Parties.Search;

/// <summary>
/// Configuration for the Dapr state store backing the per-party → memory-unit-id mapping.
/// The Dapr state-store component name is operator-configurable (default: <c>statestore</c>)
/// because Aspire / Dapr deployments commonly use named state stores per concern.
/// </summary>
internal sealed class PartyMemoryUnitMappingStoreOptions
{
    public const string SectionName = "Parties:MemoriesSearch:MappingStore";

    public string StateStoreName { get; init; } = "statestore";
}
