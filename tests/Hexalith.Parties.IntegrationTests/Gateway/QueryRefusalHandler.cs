using System.Net;
using System.Net.Http.Json;

namespace Hexalith.Parties.IntegrationTests.Gateway;

/// <summary>Returns a structured gateway refusal without starting external runtime resources.</summary>
/// <param name="status">The actual gateway response status.</param>
/// <param name="onRequest">An optional observation of the real client request.</param>
internal sealed class QueryRefusalHandler(HttpStatusCode status, Action? onRequest = null) : HttpMessageHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        onRequest?.Invoke();
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(new
            {
                title = "Catalog unavailable", type = "https://hexalith.com/problems/catalog-unavailable",
                detail = "Routing catalog unavailable.", correlationId = "query-correlation-1",
            }),
        });
    }
}
