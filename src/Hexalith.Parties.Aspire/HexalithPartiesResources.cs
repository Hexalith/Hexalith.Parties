using Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.Aspire;

namespace Hexalith.Parties.Aspire;

/// <summary>
/// Contains the resource builders created by <see cref="HexalithPartiesExtensions.AddHexalithParties"/>
/// for further customization by the consumer.
/// </summary>
/// <param name="EventStoreResources">The EventStore resources (state store, pub/sub, Parties service).</param>
/// <param name="Parties">The Parties service project resource builder.</param>
public record HexalithPartiesResources(
    HexalithEventStoreResources EventStoreResources,
    IResourceBuilder<ProjectResource> Parties);
