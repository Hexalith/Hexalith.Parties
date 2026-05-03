namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>
/// xUnit collection definition that shares a single <see cref="PartiesAspireTopologyFixture"/>
/// across all E2E health check test classes. Starts the Aspire topology (CommandApi,
/// DAPR sidecar, in-memory state store/pub/sub) ONCE for the collection.
/// </summary>
[CollectionDefinition("PartiesAspireTopology")]
public class PartiesAspireTopologyCollection : ICollectionFixture<PartiesAspireTopologyFixture>
{
}

[CollectionDefinition("PartiesAspireTopologyHealth")]
public class PartiesAspireTopologyHealthCollection : ICollectionFixture<PartiesAspireTopologyFixture>
{
}

[CollectionDefinition("PartiesAspireTopologyAdmin")]
public class PartiesAspireTopologyAdminCollection : ICollectionFixture<PartiesAspireTopologyFixture>
{
}
