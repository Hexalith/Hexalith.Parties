using System.Net;
using System.Net.Http.Json;

using Hexalith.Memories.Contracts.V1;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Provides stable remote route responses while the health check qualifies real SDK storage.</summary>
internal sealed class MemoriesHealthRuntimeHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath.Equals("/api/v1/search", StringComparison.Ordinal))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new HybridSearchResult { Results = [], TotalCount = 0, Degraded = false, UnavailableAxes = [], Query = "healthcheck" }),
            });
        }
        string code = request.RequestUri!.AbsolutePath.Contains("/ingest/", StringComparison.Ordinal)
            ? "INGESTION_STATUS_NOT_FOUND" : "MEMORY_UNIT_NOT_FOUND";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { code }) });
    }
}
