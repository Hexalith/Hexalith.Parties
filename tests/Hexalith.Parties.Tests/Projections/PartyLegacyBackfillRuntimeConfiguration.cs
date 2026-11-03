namespace Hexalith.Parties.Tests.Projections;

/// <summary>Private endpoints for an explicitly owned disposable migration rehearsal store.</summary>
internal sealed record PartyLegacyBackfillRuntimeConfiguration
{
    /// <summary>Gets the uniquely named disposable Dapr application identity.</summary>
    public string DaprAppId { get; init; } = string.Empty;

    /// <summary>Gets the loopback Dapr HTTP endpoint.</summary>
    public string DaprHttpEndpoint { get; init; } = string.Empty;

    /// <summary>Gets the loopback Dapr gRPC endpoint.</summary>
    public string DaprGrpcEndpoint { get; init; } = string.Empty;

    /// <summary>Gets the private Dapr API token.</summary>
    public string DaprApiToken { get; init; } = string.Empty;

    /// <summary>Gets the loopback endpoint of the exclusively owned Redis container for independent actor-byte controls.</summary>
    public string RedisEndpoint { get; init; } = string.Empty;

    /// <summary>Gets the disposable state component name.</summary>
    public string StateStoreName { get; init; } = "statestore";

    /// <summary>Gets explicit confirmation that the entire backing store is disposable and owned by this rehearsal.</summary>
    public bool OwnedDisposableStore { get; init; }
}
