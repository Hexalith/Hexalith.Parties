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
    /// <param name="parties">The Parties service project resource builder.</param>
    /// <param name="daprConfigPath">
    /// Path to the Dapr sidecar configuration file (access control policies).
    /// When null, the sidecar starts without access control.
    /// </param>
    /// <returns>A <see cref="HexalithPartiesResources"/> containing the resource builders for further customization.</returns>
    public static HexalithPartiesResources AddHexalithParties(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> parties,
        string? daprConfigPath = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(parties);

        IResourceBuilder<IDaprComponentResource> stateStore = builder
            .AddDaprComponent("statestore", "state.redis")
            .WithMetadata("actorStateStore", "true")
            .WithMetadata("redisHost", "localhost:6379")
            .WithMetadata("keyPrefix", "none");
        IResourceBuilder<IDaprComponentResource> pubSub = builder.AddDaprPubSub("pubsub");

        return builder.AddHexalithParties(parties, daprConfigPath, stateStore, pubSub);
    }

    /// <summary>
    /// Adds the Hexalith Parties topology using existing shared DAPR component resources.
    /// This is used by AppHosts that compose Parties with another Hexalith module, such as
    /// Hexalith.Tenants, where both modules must share the same local state store and pub/sub.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="parties">The Parties service project resource builder.</param>
    /// <param name="daprConfigPath">Path to the Dapr sidecar configuration file.</param>
    /// <param name="stateStore">Shared DAPR state store component.</param>
    /// <param name="pubSub">Shared DAPR pub/sub component.</param>
    /// <returns>A <see cref="HexalithPartiesResources"/> containing the resource builders for further customization.</returns>
    public static HexalithPartiesResources AddHexalithParties(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> parties,
        string? daprConfigPath,
        IResourceBuilder<IDaprComponentResource> stateStore,
        IResourceBuilder<IDaprComponentResource> pubSub)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(parties);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(pubSub);

        // Preserve the EventStore actor key format expected by Parties projections.
        // The shared state store from a composing module (e.g. Hexalith.Tenants) does not
        // set keyPrefix, so DAPR would default to <appId>||<key>. Re-applying it here keeps
        // Parties keys flat regardless of which AppHost overload is used.
        _ = stateStore.WithMetadata("keyPrefix", "none");

        _ = parties
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions
                {
                    AppId = "parties",
                    Config = daprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        HexalithEventStoreResources eventStoreResources = new(
            stateStore,
            pubSub,
            parties,
            parties);

        // Return Parties resources wrapping EventStore resources
        return new HexalithPartiesResources(eventStoreResources, parties);
    }
}
