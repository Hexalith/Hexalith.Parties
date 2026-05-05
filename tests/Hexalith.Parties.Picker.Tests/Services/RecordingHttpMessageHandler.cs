using System.Net;

namespace Hexalith.Parties.Picker.Tests.Services;

internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public void Enqueue(HttpResponseMessage response)
        => _responses.Enqueue(_ => response);

    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> response)
        => _responses.Enqueue(response);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(CloneRequest(request));
        Func<HttpRequestMessage, HttpResponseMessage> response = _responses.Count == 0
            ? _ => new HttpResponseMessage(HttpStatusCode.OK)
            : _responses.Dequeue();

        return Task.FromResult(response(request));
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
