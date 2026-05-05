using System.Text;

using Hexalith.Memories.Client.Rest;
using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.CommandApi.Search;

internal sealed record PartyMemoryIndexingResult(
    string PartyId,
    string SourceUri,
    string? WorkflowInstanceId,
    bool Indexed,
    string? FailureReason);

/// <remarks>
/// <b>Memory-unit-id contract (P40 / resolved decision D1).</b>
/// <see cref="MemoriesClient.IngestAsync"/> returns the DAPR workflow instance id, and the
/// Memories server reuses that id as the canonical <c>memoryUnitId</c> for the resulting
/// memory unit (verified against
/// <c>Hexalith.Memories.Server.Workflows.IngestionWorkflow.ResolveMemoryUnitId</c>). Recording
/// the workflow instance id as <c>memoryUnitId</c> in <see cref="IPartyMemoryUnitMappingStore"/>
/// is therefore correct by design — the per-unit cleanup endpoint uses the same id. The only
/// divergence path (<c>SourceType.Event</c> with the dedup-instance prefix) does not apply
/// because the SDK hard-codes <c>SourceType.File</c>.
/// </remarks>
internal sealed class PartyMemoryIndexingService(
    MemoriesClient memoriesClient,
    IPartyMemoryUnitMappingStore mappingStore,
    IOptionsMonitor<PartyMemorySearchOptions> options,
    ILogger<PartyMemoryIndexingService> logger)
{
    public async Task<PartyMemoryIndexingResult?> IndexAsync(
        PartyIndexEntry entry,
        PartyMemoryUnitMappingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(context);

        PartyMemoryUnit? unit = PartyMemoryUnitMapper.Map(entry, context);
        if (unit is null)
        {
            return null;
        }

        byte[] content = Encoding.UTF8.GetBytes(unit.Content);

        string? workflowInstanceId = null;
        try
        {
            // HXL001 marks `IngestAsync` obsolete on the Memories SDK. The replacement
            // (`IndexAsync` family) is not yet available for the per-party flow Parties
            // requires here. Suppress until the SDK exposes a non-deprecated equivalent.
#pragma warning disable HXL001
            workflowInstanceId = await memoriesClient
                .IngestAsync(
                    unit.TenantId,
                    unit.CaseId,
                    unit.SourceUri,
                    content,
                    "text/plain",
                    "Hexalith.Parties",
                    unit.Metadata,
                    cancellationToken)
                .ConfigureAwait(false);
#pragma warning restore HXL001
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            // Indexing must be best-effort for genuine transient failures (transport
            // errors, timeouts) so a Memories outage cannot abort the projection delivery
            // pipeline. Programming-bug indicators like InvalidOperationException are NOT
            // caught here — they should surface as 500 so the underlying defect is fixed
            // rather than masked as transient outage.
            logger.LogWarning(
                ex,
                "Memories indexing failed for {TenantId}/{PartyId} (source URI {SourceUri}). Search may be stale until repair runs.",
                unit.TenantId,
                unit.PartyId,
                unit.SourceUri);
            return new PartyMemoryIndexingResult(
                unit.PartyId,
                unit.SourceUri,
                WorkflowInstanceId: null,
                Indexed: false,
                FailureReason: $"{ex.GetType().Name}: {ex.Message}");
        }

        // P18: A misbehaving server returning a 2xx with empty/whitespace instanceId would
        // otherwise crash the mapping store's ArgumentException guard and surface as 500 to
        // the projection pipeline. Treat as a non-transient indexing failure with a clear
        // reason so the operator can investigate the Memories upgrade rather than chasing
        // a generic 500.
        if (string.IsNullOrWhiteSpace(workflowInstanceId))
        {
            logger.LogWarning(
                "Memories returned a successful response with no workflow/memory-unit id for {TenantId}/{PartyId} (source URI {SourceUri}). Mapping not recorded; search may be stale.",
                unit.TenantId,
                unit.PartyId,
                unit.SourceUri);
            return new PartyMemoryIndexingResult(
                unit.PartyId,
                unit.SourceUri,
                WorkflowInstanceId: null,
                Indexed: false,
                FailureReason: "Memories returned an empty workflowInstanceId for a successful ingest.");
        }

        // P2: Record the per-party → memory-unit-id mapping so erasure cleanup can iterate
        // per-unit DELETEs against the existing per-unit Memories endpoint. If the mapping
        // write fails (state-store outage, ETag retry-budget exhausted), compensate by
        // deleting the just-ingested memory unit so we do not leak an orphan that AC5
        // erasure would later miss.
        try
        {
            await mappingStore
                .RecordMappingAsync(unit.TenantId, unit.PartyId, workflowInstanceId, unit.SourceUri, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            await TryCompensatingDeleteAsync(unit, workflowInstanceId, ex, cancellationToken).ConfigureAwait(false);
            return new PartyMemoryIndexingResult(
                unit.PartyId,
                unit.SourceUri,
                WorkflowInstanceId: workflowInstanceId,
                Indexed: false,
                FailureReason: $"Mapping record failed ({ex.GetType().Name}: {ex.Message}). Compensating delete attempted.");
        }

        return new PartyMemoryIndexingResult(
            unit.PartyId,
            unit.SourceUri,
            workflowInstanceId,
            Indexed: true,
            FailureReason: null);
    }

    private async Task TryCompensatingDeleteAsync(
        PartyMemoryUnit unit,
        string workflowInstanceId,
        Exception originalFailure,
        CancellationToken cancellationToken)
    {
        try
        {
            PartyMemorySearchOptions current = options.CurrentValue;
            if (current.Endpoint is null || string.IsNullOrWhiteSpace(current.CaseId))
            {
                logger.LogError(
                    originalFailure,
                    "Compensating delete for orphaned Memories unit {WorkflowInstanceId} (tenant {TenantId}, party {PartyId}) skipped: Memories endpoint or case id not configured. Operator must run reconciliation manually.",
                    workflowInstanceId,
                    unit.TenantId,
                    unit.PartyId);
                return;
            }

            using HttpClient httpClient = new() { BaseAddress = current.Endpoint };
            PartyMemoryCleanupService.ConfigureAuthorization(httpClient, current.ApiToken, logger);
            string path = $"api/tenants/{Uri.EscapeDataString(unit.TenantId)}/cases/{Uri.EscapeDataString(unit.CaseId)}/memory-units/{Uri.EscapeDataString(workflowInstanceId)}";
            using HttpResponseMessage response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning(
                    "Compensating delete succeeded for orphaned Memories unit {WorkflowInstanceId} after mapping write failed.",
                    workflowInstanceId);
                return;
            }

            logger.LogError(
                originalFailure,
                "Compensating delete returned HTTP {StatusCode} for orphaned Memories unit {WorkflowInstanceId} (tenant {TenantId}, party {PartyId}). Operator must run reconciliation manually.",
                (int)response.StatusCode,
                workflowInstanceId,
                unit.TenantId,
                unit.PartyId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception compensateFailure) when (compensateFailure is not OutOfMemoryException and not StackOverflowException)
        {
            logger.LogError(
                compensateFailure,
                "Compensating delete threw for orphaned Memories unit {WorkflowInstanceId} (tenant {TenantId}, party {PartyId}). Operator must run reconciliation manually. Original mapping failure: {OriginalFailure}",
                workflowInstanceId,
                unit.TenantId,
                unit.PartyId,
                originalFailure.Message);
        }
    }
}
