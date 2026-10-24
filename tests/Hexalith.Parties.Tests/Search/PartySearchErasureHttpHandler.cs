namespace Hexalith.Parties.Tests.Search;

/// <summary>Runs the real Memories HTTP client and cleanup against a deterministic test transport.</summary>
/// <param name="send">The test's explicit response and side-effect callback.</param>
internal sealed class PartySearchErasureHttpHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = PartyMemoryInventoryTestWire.TryResponse(request, out HttpResponseMessage inventory)
            ? inventory : await send(request, cancellationToken).ConfigureAwait(false);
#if HEXALITH_MEMORIES_SOURCE_REFRESH
        if (response.IsSuccessStatusCode && request.Method == HttpMethod.Post
            && request.RequestUri!.AbsolutePath.EndsWith("/ingest", StringComparison.Ordinal) && request.Content is not null)
        {
            using System.Text.Json.JsonDocument payload = System.Text.Json.JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (payload.RootElement.TryGetProperty("refreshExistingSource", out System.Text.Json.JsonElement refresh) && refresh.GetBoolean())
            {
                response.Headers.Add(Hexalith.Memories.Contracts.V1.SourceRefreshProtocol.AcknowledgementHeader,
                    Hexalith.Memories.Contracts.V1.SourceRefreshProtocol.Version);
            }
        }
#endif
        return response;
    }
}
