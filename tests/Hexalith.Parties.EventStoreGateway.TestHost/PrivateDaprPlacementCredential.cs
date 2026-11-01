using Hexalith.EventStore.HealthChecks;

namespace Hexalith.Parties.EventStoreGateway.TestHost;

/// <summary>Configures the real placement probe for the explicitly private, token-protected sidecar.</summary>
public static class PrivateDaprPlacementCredential
{
    /// <summary>Adds the local sidecar credential only to the SDK placement probe's typed HTTP client.</summary>
    /// <param name="builder">The private package gateway application builder.</param>
    public static void AddTo(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!builder.Environment.IsDevelopment()
            || string.IsNullOrWhiteSpace(builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:CatalogPath"]))
        {
            return;
        }

        string? token = builder.Configuration["DAPR_API_TOKEN"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            builder.Services.AddHttpClient<IDaprActorPlacementProbe, DaprActorPlacementProbe>()
                .ConfigureHttpClient(client => client.DefaultRequestHeaders.Add("dapr-api-token", token));
        }
    }
}
