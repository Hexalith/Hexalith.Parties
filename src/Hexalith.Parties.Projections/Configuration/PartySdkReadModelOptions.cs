namespace Hexalith.Parties.Projections.Configuration;

/// <summary>Configuration shared by the SDK projection writers and readers.</summary>
public sealed record PartySdkReadModelOptions
{
    /// <summary>The canonical Parties projection configuration section.</summary>
    public const string ConfigurationSection = "Parties:Projections";

    /// <summary>The legacy SDK section used as a per-property fallback.</summary>
    public const string LegacyConfigurationSection = "EventStore:Projections";

    /// <summary>The state component holding the SDK read models.</summary>
    public string ReadModelStateStoreName { get; init; } = "statestore";

    /// <summary>The age in seconds at which a read model starts aging.</summary>
    public int FreshnessAgingSeconds { get; init; } = 30;

    /// <summary>The age in seconds at which a read model becomes stale.</summary>
    public int FreshnessStaleSeconds { get; init; } = 300;
    /// <summary>Returns the configured state component after validating its name.</summary>
    /// <returns>The nonempty state component name.</returns>
    internal string GetStoreName()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ReadModelStateStoreName);
        return ReadModelStateStoreName;
    }
}
