using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.Aspire;

namespace Hexalith.Parties.Aspire;

/// <summary>Composes the Parties domain service through the shared EventStore platform.</summary>
public static class HexalithPartiesServerExtensions
{
    /// <summary>Adds the separately built Parties server with shared Dapr state and pub/sub components.</summary>
    /// <param name="builder">The consuming AppHost builder.</param>
    /// <param name="eventStore">The shared EventStore topology.</param>
    /// <param name="daprConfigPath">The Dapr access-control configuration file.</param>
    /// <param name="appId">The resource name and Dapr application identifier.</param>
    /// <param name="daprPlacementHostAddress">The optional placement service address.</param>
    /// <param name="daprSchedulerHostAddress">The optional scheduler service address.</param>
    /// <returns>The Parties resource for authentication and other composition-specific configuration.</returns>
    /// <remarks>
    /// Authentication, Gateway routing and app-channel tokens remain the consuming AppHost's responsibility.
    /// This package contains no Parties server implementation or dependency and never builds the server.
    /// </remarks>
    public static IResourceBuilder<ProjectResource> AddHexalithPartiesServer(
        this IDistributedApplicationBuilder builder,
        HexalithEventStoreResources eventStore,
        string daprConfigPath,
        string appId = "parties",
        string? daprPlacementHostAddress = null,
        string? daprSchedulerHostAddress = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(daprConfigPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        return builder.AddProject<PartiesServerProjectMetadata>(appId)
            .AddEventStoreDomainModule(
                eventStore, appId, daprConfigPath,
                daprPlacementHostAddress: daprPlacementHostAddress,
                daprSchedulerHostAddress: daprSchedulerHostAddress);
    }
}
