using System.Net;

namespace Hexalith.Parties.Tests.HealthChecks;

/// <summary>
/// Captures cleanup and ingestion-status routing and authorization without contacting Memories.
/// </summary>
/// <param name="status">The controlled cleanup response status.</param>
/// <param name="reasonPhrase">Optional provider text for diagnostic privacy checks.</param>
internal sealed class MemoriesCleanupProbeHandler(
    HttpStatusCode status = HttpStatusCode.NoContent,
    string? reasonPhrase = null) : HttpMessageHandler
{
    /// <summary>Gets every captured method, URI and authorization header.</summary>
    internal List<(HttpMethod Method, Uri Uri, string? Authorization)> Requests { get; } = [];

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.ToString()));
        if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("/api/v1/ingest/", StringComparison.Ordinal))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"code\":\"INGESTION_STATUS_NOT_FOUND\"}", System.Text.Encoding.UTF8, "application/json"),
            });
        }

        var response = new HttpResponseMessage(status);
        if (reasonPhrase is not null)
        {
            response.ReasonPhrase = reasonPhrase;
        }

        return Task.FromResult(response);
    }
}
