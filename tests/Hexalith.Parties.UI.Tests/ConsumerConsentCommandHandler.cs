using System.Net;
using System.Text.Json;

using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.UI.Tests;

/// <summary>Captures gateway envelopes without contacting a remote service.</summary>
/// <param name="queryDetail">The optional historical detail returned for overview queries.</param>
internal sealed class ConsumerConsentCommandHandler(PartyDetail? queryDetail = null) : HttpMessageHandler
{
    /// <summary>Gets the methods, destinations and envelopes received by this handler.</summary>
    public List<(HttpMethod Method, Uri? Uri, JsonElement Envelope)> Requests { get; } = [];

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using JsonDocument document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        Requests.Add((request.Method, request.RequestUri, document.RootElement.Clone()));
        bool query = request.RequestUri?.AbsolutePath == "/api/v1/queries";
        return new HttpResponseMessage(query ? HttpStatusCode.OK : HttpStatusCode.Accepted)
        {
            Content = new StringContent(query ? JsonSerializer.Serialize(new { payload = queryDetail }, PartiesJsonOptions.Default) : "{}"),
        };
    }
}
