using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;

namespace Hexalith.Parties.Aspire;

/// <summary>
/// Provides extension methods for adding the Hexalith Parties topology
/// to an Aspire distributed application.
/// </summary>
public static class HexalithPartiesExtensions
{
    /// <summary>
    /// Adds the Hexalith Parties topology to the distributed application builder.
    /// This delegates to <see cref="HexalithEventStoreExtensions.AddHexalithEventStore"/>
    /// for DAPR state store, pub/sub, and sidecar wiring, then wraps the result
    /// in a <see cref="HexalithPartiesResources"/> record.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="commandApi">The CommandApi project resource builder.</param>
    /// <param name="daprConfigPath">
    /// Path to the Dapr sidecar configuration file (access control policies).
    /// When null, the sidecar starts without access control.
    /// </param>
    /// <returns>A <see cref="HexalithPartiesResources"/> containing the resource builders for further customization.</returns>
    public static HexalithPartiesResources AddHexalithParties(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> commandApi,
        string? daprConfigPath = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(commandApi);

        IResourceBuilder<IDaprComponentResource> stateStore = builder
            .AddDaprComponent("statestore", "state.redis")
            .WithMetadata("actorStateStore", "true")
            .WithMetadata("redisHost", "localhost:6379")
            .WithMetadata("keyPrefix", "none");
        IResourceBuilder<IDaprComponentResource> pubSub = builder.AddDaprPubSub("pubsub");

        _ = commandApi
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions
                {
                    AppId = "commandapi",
                    Config = daprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        HexalithEventStoreResources eventStoreResources = new(
            stateStore,
            pubSub,
            commandApi,
            commandApi);

        // Return Parties resources wrapping EventStore resources
        return new HexalithPartiesResources(eventStoreResources, commandApi);
    }
}
