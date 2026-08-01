namespace Hexalith.Parties.Projections.Configuration;

/// <summary>Configuration shared by the SDK projection writers and readers.</summary>
public sealed record PartySdkReadModelOptions
{
    public const string ConfigurationSection = "EventStore:Projections";

    public string ReadModelStateStoreName { get; init; } = "statestore";

    public int FreshnessAgingSeconds { get; init; } = 30;

    public int FreshnessStaleSeconds { get; init; } = 300;
}
