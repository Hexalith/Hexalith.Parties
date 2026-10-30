using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Parties.Queries;

using NSubstitute;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Supplies an explicit test-only catalog for the isolated paging transport regression.</summary>
internal static class PartyPagingCatalog
{
    /// <summary>Creates the exact catalog lease needed by the real query router, without running domain servers.</summary>
    /// <param name="queryType">The single paging discriminator exercised by the test.</param>
    /// <returns>The test-only authority over a validated immutable query snapshot.</returns>
    internal static IRouteCatalogAuthority Create(string queryType)
    {
        var declaration = new RouteContractDeclaration("parties-paging-query", "query", PartyIndexQueryTypes.PartyDomain,
            queryType, PartyIndexQueryTypes.ProjectionType, "v1", null, "workload", EventStoreOperationIdentifiers.ProjectionQuery,
            null, [], new RouteExecutionDeclaration(typeof(PartyIndexQueryHandler).FullName!, "exact", [queryType],
                "projection-actor", "projection", [], "ProjectionActor"));
        var route = new RouteCatalogEntry(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration),
            "eventstore", "QueryAsync", "v1", "workload", declaration.Operation, false, "*");
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Seal(new RouteCatalogEnvelope(2, 1, "Development", false,
            [declaration], [route], [], string.Empty, string.Empty));
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1, localAppId: "eventstore");
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        return authority;
    }
}
