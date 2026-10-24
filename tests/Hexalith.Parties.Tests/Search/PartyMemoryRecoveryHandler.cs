using System.Net;
using System.Net.Http.Json;

using Hexalith.Memories.Contracts.V1;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Controllable published workflow and delayed deletion routes for recovery tests.</summary>
internal sealed class PartyMemoryRecoveryHandler : HttpMessageHandler
{
    public Dictionary<string, IngestionWorkflowStatus> Workflows { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Units { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, (HttpStatusCode Status, string Body)> WorkflowErrors { get; } = new(StringComparer.Ordinal);

    public bool DelayDeletion { get; set; }

    public int DeleteStatus { get; set; } = 204;

    public string AbsenceBody { get; set; } = "{\"code\":\"MEMORY_UNIT_NOT_FOUND\"}";

    public List<(HttpMethod Method, string Path)> Requests { get; } = [];

    public Func<HttpRequestMessage, CancellationToken, Task>? BeforeRequest { get; set; }

    public List<(HttpMethod Method, string Path)> InventoryRequests { get; } = [];

    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? InventoryRequest { get; set; }

    public bool InventoryComplete { get; set; } = true;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("/memory-units/by-source-uri", StringComparison.Ordinal))
        {
            return new(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { code = "MEMORY_UNIT_NOT_FOUND" }) };
        }
        if (request.RequestUri!.AbsolutePath.Contains("/source-inventory", StringComparison.Ordinal))
        {
            InventoryRequests.Add((request.Method, request.RequestUri.PathAndQuery));
            if (InventoryRequest is not null) { return await InventoryRequest(request, cancellationToken).ConfigureAwait(false); }
            if (request.Method == HttpMethod.Post) { return new(HttpStatusCode.NoContent); }
            string tenant = Uri.UnescapeDataString(request.RequestUri.AbsolutePath.Split("/tenants/", StringSplitOptions.None)[1].Split('/')[0]);
            string source = Uri.UnescapeDataString(request.RequestUri.Query.Split("sourceUri=", StringSplitOptions.None)[1].Split('&')[0]);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                tenantId = tenant, sourceUri = source, version = "synthetic-complete-inventory", coverageComplete = InventoryComplete,
                admissionClosed = true, permanent = true, entries = Array.Empty<object>(), nextCursor = (string?)null,
            }) };
        }
        if (BeforeRequest is not null)
        {
            await BeforeRequest(request, cancellationToken).ConfigureAwait(false);
        }

        string path = request.RequestUri!.AbsolutePath;
        Requests.Add((request.Method, path));
        string id = Uri.UnescapeDataString(path.Split('/')[^1]);
        if (path.Contains("/ingest/", StringComparison.Ordinal))
        {
            if (WorkflowErrors.TryGetValue(id, out (HttpStatusCode Status, string Body) error))
            {
                return new(error.Status) { Content = new StringContent(error.Body, System.Text.Encoding.UTF8, "application/json") };
            }

            return Workflows.TryGetValue(id, out IngestionWorkflowStatus? status)
                ? new(HttpStatusCode.OK) { Content = JsonContent.Create(status) }
                : new(HttpStatusCode.NotFound);
        }

        if (request.Method == HttpMethod.Delete)
        {
            if (!DelayDeletion && DeleteStatus is >= 200 and < 300)
            {
                Units.Remove(id);
            }

            return new((HttpStatusCode)DeleteStatus);
        }

        return Units.Contains(id)
            ? new(HttpStatusCode.OK) { Content = JsonContent.Create(new { id }) }
            : new(HttpStatusCode.NotFound) { Content = new StringContent(AbsenceBody, System.Text.Encoding.UTF8, "application/json") };
    }

    public static IngestionWorkflowStatus Completed(string instance = "workflow", string canonical = "canonical", string tenant = "tenant", string caseId = "case")
        => new(instance, tenant, caseId, "Completed", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, canonical, MemoryUnitStatus.Indexed, null);
}
