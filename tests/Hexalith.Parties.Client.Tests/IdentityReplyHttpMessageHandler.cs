namespace Hexalith.Parties.Client.Tests;

/// <summary>Returns controlled identity replies without reaching a network transport.</summary>
/// <param name="reply">The response factory for the outgoing gateway request.</param>
internal sealed class IdentityReplyHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(reply(request));
    }
}
