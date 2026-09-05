namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>
/// xUnit collection definition that shares a single <see cref="PartiesAspireTopologyFixture"/>
/// across all E2E health check test classes. Starts the Aspire topology (Parties service,
/// DAPR sidecar, in-memory state store/pub/sub) ONCE for the collection.
/// </summary>
[CollectionDefinition("PartiesAspireTopology", DisableParallelization = true)]
public class PartiesAspireTopologyCollection : ICollectionFixture<PartiesAspireTopologyFixture>
{
}

[CollectionDefinition("PartiesAspireTopologyHealth", DisableParallelization = true)]
public class PartiesAspireTopologyHealthCollection : ICollectionFixture<PartiesAspireTopologyFixture>
{
}

[CollectionDefinition("PartiesAspireTopologyAdmin", DisableParallelization = true)]
public class PartiesAspireTopologyAdminCollection : ICollectionFixture<PartiesAspireTopologyFixture>
{
}
