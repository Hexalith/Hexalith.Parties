using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Parties.Contracts.Routing;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using Shouldly;

using HandlerAwareQueryRouter = global::Hexalith.EventStore.Queries.HandlerAwareQueryRouter;
using IDomainQueryHandlerRegistry = global::Hexalith.EventStore.Queries.IDomainQueryHandlerRegistry;
using IDomainQueryInvoker = global::Hexalith.EventStore.Queries.IDomainQueryInvoker;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Exercises the published gateway router against canonical Parties declarations; supplies no hosted runtime qualification.</summary>
public sealed class PartiesCatalogGatewaySelectorTests
{
#if HEXALITH_EVENTSTORE_QUERY_SELECTOR_BINDINGS
    /// <summary>The actual gateway entry point routes declared legacy selectors to the domain host and rejects foreign selectors before invocation.</summary>
    /// <param name="wireType">The existing public query wire identity.</param>
    /// <param name="selector">The caller-supplied compatibility selector.</param>
    /// <param name="accepted">Whether the owner enrolled this exact selector.</param>
    [Theory]
    [InlineData("PartyDetail", "PartyDetailProjectionQueryActor", true)]
    [InlineData("PartyIndex", "PartyIndexProjectionQueryActor", true)]
    [InlineData("PartySearch", "PartyIndexProjectionQueryActor", true)]
    [InlineData("PartyDetail", "ForeignQueryActor", false)]
    [InlineData("PartyIndex", "ForeignQueryActor", false)]
    [InlineData("PartySearch", "ForeignQueryActor", false)]
    [InlineData("PartyDetail", "PartyIndexProjectionQueryActor", false)]
    [InlineData("PartyIndex", "PartyDetailProjectionQueryActor", false)]
    [InlineData("PartySearch", "PartyDetailProjectionQueryActor", false)]
    [InlineData("GetParty", "PartyDetailProjectionQueryActor", true)]
    [InlineData("GetParty", "ForeignQueryActor", false)]
    [InlineData("GetParty", "PartyIndexProjectionQueryActor", false)]
    [InlineData("ExportPartyData", "PartyDetailProjectionQueryActor", true)]
    [InlineData("ExportPartyData", "ForeignQueryActor", false)]
    [InlineData("ExportPartyData", "PartyIndexProjectionQueryActor", false)]
    [InlineData("GetProcessingRecords", "PartyDetailProjectionQueryActor", true)]
    [InlineData("GetProcessingRecords", "ForeignQueryActor", false)]
    [InlineData("GetProcessingRecords", "PartyIndexProjectionQueryActor", false)]
    [InlineData("GetErasureStatus", "PartyDetailProjectionQueryActor", true)]
    [InlineData("GetErasureStatus", "ForeignQueryActor", false)]
    [InlineData("GetErasureStatus", "PartyIndexProjectionQueryActor", false)]
    [InlineData("GetErasureCertificate", "PartyDetailProjectionQueryActor", true)]
    [InlineData("GetErasureCertificate", "ForeignQueryActor", false)]
    [InlineData("GetErasureCertificate", "PartyIndexProjectionQueryActor", false)]
    public async Task CanonicalPartySelectorsEnforceTheActualGatewayBoundary(string wireType, string selector, bool accepted)
    {
        var declaration = PartiesCatalogDeclarations.Queries.Single(query => query.MessageType == wireType);
        var catalog = RouteCatalogCodec.Seal(new(2, 1, "Development", false, [declaration],
            [new(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration), "parties", "query", "v1",
                declaration.CredentialKind, declaration.Operation, false, "tenant-a")], [], "", ""));
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(catalog), RouteCatalogCodec.ComputeRootDigest(catalog), 1);
        int released = 0;
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot,
            () => { released++; return Task.CompletedTask; }));
        var inner = Substitute.For<IQueryRouter>();
        var registry = Substitute.For<IDomainQueryHandlerRegistry>();
        var invoker = Substitute.For<IDomainQueryInvoker>();
        invoker.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { source = "parties" }), declaration.ProjectionType));
        var router = new HandlerAwareQueryRouter(authority, inner, registry, invoker, NullLogger<HandlerAwareQueryRouter>.Instance);
        var request = new SubmitQuery("tenant-a", "party", "party-test", wireType, [], "correlation", "user")
        { ProjectionType = declaration.ProjectionType, ProjectionActorType = selector };
        if (accepted)
        {
            var result = await router.RouteQueryAsync(request, TestContext.Current.CancellationToken);
            result.Success.ShouldBeTrue();
            result.Payload!.Value.GetProperty("source").GetString().ShouldBe("parties");
            await invoker.Received(1).InvokeAsync(Arg.Is<QueryEnvelope>(query => query.TenantId == "tenant-a" && query.Domain == "party" && query.QueryType == wireType),
                TestContext.Current.CancellationToken);
        }
        else
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => router.RouteQueryAsync(request, TestContext.Current.CancellationToken));
            invoker.ReceivedCalls().ShouldBeEmpty();
        }
        inner.ReceivedCalls().ShouldBeEmpty();
        registry.ReceivedCalls().ShouldBeEmpty();
        released.ShouldBe(1);
    }
#endif
}
