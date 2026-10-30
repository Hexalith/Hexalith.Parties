using Shouldly;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Parties.Contracts.Routing;

namespace Hexalith.Parties.Contracts.Tests;

public class PartiesCatalogTests
{
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
}
