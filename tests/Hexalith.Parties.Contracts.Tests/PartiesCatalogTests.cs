using Shouldly;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Parties.Contracts.Routing;

namespace Hexalith.Parties.Contracts.Tests;

public class PartiesCatalogTests
{
#if HEXALITH_EVENTSTORE_CATALOG_API
    [Fact]
    public void ExistingPublicAndInternalOperationsKeepTheirPolicyAndSerializedShapes()
    {
        var source = typeof(PartiesCatalogDeclarations).Assembly.GetCustomAttributesData()
            .Single(attribute => attribute.AttributeType.Name == "RouteCatalogDeclarationSourceAttribute");
        source.ConstructorArguments.Single().Value.ShouldBe(typeof(PartiesCatalogDeclarations));
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

#if HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
    /// <summary>Requires exact owner bindings and the SDK's actual refusal of foreign selectors.</summary>
    /// <param name="wireType">The existing published query wire identity.</param>
    /// <param name="selector">Its one owner-enrolled legacy assertion.</param>
    [Theory]
    [InlineData("PartyDetail", "PartyDetailProjectionQueryActor")]
    [InlineData("PartyIndex", "PartyIndexProjectionQueryActor")]
    [InlineData("PartySearch", "PartyIndexProjectionQueryActor")]
    public void PublishedMcpCliQueriesAcceptOnlyTheirOwnerDeclaredLegacySelector(string wireType, string selector)
    {
        var route = PartiesCatalogDeclarations.Queries.Single(query => query.MessageType == wireType);
        route.Execution!.TargetKind.ShouldBe("domain-service");
        route.Execution.QueryKind.ShouldBe("projection");
        route.Execution.ActorType.ShouldBeNull();
        route.Execution.CompatibilityProjectionActorTypes.ShouldBe([selector]);
        RouteCatalogQuerySelectorPolicy.Matches(route, selector).ShouldBeTrue();
        RouteCatalogQuerySelectorPolicy.Matches(route, "ForeignQueryActor").ShouldBeFalse();
    }

    /// <summary>Prevents unrelated query routes from gaining legacy selector bindings.</summary>
    [Fact]
    public void OtherPartyQueriesDoNotAcceptLegacySelectors()
    {
        foreach (var route in PartiesCatalogDeclarations.Queries.Where(query => query.MessageType is not ("PartyDetail" or "PartyIndex" or "PartySearch")))
        {
            route.Execution!.CompatibilityProjectionActorTypes.ShouldBeNull();
            RouteCatalogQuerySelectorPolicy.Matches(route, "PartyDetailProjectionQueryActor").ShouldBeFalse();
            RouteCatalogQuerySelectorPolicy.Matches(route, "PartyIndexProjectionQueryActor").ShouldBeFalse();
        }
    }
#endif
}
