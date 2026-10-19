namespace Hexalith.Parties.Tests.Search;

/// <summary>Runs the real Memories HTTP client and cleanup against a deterministic test transport.</summary>
/// <param name="send">The test's explicit response and side-effect callback.</param>
internal sealed class PartySearchErasureHttpHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => send(request, cancellationToken);
}
