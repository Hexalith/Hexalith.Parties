using System.Net;
using System.Text;
using System.Text.Json;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Exercises the published ingestion wire format and delegates workflow/unit routes.</summary>
internal sealed class PartyMemoryRestHandler : HttpMessageHandler
{
    private readonly HttpMessageInvoker _recovery;

    public PartyMemoryRestHandler()
    {
        _recovery = new HttpMessageInvoker(Recovery);
    }

    public PartyMemoryRecoveryHandler Recovery { get; } = new();

    public List<JsonElement> Submissions { get; } = [];

    public HttpStatusCode SubmissionStatus { get; set; } = HttpStatusCode.Accepted;

    public string SubmissionBody { get; set; } = "{\"instanceId\":\"workflow\"}";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/api/v1/ingest", StringComparison.Ordinal))
        {
            using JsonDocument payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            Submissions.Add(payload.RootElement.Clone());
            return new HttpResponseMessage(SubmissionStatus)
            {
                Content = new StringContent(SubmissionBody, Encoding.UTF8, "application/json"),
            };
        }

        return await _recovery.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _recovery.Dispose();
        }

        base.Dispose(disposing);
    }
}
