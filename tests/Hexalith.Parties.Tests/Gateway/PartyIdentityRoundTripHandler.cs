using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Queries;

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Bridges real client requests to the identity query handler using the gateway JSON envelope.</summary>
/// <param name="handler">The producer exercised by the round trip.</param>
internal sealed class PartyIdentityRoundTripHandler(PartyIdentityQueryHandler handler) : HttpMessageHandler
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
        QueryResult result = await handler.ExecuteAsync(envelope, cancellationToken).ConfigureAwait(false);
        result.Success.ShouldBeTrue();
        var reply = new SubmitQueryResponse(envelope.CorrelationId, JsonSerializer.Deserialize<JsonElement>(result.PayloadBytes!), Metadata: result.Metadata);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(reply, options: PartiesJsonOptions.Default),
        };
    }
}
