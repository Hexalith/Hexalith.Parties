using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Queries;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Substitutes only the Dapr transport while exercising the published query router.</summary>
/// <param name="handler">The actual Parties paging handler invoked by the router.</param>
internal sealed class PartyPagingActorInvocationHandler(PartySdkQueryHandler handler) : HttpMessageHandler
{
    /// <summary>Gets the query envelopes serialized by the real router and Dapr proxy.</summary>
    internal List<QueryEnvelope> Envelopes { get; } = [];

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath.EndsWith("/method/QueryAsync", StringComparison.Ordinal) != true)
        {
            // The fixture has no lifecycle actor. The real router handles this observation
            // failure independently and must still preserve the domain's paging failure.
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        QueryEnvelope envelope = await request.Content!.ReadFromJsonAsync<QueryEnvelope>(PartiesJsonOptions.Default, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("The router sent no query envelope.");
        Envelopes.Add(envelope);
        QueryResult result = await handler.ExecuteAsync(envelope, cancellationToken).ConfigureAwait(false);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(result, options: PartiesJsonOptions.Default),
        };
    }
}
