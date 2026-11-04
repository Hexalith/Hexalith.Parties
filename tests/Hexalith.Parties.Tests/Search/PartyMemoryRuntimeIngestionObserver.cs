namespace Hexalith.Parties.Tests.Search;

using System.Collections.Concurrent;
using System.Text.Json;

/// <summary>Retains only content-free source mode/revision evidence for the actual runtime wire request.</summary>
internal sealed class PartyMemoryRuntimeIngestionObserver(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    private readonly ConcurrentQueue<PartyMemoryRuntimeIngestionObservation> _observations = new();
    internal IReadOnlyList<PartyMemoryRuntimeIngestionObservation> Observations => _observations.ToArray();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/ingest", StringComparison.Ordinal) && request.Content is not null)
        {
            using JsonDocument payload = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            bool refresh = payload.RootElement.TryGetProperty("refreshExistingSource", out JsonElement flag) && flag.GetBoolean();
            long revision = payload.RootElement.TryGetProperty("sourceRevisionNumber", out JsonElement number) ? number.GetInt64() : 0;
            string? source = payload.RootElement.TryGetProperty("sourceUri", out JsonElement uri) ? uri.GetString() : null;
            _observations.Enqueue(new(refresh, revision, source));
        }
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
