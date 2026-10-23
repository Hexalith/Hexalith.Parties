using System.Net;
using System.Text;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Returns a fresh route-probe response for each request.</summary>
internal sealed class ProbeHandler(int code, string body) : HttpMessageHandler
{
    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)code) { Content = new StringContent(body.Replace("{instanceId}", Uri.UnescapeDataString(request.RequestUri!.Segments[^1]), StringComparison.Ordinal), Encoding.UTF8, "application/json") });
    }
}
