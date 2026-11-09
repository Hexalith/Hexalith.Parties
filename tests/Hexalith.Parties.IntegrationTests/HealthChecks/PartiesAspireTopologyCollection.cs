namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Shares one Aspire topology fixture across the health endpoint tests.</summary>
[CollectionDefinition("PartiesAspireTopologyHealth", DisableParallelization = true)]
public class PartiesAspireTopologyHealthCollection : ICollectionFixture<PartiesAspireTopologyFixture>
{
}
