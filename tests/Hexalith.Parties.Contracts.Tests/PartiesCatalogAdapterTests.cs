using Hexalith.Parties.EventStoreExtensions;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests;

public class PartiesCatalogAdapterTests
{
    [Fact]
    public void EnrollmentRequiresCanonicalPayloadHashingFromTheSelectedEventStore()
    {
#if HEXALITH_DEVELOPMENT_CATALOG_SOURCE
        var adapters = PartiesCatalogAdapters.Create();
        adapters.Count.ShouldBe(28);
        adapters.Select(adapter => adapter.Declaration.Route.RouteId)
            .ShouldBe(Contracts.Routing.PartiesCatalogDeclarations.Commands.Select(command => command.Route.RouteId));
#else
        Should.Throw<NotSupportedException>(() => PartiesCatalogAdapters.Create())
            .Message.ShouldContain("canonical payload hashing");
#endif
    }
}
