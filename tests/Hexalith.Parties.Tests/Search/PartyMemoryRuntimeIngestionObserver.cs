namespace Hexalith.Parties.Tests.Search;

using System.Collections.Concurrent;
using System.Text.Json;

/// <summary>Retains only content-free source mode/revision evidence for the actual runtime wire request.</summary>
internal sealed class PartyMemoryRuntimeIngestionObserver(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    private readonly ConcurrentQueue<Observation> _observations = new();
    internal IReadOnlyList<Observation> Observations => _observations.ToArray();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/ingest", StringComparison.Ordinal) && request.Content is not null)
        {
            using JsonDocument payload = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            bool refresh = payload.RootElement.TryGetProperty("refreshExistingSource", out JsonElement flag) && flag.GetBoolean();
            long revision = payload.RootElement.TryGetProperty("sourceRevisionNumber", out JsonElement number) ? number.GetInt64() : 0;
            _observations.Enqueue(new(refresh, revision));
        }
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
    internal sealed record Observation(bool RefreshExistingSource, long SourceRevisionNumber);
}
