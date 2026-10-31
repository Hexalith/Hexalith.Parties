using Shouldly;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Parties.Contracts.Routing;

namespace Hexalith.Parties.Contracts.Tests;

public class PartiesCatalogTests
{
#if HEXALITH_DEVELOPMENT_CATALOG_SOURCE
    [Fact]
    public void ExistingPublicAndInternalOperationsKeepTheirPolicyAndSerializedShapes()
    {
        var operations = PartiesCatalogDeclarations.Operations;
        operations.Count.ShouldBe(41);
        foreach (var operation in operations) operation.Digest.ShouldBe(RouteCatalogCodec.ComputeOperationContractDigest(operation));
        operations.Single(o => o.RouteId == "party-erase-party").Eligibility.ShouldBe("confirmation-required");
        operations.Single(o => o.RouteId == "party-create-party-composite").Eligibility.ShouldBe("agent-eligible");
        operations.Single(o => o.RouteId == "party-establish-human-actor-binding").Ingress.ShouldBe("internal");
        operations.Single(o => o.RouteId == "party-query-index").RequestSchema.GetRawText().ShouldContain("createdAfter");
        operations.Single(o => o.RouteId == "party-query-detail").ResultSchema.GetRawText().ShouldContain("displayName");
    }
#endif

    [Fact]
    public void DeclarationsCoverEveryCommandQueryAndProjectionWithoutDuplicateRoutes()
    {
        PartiesCatalogDeclarations.Routes.Count.ShouldBe(41);
        PartiesCatalogDeclarations.Commands.Count.ShouldBe(28);
        PartiesCatalogDeclarations.Queries.Count.ShouldBe(11);
        PartiesCatalogDeclarations.Projections.Count.ShouldBe(2);
        PartiesCatalogDeclarations.Routes.Select(route => route.RouteId).Distinct(StringComparer.Ordinal).Count().ShouldBe(41);
        foreach (var command in PartiesCatalogDeclarations.Commands)
        {
            command.Route.Kind.ShouldBe("command");
            PartiesCatalogDeclarations.ReceiverMethods[command.Route.RouteId].ShouldBe("process");
        }
        foreach (var query in PartiesCatalogDeclarations.Queries)
        {
            query.Kind.ShouldBe("query");
            PartiesCatalogDeclarations.ReceiverMethods[query.RouteId].ShouldBe("query");
        }
        foreach (var projection in PartiesCatalogDeclarations.Projections)
        {
            projection.Kind.ShouldBe("projection");
            PartiesCatalogDeclarations.ReceiverMethods[projection.RouteId].ShouldBe("project/v2");
        }
    }
}
