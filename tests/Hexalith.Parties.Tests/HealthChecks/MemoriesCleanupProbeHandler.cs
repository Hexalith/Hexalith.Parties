using System.Net;

namespace Hexalith.Parties.Tests.HealthChecks;

/// <summary>
/// Captures cleanup HTTP routing and authorization without contacting Memories.
/// </summary>
/// <param name="status">The controlled cleanup response status.</param>
/// <param name="reasonPhrase">Optional provider text for diagnostic privacy checks.</param>
internal sealed class MemoriesCleanupProbeHandler(
    HttpStatusCode status = HttpStatusCode.NoContent,
    string? reasonPhrase = null) : HttpMessageHandler
{
    /// <summary>Gets the number of cleanup requests.</summary>
    internal int RequestCount { get; private set; }

    /// <summary>Gets the most recent request URI.</summary>
    internal Uri? LastUri { get; private set; }

    /// <summary>Gets the most recent HTTP method.</summary>
    internal HttpMethod? LastMethod { get; private set; }

    /// <summary>Gets the most recent authorization header.</summary>
    internal string? LastAuthorization { get; private set; }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestCount++;
        LastUri = request.RequestUri;
        LastMethod = request.Method;
        LastAuthorization = request.Headers.Authorization?.ToString();
        var response = new HttpResponseMessage(status);
        if (reasonPhrase is not null)
        {
            response.ReasonPhrase = reasonPhrase;
        }

        return Task.FromResult(response);
    }
}
