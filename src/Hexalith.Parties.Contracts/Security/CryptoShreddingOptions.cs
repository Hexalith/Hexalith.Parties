namespace Hexalith.Parties.Contracts.Security;

public sealed record CryptoShreddingOptions
{
    public const string ConfigurationSection = "Parties:CryptoShredding";

    public bool IsEnabled { get; init; } = true;

    public int CircuitBreakerFailureThreshold { get; init; } = 3;

    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan CircuitBreakerMaxOpenDuration { get; init; } = TimeSpan.FromMinutes(5);

    public int DrainRecoveryBatchSize { get; init; } = 5;

    public TimeSpan DrainRecoveryDelayBetweenEvents { get; init; } = TimeSpan.FromSeconds(2);
}
