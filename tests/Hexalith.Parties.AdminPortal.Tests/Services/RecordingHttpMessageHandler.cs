using System.Net;

namespace Hexalith.Parties.AdminPortal.Tests.Services;

internal sealed class RecordingHttpMessageHandler(
    HttpStatusCode statusCode,
    string body,
    IDictionary<string, string>? responseHeaders = null) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        };

        response.Content.Headers.ContentType = new("application/json");

        if (responseHeaders is not null)
        {
            foreach ((string key, string value) in responseHeaders)
            {
                response.Headers.TryAddWithoutValidation(key, value);
            }
        }

        return Task.FromResult(response);
    }
}
