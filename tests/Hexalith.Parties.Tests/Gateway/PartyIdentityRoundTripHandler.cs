using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Routing;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using Shouldly;

using HandlerAwareQueryRouter = global::Hexalith.EventStore.Queries.HandlerAwareQueryRouter;
using IDomainQueryHandlerRegistry = global::Hexalith.EventStore.Queries.IDomainQueryHandlerRegistry;
using IDomainQueryInvoker = global::Hexalith.EventStore.Queries.IDomainQueryInvoker;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Bridges real client requests to the identity query handler using the gateway JSON envelope.</summary>
/// <param name="handler">The producer exercised by the round trip.</param>
internal sealed class PartyIdentityRoundTripHandler(IDomainQueryHandler handler) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/queries");
        JsonElement body = await request.Content!.ReadFromJsonAsync<JsonElement>(PartiesJsonOptions.Default, cancellationToken).ConfigureAwait(false);
        var envelope = new QueryEnvelope(body.GetProperty("tenant").GetString()!, body.GetProperty("domain").GetString()!,
            body.GetProperty("aggregateId").GetString()!, body.GetProperty("queryType").GetString()!,
            JsonSerializer.SerializeToUtf8Bytes(body.GetProperty("payload")), "correlation", "reader",
            body.GetProperty("entityId").GetString());
        QueryResult result;
#if HEXALITH_EVENTSTORE_CATALOG_API
        var declaration = PartiesCatalogDeclarations.Queries.Single(query => query.MessageType == envelope.QueryType);
        var catalog = RouteCatalogCodec.Seal(new(2, 1, "Development", false, [declaration],
            [new(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration), "parties", "query", "v1",
                declaration.CredentialKind, declaration.Operation, false, envelope.TenantId)], [], "", ""));
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(catalog), RouteCatalogCodec.ComputeRootDigest(catalog), 1);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        var invoker = Substitute.For<IDomainQueryInvoker>();
        invoker.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(call => handler.ExecuteAsync(call.Arg<QueryEnvelope>(), call.Arg<CancellationToken>()));
        var router = new HandlerAwareQueryRouter(authority, Substitute.For<IQueryRouter>(),
            Substitute.For<IDomainQueryHandlerRegistry>(), invoker, NullLogger<HandlerAwareQueryRouter>.Instance);
        var submitted = new SubmitQuery(envelope.TenantId, envelope.Domain, envelope.AggregateId, envelope.QueryType,
            envelope.Payload, envelope.CorrelationId, envelope.UserId, envelope.EntityId,
            body.TryGetProperty("projectionType", out JsonElement projection) ? projection.GetString() : null,
            body.TryGetProperty("projectionActorType", out JsonElement actor) ? actor.GetString() : null);
        QueryRouterResult routed = await router.RouteQueryAsync(submitted, cancellationToken).ConfigureAwait(false);
        routed.Success.ShouldBeTrue();
        result = QueryResult.FromPayload(routed.Payload!.Value, routed.ProjectionType, routed.Metadata);
#else
        result = await handler.ExecuteAsync(envelope, cancellationToken).ConfigureAwait(false);
#endif
        result.Success.ShouldBeTrue();
        var reply = new SubmitQueryResponse(envelope.CorrelationId, JsonSerializer.Deserialize<JsonElement>(result.PayloadBytes!), Metadata: result.Metadata);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(reply, options: PartiesJsonOptions.Default),
        };
    }
}
