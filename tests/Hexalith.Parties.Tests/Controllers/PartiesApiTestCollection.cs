namespace Hexalith.Parties.Tests.Controllers;

/// <summary>
/// Serializes test classes that all share the same <see cref="PartiesApiTestFactory"/>
/// instance via <c>IClassFixture</c>. Without this collection, xUnit runs separate
/// classes in parallel by default — and the fixture's <c>TestTenantAccessService.Handler</c>
/// is mutated per test, so two classes mutating the same handler concurrently would
/// race and produce flaky failures. The collection definition has no fixture of its own;
/// it exists solely to disable cross-class parallel execution for these tests.
/// </summary>
[CollectionDefinition(PartiesApiTestCollection.Name, DisableParallelization = true)]
public sealed class PartiesApiTestCollection
{
    public const string Name = "PartiesApiFactory";
}
