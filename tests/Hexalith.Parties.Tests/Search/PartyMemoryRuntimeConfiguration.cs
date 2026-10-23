namespace Hexalith.Parties.Tests.Search;

/// <summary>Private opt-in configuration for the owned local Memories/Dapr runtime.</summary>
internal sealed record PartyMemoryRuntimeConfiguration
{
    public string Endpoint { get; init; } = string.Empty;
    public string DaprHttpEndpoint { get; init; } = string.Empty;
    public string DaprGrpcEndpoint { get; init; } = string.Empty;
    public string DaprApiToken { get; init; } = string.Empty;
    public string AppApiToken { get; init; } = string.Empty;
    public string JwtIssuer { get; init; } = string.Empty;
    public string JwtAudience { get; init; } = string.Empty;
    public string JwtSigningKey { get; init; } = string.Empty;
    public string TenantClaimName { get; init; } = "tenant";
    public string StateStoreName { get; init; } = "statestore";
    public string InventoryAclRepairEndpoint { get; init; } = string.Empty;
    public bool FakeEmbeddingEnabled { get; init; }
}
