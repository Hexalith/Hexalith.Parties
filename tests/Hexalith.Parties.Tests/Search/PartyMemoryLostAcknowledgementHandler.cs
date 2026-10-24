namespace Hexalith.Parties.Tests.Search;

/// <summary>Forwards one real accepted producer request, then loses only its acknowledgement.</summary>
internal sealed class PartyMemoryLostAcknowledgementHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    private int _lost;
    private int _submissions;

    internal int Submissions => _submissions;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/api/v1/ingest", StringComparison.Ordinal))
        { Interlocked.Increment(ref _submissions); }
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/api/v1/ingest", StringComparison.Ordinal)
            && response.IsSuccessStatusCode && Interlocked.Exchange(ref _lost, 1) == 0)
        {
            response.Dispose();
            throw new HttpRequestException("Synthetic lost producer acknowledgement after real acceptance.");
        }
        return response;
    }
}
