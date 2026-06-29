namespace Hexalith.Parties.Projections.Configuration;

public sealed record ProjectionOptions
{
    public const string ConfigurationSection = "Parties:Projections";

    public int BatchSize { get; init; } = 50;

    public int BatchTimeWindowMs { get; init; } = 500;

    public PartyProjectionPlatformAdapterMode PlatformAdapterMode { get; init; } = PartyProjectionPlatformAdapterMode.EventStore;
}
